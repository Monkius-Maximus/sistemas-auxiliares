using System;
using LifeSim.Cooking;

namespace LifeSim.Household;

/// <summary>
/// Um prato pronto que existe no mundo: tem porções, fica na geladeira e envelhece. É o que o
/// <see cref="CookedDish"/> vira quando o jogador confirma — o resultado imutável do preparo,
/// mais o estado que só um objeto guardado tem.
/// </summary>
public sealed class Meal
{
    // ---------------------------------------------------------------
    // Tuning de sobras. Mexer aqui é design, não bugfix.
    // ---------------------------------------------------------------

    /// <summary>Massa de uma porção, em kg. É daqui que o número de porções sai — nunca autorado.</summary>
    public const float ServingWeight = 0.10f;
    public const int MaxServings = 8;

    /// <summary>Sobra na geladeira: boa por dois dias, estragada no quinto.</summary>
    public const float LeftoverFreshDays = 2f;
    public const float LeftoverRotDays = 5f;

    /// <summary>Passada perde sabor; estragada fica intragável.</summary>
    public const float StaleQualityFactor = 0.80f;
    public const float RottenQualityFactor = 0.35f;

    /// <summary>Sobra estragada: o risco não desce disto, cozida ou não.</summary>
    public const float RottenLeftoverPoisoning = 0.75f;

    public Meal(CookedDish dish, BaseItemDef vessel = null)
    {
        Dish = dish ?? throw new ArgumentNullException(nameof(dish));
        Vessel = vessel;
        TotalServings = Math.Clamp((int)MathF.Round(dish.Weight / ServingWeight), 1, MaxServings);
        Servings = TotalServings;
    }

    public CookedDish Dish { get; }

    /// <summary>Onde foi feito. Dá a arte e a cor no painel; não muda mais nada depois de pronto.</summary>
    public BaseItemDef Vessel { get; }
    public int TotalServings { get; }
    public int Servings { get; private set; }

    /// <summary>Dias desde que saiu do fogão.</summary>
    public float AgeDays { get; private set; }

    public float Freshness =>
        AgeDays <= LeftoverFreshDays ? 1f
        : AgeDays >= LeftoverRotDays ? 0f
        : 1f - (AgeDays - LeftoverFreshDays) / (LeftoverRotDays - LeftoverFreshDays);

    public Spoilage State => Cooking.Freshness.StateOf(Freshness);

    /// <summary>
    /// Dias até virar "passada" — quando o frescor cruza o corte da régua, não quando a curva
    /// começa a cair. O texto da tela e o estado têm que mudar no mesmo instante.
    /// </summary>
    public float DaysUntilStale =>
        MathF.Max(0f, LeftoverFreshDays + (1f - Cooking.Freshness.StaleBelow) * (LeftoverRotDays - LeftoverFreshDays) - AgeDays);

    public string Name => State switch
    {
        Spoilage.Rotten => $"{Dish.Name} (sobra estragada)",
        Spoilage.Stale => $"{Dish.Name} (sobra passada)",
        _ => Dish.Name,
    };

    /// <summary>A nota de comer agora: a do preparo, menos o que o tempo na geladeira tirou.</summary>
    public float Quality => Dish.Quality * State switch
    {
        Spoilage.Rotten => RottenQualityFactor,
        Spoilage.Stale => StaleQualityFactor,
        _ => 1f,
    };

    /// <summary>
    /// O risco de comer agora: o que já veio do preparo, ou o piso de sobra estragada — o que
    /// for maior. Cozinhar de novo não salva comida que apodreceu depois de pronta.
    /// </summary>
    public float PoisoningChance =>
        State == Spoilage.Rotten ? MathF.Max(Dish.PoisoningChance, RottenLeftoverPoisoning) : Dish.PoisoningChance;

    /// <summary>Fome e sede saciadas por uma porção, nas unidades do prato.</summary>
    public float HungerPerServing => Dish.Hunger / TotalServings;
    public float ThirstPerServing => Dish.Thirst / TotalServings;

    internal void TakeServing()
    {
        if (Servings == 0)
            throw new InvalidOperationException($"{Name} já acabou.");
        Servings--;
    }

    internal void Age(float days)
    {
        if (days < 0f) throw new ArgumentOutOfRangeException(nameof(days));
        AgeDays += days;
    }
}
