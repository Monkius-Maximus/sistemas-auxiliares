using System;
using System.Collections.Generic;
using System.Linq;

namespace LifeSim.Cooking;

/// <summary>
/// A despensa em lotes. Dois ovos da mesma definição comprados em dias diferentes não são a
/// mesma coisa: um pode estar fresco e o outro passado, e é essa diferença que dá sentido a
/// escolher qual usar. Por isso a unidade de estoque é o lote (ingrediente, idade, unidades),
/// não um contador por ingrediente.
///
/// Só a <see cref="CookingSession"/> tira e devolve unidades — é ela que garante que despensa e
/// prato mudam na mesma operação. Quem está fora da sessão lê; o tempo passa pela sessão.
/// </summary>
public sealed class Pantry
{
    private sealed class Lot
    {
        public float AgeDays;
        public int Units;
    }

    private readonly Dictionary<string, IngredientDef> _defs = new();
    private readonly Dictionary<string, List<Lot>> _lots = new();

    public Pantry(IEnumerable<(IngredientDef Def, int Units, float AgeDays)> lots)
    {
        ArgumentNullException.ThrowIfNull(lots);
        foreach (var (def, units, age) in lots)
        {
            ArgumentNullException.ThrowIfNull(def);
            Register(def);
            if (units > 0) Merge(def.Id, age, units);
        }
    }

    private Pantry() { }

    public IEnumerable<IngredientDef> Defs => _defs.Values;

    public int UnitsOf(IngredientDef def) => _lots[def.Id].Sum(l => l.Units);

    /// <summary>Unidades nesse estado de frescor. É o que o ladrilho mostra como alerta.</summary>
    public int UnitsIn(IngredientDef def, Spoilage state) =>
        _lots[def.Id].Where(l => Freshness.StateOf(def.FreshnessAt(l.AgeDays)) == state)
                     .Sum(l => l.Units);

    public int RottenUnits => _defs.Values.Sum(d => UnitsIn(d, Spoilage.Rotten));

    /// <summary>Lotes do mais velho ao mais novo.</summary>
    public IReadOnlyList<Portion> LotsOf(IngredientDef def) =>
        _lots[def.Id].Select(l => new Portion(l.AgeDays, l.Units)).ToList();

    /// <summary>Cópia independente — para prever um prato sem tocar no estoque real.</summary>
    public Pantry Clone()
    {
        var copy = new Pantry();
        foreach (var def in _defs.Values)
        {
            copy.Register(def);
            foreach (var lot in _lots[def.Id])
                copy.Merge(def.Id, lot.AgeDays, lot.Units);
        }
        return copy;
    }

    // ---------------------------------------------------------------
    // Mutação: só a CookingSession chama.
    // ---------------------------------------------------------------

    internal IReadOnlyList<Portion> Take(IngredientDef def, int units, PickOrder order)
    {
        if (units <= 0)
            throw new ArgumentOutOfRangeException(nameof(units));
        if (UnitsOf(def) < units)
            throw new InvalidOperationException($"Sem {def.DisplayName} suficiente na despensa.");

        var lots = _lots[def.Id];
        var sequence = order == PickOrder.OldestFirst
            ? lots.OrderByDescending(l => l.AgeDays)
            : lots.OrderBy(l => l.AgeDays);

        var taken = new List<Portion>();
        int remaining = units;
        foreach (var lot in sequence.ToList())
        {
            int now = Math.Min(remaining, lot.Units);
            lot.Units -= now;
            remaining -= now;
            taken.Add(new Portion(lot.AgeDays, now));
            if (remaining == 0) break;
        }
        lots.RemoveAll(l => l.Units == 0);
        return taken;
    }

    /// <summary>Unidades novas chegando de fora — compra, presente, colheita.</summary>
    internal void Receive(IngredientDef def, int units, float ageDays)
    {
        if (units <= 0) throw new ArgumentOutOfRangeException(nameof(units));
        Register(def);
        Merge(def.Id, ageDays, units);
    }

    internal void Return(IngredientDef def, IEnumerable<Portion> portions)
    {
        foreach (var p in portions)
            Merge(def.Id, p.AgeDays, p.Units);
    }

    /// <summary>O tempo passa para tudo que está guardado. O prato em preparo não envelhece.</summary>
    internal void Age(float days)
    {
        if (days < 0f)
            throw new ArgumentOutOfRangeException(nameof(days));
        foreach (var lot in _lots.Values.SelectMany(l => l))
            lot.AgeDays += days;
    }

    /// <summary>Joga fora o que estragou. Devolve quantas unidades saíram.</summary>
    internal int DiscardRotten()
    {
        int discarded = 0;
        foreach (var def in _defs.Values)
        {
            discarded += _lots[def.Id].Where(l => IsRotten(def, l)).Sum(l => l.Units);
            _lots[def.Id].RemoveAll(l => IsRotten(def, l));
        }
        return discarded;
    }

    private static bool IsRotten(IngredientDef def, Lot lot) =>
        Freshness.StateOf(def.FreshnessAt(lot.AgeDays)) == Spoilage.Rotten;

    private void Register(IngredientDef def)
    {
        if (_defs.ContainsKey(def.Id)) return;
        _defs[def.Id] = def;
        _lots[def.Id] = new List<Lot>();
    }

    /// <summary>Unidades da mesma idade voltam para o mesmo lote; idade nova abre lote novo.</summary>
    private void Merge(string id, float age, int units)
    {
        var lots = _lots[id];
        var same = lots.FirstOrDefault(l => l.AgeDays == age);
        if (same is not null) same.Units += units;
        else lots.Add(new Lot { AgeDays = age, Units = units });
        lots.Sort((a, b) => b.AgeDays.CompareTo(a.AgeDays));
    }
}
