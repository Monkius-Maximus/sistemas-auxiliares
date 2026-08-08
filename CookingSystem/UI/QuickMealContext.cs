using System;
using System.Collections.Generic;
using System.Linq;
using LifeSim.Ui;

namespace LifeSim.Cooking;

/// <summary>
/// O menu rápido no mesmo shell do painel manual: a lista estilo The Sims vira uma grade
/// de escolha única, e as porções da âncora escolhida viram a lista de "tenho / preciso".
///
/// É a prova de que a estrutura se paga: dois jeitos de cozinhar, zero janelas diferentes.
/// A qualidade prevista aqui é o mesmo <see cref="CookedDish"/> que o preparo vai produzir —
/// quem calculou foi o <see cref="QuickMealPlanner"/>, com o avaliador de verdade.
/// </summary>
public static class QuickMealContext
{
    private const float PanelWidth = 1040f;

    public static PanelContext Build(
        IReadOnlyDictionary<IngredientDef, int> pantry,
        IReadOnlyList<QuickMealOption> options,
        QuickMealOption selected,
        Action<QuickMealOption> onSelect,
        Action onPrepare,
        Action onManual)
    {
        ArgumentNullException.ThrowIfNull(pantry);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(selected);

        // Menu vazio não é estado de tela, é interação que não deveria ter sido oferecida.
        if (options.Count == 0)
            throw new InvalidOperationException("Menu rápido sem opções: o Sim não conhece receita nenhuma.");
        if (!options.Contains(selected))
            throw new InvalidOperationException("A opção marcada não é uma das listadas — replaneje o menu inteiro.");

        var dish = selected.CanMake ? selected.Preview : null;
        var portions = selected.Portions
                               .Select(p => (p.Def, p.Units, Have: pantry[p.Def]))
                               .ToList();
        int missing = portions.Count(p => p.Have < p.Units);

        return new PanelContext
        {
            Title = "O que preparar?",
            Crumb = "fogão · menu rápido",
            Width = PanelWidth,

            Actions = new PanelRegion
            {
                Title = "Ações",
                Body = new VerbList
                {
                    Verbs = new[]
                    {
                        new Verb
                        {
                            Name = "Preparar manualmente…",
                            Note = "Abre o painel completo: você escolhe recipiente e quantidades.",
                            OnUse = onManual,
                        },
                    },
                },
            },

            Primary = new PanelRegion
            {
                Title = "Pratos que o Sim sabe",
                Count = $"{options.Count} conhecidos",
                Body = new SlotGrid
                {
                    Mode = SlotGridMode.Select,
                    Columns = 3,
                    Slots = options.Select(option => new PanelSlot
                    {
                        Name = option.Name,
                        Icon = option.Base.Icon,
                        Tint = option.Base.TintColor,
                        Sub = option.CanMake
                            ? $"{option.Preview.Quality:P0} · custo {option.Cost}"
                            : option.Blocker,
                        Warn = !option.CanMake,
                        Selected = ReferenceEquals(option, selected),
                        // Opção bloqueada continua clicável: é na lista de porções que o
                        // jogador descobre o que falta, e é isso que o manda para a loja.
                        OnPick = () => onSelect(option),
                    }).ToList(),
                },
            },

            Secondary = new PanelRegion
            {
                Title = "Porções",
                Count = missing == 0 ? "tudo na despensa" : $"{missing} em falta",
                Body = new Checklist
                {
                    Rows = portions.Select(p => new ChecklistRow
                    {
                        Name = p.Def.DisplayName,
                        Icon = p.Def.Icon,
                        Tint = p.Def.TintColor,
                        Note = p.Def.IsSeasoning ? "tempero" : "ingrediente",
                        Tally = $"{p.Have} / {p.Units}",
                        Met = p.Have >= p.Units,
                    }).ToList(),
                },
            },

            Preview = new PreviewCard
            {
                Title = "Prato previsto",
                Art = selected.Base.Icon,
                Tint = selected.Base.TintColor,
                Name = selected.Name,
                Description = selected.CanMake
                    ? "Um clique resolve: enche o recipiente, cozinha e devolve o prato."
                    : selected.Blocker,
                Tags = Tags(selected),
            },

            Readout = DishReadout.Of(dish,
                selected.CanMake ? "Qualidade prevista" : "Indisponível",
                selected.CanMake
                    ? "O número previsto é o número que o prato vai ter."
                    : selected.Blocker),

            Commit = new CommitAction
            {
                Label = selected.CanMake ? "Preparar" : "Indisponível",
                Meta = $"custo {selected.Cost}",
                Enabled = selected.CanMake,
                Hint = selected.CanMake
                    ? "Consome as porções listadas de uma vez."
                    : selected.Blocker,
                OnRun = onPrepare,
            },
        };
    }

    private static IReadOnlyList<string> Tags(QuickMealOption option) =>
        option.CanMake
            ? new[]
            {
                option.Base.DisplayName,
                FlavorProfile.Label(option.Preview.DominantAxis).ToLowerInvariant(),
                $"custo {option.Cost}",
            }
            : new[] { option.Base.DisplayName, $"custo {option.Cost}" };
}
