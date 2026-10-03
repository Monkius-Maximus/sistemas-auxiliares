using System;
using System.Collections.Generic;
using System.Linq;

namespace LifeSim.Cooking;

/// <summary>
/// A cozinha do Sim: a despensa e o prato em preparo. Única dona das duas quantidades, para
/// que exista um só lugar onde elas mudam juntas — pôr no prato é tirar da despensa, na mesma
/// operação. A UI nunca guarda estado: escuta <c>Changed</c> e redesenha.
///
/// Toda pré-condição violada lança exceção. O painel só oferece ações válidas — se uma
/// exceção subir daqui, é bug de UI, não input do jogador.
/// </summary>
public sealed class CookingSession
{
    /// <summary>Disparado após qualquer mutação, inclusive o tempo passando. A UI escuta e redesenha.</summary>
    public event Action Changed;

    private readonly Dictionary<string, IngredientStack> _ingredients = new();
    private readonly Dictionary<string, IngredientStack> _seasonings = new();

    public CookingSession(Pantry pantry, int cookingLevel)
    {
        Pantry = pantry ?? throw new ArgumentNullException(nameof(pantry));
        if (cookingLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(cookingLevel));
        CookingLevel = cookingLevel;
    }

    public int CookingLevel { get; }
    public BaseItemDef Base { get; private set; }

    /// <summary>A despensa, para leitura. Mudar estoque é sempre por um método desta classe.</summary>
    public Pantry Pantry { get; }

    /// <summary>De que lote sai a próxima unidade. Trocar reescolhe o que já está no prato.</summary>
    public PickOrder PickOrder { get; private set; } = PickOrder.OldestFirst;

    /// <summary>
    /// Tudo que mexe neste preparo além dos ingredientes. Hoje só o recipiente contribui;
    /// traço do Sim, perícia ou fogão entram somando suas listas aqui, sem tocar no avaliador.
    /// </summary>
    public ModifierStack Modifiers => Base is null ? ModifierStack.Empty : new ModifierStack(Base.Modifiers);

    public IReadOnlyCollection<IngredientStack> Ingredients => _ingredients.Values;
    public IReadOnlyCollection<IngredientStack> Seasonings => _seasonings.Values;

    public int IngredientSlots =>
        Base is null ? 0 : Base.IngredientSlotsForLevel(CookingLevel);
    public int SeasoningSlots => Base?.SeasoningSlots ?? 0;

    public int AvailableOf(IngredientDef def) => Pantry.UnitsOf(def);

    public int UnitsInDish(IngredientDef def) =>
        Bucket(def).TryGetValue(def.Id, out var s) ? s.Units : 0;

    /// <summary>Tudo que a despensa conhece, separado pelo painel em que aparece.</summary>
    public IEnumerable<IngredientDef> PantryIngredients(bool seasonings) =>
        Pantry.Defs.Where(d => d.IsSeasoning == seasonings);

    /// <summary>Trocar de recipiente esvazia o prato e devolve tudo à despensa.</summary>
    public void SetBase(BaseItemDef baseItem)
    {
        if (baseItem is null)
            throw new ArgumentNullException(nameof(baseItem));

        ReturnAllToPantry();
        Base = baseItem;
        Changed?.Invoke();
    }

    /// <summary>
    /// Trocar a ordem reescolhe os lotes do que já está no prato. Sem isso o verbo só valeria
    /// para a próxima unidade e o preview mentiria sobre o prato que está na tela.
    /// </summary>
    public void SetPickOrder(PickOrder order)
    {
        if (order == PickOrder) return;

        var units = _ingredients.Values.Concat(_seasonings.Values)
                                .Select(s => (s.Def, s.Units))
                                .ToList();
        ReturnAllToPantry();
        PickOrder = order;
        foreach (var (def, count) in units)
            Bucket(def)[def.Id] = new IngredientStack(def, Pantry.Take(def, count, PickOrder));

        Changed?.Invoke();
    }

    public void AddUnit(IngredientDef def, int units = 1)
    {
        if (Base is null)
            throw new InvalidOperationException("Escolha um recipiente antes de adicionar ingredientes.");
        if (units <= 0)
            throw new ArgumentOutOfRangeException(nameof(units));
        if (AvailableOf(def) < units)
            throw new InvalidOperationException($"Sem {def.DisplayName} suficiente na despensa.");
        if (!def.IsSeasoning && !Base.Accepts(def.Group))
            throw new InvalidOperationException($"{Base.DisplayName} não aceita {def.Group}.");

        var bucket = Bucket(def);
        bool existing = bucket.TryGetValue(def.Id, out var stack);
        if (!existing)
        {
            int limit = def.IsSeasoning ? SeasoningSlots : IngredientSlots;
            if (bucket.Count >= limit)
                throw new InvalidOperationException($"Todos os {limit} slots estão ocupados.");
        }
        if (UnitsInDish(def) + units > def.MaxUnitsInDish)
            throw new InvalidOperationException(
                $"{def.DisplayName} aceita no máximo {def.MaxUnitsInDish} unidades no prato.");

        var portions = Pantry.Take(def, units, PickOrder);
        if (existing) stack.Add(portions);
        else bucket[def.Id] = new IngredientStack(def, portions);

        Changed?.Invoke();
    }

    public void RemoveUnit(IngredientDef def, int units = 1)
    {
        var bucket = Bucket(def);
        if (!bucket.TryGetValue(def.Id, out var stack))
            throw new InvalidOperationException($"{def.DisplayName} não está no prato.");

        Pantry.Return(def, stack.Remove(units));
        if (stack.Units == 0)
            bucket.Remove(def.Id);

        Changed?.Invoke();
    }

    public void Clear()
    {
        ReturnAllToPantry();
        Changed?.Invoke();
    }

    /// <summary>
    /// O tempo passa para a despensa. É o relógio do mundo que chama isto; o prato em preparo
    /// volta à despensa antes, porque comida esquecida na bancada não fica parada no tempo.
    /// </summary>
    public void AdvanceTime(float days)
    {
        ReturnAllToPantry();
        Pantry.Age(days);
        Changed?.Invoke();
    }

    /// <summary>Unidades estragadas na despensa e no recipiente.</summary>
    public int RottenUnits =>
        Pantry.RottenUnits + _ingredients.Values.Concat(_seasonings.Values).Sum(s => s.RottenUnits);

    /// <summary>
    /// Joga fora o que estragou — da despensa e do recipiente. O que já está no prato conta:
    /// o jogador que vê o risco de intoxicação subir quer tirar o ingrediente podre dali,
    /// não da prateleira. Devolve quantas unidades saíram.
    /// </summary>
    public int DiscardRotten()
    {
        if (RottenUnits == 0)
            throw new InvalidOperationException("Não há nada estragado para jogar fora.");

        int discarded = Pantry.DiscardRotten();
        foreach (var bucket in new[] { _ingredients, _seasonings })
        {
            foreach (var stack in bucket.Values.ToList())
            {
                discarded += stack.RemoveRotten();
                if (stack.Units == 0) bucket.Remove(stack.Def.Id);
            }
        }
        Changed?.Invoke();
        return discarded;
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
            Pantry.Return(stack.Def, stack.All);
        _ingredients.Clear();
        _seasonings.Clear();
    }
}
