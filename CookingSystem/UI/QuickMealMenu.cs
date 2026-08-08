using System;
using System.Collections.Generic;
using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// O menu contextual que abre ao clicar no fogão: nome, custo e qualidade prevista.
/// Um clique resolve — o painel completo fica atrás da última linha, para quem quiser.
///
/// Esta é a porta de entrada padrão. O painel manual é a exceção, não o contrário.
/// </summary>
public partial class QuickMealMenu : Control
{
    public IReadOnlyList<QuickMealOption> Options { get; init; }
    public Action<QuickMealOption> OnChoose { get; init; }
    public Action OnManual { get; init; }

    public override void _Ready()
    {
        if (Options is null || OnChoose is null || OnManual is null)
            throw new InvalidOperationException("QuickMealMenu não foi configurado.");

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(400, 0) };
        var style = new StyleBoxFlat
        {
            BgColor = new Color("1e2124"), BorderColor = new Color("33373b"),
            ContentMarginLeft = 10, ContentMarginRight = 10,
            ContentMarginTop = 10, ContentMarginBottom = 10,
            CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
        };
        style.SetBorderWidthAll(1);
        panel.AddThemeStyleboxOverride("panel", style);
        AddChild(panel);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 3);
        panel.AddChild(column);

        var title = new Label { Text = "O que preparar?" };
        title.AddThemeFontSizeOverride("font_size", 16);
        column.AddChild(title);
        column.AddChild(new HSeparator());

        foreach (var option in Options)
            column.AddChild(Row(option));

        column.AddChild(new HSeparator());

        var manual = new Button { Text = "Preparar manualmente…", Alignment = HorizontalAlignment.Left };
        manual.Pressed += () => OnManual();
        column.AddChild(manual);
    }

    private Control Row(QuickMealOption option)
    {
        var button = new Button
        {
            Disabled = !option.CanMake,
            CustomMinimumSize = new Vector2(0, 42),
            Alignment = HorizontalAlignment.Left,
        };
        button.Pressed += () => OnChoose(option);

        // O Button não compõe filhos com layout próprio, então a linha rica vai por cima dele
        // com mouse_filter ignorando cliques — o botão continua sendo quem recebe o input.
        var row = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorRight = 1, AnchorBottom = 1,
            OffsetLeft = 10, OffsetRight = -10,
        };
        button.AddChild(row);

        var name = new Label
        {
            Text = option.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.AddChild(name);

        var detail = new Label { VerticalAlignment = VerticalAlignment.Center };
        detail.AddThemeFontSizeOverride("font_size", 11);
        if (option.CanMake)
        {
            detail.Text = $"{option.Preview.Quality:P0}   ${option.Cost}";
            detail.AddThemeColorOverride("font_color", QualityColor(option.Preview.Quality));
        }
        else
        {
            detail.Text = option.Blocker;
            detail.AddThemeColorOverride("font_color", new Color("8b9196"));
        }
        row.AddChild(detail);

        return button;
    }

    private static Color QualityColor(float quality) => quality switch
    {
        < 0.35f => new Color("c0504d"),
        < 0.70f => new Color("d6a13a"),
        _ => new Color("6aa84f"),
    };
}
