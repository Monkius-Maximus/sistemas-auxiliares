using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ContextEngine.Demo;

// The remaining six evaluators. Each is a pure function from session state to a judgement, so
// each can be driven from a CSV of inputs and expected outputs in CI — which is what lets you
// tune the maths without fear.

// ── shop ─────────────────────────────────────────────────────────────────────

public sealed record WalletState(string VendorId, IReadOnlyDictionary<string, int> Cart, bool Haggle, int Wallet);

public sealed record WalletJudgement(int Total, int Saved, int Items, bool Over, double Load, int Short);

/// <summary>
/// Additive, deliberately: a shopping basket is a set of genuinely independent contributions,
/// so the multiplicative rule from cooking would be wrong here.
/// </summary>
public sealed class WalletEvaluator : IEvaluator<WalletState, WalletJudgement>
{
    public static readonly WalletEvaluator Instance = new();

    /// <summary>Kaster does not negotiate — a per-vendor rule, not a per-skill one.</summary>
    public static bool HaggleAllowed(string vendorId) => vendorId != "hardware";

    public static int Price(StockLine line, Vendor vendor, bool haggle)
        => Mathf.Max(1, Mathf.RoundToInt((float)(line.Price * vendor.Markup * (1 - (haggle ? 0.08 : 0)))));

    public WalletJudgement Evaluate(WalletState state)
    {
        var vendor = GameData.Vendors.First(v => v.Id == state.VendorId);
        var haggle = state.Haggle && HaggleAllowed(state.VendorId);
        var lines = GameData.Stock[state.VendorId].Where(i => state.Cart.ContainsKey(i.Id)).ToList();

        var total = lines.Sum(i => Price(i, vendor, haggle) * state.Cart[i.Id]);
        var saved = haggle
            ? lines.Sum(i => (Mathf.RoundToInt((float)(i.Price * vendor.Markup)) - Price(i, vendor, true)) * state.Cart[i.Id])
            : 0;
        var items = lines.Sum(i => state.Cart[i.Id]);

        return new WalletJudgement(total, saved, items, total > state.Wallet,
            Mathf.Clamp((double)total / state.Wallet, 0, 1), Mathf.Max(0, total - state.Wallet));
    }
}

// ── crafting ─────────────────────────────────────────────────────────────────

public sealed record CraftState(string BenchId, string BlueprintId, bool Measure, bool Spares);

public sealed record CraftJudgement(double Chance, int Missing, int Blocked);

/// <summary>base − penalties + action bonuses, clamped. Each unmet requirement costs 25 points.</summary>
public sealed class CraftOddsEvaluator : IEvaluator<CraftState, CraftJudgement>
{
    public static readonly CraftOddsEvaluator Instance = new();

    public CraftJudgement Evaluate(CraftState state)
    {
        var list = GameData.Blueprints[state.BenchId];
        var blueprint = list.FirstOrDefault(b => b.Id == state.BlueprintId) ?? list[0];

        var missing = blueprint.Need.Count(pair =>
            GameData.Parts.First(p => p.Id == pair.Key).Have < pair.Value);

        // Improvise a part covers exactly one missing requirement, and charges for the privilege.
        var blocked = state.Spares ? Mathf.Max(0, missing - 1) : missing;
        var chance = blueprint.Base
                     - blocked * 0.25
                     - (state.Spares && missing > 0 ? 0.15 : 0)
                     + (state.Measure ? 0.08 : 0);

        return new CraftJudgement(Mathf.Clamp(chance, 0, 1), missing, blocked);
    }
}

// ── transfer ─────────────────────────────────────────────────────────────────

public sealed record EncumbranceState(double BaseWeight, double TakenWeight, double Capacity);

public sealed record EncumbranceJudgement(double Carried, double Load, bool Overloaded);

/// <summary>Load against capacity, with a threshold effect at 90% rather than a linear tax.</summary>
public sealed class EncumbranceEvaluator : IEvaluator<EncumbranceState, EncumbranceJudgement>
{
    public static readonly EncumbranceEvaluator Instance = new();

    public EncumbranceJudgement Evaluate(EncumbranceState state)
    {
        var carried = state.BaseWeight + state.TakenWeight;
        var load = Mathf.Clamp(carried / state.Capacity, 0, 1);
        return new EncumbranceJudgement(carried, load, load > 0.9);
    }
}

// ── exam ─────────────────────────────────────────────────────────────────────

public sealed record ExamState(
    string PaperId,
    IReadOnlyDictionary<int, int> Answers,
    IReadOnlyList<int> Working,
    int TimeLeft,
    bool ShowWorking,
    bool Eliminate,
    bool DoubleCheck);

public sealed record ExamJudgement(
    double Projected,
    int Answered,
    int Correct,
    double Accuracy,
    bool Passing,
    bool Rushed,
    bool MethodInOrder,
    IReadOnlyList<double> Trajectory);

/// <summary>
/// Additive again — an exam score is a sum of independent marks. The method bonus and the
/// rush penalty are the only places the character, rather than the answers, shows up.
/// </summary>
public sealed class ExamEvaluator : IEvaluator<ExamState, ExamJudgement>
{
    public static readonly ExamEvaluator Instance = new();

    public ExamJudgement Evaluate(ExamState state)
    {
        var paper = GameData.Papers.First(p => p.Id == state.PaperId);
        var questions = GameData.Questions[state.PaperId];
        var steps = GameData.Working[state.PaperId];

        var answered = state.Answers.Count;
        var correct = questions.Where((q, i) => state.Answers.ContainsKey(i) && q.Options[state.Answers[i]].Ok).Count();
        var accuracy = answered > 0 ? (double)correct / answered : 0;

        var inOrder = true;
        for (var i = 0; i < state.Working.Count; i++)
            if (state.Working[i] != i) inOrder = false;

        var workBonus = state.ShowWorking
            ? (inOrder ? 0.10 * ((double)state.Working.Count / steps.Length) : -0.07)
            : 0;

        var timeFraction = Mathf.Clamp((double)state.TimeLeft / paper.Secs, 0, 1);
        var rushed = timeFraction < 0.22 && answered < questions.Length;
        var guess = state.Eliminate ? 0.34 : 0.25;

        var trajectory = new List<double>();
        var running = 0.0;
        for (var i = 0; i < questions.Length; i++)
        {
            running += state.Answers.TryGetValue(i, out var pick)
                ? (questions[i].Options[pick].Ok ? 1 : 0)
                : guess;
            trajectory.Add(running / (i + 1));
        }

        var projected = Mathf.Clamp(
            (correct + (questions.Length - answered) * (answered > 0 ? accuracy * 0.85 : guess)) / questions.Length
            + workBonus
            + (state.DoubleCheck ? 0.04 : 0)
            - (rushed ? 0.08 : 0), 0, 1);

        return new ExamJudgement(projected, answered, correct, accuracy,
            projected >= paper.Pass, rushed, inOrder, trajectory);
    }
}

// ── lockpicking ──────────────────────────────────────────────────────────────

public sealed record LockState(string LockId, int PinsSet, int Strain, bool Tension, bool Rake);

public sealed record LockJudgement(double Window, double Odds, double Progress, double Speed, bool Broken, bool Open);

/// <summary>
/// The one evaluator whose inputs a reflex can change. It is still pure — the sweep phase
/// never enters here; only the resolved outcome does.
/// </summary>
public sealed class LockEvaluator : IEvaluator<LockState, LockJudgement>
{
    public static readonly LockEvaluator Instance = new();

    public static double WindowFor(Lock l, bool tension, bool rake)
        => Mathf.Clamp(l.Win + (tension ? 8 : 0) - (rake ? 5 : 0), 6, 60);

    public static double OddsFor(Lock l, bool tension, bool rake)
        => Mathf.Clamp(WindowFor(l, tension, rake) / 100 * 1.9 + (tension ? 0.08 : 0), 0.05, 0.95);

    public LockJudgement Evaluate(LockState state)
    {
        var l = GameData.Locks.First(x => x.Id == state.LockId);
        var window = WindowFor(l, state.Tension, state.Rake);
        var speed = l.Speed * (state.Tension ? 0.75 : 1) * (state.Rake ? 1.5 : 1);

        return new LockJudgement(
            window,
            OddsFor(l, state.Tension, state.Rake),
            (double)state.PinsSet / l.Pins,
            speed,
            state.Strain >= 3,
            state.PinsSet >= l.Pins);
    }
}

// ── vehicle repair ───────────────────────────────────────────────────────────

public sealed record RigState(string RigId, IReadOnlyDictionary<string, IReadOnlyList<int>> Progress, bool Torque);

public sealed record RigJudgement(
    IReadOnlyList<double> Conditions,
    double Worst,
    double Mean,
    double Reliability,
    VehicleSystem WeakLink,
    int Repaired);

/// <summary>
/// Multiplicative again, and for the same reason as the dish: worst × mean is the seized-bolt
/// rule. One dead system caps the whole job however good everything else is.
/// </summary>
public sealed class RigEvaluator : IEvaluator<RigState, RigJudgement>
{
    public static readonly RigEvaluator Instance = new();

    /// <summary>A procedure done in order lands at 95%; out of order, 62%. Torque adds three points.</summary>
    public static double ConditionOf(VehicleSystem system, IReadOnlyList<int> steps, bool torque)
    {
        if (steps == null || steps.Count < system.Steps.Length) return system.Cond;

        var inOrder = true;
        for (var i = 0; i < steps.Count; i++)
            if (steps[i] != i) inOrder = false;

        return Mathf.Clamp((inOrder ? 0.95 : 0.62) + (torque ? 0.03 : 0), 0, 1);
    }

    public RigJudgement Evaluate(RigState state)
    {
        var systems = GameData.Systems[state.RigId];
        var conditions = systems.Select(s => ConditionOf(s, Steps(state, s.Id), state.Torque)).ToList();

        var worst = conditions.Min();
        var mean = conditions.Average();
        var repaired = systems.Count(s => Steps(state, s.Id)?.Count == s.Steps.Length);

        return new RigJudgement(conditions, worst, mean, Mathf.Clamp(worst * mean, 0, 1),
            systems[conditions.IndexOf(worst)], repaired);
    }

    private static IReadOnlyList<int> Steps(RigState state, string systemId)
        => state.Progress.TryGetValue(systemId, out var steps) ? steps : null;
}
