using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// subject · picker — single-select tiles naming the thing being acted upon: vessel, vendor,
/// bench, container, vehicle, exam. Every context has one, which is why the player always
/// knows what the panel is about before they read anything else.
/// </summary>
public partial class PickerPrimitive : Primitive
{
    public override IReadOnlyList<(string Pad, string Key, string Label)> Prompts { get; } =
        new[] { ("A", "Enter", "Choose subject"), ("LB/RB", "Tab", "Next region") };

    protected override void Build(IRegionVm regionVm)
    {
        if (regionVm is not PickerVm vm) return;

        var grid = Ui.Grid(3, 5, 5);
        grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        foreach (var option in vm.Options)
            grid.AddChild(Tile(option));
        AddChild(grid);

        if (!string.IsNullOrEmpty(vm.Note))
            AddChild(Ui.Wrapped(vm.Note, 11, Ui.Regular, T.Mute, 1.4f));
    }

    private Control Tile(PickerOption option)
    {
        var cell = Cell(Box(
            option.Selected ? T.AccD : T.Cell,
            option.Selected ? T.Acc : T.Ln2,
            1, 5, 8));
        cell.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        cell.Pressed += () => Emit(new SubjectChanged(option.Id));

        var body = Ui.VBox(6);
        body.AddChild(Ui.Swatch(option.Tint, 0, 26, option.Selected ? 1f : 0.45f));
        body.AddChild(Ui.Clipped(option.Name, 11, Ui.Semi, option.Selected ? T.Ink : T.Dim));
        cell.AddChild(body);
        return cell;
    }
}
