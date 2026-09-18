using System;
using System.Collections.Generic;
using System.Linq;

namespace LifeSim.Cooking;

/// <summary>
/// Estado mutável de uma preparação em andamento. Dona da despensa E do prato, para que
/// exista um único lugar onde as duas quantidades mudam juntas. A UI nunca guarda estado:
/// escuta <c>Changed</c> e redesenha.
///
/// Toda pré-condição violada lança exceção. O painel só oferece ações válidas — se uma
/// exceção subir daqui, é bug de UI, não input do jogador.
/// </summary>
public sealed class CookingSession
{
    /// <summary>Disparado após qualquer mutação. A UI escuta e redesenha; nada mais.</summary>
    public event Action Changed;

    private readonly Dictionary<string, IngredientStack> _ingredients = new();
    private readonly Dictionary<string, IngredientStack> _seasonings = new();
    private readonly Dictionary<string, int> _pantry;
    private readonly Dictionary<string, IngredientDef> _defsById;

    public CookingSession(IReadOnlyDictionary<IngredientDef, int> pantry, int cookingLevel)
    {
        if (pantry is null)
            throw new ArgumentNullException(nameof(pantry));
        if (cookingLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(cookingLevel));

        _pantry = pantry.ToDictionary(kv => kv.Key.Id, kv => kv.Value);
        _defsById = pantry.Keys.ToDictionary(d => d.Id, d => d);
        CookingLevel = cookingLevel;
    }

    public int CookingLevel { get; }
    public BaseItemDef Base { get; private set; }

    public IReadOnlyCollection<IngredientStack> Ingredients => _ingredients.Values;
    public IReadOnlyCollection<IngredientStack> Seasonings => _seasonings.Values;

    public int IngredientSlots =>
        Base is null ? 0 : Base.IngredientSlotsForLevel(CookingLevel);
    public int SeasoningSlots => Base?.SeasoningSlots ?? 0;

    public int AvailableOf(IngredientDef def) => _pantry[def.Id];

    /// <summary>
    /// A despensa, para quem precisa planejar sobre ela — o menu rápido. Somente leitura:
    /// quem muda quantidade é esta classe, na mesma operação em que o prato muda. Materializa
    /// a visão a cada chamada; é lida quando o menu abre, não a cada quadro.
    /// </summary>
    public IReadOnlyDictionary<IngredientDef, int> Pantry =>
        _defsById.Values.ToDictionary(d => d, d => _pantry[d.Id]);

    public int UnitsInDish(IngredientDef def) =>
        Bucket(def).TryGetValue(def.Id, out var s) ? s.Units : 0;

    public IngredientDef DefById(string id) => _defsById[id];

    /// <summary>Tudo que a despensa conhece, separado pelo painel em que aparece.</summary>
    public IEnumerable<IngredientDef> PantryIngredients(bool seasonings) =>
        _defsById.Values.Where(d => d.IsSeasoning == seasonings);

    /// <summary>Trocar de recipiente esvazia o prato e devolve tudo à despensa.</summary>
    public void SetBase(BaseItemDef baseItem)
    {
        if (baseItem is null)
            throw new ArgumentNullException(nameof(baseItem));

        ReturnAllToPantry();
        Base = baseItem;
        Changed?.Invoke();
    }

    public void AddUnit(IngredientDef def, int units = 1)
    {
        if (Base is null)
            throw new InvalidOperationException("Escolha um recipiente antes de adicionar ingredientes.");
        if (units <= 0)
            throw new ArgumentOutOfRangeException(nameof(units));
        if (_pantry[def.Id] < units)
            throw new InvalidOperationException($"Sem {def.DisplayName} suficiente na despensa.");
        if (!def.IsSeasoning && !Base.Accepts(def.Group))
            throw new InvalidOperationException(
                $"{Base.DisplayName} não aceita {def.Group}.");

        var bucket = Bucket(def);
        if (bucket.TryGetValue(def.Id, out var stack))
        {
            stack.Add(units);
        }
        else
        {
            int limit = def.IsSeasoning ? SeasoningSlots : IngredientSlots;
            if (bucket.Count >= limit)
                throw new InvalidOperationException($"Todos os {limit} slots estão ocupados.");
            bucket[def.Id] = new IngredientStack(def, units, FreshnessOf(def));
        }

        _pantry[def.Id] -= units;
        Changed?.Invoke();
    }

    public void RemoveUnit(IngredientDef def, int units = 1)
    {
        var bucket = Bucket(def);
        if (!bucket.TryGetValue(def.Id, out var stack))
            throw new InvalidOperationException($"{def.DisplayName} não está no prato.");

        stack.Remove(units);
        if (stack.Units == 0)
            bucket.Remove(def.Id);

        _pantry[def.Id] += units;
        Changed?.Invoke();
    }

    public void Clear()
    {
        ReturnAllToPantry();
        Changed?.Invoke();
    }

    /// <summary>Consome o prato: o conteúdo sai da sessão de vez.</summary>
    public CookedDish Cook(IReadOnlyList<RecipeAnchor> anchors)
    {
        if (_ingredients.Count == 0)
            throw new InvalidOperationException("Não dá para cozinhar um prato vazio.");

        var dish = DishEvaluator.Evaluate(this, anchors);
        _ingredients.Clear();
        _seasonings.Clear();
        Changed?.Invoke();
        return dish;
    }

    private Dictionary<string, IngredientStack> Bucket(IngredientDef def) =>
        def.IsSeasoning ? _seasonings : _ingredients;

    private void ReturnAllToPantry()
    {
        foreach (var stack in _ingredients.Values.Concat(_seasonings.Values))
            _pantry[stack.Def.Id] += stack.Units;
        _ingredients.Clear();
        _seasonings.Clear();
    }

    /// <summary>
    /// Ponto de integração com o sistema de perecíveis do mundo. No protótipo é 1.0;
    /// troque por uma consulta ao item real da despensa quando esse sistema existir.
    /// </summary>
    private static float FreshnessOf(IngredientDef def) => 1.0f;
}
