using System;
using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// Quem vende ingrediente. Cada vendedor é uma troca: preço, horário e frescor do que está na
/// prateleira. O mercado é barato mas fecha à noite; a conveniência abre sempre, cobra mais e
/// vende produto que já passou dias exposto — e o sistema de perecíveis cobra isso depois.
/// </summary>
[GlobalClass]
public partial class VendorDef : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public string Description { get; set; } = "";
    [Export] public Texture2D Icon { get; set; }
    [Export] public Color TintColor { get; set; } = Colors.White;

    /// <summary>Multiplicador sobre o preço por unidade do ingrediente.</summary>
    [Export] public float Markup { get; set; } = 1f;

    /// <summary>Horário, em horas do dia. 0 a 24 é aberto sempre.</summary>
    [Export] public int OpensAt { get; set; } = 8;
    [Export] public int ClosesAt { get; set; } = 20;

    /// <summary>Idade, em dias, do que sai da prateleira. Comprar aqui já começa o relógio do frescor.</summary>
    [Export] public float StockAgeDays { get; set; }

    /// <summary>Ids do que este vendedor tem. Vazio vende tudo.</summary>
    [Export] public string[] Catalog { get; set; } = Array.Empty<string>();

    public bool AlwaysOpen => OpensAt <= 0 && ClosesAt >= 24;

    public bool IsOpenAt(int hourOfDay) => AlwaysOpen || (hourOfDay >= OpensAt && hourOfDay < ClosesAt);

    public bool Sells(IngredientDef def) => Catalog.Length == 0 || Array.IndexOf(Catalog, def.Id) >= 0;

    /// <summary>Preço de uma unidade aqui. Arredonda para cima: loja não dá centavo de troco.</summary>
    public int PriceOf(IngredientDef def) => (int)MathF.Ceiling(def.PricePerUnit * Markup);
}
