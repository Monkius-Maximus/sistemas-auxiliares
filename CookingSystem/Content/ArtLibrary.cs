using System.Collections.Generic;
using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// Onde a arte mora. Arte é achada pelo <b>nome do arquivo</b>, não registrada: quem desenha o
/// bacon salva <c>res://Art/Ingredients/bacon.png</c> e pronto — sem abrir <c>.tres</c>, sem
/// linha de C#. É o mesmo trato do conteúdo: arquivo novo na pasta, nada para registrar.
///
/// O campo <c>Icon</c> do <c>.tres</c> continua valendo e ganha da convenção: serve para
/// apontar uma arte com outro nome (duas variantes, um atlas). Sem nenhum dos dois, a tela
/// desenha o marcador — a cor do item com as iniciais.
///
/// Tabela completa de pastas, tamanhos e ids em <c>docs/ARTE-E-FLUXO.md</c>.
/// </summary>
public static class ArtLibrary
{
    public const string Root = "res://Art";

    public const string Ingredients = "Ingredients";
    public const string Vessels = "Vessels";
    public const string Dishes = "Dishes";
    public const string Vendors = "Vendors";
    public const string House = "House";
    public const string Hud = "Hud";
    public const string Menu = "Menu";

    /// <summary>Prefixo da arte do prato improvisado de cada recipiente: <c>generico-panela.png</c>.</summary>
    public const string GenericDishPrefix = "generico-";

    /// <summary>
    /// Inclusive os que não existem: a mesma tela pede o mesmo ícone a cada redesenho, e
    /// perguntar ao disco a cada unidade somada no prato seria desperdício.
    /// </summary>
    private static readonly Dictionary<string, Texture2D> Cache = new();

    public static string PathOf(string category, string id) => $"{Root}/{category}/{id}.png";

    /// <summary>A arte de <paramref name="id"/>, ou null se ninguém desenhou ainda.</summary>
    public static Texture2D Find(string category, string id)
    {
        var path = PathOf(category, id);
        if (Cache.TryGetValue(path, out var cached))
            return cached;

        // Exists olha o .import, que é o que sobrevive à exportação: no build o .png some
        // e só a textura importada fica.
        var texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        Cache[path] = texture;
        return texture;
    }

    /// <summary>
    /// A arte de um prato: a da receita reconhecida, senão a do improvisado naquele recipiente.
    /// Prato estragado continua com o desenho da omelete — o estado vai no nome, não na arte.
    /// </summary>
    public static Texture2D Dish(CookedDish dish, BaseItemDef vessel) =>
        dish?.Anchor?.Icon ?? vessel?.DishIcon;
}
