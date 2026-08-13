using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// grid.dual — two lists and a move axis. Transfer, loot, trade-in. Drag is never the only way
/// to do anything: click-to-move is the primary verb here, which is also what makes the pane
/// pair work on a controller as two focusable lists.
/// </summary>
public partial class DualGridPrimitive : Primitive
{
    public override IReadOnlyList<(string Pad, string Key, string Label)> Prompts { get; } =
        new[] { ("A", "Click", "Move stack"), ("LB/RB", "Tab", "Next region") };

    protected override void Build(IRegionVm regionVm)
    {
        if (regionVm is not DualGridVm vm) return;

        var row = Ui.HBox(10);
        row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(Pane(vm.Left));
        row.AddChild(Axis());
        row.AddChild(Pane(vm.Right));
        AddChild(row);
    }

    private Control Pane(DualPane pane)
    {
        var column = Ui.VBox(6);
        column.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var head = Ui.HBox(8);
        head.AddChild(Ui.Upper(pane.Name, 11, T.Ink, Ui.Semi, 1));
        head.AddChild(Ui.Fill());
        head.AddChild(Ui.Text(pane.Load, 10, Ui.Semi, T.Mute, mono: true));
        column.AddChild(head);

        var well = Ui.VBox(3);
        well.SizeFlagsVertical = SizeFlags.ExpandFill;
        foreach (var item in pane.Items)
            well.AddChild(Row(item, pane));

        if (!string.IsNullOrEmpty(pane.Empty))
            well.AddChild(Ui.Pad(Ui.Wrapped(pane.Empty, 11, Ui.Regular, T.Mute, 1.4f), 6, 4, 6, 4));

        var box = Ui.Panel(Ui.Box(T.Cell, T.Ln2, 1, 5, 5), well);
        box.CustomMinimumSize = new Vector2(0, 250);
        box.SizeFlagsVertical = SizeFlags.ExpandFill;
        column.AddChild(box);
        return column;
    }

    private Control Row(DualItem item, DualPane pane)
    {
        var cell = Cell(Box(T.Pnl2, T.Ln2, 1, 6, 5));
        cell.Pressed += () => Emit(new Moved(item.Id, pane.Direction));

        var row = Ui.HBox(8);
        row.AddChild(Ui.Swatch(item.Tint, 22, 22));
        row.AddChild(Ui.Clipped(item.Name, 11, Ui.Semi, T.Ink));
        row.AddChild(Ui.Text(item.Sub, 10, Ui.Regular, T.Mute, mono: true));
        row.AddChild(Ui.Text(item.Qty, 12, Ui.Heavy, T.Dim));
        row.AddChild(Ui.Text(pane.Arrow, 12, Ui.Semi, T.Acc));
        cell.AddChild(row);
        return cell;
    }

    private Control Axis()
    {
        var column = Ui.VBox(6);
        column.CustomMinimumSize = new Vector2(46, 0);
        column.SizeFlagsVertical = SizeFlags.ShrinkBegin;

        var glyph = Ui.Text("⇄", 18, Ui.Heavy, T.Acc);
        glyph.HorizontalAlignment = HorizontalAlignment.Center;

        var caption = Ui.Wrapped("click a row to move", 9, Ui.Regular, T.Mute, 1.2f);
        caption.HorizontalAlignment = HorizontalAlignment.Center;

        column.AddChild(Ui.Spacer(46, 40));
        column.AddChild(glyph);
        column.AddChild(caption);
        return column;
    }
}
