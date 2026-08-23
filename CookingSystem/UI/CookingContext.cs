using System;
using System.Collections.Generic;
using System.Linq;
using LifeSim.Ui;

namespace LifeSim.Cooking;

/// <summary>
/// O preparo manual escrito como definição de contexto: uma <see cref="CookingSession"/>
/// entra, as sete regiões saem. O <see cref="ContextPanel"/> não sabe o que é um
/// ingrediente — ele só desenha o que está aqui.
///
/// Função pura de sessão para definição, reconstruída inteira a cada <c>Changed</c>.
/// Não guarda nada: a verdade continua sendo a sessão.
/// </summary>
public static class CookingContext
{
    private const float PanelWidth = 1180f;

    public static PanelContext Build(
        CookingSession session,
        IReadOnlyList<BaseItemDef> bases,
        IReadOnlyList<RecipeAnchor> anchors,
        Action onCook,
        Action onClose)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(bases);
        ArgumentNullException.ThrowIfNull(anchors);

        // O mesmo avaliador do ato de cozinhar. Prato vazio não é avaliável — é o único
        // estado em que a sessão não tem número para mostrar.
        var dish = session.Ingredients.Count == 0 ? null : DishEvaluator.Evaluate(session, anchors);

        return new PanelContext
        {
            Title = "Cozinha",
            Crumb = $"fogão · culinária nv {session.CookingLevel}",
            Width = PanelWidth,
            OnClose = onClose,
            FocusEntry = PanelRegionId.Primary,

            Subject = new PanelRegion
            {
                Title = "Recipiente",
                Body = new Picker
                {
                    Note = $"{session.Base.FormName} · {session.IngredientSlots} ingredientes · " +
                           $"{session.SeasoningSlots} temperos",
                    Options = bases.Select(b => new PickerOption
                    {
                        Name = b.DisplayName,
                        Icon = b.Icon,
                        Tint = b.TintColor,
                        Selected = b.Id == session.Base.Id,
                        OnPick = () => session.SetBase(b),
                    }).ToList(),
                },
            },

            Actions = new PanelRegion
            {
                Title = "Ações",
                Body = new VerbList
                {
                    Verbs = new[]
                    {
                        new Verb
                        {
                            Name = "Esvaziar",
                            Note = "Devolve à despensa tudo o que está no recipiente.",
                            Locked = session.Ingredients.Count == 0 && session.Seasonings.Count == 0,
                            OnUse = session.Clear,
                        },
                    },
                },
            },

            Primary = new PanelRegion
            {
                Title = "Ingredientes",
                Count = $"{session.Ingredients.Count} / {session.IngredientSlots}",
                Body = new SlotGrid
                {
                    Mode = SlotGridMode.Quantity,
                    Slots = session.PantryIngredients(seasonings: false)
                                   .Select(def => Slot(session, def))
                                   .ToList(),
                },
            },

            Secondary = new PanelRegion
            {
                Title = "Temperos",
                Count = $"{session.Seasonings.Count} / {session.SeasoningSlots}",
                Body = new SlotGrid
                {
                    Mode = SlotGridMode.Quantity,
                    Slots = session.PantryIngredients(seasonings: true)
                                   .Select(def => Slot(session, def))
                                   .ToList(),
                },
            },

            Preview = new PreviewCard
            {
                Title = "Prato resultante",
                Art = session.Base.Icon,
                Tint = session.Base.TintColor,
                Name = dish?.Name ?? "—",
                Description = Description(dish),
                Tags = Tags(session, dish),
            },

            Readout = DishReadout.Of(dish, Headline(dish), Caption(session, dish)),

            Commit = new CommitAction
            {
                Label = "Cozinhar",
                Meta = session.Base.DisplayName,
                Enabled = dish is not null,
                Hint = dish is null
                    ? "Nada para cozinhar ainda."
                    : "Consome as unidades listadas e esvazia o recipiente.",
                OnRun = onCook,
            },
        };
    }

    /// <summary>
    /// Um ladrilho por item da despensa. As regras de habilitação são as mesmas que a
    /// sessão cobra em <c>AddUnit</c> — a UI desabilita exatamente o que a sessão recusaria,
    /// que é a única forma de nenhuma exceção chegar ao jogador.
    /// </summary>
    private static PanelSlot Slot(CookingSession session, IngredientDef def)
    {
        int inDish = session.UnitsInDish(def);
        int stock = session.AvailableOf(def);

        bool compatible = def.IsSeasoning || session.Base.Accepts(def.Group);
        bool slotFree = inDish > 0 ||
            (def.IsSeasoning
                ? session.Seasonings.Count < session.SeasoningSlots
                : session.Ingredients.Count < session.IngredientSlots);
        bool underCap = inDish < def.MaxUnitsInDish;

        return new PanelSlot
        {
            Name = def.DisplayName,
            Icon = def.Icon,
            Tint = def.TintColor,
            // Ladrilho apagado por incompatibilidade diz isso em vez de mentir "estoque 40".
            Sub = compatible
                ? $"estoque {stock}"
                : $"não vai na {session.Base.DisplayName.ToLowerInvariant()}",
            Warn = !compatible,
            Quantity = inDish,
            Enabled = stock > 0 && slotFree && compatible && underCap,
            OnAdd = () => session.AddUnit(def),
            OnRemove = () => session.RemoveUnit(def),
        };
    }

    private static string Headline(CookedDish dish) =>
        dish is null ? "Recipiente vazio"
        : dish.MatchedAnchor ? "Combinação conhecida ★"
        : "Prato improvisado";

    private static string Description(CookedDish dish) =>
        dish is null ? "Nada dentro ainda."
        : dish.MatchedAnchor ? "O Sim reconhece a combinação: o prato ganha nome próprio e bônus."
        : "Improvisado. O nome vem do ingrediente que domina a massa.";

    private static string Caption(CookingSession session, CookedDish dish)
    {
        if (dish is null)
            return "Adicione um ingrediente para começar.";

        // Vale mais dizer que a perícia é o gargalo do que deixar o jogador otimizar às cegas.
        return dish.CappedBySkill
            ? $"A perícia limita em {dish.QualityCeiling:P0}; o prato daria {dish.RawQuality:P0}."
            : "Qualidade é o produto dos cinco fatores, não a média deles.";
    }

    private static IReadOnlyList<string> Tags(CookingSession session, CookedDish dish) =>
        dish is null
            ? new[] { session.Base.DisplayName }
            : new[]
            {
                session.Base.DisplayName,
                FlavorProfile.Label(dish.DominantAxis).ToLowerInvariant(),
                $"custo {dish.Cost}",
            };
}
