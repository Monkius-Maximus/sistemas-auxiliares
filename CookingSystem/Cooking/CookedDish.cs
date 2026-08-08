namespace LifeSim.Cooking;

/// <summary>
/// Resultado da avaliação de uma sessão. É o que o preview ao vivo desenha e também
/// o que vira item persistente quando o jogador confirma. Imutável de propósito:
/// se algo mudou, reavalie a sessão inteira.
/// </summary>
public sealed class CookedDish
{
    public required string Name { get; init; }

    // --- Nutrição (soma linear) ---
    public required float Calories { get; init; }
    public required float Protein { get; init; }
    public required float Lipids { get; init; }
    public required float Carbs { get; init; }
    public required float Hunger { get; init; }
    public required float Thirst { get; init; }
    public required float Weight { get; init; }

    /// <summary>Custo em dinheiro, somado dos ingredientes. Nunca autorado por prato.</summary>
    public required int Cost { get; init; }

    // --- Qualidade e seus fatores (expostos para o painel poder explicar a nota) ---
    public required float Quality { get; init; }
    public required float SeasoningScore { get; init; }
    public required float BalanceScore { get; init; }
    public required float HarmonyScore { get; init; }
    public required float VarietyScore { get; init; }
    public required float FreshnessScore { get; init; }

    /// <summary>Qualidade antes do teto de perícia. Se RawQuality &gt; QualityCeiling, a perícia é o gargalo.</summary>
    public required float RawQuality { get; init; }

    public required float QualityCeiling { get; init; }

    /// <summary>Verdadeiro só quando a perícia foi de fato o fator limitante.</summary>
    public bool CappedBySkill => RawQuality > QualityCeiling;

    public required float Intensity { get; init; }
    public required FlavorAxis DominantAxis { get; init; }
    public required bool MatchedAnchor { get; init; }

    // --- Camada de life sim ---
    /// <summary>Redução de infelicidade ao comer. Prato ruim vira número negativo.</summary>
    public float UnhappinessRelief => (Quality - 0.35f) * 40f;
    /// <summary>Redução de tédio. Variedade pesa mais que qualidade bruta aqui.</summary>
    public float BoredomRelief => VarietyScore * Quality * 25f;
    /// <summary>Duração do moodlet em minutos de jogo.</summary>
    public float MoodletMinutes => Quality * 240f;
}
