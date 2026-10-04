using System;
using System.Collections.Generic;
using System.Linq;
using LifeSim.Cooking;

namespace LifeSim.Household;

/// <summary>
/// A casa: a cozinha (despensa e prato em preparo), as refeições prontas na geladeira, o Sim e o
/// relógio. Existe para que o tempo passe num lugar só — passar uma hora envelhece ingrediente,
/// envelhece sobra e cansa o Sim na mesma operação, e ninguém precisa lembrar de chamar os três.
/// </summary>
public sealed class Household
{
    /// <summary>Qualquer mudança: na cozinha, na geladeira, no Sim ou no relógio.</summary>
    public event Action Changed;

    private readonly List<Meal> _meals = new();
    private readonly Random _rng;

    /// <param name="seed">Semente do sorteio de comer. Fixa em teste, aleatória no jogo.</param>
    public Household(CookingSession kitchen, Sim sim, int? seed = null)
    {
        Kitchen = kitchen ?? throw new ArgumentNullException(nameof(kitchen));
        Sim = sim ?? throw new ArgumentNullException(nameof(sim));
        _rng = seed is null ? new Random() : new Random(seed.Value);

        // A cozinha avisa sozinha; a casa só repassa, para o host ter um evento só para escutar.
        Kitchen.Changed += () => Changed?.Invoke();
    }

    public CookingSession Kitchen { get; }
    public Sim Sim { get; }

    /// <summary>Horas desde o começo do jogo.</summary>
    public float Hours { get; private set; } = 8f;

    public int Day => (int)(Hours / 24f);
    public int HourOfDay => (int)(Hours % 24f);

    /// <summary>Refeições prontas, da mais nova à mais velha.</summary>
    public IReadOnlyList<Meal> Meals => _meals;

    /// <summary>O prato confirmado vira refeição na geladeira. É o fim do preparo.</summary>
    public Meal Store(CookedDish dish)
    {
        var meal = new Meal(dish, Kitchen.Base);
        _meals.Insert(0, meal);
        Changed?.Invoke();
        return meal;
    }

    /// <summary>Uma porção. Refeição que acaba sai da geladeira.</summary>
    public EatResult Eat(Meal meal)
    {
        RequireStored(meal);
        var result = Sim.Eat(meal, _rng);
        if (meal.Servings == 0) _meals.Remove(meal);
        Changed?.Invoke();
        return result;
    }

    public void Discard(Meal meal)
    {
        RequireStored(meal);
        _meals.Remove(meal);
        Changed?.Invoke();
    }

    public int RottenMeals => _meals.Count(m => m.State == Spoilage.Rotten);

    public void DiscardRottenMeals()
    {
        if (RottenMeals == 0)
            throw new InvalidOperationException("Não há refeição estragada para jogar fora.");
        _meals.RemoveAll(m => m.State == Spoilage.Rotten);
        Changed?.Invoke();
    }

    /// <summary>O relógio do mundo. Tudo que envelhece ou cansa passa por aqui.</summary>
    public void AdvanceHours(float hours)
    {
        if (hours <= 0f) throw new ArgumentOutOfRangeException(nameof(hours));

        Hours += hours;
        Sim.PassHours(hours);
        foreach (var meal in _meals) meal.Age(hours / 24f);
        Kitchen.AdvanceTime(hours / 24f); // dispara Changed pela cozinha
    }

    private void RequireStored(Meal meal)
    {
        if (!_meals.Contains(meal))
            throw new InvalidOperationException("Essa refeição não está na geladeira.");
    }
}
