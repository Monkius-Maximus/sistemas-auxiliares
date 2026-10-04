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

    private ContextPanel _panel;
    private PanelSkin _skin = PanelSkin.Dark;
    private bool _showRegionLabels;

    public override void _Ready()
    {
        _house = new HouseInteractions(HouseInteractions.NewHousehold(CookingLevel), CookingLevel, canCloseStove: false);
        _house.Opened += Render;
        _house.Changed += () => _panel?.Rebuild();
        // Geladeira e loja abertas pelo menu voltam a ele; sem objeto da casa para abrir
        // outra coisa, fechar aqui só pode cair de volta no fogão.
        _house.Closed += _house.OpenStove;
        _house.OpenStove();
    }

    // ------------------------------------------------------------------
    // Tela
    // ------------------------------------------------------------------

    private void Render()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _panel = new ContextPanel
        {
            Definition = _house.Definition,
            Skin = _skin,
            ShowRegionLabels = _showRegionLabels,
        };

        var screen = new PanelContainer { AnchorRight = 1, AnchorBottom = 1 };
        screen.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = _skin.Background,
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 24,
            ContentMarginBottom = 24,
        });
        AddChild(screen);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        screen.AddChild(column);

        column.AddChild(DevBar());
        column.AddChild(_panel);
        column.AddChild(Diary());
    }

    /// <summary>Barra de autoria: trocar a pele e etiquetar as regiões do shell.</summary>
    private Control DevBar()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(DevButton("← Menu", () => GetTree().ChangeSceneToFile(GameScenes.MainMenu)));
        row.AddChild(PanelPrimitives.Text("Dev", 10, _skin.Mute));
        row.AddChild(DevButton(_skin == PanelSkin.Dark ? "Pele · escura" : "Pele · clara", ToggleSkin));
        row.AddChild(DevButton(_showRegionLabels ? "Regiões · visíveis" : "Regiões · ocultas", ToggleRegionLabels));
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
        Render();
    }

    private void PassTime(float hours)
    {
        Home.AdvanceHours(hours);
        _house.TimePassed();

        // A barra de dev mostra o relógio e as necessidades, que estão fora do painel.
        Render();
    }

    private void ToggleSkin()
    {
        _skin = _skin == PanelSkin.Dark ? PanelSkin.Light : PanelSkin.Dark;
        Render();
    }

    private void ToggleRegionLabels()
    {
        _showRegionLabels = !_showRegionLabels;
        Render();
    }
}
