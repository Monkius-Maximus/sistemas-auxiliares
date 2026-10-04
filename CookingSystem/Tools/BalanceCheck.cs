using System.Collections.Generic;
using System.Linq;
using Godot;
using LifeSim.Household;

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
        // Despensa fresca, clonada por caso: a sessão debita a despensa que recebe, e um caso
        // não pode gastar o estoque do seguinte.
        var pantry = ContentLibrary.FreshPantry();
        var bases = ContentLibrary.Bases().ToDictionary(b => b.Id, b => b);
        var anchors = ContentLibrary.Anchors();
        var defs = pantry.Defs.ToDictionary(d => d.Id, d => d);

        GD.Print($"{"caso",-30}{"nota",6}{"temp",7}{"equi",7}{"harm",7}{"vari",7}{"fres",7}  esperado");
        GD.Print(new string('-', 88));

        foreach (var (name, baseId, items, expected) in Cases)
        {
            var session = new CookingSession(pantry.Clone(), Level);
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
        PrintVesselComparison(pantry, bases, anchors, defs);
        GD.Print();
        PrintFreshness(bases, anchors, defs);
        GD.Print();
        PrintServings(bases, anchors, defs);
        GD.Print();
        PrintSkillCurve(pantry, bases, anchors, defs);
        GD.Print();
        PrintDaysOfLife(bases.Values.ToList(), anchors, autonomous: true);
        GD.Print();
        PrintDaysOfLife(bases.Values.ToList(), anchors, autonomous: false);
        Quit();
    }

    /// <summary>
    /// O mesmo conteúdo em cada recipiente que o aceita. Se as três colunas saírem iguais,
    /// os modificadores do recipiente pararam de chegar ao avaliador.
    /// </summary>
    private static readonly (string Name, (string Id, int Units)[] Items)[] VesselCases =
    {
        ("legumes, tempero médio",   new[] { ("brocolis", 15), ("tomate", 10), ("sal", 2) }),
        ("legumes, tempero forte",   new[] { ("brocolis", 15), ("tomate", 10), ("sal", 3), ("pimenta", 1) }),
        ("doce × salgado (choque)",  new[] { ("tomate", 20), ("acucar", 2), ("sal", 2) }),
        ("batata + cebolinha + sal", new[] { ("batata", 20), ("cebolinha", 5), ("sal", 3) }),
        ("batata + cebolinha, pouco sal", new[] { ("batata", 20), ("cebolinha", 5), ("sal", 1) }),
    };

    private static void PrintVesselComparison(
        Pantry pantry,
        Dictionary<string, BaseItemDef> bases,
        List<RecipeAnchor> anchors,
        Dictionary<string, IngredientDef> defs)
    {
        string[] order = { "frigideira", "panela", "tigela" };
        GD.Print($"{"mesmo conteúdo, recipientes diferentes",-34}" + string.Concat(order.Select(o => $"{o,12}")));
        foreach (var (name, items) in VesselCases)
        {
            var row = $"{name,-34}";
            foreach (var baseId in order)
            {
                var session = new CookingSession(pantry.Clone(), Level);
                session.SetBase(bases[baseId]);
                bool fits = items.All(i => defs[i.Id].IsSeasoning || bases[baseId].Accepts(defs[i.Id].Group));
                if (!fits) { row += $"{"—",12}"; continue; }
                foreach (var (id, units) in items)
                    session.AddUnit(defs[id], units);
                row += $"{DishEvaluator.Evaluate(session, anchors).Quality,12:0.00}";
            }
            GD.Print(row);
        }
    }

    /// <summary>
    /// A omelete completa com os ovos em idades diferentes, e o mesmo tomate envelhecido na
    /// tigela crua e na panela. Confere três coisas: frescor puxa a nota pela massa, o nome
    /// avisa, e o calor corta o risco de intoxicação que o cru deixa inteiro.
    /// </summary>
    private static void PrintFreshness(
        Dictionary<string, BaseItemDef> bases,
        List<RecipeAnchor> anchors,
        Dictionary<string, IngredientDef> defs)
    {
        GD.Print($"{"frescor",-34}{"nota",6}{"fres",7}{"intox",7}  nome");
        var cases = new (string Name, string Base, string AgedId, float Age, (string Id, int Units)[] Items)[]
        {
            ("omelete, ovos frescos",     "frigideira", "ovo",    0f,  new[] { ("ovo", 20), ("queijo", 8), ("cebolinha", 4), ("sal", 2), ("pimenta", 1) }),
            ("omelete, ovos passados",    "frigideira", "ovo",    28f, new[] { ("ovo", 20), ("queijo", 8), ("cebolinha", 4), ("sal", 2), ("pimenta", 1) }),
            ("omelete, ovos estragados",  "frigideira", "ovo",    34f, new[] { ("ovo", 20), ("queijo", 8), ("cebolinha", 4), ("sal", 2), ("pimenta", 1) }),
            ("tomate estragado, cru",     "tigela",     "tomate", 9f,  new[] { ("tomate", 10), ("queijo", 10), ("ervas", 2), ("sal", 1) }),
            ("tomate estragado, fervido", "panela",     "tomate", 9f,  new[] { ("tomate", 10), ("batata", 10), ("ervas", 2), ("sal", 1) }),
        };
        foreach (var (name, baseId, agedId, age, items) in cases)
        {
            var pantry = new Pantry(defs.Values.Select(d => (d, 40, d.Id == agedId ? age : 0f)));
            var session = new CookingSession(pantry, Level);
            session.SetBase(bases[baseId]);
            foreach (var (id, units) in items)
                session.AddUnit(defs[id], units);
            var dish = DishEvaluator.Evaluate(session, anchors);
            GD.Print($"{name,-34}{dish.Quality,6:0.00}{dish.FreshnessScore,7:0.00}{dish.PoisoningChance,7:P0}  {dish.Name}");
        }
    }

    /// <summary>
    /// O que uma porção faz com um Sim faminto (fome e sede em 30). Confere a escala das
    /// necessidades: uma porção de prato bom deve encher boa parte da fome, não toda nem nada.
    /// </summary>
    private static void PrintServings(
        Dictionary<string, BaseItemDef> bases,
        List<RecipeAnchor> anchors,
        Dictionary<string, IngredientDef> defs)
    {
        GD.Print($"{"uma porção, Sim com fome e sede em 30",-40}{"porç",5}{"fome",7}{"sede",7}  moodlet");
        foreach (var (name, baseId, items, _) in Cases.Where(c => c.Expected is "sólido" or "excelente"))
        {
            var session = new CookingSession(ContentLibrary.FreshPantry(), Level);
            session.SetBase(bases[baseId]);
            foreach (var (id, units) in items)
                session.AddUnit(defs[id], units);

            var meal = new Meal(DishEvaluator.Evaluate(session, anchors));
            var sim = new Sim("teste", 30f, 30f);
            GD.Print($"{name,-40}{meal.TotalServings,5}{sim.HungerAfter(meal) - 30f,7:+0;-0}" +
                     $"{sim.ThirstAfter(meal) - 30f,7:+0;-0}  {Sim.MoodletFor(meal).Name}");
        }
    }

    /// <summary>
    /// Três dias de uma casa com a cozinha do demo, semente fixa. Com livre-arbítrio, o Sim deve
    /// se manter alimentado sozinho até a despensa acabar; sem, deve desmaiar. Se o diário com
    /// autonomia mostrar desmaio com comida na despensa, a autonomia regrediu.
    /// </summary>
    private static void PrintDaysOfLife(List<BaseItemDef> bases, List<RecipeAnchor> anchors, bool autonomous)
    {
        var kitchen = new CookingSession(ContentLibrary.StartingPantry(), 6);
        kitchen.SetBase(bases[0]);
        var cookbook = new Cookbook(bases, anchors, anchors.Select(a => a.DishName).ToList());
        var home = new Household.Household(kitchen, new Sim("Ana", 45f, 50f), cookbook, seed: 7) { Autonomous = autonomous };

        home.AdvanceHours(72f);

        GD.Print($"três dias, livre-arbítrio {(autonomous ? "ligado" : "desligado")}:");
        foreach (var e in home.Log.Reverse())
            GD.Print($"  {e.Clock,-12} {e.Text}");
        GD.Print($"  → fome {home.Sim.Hunger:0}, sede {home.Sim.Thirst:0}, humor {home.Sim.Mood:+0;-0;0}" +
                 $" ({string.Join(", ", home.Sim.Moodlets.Select(m => m.Name))})");
    }

    /// <summary>O mesmo prato ótimo em vários níveis: confere se a perícia é gargalo de verdade.</summary>
    private static void PrintSkillCurve(
        Pantry pantry,
        Dictionary<string, BaseItemDef> bases,
        List<RecipeAnchor> anchors,
        Dictionary<string, IngredientDef> defs)
    {
        GD.Print("omelete completa por nível de culinária:");
        foreach (int level in new[] { 0, 3, 6, 10 })
        {
            var session = new CookingSession(pantry.Clone(), level);
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
