using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// commit — one action per context, and the only thing in the panel that writes to the world.
/// It sits directly under the readout on purpose: the player should never have to move their
/// eyes across the panel to check the consequence of the thing their thumb is resting on.
/// When it refuses, it says why in the line underneath — never a greyed-out button and silence.
/// </summary>
public partial class CommitPrimitive : Primitive
{
    protected override int Separation => 7;

    // The shell always appends Commit and Close, so this region adds nothing of its own.
    public override IReadOnlyList<(string Pad, string Key, string Label)> Prompts { get; } =
        System.Array.Empty<(string, string, string)>();

    protected override void Build(IRegionVm regionVm)
    {
        if (regionVm is not CommitVm vm) return;

        var button = Cell(Box(vm.Disabled ? T.Cell : T.Acc, T.Ln2, 0, 14, 14, vm.Disabled));
        button.CustomMinimumSize = new Vector2(0, Pressable.MinHit);
        if (!vm.Disabled) button.Pressed += () => Emit(new Committed());

        var row = Ui.HBox(10);
        row.AddChild(Ui.Clipped(vm.Label, 16, Ui.Heavy, vm.Disabled ? T.Mute : T.BtnFg));

        var meta = Ui.Text(vm.Meta, 12, Ui.Semi, vm.Disabled ? T.Mute : T.BtnFg, mono: true);
        meta.Modulate = new Color(1, 1, 1, 0.75f);
        row.AddChild(meta);

        button.AddChild(row);
        AddChild(button);

        AddChild(Ui.Wrapped(vm.Hint, 11, Ui.Regular, vm.Disabled ? T.Acc : T.Mute, 1.4f));
    }
}
