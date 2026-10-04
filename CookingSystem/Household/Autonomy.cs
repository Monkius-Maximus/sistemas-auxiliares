using System.Collections.Generic;
using System.Linq;
using LifeSim.Cooking;

namespace LifeSim.Household;

/// <summary>
/// O que o Sim decide sozinho quando a fome ou a sede apertam. Só decide: escolher é função pura
/// de estado, e quem executa é a <see cref="Household"/>. Separado assim para que a regra possa
/// ser testada sem relógio e sem sorteio.
/// </summary>
public static class Autonomy
{
    /// <summary>
    /// Risco de intoxicação que o Sim aceita enquanto ainda não está faminto. Acima disto ele
    /// prefere cozinhar ou esperar; faminto, come o que tiver.
    /// </summary>
    public const float AcceptableRisk = 0.10f;

    /// <summary>Quanto um ponto de risco pesa contra um ponto de qualidade na escolha.</summary>
    private const float RiskWeight = 2f;

    /// <summary>
    /// Ganho mínimo de fome para valer a pena comer. Abaixo disto o Sim já está quase cheio, e
    /// comer de novo é compulsão — o defeito que o primeiro teste de três dias mostrou.
    /// </summary>
    public const float MinUsefulHunger = 10f;

    /// <summary>
    /// A melhor refeição da geladeira para a fome, ou nula se nenhuma serve. Sede não é motivo
    /// para comer: tem água de graça na pia. Faminto, aceita qualquer coisa — como um Sim de verdade.
    /// </summary>
    public static Meal ChooseMeal(Sim sim, IEnumerable<Meal> meals)
    {
        if (!sim.IsHungry) return null;
        bool desperate = sim.Hunger < Sim.NeedCritical;

        return meals.Where(m => m.Servings > 0)
                    .Where(m => sim.HungerAfter(m) - sim.Hunger >= MinUsefulHunger)
                    .Where(m => desperate || m.PoisoningChance <= AcceptableRisk)
                    .OrderByDescending(m => Score(sim, m))
                    .FirstOrDefault();
    }

    /// <summary>
    /// O que cozinhar quando a geladeira não serve: a opção do menu rápido viável, sem risco de
    /// intoxicação, de maior qualidade. Nula se nenhuma — o Sim não cozinha comida perigosa sozinho.
    /// </summary>
    public static QuickMealOption ChooseRecipe(IEnumerable<QuickMealOption> options) =>
        options.Where(o => o.CanMake && o.Preview.PoisoningChance <= 0f)
               .OrderByDescending(o => o.Preview.Quality)
               .FirstOrDefault();

    /// <summary>Qualidade, mais o que a porção cobre do que está faltando, menos o risco.</summary>
    private static float Score(Sim sim, Meal meal)
    {
        float cover = 0f;
        if (sim.IsHungry) cover += (sim.HungerAfter(meal) - sim.Hunger) / 100f;
        if (sim.IsThirsty) cover += (sim.ThirstAfter(meal) - sim.Thirst) / 100f;
        return meal.Quality + cover - meal.PoisoningChance * RiskWeight;
    }
}
