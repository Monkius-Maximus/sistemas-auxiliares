using System.Collections.Generic;
using System.Linq;

namespace LifeSim.Cooking;

/// <summary>
/// Os modificadores ativos num preparo, resolvidos por alavanca. Ordem fixa e única:
/// <c>(base + Σ somas) × Π multiplicadores</c>. Sem prioridade, sem sobrescrita — dois
/// efeitos sobre a mesma alavanca sempre se compõem, que é o que torna o resultado
/// previsível para quem autora conteúdo.
/// </summary>
public sealed class ModifierStack
{
    public static readonly ModifierStack Empty = new(System.Array.Empty<DishModifier>());

    private readonly IReadOnlyList<DishModifier> _modifiers;

    public ModifierStack(IEnumerable<DishModifier> modifiers)
    {
        _modifiers = modifiers?.Where(m => m is not null).ToList()
                     ?? throw new System.ArgumentNullException(nameof(modifiers));
    }

    public IReadOnlyList<DishModifier> All => _modifiers;

    public float Apply(DishStat stat, float baseValue)
    {
        float added = baseValue;
        float multiplier = 1f;
        foreach (var m in _modifiers)
        {
            if (m.Stat != stat) continue;
            if (m.Op == ModifierOp.Add) added += m.Value;
            else multiplier *= m.Value;
        }
        return added * multiplier;
    }
}
