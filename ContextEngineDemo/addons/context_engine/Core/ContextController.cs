using System;
using Godot;

namespace ContextEngine;

/// <summary>
/// A pure function from session state to a judgement. Pure means no side effects and no
/// randomness: it can be called on every keystroke to drive the live readout, and it can be
/// unit-tested against a table of expected outputs. Randomness belongs in commit, never here.
/// </summary>
public interface IEvaluator<in TState, out TJudgement>
{
    TJudgement Evaluate(TState state);
}

/// <summary>
/// The brain of one context: resolves its sources, holds the uncommitted session state, runs
/// its evaluator and builds a view-model. Nothing it does touches the world until
/// <see cref="Apply"/> receives <see cref="Committed"/> — closing the panel discards the lot,
/// which is what makes preview-before-commit safe and undo unnecessary.
/// </summary>
public interface IContextModule
{
    ContextDefinition Definition { get; }

    /// <summary>Fresh session state. Called every time the panel opens on this context.</summary>
    void Enter();

    /// <summary>State → view-model. Called after every intent; must not mutate anything.</summary>
    ContextViewModel Build();

    /// <summary>Session state ← intent. The only place state changes.</summary>
    void Apply(Intent intent);

    /// <summary>
    /// The context clock, for contexts that declare one. This is not wall time: pausing is
    /// legal and the evaluator stays pure. Return true when something changed.
    /// </summary>
    bool Tick(double delta) => false;
}

/// <summary>
/// One-way data flow, and the only place it is enforced:
/// controller builds an immutable view-model → panel binds it → primitives emit intents →
/// controller hands them to the module → rebuild. Primitives never read or write game state.
/// </summary>
public sealed partial class ContextController : RefCounted
{
    public IContextModule Module { get; private set; }
    public ContextViewModel ViewModel { get; private set; }

    /// <summary>A new view-model is ready.</summary>
    public event Action Changed;

    /// <summary>The player said yes. The host writes session state into the world here.</summary>
    public event Action<IContextModule> CommitRequested;

    /// <summary>Esc / B. The host closes the panel and discards the session.</summary>
    public event Action CloseRequested;

    public void Load(IContextModule module)
    {
        Module = module;
        Module.Enter();
        Rebuild();
    }

    public void Dispatch(Intent intent)
    {
        if (Module == null) return;

        switch (intent)
        {
            case Committed:
                if (ViewModel?.Region(RegionId.Commit) is CommitVm { Disabled: true }) return;
                Module.Apply(intent);
                Rebuild();
                CommitRequested?.Invoke(Module);
                return;
            case Closed:
                CloseRequested?.Invoke();
                return;
            default:
                Module.Apply(intent);
                Rebuild();
                return;
        }
    }

    /// <summary>Drive from the panel's _Process so context clocks run only while the panel is open.</summary>
    public void Tick(double delta)
    {
        if (Module != null && Module.Tick(delta)) Rebuild();
    }

    public void Rebuild()
    {
        ViewModel = Module?.Build();
        Changed?.Invoke();
    }
}
