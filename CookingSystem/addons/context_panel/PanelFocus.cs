using System;
using System.Collections.Generic;
using Godot;

namespace ContextUi;

/// <summary>
/// As sete regiões, nomeadas. A travessia por controle de teclado do Godot anda célula a
/// célula e não sabe que "ingredientes" e "temperos" são coisas diferentes; o painel anda
/// por região, que é a unidade que o jogador enxerga.
/// </summary>
public enum PanelRegionId
{
    Subject,
    Actions,
    Primary,
    Secondary,
    Preview,
    Readout,
    Commit,
}

/// <summary>
/// O que uma célula focável sabe fazer. Só os ladrilhos de quantidade respondem a
/// incremento; para todo o resto <see cref="CanIncrement"/> é falso e o painel não
/// oferece o atalho no rodapé.
/// </summary>
public interface IPanelCell
{
    bool CanIncrement { get; }
    bool CanDecrement { get; }
    void Increment();
    void Decrement();
}

/// <summary>
/// Coletor preenchido durante o redesenho: cada primitivo registra as células que criou, na
/// ordem em que aparecem, na região que está sendo montada. O painel usa isso para dois
/// fins — andar por região e devolver o foco à mesma posição depois de reconstruir tudo.
///
/// A posição é <c>(região, índice)</c> e não uma referência ao nó: a definição inteira é
/// jogada fora a cada mudança, então guardar o nó só guardaria um objeto morto.
/// </summary>
public sealed class PanelFocus
{
    private readonly Dictionary<PanelRegionId, List<Control>> _cells = new();
    private readonly Dictionary<PanelRegionId, int> _lastIndex = new();

    private PanelRegionId _region = PanelRegionId.Primary;

    /// <summary>Região sendo montada agora. O painel troca antes de cada bloco.</summary>
    public PanelRegionId Building { get; set; } = PanelRegionId.Primary;

    /// <summary>Região que tem o foco. Sobrevive ao redesenho.</summary>
    public PanelRegionId Region
    {
        get => _region;
        set => _region = value;
    }

    public void BeginRebuild() => _cells.Clear();

    public void Register(Control cell)
    {
        if (!_cells.TryGetValue(Building, out var list))
            _cells[Building] = list = new List<Control>();

        int index = list.Count;
        var region = Building;
        list.Add(cell);

        // O jogador também navega com o mouse: clicar numa célula precisa mover a região
        // corrente, senão o próximo LB/RB parte de um lugar que ninguém está olhando.
        cell.FocusEntered += () =>
        {
            _region = region;
            _lastIndex[region] = index;
        };
    }

    public IReadOnlyList<Control> Cells(PanelRegionId region) =>
        _cells.TryGetValue(region, out var list) ? list : Array.Empty<Control>();

    public bool HasCells(PanelRegionId region)
    {
        foreach (var cell in Cells(region))
            if (IsFocusable(cell)) return true;
        return false;
    }

    public Control Current()
    {
        var list = Cells(_region);
        if (list.Count == 0) return null;

        int index = Math.Clamp(_lastIndex.GetValueOrDefault(_region), 0, list.Count - 1);
        return IsFocusable(list[index]) ? list[index] : null;
    }

    /// <summary>
    /// Devolve o foco à célula de onde o jogador saiu. Reentrar numa região não pode jogar
    /// o foco na primeira célula — é o que faz a navegação parecer que perdeu o lugar.
    /// </summary>
    public bool Restore(PanelRegionId region)
    {
        var list = Cells(region);
        if (list.Count == 0) return false;

        int wanted = Math.Clamp(_lastIndex.GetValueOrDefault(region), 0, list.Count - 1);

        for (int step = 0; step < list.Count; step++)
        {
            var cell = list[(wanted + step) % list.Count];
            if (!IsFocusable(cell)) continue;

            _region = region;
            cell.GrabFocus();
            return true;
        }
        return false;
    }

    /// <summary>Célula viva, na árvore, focável e não desabilitada.</summary>
    private static bool IsFocusable(Control cell)
    {
        if (!GodotObject.IsInstanceValid(cell) || !cell.IsInsideTree()) return false;
        if (cell.FocusMode == Control.FocusModeEnum.None) return false;
        return cell is not Button { Disabled: true };
    }
}
