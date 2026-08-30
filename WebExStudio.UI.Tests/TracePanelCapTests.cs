using WebExStudio.Engine;
using WebExStudio.UI.ViewModels;
using Xunit;

namespace WebExStudio.UI.Tests;

/// <summary>
/// Das Protokoll ist gedeckelt. Ohne Deckel wächst es mit jeder Node-Ausführung weiter — eine
/// Schleife über ein paar tausend Elemente treibt Speicher und Renderaufwand ungebremst hoch.
/// </summary>
public class TracePanelCapTests
{
    private static TraceEntry Entry(string id) =>
        new(id, "probe", ExecutionStatus.Success, DateTime.Now, "t", new Dictionary<string, string>());

    [Fact]
    public void UeberDerObergrenze_FallenDieAeltestenRaus()
    {
        var vm = new TracePanelViewModel();

        for (var i = 0; i < TracePanelViewModel.MaxEntries + 25; i++)
            vm.AddEntry(Entry($"n{i}"));

        Assert.Equal(TracePanelViewModel.MaxEntries, vm.Entries.Count);
        Assert.Equal("n25", vm.Entries[0].NodeId);                                  // ältester ist raus
        Assert.Equal($"n{TracePanelViewModel.MaxEntries + 24}", vm.Entries[^1].NodeId); // neuester bleibt
    }

    [Fact]
    public void UnterDerObergrenze_BleibtAllesErhalten()
    {
        var vm = new TracePanelViewModel();
        for (var i = 0; i < 100; i++) vm.AddEntry(Entry($"n{i}"));
        Assert.Equal(100, vm.Entries.Count);
    }
}
