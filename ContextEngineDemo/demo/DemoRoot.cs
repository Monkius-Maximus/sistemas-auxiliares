using System.Collections.Generic;
using Godot;

namespace ContextEngine.Demo;

/// <summary>
/// The dev harness around the panel: a context switcher, the two skins, the region overlay and
/// the context-definition dump. Ship something like this behind a debug flag — being able to
/// hot-swap contexts without walking to the stove is worth an afternoon of work several times over.
///
/// Nothing in here is in-game chrome. The game opens <see cref="ContextPanel"/> directly and
/// hands it a <see cref="ContextController"/>.
/// </summary>
public partial class DemoRoot : Control
{
    private static readonly (string Id, string Name)[] Contexts =
    {
        ("cook", "Cooking"), ("shop", "Shop"), ("craft", "Crafting"), ("xfer", "Transfer"),
        ("exam", "School test"), ("lock", "Lockpick"), ("fix", "Vehicle repair"),
    };

    private static readonly (string Key, string Value)[] PrimitiveDocs =
    {
        ("subject.picker", "Single-select tiles. The thing being acted upon — vessel, vendor, bench, container."),
        ("actions.verbs", "Skill-gated toggles that mutate the evaluator: haggle, taste, measure twice. Locked entries teach the skill exists."),
        ("grid.qty", "Slot grid with ± steppers, stock line and a slot budget. Cooking, shopping."),
        ("grid.select", "Same tile, single-select. Blueprints, recipes, dialogue branches."),
        ("grid.dual", "Two lists with a move axis. Transfer, loot, trade-in."),
        ("checklist", "Have vs need, per row. Crafting requirements, quest turn-in, repair parts."),
        ("preview", "Art slot + name + one line + tags. The one region an artist owns."),
        ("readout", "Headline value + bar, optional 0–100 factor breakdown, and a numeric stack."),
        ("question", "Prompt plus single-select answers and a pager. Exams, quizzes, dialogue lines — any A/B/C/D choice."),
        ("hotspot.map", "A schematic with focusable regions, each carrying a condition value. Engine bays, body parts, floorplans."),
        ("sequence", "Ordered steps; taking them out of order is recorded and penalised. Repair procedures, exam working, methods."),
        ("timing.bar", "The only real-time primitive — a sweeping marker and a target window. Isolated, and always paired with a skill-check fallback."),
        ("readout.timer", "Countdown with a pause. A context clock, not wall time: pausing is legal and the panel stays evaluator-pure."),
        ("readout.forecast", "Value-over-time bars. Grade trajectory, reliability after n trips, spoilage, crop yield."),
        ("commit", "One action per context, label and enablement declared by the definition."),
    };

    private readonly Dictionary<string, IContextModule> _modules = new();
    private readonly ContextController _controller = new();

    private Skin _skin = Skin.Dark;
    private string _current = "cook";
    private bool _showRegions;
    private bool _showSpec;

    private ColorRect _ground;
    private VBoxContainer _column;
    private VBoxContainer _mastheadSlot;
    private VBoxContainer _devBarSlot;
    private VBoxContainer _specSlot;
    private ContextPanel _panel;

    public override void _Ready()
    {
        _modules["cook"] = new CookModule();
        _modules["shop"] = new ShopModule();
        _modules["craft"] = new CraftModule();
        _modules["xfer"] = new TransferModule();
        _modules["exam"] = new ExamModule();
        _modules["lock"] = new LockModule();
        _modules["fix"] = new RepairModule();

        SetAnchorsPreset(LayoutPreset.FullRect);

        _ground = new ColorRect();
        _ground.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_ground);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto };
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(scroll);

        _column = Ui.VBox(18);
        _column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(Ui.Pad(_column, 32, 28, 32, 64));

        _mastheadSlot = Ui.VBox(0);
        _devBarSlot = Ui.VBox(0);
        _specSlot = Ui.VBox(0);
        _panel = new ContextPanel();
        _panel.CloseRequested += () => GD.Print("[context] close requested — the host would discard the session here.");

        _column.AddChild(_mastheadSlot);
        _column.AddChild(_devBarSlot);
        _column.AddChild(_panel);
        _column.AddChild(_specSlot);

        _controller.CommitRequested += module =>
            GD.Print($"[context] commit '{module.Definition.CommitAction}' — the host writes session state into the world here.");

        _controller.Load(_modules[_current]);
        _panel.Attach(_controller);
        Render();
        FocusPanel();
    }


    private void FocusPanel()
    {
        var entry = _modules[_current].Definition.FocusEntry;
        Callable.From(() => _panel.FocusEntry(entry)).CallDeferred();
    }

    private void Render()
    {
        _ground.Color = _skin.Bg;

        Fill(_mastheadSlot, Masthead());
        Fill(_devBarSlot, DevBar());
        Fill(_specSlot, SpecSection());

        _panel.ShowRegionTags = _showRegions;
        _panel.SetSkin(_skin);
    }

    private static void Fill(VBoxContainer slot, Control content)
    {
        foreach (var child in slot.GetChildren())
        {
            slot.RemoveChild(child);
            child.QueueFree();
        }
        slot.AddChild(content);
    }

    // ── dev chrome ───────────────────────────────────────────────────────────

    private Control Masthead()
    {
        var block = Ui.VBox(6);
        block.AddChild(Ui.Upper("Context Engine · UI concept", 11, _skin.Acc, Ui.Heavy, 2));
        block.AddChild(Ui.Text("One panel, seven contexts", 34, Ui.Heavy, _skin.Ink));

        var blurb = Ui.Wrapped(
            "Every interaction is the same shell filled by a context definition. Seven named regions — " +
            "subject, actions, primary, secondary, preview, readout, commit — read left to right: what you " +
            "act on, what you do, the outcome. The panel is live: add units, haggle, sit the test, feel for " +
            "the pin, work the engine bay.", 14, Ui.Regular, _skin.Dim, 1.55f);
        blurb.CustomMinimumSize = new Vector2(0, 0);
        blurb.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        blurb.CustomMinimumSize = new Vector2(900, 0);
        block.AddChild(blurb);
        return block;
    }

    private Control DevBar()
    {
        var block = Ui.VBox(0);
        block.AddChild(Ui.Rule(_skin.Line));
        block.AddChild(Ui.Spacer(0, 12));

        var row = Ui.HBox(16);

        var left = Ui.VBox(7);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        left.AddChild(Ui.Upper("Dev · load context", 10, _skin.Mute, Ui.Semi, 2));

        var buttons = new HFlowContainer();
        buttons.AddThemeConstantOverride("h_separation", 6);
        buttons.AddThemeConstantOverride("v_separation", 6);
        foreach (var (id, name) in Contexts)
            buttons.AddChild(ContextButton(id, name));
        left.AddChild(buttons);
        row.AddChild(left);

        row.AddChild(Ui.Fill());

        var toggles = Ui.HBox(6);
        toggles.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        toggles.AddChild(Toggle(_skin.Label, () => { _skin = _skin.Id == "dark" ? Skin.Light : Skin.Dark; Render(); }));
        toggles.AddChild(Toggle(_showRegions ? "Regions · shown" : "Regions · hidden",
            () => { _showRegions = !_showRegions; Render(); }));
        row.AddChild(toggles);

        block.AddChild(row);
        block.AddChild(Ui.Spacer(0, 12));
        block.AddChild(Ui.Rule(_skin.Line));
        return block;
    }

    private Control ContextButton(string id, string name)
    {
        var active = id == _current;
        var button = new Pressable();
        button.Style(
            Ui.Box(active ? _skin.Acc : Colors.Transparent, active ? _skin.Acc : _skin.Line, 1, 14, 8),
            _skin.Acc,
            Ui.Box(active ? _skin.Acc : _skin.Pnl2, active ? _skin.Acc : _skin.Line, 1, 14, 8));
        button.Pressed += () =>
        {
            _current = id;
            _controller.Load(_modules[id]);
            Render();
            FocusPanel();
        };

        var row = Ui.HBox(9);
        row.AddChild(Ui.Text(name, 13, Ui.Semi, active ? _skin.BtnFg : _skin.Dim));
        var tag = Ui.Text(id, 10, Ui.Regular, active ? _skin.BtnFg : _skin.Dim, 0, true);
        tag.Modulate = new Color(1, 1, 1, 0.6f);
        row.AddChild(tag);
        button.AddChild(row);
        return button;
    }

    private Control Toggle(string label, System.Action onPress)
    {
        var button = new Pressable();
        button.Style(Ui.Box(Colors.Transparent, _skin.Line, 1, 13, 8), _skin.Acc,
            Ui.Box(_skin.Pnl2, _skin.Line, 1, 13, 8));
        button.Pressed += onPress;
        button.AddChild(Ui.Text(label, 12, Ui.Semi, _skin.Dim));
        return button;
    }

    // ── the definition dump ──────────────────────────────────────────────────

    private Control SpecSection()
    {
        var block = Ui.VBox(8);
        block.AddChild(Ui.Rule(_skin.Line));
        block.AddChild(Ui.Spacer(0, 4));

        var toggle = new Pressable { SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        toggle.Style(Ui.Box(Colors.Transparent, _skin.Line, 1, 12, 7), _skin.Acc,
            Ui.Box(_skin.Pnl2, _skin.Line, 1, 12, 7));
        toggle.Pressed += () => { _showSpec = !_showSpec; Render(); };
        toggle.AddChild(Ui.Upper(_showSpec ? "▾ Hide context definition" : "▸ Show context definition",
            11, _skin.Dim, Ui.Semi, 1));
        block.AddChild(toggle);

        if (!_showSpec) return block;

        var columns = Ui.HBox(14);

        var json = Ui.Text(_modules[_current].Definition.ToJson(), 11, Ui.Regular, _skin.Dim, 0, true);
        json.AutowrapMode = TextServer.AutowrapMode.Off;
        var jsonBox = Ui.Panel(Ui.Box(_skin.Pnl, _skin.Line, 1, 14, 14), json);
        jsonBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        columns.AddChild(jsonBox);

        var notes = Ui.VBox(10);
        notes.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        notes.AddChild(Ui.Wrapped(
            "The shell never changes. It reads a definition like the one on the left, binds each primitive " +
            "to a named region, and sizes itself to the declared width. Adding the vehicle-repair or " +
            "school-test minigame means writing a new definition and, at most, one new primitive — not a new window.",
            12, Ui.Regular, _skin.Dim, 1.6f));

        foreach (var (key, value) in PrimitiveDocs)
        {
            var row = Ui.HBox(10);
            var name = Ui.Text(key, 11, Ui.Semi, _skin.Acc, 0, true);
            name.CustomMinimumSize = new Vector2(126, 0);
            row.AddChild(name);

            var text = Ui.Wrapped(value, 11, Ui.Regular, _skin.Dim, 1.45f);
            text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(text);

            var line = Ui.VBox(6);
            line.AddChild(row);
            line.AddChild(Ui.Rule(_skin.Ln2, 1));
            notes.AddChild(line);
        }

        columns.AddChild(notes);
        block.AddChild(columns);
        return block;
    }
}
