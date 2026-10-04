using System;
using System.Collections.Generic;
using System.Linq;
using ContextUi;
using LifeSim.Household;

namespace LifeSim.Cooking;

/// <summary>
/// A geladeira escrita como definição de contexto: as refeições prontas, o Sim, e o que uma
/// porção vai fazer com ele antes do clique. É o segundo contexto real do painel — e o primeiro
/// que não é preparo —, montado só com primitivos que já existiam.
///
/// Função pura de casa para definição. Não guarda nada: a verdade é o <see cref="Household"/>.
/// </summary>
public static class FridgeContext
{
    private const float PanelWidth = 1180f;

    public static PanelContext Build(
        Household.Household home,
        Meal selected,
        Action<Meal> onSelect,
        Action onEat,
        Action onClose,
        string lastResult)
    {
        ArgumentNullException.ThrowIfNull(home);
        if (selected is not null && !home.Meals.Contains(selected))
            throw new InvalidOperationException("A refeição marcada não está na geladeira.");

        var sim = home.Sim;

        return new PanelContext
        {
            Title = "Geladeira",
            Crumb = $"{sim.Name} · dia {home.Day}, {home.HourOfDay:00}h",
            Width = PanelWidth,
            OnClose = onClose,
            FocusEntry = PanelRegionId.Primary,

            Actions = new PanelRegion
            {
                Title = "Ações",
                Body = new VerbList
                {
                    Verbs = new[]
                    {
                        new Verb
                        {
                            Name = "Beber água",
                            Note = sim.Thirst >= 100f
                                ? $"{sim.Name} não está com sede."
                                : $"Da pia, de graça: sede {sim.Thirst:0} → {Math.Min(100f, sim.Thirst + Sim.WaterThirst):0}.",
                            Locked = sim.Thirst >= 100f,
                            OnUse = home.DrinkWater,
                        },
                        new Verb
                        {
                            Name = "Jogar fora este prato",
                            Locked = selected is null,
                            Note = selected is null
                                ? "Escolha uma refeição primeiro."
                                : $"Descarta {selected.Servings} {(selected.Servings == 1 ? "porção" : "porções")} de {selected.Dish.Name}.",
                            OnUse = () => home.Discard(selected),
                        },
                        new Verb
                        {
                            Name = "Jogar fora as sobras estragadas",
                            Locked = home.RottenMeals == 0,
                            Note = home.RottenMeals == 0
                                ? "Nenhuma sobra estragada."
                                : $"{home.RottenMeals} {(home.RottenMeals == 1 ? "refeição estragada" : "refeições estragadas")}.",
                            OnUse = home.DiscardRottenMeals,
                        },
                    },
                },
            },

            Primary = home.Meals.Count == 0 ? EmptyShelf() : MealGrid(home, selected, onSelect),

            Secondary = new PanelRegion
            {
                Title = $"Como {sim.Name} está",
                Count = selected is null ? "" : "agora → depois de comer",
                Body = new Checklist { Rows = Needs(sim, selected) },
            },

            Preview = Preview(selected),
            Readout = Readout(sim, selected),

            Commit = new CommitAction
            {
                Label = "Comer uma porção",
                Meta = selected is null ? "" : $"{selected.Servings} na geladeira",
                Enabled = selected is not null,
                Hint = lastResult
                       ?? (selected is null ? "Nada escolhido."
                           : selected.PoisoningChance > 0f
                               ? $"{selected.PoisoningChance:P0} de chance de {sim.Name} passar mal."
                               : "O que sobrar volta para a geladeira e continua envelhecendo."),
                OnRun = onEat,
            },
        };
    }

    private static PanelRegion EmptyShelf() => new()
    {
        Title = "Prontas para comer",
        Count = "vazia",
        Body = new Checklist
        {
            Rows = new[]
            {
                new ChecklistRow
                {
                    Name = "Nada pronto",
                    Tint = Godot.Colors.Transparent,
                    Note = "cozinhe algo — o que sobrar vem para cá",
                    Met = false,
                },
            },
        },
    };

    private static PanelRegion MealGrid(Household.Household home, Meal selected, Action<Meal> onSelect) => new()
    {
        Title = "Prontas para comer",
        Count = $"{home.Meals.Count} {(home.Meals.Count == 1 ? "refeição" : "refeições")}",
        Body = new SlotGrid
        {
            Mode = SlotGridMode.Select,
            Columns = 3,
            Slots = home.Meals.Select(m => new PanelSlot
            {
                Name = m.Dish.Name,
                Icon = m.Vessel?.Icon,
                Tint = m.Vessel?.TintColor ?? Godot.Colors.Gray,
                Sub = m.State switch
                {
                    Spoilage.Rotten => "estragada",
                    Spoilage.Stale => $"passada · {m.Servings} porç.",
                    _ => $"{m.Servings} porç. · {m.Quality:P0}",
                },
                Warn = m.State != Spoilage.Fresh,
                Selected = ReferenceEquals(m, selected),
                OnPick = () => onSelect(m),
            }).ToList(),
        },
    };

    /// <summary>
    /// Fome, sede e humor, com o "depois" ao lado quando há refeição escolhida. É o preview do
    /// comer: o jogador vê o efeito antes do clique, como vê a nota antes de cozinhar.
    /// </summary>
    private static IReadOnlyList<ChecklistRow> Needs(Sim sim, Meal meal)
    {
        string Tally(float now, float? after) =>
            after is null ? $"{now:0}" : $"{now:0} → {after.Value:0}";

        float moodAfter = meal is null ? sim.Mood : sim.MoodAfter(meal);

        return new[]
        {
            new ChecklistRow
            {
                Name = "Fome", Tint = new Godot.Color("d9a24a"),
                Note = NeedNote(sim.Hunger, "com fome", "faminto"),
                Tally = Tally(sim.Hunger, meal is null ? null : sim.HungerAfter(meal)),
                Met = !sim.IsHungry,
            },
            new ChecklistRow
            {
                Name = "Sede", Tint = new Godot.Color("5b8fc7"),
                Note = NeedNote(sim.Thirst, "com sede", "desidratado"),
                Tally = Tally(sim.Thirst, meal is null ? null : sim.ThirstAfter(meal)),
                Met = !sim.IsThirsty,
            },
            new ChecklistRow
            {
                Name = "Humor", Tint = new Godot.Color("b07cc6"),
                Note = sim.Moodlets.Count == 0 ? "sem moodlet" : string.Join(", ", sim.Moodlets.Select(m => m.Name)),
                Tally = meal is null ? $"{sim.Mood:+0;-0;0}" : $"{sim.Mood:+0;-0;0} → {moodAfter:+0;-0;0}",
                Met = sim.Mood >= 0f,
            },
        };
    }

    /// <summary>Os mesmos dois cortes que dão os moodlets de necessidade: a nota e o humor dizem a mesma coisa.</summary>
    private static string NeedNote(float need, string low, string critical) =>
        need < Sim.NeedCritical ? critical : need < Sim.NeedLow ? low : "";

    private static PreviewCard Preview(Meal meal) => meal is null
        ? new PreviewCard { Title = "Refeição", Name = "—", Description = "Nada escolhido.", Tint = Godot.Colors.Transparent }
        : new PreviewCard
        {
            Title = "Refeição",
            Art = meal.Vessel?.Icon,
            Tint = meal.Vessel?.TintColor ?? Godot.Colors.Gray,
            Name = meal.Name,
            Description = meal.State switch
            {
                Spoilage.Rotten => "Ficou tempo demais na geladeira. Cozinhar de novo não salva.",
                Spoilage.Stale => "Ainda dá para comer, mas já perdeu sabor.",
                _ => $"Fresca por mais {meal.DaysUntilStale:0.#} dia(s) na geladeira.",
            },
            Tags = new[]
            {
                meal.Vessel?.DisplayName ?? "prato",
                $"{meal.Servings}/{meal.TotalServings} porções",
                $"{meal.AgeDays:0.#} d na geladeira",
            },
        };

    private static Readout Readout(Sim sim, Meal meal)
    {
        if (meal is null)
            return new Readout
            {
                Title = "Uma porção",
                Headline = "Nada escolhido",
                Value = "—",
                Fill = 0f,
                Caption = "Escolha uma refeição para ver o que ela faz.",
            };

        var moodlet = Sim.MoodletFor(meal);
        return new Readout
        {
            Title = "Uma porção",
            Headline = "Qualidade agora",
            Value = $"{meal.Quality:P0}",
            Fill = meal.Quality,
            Caption = meal.PoisoningChance > 0f
                ? $"{meal.PoisoningChance:P0} de chance de passar mal. Se passar, vira \"Enjoado\" e devolve metade."
                : $"Deve dar \"{moodlet.Name}\" ({moodlet.Mood:+0;-0;0} de humor).",
            Stats = new[]
            {
                new ReadoutStat { Key = "Fome", Value = $"+{sim.HungerAfter(meal) - sim.Hunger:0}" },
                new ReadoutStat { Key = "Sede", Value = $"+{sim.ThirstAfter(meal) - sim.Thirst:0}" },
                new ReadoutStat
                {
                    // O valor do moodlet, não o ganho: o moodlet de comida substitui o anterior,
                    // e o ganho real aparece em "Como o Sim está".
                    Key = "Moodlet", Value = $"{moodlet.Mood:+0;-0;0}",
                    Tone = moodlet.Mood < 0 ? StatTone.Alert : StatTone.Neutral,
                },
                new ReadoutStat
                {
                    Key = "Intoxicação", Value = $"{meal.PoisoningChance:P0}",
                    Tone = meal.PoisoningChance > 0f ? StatTone.Alert : StatTone.Neutral,
                },
                new ReadoutStat { Key = "Porções", Value = $"{meal.Servings}/{meal.TotalServings}" },
                new ReadoutStat
                {
                    Key = "Geladeira", Value = $"{meal.AgeDays:0.#} d",
                    Tone = meal.State == Spoilage.Fresh ? StatTone.Neutral : StatTone.Alert,
                },
            },
        };
    }
}
