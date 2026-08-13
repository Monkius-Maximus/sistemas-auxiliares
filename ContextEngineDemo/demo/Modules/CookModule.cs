using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ContextEngine.Demo;

/// <summary>
/// Kitchen. The context the whole system was derived from: pick a vessel, fill six ingredient
/// slots and four seasoning slots, and watch one headline number explain itself in five factors.
/// </summary>
public sealed class CookModule : IContextModule
{
    private const int IngredientSlots = 6;
    private const int SeasoningSlots = 4;

    private readonly ActionSet _acts = new(("taste", true), ("plate", false), ("lowflame", false));
    private readonly Dictionary<string, int> _qty = new();
    private string _vessel = "skillet";

    public ContextDefinition Definition { get; } = new()
    {
        Id = "cook",
        Title = "Kitchen",
        Crumb = "stove · Ramos house",
        Width = 1180,
        PausesWorld = true,
        FocusEntry = RegionId.Primary,
        Subject = new RegionSpec("picker", "vessels@station"),
        Actions = new RegionSpec("verbs", "", ("items", new Godot.Collections.Array { "taste", "plate", "lowflame" })),
        // "slots" is an expression against the character sheet: skill progression changes the UI
        // itself, not only the numbers. A level-2 cook literally sees fewer slots than a level-10 one.
        Primary = new RegionSpec("grid.qty", "inventory:food", ("slots", "2 + floor(skill.cooking / 4)")),
        Secondary = new RegionSpec("grid.qty", "inventory:seasoning", ("slots", SeasoningSlots)),
        Preview = new RegionSpec("preview", "", ("art", "dish.sprite"), ("from", "DishEvaluator.name")),
        Readout = new RegionSpec("readout", "", ("evaluator", "DishEvaluator"), ("factors", 5), ("gatedBy", "taste")),
        CommitAction = "cook",
        CommitLabel = "Cook",
        CommitXp = new Godot.Collections.Dictionary { { "cooking", 2 } },
    };

    public void Enter()
    {
        _qty.Clear();
        _qty["egg"] = 15;
        _qty["bacon"] = 10;
        _qty["salt"] = 2;
        _qty["pepper"] = 1;
        _vessel = "skillet";
    }

    public void Apply(Intent intent)
    {
        switch (intent)
        {
            case SubjectChanged s:
                _vessel = s.Id;
                break;
            case QtyChanged q:
                Step(q.Id, q.Delta);
                break;
            case ActionToggled a:
                _acts.Toggle(a.Id);
                break;
            case Committed:
                _qty.Clear();
                break;
        }
    }

    private void Step(string id, int delta)
    {
        var ingredient = GameData.Ingredients.FirstOrDefault(i => i.Id == id);
        var seasoning = GameData.Seasonings.FirstOrDefault(s => s.Id == id);
        if (ingredient == null && seasoning == null) return;

        var isNew = !_qty.ContainsKey(id);
        var used = ingredient != null ? IngredientsUsed : SeasoningsUsed;
        var budget = ingredient != null ? IngredientSlots : SeasoningSlots;
        if (delta > 0 && isNew && used >= budget) return;

        ModuleSupport.Bump(_qty, id, delta, ingredient?.Stock ?? seasoning.Stock);
    }

    private int IngredientsUsed => GameData.Ingredients.Count(i => _qty.ContainsKey(i.Id));
    private int SeasoningsUsed => GameData.Seasonings.Count(s => _qty.ContainsKey(s.Id));

    public ContextViewModel Build()
    {
        var vessel = GameData.Vessels.First(v => v.Id == _vessel);
        var taste = _acts["taste"];
        var dish = DishEvaluator.Instance.Evaluate(new DishState(_qty, _vessel, taste, _acts["lowflame"], _acts["plate"]));
        var empty = dish.Empty;

        return new ContextViewModel
        {
            Title = Definition.Title,
            Crumb = Definition.Crumb,
            Width = Definition.Width,
            Regions = new Dictionary<RegionId, IRegionVm>
            {
                [RegionId.Subject] = ModuleSupport.Picker("Vessel", GameData.Vessels, _vessel, vessel.Note,
                    v => (v.Id, v.Name, v.Color)),

                [RegionId.Actions] = ModuleSupport.Verbs(_acts,
                    new Verb("taste", "Taste as you go", "COOK 6",
                        taste ? "Seasoning window widened; factors readable." : "Factors hidden until you taste."),
                    new Verb("plate", "Plate carefully", "CHAR 3", "+6 mood, costs one extra minute."),
                    new Verb("lowflame", "Low flame", "COOK 4", "+12% freshness, doubles the cook time.")),

                [RegionId.Primary] = new TileGridVm
                {
                    Title = "Ingredients",
                    Count = $"{IngredientsUsed} / {IngredientSlots}",
                    Tiles = GameData.Ingredients.Select(i => Tile(i.Id, i.Name, i.Color,
                        "stock " + (i.Stock - Qty(i.Id)), i.Stock, IngredientsUsed, IngredientSlots)).ToList(),
                },

                [RegionId.Secondary] = new TileGridVm
                {
                    Title = "Seasonings",
                    Count = $"{SeasoningsUsed} / {SeasoningSlots}",
                    Tiles = GameData.Seasonings.Select(s => Tile(s.Id, s.Name, s.Color,
                        s.Profile, s.Stock, SeasoningsUsed, SeasoningSlots)).ToList(),
                },

                [RegionId.Preview] = new PreviewVm
                {
                    Title = "Resulting dish",
                    SlotLabel = "sprite 64²",
                    Name = empty ? "Empty pan" : dish.Name,
                    Desc = empty
                        ? "Nothing in the vessel yet."
                        : dish.Anchor
                            ? "A known recipe — the Sim recognises it and eats faster."
                            : "Improvised. Names itself after the dominant ingredient.",
                    Tags = empty
                        ? new List<string> { vessel.Name }
                        : new List<string> { vessel.Name, $"{dish.Groups} groups", $"{dish.Mass} units" },
                },

                [RegionId.Readout] = Readout(dish, taste),

                [RegionId.Commit] = new CommitVm
                {
                    Label = "Cook",
                    Meta = empty ? "" : vessel.Name,
                    Disabled = empty,
                    Hint = empty ? "Nothing to cook yet." : "Consumes the listed units and adds 2 XP to Cooking.",
                },
            },
        };
    }

    private int Qty(string id) => _qty.TryGetValue(id, out var n) ? n : 0;

    private TileVm Tile(string id, string name, Color tint, string sub, int stock, int used, int budget)
    {
        var qty = Qty(id);
        var blocked = qty == 0 && used >= budget;
        return new TileVm
        {
            Id = id,
            Name = name,
            Tint = tint,
            Sub = sub,
            Qty = qty > 0 ? qty.ToString() : "–",
            Active = qty > 0,
            Dimmed = blocked,
            CanAdd = !blocked && qty < stock,
            CanSub = qty > 0,
        };
    }

    private ReadoutVm Readout(DishJudgement dish, bool taste)
    {
        var factors = new (string Name, double Value)[]
        {
            ("seasoning", dish.Seasoning), ("balance", dish.Balance), ("harmony", dish.Harmony),
            ("variety", dish.Variety), ("freshness", dish.Freshness),
        };

        var rows = dish.Empty
            ? new[] { "Hunger", "Calories", "Protein", "Fat", "Carbs", "Weight", "Mood", "Groups", "Season %" }
                .Select(k => new StatVm { Key = k, Value = "—", Tone = Tone.Mute }).ToList()
            : new List<StatVm>
            {
                new() { Key = "Hunger", Value = dish.Stats.Hunger.ToString("0.0") },
                new() { Key = "Calories", Value = Mathf.RoundToInt(dish.Stats.Kcal).ToString() },
                new() { Key = "Protein", Value = dish.Stats.Prot.ToString("0.0") },
                new() { Key = "Fat", Value = dish.Stats.Fat.ToString("0.0") },
                new() { Key = "Carbs", Value = dish.Stats.Carb.ToString("0.0") },
                new() { Key = "Weight", Value = dish.Stats.Weight.ToString("0.00") },
                new() { Key = "Mood", Value = (dish.Stats.Mood > 0 ? "+" : "") + dish.Stats.Mood.ToString("0.0"),
                        Tone = dish.Stats.Mood < 0 ? Tone.Alert : Tone.Ink },
                new() { Key = "Groups", Value = $"{dish.Groups} / 3", Tone = dish.Groups < 3 ? Tone.Alert : Tone.Ink },
                new() { Key = "Season %", Value = Mathf.RoundToInt((float)(dish.Ratio * 100)).ToString() },
            };

        return new ReadoutVm
        {
            Headline = dish.Empty ? "Nothing in the pan" : dish.Anchor ? "Recipe matched ★" : "Improvised dish",
            Value = dish.Empty ? "—" : Mathf.RoundToInt((float)(dish.Quality * 100)) + "%",
            Quality = dish.Empty ? null : dish.Quality,
            BarFraction = dish.Empty ? 0 : Mathf.Max(dish.Quality, 0.02),
            Caption = dish.Empty
                ? "Add an ingredient to begin."
                : taste
                    ? "Quality is the product of the five factors, not their average."
                    : "Turn on Taste as you go to read the breakdown.",
            Factors = factors.Select(f => new FactorVm { Name = f.Name, Value = f.Value, Known = taste }).ToList(),
            Rows = rows,
        };
    }
}
