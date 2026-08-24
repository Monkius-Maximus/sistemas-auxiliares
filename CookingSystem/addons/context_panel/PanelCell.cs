using System;
using Godot;

namespace ContextUi;

/// <summary>
/// A célula focável do painel: uma caixa que aceita qualquer conteúdo, entra no grafo de
/// foco e desenha o anel. Existe porque o <c>Button</c> do Godot não compõe filhos com
/// layout próprio — ele exige que o conteúdo seja sobreposto por âncora, e conteúdo
/// sobreposto não faz o botão crescer, então qualquer texto que quebre linha vaza da coluna.
///
/// A célula não repete sozinha ao segurar o botão. Quem repete é o painel: cada unidade
/// adicionada reconstrói a definição inteira e destrói este nó, então um temporizador aqui
/// dentro morreria na primeira repetição.
/// </summary>
public partial class PanelCell : PanelContainer, IPanelCell
{
    /// <summary>A / Enter / clique. Nulo no ladrilho de quantidade: lá o corpo não age.</summary>
    public Action OnActivate { get; init; }

    public Action OnAdd { get; init; }
    public Action OnRemove { get; init; }

    public bool CanIncrement { get; init; }
    public bool CanDecrement { get; init; }

    public Color RingColor { get; init; } = Colors.White;

    /// <summary>Estilos de repouso e de mouse em cima. O segundo é opcional.</summary>
    public StyleBoxFlat Normal { get; init; }
    public StyleBoxFlat Hover { get; init; }

    public void Increment()
    {
        if (CanIncrement) OnAdd?.Invoke();
    }

    public void Decrement()
    {
        if (CanDecrement) OnRemove?.Invoke();
    }

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;

        if (Normal is not null) AddThemeStyleboxOverride("panel", Normal);
        if (Hover is null) return;

        MouseEntered += () => AddThemeStyleboxOverride("panel", Hover);
        MouseExited += () => AddThemeStyleboxOverride("panel", Normal);
    }

    public override void _GuiInput(InputEvent @event)
    {
        // Clicar move o foco para cá mesmo quando não há ação: é assim que o jogador de
        // mouse diz ao painel onde está antes de pegar o controle ou o teclado.
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            GrabFocus();
            OnActivate?.Invoke();
            AcceptEvent();
            return;
        }

        if (OnActivate is not null && @event.IsActionPressed("ui_accept"))
        {
            OnActivate();
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        // Anel por fora da caixa: por dentro ele sumiria contra o preenchimento de acento
        // de uma célula já selecionada.
        if (HasFocus())
            DrawRect(new Rect2(new Vector2(-3, -3), Size + new Vector2(6, 6)), RingColor, false, 2f);
    }
}

/// <summary>
/// Segurar para repetir, acelerando. O primeiro passo é lento o bastante para que um toque
/// curto some exatamente uma unidade; depois acelera, porque encher um recipiente de 20
/// unidades a um clique por vez é a diferença entre um sistema profundo e um tedioso.
/// </summary>
public sealed class HoldRepeat
{
    private const double FirstDelay = 0.38;
    private const double SlowInterval = 0.12;
    private const double FastInterval = 0.035;
    private const double RampTime = 1.4;

    private double _held = -1;
    private double _next;

    public int Direction { get; private set; }

    public bool Active => _held >= 0;

    public void Begin(int direction)
    {
        Direction = direction;
        _held = 0;
        _next = FirstDelay;
    }

    public void End() => _held = -1;

    /// <summary>Quantas repetições disparar neste quadro.</summary>
    public int Advance(double delta)
    {
        if (_held < 0) return 0;
        _held += delta;

        int fired = 0;
        while (_held >= _next && fired < 12)
        {
            fired++;
            double ramp = Math.Clamp((_held - FirstDelay) / RampTime, 0, 1);
            _next += SlowInterval + (FastInterval - SlowInterval) * ramp;
        }
        return fired;
    }
}
