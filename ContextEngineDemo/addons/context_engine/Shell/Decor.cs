using Godot;

namespace ContextEngine;

/// <summary>
/// Equal-width columns that reflow to fill the rail, with a minimum tile width — the panel's
/// slot grids size themselves to the declared panel width rather than to a hard column count.
/// </summary>
public partial class AutoFillGrid : Container
{
    public float MinItemWidth { get; set; } = 112;
    public float HSep { get; set; } = 5;
    public float VSep { get; set; } = 5;

    private float _measuredHeight;

    public override void _Notification(int what)
    {
        if (what == NotificationSortChildren) Layout();
    }

    public override Vector2 _GetMinimumSize() => new(MinItemWidth, _measuredHeight);

    private void Layout()
    {
        var width = Size.X;
        if (width <= 0) return;

        var cols = Mathf.Max(1, Mathf.FloorToInt((width + HSep) / (MinItemWidth + HSep)));
        var colWidth = (width - (cols - 1) * HSep) / cols;

        var visible = new System.Collections.Generic.List<Control>();
        foreach (var child in GetChildren())
            if (child is Control { Visible: true } c) visible.Add(c);

        float y = 0;
        for (var i = 0; i < visible.Count; i += cols)
        {
            float rowHeight = 0;
            for (var j = i; j < Mathf.Min(i + cols, visible.Count); j++)
                rowHeight = Mathf.Max(rowHeight, visible[j].GetCombinedMinimumSize().Y);

            for (var j = i; j < Mathf.Min(i + cols, visible.Count); j++)
            {
                var x = (j - i) * (colWidth + HSep);
                FitChildInRect(visible[j], new Rect2(x, y, colWidth, rowHeight));
            }
            y += rowHeight + VSep;
        }

        var height = Mathf.Max(0, y - VSep);
        if (!Mathf.IsEqualApprox(height, _measuredHeight))
        {
            _measuredHeight = height;
            UpdateMinimumSize();
        }
    }
}

/// <summary>
/// The placeholder texture behind an art slot, a schematic or a sweep track. Cheap, and it
/// reads instantly as "art goes here" rather than as an empty box someone forgot to fill.
/// </summary>
public partial class Pattern : Control
{
    public enum Kind
    {
        /// <summary>45° hatch — the preview art slot.</summary>
        Hatch,
        /// <summary>Square graph paper — the hotspot map.</summary>
        Graph,
        /// <summary>Vertical ticks — the timing sweep track.</summary>
        Ticks,
    }

    public Kind Mode { get; set; } = Kind.Hatch;
    public float Spacing { get; set; } = 8;
    public Color Line { get; set; } = new(0.5f, 0.5f, 0.5f, 0.16f);
    public Color Ground { get; set; } = Colors.Transparent;

    public override void _Draw()
    {
        if (Ground.A > 0) DrawRect(new Rect2(Vector2.Zero, Size), Ground);

        switch (Mode)
        {
            case Kind.Hatch:
                for (var x = -Size.Y; x < Size.X; x += Spacing)
                    DrawLine(new Vector2(x, Size.Y), new Vector2(x + Size.Y, 0), Line, 1f);
                break;
            case Kind.Graph:
                for (var x = Spacing; x < Size.X; x += Spacing)
                    DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), Line, 1f);
                for (var y = Spacing; y < Size.Y; y += Spacing)
                    DrawLine(new Vector2(0, y), new Vector2(Size.X, y), Line, 1f);
                break;
            case Kind.Ticks:
                for (var x = Spacing; x < Size.X; x += Spacing)
                    DrawLine(new Vector2(x, 0), new Vector2(x, Size.Y), Line, 1f);
                break;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }
}
