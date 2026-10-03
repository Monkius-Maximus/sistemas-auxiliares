namespace LifeSim.Cooking;

public enum Spoilage
{
    Fresh,
    Stale,
    Rotten,
}

/// <summary>
/// A régua única de frescor. O nome do prato, a linha de estoque do ladrilho, o risco de
/// intoxicação e o verbo de descarte leem estes mesmos cortes — "passado" não pode querer
/// dizer uma coisa no nome e outra no estoque.
/// </summary>
public static class Freshness
{
    public const float StaleBelow = 0.65f;
    public const float RottenBelow = 0.30f;

    public static Spoilage StateOf(float freshness) =>
        freshness < RottenBelow ? Spoilage.Rotten
        : freshness < StaleBelow ? Spoilage.Stale
        : Spoilage.Fresh;
}

/// <summary>Uma parte de um lote: tantas unidades com tal idade. A idade é o que define o frescor.</summary>
public readonly record struct Portion(float AgeDays, int Units);

/// <summary>Qual lote sai primeiro quando o jogador põe uma unidade no prato.</summary>
public enum PickOrder
{
    /// <summary>Os mais velhos antes: menos desperdício, prato um pouco pior. É o padrão de quem cozinha.</summary>
    OldestFirst,

    /// <summary>Os mais frescos antes: o melhor prato agora, e os velhos ficam para estragar.</summary>
    FreshestFirst,
}
