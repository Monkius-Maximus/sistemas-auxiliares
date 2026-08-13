using System;
using Godot;

namespace ContextEngine;

/// <summary>
/// A named slot in the panel with a fixed meaning and position. The host owns the header and
/// the rule; the primitive owns everything below it. Swapping which primitive fills a region
/// is a definition change, not a layout change — which is the whole point of the region model.
/// </summary>
public partial class RegionHost : VBoxContainer
{
    public RegionId Region { get; private set; }
    public Primitive Current { get; private set; }

    public event Action<Intent> Emitted;

    private string _primitiveKind = "";
    private VBoxContainer _headerSlot;

    public static RegionHost Create(RegionId region)
    {
        var host = new RegionHost { Region = region, Name = region.ToString() };
        host.AddThemeConstantOverride("separation", 9);
        host._headerSlot = Ui.VBox(0);
        host.AddChild(host._headerSlot);
        return host;
    }

    public void Bind(IRegionVm vm, Skin skin, bool showRegionTag)
    {
        if (vm == null)
        {
            Visible = false;
            return;
        }
        Visible = true;

        // The commit region carries no header — the button speaks for itself. It still gets the
        // dev tag when the overlay is on, so the region map stays complete.
        var isCommit = Region == RegionId.Commit;
        _headerSlot.Visible = !isCommit || showRegionTag;
        foreach (var child in _headerSlot.GetChildren())
        {
            _headerSlot.RemoveChild(child);
            child.QueueFree();
        }
        if (isCommit)
        {
            if (showRegionTag)
            {
                var row = Ui.HBox(8);
                row.AddChild(RegionTag(skin));
                row.AddChild(Ui.Fill());
                _headerSlot.AddChild(row);
            }
        }
        else
        {
            Header(vm, skin, showRegionTag);
        }

        if (_primitiveKind != vm.Primitive)
        {
            if (Current != null)
            {
                RemoveChild(Current);
                Current.QueueFree();
            }
            Current = PrimitiveFactory.Create(vm.Primitive);
            _primitiveKind = vm.Primitive;

            if (Current != null)
            {
                Current.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                Current.Emitted += intent => Emitted?.Invoke(intent);
                AddChild(Current);
            }
        }

        Current?.Bind(vm, skin);
    }

    private void Header(IRegionVm vm, Skin skin, bool showRegionTag)
    {
        var block = _headerSlot;

        var row = Ui.HBox(8);
        if (showRegionTag) row.AddChild(RegionTag(skin));
        row.AddChild(Ui.Upper(vm.Title, 10, skin.Dim, Ui.Semi, 2));
        row.AddChild(Ui.Fill());
        if (!string.IsNullOrEmpty(vm.Count))
        {
            var count = Ui.Text(vm.Count, 11, Ui.Semi, skin.Mute, mono: true);
            count.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(count);
        }

        block.AddChild(row);
        block.AddChild(Ui.Spacer(0, 6));
        block.AddChild(Ui.Rule(skin.Line));
    }

    /// <summary>The dev overlay: which region is which, for anyone learning the system.</summary>
    private Control RegionTag(Skin skin)
    {
        var label = Ui.Text(Region.ToString().ToUpperInvariant(), 9, Ui.Semi, skin.BtnFg, 1, true);
        var tag = Ui.Panel(Ui.Box(skin.Acc, null, 0, 5, 3), label);
        tag.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        return tag;
    }
}

/// <summary>
/// One entry per catalogue line. A new primitive is a class and a case — the shell itself
/// never changes.
/// </summary>
public static class PrimitiveFactory
{
    public static Primitive Create(string kind) => kind switch
    {
        "picker" => new PickerPrimitive(),
        "verbs" => new VerbsPrimitive(),
        "grid.qty" or "grid.select" => new TileGridPrimitive(),
        "grid.dual" => new DualGridPrimitive(),
        "checklist" => new ChecklistPrimitive(),
        "text" => new TextPrimitive(),
        "question" => new QuestionPrimitive(),
        "sequence" => new SequencePrimitive(),
        "hotspot.map" => new HotspotMapPrimitive(),
        "timing.bar" => new TimingBarPrimitive(),
        "preview" => new PreviewPrimitive(),
        "readout" => new ReadoutPrimitive(),
        "commit" => new CommitPrimitive(),
        _ => null,
    };
}
