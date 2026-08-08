using System.Collections.Generic;
using System.Linq;

namespace LifeSim.Cooking;

/// <summary>
/// Gera o nome do prato a partir do que foi colocado dentro. Isso é o que permite
/// abandonar a lista fixa de receitas: qualquer combinação já nasce com um nome legível.
///
/// Padrão: "{Forma} de {dominante} com {segundo} e {terceiro} ({estado})".
/// Só entram no nome os ingredientes com fração de massa relevante — 1 unidade de
/// cebolinha num assado não vira título.
/// </summary>
public static class DishNamer
{
    private const float MinMassShareToName = 0.12f;
    private const int MaxNamedIngredients = 3;

    public static string Generate(CookingSession session, float minFreshness)
    {
        var ranked = session.Ingredients
                            .OrderByDescending(s => s.Weight)
                            .ToList();

        float totalWeight = ranked.Sum(s => s.Weight);
        var named = ranked.Where(s => s.Weight / totalWeight >= MinMassShareToName)
                          .Take(MaxNamedIngredients)
                          .Select(s => s.Def.DisplayName.ToLowerInvariant())
                          .ToList();

        // O dominante sempre entra, mesmo que sozinho passe do filtro por pouco.
        if (named.Count == 0)
            named.Add(ranked[0].Def.DisplayName.ToLowerInvariant());

        string body = $"{session.Base.FormName} de {named[0]}";
        if (named.Count == 2)
            body += $" com {named[1]}";
        else if (named.Count == 3)
            body += $" com {named[1]} e {named[2]}";

        string state = StateSuffix(minFreshness);
        return state is null ? body : $"{body} ({state})";
    }

    private static string StateSuffix(float freshness) => freshness switch
    {
        < 0.30f => "Estragado",
        < 0.65f => "Passado",
        _ => null,
    };
}
