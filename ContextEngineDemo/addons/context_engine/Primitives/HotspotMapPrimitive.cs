using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// hotspot.map — a schematic with focusable regions, each carrying a condition value. Engine
/// bays today, body parts and floorplans on the same primitive. Placement is declared in 0–1
/// fractions so the diagram scales as one unit instead of reflowing, which is what protects
/// the memorised position of every control on a television.
/// </summary>
public partial class HotspotMapPrimitive : Primitive
{
    public override IReadOnlyList<(string Pad, string Key, string Label)> Prompts { get; } =
        new[] { ("A", "Click", "Select system"), ("LB/RB", "Tab", "Next region") };

    protected override void Build(IRegionVm regionVm)
    {
        if (regionVm is not HotspotMapVm vm) return;

        var board = new Pattern
        {
            Mode = Pattern.Kind.Graph,
            Spacing = 24,
            Line = new Color(0.5f, 0.5f, 0.5f, 0.12f),
            Ground = T.Cell,
            CustomMinimumSize = new Vector2(0, 292),
        };

        foreach (var spot in vm.Spots)
            board.AddChild(Spot(spot));

        AddChild(Ui.Panel(Ui.Box(T.Cell, T.Ln2, 1), board));

        if (!string.IsNullOrEmpty(vm.MapLabel))
            AddChild(Ui.Wrapped(vm.MapLabel, 11, Ui.Regular, T.Mute, 1.4f));
    }

    private Control Spot(Hotspot spot)
    {
        var cell = Cell(Box(
            spot.Selected ? T.AccD : T.Pnl2,
            spot.Selected ? T.Acc : spot.Faulty ? T.Acc : T.Ln2,
            spot.Selected ? 2 : 1,
            8, 7));
        cell.Pressed += () => Emit(new SelectionChanged(spot.Id));

        // Anchors carry the fractions, so the map is resolution-independent by construction.
        cell.AnchorLeft = spot.Rect.Position.X;
        cell.AnchorTop = spot.Rect.Position.Y;
        cell.AnchorRight = spot.Rect.Position.X + spot.Rect.Size.X;
        cell.AnchorBottom = spot.Rect.Position.Y + spot.Rect.Size.Y;
        cell.OffsetLeft = cell.OffsetTop = cell.OffsetRight = cell.OffsetBottom = 0;

        var body = Ui.VBox(4);

        var name = Ui.Wrapped(spot.Name, 12, Ui.Semi, spot.Selected ? T.Ink : T.Dim, 1.2f);
        body.AddChild(name);
        body.AddChild(Ui.VFill());

        var badge = Ui.Clipped(spot.Badge, 9, Ui.Regular,
            spot.BadgeDone ? T.Dim : spot.BadgeAlert ? T.Acc : T.Mute, mono: true);
        badge.HorizontalAlignment = HorizontalAlignment.Right;

        var foot = Ui.HBox(6);
        foot.AddChild(Ui.Text(spot.Cond, 15, Ui.Heavy, QualityColor(spot.Quality)));
        foot.AddChild(badge);
        body.AddChild(foot);

        cell.AddChild(body);
        return cell;
    }
}
