using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>The seven named regions. A context fills what it needs and leaves the rest null.</summary>
public enum RegionId
{
    Subject,
    Actions,
    Primary,
    Secondary,
    Preview,
    Readout,
    Commit,
}

/// <summary>
/// Marker for anything a region can hold. <see cref="Primitive"/> is the catalogue key the
/// shell uses to pick a widget — it matches the string in the context definition.
/// </summary>
public interface IRegionVm
{
    string Primitive { get; }
    /// <summary>Region header text.</summary>
    string Title { get; }
    /// <summary>Right-aligned budget line in the header. "4 / 6 slots" — always state the budget.</summary>
    string Count { get; }
}

/// <summary>Everything the shell needs for one frame. Built by the controller, read-only downstream.</summary>
public sealed class ContextViewModel
{
    public string Title { get; init; } = "";
    public string Crumb { get; init; } = "";
    /// <summary>Declared panel width in px. The shell honours it and lets height follow content.</summary>
    public int Width { get; init; } = 1180;
    public Dictionary<RegionId, IRegionVm> Regions { get; init; } = new();

    public IRegionVm Region(RegionId id) => Regions.TryGetValue(id, out var vm) ? vm : null;
}

// ── subject · picker ──────────────────────────────────────────────────────────

public sealed class PickerVm : IRegionVm
{
    public string Primitive => "picker";
    public string Title { get; init; } = "";
    public string Count { get; init; } = "";
    public string Note { get; init; } = "";
    public List<PickerOption> Options { get; init; } = new();
}

public sealed class PickerOption
{
    public string Id { get; init; }
    public string Name { get; init; }
    public Color Tint { get; init; }
    public bool Selected { get; init; }
}

// ── actions · verbs ───────────────────────────────────────────────────────────

public sealed class VerbsVm : IRegionVm
{
    public string Primitive => "verbs";
    public string Title { get; init; } = "Skills & actions";
    public string Count { get; init; } = "";
    public List<VerbItem> Items { get; init; } = new();
}

public sealed class VerbItem
{
    public string Id { get; init; }
    public string Name { get; init; }
    /// <summary>The gate, shown even when unmet: "BART 4". Locked verbs are content.</summary>
    public string Skill { get; init; } = "";
    public string Note { get; init; } = "";
    public bool On { get; init; }
    public bool Locked { get; init; }
}

// ── primary/secondary · grid.qty and grid.select ──────────────────────────────

public sealed class TileGridVm : IRegionVm
{
    /// <summary>"grid.qty" when the tiles carry steppers, "grid.select" when they are single-select.</summary>
    public string Primitive => Steppers ? "grid.qty" : "grid.select";
    public string Title { get; init; } = "";
    public string Count { get; init; } = "";
    public bool Steppers { get; init; } = true;
    public List<TileVm> Tiles { get; init; } = new();
}

public sealed class TileVm
{
    public string Id { get; init; }
    public string Name { get; init; }
    public Color Tint { get; init; }
    /// <summary>Subtitle: stock line, price, profile, base odds.</summary>
    public string Sub { get; init; } = "";
    /// <summary>Turns the subtitle red — bad condition, spoiled goods.</summary>
    public bool SubAlert { get; init; }
    /// <summary>Quantity glyph. "–" for none, "▸" for the selected blueprint.</summary>
    public string Qty { get; init; } = "–";
    /// <summary>Tile is in the recipe / basket / selected.</summary>
    public bool Active { get; init; }
    /// <summary>Out of budget — dimmed, + refuses.</summary>
    public bool Dimmed { get; init; }
    public bool CanAdd { get; init; } = true;
    public bool CanSub { get; init; } = true;
}

// ── primary · grid.dual ───────────────────────────────────────────────────────

public sealed class DualGridVm : IRegionVm
{
    public string Primitive => "grid.dual";
    public string Title { get; init; } = "";
    public string Count { get; init; } = "";
    public DualPane Left { get; init; }
    public DualPane Right { get; init; }
}

public sealed class DualPane
{
    public string Name { get; init; }
    /// <summary>"18.4 / 30 kg" — the budget, always stated.</summary>
    public string Load { get; init; } = "";
    public string Arrow { get; init; } = "›";
    /// <summary>Shown when the pane has no rows.</summary>
    public string Empty { get; init; } = "";
    /// <summary>+1 to take from this pane, −1 to put back.</summary>
    public int Direction { get; init; } = 1;
    public List<DualItem> Items { get; init; } = new();
}

public sealed class DualItem
{
    public string Id { get; init; }
    public string Name { get; init; }
    public Color Tint { get; init; }
    public string Sub { get; init; } = "";
    public string Qty { get; init; } = "";
}

// ── secondary · checklist ─────────────────────────────────────────────────────

public sealed class ChecklistVm : IRegionVm
{
    public string Primitive => "checklist";
    public string Title { get; init; } = "";
    public string Count { get; init; } = "";
    public List<ChecklistRow> Rows { get; init; } = new();
}

public sealed class ChecklistRow
{
    public string Name { get; init; }
    public Color? Tint { get; init; }
    public string Note { get; init; } = "";
    /// <summary>"6 / 12", "$18", "set" — the have-vs-need figure.</summary>
    public string Tally { get; init; } = "";
    public Tone TallyTone { get; init; } = Tone.Ink;
    /// <summary>Left square: alert when the row is what is stopping you.</summary>
    public bool MarkAlert { get; init; }
    /// <summary>Left square drawn flat/off — nothing to say about this row yet.</summary>
    public bool MarkOff { get; init; }
}

// ── secondary · text ──────────────────────────────────────────────────────────

public sealed class TextVm : IRegionVm
{
    public string Primitive => "text";
    public string Title { get; init; } = "";
    public string Count { get; init; } = "";
    public string Body { get; init; } = "";
}

// ── primary · question ────────────────────────────────────────────────────────

public sealed class QuestionVm : IRegionVm
{
    public string Primitive => "question";
    public string Title { get; init; } = "";
    public string Count { get; init; } = "";
    public string Topic { get; init; } = "";
    public string Prompt { get; init; } = "";
    public List<PagerCell> Pager { get; init; } = new();
    public List<AnswerOption> Options { get; init; } = new();
    /// <summary>Paper collected — answers are frozen.</summary>
    public bool Frozen { get; init; }
    public int Index { get; init; }
}

public sealed class PagerCell
{
    public int Index { get; init; }
    public string Label { get; init; }
    public bool Current { get; init; }
    public bool Answered { get; init; }
}

public sealed class AnswerOption
{
    public int Index { get; init; }
    public string Letter { get; init; }
    public string Text { get; init; }
    public bool Selected { get; init; }
    /// <summary>Crossed out by the Eliminate verb.</summary>
    public bool StruckOut { get; init; }
    public string Tag { get; init; } = "";
}

// ── secondary · sequence ──────────────────────────────────────────────────────

public sealed class SequenceVm : IRegionVm
{
    public string Primitive => "sequence";
    public string Title { get; init; } = "";
    public string Count { get; init; } = "";
    public List<SequenceStep> Steps { get; init; } = new();
    public string Status { get; init; } = "";
    public bool StatusAlert { get; init; }
}

public sealed class SequenceStep
{
    public int Index { get; init; }
    public string Name { get; init; }
    /// <summary>Position taken, 1-based, or "·" when untouched.</summary>
    public string Ord { get; init; } = "·";
    public bool Taken { get; init; }
    public bool Wrong { get; init; }
    public string Note { get; init; } = "";
}

// ── primary · hotspot.map ─────────────────────────────────────────────────────

public sealed class HotspotMapVm : IRegionVm
{
    public string Primitive => "hotspot.map";
    public string Title { get; init; } = "";
    public string Count { get; init; } = "";
    /// <summary>The line under the diagram — what the map is telling you.</summary>
    public string MapLabel { get; init; } = "";
    public List<Hotspot> Spots { get; init; } = new();
}

public sealed class Hotspot
{
    public string Id { get; init; }
    public string Name { get; init; }
    /// <summary>Placement in 0..1 fractions of the diagram, so the map scales as one unit.</summary>
    public Rect2 Rect { get; init; }
    /// <summary>"62%" or "??" when undiagnosed.</summary>
    public string Cond { get; init; } = "??";
    /// <summary>Null when the value is hidden; otherwise drives the quality ramp.</summary>
    public double? Quality { get; init; }
    public string Badge { get; init; } = "";
    public bool BadgeAlert { get; init; }
    public bool BadgeDone { get; init; }
    public bool Selected { get; init; }
    public bool Faulty { get; init; }
}

// ── primary · timing.bar ──────────────────────────────────────────────────────

public sealed class TimingBarVm : IRegionVm
{
    public string Primitive => "timing.bar";
    public string Title { get; init; } = "";
    public string Count { get; init; } = "";
    /// <summary>False when the accessibility fallback has replaced the sweep with a roll.</summary>
    public bool Live { get; init; } = true;
    /// <summary>Target window centre, 0–100.</summary>
    public double ZoneCentre { get; init; } = 50;
    /// <summary>Target window width, 0–100.</summary>
    public double ZoneWidth { get; init; } = 20;
    /// <summary>Sweeps per second, before the verb modifiers.</summary>
    public double Speed { get; init; } = 1;
    public string ActionLabel { get; init; } = "Set pin";
    public bool ActionEnabled { get; init; } = true;
    /// <summary>Roll mode headline: the same expected value as the sweep.</summary>
    public string Odds { get; init; } = "";
    public string RollBlurb { get; init; } = "";
    public string RollLabel { get; init; } = "Roll";
    public string Message { get; init; } = "";
    public bool MessageAlert { get; init; }
}

// ── preview ───────────────────────────────────────────────────────────────────

public sealed class PreviewVm : IRegionVm
{
    public string Primitive => "preview";
    public string Title { get; init; } = "";
    public string Count { get; init; } = "";
    /// <summary>The art contract, printed in the empty slot: "sprite 64²".</summary>
    public string SlotLabel { get; init; } = "sprite 64²";
    public string Name { get; init; } = "";
    public string Desc { get; init; } = "";
    public Texture2D Art { get; init; }
    public List<string> Tags { get; init; } = new();
}

// ── readout ───────────────────────────────────────────────────────────────────

public sealed class ReadoutVm : IRegionVm
{
    public string Primitive => "readout";
    public string Title { get; init; } = "Outcome";
    public string Count { get; init; } = "";
    /// <summary>One headline number per panel, set larger than everything else.</summary>
    public string Headline { get; init; } = "";
    public string Value { get; init; } = "—";
    /// <summary>Drives the quality ramp when set; otherwise <see cref="ValueTone"/> wins.</summary>
    public double? Quality { get; init; }
    public Tone ValueTone { get; init; } = Tone.Ink;
    public double BarFraction { get; init; }
    public string Caption { get; init; } = "";
    public List<FactorVm> Factors { get; init; } = new();
    public List<StatVm> Rows { get; init; } = new();
    public TimerVm Timer { get; init; }
    public ForecastVm Forecast { get; init; }
}

public sealed class FactorVm
{
    public string Name { get; init; }
    public double Value { get; init; }
    /// <summary>False renders "??" — the information verb has not been switched on.</summary>
    public bool Known { get; init; } = true;
}

public sealed class StatVm
{
    public string Key { get; init; }
    public string Value { get; init; }
    public Tone Tone { get; init; } = Tone.Ink;
}

public sealed class TimerVm
{
    public string Label { get; init; } = "Time remaining";
    public string Clock { get; init; } = "0:00";
    public double Fraction { get; init; }
    public bool Alert { get; init; }
    public string ButtonLabel { get; init; } = "Pause";
    public string Caption { get; init; } = "";
}

public sealed class ForecastVm
{
    public string Title { get; init; } = "";
    public string Delta { get; init; } = "";
    public bool DeltaAlert { get; init; }
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    public List<double> Bars { get; init; } = new();
    /// <summary>Bars below this read as a problem.</summary>
    public double Threshold { get; init; } = 0.5;
}

// ── commit ────────────────────────────────────────────────────────────────────

public sealed class CommitVm : IRegionVm
{
    public string Primitive => "commit";
    public string Title { get; init; } = "";
    public string Count { get; init; } = "";
    public string Label { get; init; } = "Commit";
    public string Meta { get; init; } = "";
    /// <summary>The button explains its own refusal here: "You are $37 short".</summary>
    public string Hint { get; init; } = "";
    public bool Disabled { get; init; }
}
