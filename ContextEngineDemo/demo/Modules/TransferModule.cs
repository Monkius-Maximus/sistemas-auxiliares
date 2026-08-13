using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ContextEngine.Demo;

/// <summary>
/// Transfer. The dual grid is the same tile primitive with a move axis instead of steppers:
/// a trade screen is this region set with a price column bound to the readout, and looting a
/// body is this with the left pane sourced from a corpse. Both are definition files.
/// </summary>
public sealed class TransferModule : IContextModule
{
    private const double BaseWeight = 4.2;
    private const double Capacity = 18;

    private readonly ActionSet _acts = new(("sortw", false), ("foodonly", false));
    private readonly Dictionary<string, Dictionary<string, int>> _moved = new();
    private string _container = "footlocker";

    public ContextDefinition Definition { get; } = new()
    {
        Id = "transfer",
        Title = "Transfer",
        Crumb = "reach 1.5 m",
        Width = 1240,
        PausesWorld = false,
        FocusEntry = RegionId.Primary,
        Subject = new RegionSpec("picker", "containers@reach"),
        // View verbs: they only reorder or filter the panel, so they need no gate at all.
        Actions = new RegionSpec("verbs", "",
            ("items", new Godot.Collections.Array { "sortw", "foodonly", "drop" }), ("kind", "view")),
        Primary = new RegionSpec("grid.dual", "",
            ("left", "container.contents"), ("right", "player.inventory")),
        Secondary = new RegionSpec("text", "designer.note"),
        Preview = new RegionSpec("preview", "container", ("art", "container.sprite")),
        Readout = new RegionSpec("readout", "", ("evaluator", "EncumbranceEvaluator"), ("factors", 0)),
        CommitAction = "takeAll",
        CommitLabel = "Take all",
    };

    public void Enter()
    {
        _moved.Clear();
        _container = "footlocker";
    }

    private Dictionary<string, int> Moved
    {
        get
        {
            if (!_moved.TryGetValue(_container, out var map))
                _moved[_container] = map = new Dictionary<string, int>();
            return map;
        }
    }

    public void Apply(Intent intent)
    {
        switch (intent)
        {
            case SubjectChanged s:
                _container = s.Id;
                break;
            case Moved m:
                Move(m.Id, m.Delta);
                break;
            case ActionToggled a when a.Id == "drop":
                DropHeaviest();
                break;
            case ActionToggled a:
                _acts.Toggle(a.Id);
                break;
            case Committed:
                foreach (var item in GameData.ContainerItems[_container])
                    Moved[item.Id] = item.Qty;
                break;
        }
    }

    private void Move(string id, int delta)
    {
        var item = GameData.ContainerItems[_container].FirstOrDefault(i => i.Id == id);
        if (item == null) return;
        Moved[id] = Mathf.Clamp((Moved.TryGetValue(id, out var n) ? n : 0) + delta, 0, item.Qty);
    }

    private void DropHeaviest()
    {
        var heaviest = Taken().OrderByDescending(i => i.Weight).FirstOrDefault();
        if (heaviest != null) Move(heaviest.Id, -Moved[heaviest.Id]);
    }

    private List<ContainerItem> Remaining() => GameData.ContainerItems[_container]
        .Select(i => i with { Qty = i.Qty - (Moved.TryGetValue(i.Id, out var n) ? n : 0) })
        .Where(i => i.Qty > 0).ToList();

    private List<ContainerItem> Taken() => GameData.ContainerItems[_container]
        .Select(i => i with { Qty = Moved.TryGetValue(i.Id, out var n) ? n : 0 })
        .Where(i => i.Qty > 0).ToList();

    private static double WeightOf(IEnumerable<ContainerItem> items) => items.Sum(i => i.Qty * i.Weight);

    public ContextViewModel Build()
    {
        var container = GameData.Containers.First(c => c.Id == _container);
        var remaining = Remaining();
        var taken = Taken();

        var left = remaining.AsEnumerable();
        if (_acts["sortw"]) left = left.OrderByDescending(i => i.Weight);
        if (_acts["foodonly"]) left = left.Where(i => i.Food);
        var leftList = left.ToList();

        var load = EncumbranceEvaluator.Instance.Evaluate(
            new EncumbranceState(BaseWeight, WeightOf(taken), Capacity));
        var heaviest = taken.OrderByDescending(i => i.Weight).FirstOrDefault();

        return new ContextViewModel
        {
            Title = Definition.Title,
            Crumb = container.Name + " · reach 1.5 m",
            Width = Definition.Width,
            Regions = new Dictionary<RegionId, IRegionVm>
            {
                [RegionId.Subject] = ModuleSupport.Picker("Container", GameData.Containers, _container, container.Note,
                    c => (c.Id, c.Name, c.Color)),

                [RegionId.Actions] = ModuleSupport.Verbs(_acts,
                    new Verb("sortw", "Sort by weight", "—", "Heaviest first, so you see the cost before the loot."),
                    new Verb("foodonly", "Filter: food only", "—", "Hides everything you cannot eat."),
                    new Verb("drop", "Drop the heaviest", "—",
                        heaviest != null ? $"Puts {heaviest.Name} back in the container." : "Locked — nothing taken yet.",
                        Locked: heaviest == null, On: false)),

                [RegionId.Primary] = new DualGridVm
                {
                    Title = "Move items",
                    Count = $"{leftList.Count} ⇄ {taken.Count}",
                    Left = new DualPane
                    {
                        Name = container.Name,
                        Direction = 1,
                        Arrow = "›",
                        Load = $"{WeightOf(remaining):0.0} / {container.Cap} kg",
                        Empty = leftList.Count > 0 ? "" : _acts["foodonly"] ? "No food in here." : "Container is empty.",
                        Items = leftList.Select(Row).ToList(),
                    },
                    Right = new DualPane
                    {
                        Name = "Your bag",
                        Direction = -1,
                        Arrow = "‹",
                        Load = $"{load.Carried:0.0} / {Capacity} kg",
                        Empty = taken.Count > 0 ? "" : "Nothing taken yet.",
                        Items = taken.Select(Row).ToList(),
                    },
                },

                [RegionId.Secondary] = new TextVm
                {
                    Title = "Designer note",
                    Body = "The dual grid is the same tile primitive with a move axis instead of steppers. " +
                           "A trade screen is this exact region set with a price column bound to the readout; " +
                           "looting a body is this with the left pane sourced from a corpse. The actions region " +
                           "is where a skill like Organisation would add auto-sort or a second filter row.",
                },

                [RegionId.Preview] = new PreviewVm
                {
                    Title = "Container",
                    SlotLabel = "sprite 96²",
                    Name = container.Name,
                    Desc = container.Desc,
                    Tags = new List<string>
                    {
                        container.Cap + " kg cap",
                        remaining.Count + " stacks",
                        taken.Count > 0 ? "taking " + taken.Count : "nothing taken",
                    },
                },

                [RegionId.Readout] = new ReadoutVm
                {
                    Headline = "Carried weight",
                    Value = $"{load.Carried:0.0} kg",
                    ValueTone = load.Overloaded ? Tone.Alert : Tone.Ink,
                    BarFraction = load.Load,
                    Caption = load.Overloaded
                        ? "Over 90% — you will move slowly."
                        : "Under the limit; no speed penalty.",
                    Rows = new List<StatVm>
                    {
                        new() { Key = "Base", Value = BaseWeight.ToString("0.0"), Tone = Tone.Dim },
                        new() { Key = "Taken", Value = WeightOf(taken).ToString("0.0") },
                        new() { Key = "Cap", Value = Capacity.ToString("0"), Tone = Tone.Dim },
                        new() { Key = "Load", Value = Mathf.RoundToInt((float)(load.Load * 100)) + "%",
                                Tone = load.Overloaded ? Tone.Alert : Tone.Ink },
                        new() { Key = "Left", Value = remaining.Count.ToString() },
                        new() { Key = "Speed", Value = load.Overloaded ? "−25%" : "100%",
                                Tone = load.Overloaded ? Tone.Alert : Tone.Ink },
                    },
                },

                [RegionId.Commit] = new CommitVm
                {
                    Label = remaining.Count > 0 ? "Take all" : "Close",
                    Meta = remaining.Count > 0 ? $"{WeightOf(remaining):0.0} kg" : "",
                    Hint = remaining.Count > 0
                        ? "Takes everything that fits, heaviest last."
                        : "Container emptied.",
                },
            },
        };
    }

    private static DualItem Row(ContainerItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Tint = item.Color,
        Sub = item.Weight + " kg",
        Qty = "×" + item.Qty,
    };
}
