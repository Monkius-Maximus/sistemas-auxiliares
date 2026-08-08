namespace LifeSim.Cooking;

/// <summary>Eixos de sabor. A ordem define o índice usado nos vetores do avaliador.</summary>
public enum FlavorAxis
{
    Sweet = 0,
    Salty = 1,
    Sour = 2,
    Bitter = 3,
    Umami = 4,
    Spicy = 5,
}

/// <summary>
/// Grupo alimentar do ingrediente. Alimenta o fator de variedade da qualidade
/// e o filtro de compatibilidade do recipiente. Temperos usam <see cref="None"/>.
/// </summary>
public enum FoodGroup
{
    None = 0,
    Egg,
    Meat,
    Fish,
    Dairy,
    Vegetable,
    Fruit,
    Starch,
    Herb,
}

public enum CookingMethod
{
    Raw,
    Fry,
    Boil,
    Bake,
}
