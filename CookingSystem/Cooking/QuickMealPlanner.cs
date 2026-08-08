using System;
using System.Collections.Generic;
using System.Linq;

namespace LifeSim.Cooking;

/// <summary>
/// Monta o menu rápido — a lista estilo The Sims que aparece ao clicar no fogão.
///
/// Nada aqui é conteúdo novo: a lista é derivada das <see cref="RecipeAnchor"/> que o Sim
/// conhece, com as porções que já estão na própria âncora. Custo e qualidade prevista saem
/// do mesmo <see cref="DishEvaluator"/> que o painel manual usa, então o número mostrado na
/// lista é o número que o prato realmente vai ter.
///
/// Custo de execução: uma avaliação por opção viável, só quando o menu abre.
/// </summary>
public static class QuickMealPlanner
{
    public static List<QuickMealOption> Plan(
        IReadOnlyDictionary<IngredientDef, int> pantry,
        IReadOnlyList<BaseItemDef> bases,
        IReadOnlyList<RecipeAnchor> anchors,
        IReadOnlyCollection<string> knownAnchorNames,
        int cookingLevel)
    {
        ArgumentNullException.ThrowIfNull(pantry);
        ArgumentNullException.ThrowIfNull(bases);
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(knownAnchorNames);

        var defsById = pantry.Keys.ToDictionary(d => d.Id, d => d);
        var basesById = bases.ToDictionary(b => b.Id, b => b);
        var stockById = pantry.ToDictionary(kv => kv.Key.Id, kv => kv.Value);

        return anchors
            .Where(a => knownAnchorNames.Contains(a.DishName))
            .Select(a => Build(a, basesById, defsById, stockById, pantry, cookingLevel))
            .OrderByDescending(o => o.CanMake)
            .ThenByDescending(o => o.Preview?.Quality ?? 0f)
            .ToList();
    }

    private static QuickMealOption Build(
        RecipeAnchor anchor,
        IReadOnlyDictionary<string, BaseItemDef> basesById,
        IReadOnlyDictionary<string, IngredientDef> defsById,
        IReadOnlyDictionary<string, int> stockById,
        IReadOnlyDictionary<IngredientDef, int> pantry,
        int cookingLevel)
    {
        var baseItem = basesById[anchor.BaseItemId];
        var portions = anchor.Portions
                             .Select(kv => (Def: defsById[kv.Key], Units: kv.Value))
                             .ToList();

        int cost = portions.Sum(p => p.Def.PricePerUnit * p.Units);

        var missing = portions.Where(p => stockById[p.Def.Id] < p.Units)
                              .Select(p => p.Def.DisplayName)
                              .ToList();
        if (missing.Count > 0)
            return Blocked(anchor, baseItem, cost, portions, $"Falta: {string.Join(", ", missing)}");

        int ingredientCount = portions.Count(p => !p.Def.IsSeasoning);
        if (ingredientCount > baseItem.IngredientSlotsForLevel(cookingLevel))
            return Blocked(anchor, baseItem, cost, portions, "Perícia insuficiente para tantos ingredientes");

        // Sessão descartável só para prever o resultado. Ela devolve tudo à despensa ao sair
        // de escopo porque nunca chamamos Cook() — o estoque real não é tocado aqui.
        var session = new CookingSession(pantry, cookingLevel);
        session.SetBase(baseItem);
        foreach (var (def, units) in portions)
            session.AddUnit(def, units);

        return new QuickMealOption
        {
            Anchor = anchor,
            Base = baseItem,
            Name = anchor.DishName,
            Cost = cost,
            CanMake = true,
            Preview = DishEvaluator.Evaluate(session, new[] { anchor }),
            Portions = portions,
        };
    }

    private static QuickMealOption Blocked(
        RecipeAnchor anchor, BaseItemDef baseItem, int cost,
        IReadOnlyList<(IngredientDef Def, int Units)> portions, string reason)
        => new()
        {
            Anchor = anchor,
            Base = baseItem,
            Name = anchor.DishName,
            Cost = cost,
            CanMake = false,
            Blocker = reason,
            Portions = portions,
        };

    /// <summary>
    /// Executa a opção na sessão real do Sim. É o clique único: preenche e cozinha,
    /// sem o painel jamais aparecer.
    /// </summary>
    public static CookedDish Prepare(
        CookingSession session, QuickMealOption option, IReadOnlyList<RecipeAnchor> anchors)
    {
        if (!option.CanMake)
            throw new InvalidOperationException($"Opção indisponível: {option.Blocker}");

        session.Clear();
        session.SetBase(option.Base);
        foreach (var (def, units) in option.Portions)
            session.AddUnit(def, units);

        return session.Cook(anchors);
    }
}
