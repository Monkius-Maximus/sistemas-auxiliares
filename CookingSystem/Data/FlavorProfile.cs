using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// Vetor de sabor por unidade de ingrediente.
/// Convenção de autoria: a soma L1 de um ingrediente comum fica entre 0.2 (insosso,
/// tipo batata) e 1.0 (assertivo, tipo bacon). Temperos usam vetor unitário num eixo
/// e expressam a força via <see cref="IngredientDef.Potency"/>.
/// </summary>
[GlobalClass]
public partial class FlavorProfile : Resource
{
    public const int AxisCount = 6;

    [Export] public float Sweet { get; set; }
    [Export] public float Salty { get; set; }
    [Export] public float Sour { get; set; }
    [Export] public float Bitter { get; set; }
    [Export] public float Umami { get; set; }
    [Export] public float Spicy { get; set; }

    public float Get(FlavorAxis axis) => axis switch
    {
        FlavorAxis.Sweet => Sweet,
        FlavorAxis.Salty => Salty,
        FlavorAxis.Sour => Sour,
        FlavorAxis.Bitter => Bitter,
        FlavorAxis.Umami => Umami,
        FlavorAxis.Spicy => Spicy,
        _ => throw new System.ArgumentOutOfRangeException(nameof(axis)),
    };

    public static string Label(FlavorAxis axis) => axis switch
    {
        FlavorAxis.Sweet => "Doce",
        FlavorAxis.Salty => "Salgado",
        FlavorAxis.Sour => "Ácido",
        FlavorAxis.Bitter => "Amargo",
        FlavorAxis.Umami => "Umami",
        FlavorAxis.Spicy => "Picante",
        _ => throw new System.ArgumentOutOfRangeException(nameof(axis)),
    };

    public static FlavorProfile Of(float sweet = 0, float salty = 0, float sour = 0,
                                   float bitter = 0, float umami = 0, float spicy = 0)
        => new()
        {
            Sweet = sweet, Salty = salty, Sour = sour,
            Bitter = bitter, Umami = umami, Spicy = spicy,
        };
}
