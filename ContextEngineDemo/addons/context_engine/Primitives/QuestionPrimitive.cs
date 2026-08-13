using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// question — a prompt, single-select answers and a pager. Exams and quizzes today; any
/// A/B/C/D choice tomorrow, which is what makes a dialogue context a definition file rather
/// than a new screen.
/// </summary>
public partial class QuestionPrimitive : Primitive
{
    protected override int Separation => 10;

    public override IReadOnlyList<(string Pad, string Key, string Label)> Prompts { get; } =
        new[] { ("A", "Enter", "Answer"), ("LB/RB", "Tab", "Next region") };

    protected override void Build(IRegionVm regionVm)
    {
        if (regionVm is not QuestionVm vm) return;

        AddChild(Pager(vm));
        AddChild(Prompt(vm));

        var options = Ui.VBox(4);
        foreach (var option in vm.Options)
            options.AddChild(Option(vm, option));
        AddChild(options);
    }

    private Control Pager(QuestionVm vm)
    {
        var row = Ui.HBox(4);
        foreach (var page in vm.Pager)
        {
            var cell = Cell(Box(
                page.Current ? T.Acc : page.Answered ? T.AccD : T.Cell,
                page.Current || page.Answered ? T.Acc : T.Ln2));
            cell.CustomMinimumSize = new Vector2(30, 30);
            var index = page.Index;
            cell.Pressed += () => Emit(new PagerChanged(index));

            var label = Ui.Text(page.Label, 12, Ui.Semi,
                page.Current ? T.BtnFg : page.Answered ? T.Ink : T.Mute, mono: true);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            cell.AddChild(label);
            row.AddChild(cell);
        }

        row.AddChild(Ui.Fill());
        row.AddChild(Ui.Upper(vm.Topic, 10, T.Mute, Ui.Semi, 2));
        return row;
    }

    private Control Prompt(QuestionVm vm)
    {
        var row = Ui.HBox(11);
        var mark = Ui.Swatch(T.Acc, 10, 10, 1f, SizeFlags.ShrinkBegin);
        row.AddChild(Ui.Pad((Control)mark, 0, 5, 0, 0));

        var text = Ui.Wrapped(vm.Prompt, 17, Ui.Semi, T.Ink, 1.4f);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(text);

        return Ui.Panel(Ui.Box(T.Cell, T.Ln2, 1, 16, 15), row);
    }

    private Control Option(QuestionVm vm, AnswerOption option)
    {
        var locked = option.StruckOut || vm.Frozen;
        var cell = Cell(Box(
            option.Selected ? T.AccD : T.Cell,
            option.Selected ? T.Acc : T.Ln2,
            1, 12, 12,
            locked));
        cell.Modulate = new Color(1, 1, 1, option.StruckOut ? 0.45f : 1f);
        cell.CustomMinimumSize = new Vector2(0, Pressable.MinHit);
        if (!locked)
            cell.Pressed += () => Emit(new Answered(vm.Index, option.Index));

        var row = Ui.HBox(11);

        var letterBox = Ui.Panel(Ui.Box(option.Selected ? T.Acc : T.Ln2));
        letterBox.CustomMinimumSize = new Vector2(22, 22);
        letterBox.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var letter = Ui.Text(option.Letter, 11, Ui.Heavy, option.Selected ? T.BtnFg : T.Mute, mono: true);
        letter.HorizontalAlignment = HorizontalAlignment.Center;
        letter.VerticalAlignment = VerticalAlignment.Center;
        letterBox.AddChild(letter);
        row.AddChild(letterBox);

        var text = Ui.Wrapped(option.Text, 14, Ui.Semi, option.StruckOut ? T.Mute : T.Ink, 1.35f);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        text.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(text);

        if (!string.IsNullOrEmpty(option.Tag))
            row.AddChild(Ui.Text(option.Tag, 10, Ui.Regular, option.StruckOut ? T.Acc : T.Mute, mono: true));

        cell.AddChild(row);
        return cell;
    }
}
