using System.Reflection;
using System.Runtime.Loader;

namespace WebExStudio.Engine.Plugins;

/// <summary>
/// Isolierter Ladekontext für ein Plugin: gemeinsame Host-Assemblies (WebExStudio.*, System.*,
/// Microsoft.*, Avalonia, NLog) kommen aus dem Default-Kontext — damit Plugin und Host DIESELBEN
/// Typen (INodePlugin, IActionHandler, NodeDefinition …) sehen. Private Plugin-Abhängigkeiten
/// werden über die <c>.deps.json</c> aufgelöst, ohne mit Host-Versionen zu kollidieren.
/// </summary>
internal sealed class PluginLoadContext(string pluginPath) : AssemblyLoadContext(isCollectible: false)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    protected override Assembly? Load(AssemblyName name)
    {
        if (IsShared(name.Name)) return null; // → Default-Kontext (gemeinsame Typen)
        var path = _resolver.ResolveAssemblyToPath(name);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    /// <summary>Assemblies, die der Host bereitstellt und die NICHT doppelt geladen werden dürfen.</summary>
    private static bool IsShared(string? name) =>
        name is null
        || name.StartsWith("WebExStudio", StringComparison.Ordinal)
        || name.StartsWith("System", StringComparison.Ordinal)
        || name.StartsWith("Microsoft.", StringComparison.Ordinal)
        || name.StartsWith("Avalonia", StringComparison.Ordinal)
        || name is "netstandard" or "mscorlib" or "NLog";
}
