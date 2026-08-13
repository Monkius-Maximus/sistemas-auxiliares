using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// preview — art slot, name, one line, up to three tags. The one region an artist owns, and
/// the main defence against every context feeling the same. Budget art for it early.
/// </summary>
public partial class PreviewPrimitive : Primitive
{
    public override IReadOnlyList<(string Pad, string Key, string Label)> Prompts { get; } =
        new[] { ("LB/RB", "Tab", "Next region") };

    protected override void Build(IRegionVm regionVm)
    {
        if (regionVm is not PreviewVm vm) return;

        var row = Ui.HBox(11);
        row.AddChild(ArtSlot(vm));

        var text = Ui.VBox(4);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        text.AddChild(Ui.Wrapped(vm.Name, 16, Ui.Heavy, T.Ink, 1.15f));
        text.AddChild(Ui.Wrapped(vm.Desc, 11, Ui.Regular, T.Dim, 1.45f));

        if (vm.Tags.Count > 0)
        {
            var tags = new HFlowContainer();
            tags.AddThemeConstantOverride("h_separation", 4);
            tags.AddThemeConstantOverride("v_separation", 4);
            foreach (var tag in vm.Tags)
                tags.AddChild(Tag(tag));
            text.AddChild(Ui.Pad(tags, 0, 2, 0, 0));
        }

        row.AddChild(text);
        AddChild(row);
    }

    private Control ArtSlot(PreviewVm vm)
    {
        var slot = new Control { CustomMinimumSize = new Vector2(96, 96), ClipContents = true };
        slot.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        slot.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;

        if (vm.Art != null)
        {
            var art = new TextureRect
            {
                Texture = vm.Art,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            };
            art.SetAnchorsPreset(LayoutPreset.FullRect);
            slot.AddChild(art);
        }
        else
        {
            var hatch = new Pattern
            {
                Mode = Pattern.Kind.Hatch,
                Spacing = 8,
                Line = new Color(0.5f, 0.5f, 0.5f, 0.18f),
                Ground = T.Cell,
            };
            hatch.SetAnchorsPreset(LayoutPreset.FullRect);
            slot.AddChild(hatch);

            // The art contract, printed where the art will go: "sprite 64²".
            var label = Ui.Text(vm.SlotLabel, 8, Ui.Semi, T.Mute, mono: true);
            label.SetAnchorsPreset(LayoutPreset.BottomLeft);
            label.OffsetLeft = 5;
            label.OffsetTop = -16;
            label.OffsetBottom = -5;
            slot.AddChild(label);
        }

        var framed = Ui.Panel(Ui.Box(Colors.Transparent, T.Line, 2), slot);
        framed.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        framed.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        return framed;
    }

    private Control Tag(string text)
        => Ui.Panel(Ui.Box(Colors.Transparent, T.Ln2, 1, 6, 4), Ui.Upper(text, 9, T.Dim, Ui.Semi, 1));
}
