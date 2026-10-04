using System;
using System.Collections.Generic;
using System.Linq;
using LifeSim.Cooking;

namespace LifeSim.Household;

/// <summary>O que o Sim sabe cozinhar sozinho: recipientes, receitas e quais delas ele conhece.</summary>
public sealed record Cookbook(
    IReadOnlyList<BaseItemDef> Bases,
    IReadOnlyList<RecipeAnchor> Anchors,
    IReadOnlyCollection<string> KnownAnchorNames);

/// <summary>Uma linha do diário da casa: o que aconteceu, e quando.</summary>
public sealed record HouseholdEvent(float Hours, string Text)
{
    public string Clock => $"dia {(int)(Hours / 24f) + 1}, {(int)(Hours % 24f):00}h";
}

/// <summary>
/// A casa: a cozinha (despensa e prato em preparo), as refeições prontas na geladeira, o Sim e o
/// relógio. Existe para que o tempo passe num lugar só — passar uma hora envelhece ingrediente,
/// envelhece sobra e cansa o Sim na mesma operação, e ninguém precisa lembrar de chamar os três.
/// </summary>
public sealed class Household
{
    /// <summary>Qualquer mudança: na cozinha, na geladeira, no Sim ou no relógio.</summary>
    public event Action Changed;

    /// <summary>Quantas linhas o diário guarda. É memória de tela, não histórico do jogo.</summary>
    public const int LogSize = 30;

    private readonly List<Meal> _meals = new();
    private readonly List<HouseholdEvent> _log = new();
    private readonly Random _rng;

    /// <param name="cookbook">O que o Sim sabe cozinhar sozinho. Nulo: ele só come o que já está pronto.</param>
    /// <param name="seed">Semente do sorteio de comer. Fixa em teste, aleatória no jogo.</param>
    public Household(CookingSession kitchen, Sim sim, Cookbook cookbook = null, int? seed = null)
    {
        Kitchen = kitchen ?? throw new ArgumentNullException(nameof(kitchen));
        Sim = sim ?? throw new ArgumentNullException(nameof(sim));
        Cookbook = cookbook;
        _rng = seed is null ? new Random() : new Random(seed.Value);

        // A cozinha avisa sozinha; a casa só repassa, para o host ter um evento só para escutar.
        Kitchen.Changed += () => Changed?.Invoke();
    }

    public CookingSession Kitchen { get; }
    public Sim Sim { get; }
    public Cookbook Cookbook { get; }

    /// <summary>
    /// Livre-arbítrio: o Sim come e cozinha sozinho quando a fome ou a sede apertam. Desligado,
    /// ele espera o jogador — e sofre as consequências.
    /// </summary>
    public bool Autonomous { get; set; } = true;

    /// <summary>O diário, do mais recente ao mais antigo.</summary>
    public IReadOnlyList<HouseholdEvent> Log => _log;

    /// <summary>
    /// Salário diário, às 9h. Substituto declarado do sistema de emprego, que não existe ainda:
    /// sem renda nenhuma, toda casa morre de fome com a ferramenta funcionando "certo".
    /// </summary>
    public const int DailyIncome = 120;
    public const int PaydayHour = 9;

    /// <summary>Dinheiro da casa. Só a compra tira; só o salário põe.</summary>
    public int Funds { get; private set; } = 150;

    /// <summary>Horas desde o começo do jogo.</summary>
    public float Hours { get; private set; } = 8f;

    /// <summary>Dia do calendário, contado a partir de 1 como o jogador conta.</summary>
    public int Day => (int)(Hours / 24f) + 1;
    public int HourOfDay => (int)(Hours % 24f);

    /// <summary>Refeições prontas, da mais nova à mais velha.</summary>
    public IReadOnlyList<Meal> Meals => _meals;

    /// <summary>O prato confirmado vira refeição na geladeira. É o fim do preparo.</summary>
    public Meal Store(CookedDish dish)
    {
        var meal = new Meal(dish, Kitchen.Base);
        _meals.Insert(0, meal);
        Record($"Prato pronto: {dish.Name}, {meal.TotalServings} porções para a geladeira.");
        Changed?.Invoke();
        return meal;
    }

    /// <summary>Uma porção. Refeição que acaba sai da geladeira.</summary>
    public EatResult Eat(Meal meal)
    {
        RequireStored(meal);
        var result = Sim.Eat(meal, _rng);
        if (meal.Servings == 0) _meals.Remove(meal);
        Record(Describe(result));
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

    /// <summary>
    /// O relógio do mundo. Tudo que envelhece ou cansa passa por aqui, uma hora por vez: a
    /// autonomia precisa poder agir no meio de um dia que passa, não só no fim dele.
    /// </summary>
    public void AdvanceHours(float hours)
    {
        if (hours <= 0f) throw new ArgumentOutOfRangeException(nameof(hours));

        for (float left = hours; left > 0f; left -= 1f)
        {
            float step = MathF.Min(1f, left);
            int dayBefore = (int)MathF.Floor((Hours - PaydayHour) / 24f);
            Hours += step;
            if ((int)MathF.Floor((Hours - PaydayHour) / 24f) > dayBefore)
            {
                Funds += DailyIncome;
                Record($"Salário: +${DailyIncome}. Saldo ${Funds}.");
            }
            foreach (var text in Sim.PassHours(step)) Record(text);
            foreach (var meal in _meals) meal.Age(step / 24f);
            Kitchen.AdvanceTime(step / 24f);

            if (Autonomous) ActOnNeeds();
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// Fecha a compra: tira do saldo e põe na despensa, com a idade da prateleira do vendedor.
    /// Recusa o que a tela também recusa — fechado, carrinho vazio, dinheiro curto.
    /// </summary>
    public void Buy(ShoppingTrip trip)
    {
        ArgumentNullException.ThrowIfNull(trip);
        if (!trip.Vendor.IsOpenAt(HourOfDay))
            throw new InvalidOperationException($"{trip.Vendor.DisplayName} está fechado.");
        if (trip.Items == 0)
            throw new InvalidOperationException("Carrinho vazio.");
        if (trip.Total > Funds)
            throw new InvalidOperationException($"Faltam ${trip.Total - Funds}.");

        int total = trip.Total, items = trip.Items;
        Funds -= total;
        Kitchen.Receive(trip.Cart.Select(kv => (kv.Key, kv.Value, trip.Vendor.StockAgeDays)).ToList());
        Record($"Compra em {trip.Vendor.DisplayName}: {items} itens por ${total}. Saldo ${Funds}.");
        trip.Clear();
        Changed?.Invoke();
    }

    /// <summary>Um copo d'água. O jogador também pode mandar; a autonomia faz isso sozinha.</summary>
    public void DrinkWater()
    {
        float gained = Sim.DrinkWater();
        Record($"{Sim.Name} bebeu água: sede +{gained:0}.");
        Changed?.Invoke();
    }

    /// <summary>
    /// O que o Sim faz sozinho quando a necessidade aperta. Sede: água, que é de graça. Fome: come
    /// da geladeira; se nada serve, cozinha pelo menu rápido e come; se nem isso, registra.
    /// </summary>
    private void ActOnNeeds()
    {
        if (Sim.IsThirsty)
            Record($"Sozinho: {Sim.Name} bebeu água: sede +{Sim.DrinkWater():0}.");

        if (!Sim.IsHungry) return;

        var meal = Autonomy.ChooseMeal(Sim, _meals);
        if (meal is null && Cookbook is not null)
            meal = CookAlone();

        if (meal is null)
        {
            RecordOnce($"{Sim.Name} está com fome, mas não há o que comer nem o que cozinhar.");
            return;
        }

        var result = Sim.Eat(meal, _rng);
        if (meal.Servings == 0) _meals.Remove(meal);
        Record("Sozinho: " + Describe(result));
    }

    private Meal CookAlone()
    {
        var options = QuickMealPlanner.Plan(Kitchen.Pantry, Cookbook.Bases, Cookbook.Anchors,
                                            Cookbook.KnownAnchorNames, Kitchen.CookingLevel, Kitchen.PickOrder);
        var choice = Autonomy.ChooseRecipe(options);
        if (choice is null) return null;

        var dish = QuickMealPlanner.Prepare(Kitchen, choice, Cookbook.Anchors);
        var meal = new Meal(dish, Kitchen.Base);
        _meals.Insert(0, meal);
        Record($"Sozinho: {Sim.Name} cozinhou {dish.Name} ({dish.Quality:P0}, {meal.TotalServings} porções).");
        return meal;
    }

    private string Describe(EatResult r) => r.Poisoned
        ? $"{Sim.Name} comeu {r.MealName} e passou mal."
        : $"{Sim.Name} comeu {r.MealName}: fome +{r.HungerGained:0}, sede +{r.ThirstGained:0}, \"{r.Moodlet.Name}\".";

    private void Record(string text)
    {
        _log.Insert(0, new HouseholdEvent(Hours, text));
        if (_log.Count > LogSize) _log.RemoveAt(_log.Count - 1);
    }

    /// <summary>Não repete a mesma linha a cada hora em que nada muda — o diário viraria spam.</summary>
    private void RecordOnce(string text)
    {
        if (_log.Count > 0 && _log[0].Text == text) return;
        Record(text);
    }

    private void RequireStored(Meal meal)
    {
        if (!_meals.Contains(meal))
            throw new InvalidOperationException("Essa refeição não está na geladeira.");
    }
}
