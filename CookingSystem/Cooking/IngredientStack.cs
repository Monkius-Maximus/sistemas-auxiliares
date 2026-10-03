using System;
using System.Collections.Generic;
using System.Linq;

namespace LifeSim.Cooking;

/// <summary>
/// Uma porção de um ingrediente dentro do prato. Lembra de que lote veio cada unidade: o
/// frescor do prato sai daí, e tirar uma unidade devolve à despensa a mesma idade que entrou —
/// senão tirar e pôr de volta rejuvenesceria ou envelheceria o estoque.
/// </summary>
public sealed class IngredientStack
{
    private readonly List<Portion> _portions = new();

    public IngredientStack(IngredientDef def, IReadOnlyList<Portion> portions)
    {
        Def = def ?? throw new ArgumentNullException(nameof(def));
        Add(portions);
    }

    public IngredientDef Def { get; }
    public int Units => _portions.Sum(p => p.Units);
    public float Weight => Units * Def.WeightPerUnit;

    /// <summary>Frescor médio por unidade. É linear, então pesa no prato exatamente como as unidades somadas.</summary>
    public float Freshness =>
        _portions.Sum(p => Def.FreshnessAt(p.AgeDays) * p.Units) / Units;

    /// <summary>Massa de unidades estragadas. É o que gera risco de intoxicação.</summary>
    public float RottenWeight =>
        _portions.Where(p => Cooking.Freshness.StateOf(Def.FreshnessAt(p.AgeDays)) == Spoilage.Rotten)
                 .Sum(p => p.Units) * Def.WeightPerUnit;

    public void Add(IReadOnlyList<Portion> portions)
    {
        ArgumentNullException.ThrowIfNull(portions);
        int adding = portions.Sum(p => p.Units);
        if (adding <= 0)
            throw new ArgumentOutOfRangeException(nameof(portions), "Stack precisa de pelo menos 1 unidade.");
        if (Units + adding > Def.MaxUnitsInDish)
            throw new InvalidOperationException(
                $"{Def.DisplayName} aceita no máximo {Def.MaxUnitsInDish} unidades no prato.");
        _portions.AddRange(portions);
    }

    /// <summary>Tira as últimas unidades que entraram e devolve de que lote eram.</summary>
    public IReadOnlyList<Portion> Remove(int units)
    {
        if (units <= 0)
            throw new ArgumentOutOfRangeException(nameof(units));
        if (units > Units)
            throw new InvalidOperationException("Não há tanta quantidade nesse stack.");

        var removed = new List<Portion>();
        while (units > 0)
        {
            var last = _portions[^1];
            int now = Math.Min(units, last.Units);
            removed.Add(last with { Units = now });
            units -= now;
            if (now == last.Units) _portions.RemoveAt(_portions.Count - 1);
            else _portions[^1] = last with { Units = last.Units - now };
        }
        return removed;
    }

    /// <summary>Tira as unidades estragadas, de qualquer posição. Devolve quantas saíram.</summary>
    public int RemoveRotten()
    {
        int before = Units;
        _portions.RemoveAll(p => Cooking.Freshness.StateOf(Def.FreshnessAt(p.AgeDays)) == Spoilage.Rotten);
        return before - Units;
    }

    public int RottenUnits =>
        _portions.Where(p => Cooking.Freshness.StateOf(Def.FreshnessAt(p.AgeDays)) == Spoilage.Rotten)
                 .Sum(p => p.Units);

    /// <summary>Tudo de uma vez, com as idades — para devolver o prato inteiro à despensa.</summary>
    public IReadOnlyList<Portion> All => _portions.ToList();
}
