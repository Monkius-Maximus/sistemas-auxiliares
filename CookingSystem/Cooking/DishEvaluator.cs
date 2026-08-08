using System;
using System.Collections.Generic;
using System.Linq;

namespace LifeSim.Cooking;

/// <summary>
/// Função pura: sessão entra, prato sai. Sem estado, sem efeito colateral.
/// É por isso que o preview ao vivo e o ato de cozinhar podem chamar exatamente
/// o mesmo código — o número que o jogador vê é o número que ele recebe.
///
/// Qualidade é o PRODUTO de cinco fatores 0..1, limitado pelo teto de perícia.
/// Multiplicativo e não somado de propósito: um erro grave em qualquer eixo
/// (sal demais, ingrediente podre) afunda o prato inteiro, que é como cozinhar funciona.
/// </summary>
public static class DishEvaluator
{
    // ---------------------------------------------------------------
    // Constantes de tuning. Calibradas para esta distribuição-alvo:
    //   lixo 0.05 | preguiçoso 0.45 | sólido 0.80 | excelente 0.95
    // Mexer aqui é design, não bugfix.
    // ---------------------------------------------------------------

    /// <summary>Janela de intensidade de sabor por unidade onde o tempero está "certo".</summary>
    private const float IdealIntensityMin = 0.55f;
    private const float IdealIntensityMax = 1.10f;
    private const float IntensityFalloff = 0.90f;

    /// <summary>Fração ideal do eixo dominante: um sabor lidera, os outros acompanham.</summary>
    private const float IdealDominantShare = 0.42f;
    private const float DominantShareTolerance = 0.42f;
    private const float DominantShareExponent = 1.6f;
    private const float BalanceFloor = 0.08f;

    /// <summary>Choque só conta quando os dois eixos estão realmente presentes.</summary>
    private const float ClashOnset = 0.12f;
    private const float ClashFull = 0.30f;

    private static readonly (FlavorAxis A, FlavorAxis B, float Penalty)[] Clashes =
    {
        (FlavorAxis.Sweet, FlavorAxis.Umami, 0.55f),
        (FlavorAxis.Sweet, FlavorAxis.Salty, 0.45f),
        (FlavorAxis.Bitter, FlavorAxis.Sour, 0.50f),
    };

    /// <summary>Índice = número de grupos alimentares distintos. 3 é o ponto ótimo;
    /// jogar tudo dentro (5+) volta a piorar.</summary>
    private static readonly float[] VarietyByGroupCount = { 0.30f, 0.50f, 0.82f, 1.00f, 0.92f, 0.80f };

    private const float CeilingAtLevelZero = 0.50f;
    private const float CeilingPerLevel = 0.05f;

    public static CookedDish Evaluate(CookingSession session, IReadOnlyList<RecipeAnchor> anchors)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(anchors);
        if (session.Base is null)
            throw new InvalidOperationException("Sessão sem recipiente não pode ser avaliada.");
        if (session.Ingredients.Count == 0)
            throw new InvalidOperationException("Sessão sem ingredientes não pode ser avaliada.");

        var stacks = session.Ingredients.Concat(session.Seasonings).ToList();

        int totalUnits = stacks.Sum(s => s.Units);
        float totalWeight = stacks.Sum(s => s.Weight);

        // --- Nutrição: soma linear pura ---
        float calories = 0, protein = 0, lipids = 0, carbs = 0, hunger = 0, thirst = 0;
        int cost = 0;
        var raw = new float[FlavorProfile.AxisCount];

        foreach (var stack in stacks)
        {
            var n = stack.Def.Nutrition;
            calories += n.Calories * stack.Units;
            protein += n.Protein * stack.Units;
            lipids += n.Lipids * stack.Units;
            carbs += n.Carbs * stack.Units;
            hunger += n.Hunger * stack.Units;
            thirst += n.Thirst * stack.Units;
            cost += stack.Def.PricePerUnit * stack.Units;

            for (int i = 0; i < FlavorProfile.AxisCount; i++)
                raw[i] += stack.Def.Flavor.Get((FlavorAxis)i) * stack.Def.Potency * stack.Units;
        }

        // Normaliza por unidade, não por peso: é isso que faz o tempero (potência alta,
        // poucas unidades) empurrar o sabor sem inflar o denominador.
        var norm = raw.Select(v => v / totalUnits).ToArray();
        float intensity = norm.Sum();

        int dominantIndex = Array.IndexOf(norm, norm.Max());

        float seasoning = ScoreIntensity(intensity);
        float balance = ScoreBalance(norm, intensity);
        float harmony = ScoreHarmony(norm, intensity);
        float variety = ScoreVariety(session.Ingredients);
        float freshness = ScoreFreshness(stacks, totalWeight);

        float product = seasoning * balance * harmony * variety * freshness;

        // A âncora fecha uma fração da distância até 1.0 em vez de multiplicar: mantém a
        // granularidade no topo e ainda premia bem uma execução mediana de receita conhecida.
        var anchor = MatchAnchor(session, anchors);
        float lifted = anchor is null ? product : product + (1f - product) * anchor.QualityLift;

        float ceiling = Math.Min(1f, CeilingAtLevelZero + CeilingPerLevel * session.CookingLevel);
        float quality = Math.Clamp(lifted, 0f, ceiling);

        return new CookedDish
        {
            Name = anchor?.DishName ?? DishNamer.Generate(session, MinFreshness(stacks)),
            Calories = calories,
            Protein = protein,
            Lipids = lipids,
            Carbs = carbs,
            Hunger = hunger,
            Thirst = thirst,
            Weight = totalWeight,
            Cost = cost,
            Quality = quality,
            RawQuality = lifted,
            SeasoningScore = seasoning,
            BalanceScore = balance,
            HarmonyScore = harmony,
            VarietyScore = variety,
            FreshnessScore = freshness,
            QualityCeiling = ceiling,
            Intensity = intensity,
            DominantAxis = (FlavorAxis)dominantIndex,
            MatchedAnchor = anchor is not null,
        };
    }

    /// <summary>Insosso e salgado demais caem pelos dois lados da mesma janela.</summary>
    private static float ScoreIntensity(float intensity)
    {
        if (intensity < IdealIntensityMin)
            return Math.Max(0f, 1f - (IdealIntensityMin - intensity) / (IdealIntensityMin * IntensityFalloff));
        if (intensity > IdealIntensityMax)
            return Math.Max(0f, 1f - (intensity - IdealIntensityMax) / (IdealIntensityMax * IntensityFalloff));
        return 1f;
    }

    /// <summary>Um eixo dominando 100% é monótono; nenhum liderando é sopa sem identidade.</summary>
    private static float ScoreBalance(float[] norm, float intensity)
    {
        if (intensity <= 0f)
            return BalanceFloor;

        float share = norm.Max() / intensity;
        float deviation = Math.Abs(share - IdealDominantShare) / DominantShareTolerance;
        return Math.Max(BalanceFloor, 1f - MathF.Pow(deviation, DominantShareExponent));
    }

    private static float ScoreHarmony(float[] norm, float intensity)
    {
        if (intensity <= 0f)
            return 0f;

        float harmony = 1f;
        foreach (var (a, b, penalty) in Clashes)
        {
            float shareA = norm[(int)a] / intensity;
            float shareB = norm[(int)b] / intensity;
            float weaker = Math.Min(shareA, shareB);
            if (weaker <= ClashOnset)
                continue;

            float ramp = Math.Min(1f, (weaker - ClashOnset) / (ClashFull - ClashOnset));
            harmony -= penalty * ramp;
        }
        return Math.Max(0f, harmony);
    }

    private static float ScoreVariety(IReadOnlyCollection<IngredientStack> ingredients)
    {
        int groups = ingredients.Select(s => s.Def.Group).Distinct().Count();
        return VarietyByGroupCount[Math.Min(groups, VarietyByGroupCount.Length - 1)];
    }

    /// <summary>
    /// Frescor não é média: é penalidade proporcional à massa estragada. Um pedaço
    /// pequeno de queijo podre num prato grande estraga pouco; metade do prato, muito.
    /// </summary>
    private static float ScoreFreshness(IReadOnlyList<IngredientStack> stacks, float totalWeight)
    {
        float score = 1f;
        foreach (var stack in stacks)
            score -= (stack.Weight / totalWeight) * (1f - stack.Freshness);
        return Math.Max(0f, score);
    }

    private static float MinFreshness(IReadOnlyList<IngredientStack> stacks) =>
        stacks.Min(s => s.Freshness);

    private static RecipeAnchor MatchAnchor(CookingSession session, IReadOnlyList<RecipeAnchor> anchors)
    {
        var present = session.Ingredients.Concat(session.Seasonings)
                             .Select(s => s.Def.Id)
                             .ToHashSet();

        return anchors.FirstOrDefault(a =>
            a.BaseItemId == session.Base.Id &&
            a.Portions.Keys.All(present.Contains) &&
            !a.ForbiddenIngredientIds.Any(present.Contains));
    }
}
