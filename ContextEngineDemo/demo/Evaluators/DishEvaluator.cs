using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ContextEngine.Demo;

public sealed record DishState(
    IReadOnlyDictionary<string, int> Qty,
    string VesselId,
    bool Taste,
    bool LowFlame,
    bool Plate);

public sealed record DishStats(double Hunger, double Kcal, double Prot, double Fat, double Carb, double Mood, double Weight);

public sealed record DishJudgement(
    bool Empty,
    double Quality,
    double Seasoning,
    double Balance,
    double Harmony,
    double Variety,
    double Freshness,
    DishStats Stats,
    string Name,
    bool Anchor,
    int Groups,
    int Mass,
    double Ratio);

/// <summary>
/// Cooking quality is the <em>product</em> of its five factors, not their average. This is the
/// design choice with the strongest feel in the whole system: one catastrophic factor ruins
/// the dish no matter how good the rest is, exactly like real cooking. An average would
/// politely report 68% and teach the player nothing.
///
/// Use the same shape wherever the fiction supports "a chain is as strong as its weakest link".
/// Use additive scoring where contributions are genuinely independent — a basket, an exam score.
/// </summary>
public sealed class DishEvaluator : IEvaluator<DishState, DishJudgement>
{
    public static readonly DishEvaluator Instance = new();

    private static double Clamp(double v, double a, double b) => Mathf.Clamp(v, a, b);

    public DishJudgement Evaluate(DishState state)
    {
        var ingredients = GameData.Ingredients.Where(i => Qty(state, i.Id) > 0).ToList();
        var seasonings = GameData.Seasonings.Where(s => Qty(state, s.Id) > 0).ToList();

        var mass = ingredients.Sum(i => Qty(state, i.Id));
        if (mass == 0)
            return new DishJudgement(true, 0, 0, 0, 0, 0, 0, new DishStats(0, 0, 0, 0, 0, 0, 0), "Empty pan", false, 0, 0, 0);

        var power = seasonings.Sum(s => Qty(state, s.Id) * s.Power);
        var ratio = power / mass;

        // Taste as you go widens the window it is possible to hit — an information verb that
        // also changes the maths, which is why it is worth a skill gate.
        var window = state.Taste ? 0.14 : 0.09;
        var seasoning = Clamp(1 - System.Math.Abs(ratio - 0.10) / window, 0.05, 1);

        var share = ingredients.Max(i => (double)Qty(state, i.Id) / mass);
        var balance = Clamp(1 - System.Math.Max(0, share - 0.62) / 0.38, 0.08, 1);

        var groups = ingredients.Select(i => i.Group).Distinct().Count();
        var variety = Clamp(groups / 3.0, 0.34, 1);

        var profiles = seasonings.Select(s => s.Profile).ToHashSet();
        var harmony = 1.0;
        if (profiles.Contains("sweet") && profiles.Contains("salty")) harmony *= 0.7;
        if (profiles.Contains("sour") && ingredients.Any(i => i.Group == "dairy")) harmony *= 0.8;

        var freshness = state.VesselId == "bowl" ? 1 : Clamp(1 - System.Math.Max(0, mass - 30) / 60.0, 0.6, 1);
        if (state.LowFlame) freshness = Clamp(freshness + 0.12, 0, 1);

        var quality = seasoning * balance * harmony * variety * freshness;

        // A recipe anchor is a named combination the game recognises: systemic floor, authored peaks.
        var anchor = groups >= 3 && seasoning > 0.85 && quality > 0.55;
        if (anchor) quality = System.Math.Min(1, quality * 1.25);

        double hunger = 0, kcal = 0, prot = 0, fat = 0, carb = 0, mood = 0, weight = 0;
        foreach (var i in ingredients)
        {
            var n = Qty(state, i.Id);
            hunger += i.Hunger * n;
            kcal += i.Kcal * n;
            prot += i.Prot * n;
            fat += i.Fat * n;
            carb += i.Carb * n;
            mood += i.Mood * n;
            weight += n * 0.02;
        }

        var oversalt = Clamp((ratio - 0.24) * 40, 0, 30);
        mood = mood * (quality < 0.35 ? -1 : 1) - oversalt + (state.Plate ? 6 : 0);

        var lead = ingredients.OrderByDescending(i => Qty(state, i.Id)).First();
        var verb = state.VesselId switch { "skillet" => "Fried", "pot" => "Boiled", _ => "Raw" };
        var name = anchor
            ? (lead.Id == "egg" ? "Farmhouse Omelette" : lead.Name + " Skillet Plate")
            : verb + " " + lead.Name + (ingredients.Count > 1 ? " mix" : "");

        return new DishJudgement(
            false,
            Clamp(quality, 0.01, 1),
            seasoning, balance, harmony, variety, freshness,
            new DishStats(hunger, kcal, prot, fat, carb, mood, weight),
            name, anchor, groups, mass, ratio);
    }

    private static int Qty(DishState state, string id) => state.Qty.TryGetValue(id, out var n) ? n : 0;
}
