using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ContextUi;
using LifeSim.Household;

namespace LifeSim.Cooking;

/// <summary>
/// As interações da casa: que telas existem e o que cada clique delas faz. É o estado de tela
/// que não tem dono no modelo — qual prato está marcado no menu, qual refeição na geladeira, o
/// carrinho aberto — num lugar só, para que o jogo e a bancada de testes abram as mesmas telas.
///
/// Quem empilha e desempilha é o <see cref="ContextNavigator"/>: abrir é <c>Push</c>, o ✕ de
/// toda tela é <c>Back</c>. A geladeira aberta pelo menu do fogão volta ao fogão, e aberta pelo
/// objeto da casa fecha, sem ninguém aqui precisar lembrar de onde veio.
/// </summary>
public sealed class HouseInteractions
{
    public HouseInteractions(Household.Household home, int cookingLevel, ContextNavigator navigator, bool canCloseStove)
    {
        Home = home ?? throw new ArgumentNullException(nameof(home));
        _nav = navigator ?? throw new ArgumentNullException(nameof(navigator));
        _cookingLevel = cookingLevel;
        _canCloseStove = canCloseStove;
        _bases = ContentLibrary.Bases();
        _anchors = ContentLibrary.Anchors();
        _vendors = ContentLibrary.Vendors();

        Home.Changed += _nav.Rebuild;
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

    private readonly ContextNavigator _nav;
    private readonly int _cookingLevel;
    private readonly List<BaseItemDef> _bases;
    private readonly List<RecipeAnchor> _anchors;
    private readonly List<VendorDef> _vendors;

    /// <summary>
    /// Na bancada o fogão é a tela de base e não fecha; no jogo, fechar o fogão devolve o
    /// jogador à casa.
    /// </summary>
    private readonly bool _canCloseStove;

    /// <summary>A tela do fogão aberta, para saber quando ela é a do topo.</summary>
    private ContextScreen _stove;

    private List<QuickMealOption> _options;
    private QuickMealOption _selected;

    private Meal _selectedMeal;
    private string _lastEat;

    /// <summary>A ida às compras aberta. Estado da interação, mas do modelo: o carrinho é de verdade.</summary>
    private ShoppingTrip _trip;

    // ------------------------------------------------------------------
    // Telas
    // ------------------------------------------------------------------

    /// <summary>Clicar no fogão: o menu rápido, porta de entrada padrão do cozinhar.</summary>
    public void OpenStove()
    {
        Replan();
        _nav.Push(_stove = new ContextScreen
        {
            Title = "Fogão",
            Definition = () => QuickMealContext.Build(
                Kitchen.Pantry, _options, _selected, Select, PrepareQuick, OpenManual,
                OpenFridge, Home.Meals.Count, OpenShop,
                _canCloseStove ? _nav.Back : null),
            // Voltar da loja ou do painel manual: a despensa mudou, e custo, qualidade prevista e
            // disponibilidade do menu saem dela.
            OnReveal = Replan,
        });
    }

    private void OpenManual() => _nav.Push(new ContextScreen
    {
        Title = "Preparar manualmente",
        Definition = () => CookingContext.Build(Kitchen, _bases, _anchors, Cook, _nav.Back),
        // O que ficou no recipiente volta para a despensa ao sair.
        OnLeave = Kitchen.Clear,
    });

    /// <summary>
    /// A geladeira. A refeição marcada é revalidada a cada redesenho: comer a última porção
    /// tira a refeição do mundo, e o painel não pode desenhar um prato que não existe mais.
    /// </summary>
    public void OpenFridge()
    {
        _selectedMeal = null;
        _lastEat = null;
        _nav.Push(new ContextScreen
        {
            Title = "Geladeira",
            Definition = () => FridgeContext.Build(Home, CurrentMeal(), SelectMeal, EatSelected, _nav.Back, _lastEat),
        });
    }

    /// <summary>
    /// A loja. Abre no primeiro vendedor aberto agora — às 23h, ninguém quer cair na porta
    /// fechada do mercado. Sair descarta o carrinho: nada é cobrado antes de comprar.
    /// </summary>
    public void OpenShop()
    {
        var vendor = _vendors.FirstOrDefault(v => v.IsOpenAt(Home.HourOfDay) && !v.AlwaysOpen)
                     ?? _vendors.FirstOrDefault(v => v.IsOpenAt(Home.HourOfDay))
                     ?? _vendors[0];
        _trip = new ShoppingTrip(vendor);
        _trip.Changed += _nav.Rebuild;
        _nav.Push(new ContextScreen
        {
            Title = "Mercearia",
            Definition = () => ShopContext.Build(Home, _trip, _vendors, _anchors, BuyCart, _nav.Back),
            OnLeave = () => _trip = null,
        });
    }

    /// <summary>
    /// O tempo passou com uma tela aberta (só acontece na bancada, onde o tempo anda pelos
    /// botões). O menu rápido é o único que guarda algo derivado do estoque.
    /// </summary>
    public void TimePassed()
    {
        if (_nav.Top == _stove) Replan();
        _nav.Rebuild();
    }

    // ------------------------------------------------------------------
    // Ações das telas
    // ------------------------------------------------------------------

    /// <summary>
    /// Refaz as opções do menu a partir da despensa, mantendo marcado o prato que estava — o
    /// jogador que acabou de comprar o que faltava quer ver o mesmo prato, agora disponível.
    /// </summary>
    private void Replan()
    {
        // No jogo real isto vem do Sim: as âncoras que ele já descobriu.
        var known = _anchors.Select(a => a.DishName).ToList();
        var marked = _selected?.Anchor;
        _options = QuickMealPlanner.Plan(Kitchen.Pantry, _bases, _anchors, known, _cookingLevel, Kitchen.PickOrder);
        _selected = _options.FirstOrDefault(o => o.Anchor == marked) ?? _options[0];
    }

    private void Select(QuickMealOption option)
    {
        _selected = option;
        _nav.Rebuild();
    }

    private void PrepareQuick()
    {
        var meal = Home.Store(QuickMealPlanner.Prepare(Kitchen, _selected, _anchors));
        Report("Rápido", meal);

        // A despensa desceu: as opções precisam ser replanejadas, porque custo, qualidade
        // prevista e disponibilidade saem do estoque. Store já pediu redesenho, mas com as
        // opções velhas — por isso o segundo.
        Replan();
        _nav.Rebuild();
    }

    private void Cook() => Report("Manual", Home.Store(Kitchen.Cook(_anchors)));

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
        _nav.Rebuild();
    }

    private void EatSelected()
    {
        var result = Home.Eat(CurrentMeal());
        _lastEat = result.Poisoned
            ? $"{Home.Sim.Name} passou mal: \"{result.Moodlet.Name}\"."
            : $"{Home.Sim.Name} comeu {result.MealName}: fome +{result.HungerGained:0}, \"{result.Moodlet.Name}\".";
        GD.Print($"[Comer] {_lastEat}");
        _nav.Rebuild();
    }

    private void BuyCart() => Home.Buy(_trip);

    /// <summary>O prato virou refeição na geladeira. O console só registra; o modelo não imprime nada.</summary>
    private static void Report(string source, Meal meal) =>
        GD.Print($"[{source}] {meal.Dish.Name} — qualidade {meal.Dish.Quality:P0}, " +
                 $"{meal.TotalServings} porções para a geladeira");
}
