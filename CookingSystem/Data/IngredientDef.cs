using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// Definição estática de um ingrediente ou tempero. Editável no inspetor como .tres.
/// Não guarda estado de runtime — frescor e quantidade vivem em <see cref="IngredientStack"/>.
/// </summary>
[GlobalClass]
public partial class IngredientDef : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Texture2D Icon { get; set; }

    /// <summary>Grupo alimentar. Temperos devem usar <see cref="FoodGroup.None"/>.</summary>
    [Export] public FoodGroup Group { get; set; } = FoodGroup.None;

    /// <summary>Tempero vai para o painel de temperos e não conta como grupo de variedade.</summary>
    [Export] public bool IsSeasoning { get; set; }

    /// <summary>Peso em kg por unidade. Define a fração de massa usada no cálculo de frescor.</summary>
    [Export] public float WeightPerUnit { get; set; } = 0.01f;

    [Export] public Nutrition Nutrition { get; set; }
    [Export] public FlavorProfile Flavor { get; set; }

    /// <summary>
    /// Multiplicador de sabor. 1.0 para ingredientes; 3–4 para temperos, que entram em
    /// poucas unidades mas precisam mover o vetor de sabor do prato inteiro.
    /// </summary>
    [Export] public float Potency { get; set; } = 1.0f;

    /// <summary>Preço por unidade. O custo do prato é derivado daqui, nunca autorado por prato.</summary>
    [Export] public int PricePerUnit { get; set; } = 1;

    /// <summary>Teto de unidades desse ingrediente em um único prato.</summary>
    [Export] public int MaxUnitsInDish { get; set; } = 30;

    /// <summary>Cor usada pelos ícones de placeholder e pela borda do slot.</summary>
    [Export] public Color TintColor { get; set; } = Colors.White;
}
