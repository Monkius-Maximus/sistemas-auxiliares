using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// Combinação âncora: NÃO é uma receita travada. O jogador continua livre para montar
/// o que quiser; se por acaso acertar o conjunto de uma âncora, ganha bônus de qualidade
/// e o prato passa a ter nome próprio em vez de nome gerado.
/// É o anteparo contra o "tudo emergente vira sopa sem graça".
/// </summary>
[GlobalClass]
public partial class RecipeAnchor : Resource
{
    [Export] public string DishName { get; set; } = "";
    [Export] public string BaseItemId { get; set; } = "";

    /// <summary>
    /// Ingrediente -> unidades. Serve para as duas coisas: a presença de todas as chaves
    /// é o que casa a âncora, e as quantidades são a porção usada pelo menu rápido.
    /// Um campo só, para não existirem duas verdades sobre o que é essa receita.
    /// </summary>
    [Export] public Godot.Collections.Dictionary<string, int> Portions { get; set; } = new();

    /// <summary>Se qualquer um estiver presente, a âncora não vale.</summary>
    [Export] public string[] ForbiddenIngredientIds { get; set; } = System.Array.Empty<string>();

    /// <summary>
    /// Fração da distância até 1.0 que a âncora fecha: <c>q + (1-q) * lift</c>.
    /// Multiplicador simples fazia qualquer prato âncora bem executado estourar o teto
    /// e ser aparado em 100%, apagando a granularidade no topo da escala.
    /// </summary>
    [Export] public float QualityLift { get; set; } = 0.35f;
}
