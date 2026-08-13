using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// checklist — have versus need, one row at a time. Crafting requirements, a shop receipt, a
/// pin stack, a quest turn-in. Red on a row means "this is what is stopping you", the same
/// meaning red carries everywhere else in the panel.
/// </summary>
public partial class ChecklistPrimitive : Primitive
{
    public override IReadOnlyList<(string Pad, string Key, string Label)> Prompts { get; } =
        new[] { ("LB/RB", "Tab", "Next region") };

    protected override void Build(IRegionVm regionVm)
    {
        if (regionVm is not ChecklistVm vm) return;

        // A 1 px gap over an Ln2 ground gives the hairline separators without eleven borders.
        var rows = Ui.VBox(1);
        foreach (var row in vm.Rows)
            rows.AddChild(Row(row));

        AddChild(Ui.Panel(Ui.Box(T.Ln2, T.Ln2, 1), rows));
    }

    private Control Row(ChecklistRow row)
    {
        var body = Ui.HBox(10);

        body.AddChild(Ui.Swatch(row.MarkOff ? T.Ln2 : row.MarkAlert ? T.Acc : T.Dim, 9, 9));
        if (row.Tint.HasValue) body.AddChild(Ui.Swatch(row.Tint.Value, 22, 22));
        body.AddChild(Ui.Clipped(row.Name, 12, Ui.Semi, T.Ink));
        if (!string.IsNullOrEmpty(row.Note))
            body.AddChild(Ui.Text(row.Note, 10, Ui.Regular, T.Mute));
        body.AddChild(Ui.Text(row.Tally, 12, Ui.Semi, T.Of(row.TallyTone), mono: true));

        return Ui.Panel(Ui.Box(T.Cell, null, 0, 10, 8), body);
    }
}

/// <summary>
/// text — the escape hatch. A region that needs prose rather than a widget: a designer note,
/// or the explanation of a primitive that is currently switched off.
/// </summary>
public partial class TextPrimitive : Primitive
{
    public override IReadOnlyList<(string Pad, string Key, string Label)> Prompts { get; } =
        new[] { ("LB/RB", "Tab", "Next region") };

    protected override void Build(IRegionVm regionVm)
    {
        if (regionVm is not TextVm vm) return;

        var body = Ui.Wrapped(vm.Body, 12, Ui.Regular, T.Dim, 1.55f);
        body.CustomMinimumSize = new Vector2(0, 0);
        AddChild(body);
    }
}
