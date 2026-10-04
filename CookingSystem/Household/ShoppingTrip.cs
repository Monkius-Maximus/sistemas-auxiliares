using System;
using System.Collections.Generic;
using System.Linq;
using LifeSim.Cooking;

namespace LifeSim.Household;

/// <summary>
/// Uma ida às compras: o vendedor escolhido e o carrinho. É estado da interação — como o prato em
/// preparo é da cozinha —, então mora no modelo e não na tela. Fechar a loja sem comprar descarta
/// o carrinho; nada sai do saldo nem entra na despensa antes de <see cref="Household.Buy"/>.
/// </summary>
public sealed class ShoppingTrip
{
    public event Action Changed;

    private readonly Dictionary<IngredientDef, int> _cart = new();

    public ShoppingTrip(VendorDef vendor)
    {
        Vendor = vendor ?? throw new ArgumentNullException(nameof(vendor));
    }

    public VendorDef Vendor { get; private set; }

    public IReadOnlyDictionary<IngredientDef, int> Cart => _cart;

    public int UnitsOf(IngredientDef def) => _cart.TryGetValue(def, out var n) ? n : 0;

    public int Total => _cart.Sum(kv => Vendor.PriceOf(kv.Key) * kv.Value);

    public int Items => _cart.Values.Sum();

    /// <summary>
    /// Trocar de vendedor mantém no carrinho o que o novo também vende, e reprecifica tudo.
    /// O que ele não vende sai — carrinho não pode ter item que ninguém ali vende.
    /// </summary>
    public void SetVendor(VendorDef vendor)
    {
        Vendor = vendor ?? throw new ArgumentNullException(nameof(vendor));
        foreach (var def in _cart.Keys.Where(d => !Vendor.Sells(d)).ToList())
            _cart.Remove(def);
        Changed?.Invoke();
    }

    public void Add(IngredientDef def, int units = 1)
    {
        if (units <= 0) throw new ArgumentOutOfRangeException(nameof(units));
        if (!Vendor.Sells(def))
            throw new InvalidOperationException($"{Vendor.DisplayName} não vende {def.DisplayName}.");
        _cart[def] = UnitsOf(def) + units;
        Changed?.Invoke();
    }

    public void Remove(IngredientDef def, int units = 1)
    {
        if (units <= 0 || units > UnitsOf(def)) throw new ArgumentOutOfRangeException(nameof(units));
        _cart[def] -= units;
        if (_cart[def] == 0) _cart.Remove(def);
        Changed?.Invoke();
    }

    public void Clear()
    {
        _cart.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    /// Põe no carrinho o que falta na despensa para uma receita. Conta o que já está no carrinho:
    /// clicar duas vezes não compra em dobro.
    /// </summary>
    public void AddMissingFor(RecipeAnchor anchor, Pantry pantry)
    {
        foreach (var (def, missing) in MissingFor(anchor, pantry))
            _cart[def] = UnitsOf(def) + missing;
        Changed?.Invoke();
    }

    /// <summary>
    /// O que dá para cozinhar na despensa: tudo menos o estragado. Ovo podre não conta como ovo
    /// na hora de decidir o que comprar.
    /// </summary>
    public static int Usable(Pantry pantry, IngredientDef def) =>
        pantry.UnitsOf(def) - pantry.UnitsIn(def, Spoilage.Rotten);

    /// <summary>O que ainda falta para a receita, depois da despensa e do carrinho. Só o que este vendedor vende.</summary>
    public IReadOnlyList<(IngredientDef Def, int Missing)> MissingFor(RecipeAnchor anchor, Pantry pantry)
    {
        var defs = pantry.Defs.ToDictionary(d => d.Id);
        return anchor.Portions
                     .Where(p => defs.ContainsKey(p.Key))
                     .Select(p => (Def: defs[p.Key], Missing: p.Value - Usable(pantry, defs[p.Key]) - UnitsOf(defs[p.Key])))
                     .Where(x => x.Missing > 0 && Vendor.Sells(x.Def))
                     .ToList();
    }

    /// <summary>A receita tem algum ingrediente faltando que este vendedor não tem.</summary>
    public bool CannotCompleteHere(RecipeAnchor anchor, Pantry pantry)
    {
        var defs = pantry.Defs.ToDictionary(d => d.Id);
        return anchor.Portions.Any(p => defs.ContainsKey(p.Key)
                                        && p.Value > Usable(pantry, defs[p.Key])
                                        && !Vendor.Sells(defs[p.Key]));
    }
}
