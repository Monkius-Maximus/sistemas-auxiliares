using System.Collections.Generic;
using System.Linq;
using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// Ponto de entrada do protótipo. Anexe este script a um Control raiz de uma cena e rode.
///
/// Reproduz o fluxo pretendido no jogo: clicar no fogão abre o MENU RÁPIDO. O painel
/// completo só aparece se o jogador escolher "Preparar manualmente". No jogo real quem
/// faz esse papel é a interação com o eletrodoméstico.
/// </summary>
public partial class CookingDemo : Control
{
    [Export] public int CookingLevel { get; set; } = 6;

    private Dictionary<IngredientDef, int> _pantry;
    private List<BaseItemDef> _bases;
    private List<RecipeAnchor> _anchors;

    public override void _Ready()
    {
        _pantry = SampleContent.Pantry();
        _bases = SampleContent.Bases();
        _anchors = SampleContent.Anchors();
        ShowQuickMenu();
    }

    private void ShowQuickMenu()
    {
        ClearScreen();

        // No jogo real isto vem do Sim: as âncoras que ele já descobriu.
        var known = _anchors.Select(a => a.DishName).ToList();

        var menu = new QuickMealMenu
        {
            Options = QuickMealPlanner.Plan(_pantry, _bases, _anchors, known, CookingLevel),
            OnChoose = PrepareQuick,
            OnManual = ShowFullPanel,
            OffsetLeft = 24, OffsetTop = 24,
        };
        AddChild(menu);
    }

    private void PrepareQuick(QuickMealOption option)
    {
        var session = new CookingSession(_pantry, CookingLevel);
        var dish = QuickMealPlanner.Prepare(session, option, _anchors);
        GD.Print($"[Rápido] {dish.Name} — qualidade {dish.Quality:P0}, custo {dish.Cost}, " +
                 $"{dish.Calories:0} kcal, moodlet {dish.MoodletMinutes:0} min");
    }

    private void ShowFullPanel()
    {
        ClearScreen();

        var panel = new CookingPanel
        {
            AnchorRight = 1, AnchorBottom = 1,
            OffsetLeft = 16, OffsetTop = 16,
            OffsetRight = -16, OffsetBottom = -16,
        };
        panel.Setup(_pantry, _bases, _anchors, CookingLevel);
        AddChild(panel);
    }

    private void ClearScreen()
    {
        foreach (var child in GetChildren())
            child.QueueFree();
    }
}
