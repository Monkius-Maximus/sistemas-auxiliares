using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Ordem de travessia por região. Preview não entra: não tem nada focável dentro.
    /// </summary>
    private static readonly PanelRegionId[] Traversal =
    {
        PanelRegionId.Subject, PanelRegionId.Actions, PanelRegionId.Primary,
        PanelRegionId.Secondary, PanelRegionId.Commit,
    };

    private readonly PanelFocus _focus = new();
    private readonly HoldRepeat _repeat = new();
    private PromptBar _prompts;

    public override void _Ready()
    {
        if (Definition is null || Skin is null)
            throw new InvalidOperationException("ContextPanel exige Definition e Skin.");

        Rebuild();

        // Foco autorado: o contexto diz onde ele começa. Deixar o Godot escolher põe o
        // jogador de controle no primeiro nó da árvore, que raramente é onde ele quer.
        FocusRegion(Definition().FocusEntry);
    }

    public void Rebuild()
    {
        // Reconstruir joga fora o nó que tinha o foco. A posição (região, índice) sobrevive
        // em PanelFocus, então o foco volta para o mesmo lugar — sem isso, cada unidade
        // somada a um ingrediente devolveria o jogador de controle ao começo do painel.
        bool hadFocus = GetViewport()?.GuiGetFocusOwner() is Control owner && IsAncestorOf(owner);

        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _focus.BeginRebuild();
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

        _prompts = new PromptBar();
        column.AddChild(_prompts);
        RefreshPrompts(context);

        if (hadFocus)
            _focus.Restore(_focus.Region);
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
        AddRegion(column, PanelRegionId.Subject, context.Subject);
        AddRegion(column, PanelRegionId.Actions, context.Actions);
        return column.Frame;
    }

    private Control MiddleColumn(PanelContext context)
    {
        var column = Column(0, Skin.Panel, borderRight: 2, separation: 16);
        column.Frame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddRegion(column, PanelRegionId.Primary, context.Primary);
        AddRegion(column, PanelRegionId.Secondary, context.Secondary);
        return column.Frame;
    }

    private Control RightColumn(PanelContext context)
    {
        var column = Column(RightColumnWidth, Skin.PanelAlt, borderRight: 0, separation: 16);

        column.Stack.AddChild(FixedRegion(PanelRegionId.Preview, context.Preview.Title,
            PanelPrimitives.BuildPreview(context.Preview, Skin)));
        column.Stack.AddChild(FixedRegion(PanelRegionId.Readout, context.Readout.Title,
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
    private void AddRegion((PanelContainer Frame, VBoxContainer Stack) column, PanelRegionId id, PanelRegion region)
    {
        if (region is null)
            return;

        _focus.Building = id;

        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 9);
        block.AddChild(RegionHeader(id, region.Title, region.Count));
        block.AddChild(PanelPrimitives.Build(region.Body, Skin, _focus));
        column.Stack.AddChild(block);
    }

    private Control FixedRegion(PanelRegionId id, string title, Control body)
    {
        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 9);
        block.AddChild(RegionHeader(id, title, ""));
        block.AddChild(body);
        return block;
    }

    private Control CommitRegion(CommitAction commit)
    {
        _focus.Building = PanelRegionId.Commit;

        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 7);

        if (ShowRegionLabels)
        {
            var badges = new HBoxContainer();
            badges.AddChild(Badge(PanelRegionId.Commit));
            block.AddChild(badges);
        }

        block.AddChild(PanelPrimitives.BuildCommit(commit, Skin, _focus));
        return block;
    }

    private Control RegionHeader(PanelRegionId id, string title, string count)
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
            row.AddChild(Badge(id));

        var name = PanelPrimitives.Text(title, 10, Skin.Dim);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(name);

        if (count.Length > 0)
            row.AddChild(PanelPrimitives.Text(count, 10, Skin.Mute));

        underline.AddChild(row);
        return underline;
    }

    private Control Badge(PanelRegionId id)
    {
        var badge = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        badge.AddThemeStyleboxOverride("panel", PanelPrimitives.Box(Skin.Accent, Skin.Accent, 5, 3));
        badge.AddChild(PanelPrimitives.Text(id.ToString().ToUpperInvariant(), 9, Skin.AccentInk));
        return badge;
    }

    // ------------------------------------------------------------------
    // Navegação
    // ------------------------------------------------------------------

    /// <summary>
    /// A unidade de travessia é a região, não o controle. Andar célula a célula pelo painel
    /// inteiro obriga o jogador a atravessar oito ingredientes para chegar aos temperos;
    /// LB/RB pulam o bloco inteiro, que é como ele lê a tela.
    /// </summary>
    private void JumpRegion(int direction)
    {
        int from = Array.IndexOf(Traversal, _focus.Region);
        if (from < 0) from = 0;

        for (int step = 1; step <= Traversal.Length; step++)
        {
            var next = Traversal[((from + direction * step) % Traversal.Length + Traversal.Length) % Traversal.Length];
            if (!_focus.HasCells(next)) continue;

            FocusRegion(next);
            return;
        }
    }

    private void FocusRegion(PanelRegionId region)
    {
        if (!_focus.Restore(region)) return;
        RefreshPrompts(Definition());
    }

    /// <summary>
    /// A legenda mostra a região focada. Ela é derivada da definição corrente, nunca
    /// guardada: se a definição mudar de primitivo, o rodapé muda junto sem ninguém avisar.
    /// </summary>
    private void RefreshPrompts(PanelContext context)
    {
        if (_prompts is null) return;

        var body = _focus.Region switch
        {
            PanelRegionId.Subject => context.Subject?.Body,
            PanelRegionId.Actions => context.Actions?.Body,
            PanelRegionId.Primary => context.Primary?.Body,
            PanelRegionId.Secondary => context.Secondary?.Body,
            _ => null,
        };

        var prompts = new List<Prompt>(PanelPrompts.For(body));
        prompts.Add(new Prompt("LB / RB", "Tab", "Trocar de região"));
        prompts.Add(new Prompt("Y", "Enter", "Confirmar"));
        if (context.OnClose is not null)
            prompts.Add(new Prompt("B", "Esc", "Fechar"));

        _prompts.Bind(prompts, Skin);
    }

    // ------------------------------------------------------------------
    // Entrada
    // ------------------------------------------------------------------

    public override void _Input(InputEvent @event)
    {
        // O rodapé fala a língua do último dispositivo tocado, não a do que está plugado.
        if (@event is InputEventJoypadButton or InputEventJoypadMotion)
            _prompts?.UseGamepad(true);
        else if (@event is InputEventKey or InputEventMouseButton)
            _prompts?.UseGamepad(false);

        // A troca de região é interceptada antes da GUI porque o Godot também usa Tab, para
        // andar célula a célula. As duas travessias não podem coexistir na mesma tecla, e a
        // que o jogador entende é a por região.
        if (@event.IsActionPressed("panel_region_next")) { JumpRegion(1); AcceptInput(); }
        else if (@event.IsActionPressed("panel_region_prev")) { JumpRegion(-1); AcceptInput(); }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Confirmar chega aqui só quando nenhum botão focado consumiu o Enter — que é o
        // comportamento certo: com o foco num botão, Enter aciona o botão.
        if (@event.IsActionPressed("panel_commit")) { RunCommit(); AcceptInput(); }
        else if (@event.IsActionPressed("panel_close")) { RunClose(); AcceptInput(); }
        else if (@event.IsActionPressed("panel_increment")) { StartRepeat(1); AcceptInput(); }
        else if (@event.IsActionPressed("panel_decrement")) { StartRepeat(-1); AcceptInput(); }
        else if (@event.IsActionReleased("panel_increment") || @event.IsActionReleased("panel_decrement"))
            _repeat.End();
    }

    private void AcceptInput() => GetViewport().SetInputAsHandled();

    /// <summary>
    /// Confirmar e fechar ficam fora do grafo de foco: são as duas coisas que o jogador
    /// precisa alcançar de qualquer lugar do painel, sem navegar até elas.
    /// </summary>
    private void RunCommit()
    {
        var commit = Definition().Commit;
        if (commit.Enabled) commit.OnRun();
    }

    private void RunClose() => Definition().OnClose?.Invoke();

    // ------------------------------------------------------------------
    // Segurar para repetir
    // ------------------------------------------------------------------

    private void StartRepeat(int direction)
    {
        _repeat.Begin(direction);
        Step(direction);
    }

    public override void _Process(double delta)
    {
        if (!_repeat.Active) return;

        for (int i = 0; i < _repeat.Advance(delta); i++)
            Step(_repeat.Direction);
    }

    /// <summary>
    /// Quem repete é o painel, não a célula: somar uma unidade reconstrói a definição
    /// inteira e destrói o nó focado, então a repetição resolve a célula de novo a cada
    /// passo — e para sozinha quando a sessão deixa de aceitar.
    /// </summary>
    private void Step(int direction)
    {
        if (_focus.Current() is not IPanelCell cell)
        {
            _repeat.End();
            return;
        }

        bool allowed = direction > 0 ? cell.CanIncrement : cell.CanDecrement;
        if (!allowed)
        {
            _repeat.End();
            return;
        }

        if (direction > 0) cell.Increment();
        else cell.Decrement();
    }
}
