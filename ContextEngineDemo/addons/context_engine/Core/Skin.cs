using Godot;

namespace ContextEngine;

/// <summary>
/// Every colour in the panel comes from here — never from a primitive and never from a scene.
/// The two skins are the same layout with one token set swapped, exactly as in the design doc;
/// adding a high-contrast accessibility skin means adding a third entry, nothing else.
/// </summary>
public sealed class Skin
{
    public string Id { get; init; }
    public string Label { get; init; }

    /// <summary>Page ground, behind the panel.</summary>
    public Color Bg { get; init; }
    /// <summary>Panel body.</summary>
    public Color Pnl { get; init; }
    /// <summary>Title bar and the right rail — one step off the body.</summary>
    public Color Pnl2 { get; init; }
    /// <summary>Inset cell: tiles, bars, wells.</summary>
    public Color Cell { get; init; }
    /// <summary>Structural 2 px rules.</summary>
    public Color Line { get; init; }
    /// <summary>Hairline 1 px rules inside a region.</summary>
    public Color Ln2 { get; init; }
    /// <summary>Primary text.</summary>
    public Color Ink { get; init; }
    /// <summary>Secondary text.</summary>
    public Color Dim { get; init; }
    /// <summary>Tertiary text, labels, units.</summary>
    public Color Mute { get; init; }
    /// <summary>"This is your problem" — never decorative, never a category colour.</summary>
    public Color Acc { get; init; }
    /// <summary>Accent ground for an active tile.</summary>
    public Color AccD { get; init; }
    /// <summary>Text drawn on top of <see cref="Acc"/>.</summary>
    public Color BtnFg { get; init; }
    /// <summary>Mid-quality warning, between Acc and Ink on the quality ramp.</summary>
    public Color Mid { get; init; }

    private static Color C(string hex) => new(hex);

    public static readonly Skin Dark = new()
    {
        Id = "dark",
        Label = "Skin · in-game dark",
        Bg = C("#131211"), Pnl = C("#1c1a19"), Pnl2 = C("#232120"), Cell = C("#2b2827"),
        Line = C("#3a3634"), Ln2 = C("#2f2b2a"),
        Ink = C("#f3f2f2"), Dim = C("#9b9797"), Mute = C("#605d5d"),
        Acc = C("#ff563c"), AccD = C("#4d170e"), BtnFg = C("#17110f"), Mid = C("#ff9783"),
    };

    public static readonly Skin Light = new()
    {
        Id = "light",
        Label = "Skin · editor light",
        Bg = C("#f3f2f2"), Pnl = C("#f8f4f4"), Pnl2 = C("#eae7e7"), Cell = C("#eae7e7"),
        Line = C("#201e1d"), Ln2 = C("#d7d3d3"),
        Ink = C("#201e1d"), Dim = C("#605d5d"), Mute = C("#9b9797"),
        Acc = C("#ec3013"), AccD = C("#ffe0d9"), BtnFg = C("#ffffff"), Mid = C("#c94b39"),
    };

    /// <summary>
    /// The quality ramp used by every headline number and factor bar: red under 35%,
    /// warning to 70%, plain ink above. One meaning for red across the whole panel.
    /// </summary>
    public Color Quality(double q) => q < 0.35 ? Acc : q < 0.70 ? Mid : Ink;

    /// <summary>Resolves a semantic <see cref="Tone"/> to a concrete colour.</summary>
    public Color Of(Tone tone) => tone switch
    {
        Tone.Ink => Ink,
        Tone.Dim => Dim,
        Tone.Mute => Mute,
        Tone.Alert => Acc,
        Tone.OnAccent => BtnFg,
        _ => Ink,
    };
}

/// <summary>
/// View-models talk in tones, not colours, so a re-skin never means touching a module.
/// </summary>
public enum Tone
{
    Ink,
    Dim,
    Mute,
    Alert,
    OnAccent,
}
