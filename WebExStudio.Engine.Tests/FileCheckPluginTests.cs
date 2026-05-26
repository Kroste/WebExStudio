using WebExStudio.Core.Models;
using WebExStudio.Engine.Plugins;
using EngineContext = WebExStudio.Engine.ExecutionContext;
using Xunit;

namespace WebExStudio.Engine.Tests;

/// <summary>
/// End-to-End-Test des Beispiel-Plugins: Die gebaute FileCheckPlugin.dll wird in einen Temp-Ordner
/// kopiert und über den echten Plugin-Loader (AssemblyLoadContext) geladen — dann wird der Handler
/// ausgeführt und die Verzweigung geprüft.
/// </summary>
public class FileCheckPluginTests
{
    private static string CopyPluginToTemp()
    {
        // Über die Projektreferenz liegt die DLL im Test-Output → robuster Pfad.
        var src = typeof(FileCheckPlugin.FileCheckPlugin).Assembly.Location;
        var dir = Directory.CreateTempSubdirectory("webex_plugin_").FullName;
        File.Copy(src, Path.Combine(dir, Path.GetFileName(src)));
        var deps = Path.ChangeExtension(src, ".deps.json");
        if (File.Exists(deps)) File.Copy(deps, Path.Combine(dir, Path.GetFileName(deps)));
        return dir;
    }

    [Fact]
    public void LoadsViaAssemblyLoadContext_AndExposesBranchNode()
    {
        var dir = CopyPluginToTemp();
        var node = Assert.Single(NodePluginLoader.LoadFromDirectory(dir), n => n.Definition.Type == "file_exists");
        Assert.True(node.Definition.RoutesOutputs);
        Assert.Equal(2, node.Definition.OutputPorts);
    }

    [Fact]
    public async Task Handler_Branches_FoundVsNotFound()
    {
        var node = Assert.Single(NodePluginLoader.LoadFromDirectory(CopyPluginToTemp()), n => n.Definition.Type == "file_exists");

        var work = Directory.CreateTempSubdirectory("webex_files_").FullName;
        File.WriteAllText(Path.Combine(work, "bericht.pdf"), "x");

        var port = -1;
        var ctx = new EngineContext(page: null!, new TargetConfig { Name = "t" }, new RunConfig(), projectDir: "")
        {
            FollowOutputCallback = (_, p, _) => { port = p; return Task.CompletedTask; },
        };

        await node.Handler.ExecuteAsync(ctx,
            new FlowNode { Type = "file_exists", Config = new() { ["name"] = "*.pdf", ["dir"] = work, ["ctx_key"] = "fp" } });
        Assert.Equal(0, port); // gefunden
        Assert.Equal(Path.Combine(work, "bericht.pdf"), ctx.Get("fp"));

        await node.Handler.ExecuteAsync(ctx,
            new FlowNode { Type = "file_exists", Config = new() { ["name"] = "fehlt.zip", ["dir"] = work } });
        Assert.Equal(1, port); // nicht gefunden
    }
}
