using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ContextUi;
using LifeSim.Household;

namespace LifeSim.Cooking;

/// <summary>
/// Ferramenta de dev: o fogão em tela cheia, com pele, regiões e relógio na mão. O jogo de
/// verdade começa em <see cref="MainMenu"/>; esta cena abre pelo botão "Bancada de testes".
///
/// Reproduz o fluxo pretendido: clicar no fogão abre o MENU RÁPIDO; o painel manual está
/// atrás do verbo "Preparar manualmente…" e volta pelo ✕. Os dois são o mesmo
/// <see cref="ContextPanel"/> com definições diferentes.
///
/// É aqui que mora o estado de tela — qual contexto está aberto, qual prato está marcado no
/// menu. O painel não guarda nada disso de propósito: no jogo real quem abre o contexto é a
/// interação com o eletrodoméstico, e é ela que sabe o que o Sim estava fazendo.
/// </summary>
public partial class CookingDemo : Control
{
    [Export] public int CookingLevel { get; set; } = 6;

    /// <summary>
    /// A casa e as telas dela. As mesmas do jogo (<see cref="GameScreen"/>): aqui o fogão é a
    /// tela inteira e o tempo só anda pelos botões da barra de dev.
    /// </summary>
    private HouseInteractions _house;
    private Household.Household Home => _house.Home;

    private ContextNavigator _nav;
    private PanelContainer _screen;
    private Control _devBar;
    private Control _diary;
    private PanelSkin _skin = PanelSkin.Dark;

    public override void _Ready()
    {
        _screen = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(_screen);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        _screen.AddChild(column);

        _devBar = new MarginContainer();
        // Inline: a bancada é o fogão em tela cheia, sem casa por trás para escurecer.
        _nav = new ContextNavigator { Skin = _skin, Modal = false, SizeFlagsVertical = SizeFlags.ExpandFill };
        _diary = new MarginContainer();
        column.AddChild(_devBar);
        column.AddChild(_nav);
        column.AddChild(_diary);

        _house = new HouseInteractions(HouseInteractions.NewHousehold(CookingLevel), CookingLevel, _nav, canCloseStove: false);
        Home.Changed += Refresh;
        _house.OpenStove();
        Refresh();
    }

    // ------------------------------------------------------------------
    // Tela
    // ------------------------------------------------------------------

    /// <summary>
    /// Barra de dev, diário e fundo, que estão fora do painel. O painel não é tocado: quem o
    /// redesenha é o navegador, e recriá-lo aqui perderia a pilha e o foco.
    /// </summary>
    private void Refresh()
    {
        _screen.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = _skin.Background,
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 24,
            ContentMarginBottom = 24,
        });
        Replace(_devBar, DevBar());
        Replace(_diary, Diary());
    }

    private static void Replace(Node slot, Node content)
    {
        foreach (var child in slot.GetChildren())
        {
            slot.RemoveChild(child);
            child.QueueFree();
        }
        slot.AddChild(content);
    }

    /// <summary>Barra de autoria: trocar a pele e etiquetar as regiões do shell.</summary>
    private Control DevBar()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(DevButton("← Menu", () => SceneTransition.Go(this, GameScenes.MainMenu)));
        row.AddChild(PanelPrimitives.Text("Dev", 10, _skin.Mute));
        row.AddChild(DevButton(_skin == PanelSkin.Dark ? "Pele · escura" : "Pele · clara", ToggleSkin));
        row.AddChild(DevButton(_nav.ShowRegionLabels ? "Regiões · visíveis" : "Regiões · ocultas", ToggleRegionLabels));
        row.AddChild(PanelPrimitives.Text(
            $"Dia {Home.Day} · {Home.HourOfDay:00}h  ·  {Home.Sim.Name}: fome {Home.Sim.Hunger:0}, " +
            $"sede {Home.Sim.Thirst:0}, humor {Home.Sim.Mood:+0;-0;0}", 10, _skin.Mute));
        row.AddChild(DevButton(Home.Autonomous ? "Livre-arbítrio · ligado" : "Livre-arbítrio · desligado", ToggleAutonomy));
        row.AddChild(DevButton("+4 h", () => PassTime(4f)));
        row.AddChild(DevButton("+1 dia", () => PassTime(24f)));
        return row;
    }

    private Button DevButton(string text, Action onPress)
    {
        var button = PanelPrimitives.FlatButton(_skin, _skin.Panel, _skin.Line, 8, 3);
        button.Text = text;
        button.AddThemeFontSizeOverride("font_size", 11);
        button.Pressed += () => onPress();
        return button;
    }

    /// <summary>
    /// As últimas linhas do diário da casa, abaixo do painel. No jogo isto é a parede de
    /// notificações; aqui é o que deixa ver a autonomia agindo enquanto o tempo passa.
    /// </summary>
    private Control Diary()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);
        box.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        box.CustomMinimumSize = new Vector2(1180, 0);

        box.AddChild(PanelPrimitives.Text("Diário da casa", 10, _skin.Dim));
        if (Home.Log.Count == 0)
            box.AddChild(PanelPrimitives.Text("Nada aconteceu ainda.", 11, _skin.Mute));
        foreach (var e in Home.Log.Take(6))
            box.AddChild(PanelPrimitives.Text($"{e.Clock,-12}  {e.Text}", 11,
                DiaryText.IsAlarm(e) ? _skin.Accent : _skin.Mute));
        return box;
    }

    private void ToggleAutonomy()
    {
        Home.Autonomous = !Home.Autonomous;
        Refresh();
    }

    private void PassTime(float hours)
    {
        Home.AdvanceHours(hours);
        _house.TimePassed();
    }

    private void ToggleSkin()
    {
        _skin = _skin == PanelSkin.Dark ? PanelSkin.Light : PanelSkin.Dark;
        _nav.Skin = _skin;
        _nav.Restyle();
        Refresh();
    }

    private void ToggleRegionLabels()
    {
        _nav.ShowRegionLabels = !_nav.ShowRegionLabels;
        _nav.Restyle();
        Refresh();
    }
}
