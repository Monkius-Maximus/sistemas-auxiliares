using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// O que um modificador pode mexer no prato. Conjunto fechado de propósito: cada entrada é
/// uma alavanca que o avaliador conhece e testa. Conteúdo combina alavancas; não inventa novas.
/// </summary>
public enum DishStat
{
    /// <summary>Multiplica a intensidade de sabor antes de pontuar o tempero. Dourar concentra; água dilui.</summary>
    FlavorIntensity,

    /// <summary>Multiplica a penalidade dos pares de sabor que brigam. Fervura funde; cru expõe.</summary>
    ClashPenalty,

    /// <summary>Multiplica o quanto ingrediente estragado pesa no frescor. Cozinhar disfarça; cru não.</summary>
    SpoilagePenalty,

    /// <summary>Multiplica a sede saciada. Caldo hidrata.</summary>
    Thirst,

    /// <summary>Multiplica a gordura do prato. Fritura soma óleo.</summary>
    Lipids,

    /// <summary>
    /// Multiplica o risco de intoxicação de ingrediente estragado. Calor mata bactéria; cru não.
    /// Entradas novas vão sempre no fim: o .tres grava este enum como número.
    /// </summary>
    PoisoningRisk,
}

public enum ModifierOp
{
    /// <summary>Soma ao valor base. Aplicado antes dos multiplicadores.</summary>
    Add,

    /// <summary>Multiplica o resultado. Vários se compõem por produto.</summary>
    Multiply,
}

/// <summary>
/// Um efeito que o conteúdo contribui ao prato sem que o avaliador saiba quem o mandou:
/// o recipiente hoje; traço de personalidade, perícia ou eletrodoméstico amanhã. O avaliador
/// soma o stack e continua puro — conteúdo novo entra sem tocar nele.
/// </summary>
[GlobalClass]
public partial class DishModifier : Resource
{
    [Export] public DishStat Stat { get; set; }
    [Export] public ModifierOp Op { get; set; } = ModifierOp.Multiply;
    [Export] public float Value { get; set; } = 1f;

    /// <summary>
    /// O efeito na língua do jogador: "fervura funde os sabores". Aparece no painel ao escolher
    /// o recipiente — efeito que o jogador não consegue ler é efeito que ele não usa.
    /// </summary>
    [Export] public string Description { get; set; } = "";
}
