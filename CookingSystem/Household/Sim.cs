using System;
using System.Collections.Generic;
using System.Linq;

namespace LifeSim.Household;

/// <summary>Um efeito de humor com prazo. "Refeição deliciosa", "Enjoado".</summary>
public sealed class Moodlet
{
    public Moodlet(string name, float mood, float minutes, bool fromFood = false)
    {
        Name = name;
        Mood = mood;
        MinutesLeft = minutes;
        FromFood = fromFood;
    }

    public string Name { get; }

    /// <summary>Moodlet de refeição: comer de novo substitui em vez de empilhar.</summary>
    public bool FromFood { get; }
    public float Mood { get; }
    public float MinutesLeft { get; internal set; }
}

/// <summary>O que aconteceu ao comer uma porção. O sorteio de intoxicação já foi feito.</summary>
public sealed record EatResult(string MealName, float HungerGained, float ThirstGained, Moodlet Moodlet, bool Poisoned);

/// <summary>
/// O Sim, no que importa para comer: fome, sede e humor. Necessidades vão de 0 (desesperado) a
/// 100 (satisfeito) e caem com o tempo; humor é a soma dos moodlets ativos.
///
/// Comer é o único ponto do sistema inteiro onde existe sorteio. Tudo antes dele — avaliador,
/// preview, risco — é número determinístico, para que a tela possa mostrar o que vai acontecer.
/// </summary>
public sealed class Sim
{
    // ---------------------------------------------------------------
    // Tuning de necessidades. Mexer aqui é design, não bugfix.
    // ---------------------------------------------------------------

    /// <summary>Pontos de necessidade por unidade de fome/sede do prato. Uma porção de omelete enche ~45.</summary>
    public const float HungerScale = 3.2f;
    public const float ThirstScale = 8f;

    /// <summary>Queda por hora: sem comer, um dia inteiro leva da saciedade à fome desesperada.</summary>
    public const float HungerDecayPerHour = 4f;
    public const float ThirstDecayPerHour = 5f;

    /// <summary>Intoxicação: humor muito ruim por seis horas, e o estômago devolve metade do que comeu.</summary>
    public const float PoisonedMood = -35f;
    public const float PoisonedMinutes = 360f;

    private readonly List<Moodlet> _moodlets = new();

    public Sim(string name, float hunger, float thirst)
    {
        Name = name;
        Hunger = Math.Clamp(hunger, 0f, 100f);
        Thirst = Math.Clamp(thirst, 0f, 100f);
    }

    public string Name { get; }
    public float Hunger { get; private set; }
    public float Thirst { get; private set; }
    public float Mood => _moodlets.Sum(m => m.Mood);
    public IReadOnlyList<Moodlet> Moodlets => _moodlets;

    /// <summary>Fome depois de comer uma porção, sem sortear nada. É o que o painel mostra antes do clique.</summary>
    public float HungerAfter(Meal meal) => Math.Min(100f, Hunger + meal.HungerPerServing * HungerScale);
    public float ThirstAfter(Meal meal) => Math.Min(100f, Thirst + meal.ThirstPerServing * ThirstScale);

    /// <summary>Humor depois de comer, sem intoxicação: o moodlet novo substitui o de comida anterior.</summary>
    public float MoodAfter(Meal meal) =>
        _moodlets.Where(m => !m.FromFood).Sum(m => m.Mood) + MoodletFor(meal).Mood;

    /// <summary>
    /// O moodlet que uma porção dá se não houver intoxicação. Mesmos cortes de qualidade do
    /// painel (0.35 e 0.70): o número que o jogador viu cozinhando é o que vira humor.
    /// </summary>
    public static Moodlet MoodletFor(Meal meal)
    {
        float q = meal.Quality;
        float minutes = meal.Dish.MoodletMinutes;
        return q switch
        {
            >= 0.80f => new Moodlet("Refeição deliciosa", 25f, minutes, fromFood: true),
            >= 0.55f => new Moodlet("Bem alimentado", 12f, minutes, fromFood: true),
            >= 0.35f => new Moodlet("Comeu qualquer coisa", 0f, 60f, fromFood: true),
            _ => new Moodlet("Comida ruim", -15f, 180f, fromFood: true),
        };
    }

    internal EatResult Eat(Meal meal, Random rng)
    {
        meal.TakeServing();

        bool poisoned = rng.NextDouble() < meal.PoisoningChance;
        float hunger = HungerAfter(meal) - Hunger;
        float thirst = ThirstAfter(meal) - Thirst;
        if (poisoned) { hunger *= 0.5f; thirst *= 0.5f; }

        var moodlet = poisoned ? new Moodlet("Enjoado", PoisonedMood, PoisonedMinutes) : MoodletFor(meal);

        Hunger += hunger;
        Thirst += thirst;

        // Um moodlet de comida por vez: comer de novo substitui, não empilha — senão três
        // porções seguidas virariam euforia.
        _moodlets.RemoveAll(m => m.FromFood);
        if (poisoned) _moodlets.RemoveAll(m => m.Name == moodlet.Name);
        _moodlets.Add(moodlet);

        return new EatResult(meal.Name, hunger, thirst, moodlet, poisoned);
    }

    internal void PassHours(float hours)
    {
        if (hours < 0f) throw new ArgumentOutOfRangeException(nameof(hours));
        Hunger = Math.Max(0f, Hunger - HungerDecayPerHour * hours);
        Thirst = Math.Max(0f, Thirst - ThirstDecayPerHour * hours);
        foreach (var m in _moodlets) m.MinutesLeft -= hours * 60f;
        _moodlets.RemoveAll(m => m.MinutesLeft <= 0f);
    }
}
