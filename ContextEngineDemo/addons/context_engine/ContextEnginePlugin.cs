#if TOOLS
using Godot;

namespace ContextEngine;

/// <summary>
/// Editor plug-in shell. The panel itself needs no editor integration — it is plain C# and is
/// instantiated from game code — but registering the addon keeps <see cref="ContextDefinition"/>
/// and <see cref="RegionSpec"/> visible in the "create new resource" dialog, which is how a
/// designer authors a context without touching the project.
/// </summary>
[Tool]
public partial class ContextEnginePlugin : EditorPlugin
{
    public override void _EnterTree()
    {
        // ContextDefinition and RegionSpec are [GlobalClass] resources, so Godot registers them
        // on its own. Add custom inspector docks or a definition preview here if you want them.
    }

    public override void _ExitTree()
    {
    }
}
#endif
