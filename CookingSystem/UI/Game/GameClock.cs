using System;

namespace LifeSim.Cooking;

/// <summary>
/// Converte tempo real em horas de jogo. A casa só anda de hora em hora
/// (<c>Household.AdvanceHours</c> age uma hora por vez, e a autonomia decide nessa batida); o
/// relógio guarda a fração que sobra para a tela mostrar os minutos passando entre uma hora e outra.
///
/// Sem Godot dentro: é aritmética, e a regra de velocidade fica testável fora do engine.
/// </summary>
public sealed class GameClock
{
    /// <summary>Segundos reais por hora de jogo na velocidade 1. Um dia inteiro dura 96 s.</summary>
    public const float SecondsPerHour = 4f;

    /// <summary>Multiplicador de cada velocidade. Índice 0 é pausa.</summary>
    private static readonly float[] Multipliers = { 0f, 1f, 3f, 10f };
    public static int FastestSpeed => Multipliers.Length - 1;

    private float _fraction;

    /// <summary>0 pausado, 1 normal, 2 rápido, 3 ultra.</summary>
    public int Speed { get; private set; } = 1;

    /// <summary>A velocidade para onde despausar volta: pausar e despausar não pode zerar a escolha.</summary>
    private int _resumeSpeed = 1;

    /// <summary>Fração da hora corrente que já passou, 0..1. Só para desenhar os minutos.</summary>
    public float HourFraction => _fraction;

    public void SetSpeed(int speed)
    {
        if (speed < 0 || speed > FastestSpeed) throw new ArgumentOutOfRangeException(nameof(speed));
        Speed = speed;
        if (speed > 0) _resumeSpeed = speed;
    }

    public void TogglePause() => SetSpeed(Speed == 0 ? _resumeSpeed : 0);

    /// <summary>
    /// Quantas horas inteiras passaram em <paramref name="seconds"/> reais. Quem chama decide se o
    /// tempo corre — com um painel aberto ou o menu de pausa na tela, simplesmente não chama.
    /// </summary>
    public int Tick(double seconds)
    {
        _fraction += (float)seconds * Multipliers[Speed] / SecondsPerHour;
        int whole = (int)_fraction;
        _fraction -= whole;
        return whole;
    }
}
