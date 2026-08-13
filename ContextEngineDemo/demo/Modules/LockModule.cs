using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ContextEngine.Demo;

/// <summary>
/// Lockpicking. The one context built on a real-time primitive, and therefore the one that
/// needs the most care: the timing bar is isolated in its own widget, and the accessibility
/// verb converts it into a skill check with the same expected value. A real-time primitive
/// must never be the only path to a goal.
/// </summary>
public sealed class LockModule : IContextModule
{
    private readonly ActionSet _acts = new(("tension", true), ("rake", false));
    private readonly RandomNumberGenerator _rng = new();

    private string _lockId = "padlock";
    private int _pinsSet;
    private int _strain;
    private double _zone = 52;
    private bool _rollMode;
    private bool? _lastHit;
    private string _message = "";

    public ContextDefinition Definition { get; } = new()
    {
        Id = "lockpick",
        Title = "Lockpicking",
        Crumb = "back door",
        Width = 1080,
        // The world does not politely stop while you are crouched at somebody's back door.
        PausesWorld = false,
        FocusEntry = RegionId.Primary,
        Subject = new RegionSpec("picker", "locks@reach"),
        Actions = new RegionSpec("verbs", "",
            ("items", new Godot.Collections.Array { "tension", "rake", "srolls", "bump" }),
            ("accessibility", "srolls forces evaluator mode")),
        Primary = new RegionSpec("timing.bar", "",
            ("realtime", true),
            ("window", "lock.win + tension - rake"),
            ("fallback", "skillCheck(tinkering)")),
        Secondary = new RegionSpec("checklist", "lock.pins", ("mode", "state")),
        Preview = new RegionSpec("preview", "lock", ("art", "lock.sprite")),
        Readout = new RegionSpec("readout", "", ("evaluator", "LockEvaluator"), ("strikes", 3)),
        CommitAction = "open",
        CommitLabel = "Open the door",
        CommitBlockedWhen = "pinsSet < lock.pins",
    };

    public void Enter()
    {
        _rng.Randomize();
        _lockId = "padlock";
        Reset("Fresh pick seated in the keyway.");
        _zone = 52;
        _message = "";
    }

    private void Reset(string message)
    {
        _pinsSet = 0;
        _strain = 0;
        _lastHit = null;
        _zone = 14 + _rng.Randf() * 72;
        _message = message;
    }

    public void Apply(Intent intent)
    {
        var l = GameData.Locks.First(x => x.Id == _lockId);

        switch (intent)
        {
            case SubjectChanged s:
                _lockId = s.Id;
                Reset("");
                _zone = 52;
                break;

            case ActionToggled a when a.Id == "srolls":
                _rollMode = !_rollMode;
                _message = "";
                break;

            case ActionToggled a:
                _acts.Toggle(a.Id);
                break;

            case TimingStrike strike:
                if (_strain >= 3 || _pinsSet >= l.Pins) break;
                Resolve(Math.Abs(strike.Position - _zone) <= LockEvaluator.WindowFor(l, _acts["tension"], _acts["rake"]) / 2,
                    strike.Position);
                break;

            case TimingRoll:
                if (_strain >= 3 || _pinsSet >= l.Pins) break;
                // Same expected value as the sweep. Randomness lives in the resolution step,
                // never in the preview — the readout above stays a pure function of state.
                Resolve(_rng.Randf() < LockEvaluator.OddsFor(l, _acts["tension"], _acts["rake"]), null);
                break;

            case TimingReset:
                Reset("Fresh pick seated in the keyway.");
                break;

            case Committed:
                Reset("Fresh pick seated in the keyway.");
                break;
        }
    }

    private void Resolve(bool hit, double? position)
    {
        var l = GameData.Locks.First(x => x.Id == _lockId);

        if (hit) _pinsSet = Math.Min(l.Pins, _pinsSet + 1);
        else _strain++;

        _lastHit = hit;
        _zone = 14 + _rng.Randf() * 72;

        _message = _strain >= 3
            ? "The pick snapped in the keyway. Reset and start the lock again."
            : hit
                ? _pinsSet == l.Pins
                    ? "All pins set — the cylinder turns."
                    : $"Pin {_pinsSet} set. The next one binds somewhere else."
                : position == null
                    ? $"Roll failed — strike {_strain} of 3."
                    : $"Missed by {Mathf.RoundToInt((float)Math.Abs(position.Value - _zone))} points — strike {_strain} of 3.";
    }

    public ContextViewModel Build()
    {
        var l = GameData.Locks.First(x => x.Id == _lockId);
        var judgement = LockEvaluator.Instance.Evaluate(
            new LockState(_lockId, _pinsSet, _strain, _acts["tension"], _acts["rake"]));

        var broken = judgement.Broken;
        var open = judgement.Open;

        return new ContextViewModel
        {
            Title = Definition.Title,
            Crumb = l.Name + " · back door · nobody watching yet",
            Width = Definition.Width,
            Regions = new Dictionary<RegionId, IRegionVm>
            {
                [RegionId.Subject] = ModuleSupport.Picker("Lock", GameData.Locks, _lockId, l.Note,
                    x => (x.Id, x.Name, x.Color)),

                [RegionId.Actions] = ModuleSupport.Verbs(_acts,
                    new Verb("tension", "Light tension", "TINK 4",
                        _acts["tension"] ? "Window +8, sweep 25% slower." : "Widens the window and slows the sweep."),
                    new Verb("rake", "Rake it", "—",
                        _acts["rake"] ? "Window −5, sweep 50% faster. Fast and stupid."
                                      : "Faster, narrower — for cheap locks and short tempers."),
                    new Verb("srolls", _rollMode ? "Timing bar is off" : "Use a skill check instead", "ACCESS",
                        _rollMode ? "One roll per pin. Same odds, no reflexes required."
                                  : "Accessibility: converts the real-time bar into a roll per pin.",
                        On: _rollMode),
                    new Verb("bump", "Bump key", "TINK 8",
                        "Locked — needs a filed blank for this keyway.", Locked: true)),

                [RegionId.Primary] = new TimingBarVm
                {
                    Title = _rollMode ? "Skill check" : "Feel for the pin",
                    Count = $"{_pinsSet}/{l.Pins} pins",
                    Live = !_rollMode,
                    ZoneCentre = _zone,
                    ZoneWidth = judgement.Window,
                    Speed = judgement.Speed,
                    ActionLabel = open ? "Cylinder is turned" : broken ? "Pick snapped" : "Set pin",
                    ActionEnabled = !open && !broken,
                    RollLabel = "Roll for the pin",
                    Odds = Mathf.RoundToInt((float)(judgement.Odds * 100)) + "%",
                    RollBlurb = "Per-pin chance, rolled from Tinkering and the picks in your bag. Same expected " +
                                "value as the sweep — the real-time bar is never the only route to a goal.",
                    Message = string.IsNullOrEmpty(_message)
                        ? _rollMode
                            ? "Roll once per pin. Failure costs a strike, not a reflex."
                            : "Press Set pin as the sweep crosses the window. Three misses and the pick snaps."
                        : _message,
                    MessageAlert = _lastHit == false,
                },

                [RegionId.Secondary] = new ChecklistVm
                {
                    Title = "Pin stack",
                    Count = _pinsSet + " set",
                    Rows = Enumerable.Range(0, l.Pins).Select(i =>
                    {
                        var set = i < _pinsSet;
                        return new ChecklistRow
                        {
                            Name = "Pin " + (i + 1),
                            MarkAlert = set,
                            MarkOff = !set,
                            Note = set ? "seated on the shear line" : i == _pinsSet ? "binding now" : "waiting",
                            Tally = set ? "set" : "—",
                            TallyTone = set ? Tone.Ink : Tone.Mute,
                        };
                    }).ToList(),
                },

                [RegionId.Preview] = new PreviewVm
                {
                    Title = "The lock",
                    SlotLabel = "sprite 96²",
                    Name = l.Name,
                    Desc = broken
                        ? "Broken pick in the keyway — this lock now needs the door taken off its hinges."
                        : open
                            ? "Turned. It stays unlocked until somebody notices."
                            : "Brass, worn bright around the keyway. Someone opens this a lot.",
                    Tags = new List<string>
                    {
                        l.Pins + " pins",
                        Mathf.RoundToInt((float)judgement.Window) + "% window",
                        _rollMode ? "skill check" : "real time",
                    },
                },

                [RegionId.Readout] = new ReadoutVm
                {
                    Headline = broken ? "Pick broken" : open ? "Cylinder open" : "Cylinder progress",
                    Value = Mathf.RoundToInt((float)(judgement.Progress * 100)) + "%",
                    Quality = broken ? null : open ? null : judgement.Progress,
                    ValueTone = broken ? Tone.Alert : Tone.Ink,
                    BarFraction = Mathf.Max(judgement.Progress, 0.02),
                    Caption = broken
                        ? "Strain reached three. This is the only primitive that can fail on reflex — hence the fallback."
                        : open
                            ? "Every pin on the shear line. Turn it before the tension slips."
                            : "Each miss adds strain. Three strikes ends the attempt.",
                    Rows = new List<StatVm>
                    {
                        new() { Key = "Pins", Value = $"{_pinsSet} / {l.Pins}" },
                        new() { Key = "Window", Value = Mathf.RoundToInt((float)judgement.Window) + "%",
                                Tone = judgement.Window > 22 ? Tone.Ink : Tone.Alert },
                        new() { Key = "Sweep", Value = judgement.Speed.ToString("0.00") + "×", Tone = Tone.Dim },
                        new() { Key = "Strain", Value = $"{_strain} / 3", Tone = _strain > 0 ? Tone.Alert : Tone.Ink },
                        new() { Key = "Noise", Value = _acts["rake"] ? "high" : "low",
                                Tone = _acts["rake"] ? Tone.Alert : Tone.Ink },
                        new() { Key = "Mode", Value = _rollMode ? "roll" : "timed", Tone = Tone.Dim },
                    },
                },

                [RegionId.Commit] = new CommitVm
                {
                    Label = open ? "Open the door" : broken ? "Give up" : "Locked",
                    Meta = open ? l.Name : "",
                    Disabled = !open && !broken,
                    Hint = open
                        ? "Opens quietly. Relocking behind you costs nothing."
                        : broken
                            ? "Leaves the broken pick behind as evidence."
                            : "Set every pin first.",
                },
            },
        };
    }
}
