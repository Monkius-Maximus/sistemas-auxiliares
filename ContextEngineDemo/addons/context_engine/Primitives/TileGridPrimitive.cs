using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// grid.qty and grid.select — the same tile, with or without steppers. Quantities for cooking
/// and shopping; single-select for blueprints and recipes. The tile states its own stock line
/// and the header states the slot budget, because a limit the player cannot see is a limit
/// they will resent.
/// </summary>
public partial class TileGridPrimitive : Primitive
{
    private bool _steppers;

    protected override int Separation => 5;

    public override IReadOnlyList<(string Pad, string Key, string Label)> Prompts =>
        _steppers
            ? new[] { ("A / X", "Click ± · Wheel", "+1 / −1"), ("RT + A", "Shift", "+5"), ("LB/RB", "Tab", "Next region") }
            : new[] { ("A", "Enter", "Select"), ("LB/RB", "Tab", "Next region") };

    protected override void Build(IRegionVm regionVm)
    {
        if (regionVm is not TileGridVm vm) return;
        _steppers = vm.Steppers;

        var grid = new AutoFillGrid { MinItemWidth = 112, HSep = 5, VSep = 5 };
        grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        foreach (var tile in vm.Tiles)
            grid.AddChild(Tile(tile, vm.Steppers));
        AddChild(grid);
    }

    private Control Tile(TileVm tile, bool steppers)
    {
        var cell = Cell(Box(
            tile.Active ? T.AccD : T.Cell,
            tile.Active ? T.Acc : T.Ln2,
            1, 7, 7));
        cell.Modulate = new Color(1, 1, 1, tile.Dimmed ? 0.4f : 1f);
        cell.Steppable = steppers;
        cell.Pressed += () => Emit(steppers ? new QtyChanged(tile.Id, 1) : new SelectionChanged(tile.Id));
        if (steppers)
            cell.Stepped += delta => Step(tile, delta);

        var body = Ui.VBox(5);

        var head = Ui.HBox(7);
        head.AddChild(Ui.Swatch(tile.Tint, 30, 30, 1f, SizeFlags.ShrinkBegin));

        var text = Ui.VBox(1);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        text.AddChild(Ui.Clipped(tile.Name, 11, Ui.Semi, T.Ink));
        text.AddChild(Ui.Clipped(tile.Sub, 10, Ui.Regular, tile.SubAlert ? T.Acc : T.Mute));
        head.AddChild(text);

        var qty = Ui.Text(tile.Qty, 14, Ui.Heavy, tile.Active ? T.Acc : T.Mute);
        qty.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        head.AddChild(qty);
        body.AddChild(head);

        if (steppers)
        {
            var steps = Ui.HBox(3);
            steps.AddChild(Stepper("−", tile, -1, tile.CanSub));
            steps.AddChild(Stepper("+", tile, 1, tile.CanAdd));
            body.AddChild(steps);
        }

        cell.AddChild(body);
        return cell;
    }

    private Control Stepper(string glyph, TileVm tile, int direction, bool enabled)
    {
        var button = new Pressable();
        button.Style(Ui.Box(T.Pnl2, T.Ln2, 1, 0, 3), T.Acc, Ui.Box(T.Pnl2.Lerp(T.Ink, 0.07f), T.Ln2, 1, 0, 3));
        button.FocusMode = FocusModeEnum.None; // the tile is the focusable unit; these are for the mouse
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        button.CustomMinimumSize = new Vector2(0, 22);
        button.Pressed += () => Step(tile, direction * (Input.IsKeyPressed(Key.Shift) ? 5 : 1));

        var label = Ui.Text(glyph, 12, Ui.Semi, enabled ? T.Ink : T.Mute, mono: true);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        button.AddChild(label);
        return button;
    }

    private void Step(TileVm tile, int delta)
    {
        if (delta > 0 && !tile.CanAdd) return;
        if (delta < 0 && !tile.CanSub) return;
        Emit(new QtyChanged(tile.Id, delta));
    }
}
