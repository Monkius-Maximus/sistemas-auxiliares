using System;
using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// A primitive is a self-contained widget with a declared data shape, a set of emitted intents
/// and defined focus behaviour. Two rules keep the catalogue from rotting: a primitive must
/// earn its place in at least two contexts, and a primitive never talks to game systems — it
/// receives a view-model and emits an intent, and the module does the rest.
/// </summary>
public abstract partial class Primitive : VBoxContainer
{
    public event Action<Intent> Emitted;

    protected Skin T { get; private set; } = Skin.Dark;

    /// <summary>Focusable cells, in traversal order. The shell uses these to restore focus.</summary>
    protected readonly List<Pressable> Cells = new();

    private int _lastFocused;

    /// <summary>The prompt-bar legend for this primitive while it holds focus.</summary>
    public virtual IReadOnlyList<(string Pad, string Key, string Label)> Prompts { get; } =
        new[] { ("A", "Click", "Select") };

    protected void Emit(Intent intent) => Emitted?.Invoke(intent);

    public void Bind(IRegionVm vm, Skin skin)
    {
        var hadFocus = false;
        for (var i = 0; i < Cells.Count; i++)
        {
            if (!IsInstanceValid(Cells[i]) || !Cells[i].HasFocus()) continue;
            _lastFocused = i;
            hadFocus = true;
        }

        T = skin;
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
        Cells.Clear();

        AddThemeConstantOverride("separation", Separation);
        Build(vm);

        if (hadFocus) FocusCell(_lastFocused);
    }

    protected abstract void Build(IRegionVm vm);

    /// <summary>Vertical rhythm inside the primitive. Most sit at 9–10 px, grids tighter.</summary>
    protected virtual int Separation => 9;

    /// <summary>Registers a cell so the focus graph and focus restoration can see it.</summary>
    protected Pressable Cell(Pressable cell)
    {
        var index = Cells.Count;
        Cells.Add(cell);
        cell.FocusEntered += () => _lastFocused = index;
        return cell;
    }

    /// <summary>Where focus lands when the player jumps into this region.</summary>
    public bool FocusCell(int index)
    {
        if (Cells.Count == 0) return false;
        var cell = Cells[Mathf.Clamp(index, 0, Cells.Count - 1)];
        if (!IsInstanceValid(cell) || cell.Locked) return FocusFirst();
        Grab(cell);
        return true;
    }

    public bool FocusFirst()
    {
        foreach (var cell in Cells)
        {
            if (!IsInstanceValid(cell) || cell.Locked) continue;
            Grab(cell);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Deferred, and re-checked on arrival: a rebuild between the request and the flush would
    /// otherwise leave us grabbing focus on a freed cell.
    /// </summary>
    private static void Grab(Pressable cell)
        => Callable.From(() =>
        {
            if (IsInstanceValid(cell) && cell.IsInsideTree() && !cell.Locked) cell.GrabFocus();
        }).CallDeferred();

    /// <summary>Re-entering a region restores the cell the player left from.</summary>
    public bool FocusRestore() => FocusCell(_lastFocused);

    public bool HasFocusable
    {
        get
        {
            foreach (var cell in Cells)
                if (IsInstanceValid(cell) && !cell.Locked) return true;
            return false;
        }
    }

    // ── shared construction helpers ──────────────────────────────────────────

    /// <summary>A focusable box: normal fill, accent ring, and a hover tint for the mouse.</summary>
    protected Pressable Box(Color bg, Color border, int borderWidth = 1, int padH = 0, int padV = 0, bool locked = false)
    {
        var cell = new Pressable { Locked = locked };
        var normal = Ui.Box(bg, border, borderWidth, padH, padV);
        var hover = Ui.Box(bg.Lerp(T.Ink, 0.06f), border, borderWidth, padH, padV);
        cell.Style(normal, T.Acc, hover);
        return cell;
    }

    /// <summary>Colour resolution for a value that may be hidden behind an information verb.</summary>
    protected Color QualityColor(double? quality) => quality.HasValue ? T.Quality(quality.Value) : T.Mute;
}
