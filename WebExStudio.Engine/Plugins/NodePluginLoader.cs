using System.Reflection;
using NLog;
using WebExStudio.Core.Models;
using WebExStudio.Engine.Actions;

namespace WebExStudio.Engine.Plugins;

/// <summary>
/// Lädt Node-Plugins (siehe <see cref="INodePlugin"/>) aus DLLs und registriert sie in
/// <see cref="NodeCatalog"/> (Metadaten) und <see cref="ActionRegistry"/> (Verhalten).
/// Hinweis: Plugins sind beliebiger Code mit vollen App-Rechten — nur vertrauenswürdige laden.
/// </summary>
public static class NodePluginLoader
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static readonly HashSet<string> _loadedAssemblies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Findet alle <see cref="INodePlugin"/>-Implementierungen in einer Assembly und erzeugt deren Nodes.</summary>
    public static IReadOnlyList<NodePluginNode> LoadFromAssembly(Assembly asm)
    {
        var result = new List<NodePluginNode>();
        Type?[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types; }

        foreach (var t in types)
        {
            if (t is null || t.IsAbstract || t.IsInterface || !typeof(INodePlugin).IsAssignableFrom(t)) continue;
            try
            {
                if (Activator.CreateInstance(t) is INodePlugin plugin)
                    foreach (var node in plugin.CreateNodes())
                        if (node?.Definition is not null && node.Handler is not null)
                            result.Add(node);
            }
            catch (Exception ex)
            {
                Log.Warn("Plugin-Typ {0} übersprungen: {1}", t.FullName, ex.Message);
            }
        }
        return result;
    }

    /// <summary>Lädt alle Plugin-DLLs eines Ordners (fehlerisoliert pro DLL).</summary>
    public static IReadOnlyList<NodePluginNode> LoadFromDirectory(string dir)
    {
        var result = new List<NodePluginNode>();
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return result;

        foreach (var dll in Directory.EnumerateFiles(dir, "*.dll"))
        {
            var full = Path.GetFullPath(dll);
            if (!_loadedAssemblies.Add(full)) continue; // schon geladen
            try
            {
                var nodes = LoadFromAssembly(Assembly.LoadFrom(full));
                result.AddRange(nodes);
                if (nodes.Count > 0) Log.Info("Plugin geladen: {0} ({1} Node(s))", Path.GetFileName(dll), nodes.Count);
            }
            catch (Exception ex)
            {
                Log.Warn("Plugin-DLL {0} nicht ladbar: {1}", Path.GetFileName(dll), ex.Message);
            }
        }
        return result;
    }

    /// <summary>Lädt Plugins aus den angegebenen Ordnern und registriert sie in Katalog + Registry.
    /// Liefert die Anzahl neu registrierter Nodes.</summary>
    public static int LoadAndRegister(params string[] dirs)
    {
        var count = 0;
        foreach (var dir in dirs)
            foreach (var node in LoadFromDirectory(dir))
            {
                if (NodeCatalog.Register(node.Definition))
                {
                    ActionRegistry.RegisterPlugin(node.Handler);
                    count++;
                }
                else
                {
                    Log.Warn("Plugin-Node '{0}' übersprungen (Typ bereits vorhanden).", node.Definition.Type);
                }
            }
        if (count > 0) Log.Info("Plugins: {0} Node(s) registriert.", count);
        return count;
    }
}
