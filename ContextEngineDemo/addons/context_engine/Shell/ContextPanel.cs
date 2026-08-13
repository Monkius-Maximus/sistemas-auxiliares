using System;
using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// The shell. It never changes: it reads a view-model, binds each primitive to a named region,
/// sizes itself to the declared width and owns the focus graph and the prompt bar. Adding the
/// vehicle-repair or school-test minigame means writing a definition and, at most, one new
/// primitive — not a new window.
/// </summary>
public partial class ContextPanel : VBoxContainer
{
    /// <summary>Fixed rail widths. Reading order is left to right, always the same.</summary>
    private const int LeftRailWidth = 244;
    private const int RightRailWidth = 332;

    private readonly Dictionary<RegionId, RegionHost> _hosts = new();

    /// <summary>Regions in traversal order — the region is the traversal unit, not the control.</summary>
    private static readonly RegionId[] Order =
    {
        RegionId.Subject, RegionId.Actions, RegionId.Primary,
        RegionId.Secondary, RegionId.Readout, RegionId.Commit,
    };

    private PanelContainer _frame;
    private Label _title;
    private Label _crumb;
    private ColorRect _titleMark;
    private Label _close;
    private PanelContainer _titleBar;
    private PanelContainer _leftRail;
    private PanelContainer _centreRail;
    private PanelContainer _rightRail;
    private PromptBar _prompts;

    private ContextController _controller;
    private Skin _skin = Skin.Dark;
    private int _focusRegion = 2;

    /// <summary>Draws the region name over every header. A dev overlay, not in-game chrome.</summary>
    public bool ShowRegionTags { get; set; }

    /// <summary>Fired when the player asks to close — the host discards the session.</summary>
    public event Action CloseRequested;

    public override void _Ready()
    {
        BuildChrome();
        SetProcessUnhandledInput(true);
    }

    public void Attach(ContextController controller)
    {
        if (_controller != null) _controller.Changed -= Render;
        _controller = controller;
        _controller.Changed += Render;
        Render();
    }

    public void SetSkin(Skin skin)
    {
        _skin = skin;
        Render();
    }

    public override void _Process(double delta) => _controller?.Tick(delta);

    // ── chrome ───────────────────────────────────────────────────────────────

    private void BuildChrome()
    {
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;

        // The definition declares a width; the shell honours it and lets height follow content.
        _frame = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(_frame);

        var stack = Ui.VBox(0);
        _frame.AddChild(stack);

        // Title bar
        _titleMark = new ColorRect { CustomMinimumSize = new Vector2(12, 12), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _title = Ui.Text("", 15, Ui.Heavy, _skin.Ink);
        _crumb = Ui.Text("", 12, Ui.Regular, _skin.Mute);
        _close = Ui.Text("✕", 13, Ui.Semi, _skin.Mute);

        var titleRow = Ui.HBox(10);
        titleRow.AddChild(_titleMark);
        titleRow.AddChild(_title);
        titleRow.AddChild(_crumb);
        titleRow.AddChild(Ui.Fill());
        titleRow.AddChild(_close);

        _titleBar = Ui.Panel(Ui.Box(_skin.Pnl2, _skin.Line, 0, 14, 11), titleRow);
        stack.AddChild(_titleBar);

        // Three rails
        var body = Ui.HBox(0);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;

        _leftRail = Rail(LeftRailWidth, Ui.VBox(18), RegionId.Subject, RegionId.Actions);
        _centreRail = Rail(0, Ui.VBox(16), RegionId.Primary, RegionId.Secondary);
        _rightRail = Rail(RightRailWidth, Ui.VBox(16), RegionId.Preview, RegionId.Readout, RegionId.Commit);

        _centreRail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        body.AddChild(_leftRail);
        body.AddChild(_centreRail);
        body.AddChild(_rightRail);
        stack.AddChild(body);

        _prompts = new PromptBar();
        stack.AddChild(_prompts);
    }

    private PanelContainer Rail(int width, VBoxContainer column, params RegionId[] regions)
    {
        foreach (var region in regions)
        {
            var host = RegionHost.Create(region);
            host.Emitted += OnIntent;
            _hosts[region] = host;

            // The outcome sits immediately above the button that causes it.
            if (region == RegionId.Commit) column.AddChild(Ui.VFill());
            column.AddChild(host);
        }

        var rail = Ui.Panel(Ui.Box(Colors.Transparent), column);
        if (width > 0) rail.CustomMinimumSize = new Vector2(width, 0);
        return rail;
    }

    // ── render ───────────────────────────────────────────────────────────────

    private void Render()
    {
        var vm = _controller?.ViewModel;
        if (vm == null) return;

        _frame.AddThemeStyleboxOverride("panel", Ui.Box(_skin.Pnl, _skin.Line, 2));
        _frame.CustomMinimumSize = new Vector2(vm.Width, 0);
        CustomMinimumSize = new Vector2(vm.Width, 0);

        _titleBar.AddThemeStyleboxOverride("panel", TitleBox());
        _titleMark.Color = _skin.Acc;
        _title.Text = vm.Title;
        _title.AddThemeColorOverride("font_color", _skin.Ink);
        _crumb.Text = vm.Crumb;
        _crumb.AddThemeColorOverride("font_color", _skin.Mute);
        _close.AddThemeColorOverride("font_color", _skin.Mute);

        RailStyle(_leftRail, Colors.Transparent, right: true);
        RailStyle(_centreRail, Colors.Transparent, right: true);
        RailStyle(_rightRail, _skin.Pnl2, right: false);

        foreach (var (region, host) in _hosts)
            host.Bind(vm.Region(region), _skin, ShowRegionTags);

        RefreshPrompts();
    }

    private StyleBoxFlat TitleBox()
    {
        var box = Ui.Box(_skin.Pnl2, _skin.Line, 0, 14, 11);
        box.BorderColor = _skin.Line;
        box.BorderWidthBottom = 2;
        return box;
    }

    private void RailStyle(PanelContainer rail, Color background, bool right)
    {
        var box = Ui.Box(background, _skin.Line, 0, 14, 14);
        box.BorderColor = _skin.Line;
        if (right) box.BorderWidthRight = 2;
        rail.AddThemeStyleboxOverride("panel", box);
    }

    private void RefreshPrompts()
    {
        var host = _hosts.TryGetValue(Order[_focusRegion], out var h) ? h : null;
        var prompts = new List<(string, string, string)>();
        if (host?.Current != null) prompts.AddRange(host.Current.Prompts);
        prompts.Add(("Y / Start", "Enter", "Commit"));
        prompts.Add(("B", "Esc", "Close"));
        _prompts.Bind(prompts, _skin);
    }

    // ── input ────────────────────────────────────────────────────────────────

    private void OnIntent(Intent intent) => _controller?.Dispatch(intent);

    public override void _Input(InputEvent @event)
    {
        if (_prompts == null) return;
        if (@event is InputEventJoypadButton or InputEventJoypadMotion) _prompts.Gamepad = true;
        else if (@event is InputEventKey or InputEventMouseButton) _prompts.Gamepad = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ce_region_next")) { JumpRegion(1); GetViewport().SetInputAsHandled(); }
        else if (@event.IsActionPressed("ce_region_prev")) { JumpRegion(-1); GetViewport().SetInputAsHandled(); }
        else if (@event.IsActionPressed("ce_commit")) { _controller?.Dispatch(new Committed()); GetViewport().SetInputAsHandled(); }
        else if (@event.IsActionPressed("ce_close")) { CloseRequested?.Invoke(); GetViewport().SetInputAsHandled(); }
    }

    /// <summary>
    /// LB / RB and Tab move by region, never by control: the region is the traversal unit, and
    /// re-entering one restores the cell the player left from, so focus never falls in a corner.
    /// </summary>
    public void JumpRegion(int direction)
    {
        for (var step = 1; step <= Order.Length; step++)
        {
            var index = ((_focusRegion + direction * step) % Order.Length + Order.Length) % Order.Length;
            var host = _hosts.TryGetValue(Order[index], out var h) ? h : null;
            if (host is not { Visible: true } || host.Current is not { HasFocusable: true }) continue;

            _focusRegion = index;
            host.Current.FocusRestore();
            RefreshPrompts();
            return;
        }
    }

    /// <summary>Focus lands where the definition says it should, not wherever the tree happens to start.</summary>
    public void FocusEntry(RegionId region)
    {
        for (var i = 0; i < Order.Length; i++)
        {
            if (Order[i] != region) continue;
            _focusRegion = i;
            break;
        }
        if (_hosts.TryGetValue(region, out var host) && host.Current is { HasFocusable: true })
            host.Current.FocusFirst();
        RefreshPrompts();
    }
}
