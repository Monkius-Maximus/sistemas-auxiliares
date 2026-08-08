using System.Collections.Generic;

namespace LifeSim.Cooking;

/// <summary>
/// Uma linha do menu rápido: o que o Sim sabe fazer, quanto custa e quão bem vai sair.
/// Imutável — se a despensa mudou, replaneje a lista inteira.
/// </summary>
public sealed class QuickMealOption
{
    public required RecipeAnchor Anchor { get; init; }
    public required BaseItemDef Base { get; init; }
    public required string Name { get; init; }
    public required int Cost { get; init; }

    /// <summary>Falso quando falta ingrediente ou slots. <see cref="Blocker"/> diz o porquê.</summary>
    public required bool CanMake { get; init; }

    /// <summary>Nulo quando <see cref="CanMake"/> é verdadeiro.</summary>
    public string Blocker { get; init; }

    /// <summary>Só existe quando dá para fazer — prever qualidade exige avaliar de verdade.</summary>
    public CookedDish Preview { get; init; }

    public IReadOnlyList<(IngredientDef Def, int Units)> Portions { get; init; }
}
