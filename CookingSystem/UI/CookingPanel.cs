using System;
using System.Collections.Generic;
using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// Painel único: recipiente + ingredientes + temperos + preview, tudo na mesma tela.
/// Nenhuma etapa, nenhum wizard. Cada +/- reavalia a sessão inteira e redesenha.
///
/// Regra estrutural: este Control não guarda estado nenhum. A verdade está na
/// <see cref="CookingSession"/>; o painel só escuta <c>Changed</c> e reflete.
/// </summary>
public partial class CookingPanel : Control
{
    private CookingSession _session;
    private IReadOnlyList<BaseItemDef> _bases;
    private IReadOnlyList<RecipeAnchor> _anchors;

    private readonly Dictionary<string, IngredientSlotView> _slotViews = new();
    private readonly List<(Button Button, BaseItemDef Def)> _baseButtons = new();

    private Label _dishNameLabel;
    private Label _levelLabel;
    private Label _ingredientCounter;
    private Label _seasoningCounter;
    private Label _qualityLabel;
    private ProgressBar _qualityBar;
    private Label _factorsLabel;
    private Button _cookButton;

    private readonly Dictionary<string, Label> _statValues = new();

    /// <summary>Rótulo interno -> texto exibido. Espelha o bloco de stats da referência.</summary>
    private static readonly (string Key, string Label)[] StatRows =
    {
        ("hunger", "Fome"), ("thirst", "Sede"), ("calories", "Calorias"),
        ("protein", "Proteína"), ("lipids", "Gordura"), ("carbs", "Carbo."),
        ("unhappiness", "Ânimo"), ("boredom", "Tédio"), ("weight", "Peso"),
    };

    /// <summary>
    /// Injeta o mundo. Precisa ser chamado antes de o nó entrar na árvore de render —
    /// não existe caminho alternativo de inicialização.
    /// </summary>
    public void Setup(
        IReadOnlyDictionary<IngredientDef, int> pantry,
        IReadOnlyList<BaseItemDef> bases,
        IReadOnlyList<RecipeAnchor> anchors,
        int cookingLevel)
    {
        if (bases is null || bases.Count == 0)
            throw new ArgumentException("O painel precisa de pelo menos um recipiente.", nameof(bases));

        _bases = bases;
        _anchors = anchors ?? throw new ArgumentNullException(nameof(anchors));
        _session = new CookingSession(pantry, cookingLevel);
        _session.Changed += Refresh;
    }

    public override void _Ready()
    {
        if (_session is null)
            throw new InvalidOperationException("Setup() precisa ser chamado antes de adicionar o painel à árvore.");

        var root = new HBoxContainer { AnchorRight = 1, AnchorBottom = 1 };
        root.AddThemeConstantOverride("separation", 10);
        AddChild(root);

        root.AddChild(BuildLeftColumn());
        root.AddChild(BuildRightColumn());

        _session.SetBase(_bases[0]);
    }

    // ------------------------------------------------------------------
    // Construção
    // ------------------------------------------------------------------

    private Control BuildLeftColumn()
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(260, 0) };
        column.AddThemeConstantOverride("separation", 8);

        column.AddChild(SectionHeader("Recipiente"));

        var baseRow = new HBoxContainer();
        baseRow.AddThemeConstantOverride("separation", 4);
        foreach (var def in _bases)
        {
            var button = new Button
            {
                Text = def.DisplayName,
                ToggleMode = true,
                CustomMinimumSize = new Vector2(0, 34),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            button.AddThemeFontSizeOverride("font_size", 11);
            var captured = def;
            button.Pressed += () => _session.SetBase(captured);
            _baseButtons.Add((button, def));
            baseRow.AddChild(button);
        }
        column.AddChild(baseRow);

        _dishNameLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _dishNameLabel.AddThemeFontSizeOverride("font_size", 16);
        column.AddChild(_dishNameLabel);

        _levelLabel = new Label();
        _levelLabel.AddThemeFontSizeOverride("font_size", 11);
        _levelLabel.AddThemeColorOverride("font_color", new Color("9a9a9a"));
        column.AddChild(_levelLabel);

        column.AddChild(BuildQualityBlock());
        column.AddChild(BuildStatGrid());

        _cookButton = new Button { Text = "Cozinhar", CustomMinimumSize = new Vector2(0, 38) };
        _cookButton.Pressed += OnCookPressed;
        column.AddChild(_cookButton);

        var clear = new Button { Text = "Esvaziar" };
        clear.Pressed += () => _session.Clear();
        column.AddChild(clear);

        return column;
    }

    private Control BuildQualityBlock()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);

        _qualityLabel = new Label();
        _qualityLabel.AddThemeFontSizeOverride("font_size", 12);
        box.AddChild(_qualityLabel);

        _qualityBar = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 10),
        };
        box.AddChild(_qualityBar);

        // Os cinco fatores expostos: é isso que ensina o sistema ao jogador sem tutorial.
        _factorsLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _factorsLabel.AddThemeFontSizeOverride("font_size", 10);
        _factorsLabel.AddThemeColorOverride("font_color", new Color("8f8f8f"));
        box.AddChild(_factorsLabel);

        return box;
    }

    private Control BuildStatGrid()
    {
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);

        foreach (var (key, label) in StatRows)
        {
            var cell = new PanelContainer();
            var style = new StyleBoxFlat
            {
                BgColor = new Color("242424"),
                ContentMarginLeft = 6, ContentMarginRight = 6,
                ContentMarginTop = 4, ContentMarginBottom = 4,
                CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
                CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
            };
            cell.AddThemeStyleboxOverride("panel", style);

            var inner = new VBoxContainer();
            inner.AddThemeConstantOverride("separation", 0);

            var caption = new Label { Text = label, HorizontalAlignment = HorizontalAlignment.Center };
            caption.AddThemeFontSizeOverride("font_size", 9);
            caption.AddThemeColorOverride("font_color", new Color("7d7d7d"));
            inner.AddChild(caption);

            var value = new Label { Text = "0", HorizontalAlignment = HorizontalAlignment.Center };
            value.AddThemeFontSizeOverride("font_size", 14);
            inner.AddChild(value);

            _statValues[key] = value;
            cell.AddChild(inner);
            grid.AddChild(cell);
        }

        return grid;
    }

    private Control BuildRightColumn()
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 6);

        _ingredientCounter = new Label();
        column.AddChild(HeaderRow("Ingredientes", _ingredientCounter));
        column.AddChild(BuildSlotGrid(_session.PantryIngredients(seasonings: false)));

        _seasoningCounter = new Label();
        column.AddChild(HeaderRow("Temperos", _seasoningCounter));
        column.AddChild(BuildSlotGrid(_session.PantryIngredients(seasonings: true)));

        return column;
    }

    private Control BuildSlotGrid(IEnumerable<IngredientDef> defs)
    {
        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);

        foreach (var def in defs)
        {
            var captured = def;
            var view = new IngredientSlotView
            {
                Def = captured,
                OnAdd = () => _session.AddUnit(captured),
                OnRemove = () => _session.RemoveUnit(captured),
            };
            _slotViews[captured.Id] = view;
            grid.AddChild(view);
        }

        return grid;
    }

    private static Label SectionHeader(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", new Color("9a9a9a"));
        return label;
    }

    private static Control HeaderRow(string title, Label counter)
    {
        var row = new HBoxContainer();
        row.AddChild(SectionHeader(title));
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        counter.AddThemeFontSizeOverride("font_size", 11);
        counter.AddThemeColorOverride("font_color", new Color("9a9a9a"));
        row.AddChild(counter);
        return row;
    }

    // ------------------------------------------------------------------
    // Refresh: a única função que escreve na UI
    // ------------------------------------------------------------------

    private void Refresh()
    {
        foreach (var (button, def) in _baseButtons)
            button.ButtonPressed = def.Id == _session.Base.Id;

        _levelLabel.Text = $"Culinária Nv {_session.CookingLevel}  ·  {_session.Base.DisplayName}";
        _ingredientCounter.Text = $"{_session.Ingredients.Count} / {_session.IngredientSlots}";
        _seasoningCounter.Text = $"{_session.Seasonings.Count} / {_session.SeasoningSlots}";

        RefreshSlots();

        if (_session.Ingredients.Count == 0)
        {
            ShowEmptyPreview();
            return;
        }

        ShowDish(DishEvaluator.Evaluate(_session, _anchors));
    }

    private void RefreshSlots()
    {
        foreach (var (id, view) in _slotViews)
        {
            var def = _session.DefById(id);
            int inDish = _session.UnitsInDish(def);
            int stock = _session.AvailableOf(def);

            bool slotFree = inDish > 0 ||
                (def.IsSeasoning
                    ? _session.Seasonings.Count < _session.SeasoningSlots
                    : _session.Ingredients.Count < _session.IngredientSlots);
            bool compatible = def.IsSeasoning || _session.Base.Accepts(def.Group);
            bool underCap = inDish < def.MaxUnitsInDish;

            view.Refresh(inDish, stock, canAdd: stock > 0 && slotFree && compatible && underCap);
        }
    }

    private void ShowEmptyPreview()
    {
        _dishNameLabel.Text = "—";
        _qualityLabel.Text = "Adicione um ingrediente";
        _qualityBar.Value = 0;
        _factorsLabel.Text = "";
        _cookButton.Disabled = true;

        foreach (var value in _statValues.Values)
        {
            value.Text = "0";
            value.AddThemeColorOverride("font_color", new Color("4a4a4a"));
        }
    }

    private void ShowDish(CookedDish dish)
    {
        _cookButton.Disabled = false;
        _dishNameLabel.Text = dish.Name;

        _qualityLabel.Text = dish.CappedBySkill
            ? $"Qualidade {dish.Quality:P0}  (limitado pela perícia)"
            : $"Qualidade {dish.Quality:P0}  ·  {FlavorProfile.Label(dish.DominantAxis)}";

        _qualityBar.Value = dish.Quality;
        _qualityBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = QualityColor(dish.Quality) });

        _factorsLabel.Text =
            $"tempero {dish.SeasoningScore:P0} · equilíbrio {dish.BalanceScore:P0} · " +
            $"harmonia {dish.HarmonyScore:P0} · variedade {dish.VarietyScore:P0} · " +
            $"frescor {dish.FreshnessScore:P0}  ·  custo {dish.Cost}" +
            (dish.MatchedAnchor ? "  ★ combinação conhecida" : "");

        SetStat("hunger", dish.Hunger, "0.0", positive: true);
        SetStat("thirst", dish.Thirst, "0.0", positive: true);
        SetStat("calories", dish.Calories, "0", positive: null);
        SetStat("protein", dish.Protein, "0.0", positive: null);
        SetStat("lipids", dish.Lipids, "0.0", positive: null);
        SetStat("carbs", dish.Carbs, "0.0", positive: null);
        SetStat("unhappiness", dish.UnhappinessRelief, "+0.0;-0.0", positive: true);
        SetStat("boredom", dish.BoredomRelief, "0.0", positive: true);
        SetStat("weight", dish.Weight, "0.00", positive: null);
    }

    private void SetStat(string key, float value, string format, bool? positive)
    {
        var label = _statValues[key];
        label.Text = value.ToString(format);
        label.AddThemeColorOverride("font_color",
            positive is null ? new Color("d6d6d6")
            : value >= 0 ? new Color("7fc97f")
            : new Color("d97a7a"));
    }

    private static Color QualityColor(float quality) => quality switch
    {
        < 0.35f => new Color("c0504d"),
        < 0.70f => new Color("d6a13a"),
        _ => new Color("6aa84f"),
    };

    private void OnCookPressed()
    {
        var dish = _session.Cook(_anchors);
        GD.Print($"[Cozinha] {dish.Name} — qualidade {dish.Quality:P0}, " +
                 $"{dish.Calories:0} kcal, moodlet {dish.MoodletMinutes:0} min");
    }
}
