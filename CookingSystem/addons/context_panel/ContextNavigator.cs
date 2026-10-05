using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ContextUi;

/// <summary>
/// Uma tela na pilha do navegador: a definição do painel e o que a tela pede do mundo.
/// </summary>
public sealed class ContextScreen
{
    /// <summary>Nome curto na trilha do cabeçalho das telas de cima ("Fogão › …").</summary>
    public required string Title { get; init; }

    public required Func<PanelContext> Definition { get; init; }

    /// <summary>
    /// Se o mundo espera enquanto esta tela está no topo. Quem lê é o host (o relógio do
    /// jogo); o navegador só informa. Cozinhar e comprar pausam; uma tela de consulta pode não.
    /// </summary>
    public bool PausesWorld { get; init; } = true;

    /// <summary>A tela voltou ao topo depois de a de cima sair. Para replanejar o que envelheceu.</summary>
    public Action OnReveal { get; init; }

    /// <summary>A tela saiu da pilha. Para devolver o que ela segurava (o recipiente, o carrinho).</summary>
    public Action OnLeave { get; init; }
}

/// <summary>
/// O navegador de contextos: uma pilha de telas, cada uma um <see cref="ContextPanel"/>.
///
/// <list type="bullet">
/// <item><see cref="Push"/> abre por cima; <see cref="Back"/> volta uma; <see cref="Replace"/>
/// troca a do topo; <see cref="CloseAll"/> esvazia. Esc, B e ✕ são sempre <see cref="Back"/> —
/// o contexto passa <c>Back</c> como <c>OnClose</c>.</item>
/// <item>As telas de baixo continuam vivas e escondidas: voltar devolve o cursor ao mesmo
/// ladrilho, e a trilha no cabeçalho diz para onde o ✕ leva.</item>
/// <item>Toda troca passa por um fade curto, feito só aqui.</item>
/// </list>
///
/// Modal, cobre a tela inteira com um fundo escuro e centraliza o painel; inline, ocupa o
/// espaço que o container der (a bancada de testes). Sem regra de jogo: o host decide o que
/// cada tela é, o navegador só empilha.
/// </summary>
public partial class ContextNavigator : Control
{
    private const float FadeSeconds = 0.12f;

    /// <summary>Pele de todas as telas da pilha. Trocar exige <see cref="Restyle"/>.</summary>
    public PanelSkin Skin { get; set; }

    /// <summary>Fundo escuro em tela cheia, que engole o clique na cena por trás.</summary>
    public bool Modal { get; init; } = true;

    /// <summary>Desligado, as trocas são instantâneas (capturas, testes, preferência do jogador).</summary>
    public bool Animate { get; set; } = true;

    public bool ShowRegionLabels { get; set; }

    /// <summary>A pilha mudou: abriu, voltou, fechou. O host decide se o mundo pausa.</summary>
    public event Action StackChanged;

    private readonly List<(ContextScreen Screen, ContextPanel Panel)> _stack = new();
    private Control _layer;
    private ColorRect _dim;

    public bool IsOpen => _stack.Count > 0;
    public ContextScreen Top => _stack.Count == 0 ? null : _stack[^1].Screen;
    public bool PausesWorld => Top?.PausesWorld ?? false;

    public override void _Ready()
    {
        if (Skin is null)
            throw new InvalidOperationException("ContextNavigator exige Skin.");

        if (Modal)
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop };
            _dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(_dim);

            var center = new CenterContainer();
            center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(center);
            _layer = center;
            Visible = false;
        }
        else
        {
            var stack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            stack.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(stack);
            _layer = stack;
        }
    }

    // ------------------------------------------------------------------
    // Pilha
    // ------------------------------------------------------------------

    public void Push(ContextScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);

        if (_stack.Count == 0) ShowLayer();
        else Hide(_stack[^1].Panel);

        // Foco fora da pilha sai antes: com o painel aberto, Enter não pode apertar o botão da
        // cena por trás, que o fundo escuro esconde do mouse mas não do teclado.
        if (GetViewport().GuiGetFocusOwner() is Control owner && !IsAncestorOf(owner))
            GetViewport().GuiReleaseFocus();

        Enter(screen);
        StackChanged?.Invoke();
    }

    /// <summary>Troca a tela do topo sem passar pela de baixo. A que sai é avisada antes.</summary>
    public void Replace(ContextScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        if (_stack.Count == 0) throw new InvalidOperationException("Replace sem tela aberta.");

        Leave(_stack.Count - 1);
        Enter(screen);
        StackChanged?.Invoke();
    }

    /// <summary>Volta uma tela. Da última, fecha o navegador.</summary>
    public void Back()
    {
        if (_stack.Count == 0) throw new InvalidOperationException("Back sem tela aberta.");

        // A tela sai da pilha antes de ser avisada: o aviso costuma mexer no modelo, o modelo
        // pede redesenho, e o redesenho não pode cair na definição de quem está saindo.
        Leave(_stack.Count - 1);

        if (_stack.Count == 0)
            HideLayer();
        else
            Reveal(_stack[^1]);

        StackChanged?.Invoke();
    }

    public void CloseAll()
    {
        if (_stack.Count == 0) return;
        while (_stack.Count > 0) Leave(_stack.Count - 1);
        HideLayer();
        StackChanged?.Invoke();
    }

    /// <summary>Aplica <see cref="Skin"/> e <see cref="ShowRegionLabels"/> à pilha inteira.</summary>
    public void Restyle()
    {
        foreach (var (_, panel) in _stack)
        {
            panel.Skin = Skin;
            panel.ShowRegionLabels = ShowRegionLabels;
        }
        Rebuild();
    }

    /// <summary>Redesenha a tela do topo. As de baixo redesenham quando voltarem ao topo.</summary>
    public void Rebuild()
    {
        if (_stack.Count > 0) _stack[^1].Panel.Rebuild();
    }

    // ------------------------------------------------------------------
    // Painéis
    // ------------------------------------------------------------------

    private void Enter(ContextScreen screen)
    {
        var panel = new ContextPanel
        {
            Definition = screen.Definition,
            Skin = Skin,
            ShowRegionLabels = ShowRegionLabels,
            Trail = _stack.Select(s => s.Screen.Title).ToList(),
        };
        _stack.Add((screen, panel));
        _layer.AddChild(panel);
        FadeIn(panel);
    }

    private void Leave(int index)
    {
        var (screen, panel) = _stack[index];
        _stack.RemoveAt(index);

        // Sem entrada a partir daqui: o painel some num fade, mas não pode mais responder a
        // Esc nem a Enter enquanto isso.
        panel.ProcessMode = ProcessModeEnum.Disabled;
        panel.MouseFilter = MouseFilterEnum.Ignore;
        FadeOutAndFree(panel);

        screen.OnLeave?.Invoke();
    }

    private void Reveal((ContextScreen Screen, ContextPanel Panel) entry)
    {
        entry.Screen.OnReveal?.Invoke();

        var panel = entry.Panel;
        panel.ProcessMode = ProcessModeEnum.Inherit;
        panel.Visible = true;
        panel.Rebuild();
        panel.RestoreFocus();
        FadeIn(panel);
    }

    /// <summary>
    /// Escondida e desligada. Desligar é o que importa: um nó escondido continua recebendo
    /// <c>_Input</c>, e um painel de baixo ouvindo Esc fecharia duas telas de uma vez.
    /// </summary>
    private static void Hide(ContextPanel panel)
    {
        panel.Visible = false;
        panel.ProcessMode = ProcessModeEnum.Disabled;
    }

    // ------------------------------------------------------------------
    // Transições
    // ------------------------------------------------------------------

    private void ShowLayer()
    {
        Visible = true;
        if (_dim is null) return;
        _dim.Modulate = new Color(1, 1, 1, Animate ? 0 : 1);
        if (Animate) CreateTween().TweenProperty(_dim, "modulate:a", 1f, FadeSeconds);
    }

    private void HideLayer()
    {
        if (!Modal) return;
        if (!Animate) { Visible = false; return; }

        var tween = CreateTween();
        tween.TweenProperty(_dim, "modulate:a", 0f, FadeSeconds);
        // Reabrir durante o fade cancela o sumiço: a pilha já tem tela de novo.
        tween.TweenCallback(Callable.From(() => Visible = _stack.Count > 0));
    }

    private void FadeIn(ContextPanel panel)
    {
        if (!Animate) return;
        panel.Modulate = new Color(1, 1, 1, 0);
        panel.CreateTween().TweenProperty(panel, "modulate:a", 1f, FadeSeconds);
    }

    private void FadeOutAndFree(ContextPanel panel)
    {
        if (!Animate)
        {
            panel.QueueFree();
            return;
        }

        // O tween é do navegador, não do painel: painel desligado não processa os próprios tweens.
        var tween = CreateTween();
        tween.TweenProperty(panel, "modulate:a", 0f, FadeSeconds * 0.7f);
        tween.TweenCallback(Callable.From(panel.QueueFree));
    }
}
