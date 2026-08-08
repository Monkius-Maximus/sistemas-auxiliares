namespace LifeSim.Cooking;

/// <summary>
/// Uma porção de um ingrediente dentro do prato. Frescor vive aqui, não no def:
/// dois ovos da mesma definição podem estar em estados diferentes.
/// </summary>
public sealed class IngredientStack
{
    public IngredientStack(IngredientDef def, int units, float freshness)
    {
        if (def is null)
            throw new System.ArgumentNullException(nameof(def));
        if (units <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(units), "Stack precisa de pelo menos 1 unidade.");
        if (freshness < 0f || freshness > 1f)
            throw new System.ArgumentOutOfRangeException(nameof(freshness), "Frescor é 0..1.");

        Def = def;
        Units = units;
        Freshness = freshness;
    }

    public IngredientDef Def { get; }
    public int Units { get; private set; }
    public float Freshness { get; }

    public float Weight => Units * Def.WeightPerUnit;

    public void Add(int units)
    {
        if (units <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(units));
        if (Units + units > Def.MaxUnitsInDish)
            throw new System.InvalidOperationException(
                $"{Def.DisplayName} aceita no máximo {Def.MaxUnitsInDish} unidades no prato.");
        Units += units;
    }

    public void Remove(int units)
    {
        if (units <= 0)
            throw new System.ArgumentOutOfRangeException(nameof(units));
        if (units > Units)
            throw new System.InvalidOperationException("Não há tanta quantidade nesse stack.");
        Units -= units;
    }
}
