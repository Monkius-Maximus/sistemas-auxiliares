using System;
using System.Collections.Generic;
using Godot;

namespace LifeSim.Ui;

/// <summary>
/// A legenda de botões no rodapé do painel. Mostra o que a região focada aceita, no
/// vocabulário do dispositivo que o jogador tocou por último. Nada importante neste painel
/// depende de passar o mouse por cima, e nada fica para o jogador adivinhar.
/// </summary>
public partial class PromptBar : PanelContainer
{
    private PanelSkin _skin;
    private HBoxContainer _row;
    private IReadOnlyList<Prompt> _prompts = Array.Empty<Prompt>();

    /// <summary>Vocabulário: verdadeiro mostra os botões do controle, falso as teclas.</summary>
    public bool Gamepad { get; private set; }

    public void Bind(IReadOnlyList<Prompt> prompts, PanelSkin skin)
    {
        _prompts = prompts;
        _skin = skin;
        Redraw();
    }

    public void UseGamepad(bool gamepad)
    {
        if (Gamepad == gamepad) return;
        Gamepad = gamepad;
        Redraw();
    }

    private void Redraw()
    {
        if (_skin is null) return;

        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = _skin.PanelAlt,
            BorderColor = _skin.Line,
            BorderWidthTop = 2,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        });

        _row = new HBoxContainer();
        _row.AddThemeConstantOverride("separation", 16);
        AddChild(_row);

        foreach (var prompt in _prompts)
        {
            var group = new HBoxContainer();
            group.AddThemeConstantOverride("separation", 7);

            var chip = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
            chip.AddThemeStyleboxOverride("panel", PanelPrimitives.Box(_skin.Cell, _skin.LineSoft, 6, 3));
            chip.AddChild(PanelPrimitives.Text(Gamepad ? prompt.Pad : prompt.Key, 10, _skin.Ink));
            group.AddChild(chip);

            group.AddChild(PanelPrimitives.Text(prompt.Label, 11, _skin.Mute));
            _row.AddChild(group);
        }

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _row.AddChild(spacer);
        _row.AddChild(PanelPrimitives.Text(Gamepad ? "CONTROLE" : "TECLADO E MOUSE", 9, _skin.Mute));
    }
}

/// <summary>Um atalho, nas duas línguas. O painel escolhe qual mostrar.</summary>
public readonly record struct Prompt(string Pad, string Key, string Label);

/// <summary>
/// Que atalhos cada primitivo oferece. Fica junto do painel e não do primitivo porque a
/// legenda é do painel: ela mostra a região focada, não o widget sob o cursor.
/// </summary>
public static class PanelPrompts
{
    private static readonly Prompt[] None = Array.Empty<Prompt>();

    public static IReadOnlyList<Prompt> For(RegionBody body) => body switch
    {
        Picker => new[] { new Prompt("A", "Enter", "Escolher") },
        VerbList => new[] { new Prompt("A", "Enter", "Usar") },
        SlotGrid { Mode: SlotGridMode.Quantity } =>
            new[] { new Prompt("A / X", "Clique ±", "+1 / −1 (segure para repetir)") },
        SlotGrid => new[] { new Prompt("A", "Enter", "Escolher") },
        Checklist => None,
        _ => None,
    };
}
