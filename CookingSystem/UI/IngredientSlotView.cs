using System;
using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// Uma célula da grade. Mostra um ingrediente da despensa e quanto dele já foi para o
/// prato. Manipulação direta por +/-, não drag-and-drop: a quantidade é a decisão,
/// então ela precisa custar um clique, não um arrasto.
///
/// A view não decide nada — recebe callbacks e é redesenhada por <see cref="Refresh"/>.
/// </summary>
public partial class IngredientSlotView : PanelContainer
{
    private const int IconSize = 44;

    // Godot exige construtor sem parâmetros em nós usados como script, então a injeção
    // acontece por inicializador de objeto. Continua sendo um único caminho de construção:
    // _Ready() lança se algo não foi preenchido.
    public IngredientDef Def { get; init; }
    public Action OnAdd { get; init; }
    public Action OnRemove { get; init; }

    private IngredientDef _def;
    private Label _unitsLabel;
    private Label _stockLabel;
    private Button _addButton;
    private Button _removeButton;
    private StyleBoxFlat _style;

    public override void _Ready()
    {
        _def = Def ?? throw new InvalidOperationException($"{nameof(Def)} não foi definido.");
        if (OnAdd is null || OnRemove is null)
            throw new InvalidOperationException("Callbacks do slot não foram definidos.");

        CustomMinimumSize = new Vector2(78, 108);

        _style = new StyleBoxFlat
        {
            BgColor = new Color("2b2b2b"),
            BorderColor = new Color("3d3d3d"),
            ContentMarginLeft = 4, ContentMarginRight = 4,
            ContentMarginTop = 4, ContentMarginBottom = 4,
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        };
        _style.SetBorderWidthAll(1);
        AddThemeStyleboxOverride("panel", _style);

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 1);
        AddChild(column);

        var icon = new TextureRect
        {
            Texture = _def.Icon,
            CustomMinimumSize = new Vector2(IconSize, IconSize),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        column.AddChild(icon);

        var name = new Label
        {
            Text = _def.DisplayName,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Off,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            CustomMinimumSize = new Vector2(70, 0),
        };
        name.AddThemeFontSizeOverride("font_size", 10);
        name.AddThemeColorOverride("font_color", new Color("9a9a9a"));
        column.AddChild(name);

        _unitsLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _unitsLabel.AddThemeFontSizeOverride("font_size", 14);
        column.AddChild(_unitsLabel);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 2);
        column.AddChild(buttons);

        _removeButton = MakeStepButton("−");
        _removeButton.Pressed += () => OnRemove();
        buttons.AddChild(_removeButton);

        _addButton = MakeStepButton("+");
        _addButton.Pressed += () => OnAdd();
        buttons.AddChild(_addButton);

        _stockLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _stockLabel.AddThemeFontSizeOverride("font_size", 9);
        _stockLabel.AddThemeColorOverride("font_color", new Color("6d6d6d"));
        column.AddChild(_stockLabel);
    }

    private static Button MakeStepButton(string text)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(30, 20) };
        button.AddThemeFontSizeOverride("font_size", 12);
        return button;
    }

    /// <param name="unitsInDish">Quanto desse ingrediente já está no prato.</param>
    /// <param name="stock">Quanto sobrou na despensa.</param>
    /// <param name="canAdd">Falso quando não há estoque, slot livre ou compatibilidade.</param>
    public void Refresh(int unitsInDish, int stock, bool canAdd)
    {
        bool inDish = unitsInDish > 0;

        _unitsLabel.Text = inDish ? unitsInDish.ToString() : "–";
        _unitsLabel.AddThemeColorOverride("font_color",
            inDish ? new Color("ffd479") : new Color("4a4a4a"));

        _stockLabel.Text = $"estoque {stock}";

        _addButton.Disabled = !canAdd;
        _removeButton.Disabled = !inDish;
        _removeButton.Visible = inDish;

        _style.BgColor = inDish ? new Color("29381f") : new Color("2b2b2b");
        _style.BorderColor = inDish ? _def.TintColor : new Color("3d3d3d");
        Modulate = canAdd || inDish ? Colors.White : new Color(1, 1, 1, 0.45f);
    }
}
