using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ContextUi;
using LifeSim.Household;

namespace LifeSim.Cooking;

/// <summary>
/// As interações da casa: qual contexto do painel está aberto e o que cada clique dele faz.
/// É o estado de tela que não tem dono no modelo — qual prato está marcado no menu, qual
/// refeição na geladeira, o carrinho aberto — num lugar só, para que o jogo e a ferramenta de
/// dev abram exatamente as mesmas telas.
///
/// Não desenha nada. Quem hospeda escuta três eventos: <see cref="Opened"/> (outro contexto:
/// criar um painel novo), <see cref="Changed"/> (mesmo contexto: redesenhar) e
/// <see cref="Closed"/> (nenhum contexto aberto).
/// </summary>
public sealed class HouseInteractions
{
    public HouseInteractions(Household.Household home, int cookingLevel, bool canCloseStove)
    {
        Home = home ?? throw new ArgumentNullException(nameof(home));
        _cookingLevel = cookingLevel;
        _canCloseStove = canCloseStove;
        _bases = ContentLibrary.Bases();
        _anchors = ContentLibrary.Anchors();
        _vendors = ContentLibrary.Vendors();

        Home.Changed += () => { if (IsOpen) Changed?.Invoke(); };
    }

    /// <summary>A casa de um jogo novo: a cozinha do demo, Ana, e o livro de receitas inteiro.</summary>
    public static Household.Household NewHousehold(int cookingLevel)
    {
        var bases = ContentLibrary.Bases();
        var anchors = ContentLibrary.Anchors();

        var kitchen = new CookingSession(ContentLibrary.StartingPantry(), cookingLevel);
        kitchen.SetBase(bases[0]);
        // O livro de receitas da autonomia: as mesmas âncoras do menu rápido. No jogo real, só
        // as que o Sim já descobriu.
        var cookbook = new Cookbook(bases, anchors, anchors.Select(a => a.DishName).ToList());
        return new Household.Household(kitchen, new Sim("Ana", hunger: 45f, thirst: 50f), cookbook);
    }

    public Household.Household Home { get; }
    private CookingSession Kitchen => Home.Kitchen;

    public event Action Opened;
    public event Action Changed;
    public event Action Closed;

    /// <summary>De onde o painel aberto tira a definição. Null quando nada está aberto.</summary>
    public Func<PanelContext> Definition { get; private set; }
    public bool IsOpen => Definition is not null;

    private enum View { Stove, Manual, Fridge, Shop }
    private View _view;

    private readonly int _cookingLevel;
    private readonly List<BaseItemDef> _bases;
    private readonly List<RecipeAnchor> _anchors;
    private readonly List<VendorDef> _vendors;

    /// <summary>
    /// Na ferramenta de dev o fogão é a tela inteira e não fecha; no jogo, fechar o fogão
    /// devolve o jogador à casa.
    /// </summary>
    private readonly bool _canCloseStove;

    /// <summary>
    /// Geladeira e loja abertas pelo menu do fogão voltam para ele ao fechar; abertas direto
    /// pelo objeto da casa, fecham. O jogador volta para onde estava.
    /// </summary>
    private bool _fromStove;

    private List<QuickMealOption> _options;
    private QuickMealOption _selected;

    private Meal _selectedMeal;
    private string _lastEat;

    /// <summary>A ida às compras aberta. Estado da interação, mas do modelo: o carrinho é de verdade.</summary>
    private ShoppingTrip _trip;

    // ------------------------------------------------------------------
    // Abrir e fechar
    // ------------------------------------------------------------------

    /// <summary>Clicar no fogão: o menu rápido, porta de entrada padrão do cozinhar.</summary>
    public void OpenStove()
    {
        // Sair do painel manual devolve à despensa o que ficou no recipiente, antes de
        // planejar: o menu precisa contar com esses ingredientes de volta.
        Kitchen.Clear();

        // No jogo real isto vem do Sim: as âncoras que ele já descobriu.
        var known = _anchors.Select(a => a.DishName).ToList();

        _view = View.Stove;
        _fromStove = false;
        _options = QuickMealPlanner.Plan(Kitchen.Pantry, _bases, _anchors, known, _cookingLevel, Kitchen.PickOrder);
        _selected = _options[0];
        Open(() => QuickMealContext.Build(
            Kitchen.Pantry, _options, _selected, Select, PrepareQuick, OpenManual,
            () => OpenFridge(fromStove: true), Home.Meals.Count, () => OpenShop(fromStove: true),
            _canCloseStove ? Close : null));
    }

    private void OpenManual()
    {
        _view = View.Manual;
        Kitchen.Clear();
        Open(() => CookingContext.Build(Kitchen, _bases, _anchors, Cook, OpenStove));
    }

    /// <summary>
    /// A geladeira. A refeição marcada é revalidada a cada redesenho: comer a última porção
    /// tira a refeição do mundo, e o painel não pode desenhar um prato que não existe mais.
    /// </summary>
    public void OpenFridge(bool fromStove = false)
    {
        _view = View.Fridge;
        _fromStove = fromStove;
        Kitchen.Clear();
        _selectedMeal = null;
        _lastEat = null;
        Open(() => FridgeContext.Build(Home, CurrentMeal(), SelectMeal, EatSelected, Back, _lastEat));
    }

    /// <summary>
    /// A loja. Abre no primeiro vendedor aberto agora — às 23h, ninguém quer cair na porta
    /// fechada do mercado. Fechar descarta o carrinho: nada é cobrado antes de comprar.
    /// </summary>
    public void OpenShop(bool fromStove = false)
    {
        _view = View.Shop;
        _fromStove = fromStove;
        Kitchen.Clear();
        var vendor = _vendors.FirstOrDefault(v => v.IsOpenAt(Home.HourOfDay) && !v.AlwaysOpen)
                     ?? _vendors.FirstOrDefault(v => v.IsOpenAt(Home.HourOfDay))
                     ?? _vendors[0];
        _trip = new ShoppingTrip(vendor);
        _trip.Changed += () => Changed?.Invoke();
        Open(() => ShopContext.Build(Home, _trip, _vendors, _anchors, BuyCart, Back));
    }

    /// <summary>Nada aberto. O que estava no recipiente volta para a despensa.</summary>
    public void Close()
    {
        // A definição sai antes de esvaziar a cozinha: Clear dispara Changed, e um painel
        // ainda ouvindo redesenharia uma definição que acabou de deixar de valer.
        Definition = null;
        _trip = null;
        Kitchen.Clear();
        Closed?.Invoke();
    }

    /// <summary>
    /// O tempo passou com um contexto aberto. O menu rápido é o único que guarda algo derivado
    /// do estoque — custo, qualidade prevista e disponibilidade — e precisa ser replanejado;
    /// os outros leem a casa a cada redesenho.
    /// </summary>
    public void TimePassed()
    {
        if (!IsOpen) return;
        if (_view == View.Stove) OpenStove();
        else Changed?.Invoke();
    }

    private void Open(Func<PanelContext> definition)
    {
        Definition = definition;
        Opened?.Invoke();
    }

    /// <summary>
    /// Fechar a geladeira ou a loja. Troca de contexto primeiro, solta o carrinho depois: abrir
    /// o menu esvazia a cozinha, o que redesenha o painel ainda com a definição da loja — sem
    /// carrinho, ela quebraria.
    /// </summary>
    private void Back()
    {
        if (_fromStove) OpenStove();
        else Close();
        _trip = null;
    }

    // ------------------------------------------------------------------
    // Ações dos contextos
    // ------------------------------------------------------------------

    private void Select(QuickMealOption option)
    {
        _selected = option;
        Changed?.Invoke();
    }

    private Meal CurrentMeal()
    {
        if (_selectedMeal is null || !Home.Meals.Contains(_selectedMeal))
            _selectedMeal = Home.Meals.FirstOrDefault();
        return _selectedMeal;
    }

    private void SelectMeal(Meal meal)
    {
        _selectedMeal = meal;
        _lastEat = null;
        Changed?.Invoke();
    }

    private void EatSelected()
    {
        var result = Home.Eat(CurrentMeal());
        _lastEat = result.Poisoned
            ? $"{Home.Sim.Name} passou mal: \"{result.Moodlet.Name}\"."
            : $"{Home.Sim.Name} comeu {result.MealName}: fome +{result.HungerGained:0}, \"{result.Moodlet.Name}\".";
        GD.Print($"[Comer] {_lastEat}");
        Changed?.Invoke();
    }

    private void BuyCart() => Home.Buy(_trip);

    private void PrepareQuick()
    {
        Report("Rápido", Home.Store(QuickMealPlanner.Prepare(Kitchen, _selected, _anchors)));

        // A despensa desceu: as opções precisam ser replanejadas, porque custo, qualidade
        // prevista e disponibilidade saem do estoque.
        OpenStove();
    }

    private void Cook() => Report("Manual", Home.Store(Kitchen.Cook(_anchors)));

    /// <summary>O prato virou refeição na geladeira. O console só registra; o modelo não imprime nada.</summary>
    private static void Report(string source, Meal meal) =>
        GD.Print($"[{source}] {meal.Dish.Name} — qualidade {meal.Dish.Quality:P0}, " +
                 $"{meal.TotalServings} porções para a geladeira");
}
