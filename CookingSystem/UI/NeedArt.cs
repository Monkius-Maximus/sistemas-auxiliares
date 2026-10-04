using Godot;

namespace LifeSim.Cooking;

/// <summary>
/// Cor e arte de cada necessidade, iguais no HUD da casa e na geladeira: o jogador aprende o
/// ícone da fome uma vez. A arte vem de <c>res://Art/Hud/&lt;id&gt;.png</c>.
/// </summary>
public static class NeedArt
{
    public const string HungerId = "fome";
    public const string ThirstId = "sede";
    public const string MoodId = "humor";

    public static readonly Color HungerTint = new("d9a24a");
    public static readonly Color ThirstTint = new("5b8fc7");
    public static readonly Color MoodTint = new("b07cc6");

    public static Texture2D Icon(string id) => ArtLibrary.Find(ArtLibrary.Hud, id);
}
