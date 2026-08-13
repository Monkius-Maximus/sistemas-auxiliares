using System.Collections.Generic;
using System.Linq;

namespace ContextEngine.Demo;

/// <summary>
/// The verb toggles for one context. Three flavours are worth distinguishing when authoring,
/// because they cost different amounts to build: <em>information</em> verbs reveal hidden state,
/// <em>modifier</em> verbs change the maths, and <em>view</em> verbs only reorder or filter and
/// need no gate at all.
/// </summary>
public sealed class ActionSet
{
    private readonly Dictionary<string, bool> _on = new();

    public ActionSet(params (string Id, bool On)[] defaults)
    {
        foreach (var (id, on) in defaults) _on[id] = on;
    }

    public bool this[string id] => _on.TryGetValue(id, out var on) && on;

    public void Toggle(string id) => _on[id] = !this[id];

    public void Set(string id, bool on) => _on[id] = on;
}

/// <summary>One row of the verbs region, before the view-model is assembled.</summary>
public sealed record Verb(string Id, string Name, string Skill, string Note, bool Locked = false, bool? On = null);

public static class ModuleSupport
{
    /// <summary>
    /// Every context should carry at least one locked verb the player cannot yet use — it is
    /// the cheapest tutorial in the game, and it costs nothing to author.
    /// </summary>
    public static VerbsVm Verbs(ActionSet actions, params Verb[] verbs)
    {
        var items = verbs.Select(v => new VerbItem
        {
            Id = v.Id,
            Name = v.Name,
            Skill = v.Skill,
            Note = v.Note,
            Locked = v.Locked,
            On = !v.Locked && (v.On ?? actions[v.Id]),
        }).ToList();

        return new VerbsVm
        {
            Count = items.Count(i => i.On) + " on",
            Items = items,
        };
    }

    public static PickerVm Picker<T>(string title, IEnumerable<T> options, string selectedId, string note,
        System.Func<T, (string Id, string Name, Godot.Color Tint)> project)
        => new()
        {
            Title = title,
            Note = note,
            Options = options.Select(o =>
            {
                var (id, name, tint) = project(o);
                return new PickerOption { Id = id, Name = name, Tint = tint, Selected = id == selectedId };
            }).ToList(),
        };

    /// <summary>
    /// The sequence primitive's view-model plus the two numbers the evaluator wants back:
    /// whether the order held, and how far through the player is.
    /// </summary>
    public static (SequenceVm Vm, bool InOrder, bool Done, int Slips) Sequence(
        IReadOnlyList<string> steps, IReadOnlyList<int> taken, string title, string idleMessage)
    {
        var rows = new List<SequenceStep>();
        for (var i = 0; i < steps.Count; i++)
        {
            var position = taken.ToList().IndexOf(i);
            var isTaken = position >= 0;
            var wrong = isTaken && position != i;

            rows.Add(new SequenceStep
            {
                Index = i,
                Name = steps[i],
                Ord = isTaken ? (position + 1).ToString() : "·",
                Taken = isTaken,
                Wrong = wrong,
                Note = isTaken ? (wrong ? "out of order" : $"step {i + 1}") : "not done",
            });
        }

        var inOrder = true;
        var slips = 0;
        for (var i = 0; i < taken.Count; i++)
            if (taken[i] != i) { inOrder = false; slips++; }

        var done = taken.Count == steps.Count;
        var status = taken.Count == 0
            ? idleMessage
            : done
                ? (inOrder ? "Procedure followed to the letter." : $"{slips} step(s) out of order — the result will show it.")
                : (inOrder ? $"{taken.Count} of {steps.Count}, in order." : "Out of order already.");

        var vm = new SequenceVm
        {
            Title = title,
            Count = $"{taken.Count} / {steps.Count}",
            Steps = rows,
            Status = status,
            StatusAlert = taken.Count > 0 && !inOrder,
        };

        return (vm, inOrder, done, slips);
    }

    public static string Clock(int seconds)
    {
        seconds = System.Math.Max(0, seconds);
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    /// <summary>Applies a stepper delta to a quantity map, dropping the key when it hits zero.</summary>
    public static void Bump(Dictionary<string, int> map, string id, int delta, int max)
    {
        var next = System.Math.Clamp((map.TryGetValue(id, out var n) ? n : 0) + delta, 0, max);
        if (next == 0) map.Remove(id);
        else map[id] = next;
    }
}
