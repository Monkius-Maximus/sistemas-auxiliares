namespace ContextEngine;

/// <summary>
/// What a primitive says happened. Primitives never touch game state — they receive a
/// view-model and emit one of these; the module decides what it means.
/// </summary>
public abstract record Intent;

public sealed record SubjectChanged(string Id) : Intent;

public sealed record ActionToggled(string Id) : Intent;

/// <summary>Stepper on a slot. <paramref name="Delta"/> may be ±1, ±5 (bulk) or ±<see cref="int.MaxValue"/> (all).</summary>
public sealed record QtyChanged(string Id, int Delta) : Intent;

/// <summary>Single-select: a blueprint, a hotspot, a dialogue line.</summary>
public sealed record SelectionChanged(string Id) : Intent;

/// <summary>Dual grid. <paramref name="Delta"/> is +1 to take, −1 to put back.</summary>
public sealed record Moved(string Id, int Delta) : Intent;

public sealed record Answered(int QuestionIndex, int OptionIndex) : Intent;

public sealed record PagerChanged(int Index) : Intent;

public sealed record StepTaken(int Index) : Intent;

public sealed record SequenceReset : Intent;

/// <summary>Real-time strike. <paramref name="Position"/> is the marker at 0–100 when the player acted.</summary>
public sealed record TimingStrike(double Position) : Intent;

/// <summary>The accessibility fallback: the same expected value, resolved as a roll.</summary>
public sealed record TimingRoll : Intent;

public sealed record TimingReset : Intent;

public sealed record TimerToggled : Intent;

public sealed record Committed : Intent;

public sealed record Closed : Intent;
