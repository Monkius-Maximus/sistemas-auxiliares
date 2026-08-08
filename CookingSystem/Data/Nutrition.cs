using Godot;

namespace LifeSim.Cooking;

/// <summary>Contribuição nutricional por unidade de ingrediente. Sempre somada linearmente.</summary>
[GlobalClass]
public partial class Nutrition : Resource
{
    [Export] public float Calories { get; set; }
    [Export] public float Protein { get; set; }
    [Export] public float Lipids { get; set; }
    [Export] public float Carbs { get; set; }
    /// <summary>Quanto de fome esse ingrediente mata, por unidade (0..100 na escala de motivo).</summary>
    [Export] public float Hunger { get; set; }
    /// <summary>Quanto de sede mata, por unidade. Caldos e frutas suculentas &gt; 0.</summary>
    [Export] public float Thirst { get; set; }

    public static Nutrition Of(float calories = 0, float protein = 0, float lipids = 0,
                               float carbs = 0, float hunger = 0, float thirst = 0)
        => new()
        {
            Calories = calories, Protein = protein, Lipids = lipids,
            Carbs = carbs, Hunger = hunger, Thirst = thirst,
        };
}
