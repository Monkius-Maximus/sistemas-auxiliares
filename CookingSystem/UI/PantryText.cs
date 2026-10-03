namespace LifeSim.Cooking;

/// <summary>
/// Como o estado da despensa vira texto curto. O ladrilho do painel manual e a lista de
/// porções do menu rápido passam por aqui, para que "3 passados" signifique a mesma coisa
/// nas duas telas.
/// </summary>
internal static class PantryText
{
    /// <summary>
    /// O pior estado presente na despensa, com quantidade. Estragado vence passado: é o que
    /// exige ação do jogador. Nada a dizer devolve nulo — fresco não é notícia.
    /// </summary>
    public static string Spoilage(Pantry pantry, IngredientDef def)
    {
        int rotten = pantry.UnitsIn(def, Cooking.Spoilage.Rotten);
        if (rotten > 0) return $"{rotten} {(rotten == 1 ? "estragado" : "estragados")}";

        int stale = pantry.UnitsIn(def, Cooking.Spoilage.Stale);
        if (stale > 0) return $"{stale} {(stale == 1 ? "passado" : "passados")}";

        return null;
    }

    /// <summary>
    /// "estoque 40", ou só o alerta — "10 estragados" — enquanto houver algo a resolver. O
    /// ladrilho tem uma linha estreita, e cortar o alerta pela metade é pior que esconder o
    /// estoque por um momento: o alerta é o que pede ação, e o + já recusa quando o estoque acaba.
    /// </summary>
    public static string Stock(Pantry pantry, IngredientDef def, int available) =>
        Spoilage(pantry, def) ?? $"estoque {available}";
}
