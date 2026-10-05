using System;
using Godot;
using ContextUi;

namespace LifeSim.Cooking;

/// <summary>
/// A primeira tela. Três saídas: jogo novo, a bancada de testes (o fogão em tela cheia, para
/// mexer em conteúdo e balanceamento) e sair.
///
/// Fundo e logotipo vêm de <c>res://Art/Menu/</c> quando existirem; até lá, cor da pele e texto.
/// </summary>
public partial class MainMenu : Control
{
    private readonly PanelSkin _skin = PanelSkin.Dark;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(Background());

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
        column.AddThemeConstantOverride("separation", 10);
        center.AddChild(column);

        column.AddChild(Logo());
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 24) });

        var play = MenuButton("Novo jogo", "A casa da Ana, começando às 8h do dia 1.",
            () => SceneTransition.Go(this, GameScenes.Game));
        column.AddChild(play);
        column.AddChild(MenuButton("Bancada de testes", "O fogão em tela cheia, com relógio na mão. Ferramenta de dev.",
            () => SceneTransition.Go(this, GameScenes.Workbench)));
        column.AddChild(MenuButton("Sair", "", () => GetTree().Quit()));

        // Foco no primeiro botão: com controle, o jogador precisa de um lugar para começar.
        play.CallDeferred(Control.MethodName.GrabFocus);

        var version = PanelPrimitives.Text("Protótipo · sistema de cozinha", 10, _skin.Mute);
        version.SetAnchorsPreset(LayoutPreset.BottomLeft);
        version.Position = new Vector2(24, -36);
        AddChild(version);
    }

    private Control Background()
    {
        var art = ArtLibrary.Find(ArtLibrary.Menu, "fundo");
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

    private Control Logo()
    {
        var art = ArtLibrary.Find(ArtLibrary.Menu, "logo");
        if (art is not null)
            return new TextureRect
            {
                Texture = art,
                CustomMinimumSize = new Vector2(380, 160),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            };

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        column.AddChild(PanelPrimitives.Text("Vida em Casa", 34, _skin.Ink));
        column.AddChild(PanelPrimitives.Text("cozinhar · comer · comprar", 13, _skin.Accent));
        return column;
    }

    private Button MenuButton(string text, string note, Action onPress)
    {
        var button = PanelPrimitives.FlatButton(_skin, _skin.Panel, _skin.Line);
        button.CustomMinimumSize = new Vector2(380, note.Length > 0 ? 58 : 44);

        var content = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        content.AddThemeConstantOverride("separation", 2);
        content.AddChild(PanelPrimitives.Text(text, 15, _skin.Ink));
        if (note.Length > 0)
            content.AddChild(PanelPrimitives.Text(note, 10, _skin.Mute));
        PanelPrimitives.Fill(button, content, inset: 14);

        button.Pressed += () => onPress();
        return button;
    }
}
