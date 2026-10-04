using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ContextUi;
using LifeSim.Household;

namespace LifeSim.Cooking;

/// <summary>
/// A casa em jogo. O loop é o de qualquer life sim:
///
/// <code>
///   _Process ─ relógio corre? ─ sim ─ Household.AdvanceHours(1) a cada hora inteira
///                                      └ Changed ─ HUD e diário redesenham
///   clique num objeto ─ HouseInteractions abre um contexto ─ painel por cima, tempo parado
///   ✕ / Esc no painel ─ fecha o contexto ─ tempo volta a correr
///   Esc sem painel ─ menu de pausa ─ Continuar · Menu principal · Sair
/// </code>
///
/// Nada de regra mora aqui: comer, cozinhar, beber e comprar são da <c>Household</c>; qual
/// tela abre é da <see cref="HouseInteractions"/>. Esta classe só liga clique a chamada e
/// redesenha quando a casa avisa.
/// </summary>
public partial class GameScreen : Control
{
    [Export] public int CookingLevel { get; set; } = 6;

    private readonly PanelSkin _skin = PanelSkin.Dark;
    private readonly GameClock _clock = new();

    private HouseInteractions _house;
    private Household.Household Home => _house.Home;

    private Control _hud;
    private Control _room;
    private Control _diary;
    private Control _overlay;
    private ContextPanel _panel;
    private Control _pauseMenu;

    /// <summary>O objeto clicado por último: fechar o painel devolve o foco a ele, não ao começo da tela.</summary>
    private string _lastObject;

    /// <summary>
    /// Os objetos da casa. Cada um é um id (que acha a arte em <c>Art/House/&lt;id&gt;.png</c>),
    /// um nome, o verbo do clique, a cor do marcador e o que o clique faz.
    /// </summary>
    private sealed record HouseObject(string Id, string Name, string Verb, Color Tint,
                                      Action Use, Func<string> Status, Func<bool> Enabled);

    private List<HouseObject> _objects;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        _house = new HouseInteractions(HouseInteractions.NewHousehold(CookingLevel), CookingLevel, canCloseStove: true);
        _house.Opened += ShowPanel;
        _house.Changed += () => _panel?.Rebuild();
        _house.Closed += HidePanel;
        Home.Changed += Refresh;

        _objects = new List<HouseObject>
        {
            new("geladeira", "Geladeira", "Comer", new Color("8fb3c9"),
                () => _house.OpenFridge(), FridgeStatus, () => true),
            new("fogao", "Fogão", "Cozinhar", new Color("c96f4a"),
                _house.OpenStove, PantryStatus, () => true),
            new("pia", "Pia", "Beber água", new Color("5b8fc7"),
                Home.DrinkWater, () => $"sede {Home.Sim.Thirst:0} / 100", () => Home.Sim.Thirst < 100f),
            new("porta", "Porta", "Ir às compras", new Color("6f9a5a"),
                () => _house.OpenShop(), ShopStatus, () => true),
        };

        AddChild(Background());

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 24);
        AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 18);
        margin.AddChild(column);

        _hud = new MarginContainer();
        _room = new CenterContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _diary = new MarginContainer();
        column.AddChild(_hud);
        column.AddChild(_room);
        column.AddChild(_diary);

        Refresh();
        FocusObject(_objects[1].Id);
    }

    // ------------------------------------------------------------------
    // Loop
    // ------------------------------------------------------------------

    /// <summary>
    /// O tempo só corre na casa: com o painel ou o menu de pausa abertos, o mundo espera o
    /// jogador. Decisão de protótipo — no The Sims o tempo segue com a interface aberta, e
    /// isso entra quando a autonomia souber não atropelar o que o jogador está montando.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_house.IsOpen || _pauseMenu is not null)
            return;

        int hours = _clock.Tick(delta);
        if (hours > 0)
            Home.AdvanceHours(hours);
        else if (ShownMinutes() != _shownMinutes)
            RefreshHud();  // os minutos andam entre uma hora e outra, de dez em dez
    }

    /// <summary>O minuto que o relógio do HUD mostra. Redesenhar só quando ele muda poupa um HUD novo por quadro.</summary>
    private int ShownMinutes() => (int)(_clock.HourFraction * 60f) / 10 * 10;
    private int _shownMinutes;

    public override void _UnhandledInput(InputEvent @event)
    {
        // O painel aberto recebe a entrada antes (é filho mais fundo) e consome o Esc dele.
        if (@event.IsActionPressed("game_menu"))
        {
            if (_pauseMenu is null) ShowPauseMenu();
            else HidePauseMenu();
            AcceptEvent();
            return;
        }

        if (_pauseMenu is not null || _house.IsOpen)
            return;

        if (@event.IsActionPressed("game_pause")) { _clock.TogglePause(); RefreshHud(); AcceptEvent(); }
        else if (@event.IsActionPressed("game_speed_1")) { SetSpeed(1); AcceptEvent(); }
        else if (@event.IsActionPressed("game_speed_2")) { SetSpeed(2); AcceptEvent(); }
        else if (@event.IsActionPressed("game_speed_3")) { SetSpeed(3); AcceptEvent(); }
    }

    private void SetSpeed(int speed)
    {
        _clock.SetSpeed(speed);
        RefreshHud();
    }

    private void Refresh()
    {
        RefreshHud();
        RefreshRoom();
        RefreshDiary();
    }

    // ------------------------------------------------------------------
    // HUD
    // ------------------------------------------------------------------

    private void RefreshHud()
    {
        Clear(_hud);

        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", PanelPrimitives.Box(_skin.Panel, _skin.Line, 14, 10));
        _hud.AddChild(bar);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);
        bar.AddChild(row);

        _shownMinutes = ShownMinutes();
        row.AddChild(Stacked($"Dia {Home.Day}", $"{Home.HourOfDay:00}:{_shownMinutes:00}", 20));
        row.AddChild(SpeedButtons());
        row.AddChild(Separator());

        row.AddChild(HudIcon("retrato-sim", Home.Sim.Name, NeedArt.MoodTint));
        row.AddChild(Need("Fome", NeedArt.HungerId, Home.Sim.Hunger, NeedArt.HungerTint));
        row.AddChild(Need("Sede", NeedArt.ThirstId, Home.Sim.Thirst, NeedArt.ThirstTint));
        row.AddChild(Stacked("Humor", $"{Home.Sim.Mood:+0;-0;0}", 20,
            Home.Sim.Mood < 0 ? _skin.Accent : _skin.Ink));
        row.AddChild(Separator());
        row.AddChild(Stacked("Dinheiro", $"${Home.Funds}", 20));

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        row.AddChild(HudButton(Home.Autonomous ? "Livre-arbítrio · ligado" : "Livre-arbítrio · desligado", () =>
        {
            Home.Autonomous = !Home.Autonomous;
            RefreshHud();
        }, Home.Autonomous));
        row.AddChild(HudButton("Menu  (Esc)", ShowPauseMenu, false));
    }

    private Control SpeedButtons()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        string[] labels = { "II", ">", ">>", ">>>" };
        string[] art = { "pausa", "velocidade-1", "velocidade-2", "velocidade-3" };
        for (int speed = 0; speed <= GameClock.FastestSpeed; speed++)
        {
            int captured = speed;
            var button = HudButton(labels[speed], () => SetSpeed(captured), _clock.Speed == speed);
            button.CustomMinimumSize = new Vector2(38, 34);
            button.Alignment = HorizontalAlignment.Center;
            var icon = ArtLibrary.Find(ArtLibrary.Hud, art[speed]);
            if (icon is not null)
            {
                button.Text = "";
                button.Icon = icon;
                button.ExpandIcon = true;
            }
            row.AddChild(button);
        }
        return row;
    }

    private Control Need(string name, string artId, float value, Color color)
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(130, 0) };
        column.AddThemeConstantOverride("separation", 4);

        var head = new HBoxContainer();
        head.AddChild(HudIcon(artId, "", color, 16));
        var label = PanelPrimitives.Text(name, 10, _skin.Dim);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(label);
        head.AddChild(PanelPrimitives.Text($"{value:0}", 12, value < Sim.NeedLow ? _skin.Accent : _skin.Ink));
        column.AddChild(head);

        var bar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = value, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) };
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = _skin.Cell });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = value < Sim.NeedLow ? _skin.Accent : color });
        column.AddChild(bar);
        return column;
    }

    /// <summary>Ícone do HUD (<c>Art/Hud/&lt;id&gt;.png</c>), ou o marcador com as iniciais.</summary>
    private Control HudIcon(string id, string caption, Color tint, int size = 28)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(PanelPrimitives.Swatch(ArtLibrary.Find(ArtLibrary.Hud, id), tint,
            caption.Length > 0 ? caption : id, size, size));
        if (caption.Length > 0)
            row.AddChild(PanelPrimitives.Text(caption, 14, _skin.Ink));
        return row;
    }

    private Control Stacked(string caption, string value, int size, Color? color = null)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        column.AddChild(PanelPrimitives.Text(caption, 10, _skin.Dim));
        column.AddChild(PanelPrimitives.Text(value, size, color ?? _skin.Ink));
        return column;
    }

    private Control Separator()
    {
        var line = new ColorRect { Color = _skin.LineSoft, CustomMinimumSize = new Vector2(2, 0) };
        return line;
    }

    private Button HudButton(string text, Action onPress, bool active)
    {
        var button = PanelPrimitives.FlatButton(_skin, active ? _skin.AccentDeep : _skin.Cell, active ? _skin.Accent : _skin.LineSoft, 10, 6);
        button.Text = text;
        button.AddThemeFontSizeOverride("font_size", 12);
        // Botões do HUD são de mouse: com foco, Espaço e Enter os apertariam de novo no meio
        // do jogo. O controle chega a eles pelas teclas próprias (velocidade, menu).
        button.FocusMode = FocusModeEnum.None;
        button.Pressed += () => onPress();
        return button;
    }

    // ------------------------------------------------------------------
    // A casa
    // ------------------------------------------------------------------

    private void RefreshRoom()
    {
        var focused = GetViewport()?.GuiGetFocusOwner() is Control owner && _room.IsAncestorOf(owner)
            ? owner.Name.ToString()
            : null;

        Clear(_room);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        _room.AddChild(column);

        column.AddChild(PanelPrimitives.Text($"Cozinha de {Home.Sim.Name}", 13, _skin.Dim));

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);
        column.AddChild(row);
        foreach (var obj in _objects)
            row.AddChild(ObjectButton(obj));

        column.AddChild(PanelPrimitives.Text(
            "Clique num objeto · P pausa · 1 2 3 velocidade · Esc menu", 10, _skin.Mute));

        if (focused is not null)
            FocusObject(focused);
    }

    private Control ObjectButton(HouseObject obj)
    {
        var button = PanelPrimitives.FlatButton(_skin, _skin.Panel, _skin.Line);
        button.Name = obj.Id;
        button.CustomMinimumSize = new Vector2(220, 280);
        button.Disabled = !obj.Enabled();
        button.TooltipText = $"{obj.Verb}: {obj.Name}";

        var content = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        content.AddThemeConstantOverride("separation", 8);

        var art = PanelPrimitives.Swatch(ArtLibrary.Find(ArtLibrary.House, obj.Id), obj.Tint, obj.Name, 180, 180);
        art.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        content.AddChild(art);

        content.AddChild(Centered(PanelPrimitives.Text(obj.Name, 16, _skin.Ink)));
        content.AddChild(Centered(PanelPrimitives.Text(obj.Verb, 11, _skin.Accent)));
        content.AddChild(Centered(PanelPrimitives.Text(obj.Status(), 10, _skin.Mute)));
        PanelPrimitives.Fill(button, content, inset: 12);

        button.Pressed += () =>
        {
            _lastObject = obj.Id;
            obj.Use();
        };
        return button;
    }

    private static Label Centered(Label label)
    {
        label.HorizontalAlignment = HorizontalAlignment.Center;
        return label;
    }

    /// <summary>
    /// Procura o botão só no fim do quadro: uma ação costuma redesenhar a casa mais de uma vez
    /// (o modelo avisa a cada mudança), e o botão achado agora pode já não existir no fim.
    /// </summary>
    private void FocusObject(string id) => Callable.From(() =>
    {
        if (_room.FindChild(id, recursive: true, owned: false) is Control target)
            target.GrabFocus();
    }).CallDeferred();

    private string PantryStatus()
    {
        var pantry = Home.Kitchen.Pantry;
        int units = pantry.Defs.Sum(pantry.UnitsOf);
        return pantry.RottenUnits > 0 ? $"{units} na despensa · {pantry.RottenUnits} estragados" : $"{units} na despensa";
    }

    private string FridgeStatus()
    {
        int servings = Home.Meals.Sum(m => m.Servings);
        return Home.Meals.Count == 0 ? "vazia"
            : $"{Home.Meals.Count} {(Home.Meals.Count == 1 ? "refeição" : "refeições")} · {servings} {(servings == 1 ? "porção" : "porções")}";
    }

    private string ShopStatus()
    {
        var open = ContentLibrary.Vendors().Where(v => v.IsOpenAt(Home.HourOfDay)).Select(v => v.DisplayName).ToList();
        return open.Count == 0 ? "tudo fechado" : "aberto: " + string.Join(", ", open).ToLowerInvariant();
    }

    // ------------------------------------------------------------------
    // Diário
    // ------------------------------------------------------------------

    private void RefreshDiary()
    {
        Clear(_diary);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);
        _diary.AddChild(box);

        box.AddChild(PanelPrimitives.Text("Diário da casa", 10, _skin.Dim));
        if (Home.Log.Count == 0)
            box.AddChild(PanelPrimitives.Text("Nada aconteceu ainda.", 11, _skin.Mute));
        foreach (var e in Home.Log.Take(5))
            box.AddChild(PanelPrimitives.Text($"{e.Clock,-12}  {e.Text}", 11,
                DiaryText.IsAlarm(e) ? _skin.Accent : _skin.Mute));
    }

    // ------------------------------------------------------------------
    // Painel e pausa
    // ------------------------------------------------------------------

    /// <summary>Um contexto abriu (ou trocou): painel novo por cima da casa, que escurece.</summary>
    private void ShowPanel()
    {
        RemoveOverlay();

        _overlay = Dimmer();
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        _overlay.AddChild(center);

        _panel = new ContextPanel { Definition = _house.Definition, Skin = _skin };
        center.AddChild(_panel);
        AddChild(_overlay);
    }

    private void HidePanel()
    {
        RemoveOverlay();
        Refresh();
        if (_lastObject is not null)
            FocusObject(_lastObject);
    }

    private void RemoveOverlay()
    {
        _panel = null;
        if (_overlay is null) return;
        RemoveChild(_overlay);
        _overlay.QueueFree();
        _overlay = null;
    }

    private void ShowPauseMenu()
    {
        if (_pauseMenu is not null || _house.IsOpen) return;

        _pauseMenu = Dimmer();
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        _pauseMenu.AddChild(center);

        var frame = new PanelContainer();
        frame.AddThemeStyleboxOverride("panel", PanelPrimitives.Box(_skin.Panel, _skin.Line, 24, 20));
        center.AddChild(frame);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
        column.AddThemeConstantOverride("separation", 8);
        frame.AddChild(column);

        column.AddChild(PanelPrimitives.Text("Pausado", 18, _skin.Ink));
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var resume = PauseButton("Continuar", HidePauseMenu);
        column.AddChild(resume);
        column.AddChild(PauseButton("Menu principal", () => GetTree().ChangeSceneToFile(GameScenes.MainMenu)));
        column.AddChild(PauseButton("Sair do jogo", () => GetTree().Quit()));

        AddChild(_pauseMenu);
        resume.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void HidePauseMenu()
    {
        if (_pauseMenu is null) return;
        RemoveChild(_pauseMenu);
        _pauseMenu.QueueFree();
        _pauseMenu = null;
        FocusObject(_lastObject ?? _objects[1].Id);
    }

    private Button PauseButton(string text, Action onPress)
    {
        var button = PanelPrimitives.FlatButton(_skin, _skin.Cell, _skin.LineSoft, 14, 0);
        button.Text = text;
        button.CustomMinimumSize = new Vector2(0, 40);
        button.Pressed += () => onPress();
        return button;
    }

    /// <summary>Cobre a tela inteira e engole o clique: com algo aberto, a casa por trás não responde.</summary>
    private Control Dimmer()
    {
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        return dim;
    }

    private Control Background()
    {
        var art = ArtLibrary.Find(ArtLibrary.House, "fundo-cozinha");
        if (art is null)
        {
            var flat = new ColorRect { Color = _skin.Background };
            flat.SetAnchorsPreset(LayoutPreset.FullRect);
            return flat;
        }

        var picture = new TextureRect
        {
            Texture = art,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        };
        picture.SetAnchorsPreset(LayoutPreset.FullRect);
        return picture;
    }

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
