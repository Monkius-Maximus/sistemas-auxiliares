using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// The drawing vocabulary of the panel: fonts, rules, tiles, meters. Everything visual that is
/// not a primitive lives here, so retuning the look is a one-file job.
/// </summary>
public static class Ui
{
    public const string SansPath = "res://assets/fonts/Archivo.ttf";
    public const string MonoPath = "res://assets/fonts/IBMPlexMono-Regular.ttf";

    // Weights used across the panel, matching the prototype's 400 / 600 / 800.
    public const int Regular = 400;
    public const int Semi = 600;
    public const int Heavy = 800;

    /// <summary>Packs a four-character OpenType tag the way TextServer expects it.</summary>
    private static int Tag(string tag) => (tag[0] << 24) | (tag[1] << 16) | (tag[2] << 8) | tag[3];

    private static readonly Dictionary<(int Weight, int Tracking, bool Mono), Font> Cache = new();
    private static FontFile _sansBase;
    private static FontFile _monoBase;

    private static FontFile SansBase => _sansBase ??= ResourceLoader.Exists(SansPath) ? ResourceLoader.Load<FontFile>(SansPath) : null;
    private static FontFile MonoBase => _monoBase ??= ResourceLoader.Exists(MonoPath) ? ResourceLoader.Load<FontFile>(MonoPath) : null;

    /// <summary>
    /// Archivo is a variable font, so weight is an axis rather than four files. Tracking is
    /// glyph spacing in px — the panel's uppercase micro-labels lean on it heavily.
    /// </summary>
    public static Font Face(int weight = Regular, int tracking = 0, bool mono = false)
    {
        var key = (weight, tracking, mono);
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var baseFont = mono ? (Font)MonoBase : SansBase;
        var variation = new FontVariation();
        if (baseFont != null) variation.BaseFont = baseFont;

        if (mono)
        {
            // IBM Plex Mono ships as a static face: fake the heavier grades.
            variation.VariationEmbolden = weight >= Heavy ? 0.6f : weight >= Semi ? 0.32f : 0f;
        }
        else
        {
            variation.VariationOpentype = new Godot.Collections.Dictionary { { Tag("wght"), weight } };
        }

        // Numbers are tabular and never reflow as they change — a number that jitters while
        // you hold a stepper is unreadable.
        variation.OpentypeFeatures = new Godot.Collections.Dictionary { { Tag("tnum"), 1 } };

        if (tracking != 0) variation.SetSpacing(TextServer.SpacingType.Glyph, tracking);

        Cache[key] = variation;
        return variation;
    }

    public static Label Text(string text, int size, int weight, Color color, int tracking = 0, bool mono = false)
    {
        var label = new Label
        {
            Text = text ?? "",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontOverride("font", Face(weight, tracking, mono));
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    /// <summary>A label that wraps and reports its wrapped height to the container.</summary>
    public static Label Wrapped(string text, int size, int weight, Color color, float lineHeight = 1.45f)
    {
        var label = Text(text, size, weight, color);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.AddThemeConstantOverride("line_spacing", Mathf.RoundToInt(size * (lineHeight - 1f)));
        return label;
    }

    /// <summary>Single-line label that ellipsises rather than pushing the row wider.</summary>
    public static Label Clipped(string text, int size, int weight, Color color, int tracking = 0, bool mono = false)
    {
        var label = Text(text, size, weight, color, tracking, mono);
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.ClipText = true;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return label;
    }

    public static Label Upper(string text, int size, Color color, int weight = Semi, int tracking = 2)
        => Text((text ?? "").ToUpperInvariant(), size, weight, color, tracking);

    // ── boxes ────────────────────────────────────────────────────────────────

    public static StyleBoxFlat Box(Color bg, Color? border = null, int borderWidth = 0, int padH = 0, int padV = 0)
    {
        var box = new StyleBoxFlat
        {
            BgColor = bg,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0,
        };
        if (border.HasValue && borderWidth > 0)
        {
            box.BorderColor = border.Value;
            box.SetBorderWidthAll(borderWidth);
        }
        box.ContentMarginLeft = padH;
        box.ContentMarginRight = padH;
        box.ContentMarginTop = padV;
        box.ContentMarginBottom = padV;
        return box;
    }

    /// <summary>A panel whose only job is a background and/or an edge.</summary>
    public static PanelContainer Panel(StyleBoxFlat box, Control child = null)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", box);
        if (child != null) panel.AddChild(child);
        return panel;
    }

    public static VBoxContainer VBox(int separation = 0)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", separation);
        return box;
    }

    public static HBoxContainer HBox(int separation = 0)
    {
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", separation);
        return box;
    }

    public static GridContainer Grid(int columns, int hSep, int vSep)
    {
        var grid = new GridContainer { Columns = Mathf.Max(1, columns) };
        grid.AddThemeConstantOverride("h_separation", hSep);
        grid.AddThemeConstantOverride("v_separation", vSep);
        return grid;
    }

    /// <summary>Fixed-height rule. The panel's structure is drawn, not implied by whitespace.</summary>
    public static ColorRect Rule(Color color, int thickness = 2)
        => new()
        {
            Color = color,
            CustomMinimumSize = new Vector2(0, thickness),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

    /// <summary>The colour-block stand-in for item art. Real sprites drop in at the same size.</summary>
    public static Control Swatch(Color color, float width, float height, float opacity = 1f,
        Control.SizeFlags valign = Control.SizeFlags.ShrinkCenter)
        => new ColorRect
        {
            Color = new Color(color, color.A * opacity),
            CustomMinimumSize = new Vector2(width, height),
            SizeFlagsVertical = valign,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

    public static Control Spacer(float width = 0, float height = 0)
        => new Control
        {
            CustomMinimumSize = new Vector2(width, height),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

    /// <summary>Pushes whatever follows it to the far end of the row.</summary>
    public static Control Fill()
    {
        var c = Spacer();
        c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return c;
    }

    /// <summary>Pushes whatever follows it to the bottom of the column.</summary>
    public static Control VFill()
    {
        var c = Spacer();
        c.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        return c;
    }

    public static MarginContainer Pad(Control child, int left, int top, int right, int bottom)
    {
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", left);
        m.AddThemeConstantOverride("margin_top", top);
        m.AddThemeConstantOverride("margin_right", right);
        m.AddThemeConstantOverride("margin_bottom", bottom);
        if (child != null) m.AddChild(child);
        return m;
    }

    public static MarginContainer Pad(Control child, int all) => Pad(child, all, all, all, all);
}

/// <summary>
/// A value bar. Two rectangles and nothing else — but it is the panel's second-most-read
/// element after the headline number, so it gets a real control rather than a styled box.
/// </summary>
public partial class MeterBar : Control
{
    private double _fraction;
    private Color _fill = Colors.White;
    private Color _ground = Colors.Black;

    public void Set(double fraction, Color fill, Color ground, float height)
    {
        _fraction = Mathf.Clamp(fraction, 0, 1);
        _fill = fill;
        _ground = ground;
        CustomMinimumSize = new Vector2(0, height);
        MouseFilter = MouseFilterEnum.Ignore;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), _ground);
        if (_fraction > 0)
            DrawRect(new Rect2(Vector2.Zero, new Vector2((float)(Size.X * _fraction), Size.Y)), _fill);
    }
}
