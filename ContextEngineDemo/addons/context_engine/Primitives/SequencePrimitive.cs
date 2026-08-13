using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// sequence — ordered steps, where doing them in the wrong order is recorded and penalised.
/// Repair procedures, exam working, any recipe with a method. It is the cheapest way to make
/// a systemic panel reward knowing how the job is actually done.
/// </summary>
public partial class SequencePrimitive : Primitive
{
    protected override int Separation => 5;

    public override IReadOnlyList<(string Pad, string Key, string Label)> Prompts { get; } =
        new[] { ("A", "Enter", "Take step"), ("LB/RB", "Tab", "Next region") };

    protected override void Build(IRegionVm regionVm)
    {
        if (regionVm is not SequenceVm vm) return;

        foreach (var step in vm.Steps)
            AddChild(Step(step));

        var status = Ui.Wrapped(vm.Status, 11, Ui.Regular, vm.StatusAlert ? T.Acc : T.Mute, 1.4f);
        status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        status.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        var footer = Ui.HBox(10);
        footer.AddChild(Reset());
        footer.AddChild(status);
        AddChild(Ui.Pad(footer, 0, 3, 0, 0));
    }

    private Control Step(SequenceStep step)
    {
        var cell = Cell(Box(
            step.Taken ? T.Cell : T.Pnl,
            step.Wrong ? T.Acc : T.Ln2,
            1, 10, 10,
            step.Taken));
        cell.CustomMinimumSize = new Vector2(0, Pressable.MinHit);
        if (!step.Taken)
            cell.Pressed += () => Emit(new StepTaken(step.Index));

        var row = Ui.HBox(10);

        var ordBox = Ui.Panel(Ui.Box(step.Taken ? (step.Wrong ? T.Acc : T.Dim) : T.Ln2));
        ordBox.CustomMinimumSize = new Vector2(22, 22);
        ordBox.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var ord = Ui.Text(step.Ord, 11, Ui.Heavy, step.Taken ? T.BtnFg : T.Mute, mono: true);
        ord.HorizontalAlignment = HorizontalAlignment.Center;
        ord.VerticalAlignment = VerticalAlignment.Center;
        ordBox.AddChild(ord);
        row.AddChild(ordBox);

        var name = Ui.Wrapped(step.Name, 12, Ui.Semi, step.Taken ? T.Ink : T.Dim, 1.3f);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(name);

        row.AddChild(Ui.Text(step.Note, 10, Ui.Regular, step.Wrong ? T.Acc : T.Mute));

        cell.AddChild(row);
        return cell;
    }

    private Control Reset()
    {
        var button = Cell(Box(T.Pnl, T.Line, 1, 10, 6));
        button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        button.Pressed += () => Emit(new SequenceReset());
        button.AddChild(Ui.Upper("Start over", 10, T.Dim, Ui.Semi, 1));
        return button;
    }
}
