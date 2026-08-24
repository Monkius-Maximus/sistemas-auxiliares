using System.Collections.Generic;
using System.Linq;
using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// O conteúdo do jogo, lido dos arquivos. Ingrediente novo é um `.tres` novo na pasta —
/// não uma linha de C#, não um registro em lista nenhuma. É o que permite que o conteúdo
/// cresça (e que um dia seja modificado por quem joga) sem recompilar o jogo.
///
/// A biblioteca não interpreta nada: ela só carrega e ordena. Toda regra continua no
/// modelo, em <c>Cooking/</c>.
/// </summary>
public static class ContentLibrary
{
    public const string IngredientsFolder = "res://Content/Ingredients";
    public const string BasesFolder = "res://Content/Bases";
    public const string AnchorsFolder = "res://Content/Anchors";

    /// <summary>
    /// Ingredientes e temperos juntos: quem separa os dois é <see cref="IngredientDef.IsSeasoning"/>,
    /// e quem faz a separação é a sessão. Duas listas aqui seriam duas fontes da mesma verdade.
    /// </summary>
    public static List<IngredientDef> Ingredients() =>
        Load<IngredientDef>(IngredientsFolder).OrderBy(d => d.DisplayName).ToList();

    public static List<BaseItemDef> Bases() =>
        Load<BaseItemDef>(BasesFolder).OrderBy(d => d.DisplayName).ToList();

    public static List<RecipeAnchor> Anchors() =>
        Load<RecipeAnchor>(AnchorsFolder).OrderBy(a => a.DishName).ToList();

    /// <summary>
    /// Despensa inicial. Isto não é conteúdo: é estado de partida, e vai embora quando
    /// existir save. Temperos vêm em quantidade alta porque entram em doses pequenas.
    /// </summary>
    public static Dictionary<IngredientDef, int> StartingPantry()
    {
        var pantry = new Dictionary<IngredientDef, int>();
        foreach (var def in Ingredients())
            pantry[def] = def.IsSeasoning ? 25 : 40;
        return pantry;
    }

    /// <summary>
    /// Carrega uma pasta inteira. Pasta vazia é erro de instalação, não estado possível:
    /// um jogo sem ingrediente nenhum não tem como se recuperar disso mais adiante.
    /// </summary>
    private static IEnumerable<T> Load<T>(string folder) where T : Resource
    {
        var files = DirAccess.GetFilesAt(folder);
        if (files.Length == 0)
            throw new System.InvalidOperationException($"Sem conteúdo em {folder}.");

        var loaded = new List<T>();
        foreach (var entry in files)
        {
            // Ao exportar, o Godot converte .tres em binário e deixa um .remap no lugar;
            // no projeto aberto o arquivo é o próprio .tres. Aceitar as duas formas é o que
            // faz a mesma pasta funcionar no editor e no build.
            var file = entry.EndsWith(".remap") ? entry.GetBaseName() : entry;
            if (!file.EndsWith(".tres") && !file.EndsWith(".res"))
                continue;

            var path = $"{folder}/{file}";
            loaded.Add(ResourceLoader.Load<T>(path)
                       ?? throw new System.InvalidOperationException($"{path} não é {typeof(T).Name}."));
        }
        return loaded;
    }
}
