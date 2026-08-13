using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ContextEngine.Demo;

/// <summary>
/// Workshop. The first context where the primary region is single-select rather than
/// quantities — same tile, no steppers — and where the checklist carries the whole "what is
/// stopping me" answer.
/// </summary>
public sealed class CraftModule : IContextModule
{
    private readonly ActionSet _acts = new(("measure", false), ("spares", false));
    private string _bench = "workbench";
    private string _blueprint = "barricade";

    public ContextDefinition Definition { get; } = new()
    {
        Id = "craft",
        Title = "Workshop",
        Crumb = "garage",
        Width = 1140,
        PausesWorld = true,
        FocusEntry = RegionId.Primary,
        Subject = new RegionSpec("picker", "benches@world"),
        Actions = new RegionSpec("verbs", "",
            ("items", new Godot.Collections.Array { "measure", "spares", "manual" }),
            ("gate", new Godot.Collections.Dictionary { { "manual", "has(item:recipeBook)" } })),
        Primary = new RegionSpec("grid.select", "blueprints[bench]"),
        Secondary = new RegionSpec("checklist", "blueprint.need", ("against", "inventory")),
        Preview = new RegionSpec("preview", "blueprint.out", ("art", "blueprint.out.sprite")),
        Readout = new RegionSpec("readout", "", ("evaluator", "CraftOddsEvaluator"), ("factors", 0)),
        CommitAction = "craft",
        CommitLabel = "Craft",
        CommitBlockedWhen = "blockedRequirements > 0",
    };

    public void Enter()
    {
        _bench = "workbench";
        _blueprint = "barricade";
    }

    public void Apply(Intent intent)
    {
        switch (intent)
        {
            case SubjectChanged s:
                _bench = s.Id;
                _blueprint = GameData.Blueprints[_bench][0].Id;
                break;
            case SelectionChanged s:
                _blueprint = s.Id;
                break;
            case ActionToggled a:
                _acts.Toggle(a.Id);
                break;
        }
    }

    public ContextViewModel Build()
    {
        var bench = GameData.Benches.First(b => b.Id == _bench);
        var list = GameData.Blueprints[_bench];
        var blueprint = list.FirstOrDefault(b => b.Id == _blueprint) ?? list[0];
        var spares = _acts["spares"];

        var craft = CraftOddsEvaluator.Instance.Evaluate(new CraftState(_bench, blueprint.Id, _acts["measure"], spares));

        var rows = blueprint.Need.Select(pair =>
        {
            var part = GameData.Parts.First(p => p.Id == pair.Key);
            var ok = part.Have >= pair.Value;
            return new ChecklistRow
            {
                Name = part.Name,
                Tint = part.Color,
                MarkAlert = !ok,
                Note = ok ? "in bag" : spares ? "substituting" : $"missing {pair.Value - part.Have}",
                Tally = $"{part.Have} / {pair.Value}",
                TallyTone = ok ? Tone.Ink : spares ? Tone.Dim : Tone.Alert,
            };
        }).ToList();

        return new ContextViewModel
        {
            Title = Definition.Title,
            Crumb = bench.Name + " · garage",
            Width = Definition.Width,
            Regions = new Dictionary<RegionId, IRegionVm>
            {
                [RegionId.Subject] = ModuleSupport.Picker("Bench", GameData.Benches, _bench, bench.Note,
                    b => (b.Id, b.Name, b.Color)),

                [RegionId.Actions] = ModuleSupport.Verbs(_acts,
                    new Verb("measure", "Measure twice", "CARP 3", "+8% success, +50% time."),
                    new Verb("spares", "Improvise a part", "TINK 6",
                        spares ? "One missing part substituted at −15% odds." : "Cover one missing requirement with scrap."),
                    new Verb("manual", "Follow the manual", "READ",
                        "Locked — the recipe book is not in your bag.", Locked: true)),

                [RegionId.Primary] = new TileGridVm
                {
                    Title = "Blueprints",
                    Count = list.Length + " known",
                    Steppers = false,
                    Tiles = list.Select(b => new TileVm
                    {
                        Id = b.Id,
                        Name = b.Name,
                        Tint = b.Color,
                        Sub = $"{b.Time} · {Mathf.RoundToInt((float)(b.Base * 100))}% base",
                        Qty = b.Id == blueprint.Id ? "▸" : "",
                        Active = b.Id == blueprint.Id,
                    }).ToList(),
                },

                [RegionId.Secondary] = new ChecklistVm
                {
                    Title = "Requirements",
                    Count = craft.Missing > 0 ? craft.Missing + " missing" : "all met",
                    Rows = rows,
                },

                [RegionId.Preview] = new PreviewVm
                {
                    Title = "Output",
                    SlotLabel = "sprite 64²",
                    Name = blueprint.Out,
                    Desc = blueprint.Desc,
                    Tags = new List<string> { blueprint.Time, bench.Name, craft.Missing > 0 ? craft.Missing + " missing" : "ready" },
                },

                [RegionId.Readout] = new ReadoutVm
                {
                    Headline = "Chance of success",
                    Value = Mathf.RoundToInt((float)(craft.Chance * 100)) + "%",
                    Quality = craft.Chance,
                    BarFraction = craft.Chance,
                    Caption = craft.Blocked > 0
                        ? "Each unmet requirement drops the odds 25 points."
                        : spares && craft.Missing > 0
                            ? "Improvised part costs 15 points of certainty."
                            : "Bench bonus and skill are already folded in.",
                    Rows = new List<StatVm>
                    {
                        new() { Key = "Time", Value = blueprint.Time },
                        new() { Key = "Base", Value = Mathf.RoundToInt((float)(blueprint.Base * 100)) + "%" },
                        new() { Key = "Bench", Value = bench.Name, Tone = Tone.Dim },
                        new() { Key = "Missing", Value = craft.Missing.ToString(), Tone = craft.Missing > 0 ? Tone.Alert : Tone.Ink },
                        new() { Key = "XP", Value = "+4" },
                        new() { Key = "Salvage", Value = "60%", Tone = Tone.Dim },
                    },
                },

                [RegionId.Commit] = new CommitVm
                {
                    Label = craft.Blocked > 0 ? "Missing parts" : "Craft",
                    Meta = blueprint.Time,
                    Disabled = craft.Blocked > 0,
                    Hint = craft.Blocked > 0
                        ? "Find or buy the flagged parts — or improvise one."
                        : "Failure salvages 60% of the materials.",
                },
            },
        };
    }
}
