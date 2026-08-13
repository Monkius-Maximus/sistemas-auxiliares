using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// readout — is this good, and why. One headline number set larger than everything else, its
/// bar, and then reference: an optional context clock, an optional forecast strip, the factor
/// breakdown and a numeric stack. The stack of nine small numbers is reference, not the message.
/// </summary>
public partial class ReadoutPrimitive : Primitive
{
    protected override int Separation => 10;

    public override IReadOnlyList<(string Pad, string Key, string Label)> Prompts { get; } =
        new[] { ("A", "Enter", "Pause clock"), ("LB/RB", "Tab", "Next region") };

    protected override void Build(IRegionVm regionVm)
    {
        if (regionVm is not ReadoutVm vm) return;

        var value = vm.Quality.HasValue ? T.Quality(vm.Quality.Value) : T.Of(vm.ValueTone);

        AddChild(Headline(vm, value));
        if (vm.Timer != null) AddChild(Timer(vm.Timer));
        if (vm.Forecast != null) AddChild(Forecast(vm.Forecast));
        if (vm.Factors.Count > 0) AddChild(Factors(vm.Factors));
        if (vm.Rows.Count > 0) AddChild(Stack(vm.Rows));
    }

    private Control Headline(ReadoutVm vm, Color value)
    {
        var block = Ui.VBox(5);

        var row = Ui.HBox(8);
        row.AddChild(Ui.Clipped(vm.Headline, 13, Ui.Semi, T.Ink));
        var number = Ui.Text(vm.Value, 24, Ui.Heavy, value);
        number.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(number);
        block.AddChild(row);

        var bar = new MeterBar();
        bar.Set(vm.BarFraction, value, T.Cell, 6);
        block.AddChild(bar);

        block.AddChild(Ui.Wrapped(vm.Caption, 11, Ui.Regular, T.Mute, 1.4f));
        return block;
    }

    private Control Timer(TimerVm timer)
    {
        var color = timer.Alert ? T.Acc : T.Ink;

        var block = Ui.VBox(6);
        block.AddChild(Ui.Rule(T.Ln2, 1));
        block.AddChild(Ui.Spacer(0, 3));

        var row = Ui.HBox(9);
        row.AddChild(Ui.Upper(timer.Label, 10, T.Mute, Ui.Semi, 2));
        row.AddChild(Ui.Fill());
        row.AddChild(Ui.Text(timer.Clock, 24, Ui.Heavy, color, mono: true));

        var button = Cell(Box(T.Pnl2, T.Line, 1, 9, 6));
        button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        button.Pressed += () => Emit(new TimerToggled());
        button.AddChild(Ui.Text(timer.ButtonLabel, 10, Ui.Semi, T.Dim));
        row.AddChild(button);
        block.AddChild(row);

        var bar = new MeterBar();
        bar.Set(timer.Fraction, color, T.Cell, 8);
        block.AddChild(bar);

        block.AddChild(Ui.Wrapped(timer.Caption, 11, Ui.Regular, T.Mute, 1.4f));
        return block;
    }

    private Control Forecast(ForecastVm forecast)
    {
        var block = Ui.VBox(5);

        var head = Ui.HBox(8);
        head.AddChild(Ui.Upper(forecast.Title, 10, T.Mute, Ui.Semi, 2));
        head.AddChild(Ui.Fill());
        head.AddChild(Ui.Text(forecast.Delta, 11, Ui.Semi, forecast.DeltaAlert ? T.Acc : T.Dim, mono: true));
        block.AddChild(head);

        var chart = new ForecastStrip();
        chart.Set(forecast.Bars, forecast.Threshold, T.Ink, T.Acc);
        block.AddChild(Ui.Panel(Ui.Box(T.Cell, T.Ln2, 1, 3, 3), chart));

        var axis = Ui.HBox(0);
        axis.AddChild(Ui.Text(forecast.From, 9, Ui.Regular, T.Mute, mono: true));
        axis.AddChild(Ui.Fill());
        axis.AddChild(Ui.Text(forecast.To, 9, Ui.Regular, T.Mute, mono: true));
        block.AddChild(axis);
        return block;
    }

    private Control Factors(List<FactorVm> factors)
    {
        var block = Ui.VBox(4);
        foreach (var factor in factors)
        {
            var row = Ui.HBox(8);

            var name = Ui.Text(factor.Name, 11, Ui.Regular, T.Mute);
            name.CustomMinimumSize = new Vector2(74, 0);
            row.AddChild(name);

            var color = factor.Known ? T.Quality(factor.Value) : T.Mute;

            var bar = new MeterBar();
            bar.Set(factor.Value, color, T.Cell, 3);
            bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            bar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(bar);

            var pct = Ui.Text(factor.Known ? Mathf.RoundToInt(factor.Value * 100).ToString() : "??",
                10, Ui.Semi, color, mono: true);
            pct.CustomMinimumSize = new Vector2(26, 0);
            pct.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(pct);

            block.AddChild(row);
        }
        return block;
    }

    private Control Stack(List<StatVm> rows)
    {
        var grid = Ui.Grid(3, 1, 1);
        foreach (var stat in rows)
        {
            var cell = Ui.VBox(2);
            cell.AddChild(Ui.Clipped(stat.Key.ToUpperInvariant(), 9, Ui.Semi, T.Mute, 1));
            cell.AddChild(Ui.Clipped(stat.Value, 14, Ui.Semi, T.Of(stat.Tone)));

            var box = Ui.Panel(Ui.Box(T.Cell, null, 0, 6, 6), cell);
            box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            grid.AddChild(box);
        }
        return Ui.Panel(Ui.Box(T.Ln2, T.Ln2, 1), grid);
    }
}

/// <summary>
/// readout.forecast — value over time, as bars. Turns any evaluator into a projection: how
/// this dish spoils, how this crop grows, how this engine holds up over the next eight trips.
/// </summary>
public partial class ForecastStrip : Control
{
    private readonly List<double> _values = new();
    private double _threshold = 0.5;
    private Color _ok = Colors.White;
    private Color _bad = Colors.Red;

    public void Set(IEnumerable<double> values, double threshold, Color ok, Color bad)
    {
        _values.Clear();
        _values.AddRange(values);
        _threshold = threshold;
        _ok = ok;
        _bad = bad;
        CustomMinimumSize = new Vector2(0, 48);
        MouseFilter = MouseFilterEnum.Ignore;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_values.Count == 0) return;

        const float gap = 2f;
        var width = (Size.X - gap * (_values.Count - 1)) / _values.Count;

        for (var i = 0; i < _values.Count; i++)
        {
            var value = Mathf.Clamp(_values[i], 0, 1);
            var height = Mathf.Max(2f, (float)(Size.Y * value));
            var x = i * (width + gap);
            DrawRect(new Rect2(x, Size.Y - height, width, height), value >= _threshold ? _ok : _bad);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }
}
