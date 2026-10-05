using Godot;

namespace ContextUi;

/// <summary>
/// Troca de cena com fade para o preto e de volta. A camada do fade é filha da raiz da árvore,
/// não da cena: ela sobrevive à troca, cobre a cena nova enquanto ela monta e some sozinha.
///
/// Toda troca de tela inteira (menu, jogo, bancada) passa por aqui, para que o corte tenha a
/// mesma cara em todo lugar e mude num ponto só.
/// </summary>
public static class SceneTransition
{
    private const float FadeSeconds = 0.18f;

    /// <summary>Acima de qualquer interface do jogo.</summary>
    private const int Layer = 128;

    /// <summary>Uma troca de cada vez: clicar duas vezes em "Novo jogo" não pode empilhar fades.</summary>
    private static bool _busy;

    public static void Go(Node from, string scenePath)
    {
        if (_busy) return;
        _busy = true;

        var tree = from.GetTree();
        var layer = new CanvasLayer { Layer = Layer };
        var black = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Stop };
        black.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        black.Modulate = new Color(1, 1, 1, 0);
        layer.AddChild(black);
        tree.Root.AddChild(layer);

        var tween = layer.CreateTween();
        tween.TweenProperty(black, "modulate:a", 1f, FadeSeconds);
        tween.TweenCallback(Callable.From(() => tree.ChangeSceneToFile(scenePath)));
        // Um quadro de folga: a cena nova monta o _Ready no quadro seguinte à troca.
        tween.TweenInterval(0.05f);
        tween.TweenProperty(black, "modulate:a", 0f, FadeSeconds);
        tween.TweenCallback(Callable.From(() =>
        {
            layer.QueueFree();
            _busy = false;
        }));
    }
}
