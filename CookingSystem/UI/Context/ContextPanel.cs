using System;
using Godot;

namespace LifeSim.Ui;

/// <summary>
/// O shell. Um painel para todas as interações do jogo: ele lê uma <see cref="PanelContext"/>,
/// liga cada primitivo à sua região e se dimensiona pela largura declarada. Cozinhar,
/// comprar, construir e transferir são definições diferentes — não janelas diferentes.
///
/// O shell não guarda estado: a definição é reconstruída inteira a cada mudança, e é o dono
/// do contexto (a <c>CookingSession</c>, por exemplo) que sabe o que mudou. Não existe
/// caminho que atualize um pedaço da tela sem passar por <see cref="Rebuild"/> — é isso
/// que garante que a tela nunca discorde do estado que a gerou.
/// </summary>
public partial class ContextPanel : PanelContainer
{
    private const int LeftColumnWidth = 244;
    private const int RightColumnWidth = 332;
    private const int ColumnPadding = 14;

    /// <summary>De onde vem a definição. Chamada a cada redesenho, nunca guardada.</summary>
    public Func<PanelContext> Definition { get; init; }

    /// <summary>Pele em uso. Trocar exige <see cref="Rebuild"/>.</summary>
    public PanelSkin Skin { get; set; }

    /// <summary>Etiqueta o nome de cada região sobre o painel. Ferramenta de autoria.</summary>
    public bool ShowRegionLabels { get; set; }

    public override void _Ready()
    {
        if (Definition is null || Skin is null)
            throw new InvalidOperationException("ContextPanel exige Definition e Skin.");

        Rebuild();
    }

    public void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        var context = Definition();

        // O contexto declara a largura e o painel obedece encolhendo até ela, centralizado:
        // uma tela de transferência precisa de mais espaço que um menu de pratos.
        CustomMinimumSize = new Vector2(context.Width, 0);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        AddThemeStyleboxOverride("panel", Frame());

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        AddChild(column);

        column.AddChild(Header(context));
        column.AddChild(Body(context));
    }

    // ------------------------------------------------------------------
    // Moldura
    // ------------------------------------------------------------------

    private StyleBoxFlat Frame()
    {
        var box = new StyleBoxFlat { BgColor = Skin.Panel, BorderColor = Skin.Line };
        box.SetBorderWidthAll(2);
        return box;
    }

    private Control Header(PanelContext context)
    {
        var bar = new PanelContainer();
        var style = new StyleBoxFlat
        {
            BgColor = Skin.PanelAlt,
            BorderColor = Skin.Line,
            BorderWidthBottom = 2,
            ContentMarginLeft = ColumnPadding,
            ContentMarginRight = ColumnPadding,
            ContentMarginTop = 11,
            ContentMarginBottom = 11,
        };
        bar.AddThemeStyleboxOverride("panel", style);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(AccentMark());
        row.AddChild(PanelPrimitives.Text(context.Title, 15, Skin.Ink));

        var crumb = PanelPrimitives.Text(context.Crumb, 12, Skin.Mute);
        crumb.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(crumb);

        if (context.OnClose is not null)
            row.AddChild(CloseButton(context.OnClose));

        bar.AddChild(row);
        return bar;
    }

    private Control AccentMark()
    {
        var mark = new PanelContainer
        {
            CustomMinimumSize = new Vector2(12, 12),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        mark.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = Skin.Accent });
        return mark;
    }

    private Button CloseButton(Action onClose)
    {
        var button = new Button { Text = "✕", CustomMinimumSize = new Vector2(26, 22) };
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeFontSizeOverride("font_size", 13);
        button.AddThemeColorOverride("font_color", Skin.Mute);
        button.AddThemeColorOverride("font_hover_color", Skin.Accent);
        button.Pressed += () => onClose();
        return button;
    }

    private Control Body(PanelContext context)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);

        row.AddChild(LeftColumn(context));
        row.AddChild(MiddleColumn(context));
        row.AddChild(RightColumn(context));
        return row;
    }

    private Control LeftColumn(PanelContext context)
    {
        var column = Column(LeftColumnWidth, Skin.Panel, borderRight: 2, separation: 18);
        AddRegion(column, "SUBJECT", context.Subject);
        AddRegion(column, "ACTIONS", context.Actions);
        return column.Frame;
    }

    private Control MiddleColumn(PanelContext context)
    {
        var column = Column(0, Skin.Panel, borderRight: 2, separation: 16);
        column.Frame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddRegion(column, "PRIMARY", context.Primary);
        AddRegion(column, "SECONDARY", context.Secondary);
        return column.Frame;
    }

    private Control RightColumn(PanelContext context)
    {
        var column = Column(RightColumnWidth, Skin.PanelAlt, borderRight: 0, separation: 16);

        column.Stack.AddChild(FixedRegion("PREVIEW", context.Preview.Title,
            PanelPrimitives.BuildPreview(context.Preview, Skin)));
        column.Stack.AddChild(FixedRegion("READOUT", context.Readout.Title,
            PanelPrimitives.BuildReadout(context.Readout, Skin)));

        // O commit desce para o rodapé: a ação final fica no mesmo lugar em todo contexto.
        column.Stack.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        column.Stack.AddChild(CommitRegion(context.Commit));
        return column.Frame;
    }

    // ------------------------------------------------------------------
    // Regiões
    // ------------------------------------------------------------------

    private (PanelContainer Frame, VBoxContainer Stack) Column(
        int width, Color background, int borderRight, int separation)
    {
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(width, 0) };
        frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = Skin.Line,
            BorderWidthRight = borderRight,
            ContentMarginLeft = ColumnPadding,
            ContentMarginRight = ColumnPadding,
            ContentMarginTop = ColumnPadding,
            ContentMarginBottom = ColumnPadding,
        });

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", separation);
        frame.AddChild(stack);
        return (frame, stack);
    }

    /// <summary>Região não preenchida simplesmente não existe na tela — sem espaço reservado.</summary>
    private void AddRegion((PanelContainer Frame, VBoxContainer Stack) column, string label, PanelRegion region)
    {
        if (region is null)
            return;

        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 9);
        block.AddChild(RegionHeader(label, region.Title, region.Count));
        block.AddChild(PanelPrimitives.Build(region.Body, Skin));
        column.Stack.AddChild(block);
    }

    private Control FixedRegion(string label, string title, Control body)
    {
        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 9);
        block.AddChild(RegionHeader(label, title, ""));
        block.AddChild(body);
        return block;
    }

    private Control CommitRegion(CommitAction commit)
    {
        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 7);

        if (ShowRegionLabels)
        {
            var badges = new HBoxContainer();
            badges.AddChild(Badge("COMMIT"));
            block.AddChild(badges);
        }

        block.AddChild(PanelPrimitives.BuildCommit(commit, Skin));
        return block;
    }

    private Control RegionHeader(string label, string title, string count)
    {
        var underline = new PanelContainer();
        underline.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0),
            BorderColor = Skin.Line,
            BorderWidthBottom = 2,
            ContentMarginBottom = 6,
        });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        if (ShowRegionLabels)
            row.AddChild(Badge(label));

        var name = PanelPrimitives.Text(title, 10, Skin.Dim);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(name);

        if (count.Length > 0)
            row.AddChild(PanelPrimitives.Text(count, 10, Skin.Mute));

        underline.AddChild(row);
        return underline;
    }

    private Control Badge(string label)
    {
        var badge = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        badge.AddThemeStyleboxOverride("panel", PanelPrimitives.Box(Skin.Accent, Skin.Accent, 5, 3));
        badge.AddChild(PanelPrimitives.Text(label, 9, Skin.AccentInk));
        return badge;
    }
}
