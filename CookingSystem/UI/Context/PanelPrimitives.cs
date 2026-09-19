using System;
using Godot;

namespace LifeSim.Ui;

/// <summary>
/// Desenha os primitivos. Cada função aqui transforma um pedaço de definição em nós do
/// Godot e nada mais: não lê estado, não decide regra, não guarda referência. Adicionar
/// um sistema ao jogo não deveria exigir tocar neste arquivo — só adicionar um primitivo
/// deveria.
/// </summary>
public static class PanelPrimitives
{
    private const int TileMinWidth = 112;
    private const int SwatchSize = 30;
    private const int ArtSize = 96;

    public static Control Build(RegionBody body, PanelSkin skin) => body switch
    {
        Picker picker => BuildPicker(picker, skin),
        VerbList verbs => BuildVerbs(verbs, skin),
        SlotGrid grid => BuildSlotGrid(grid, skin),
        Checklist checklist => BuildChecklist(checklist, skin),
        _ => throw new ArgumentOutOfRangeException(
            nameof(body), $"Primitivo sem desenho: {body?.GetType().Name ?? "null"}."),
    };

    // ------------------------------------------------------------------
    // Primitivos de região
    // ------------------------------------------------------------------

    private static Control BuildPicker(Picker picker, PanelSkin skin)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 5);
        grid.AddThemeConstantOverride("v_separation", 5);
        column.AddChild(grid);

        foreach (var option in picker.Options)
        {
            var button = FlatButton(skin,
                option.Selected ? skin.AccentDeep : skin.Cell,
                option.Selected ? skin.Accent : skin.LineSoft);
            button.CustomMinimumSize = new Vector2(0, 62);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var captured = option;
            button.Pressed += () => captured.OnPick();

            var content = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            content.AddThemeConstantOverride("separation", 5);
            content.AddChild(Swatch(option.Icon, option.Tint, 0, 22));
            content.AddChild(Text(option.Name, 11, option.Selected ? skin.Ink : skin.Dim));
            Fill(button, content, inset: 5);

            // Opacidade separa o escolhido dos demais sem gastar uma segunda cor.
            button.Modulate = option.Selected ? Colors.White : new Color(1, 1, 1, 0.55f);
            grid.AddChild(button);
        }

        if (picker.Note.Length > 0)
            column.AddChild(Wrapped(picker.Note, 11, skin.Mute));

        return column;
    }

    private static Control BuildVerbs(VerbList verbs, PanelSkin skin)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);

        foreach (var verb in verbs.Verbs)
        {
            var button = FlatButton(skin,
                verb.Active ? skin.AccentDeep : skin.Cell,
                verb.Active ? skin.Accent : skin.LineSoft);
            button.Disabled = verb.Locked;
            button.CustomMinimumSize = new Vector2(0, 58);
            var captured = verb;
            button.Pressed += () => captured.OnUse();

            var content = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            content.AddThemeConstantOverride("separation", 3);

            var head = new HBoxContainer();
            head.AddThemeConstantOverride("separation", 7);
            head.AddChild(Swatch(null, verb.Locked ? skin.LineSoft : verb.Active ? skin.Accent : skin.Mute, 8, 8));
            head.AddChild(Expanding(Text(verb.Name, 12, verb.Locked ? skin.Mute : skin.Ink)));
            head.AddChild(Text(verb.Skill, 9, skin.Mute));
            content.AddChild(head);

            // A nota quebra linha: o conteúdo vai ancorado por cima do botão, então um rótulo
            // sem quebra não é apertado por ninguém — ele simplesmente vaza para fora da coluna.
            content.AddChild(Wrapped(verb.Note, 10, skin.Mute));

            Fill(button, content, inset: 9);
            button.Modulate = verb.Locked ? new Color(1, 1, 1, 0.5f) : Colors.White;
            column.AddChild(button);
        }

        return column;
    }

    private static Control BuildSlotGrid(SlotGrid slotGrid, PanelSkin skin)
    {
        var grid = new GridContainer { Columns = slotGrid.Columns };
        grid.AddThemeConstantOverride("h_separation", 5);
        grid.AddThemeConstantOverride("v_separation", 5);

        foreach (var slot in slotGrid.Slots)
            grid.AddChild(slotGrid.Mode == SlotGridMode.Quantity
                ? QuantityTile(slot, skin)
                : SelectTile(slot, skin));

        return grid;
    }

    /// <summary>
    /// Ladrilho com steppers. A quantidade é a decisão do jogador, então ela custa um
    /// clique por unidade e não um arrasto — e o ladrilho inteiro não é clicável, para
    /// que os únicos alvos sejam o + e o −.
    /// </summary>
    private static Control QuantityTile(PanelSlot slot, PanelSkin skin)
    {
        bool inDish = slot.Quantity > 0;

        var tile = new PanelContainer
        {
            CustomMinimumSize = new Vector2(TileMinWidth, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        tile.AddThemeStyleboxOverride("panel", Box(
            inDish ? skin.AccentDeep : skin.Cell,
            inDish ? skin.Accent : skin.LineSoft, 7, 6));
        tile.Modulate = slot.Enabled || inDish ? Colors.White : new Color(1, 1, 1, 0.4f);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 5);
        tile.AddChild(column);

        column.AddChild(TileHead(slot, skin, inDish ? slot.Quantity.ToString() : "–",
                                inDish ? skin.Accent : skin.Mute));

        var steppers = new HBoxContainer();
        steppers.AddThemeConstantOverride("separation", 3);

        var remove = StepButton("−", skin, inDish ? skin.Ink : skin.Mute);
        remove.Disabled = !inDish;
        remove.Pressed += () => slot.OnRemove();
        steppers.AddChild(remove);

        var add = StepButton("+", skin, slot.Enabled ? skin.Ink : skin.Mute);
        add.Disabled = !slot.Enabled;
        add.Pressed += () => slot.OnAdd();
        steppers.AddChild(add);

        column.AddChild(steppers);
        return tile;
    }

    /// <summary>Mesmo ladrilho, escolha única: sem steppers, com marca no escolhido.</summary>
    private static Control SelectTile(PanelSlot slot, PanelSkin skin)
    {
        var button = FlatButton(skin,
            slot.Selected ? skin.AccentDeep : skin.Cell,
            slot.Selected ? skin.Accent : skin.LineSoft);
        button.Disabled = !slot.Enabled;
        button.CustomMinimumSize = new Vector2(TileMinWidth, 54);
        button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        button.Pressed += () => slot.OnPick();

        Fill(button, TileHead(slot, skin, slot.Selected ? "▸" : "", skin.Accent), inset: 7);
        return button;
    }

    /// <summary>A linha de cima de qualquer ladrilho: amostra, nome, apoio e o número.</summary>
    private static Control TileHead(PanelSlot slot, PanelSkin skin, string mark, Color markColor)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 7);

        row.AddChild(Swatch(slot.Icon, slot.Tint, SwatchSize, SwatchSize));

        var names = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        names.AddThemeConstantOverride("separation", 1);
        names.AddChild(Clipped(slot.Name, 11, skin.Ink));
        names.AddChild(Clipped(slot.Sub, 10, slot.Warn ? skin.Accent : skin.Mute));
        row.AddChild(names);

        if (mark.Length > 0)
            row.AddChild(Text(mark, 14, markColor));

        return row;
    }

    private static Control BuildChecklist(Checklist checklist, PanelSkin skin)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 1);

        foreach (var entry in checklist.Rows)
        {
            var line = new PanelContainer();
            line.AddThemeStyleboxOverride("panel", Box(skin.Cell, skin.LineSoft, 10, 8));

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            row.AddChild(Swatch(null, entry.Met ? skin.Dim : skin.Accent, 9, 9));
            row.AddChild(Swatch(entry.Icon, entry.Tint, 22, 22));
            row.AddChild(Expanding(Clipped(entry.Name, 12, skin.Ink)));
            row.AddChild(Text(entry.Note, 10, skin.Mute));
            row.AddChild(Text(entry.Tally, 12, entry.Met ? skin.Ink : skin.Accent));

            line.AddChild(row);
            column.AddChild(line);
        }

        return column;
    }

    // ------------------------------------------------------------------
    // Regiões de resultado
    // ------------------------------------------------------------------

    public static Control BuildPreview(PreviewCard preview, PanelSkin skin)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 11);
        row.AddChild(Swatch(preview.Art, preview.Tint, ArtSize, ArtSize, skin.Line));

        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 4);
        column.AddChild(Wrapped(preview.Name, 16, skin.Ink));
        column.AddChild(Wrapped(preview.Description, 11, skin.Dim));

        var tags = new HFlowContainer();
        tags.AddThemeConstantOverride("h_separation", 4);
        tags.AddThemeConstantOverride("v_separation", 4);
        foreach (var tag in preview.Tags)
        {
            var chip = new PanelContainer();
            chip.AddThemeStyleboxOverride("panel", Box(skin.Panel, skin.LineSoft, 6, 4));
            chip.AddChild(Text(tag, 9, skin.Dim));
            tags.AddChild(chip);
        }
        column.AddChild(tags);

        row.AddChild(column);
        return row;
    }

    public static Control BuildReadout(Readout readout, PanelSkin skin)
    {
        var color = skin.Scale(readout.Fill);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);

        var value = new VBoxContainer();
        value.AddThemeConstantOverride("separation", 5);

        var head = new HBoxContainer();
        head.AddChild(Expanding(Text(readout.Headline, 13, skin.Ink)));
        head.AddChild(Text(readout.Value, 24, color));
        value.AddChild(head);

        value.AddChild(Bar(readout.Fill, color, skin, height: 6));
        value.AddChild(Wrapped(readout.Caption, 11, skin.Mute));
        column.AddChild(value);

        if (readout.Factors.Count > 0)
        {
            var factors = new VBoxContainer();
            factors.AddThemeConstantOverride("separation", 4);
            foreach (var factor in readout.Factors)
            {
                var line = new HBoxContainer();
                line.AddThemeConstantOverride("separation", 8);

                var name = Text(factor.Name, 11, skin.Mute);
                name.CustomMinimumSize = new Vector2(74, 0);
                line.AddChild(name);

                var bar = Bar(factor.Value, skin.Scale(factor.Value), skin, height: 3);
                bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                line.AddChild(bar);

                var pct = Text($"{factor.Value:P0}", 10, skin.Scale(factor.Value));
                pct.CustomMinimumSize = new Vector2(34, 0);
                pct.HorizontalAlignment = HorizontalAlignment.Right;
                line.AddChild(pct);

                factors.AddChild(line);
            }
            column.AddChild(factors);
        }

        if (readout.Stats.Count > 0)
        {
            var grid = new GridContainer { Columns = 3 };
            grid.AddThemeConstantOverride("h_separation", 1);
            grid.AddThemeConstantOverride("v_separation", 1);
            foreach (var stat in readout.Stats)
            {
                var cell = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                cell.AddThemeStyleboxOverride("panel", Box(skin.Cell, skin.Cell, 6, 5));

                var inner = new VBoxContainer();
                inner.AddThemeConstantOverride("separation", 2);
                inner.AddChild(Clipped(stat.Key, 9, skin.Mute));
                inner.AddChild(Clipped(stat.Value, 14, skin.Of(stat.Tone)));

                cell.AddChild(inner);
                grid.AddChild(cell);
            }
            column.AddChild(grid);
        }

        return column;
    }

    public static Control BuildCommit(CommitAction commit, PanelSkin skin)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 7);

        var button = FlatButton(skin,
            commit.Enabled ? skin.Accent : skin.Cell,
            commit.Enabled ? skin.Accent : skin.LineSoft);
        button.Disabled = !commit.Enabled;
        button.CustomMinimumSize = new Vector2(0, 48);
        button.Pressed += () => commit.OnRun();

        var content = new HBoxContainer();
        content.AddChild(Expanding(Text(commit.Label, 16, commit.Enabled ? skin.AccentInk : skin.Mute)));
        content.AddChild(Text(commit.Meta, 12, commit.Enabled ? skin.AccentInk : skin.Mute));
        Fill(button, content, inset: 14);

        column.AddChild(button);
        column.AddChild(Wrapped(commit.Hint, 11, commit.Enabled ? skin.Mute : skin.Accent));
        return column;
    }

    // ------------------------------------------------------------------
    // Peças compartilhadas
    // ------------------------------------------------------------------

    public static Label Text(string text, int size, Color color)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    /// <summary>Texto que quebra linha. Para prosa: nota, legenda, descrição.</summary>
    public static Label Wrapped(string text, int size, Color color)
    {
        var label = Text(text, size, color);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    /// <summary>Texto que nunca quebra: corta com reticências. Para nome dentro de ladrilho.</summary>
    private static Label Clipped(string text, int size, Color color)
    {
        var label = Text(text, size, color);
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.CustomMinimumSize = new Vector2(24, 0);
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return label;
    }

    private static Control Expanding(Control control)
    {
        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return control;
    }

    public static StyleBoxFlat Box(Color background, Color border, int marginX = 0, int marginY = 0)
    {
        // Cantos retos e borda de 1px em tudo: a hierarquia do painel vem da cor e das
        // divisórias de 2px, não de arredondamento.
        var box = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            ContentMarginLeft = marginX,
            ContentMarginRight = marginX,
            ContentMarginTop = marginY,
            ContentMarginBottom = marginY,
        };
        box.SetBorderWidthAll(1);
        return box;
    }

    /// <summary>O botão do painel: retângulo chapado, borda de 1px, acento só quando age.</summary>
    public static Button FlatButton(PanelSkin skin, Color background, Color border)
    {
        var button = new Button { Alignment = HorizontalAlignment.Left };
        button.AddThemeStyleboxOverride("normal", Box(background, border));
        button.AddThemeStyleboxOverride("hover", Box(background, skin.Accent));
        button.AddThemeStyleboxOverride("pressed", Box(skin.AccentDeep, skin.Accent));
        button.AddThemeStyleboxOverride("disabled", Box(skin.Cell, skin.LineSoft));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", skin.Ink);
        button.AddThemeColorOverride("font_hover_color", skin.Ink);
        button.AddThemeColorOverride("font_pressed_color", skin.Ink);
        button.AddThemeColorOverride("font_disabled_color", skin.Mute);
        return button;
    }

    private static Button StepButton(string text, PanelSkin skin, Color color)
    {
        var button = FlatButton(skin, skin.PanelAlt, skin.LineSoft);
        button.Text = text;
        button.Alignment = HorizontalAlignment.Center;
        button.CustomMinimumSize = new Vector2(0, 20);
        button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        button.AddThemeFontSizeOverride("font_size", 12);
        button.AddThemeColorOverride("font_color", color);
        return button;
    }

    /// <summary>
    /// O Button não compõe filhos com layout próprio, então a linha rica vai por cima dele
    /// com o mouse ignorando cliques — quem recebe o input continua sendo o botão.
    /// </summary>
    private static void Fill(Button button, Control content, int inset)
    {
        content.MouseFilter = Control.MouseFilterEnum.Ignore;
        content.AnchorRight = 1;
        content.AnchorBottom = 1;
        content.OffsetLeft = inset;
        content.OffsetRight = -inset;
        button.AddChild(content);
    }

    /// <summary>
    /// Retângulo de cor com o ícone por cima. É um caminho só para os dois casos: quando
    /// não há arte ainda, sobra a cor do item; quando há, ela entra sem mudar o layout.
    /// </summary>
    private static Control Swatch(Texture2D icon, Color tint, int width, int height, Color? border = null)
    {
        var box = new PanelContainer
        {
            CustomMinimumSize = new Vector2(width, height),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        box.AddThemeStyleboxOverride("panel", border is null
            ? new StyleBoxFlat { BgColor = tint }
            : Box(tint, border.Value));

        box.AddChild(new TextureRect
        {
            Texture = icon,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        });
        return box;
    }

    private static ProgressBar Bar(float value, Color color, PanelSkin skin, int height)
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = value,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, height),
        };
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = skin.Cell });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = color });
        return bar;
    }
}
