using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ContextEngine.Demo;

/// <summary>
/// Vehicle repair. The highest-value primitive in the catalogue meets the cheapest one: a
/// hotspot map picks the system, a sequence scores the procedure, and the readout multiplies
/// worst × mean so one dead system caps the whole job. The same pair unlocks medical treatment
/// and building inspection without a line of new UI.
/// </summary>
public sealed class RepairModule : IContextModule
{
    private readonly ActionSet _acts = new(("diag", true), ("torque", false));
    private readonly Dictionary<string, Dictionary<string, List<int>>> _progress = new();

    private string _rig = "sedan";
    private string _system = "batt";

    public ContextDefinition Definition { get; } = new()
    {
        Id = "repair",
        Title = "Vehicle repair",
        Crumb = "hood up · toolbox open",
        Width = 1240,
        PausesWorld = false,
        FocusEntry = RegionId.Primary,
        Subject = new RegionSpec("picker", "vehicles@reach"),
        Actions = new RegionSpec("verbs", "",
            ("items", new Godot.Collections.Array { "diag", "torque", "hmanual" }),
            ("gate", new Godot.Collections.Dictionary { { "hmanual", "has(item:workshopManual)" } })),
        Primary = new RegionSpec("hotspot.map", "vehicle.systems",
            ("value", "system.condition"), ("gatedBy", "diag")),
        Secondary = new RegionSpec("sequence", "system.procedure",
            ("scored", "order"), ("penalty", "cond 0.95 -> 0.62")),
        Preview = new RegionSpec("preview", "selected.system", ("art", "system.sprite")),
        Readout = new RegionSpec("readout", "",
            ("evaluator", "RigEvaluator"), ("rule", "worst * mean"), ("forecast", "reliability@trip")),
        CommitAction = "roadTest",
        CommitLabel = "Road test",
        CommitBlockedWhen = "worstSystem < 0.4",
    };

    public void Enter()
    {
        _progress.Clear();
        _rig = "sedan";
        _system = GameData.Systems[_rig][0].Id;
    }

    private Dictionary<string, List<int>> RigProgress
    {
        get
        {
            if (!_progress.TryGetValue(_rig, out var map))
                _progress[_rig] = map = new Dictionary<string, List<int>>();
            return map;
        }
    }

    private List<int> StepsFor(string systemId)
    {
        if (!RigProgress.TryGetValue(systemId, out var steps))
            RigProgress[systemId] = steps = new List<int>();
        return steps;
    }

    public void Apply(Intent intent)
    {
        switch (intent)
        {
            case SubjectChanged s:
                _rig = s.Id;
                _system = GameData.Systems[_rig][0].Id;
                break;
            case SelectionChanged s:
                _system = s.Id;
                break;
            case StepTaken s:
                var steps = StepsFor(_system);
                if (!steps.Contains(s.Index)) steps.Add(s.Index);
                break;
            case SequenceReset:
                StepsFor(_system).Clear();
                break;
            case ActionToggled a:
                _acts.Toggle(a.Id);
                break;
        }
    }

    public ContextViewModel Build()
    {
        var rig = GameData.Rigs.First(r => r.Id == _rig);
        var systems = GameData.Systems[_rig];
        var current = systems.FirstOrDefault(s => s.Id == _system) ?? systems[0];
        var diag = _acts["diag"];

        var state = new RigState(_rig,
            RigProgress.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<int>)kv.Value),
            _acts["torque"]);
        var judgement = RigEvaluator.Instance.Evaluate(state);

        var sequence = ModuleSupport.Sequence(current.Steps, StepsFor(current.Id), "Procedure · " + current.Name,
            "Tap the procedure in order. The manual order is not always the obvious one.");

        // Reliability decays 6% a trip — the forecast strip turns the evaluator into a projection.
        var forecast = Enumerable.Range(0, 8)
            .Select(i => judgement.Reliability * Mathf.Pow(0.94f, i)).ToList();

        var blocked = judgement.Worst < 0.4;

        return new ContextViewModel
        {
            Title = Definition.Title,
            Crumb = rig.Name + " · hood up · toolbox open",
            Width = Definition.Width,
            Regions = new Dictionary<RegionId, IRegionVm>
            {
                [RegionId.Subject] = ModuleSupport.Picker("Rig", GameData.Rigs, _rig, rig.Note,
                    r => (r.Id, r.Name, r.Color)),

                [RegionId.Actions] = ModuleSupport.Verbs(_acts,
                    new Verb("diag", "Run diagnostics", "MECH 3",
                        diag ? "Condition read out on every system." : "Condition stays hidden until you meter it."),
                    new Verb("torque", "Torque to spec", "MECH 5",
                        _acts["torque"] ? "+3 points on every finished system, +50% time."
                                        : "Slower, but nothing works loose on the road."),
                    new Verb("hmanual", "Consult the manual", "READ",
                        "Locked — the workshop manual is in the glovebox of the other car.", Locked: true)),

                [RegionId.Primary] = new HotspotMapVm
                {
                    Title = "Engine bay",
                    Count = systems.Length + " systems",
                    MapLabel = diag
                        ? "Multiplicative: the worst system caps the whole job — the seized bolt rule."
                        : "Hotspot map. Turn on diagnostics to read condition per system.",
                    Spots = systems.Select((s, i) =>
                    {
                        var condition = judgement.Conditions[i];
                        var repaired = StepsFor(s.Id).Count == s.Steps.Length;
                        return new Hotspot
                        {
                            Id = s.Id,
                            Name = s.Name,
                            Rect = s.Rect,
                            Selected = s.Id == current.Id,
                            Faulty = condition < 0.4,
                            Cond = diag ? Mathf.RoundToInt((float)(condition * 100)) + "%" : "??",
                            Quality = diag ? condition : null,
                            Badge = repaired ? "repaired" : condition < 0.4 ? "faulty" : "ok",
                            BadgeAlert = !repaired && condition < 0.4,
                            BadgeDone = repaired,
                        };
                    }).ToList(),
                },

                [RegionId.Secondary] = sequence.Vm,

                [RegionId.Preview] = new PreviewVm
                {
                    Title = "Selected system",
                    SlotLabel = "sprite 96²",
                    Name = current.Name,
                    Desc = sequence.Done
                        ? sequence.InOrder
                            ? "Done properly. Torque marks lined up, nothing weeping."
                            : "Back together, but out of sequence — it will not hold long."
                        : current.Fault,
                    Tags = new List<string>
                    {
                        rig.Name,
                        current.Tool,
                        sequence.Done ? (sequence.InOrder ? "repaired" : "botched") : "faulty",
                    },
                },

                [RegionId.Readout] = new ReadoutVm
                {
                    Headline = "Reliability",
                    Value = Mathf.RoundToInt((float)(judgement.Reliability * 100)) + "%",
                    Quality = judgement.Reliability,
                    BarFraction = Mathf.Max(judgement.Reliability, 0.02),
                    Caption = diag
                        ? $"Worst system × the fleet mean. {judgement.WeakLink.Name} is what is holding this number down."
                        : "Meter the systems first — this number is a guess without diagnostics.",
                    Factors = systems.Take(5).Select((s, i) => new FactorVm
                    {
                        Name = s.Name.ToLowerInvariant(),
                        Value = judgement.Conditions[i],
                        Known = diag,
                    }).ToList(),
                    Forecast = new ForecastVm
                    {
                        Title = "Chance it starts, next 8 trips",
                        Bars = forecast,
                        Threshold = 0.5,
                        From = "trip 1",
                        To = "trip 8",
                        Delta = $"{Mathf.RoundToInt((float)(judgement.Reliability * 100))}% → " +
                                $"{Mathf.RoundToInt((float)(judgement.Reliability * Mathf.Pow(0.94f, 7) * 100))}%",
                        DeltaAlert = judgement.Reliability < 0.5,
                    },
                    Rows = new List<StatVm>
                    {
                        new() { Key = "Worst", Value = Mathf.RoundToInt((float)(judgement.Worst * 100)) + "%",
                                Tone = judgement.Worst < 0.4 ? Tone.Alert : Tone.Ink },
                        new() { Key = "Weak link", Value = diag ? judgement.WeakLink.Name : "??",
                                Tone = diag ? Tone.Alert : Tone.Mute },
                        new() { Key = "Mean", Value = Mathf.RoundToInt((float)(judgement.Mean * 100)) + "%", Tone = Tone.Dim },
                        new() { Key = "Fixed", Value = $"{judgement.Repaired} / {systems.Length}" },
                        new() { Key = "Torque", Value = _acts["torque"] ? "to spec" : "by feel",
                                Tone = _acts["torque"] ? Tone.Ink : Tone.Mute },
                        new() { Key = "XP", Value = "+6", Tone = Tone.Dim },
                    },
                },

                [RegionId.Commit] = new CommitVm
                {
                    Label = blocked ? "Will not start" : "Road test",
                    Meta = Mathf.RoundToInt((float)(judgement.Reliability * 100)) + "%",
                    Disabled = blocked,
                    Hint = blocked
                        ? $"Fix {judgement.WeakLink.Name} first — one dead system stops the whole job."
                        : "Ten kilometres of road. Anything loose shows up in the first two.",
                },
            },
        };
    }
}
