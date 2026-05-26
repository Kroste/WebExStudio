using System.Reflection;
using NLog;
using WebExStudio.Core.Models;
using WebExStudio.Engine.Actions;

namespace WebExStudio.Engine.Plugins;

/// <summary>Status eines entdeckten Plugins (für den Plugin-Manager).</summary>
public sealed record PluginInfo(string File, string Path, bool Enabled, int NodeCount, string Status);

/// <summary>
/// Lädt Node-Plugins (siehe <see cref="INodePlugin"/>) aus DLLs in einem isolierten
/// <see cref="PluginLoadContext"/> und registriert sie in <see cref="NodeCatalog"/> + <see cref="ActionRegistry"/>.
/// Hinweis: Plugins sind beliebiger Code mit vollen App-Rechten — nur vertrauenswürdige laden.
/// </summary>
public static class NodePluginLoader
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<PluginInfo> _infos = [];

    /// <summary>Entdeckte Plugins samt Status — für den Plugin-Manager.</summary>
    public static IReadOnlyList<PluginInfo> Plugins => _infos;

    /// <summary>Wird vor dem Laden gesetzt: liefert true für deaktivierte Plugin-Dateinamen.</summary>
    public static Func<string, bool>? IsDisabled { get; set; }

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

    /// <summary>Lädt die Plugin-Nodes eines Ordners (isoliert), ohne sie zu registrieren.</summary>
    public static IReadOnlyList<NodePluginNode> LoadFromDirectory(string dir)
    {
        var result = new List<NodePluginNode>();
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return result;
        foreach (var dll in Directory.EnumerateFiles(dir, "*.dll"))
        {
            try { result.AddRange(LoadFromAssembly(new PluginLoadContext(dll).LoadFromAssemblyPath(Path.GetFullPath(dll)))); }
            catch (Exception ex) { Log.Warn("Plugin-DLL {0} nicht ladbar: {1}", Path.GetFileName(dll), ex.Message); }
        }
        return result;
    }

    /// <summary>Lädt Plugins aus den Ordnern (isoliert, deaktivierte überspringend) und registriert sie.
    /// Liefert die Anzahl neu registrierter Nodes; Status pro DLL in <see cref="Plugins"/>.</summary>
    public static int LoadAndRegister(params string[] dirs)
    {
        var count = 0;
        foreach (var dir in dirs)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) continue;
            foreach (var dll in Directory.EnumerateFiles(dir, "*.dll"))
            {
                var full = Path.GetFullPath(dll);
                if (!_seen.Add(full)) continue; // jede DLL nur einmal
                var file = Path.GetFileName(dll);

                if (IsDisabled?.Invoke(file) == true)
                {
                    _infos.Add(new PluginInfo(file, full, false, 0, "deaktiviert"));
                    continue;
                }
                try
                {
                    var asm = new PluginLoadContext(full).LoadFromAssemblyPath(full);
                    WarnOnApiMismatch(asm, file);
                    var added = 0;
                    foreach (var node in LoadFromAssembly(asm))
                        if (NodeCatalog.Register(node.Definition)) { ActionRegistry.RegisterPlugin(node.Handler); added++; }
                    count += added;
                    _infos.Add(new PluginInfo(file, full, true, added,
                        added > 0 ? $"{added} Node(s) geladen" : "keine neuen Nodes"));
                    if (added > 0) Log.Info("Plugin geladen: {0} ({1} Node(s))", file, added);
                }
                catch (Exception ex)
                {
                    _infos.Add(new PluginInfo(file, full, true, 0, "Fehler: " + ex.Message));
                    Log.Warn("Plugin {0} nicht ladbar: {1}", file, ex.Message);
                }
            }
        }
        if (count > 0) Log.Info("Plugins: {0} Node(s) registriert.", count);
        return count;
    }

    private static void WarnOnApiMismatch(Assembly asm, string file)
    {
        var attr = asm.GetCustomAttribute<WebExStudioPluginAttribute>();
        if (attr is not null && attr.ApiVersion != PluginApi.Version)
            Log.Warn("Plugin {0} ist für API-Version {1} gebaut, aktuell ist {2} — evtl. inkompatibel.",
                file, attr.ApiVersion, PluginApi.Version);
    }
}
