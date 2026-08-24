using System.Collections.Generic;
using System.Linq;
using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// Verificador de balanceamento. Roda o <see cref="DishEvaluator"/> REAL sobre casos de
/// referência e imprime a tabela de notas. Existe para que ajustar as constantes de tuning
/// não exija abrir o painel e clicar — e para que não exista uma segunda cópia da
/// matemática em algum script auxiliar.
///
/// Rodar: godot --headless --path . --script res://Tools/BalanceCheck.cs
/// (ou anexar a um Node em uma cena e dar play)
///
/// Distribuição-alvo: lixo ~0.05 · preguiçoso ~0.45 · sólido ~0.80 · excelente ~0.95
/// Se um caso sair muito fora da faixa comentada, ou o tuning regrediu ou o conteúdo mudou.
/// </summary>
public partial class BalanceCheck : SceneTree
{
    private const int Level = 10;

    /// <summary>caso => (recipiente, [(ingrediente, unidades)]), com a nota esperada.</summary>
    private static readonly (string Name, string Base, (string Id, int Units)[] Items, string Expected)[] Cases =
    {
        ("ovo puro, sem sal",        "frigideira", new[] { ("ovo", 20) },                                                          "lixo"),
        ("sopa de batata sem tempero","panela",    new[] { ("batata", 30) },                                                       "lixo"),
        ("sal demais",               "frigideira", new[] { ("ovo", 20), ("queijo", 8), ("sal", 12) },                              "lixo"),
        ("bacon + açúcar (choque)",  "frigideira", new[] { ("bacon", 15), ("acucar", 4), ("sal", 2) },                             "lixo"),
        ("só brócolis + sal",        "frigideira", new[] { ("brocolis", 25), ("sal", 2) },                                         "preguiçoso"),
        ("tudo junto (bagunça)",     "frigideira", new[] { ("ovo", 10), ("bacon", 10), ("acucar", 3), ("brocolis", 10), ("vinagre", 3), ("sal", 3) }, "preguiçoso"),
        ("bacon + ovo + sal",        "frigideira", new[] { ("ovo", 15), ("bacon", 10), ("sal", 2), ("pimenta", 1) },               "sólido"),
        ("omelete completa (âncora)","frigideira", new[] { ("ovo", 20), ("queijo", 8), ("cebolinha", 4), ("sal", 2), ("pimenta", 1) }, "excelente"),
        ("caldo verde",              "panela",     new[] { ("batata", 25), ("cogumelo", 10), ("cebolinha", 5), ("sal", 3), ("ervas", 2) }, "excelente"),
        ("caprese",                  "tigela",     new[] { ("tomate", 20), ("queijo", 10), ("ervas", 2), ("sal", 1) },             "sólido"),
    };

    public override void _Initialize()
    {
        var pantry = ContentLibrary.StartingPantry();
        var bases = ContentLibrary.Bases().ToDictionary(b => b.Id, b => b);
        var anchors = ContentLibrary.Anchors();
        var defs = pantry.Keys.ToDictionary(d => d.Id, d => d);

        GD.Print($"{"caso",-30}{"nota",6}{"temp",7}{"equi",7}{"harm",7}{"vari",7}{"fres",7}  esperado");
        GD.Print(new string('-', 88));

        foreach (var (name, baseId, items, expected) in Cases)
        {
            var session = new CookingSession(pantry, Level);
            session.SetBase(bases[baseId]);
            foreach (var (id, units) in items)
                session.AddUnit(defs[id], units);

            var dish = DishEvaluator.Evaluate(session, anchors);
            GD.Print(
                $"{name,-30}{dish.Quality,6:0.00}{dish.SeasoningScore,7:0.00}{dish.BalanceScore,7:0.00}" +
                $"{dish.HarmonyScore,7:0.00}{dish.VarietyScore,7:0.00}{dish.FreshnessScore,7:0.00}  {expected}" +
                (dish.MatchedAnchor ? "  ★" : ""));
        }

        GD.Print();
        PrintSkillCurve(pantry, bases, anchors, defs);
        Quit();
    }

    /// <summary>O mesmo prato ótimo em vários níveis: confere se a perícia é gargalo de verdade.</summary>
    private static void PrintSkillCurve(
        Dictionary<IngredientDef, int> pantry,
        Dictionary<string, BaseItemDef> bases,
        List<RecipeAnchor> anchors,
        Dictionary<string, IngredientDef> defs)
    {
        GD.Print("omelete completa por nível de culinária:");
        foreach (int level in new[] { 0, 3, 6, 10 })
        {
            var session = new CookingSession(pantry, level);
            session.SetBase(bases["frigideira"]);
            foreach (var (id, units) in new[] { ("ovo", 20), ("queijo", 8), ("cebolinha", 4), ("sal", 2), ("pimenta", 1) })
            {
                // Níveis baixos têm menos slots — para na primeira recusa, que é o ponto.
                if (!defs[id].IsSeasoning && session.Ingredients.Count >= session.IngredientSlots &&
                    session.UnitsInDish(defs[id]) == 0)
                    continue;
                session.AddUnit(defs[id], units);
            }

            var dish = DishEvaluator.Evaluate(session, anchors);
            GD.Print($"  nível {level,2}  ->  {dish.Quality:0.00}   (teto {dish.QualityCeiling:0.00}, " +
                     $"{session.Ingredients.Count}/{session.IngredientSlots} slots)");
        }
    }
}
