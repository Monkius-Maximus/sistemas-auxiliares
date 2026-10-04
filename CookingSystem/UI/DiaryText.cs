using LifeSim.Household;

namespace LifeSim.Cooking;

/// <summary>Como uma linha do diário da casa é lida pela tela. Uma regra, usada pelo jogo e pelo dev.</summary>
public static class DiaryText
{
    /// <summary>O que pede atenção do jogador: o Sim se machucou ou não tem saída sozinho.</summary>
    public static bool IsAlarm(HouseholdEvent e) =>
        e.Text.Contains("desmaiou") || e.Text.Contains("passou mal") || e.Text.Contains("não há o que comer");
}
