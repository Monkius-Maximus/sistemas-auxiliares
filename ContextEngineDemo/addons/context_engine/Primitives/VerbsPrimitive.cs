using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// actions · verbs — skill-gated toggles that change how the panel behaves, not just what it
/// computes. Without this region the panel is a calculator and the character is irrelevant.
/// Locked verbs stay visible on purpose: "Ask for credit — needs standing 8" teaches that
/// reputation exists, is tracked per vendor and unlocks something, with no tutorial.
/// </summary>
public partial class VerbsPrimitive : Primitive
{
    protected override int Separation => 4;

    public override IReadOnlyList<(string Pad, string Key, string Label)> Prompts { get; } =
        new[] { ("A", "Enter", "Toggle action"), ("LB/RB", "Tab", "Next region") };

    protected override void Build(IRegionVm regionVm)
    {
        if (regionVm is not VerbsVm vm) return;

        foreach (var item in vm.Items)
            AddChild(Row(item));
    }

    private Control Row(VerbItem item)
    {
        var cell = Cell(Box(
            item.On ? T.AccD : T.Cell,
            item.On ? T.Acc : T.Ln2,
            1, 9, 8,
            item.Locked));
        cell.Modulate = new Color(1, 1, 1, item.Locked ? 0.5f : 1f);
        if (!item.Locked) cell.Pressed += () => Emit(new ActionToggled(item.Id));

        var body = Ui.VBox(3);

        var head = Ui.HBox(7);
        head.AddChild(Ui.Swatch(item.Locked ? T.Ln2 : item.On ? T.Acc : T.Mute, 8, 8));
        head.AddChild(Ui.Clipped(item.Name, 12, Ui.Semi, item.Locked ? T.Mute : T.Ink));
        if (!string.IsNullOrEmpty(item.Skill))
            head.AddChild(Ui.Text(item.Skill, 9, Ui.Regular, T.Mute, mono: true));
        body.AddChild(head);

        var note = Ui.Wrapped(item.Note, 10, Ui.Regular, T.Mute, 1.35f);
        body.AddChild(Ui.Pad(note, 15, 0, 0, 0));

        cell.AddChild(body);
        return cell;
    }
}
