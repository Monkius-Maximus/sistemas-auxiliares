using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// O recipiente/método. É ele que define quantos slots existem, quais grupos são
/// aceitos e qual substantivo o gerador de nome usa ("Sopa de...", "Salada de...").
/// </summary>
[GlobalClass]
public partial class BaseItemDef : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Texture2D Icon { get; set; }
    [Export] public CookingMethod Method { get; set; } = CookingMethod.Raw;

    /// <summary>Substantivo do prato resultante. Usado por <see cref="DishNamer"/>.</summary>
    [Export] public string FormName { get; set; } = "Prato";

    /// <summary>Slots de ingrediente no nível 0. A perícia adiciona mais.</summary>
    [Export] public int BaseIngredientSlots { get; set; } = 2;

    [Export] public int SeasoningSlots { get; set; } = 4;

    /// <summary>Grupos que esse recipiente aceita. Tentar adicionar outro grupo lança exceção.</summary>
    [Export] public Godot.Collections.Array<int> AllowedGroups { get; set; } = new();

    [Export] public Color TintColor { get; set; } = Colors.White;

    public bool Accepts(FoodGroup group) => AllowedGroups.Contains((int)group);

    /// <summary>
    /// Um slot extra a cada 4 níveis de culinária. É a progressão principal:
    /// mais slots = mais grupos = teto de variedade mais alto.
    /// </summary>
    public int IngredientSlotsForLevel(int cookingLevel)
    {
        if (cookingLevel < 0)
            throw new System.ArgumentOutOfRangeException(nameof(cookingLevel));
        return BaseIngredientSlots + cookingLevel / 4;
    }
}
