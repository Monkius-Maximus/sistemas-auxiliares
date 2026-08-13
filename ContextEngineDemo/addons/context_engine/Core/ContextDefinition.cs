using Godot;
using Godot.Collections;

namespace ContextEngine;

/// <summary>
/// One region's line in a definition: which primitive fills it and where its data comes from.
/// A source is a query, not a hard-coded list — "inventory:food", "vendor.stock",
/// "containers@reach". Resolving it is the module's job; keep the query language small.
/// </summary>
[GlobalClass]
public partial class RegionSpec : Resource
{
    [Export] public string Primitive { get; set; } = "";
    [Export] public string Source { get; set; } = "";

    /// <summary>Free-form extras — "slots", "gatedBy", "evaluator", "scored", "mode".</summary>
    [Export] public Dictionary Options { get; set; } = new();

    public RegionSpec() { }

    public RegionSpec(string primitive, string source = "", params (string Key, Variant Value)[] options)
    {
        Primitive = primitive;
        Source = source;
        Options = new Dictionary();
        foreach (var (key, value) in options)
            Options[key] = value;
    }

    public Dictionary ToDict()
    {
        var d = new Dictionary { { "primitive", Primitive } };
        if (!string.IsNullOrEmpty(Source)) d["source"] = Source;
        foreach (var key in Options.Keys)
            d[key] = Options[key];
        return d;
    }
}

/// <summary>
/// A context is a description of an interaction; the panel is a renderer for that description.
/// Saved as a .tres so a designer gets inspector editing, hot reload and version control for free.
/// Adding an activity means writing one of these — never a new window.
/// </summary>
[GlobalClass]
public partial class ContextDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string Title { get; set; } = "";
    /// <summary>The breadcrumb under the title. Half the reason two contexts feel different.</summary>
    [Export] public string Crumb { get; set; } = "";

    /// <summary>Declared width in px. Height follows content; on console this is clamped to title-safe.</summary>
    [Export] public int Width { get; set; } = 1180;

    /// <summary>
    /// §12's open question, made a flag rather than an assumption: a shop pauses the world,
    /// a repair under pressure does not. Read by the host before it opens the panel.
    /// </summary>
    [Export] public bool PausesWorld { get; set; } = true;

    [Export] public RegionSpec Subject { get; set; }
    [Export] public RegionSpec Actions { get; set; }
    [Export] public RegionSpec Primary { get; set; }
    [Export] public RegionSpec Secondary { get; set; }
    [Export] public RegionSpec Preview { get; set; }
    [Export] public RegionSpec Readout { get; set; }

    [Export] public string CommitAction { get; set; } = "";
    [Export] public string CommitLabel { get; set; } = "";
    /// <summary>The refusal, as an expression: "total > wallet". Also printed under the button.</summary>
    [Export] public string CommitBlockedWhen { get; set; } = "";
    [Export] public Dictionary CommitXp { get; set; } = new();

    /// <summary>Which region takes focus when the panel opens. Focus is authored, never inferred.</summary>
    [Export] public RegionId FocusEntry { get; set; } = RegionId.Primary;

    public RegionSpec Region(RegionId id) => id switch
    {
        RegionId.Subject => Subject,
        RegionId.Actions => Actions,
        RegionId.Primary => Primary,
        RegionId.Secondary => Secondary,
        RegionId.Preview => Preview,
        RegionId.Readout => Readout,
        _ => null,
    };

    /// <summary>The definition dump shown behind the dev "context definition" toggle.</summary>
    public string ToJson()
    {
        var d = new Dictionary
        {
            { "id", Id },
            { "title", Title },
            { "width", Width },
            { "pausesWorld", PausesWorld },
        };
        if (!string.IsNullOrEmpty(Crumb)) d["crumb"] = Crumb;

        foreach (var id in new[] { RegionId.Subject, RegionId.Actions, RegionId.Primary, RegionId.Secondary, RegionId.Preview, RegionId.Readout })
        {
            var spec = Region(id);
            if (spec != null) d[id.ToString().ToLowerInvariant()] = spec.ToDict();
        }

        var commit = new Dictionary { { "action", CommitAction }, { "label", CommitLabel } };
        if (!string.IsNullOrEmpty(CommitBlockedWhen)) commit["blockedWhen"] = CommitBlockedWhen;
        if (CommitXp.Count > 0) commit["xp"] = CommitXp;
        d["commit"] = commit;
        d["focusEntry"] = FocusEntry.ToString().ToLowerInvariant();

        // Insertion order, not alphabetical: the dump should read in the order the panel does.
        return Json.Stringify(d, "  ", false);
    }
}
