using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ContextUi;
using LifeSim.Household;

namespace LifeSim.Cooking;

/// <summary>
/// Ponto de entrada do protótipo. Anexe este script a um Control raiz de uma cena e rode.
///
/// Reproduz o fluxo pretendido: clicar no fogão abre o MENU RÁPIDO; o painel manual está
/// atrás do verbo "Preparar manualmente…" e volta pelo ✕. Os dois são o mesmo
/// <see cref="ContextPanel"/> com definições diferentes.
///
/// É aqui que mora o estado de tela — qual contexto está aberto, qual prato está marcado no
/// menu. O painel não guarda nada disso de propósito: no jogo real quem abre o contexto é a
/// interação com o eletrodoméstico, e é ela que sabe o que o Sim estava fazendo.
/// </summary>
public partial class CookingDemo : Control
{
    [Export] public int CookingLevel { get; set; } = 6;

    private List<BaseItemDef> _bases;
    private List<RecipeAnchor> _anchors;

    /// <summary>
    /// A casa do Sim, viva enquanto o protótipo roda: cozinha, geladeira, o Sim e o relógio.
    /// Uma só — preparar e comer precisam acontecer sobre a mesma despensa e a mesma geladeira.
    /// </summary>
    private Household.Household _home;
    private CookingSession Kitchen => _home.Kitchen;

    private enum View { QuickMenu, Manual, Fridge }

    /// <summary>
    /// Qual contexto está aberto. Estado de tela sem dono no modelo, por isso mora aqui. O menu
    /// rápido precisa ser replanejado quando o estoque muda por fora dele.
    /// </summary>
    private View _view;

    /// <summary>A refeição marcada na geladeira, e a frase do que aconteceu na última porção.</summary>
    private Meal _selectedMeal;
    private string _lastEat;

    private List<QuickMealOption> _options;
    private QuickMealOption _selected;

    private Func<PanelContext> _definition;
    private ContextPanel _panel;
    private PanelSkin _skin = PanelSkin.Dark;
    private bool _showRegionLabels;

    public override void _Ready()
    {
        _bases = ContentLibrary.Bases();
        _anchors = ContentLibrary.Anchors();

        var kitchen = new CookingSession(ContentLibrary.StartingPantry(), CookingLevel);
        kitchen.SetBase(_bases[0]);
        _home = new Household.Household(kitchen, new Sim("Ana", hunger: 45f, thirst: 50f));
        _home.Changed += () => _panel?.Rebuild();

        OpenQuickMenu();
    }

    // ------------------------------------------------------------------
    // Contextos
    // ------------------------------------------------------------------

    private void OpenQuickMenu()
    {
        // Sair do painel manual devolve à despensa o que ficou no recipiente, antes de
        // planejar: o menu precisa contar com esses ingredientes de volta.
        Kitchen.Clear();

        // No jogo real isto vem do Sim: as âncoras que ele já descobriu.
        var known = _anchors.Select(a => a.DishName).ToList();

        _view = View.QuickMenu;
        _options = QuickMealPlanner.Plan(Kitchen.Pantry, _bases, _anchors, known, CookingLevel, Kitchen.PickOrder);
        _selected = _options[0];
        _definition = () => QuickMealContext.Build(
            Kitchen.Pantry, _options, _selected, Select, PrepareQuick, OpenManualPanel,
            OpenFridge, _home.Meals.Count);

        Render();
    }

    private void OpenManualPanel()
    {
        _view = View.Manual;
        Kitchen.Clear();
        _definition = () => CookingContext.Build(Kitchen, _bases, _anchors, Cook, OpenQuickMenu);
        Render();
    }

    private void Select(QuickMealOption option)
    {
        _selected = option;
        _panel.Rebuild();
    }

    /// <summary>
    /// A geladeira. A refeição marcada é revalidada a cada redesenho: comer a última porção
    /// tira a refeição do mundo, e o painel não pode desenhar um prato que não existe mais.
    /// </summary>
    private void OpenFridge()
    {
        _view = View.Fridge;
        Kitchen.Clear();
        _selectedMeal = null;
        _lastEat = null;
        _definition = () => FridgeContext.Build(_home, CurrentMeal(), SelectMeal, EatSelected, OpenQuickMenu, _lastEat);
        Render();
    }

    private Meal CurrentMeal()
    {
        if (_selectedMeal is null || !_home.Meals.Contains(_selectedMeal))
            _selectedMeal = _home.Meals.FirstOrDefault();
        return _selectedMeal;
    }

    private void SelectMeal(Meal meal)
    {
        _selectedMeal = meal;
        _lastEat = null;
        _panel.Rebuild();
    }

    private void EatSelected()
    {
        var result = _home.Eat(CurrentMeal());
        _lastEat = result.Poisoned
            ? $"{_home.Sim.Name} passou mal: \"{result.Moodlet.Name}\"."
            : $"{_home.Sim.Name} comeu {result.MealName}: fome +{result.HungerGained:0}, \"{result.Moodlet.Name}\".";
        GD.Print($"[Comer] {_lastEat}");
        Render();
    }

    private void PrepareQuick()
    {
        Report("Rápido", _home.Store(QuickMealPlanner.Prepare(Kitchen, _selected, _anchors)));

        // A despensa desceu: as opções precisam ser replanejadas, porque custo, qualidade
        // prevista e disponibilidade saem do estoque.
        OpenQuickMenu();
    }

    private void Cook() => Report("Manual", _home.Store(Kitchen.Cook(_anchors)));

    /// <summary>O prato virou refeição na geladeira. O console só registra; o modelo não imprime nada.</summary>
    private static void Report(string source, Meal meal) =>
        GD.Print($"[{source}] {meal.Dish.Name} — qualidade {meal.Dish.Quality:P0}, " +
                 $"{meal.TotalServings} porções para a geladeira");

    // ------------------------------------------------------------------
    // Tela
    // ------------------------------------------------------------------

    private void Render()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _panel = new ContextPanel
        {
            Definition = _definition,
            Skin = _skin,
            ShowRegionLabels = _showRegionLabels,
        };

        var screen = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        screen.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = _skin.Background,
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 24,
            ContentMarginBottom = 24,
        });
        AddChild(screen);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        screen.AddChild(column);

        column.AddChild(DevBar());
        column.AddChild(_panel);
    }

    /// <summary>Barra de autoria: trocar a pele e etiquetar as regiões do shell.</summary>
    private Control DevBar()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(PanelPrimitives.Text("Dev", 10, _skin.Mute));
        row.AddChild(DevButton(_skin == PanelSkin.Dark ? "Pele · escura" : "Pele · clara", ToggleSkin));
        row.AddChild(DevButton(_showRegionLabels ? "Regiões · visíveis" : "Regiões · ocultas", ToggleRegionLabels));
        row.AddChild(PanelPrimitives.Text(
            $"Dia {_home.Day} · {_home.HourOfDay:00}h  ·  {_home.Sim.Name}: fome {_home.Sim.Hunger:0}, " +
            $"sede {_home.Sim.Thirst:0}, humor {_home.Sim.Mood:+0;-0;0}", 10, _skin.Mute));
        row.AddChild(DevButton("+4 h", () => PassTime(4f)));
        row.AddChild(DevButton("+1 dia", () => PassTime(24f)));
        return row;
    }

    private Button DevButton(string text, Action onPress)
    {
        var button = PanelPrimitives.FlatButton(_skin, _skin.Panel, _skin.Line);
        button.Text = text;
        button.AddThemeFontSizeOverride("font_size", 11);
        button.Pressed += () => onPress();
        return button;
    }

    private void PassTime(float hours)
    {
        _home.AdvanceHours(hours);

        // Custo, qualidade prevista e disponibilidade do menu saem do estoque, que acabou de
        // envelhecer. Os outros contextos leem a casa a cada redesenho; Render só atualiza a
        // barra de dev com o relógio e as necessidades.
        if (_view == View.QuickMenu) OpenQuickMenu();
        else Render();
    }

    private void ToggleSkin()
    {
        _skin = _skin == PanelSkin.Dark ? PanelSkin.Light : PanelSkin.Dark;
        Render();
    }

    private void ToggleRegionLabels()
    {
        _showRegionLabels = !_showRegionLabels;
        Render();
    }
}
