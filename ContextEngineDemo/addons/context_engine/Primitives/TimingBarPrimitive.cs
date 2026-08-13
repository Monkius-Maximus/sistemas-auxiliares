using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// timing.bar — a sweeping marker and a target window, and the only real-time primitive in the
/// catalogue. It breaks the pause-safe, pure-evaluator rule, so it is isolated here: the sweep
/// phase lives inside this widget and leaves as a position on a strike, which keeps the module
/// and its evaluator pure. It is never the only path to a goal — the accessibility fallback
/// swaps the sweep for a roll with the same expected value, and that mode is built in, not bolted on.
/// </summary>
public partial class TimingBarPrimitive : Primitive
{
    private const float TrackHeight = 72f;

    private Control _marker;
    private double _phase;
    private double _speed = 1;
    private bool _live;

    protected override int Separation => 11;

    public override IReadOnlyList<(string Pad, string Key, string Label)> Prompts =>
        _live
            ? new[] { ("A", "Enter", "Strike"), ("X", "R", "Reset"), ("LB/RB", "Tab", "Next region") }
            : new[] { ("A", "Enter", "Roll"), ("X", "R", "Reset"), ("LB/RB", "Tab", "Next region") };

    protected override void Build(IRegionVm regionVm)
    {
        if (regionVm is not TimingBarVm vm) return;

        _live = vm.Live;
        _speed = vm.Speed;
        _marker = null;

        AddChild(vm.Live ? Sweep(vm) : Roll(vm));
        AddChild(Buttons(vm));
        AddChild(Ui.Wrapped(vm.Message, 12, Ui.Regular, vm.MessageAlert ? T.Acc : T.Mute, 1.45f));

        SetProcess(vm.Live);
    }

    private Control Sweep(TimingBarVm vm)
    {
        var track = new Pattern
        {
            Mode = Pattern.Kind.Ticks,
            Spacing = 20,
            Line = new Color(0.5f, 0.5f, 0.5f, 0.16f),
            Ground = T.Cell,
            CustomMinimumSize = new Vector2(0, TrackHeight),
            ClipContents = true,
        };

        var left = (float)Mathf.Clamp((vm.ZoneCentre - vm.ZoneWidth / 2) / 100.0, 0, 1);
        var right = (float)Mathf.Clamp((vm.ZoneCentre + vm.ZoneWidth / 2) / 100.0, 0, 1);

        var zone = new Panel();
        zone.AddThemeStyleboxOverride("panel", ZoneBox());
        zone.AnchorLeft = left;
        zone.AnchorRight = right;
        zone.AnchorTop = 0;
        zone.AnchorBottom = 1;
        zone.OffsetLeft = zone.OffsetTop = zone.OffsetRight = zone.OffsetBottom = 0;
        zone.MouseFilter = MouseFilterEnum.Ignore;
        track.AddChild(zone);

        _marker = new ColorRect { Color = T.Ink, MouseFilter = MouseFilterEnum.Ignore };
        _marker.AnchorTop = 0;
        _marker.AnchorBottom = 1;
        _marker.OffsetLeft = -1;
        _marker.OffsetRight = 2;
        _marker.OffsetTop = _marker.OffsetBottom = 0;
        track.AddChild(_marker);
        PlaceMarker();

        var caption = Ui.Upper("Tension sweep · real time", 9, T.Mute, Ui.Semi, 1);
        caption.SetAnchorsPreset(LayoutPreset.BottomLeft);
        caption.OffsetLeft = 7;
        caption.OffsetTop = -16;
        caption.OffsetBottom = -5;
        track.AddChild(caption);

        return Ui.Panel(Ui.Box(T.Cell, T.Ln2, 1), track);
    }

    private StyleBoxFlat ZoneBox()
    {
        var box = Ui.Box(T.AccD);
        box.BorderColor = T.Acc;
        box.BorderWidthLeft = 2;
        box.BorderWidthRight = 2;
        return box;
    }

    private Control Roll(TimingBarVm vm)
    {
        var body = Ui.VBox(7);
        body.AddChild(Ui.Upper("Accessibility · timing bar off", 10, T.Mute, Ui.Semi, 2));
        body.AddChild(Ui.Text(vm.Odds, 34, Ui.Heavy, T.Acc));
        body.AddChild(Ui.Wrapped(vm.RollBlurb, 12, Ui.Regular, T.Dim, 1.5f));
        return Ui.Panel(Ui.Box(T.Cell, T.Ln2, 1, 16, 15), body);
    }

    private Control Buttons(TimingBarVm vm)
    {
        var row = Ui.HBox(6);

        var act = Cell(Box(vm.ActionEnabled ? T.Acc : T.Cell, T.Ln2, 0, 14, 13, !vm.ActionEnabled));
        act.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        act.CustomMinimumSize = new Vector2(0, Pressable.MinHit);
        act.Pressed += () =>
        {
            if (!vm.ActionEnabled) return;
            Emit(_live ? new TimingStrike(MarkerPercent()) : new TimingRoll());
        };
        act.AddChild(Ui.Text(_live ? vm.ActionLabel : vm.RollLabel, 15, Ui.Heavy,
            vm.ActionEnabled ? T.BtnFg : T.Mute));
        row.AddChild(act);

        var reset = Cell(Box(T.Pnl, T.Line, 1, 15, 13));
        reset.CustomMinimumSize = new Vector2(0, Pressable.MinHit);
        reset.Pressed += () => Emit(new TimingReset());
        reset.AddChild(Ui.Text("Reset", 12, Ui.Semi, T.Dim));
        row.AddChild(reset);

        return row;
    }

    /// <summary>Marker position 0–100. A triangle wave, so the return sweep is playable too.</summary>
    private double MarkerPercent() => (_phase < 0.5 ? _phase * 2 : 2 - _phase * 2) * 100.0;

    private void PlaceMarker()
    {
        if (_marker == null || !IsInstanceValid(_marker)) return;
        var x = (float)(MarkerPercent() / 100.0);
        _marker.AnchorLeft = x;
        _marker.AnchorRight = x;
        _marker.OffsetLeft = -1;
        _marker.OffsetRight = 2;
    }

    public override void _Process(double delta)
    {
        if (!_live) return;
        _phase = Mathf.PosMod(_phase + delta * _speed * 0.55, 1.0);
        PlaceMarker();
    }
}
