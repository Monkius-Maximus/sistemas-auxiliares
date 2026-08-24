using Godot;

namespace ContextUi;

/// <summary>
/// A pele do painel, por papel semântico. Nenhuma definição de contexto fala de cor: ela
/// diz "selecionado", "alarme", "trancado", e a pele decide como isso se parece. É o que
/// permite trocar a aparência do jogo inteiro sem tocar em contexto nenhum.
/// </summary>
public sealed class PanelSkin
{
    /// <summary>Fundo atrás do painel.</summary>
    public required Color Background { get; init; }

    /// <summary>Corpo do painel.</summary>
    public required Color Panel { get; init; }

    /// <summary>Faixas que se destacam do corpo: cabeçalho e coluna de resultado.</summary>
    public required Color PanelAlt { get; init; }

    /// <summary>Célula: ladrilho apagado, linha de lista, trilho de barra.</summary>
    public required Color Cell { get; init; }

    /// <summary>Divisória estrutural, 2px.</summary>
    public required Color Line { get; init; }

    /// <summary>Divisória interna, 1px.</summary>
    public required Color LineSoft { get; init; }

    /// <summary>Texto principal.</summary>
    public required Color Ink { get; init; }

    /// <summary>Texto de apoio.</summary>
    public required Color Dim { get; init; }

    /// <summary>Texto de terceira ordem: rótulos, contadores, unidades.</summary>
    public required Color Mute { get; init; }

    /// <summary>Acento: seleção, alarme, ação de confirmar. Um só, de propósito.</summary>
    public required Color Accent { get; init; }

    /// <summary>Acento fraco, para o meio da escala.</summary>
    public required Color AccentSoft { get; init; }

    /// <summary>Fundo de item selecionado.</summary>
    public required Color AccentDeep { get; init; }

    /// <summary>Texto sobre o acento.</summary>
    public required Color AccentInk { get; init; }

    /// <summary>
    /// Cor de um valor 0..1. Os cortes são os mesmos que o jogo já usa para qualidade
    /// (0.35 e 0.70), então a barra de qualidade, os fatores e o número grande contam
    /// a mesma história com a mesma régua.
    /// </summary>
    public Color Scale(float value) => value switch
    {
        < 0.35f => Accent,
        < 0.70f => AccentSoft,
        _ => Ink,
    };

    public Color Of(StatTone tone) => tone switch
    {
        StatTone.Alert => Accent,
        StatTone.Muted => Mute,
        _ => Ink,
    };

    public static readonly PanelSkin Dark = new()
    {
        Background = new Color("131211"),
        Panel = new Color("1c1a19"),
        PanelAlt = new Color("232120"),
        Cell = new Color("2b2827"),
        Line = new Color("3a3634"),
        LineSoft = new Color("2f2b2a"),
        Ink = new Color("f3f2f2"),
        Dim = new Color("9b9797"),
        Mute = new Color("605d5d"),
        Accent = new Color("ff563c"),
        AccentSoft = new Color("ff9783"),
        AccentDeep = new Color("4d170e"),
        AccentInk = new Color("17110f"),
    };

    public static readonly PanelSkin Light = new()
    {
        Background = new Color("f3f2f2"),
        Panel = new Color("f8f4f4"),
        PanelAlt = new Color("eae7e7"),
        Cell = new Color("eae7e7"),
        Line = new Color("201e1d"),
        LineSoft = new Color("d7d3d3"),
        Ink = new Color("201e1d"),
        Dim = new Color("605d5d"),
        Mute = new Color("9b9797"),
        Accent = new Color("ec3013"),
        AccentSoft = new Color("c94b39"),
        AccentDeep = new Color("ffe0d9"),
        AccentInk = new Color("ffffff"),
    };
}
