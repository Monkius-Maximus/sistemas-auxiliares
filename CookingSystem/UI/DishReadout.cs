using System;
using System.Collections.Generic;
using LifeSim.Ui;

namespace LifeSim.Cooking;

/// <summary>
/// Como um prato vira número na tela. Os dois contextos que mostram prato — o painel manual
/// e o menu rápido — passam por aqui, para que a mesma comida nunca seja apresentada de
/// dois jeitos diferentes.
///
/// Nada é calculado aqui: tudo já veio pronto do <see cref="DishEvaluator"/>. Se aparecer
/// uma conta neste arquivo, ela está no lugar errado.
/// </summary>
internal static class DishReadout
{
    public static Readout Of(CookedDish dish, string headline, string caption) => new()
    {
        Title = "Resultado",
        Headline = headline,
        Value = dish is null ? "—" : $"{dish.Quality:P0}",
        Fill = dish?.Quality ?? 0f,
        Caption = caption,
        Factors = Factors(dish),
        Stats = Stats(dish),
    };

    /// <summary>
    /// Os cinco fatores, na ordem em que o avaliador os multiplica. Exibir todos é o que
    /// ensina o sistema sem tutorial: o jogador vê qual eixo afundou o prato.
    /// </summary>
    private static IReadOnlyList<ReadoutFactor> Factors(CookedDish dish) =>
        dish is null
            ? Array.Empty<ReadoutFactor>()
            : new[]
            {
                new ReadoutFactor { Name = "tempero", Value = dish.SeasoningScore },
                new ReadoutFactor { Name = "equilíbrio", Value = dish.BalanceScore },
                new ReadoutFactor { Name = "harmonia", Value = dish.HarmonyScore },
                new ReadoutFactor { Name = "variedade", Value = dish.VarietyScore },
                new ReadoutFactor { Name = "frescor", Value = dish.FreshnessScore },
            };

    private static IReadOnlyList<ReadoutStat> Stats(CookedDish dish) => new[]
    {
        Stat("Fome", dish?.Hunger, "0.0"),
        Stat("Sede", dish?.Thirst, "0.0"),
        Stat("Calorias", dish?.Calories, "0"),
        Stat("Proteína", dish?.Protein, "0.0"),
        Stat("Gordura", dish?.Lipids, "0.0"),
        Stat("Carbo.", dish?.Carbs, "0.0"),
        Stat("Ânimo", dish?.UnhappinessRelief, "+0.0;-0.0"),
        Stat("Tédio", dish?.BoredomRelief, "0.0"),
        Stat("Peso", dish?.Weight, "0.00"),
    };

    /// <summary>Sem prato o número vira travessão apagado: a grade não muda de forma.</summary>
    private static ReadoutStat Stat(string key, float? value, string format) => new()
    {
        Key = key,
        Value = value is null ? "—" : value.Value.ToString(format),
        Tone = value is null ? StatTone.Muted
             : value.Value < 0 ? StatTone.Alert
             : StatTone.Neutral,
    };
}
