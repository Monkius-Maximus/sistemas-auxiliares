using System.Collections.Generic;
using Godot;

namespace ContextEngine;

/// <summary>
/// The contextual button legend along the panel's bottom edge. It shows the live prompts for
/// the region that currently holds focus, in the vocabulary of whichever device was last
/// touched — nothing important in this panel is hover-only, and nothing is left to be guessed.
/// </summary>
public partial class PromptBar : PanelContainer
{
    private HBoxContainer _row;
    private Skin _skin = Skin.Dark;
    private bool _gamepad;
    private IReadOnlyList<(string Pad, string Key, string Label)> _prompts = System.Array.Empty<(string, string, string)>();

    public bool Gamepad
    {
        get => _gamepad;
        set
        {
            if (_gamepad == value) return;
            _gamepad = value;
            Refresh();
        }
    }

    public override void _Ready()
    {
        _row = Ui.HBox(16);
        AddChild(Ui.Pad(_row, 14, 8, 14, 8));
    }

    public void Bind(IReadOnlyList<(string Pad, string Key, string Label)> prompts, Skin skin)
    {
        _prompts = prompts ?? System.Array.Empty<(string, string, string)>();
        _skin = skin;
        AddThemeStyleboxOverride("panel", Ui.Box(skin.Pnl2, skin.Line, 0));
        Refresh();
    }

    private void Refresh()
    {
        if (_row == null) return;

        foreach (var child in _row.GetChildren())
        {
            _row.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var (pad, key, label) in _prompts)
        {
            var group = Ui.HBox(7);
            group.AddChild(Ui.Panel(Ui.Box(_skin.Cell, _skin.Ln2, 1, 6, 3),
                Ui.Text(_gamepad ? pad : key, 10, Ui.Semi, _skin.Ink, 0, true)));
            group.AddChild(Ui.Text(label, 11, Ui.Regular, _skin.Mute));
            _row.AddChild(group);
        }

        _row.AddChild(Ui.Fill());
        _row.AddChild(Ui.Upper(_gamepad ? "Gamepad" : "Keyboard & mouse", 9, _skin.Mute, Ui.Semi, 2));
    }
}
