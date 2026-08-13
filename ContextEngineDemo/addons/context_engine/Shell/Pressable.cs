using System;
using Godot;

namespace ContextEngine;

/// <summary>
/// The one interactive surface in the panel: a focusable box that can hold any layout.
/// Tiles, verb rows, answers, hotspots and steppers are all this control with a different
/// child and a different style box, which is how the focus ring, the hit target and the
/// click-to-move rule stay identical everywhere without being reimplemented.
/// </summary>
public partial class Pressable : PanelContainer
{
    /// <summary>Fired by mouse click, by ui_accept, and by the gamepad's A on the focused cell.</summary>
    public event Action Pressed;

    /// <summary>Steppers on the focused cell. Delta is signed and already includes the bulk modifier.</summary>
    public event Action<int> Stepped;

    private StyleBoxFlat _normal;
    private StyleBoxFlat _hover;
    private Color _ring = Colors.Transparent;
    private bool _locked;
    private bool _hovered;

    private readonly HoldRepeat _up = new();
    private readonly HoldRepeat _down = new();

    /// <summary>Console-legible hit target. 44 px at 1080p is the floor for anything you aim at.</summary>
    public static readonly float MinHit = 44f;

    /// <summary>Set when this control accepts ce_increment / ce_decrement while focused.</summary>
    public bool Steppable { get; set; }

    public bool Locked
    {
        get => _locked;
        set
        {
            _locked = value;
            FocusMode = value ? FocusModeEnum.None : FocusModeEnum.All;
            MouseDefaultCursorShape = value ? CursorShape.Forbidden : CursorShape.PointingHand;
        }
    }

    public void Style(StyleBoxFlat normal, Color ring, StyleBoxFlat hover = null)
    {
        _normal = normal;
        _hover = hover;
        _ring = ring;
        AddThemeStyleboxOverride("panel", normal);
        FocusMode = _locked ? FocusModeEnum.None : FocusModeEnum.All;
        MouseDefaultCursorShape = _locked ? CursorShape.Forbidden : CursorShape.PointingHand;
    }

    public override void _Ready()
    {
        MouseEntered += () => { _hovered = true; Repaint(); };
        MouseExited += () => { _hovered = false; Repaint(); };
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
        SetProcess(false);
    }

    private void Repaint()
    {
        if (_normal == null) return;
        AddThemeStyleboxOverride("panel", _hovered && !_locked && _hover != null ? _hover : _normal);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_locked) return;

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            GrabFocus();
            Pressed?.Invoke();
            AcceptEvent();
            return;
        }

        // Scroll wheel over a tile is the mouse accelerator for the steppers.
        if (Steppable && @event is InputEventMouseButton { Pressed: true } wheel &&
            wheel.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            Stepped?.Invoke(wheel.ButtonIndex == MouseButton.WheelUp ? BulkFactor() : -BulkFactor());
            AcceptEvent();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_locked || !HasFocus()) return;

        if (@event.IsActionPressed("ui_accept"))
        {
            Pressed?.Invoke();
            AcceptEvent();
            return;
        }

        if (!Steppable) return;

        // A / X on a focused slot, held to repeat with acceleration.
        if (@event.IsActionPressed("ce_increment") || @event.IsActionPressed("ce_decrement"))
        {
            var up = @event.IsActionPressed("ce_increment");
            (up ? _up : _down).Begin();
            Stepped?.Invoke(up ? BulkFactor() : -BulkFactor());
            SetProcess(true);
            AcceptEvent();
        }
        else if (@event.IsActionReleased("ce_increment") || @event.IsActionReleased("ce_decrement"))
        {
            (@event.IsActionReleased("ce_increment") ? _up : _down).End();
            if (!_up.Held && !_down.Held) SetProcess(false);
        }
    }

    public override void _Process(double delta)
    {
        if (!HasFocus() || _locked)
        {
            _up.End();
            _down.End();
            SetProcess(false);
            return;
        }

        for (var i = 0; i < _up.Advance(delta); i++) Stepped?.Invoke(BulkFactor());
        for (var i = 0; i < _down.Advance(delta); i++) Stepped?.Invoke(-BulkFactor());
    }

    /// <summary>Shift on the keyboard, RT on the pad: +5 instead of +1.</summary>
    private static int BulkFactor()
        => Input.IsActionPressed("ce_bulk") || Input.IsKeyPressed(Key.Shift) ? 5 : 1;

    public override void _Draw()
    {
        // The ring is drawn outside the box so it survives on a tinted fill.
        if (HasFocus())
            DrawRect(new Rect2(new Vector2(-3, -3), Size + new Vector2(6, 6)), _ring, false, 2f);
    }
}

/// <summary>Hold-to-repeat with acceleration: a slow first repeat, then it speeds up.</summary>
public sealed class HoldRepeat
{
    private const double FirstDelay = 0.38;
    private const double SlowInterval = 0.12;
    private const double FastInterval = 0.035;
    private const double RampTime = 1.4;

    private double _held = -1;
    private double _next;

    public bool Held => _held >= 0;

    public void Begin()
    {
        _held = 0;
        _next = FirstDelay;
    }

    public void End() => _held = -1;

    /// <summary>How many repeats to fire this frame.</summary>
    public int Advance(double delta)
    {
        if (_held < 0) return 0;
        _held += delta;

        var fired = 0;
        while (_held >= _next && fired < 12)
        {
            fired++;
            var ramp = Mathf.Clamp((_held - FirstDelay) / RampTime, 0, 1);
            _next += Mathf.Lerp(SlowInterval, FastInterval, ramp);
        }
        return fired;
    }
}
