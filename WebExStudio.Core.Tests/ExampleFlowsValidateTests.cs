using WebExStudio.Core.Serialization;
using WebExStudio.Core.Validation;
using Xunit;

namespace WebExStudio.Core.Tests;

/// <summary>
/// Stellt sicher, dass die mitgelieferten Beispiel-Flows unter <c>projects/</c> fehlerfrei
/// durch den <see cref="FlowValidator"/> laufen — fängt sowohl kaputte Beispiele als auch
/// einen zu strengen Validator ab.
/// </summary>
public class ExampleFlowsValidateTests
{
    public static IEnumerable<object[]> ExampleFlows()
    {
        var dir = Path.Combine(RepoRoot(), "projects");
        foreach (var path in Directory.EnumerateFiles(dir, "flow.json", SearchOption.AllDirectories))
            yield return [path];
    }

    [Theory]
    [MemberData(nameof(ExampleFlows))]
    public async Task ExampleFlow_HasNoValidationErrors(string path)
    {
        var doc = await FlowSerializer2.LoadAsync(path);
        var result = FlowValidator.Validate(doc);
        Assert.True(result.IsValid,
            $"{Path.GetFileName(Path.GetDirectoryName(path))}:\n{result}");
    }

    /// <summary>Walks up from the test assembly until the repo root (containing projects/) is found.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "projects")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo-Wurzel (projects/) nicht gefunden.");
    }
}
