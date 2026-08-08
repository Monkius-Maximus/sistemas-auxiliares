using System.Collections.Generic;
using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// Conteúdo de teste construído em código para o protótipo rodar sem arte nem .tres.
/// Quando o pipeline de assets existir, cada def vira um .tres editável no inspetor e
/// esta classe some — as classes de dados já são Resource justamente para isso.
///
/// Convenção de autoria do vetor de sabor: soma L1 entre 0.2 (batata, insosso) e
/// 1.0 (bacon, assertivo). Temperos: vetor unitário + Potency 3–4.
/// </summary>
public static class SampleContent
{
    private static Texture2D Swatch(Color color)
    {
        var image = Image.CreateEmpty(48, 48, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }

    private static IngredientDef Ingredient(
        string id, string name, FoodGroup group, Color tint,
        Nutrition nutrition, FlavorProfile flavor, int price)
        => new()
        {
            Id = id,
            DisplayName = name,
            Group = group,
            TintColor = tint,
            Icon = Swatch(tint),
            WeightPerUnit = 0.010f,
            Nutrition = nutrition,
            Flavor = flavor,
            Potency = 1.0f,
            PricePerUnit = price,
            MaxUnitsInDish = 30,
        };

    private static IngredientDef Seasoning(
        string id, string name, Color tint, FlavorProfile flavor, float potency, float calories = 0f)
        => new()
        {
            Id = id,
            DisplayName = name,
            Group = FoodGroup.None,
            IsSeasoning = true,
            TintColor = tint,
            Icon = Swatch(tint),
            WeightPerUnit = 0.001f,
            Nutrition = Nutrition.Of(calories: calories),
            Flavor = flavor,
            Potency = potency,
            PricePerUnit = 1,
            MaxUnitsInDish = 12,
        };

    public static List<IngredientDef> Ingredients() => new()
    {
        Ingredient("ovo", "Ovo", FoodGroup.Egg, new Color("f2c94c"),
            Nutrition.Of(calories: 14f, protein: 1.2f, lipids: 1.0f, carbs: 0.1f, hunger: 1.4f),
            FlavorProfile.Of(umami: 0.35f, sweet: 0.10f), 2),

        Ingredient("bacon", "Bacon", FoodGroup.Meat, new Color("c65f5f"),
            Nutrition.Of(calories: 54f, protein: 3.7f, lipids: 4.2f, hunger: 2.2f),
            FlavorProfile.Of(salty: 0.45f, umami: 0.55f), 6),

        Ingredient("queijo", "Queijo", FoodGroup.Dairy, new Color("e8d18a"),
            Nutrition.Of(calories: 40f, protein: 2.5f, lipids: 3.3f, hunger: 1.8f),
            FlavorProfile.Of(salty: 0.35f, umami: 0.40f, sour: 0.10f), 5),

        Ingredient("brocolis", "Brócolis", FoodGroup.Vegetable, new Color("4f8f4a"),
            Nutrition.Of(calories: 3.4f, protein: 0.28f, carbs: 0.7f, hunger: 0.6f, thirst: 0.3f),
            FlavorProfile.Of(bitter: 0.35f, sweet: 0.12f), 2),

        Ingredient("tomate", "Tomate", FoodGroup.Vegetable, new Color("d9583f"),
            Nutrition.Of(calories: 1.8f, protein: 0.09f, carbs: 0.4f, hunger: 0.4f, thirst: 0.8f),
            FlavorProfile.Of(sour: 0.35f, sweet: 0.20f, umami: 0.25f), 2),

        Ingredient("cogumelo", "Cogumelo", FoodGroup.Vegetable, new Color("9c8271"),
            Nutrition.Of(calories: 2.2f, protein: 0.31f, hunger: 0.5f, thirst: 0.2f),
            FlavorProfile.Of(umami: 0.60f, bitter: 0.12f), 3),

        Ingredient("batata", "Batata", FoodGroup.Starch, new Color("c9a86a"),
            Nutrition.Of(calories: 7.7f, protein: 0.2f, carbs: 1.7f, hunger: 1.6f),
            FlavorProfile.Of(sweet: 0.12f, umami: 0.08f), 1),

        Ingredient("cebolinha", "Cebolinha", FoodGroup.Herb, new Color("6fbf5e"),
            Nutrition.Of(calories: 3.2f, hunger: 0.1f),
            FlavorProfile.Of(bitter: 0.20f, spicy: 0.15f, sweet: 0.05f), 1),
    };

    public static List<IngredientDef> Seasonings() => new()
    {
        Seasoning("sal", "Sal", new Color("e6e6e6"), FlavorProfile.Of(salty: 1.0f), 4.0f),
        Seasoning("pimenta", "Pimenta", new Color("6b5344"), FlavorProfile.Of(spicy: 1.0f, bitter: 0.2f), 3.5f),
        Seasoning("acucar", "Açúcar", new Color("f5f0dc"), FlavorProfile.Of(sweet: 1.0f), 3.0f, calories: 4f),
        Seasoning("vinagre", "Vinagre", new Color("d6c07a"), FlavorProfile.Of(sour: 1.0f), 3.0f),
        Seasoning("ervas", "Ervas", new Color("55803f"), FlavorProfile.Of(bitter: 0.4f, spicy: 0.2f, sweet: 0.1f), 3.0f),
    };

    public static List<BaseItemDef> Bases() => new()
    {
        new BaseItemDef
        {
            Id = "frigideira",
            DisplayName = "Frigideira",
            FormName = "Fritada",
            Method = CookingMethod.Fry,
            BaseIngredientSlots = 2,
            SeasoningSlots = 4,
            TintColor = new Color("8a8a8a"),
            Icon = Swatch(new Color("8a8a8a")),
            AllowedGroups = new Godot.Collections.Array<int>
            {
                (int)FoodGroup.Egg, (int)FoodGroup.Meat, (int)FoodGroup.Dairy,
                (int)FoodGroup.Vegetable, (int)FoodGroup.Starch, (int)FoodGroup.Herb,
            },
        },
        new BaseItemDef
        {
            Id = "panela",
            DisplayName = "Panela",
            FormName = "Sopa",
            Method = CookingMethod.Boil,
            BaseIngredientSlots = 3,
            SeasoningSlots = 3,
            TintColor = new Color("6f7a8a"),
            Icon = Swatch(new Color("6f7a8a")),
            AllowedGroups = new Godot.Collections.Array<int>
            {
                (int)FoodGroup.Meat, (int)FoodGroup.Vegetable,
                (int)FoodGroup.Starch, (int)FoodGroup.Herb,
            },
        },
        new BaseItemDef
        {
            Id = "tigela",
            DisplayName = "Tigela",
            FormName = "Salada",
            Method = CookingMethod.Raw,
            BaseIngredientSlots = 3,
            SeasoningSlots = 4,
            TintColor = new Color("b0a48c"),
            Icon = Swatch(new Color("b0a48c")),
            AllowedGroups = new Godot.Collections.Array<int>
            {
                (int)FoodGroup.Vegetable, (int)FoodGroup.Fruit,
                (int)FoodGroup.Dairy, (int)FoodGroup.Herb,
            },
        },
    };

    /// <summary>
    /// Poucas âncoras de propósito. Elas fazem duas coisas: premiam quem descobre a
    /// combinação no painel manual, e SÃO as linhas do menu rápido — a lista estilo Sims
    /// é derivada daqui, não escrita à parte.
    /// </summary>
    public static List<RecipeAnchor> Anchors() => new()
    {
        new RecipeAnchor
        {
            DishName = "Omelete de Queijo",
            BaseItemId = "frigideira",
            Portions = new Godot.Collections.Dictionary<string, int>
                { { "ovo", 20 }, { "queijo", 8 }, { "cebolinha", 4 }, { "sal", 2 }, { "pimenta", 1 } },
            ForbiddenIngredientIds = new[] { "acucar", "vinagre" },
            QualityLift = 0.35f,
        },
        new RecipeAnchor
        {
            DishName = "Caldo Verde da Casa",
            BaseItemId = "panela",
            Portions = new Godot.Collections.Dictionary<string, int>
                { { "batata", 25 }, { "cogumelo", 10 }, { "cebolinha", 5 }, { "sal", 3 }, { "ervas", 2 } },
            ForbiddenIngredientIds = new[] { "acucar" },
            QualityLift = 0.35f,
        },
        new RecipeAnchor
        {
            DishName = "Salada Caprese",
            BaseItemId = "tigela",
            Portions = new Godot.Collections.Dictionary<string, int>
                { { "tomate", 20 }, { "queijo", 10 }, { "ervas", 2 }, { "sal", 1 } },
            ForbiddenIngredientIds = new[] { "acucar" },
            QualityLift = 0.35f,
        },
    };

    /// <summary>Despensa inicial. Temperos vêm em quantidade alta porque entram em doses pequenas.</summary>
    public static Dictionary<IngredientDef, int> Pantry()
    {
        var pantry = new Dictionary<IngredientDef, int>();
        foreach (var def in Ingredients())
            pantry[def] = 40;
        foreach (var def in Seasonings())
            pantry[def] = 25;
        return pantry;
    }
}
