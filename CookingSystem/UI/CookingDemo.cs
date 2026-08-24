using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ContextUi;

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

    private Dictionary<IngredientDef, int> _pantry;
    private List<BaseItemDef> _bases;
    private List<RecipeAnchor> _anchors;

    private List<QuickMealOption> _options;
    private QuickMealOption _selected;

    private Func<PanelContext> _definition;
    private ContextPanel _panel;
    private PanelSkin _skin = PanelSkin.Dark;
    private bool _showRegionLabels;

    public override void _Ready()
    {
        _pantry = ContentLibrary.StartingPantry();
        _bases = ContentLibrary.Bases();
        _anchors = ContentLibrary.Anchors();
        OpenQuickMenu();
    }

    // ------------------------------------------------------------------
    // Contextos
    // ------------------------------------------------------------------

    private void OpenQuickMenu()
    {
        // No jogo real isto vem do Sim: as âncoras que ele já descobriu.
        var known = _anchors.Select(a => a.DishName).ToList();

        _options = QuickMealPlanner.Plan(_pantry, _bases, _anchors, known, CookingLevel);
        _selected = _options[0];
        _definition = () => QuickMealContext.Build(
            _pantry, _options, _selected, Select, PrepareQuick, OpenManualPanel);

        Render();
    }

    private void OpenManualPanel()
    {
        var session = new CookingSession(_pantry, CookingLevel);
        session.SetBase(_bases[0]);
        session.Changed += () => _panel.Rebuild();

        _definition = () => CookingContext.Build(session, _bases, _anchors, () => Cook(session), OpenQuickMenu);
        Render();
    }

    private void Select(QuickMealOption option)
    {
        _selected = option;
        _panel.Rebuild();
    }

    private void PrepareQuick() =>
        Report("Rápido", QuickMealPlanner.Prepare(new CookingSession(_pantry, CookingLevel), _selected, _anchors));

    private void Cook(CookingSession session) => Report("Manual", session.Cook(_anchors));

    /// <summary>
    /// Fim de linha do protótipo. Enquanto o prato não virar item do mundo, o resultado
    /// sai no console — e sai daqui, da UI, para o modelo continuar sem <c>GD.Print</c>.
    /// </summary>
    private static void Report(string source, CookedDish dish) =>
        GD.Print($"[{source}] {dish.Name} — qualidade {dish.Quality:P0}, custo {dish.Cost}, " +
                 $"{dish.Calories:0} kcal, moodlet {dish.MoodletMinutes:0} min");

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
