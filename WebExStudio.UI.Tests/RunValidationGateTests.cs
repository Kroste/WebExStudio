using WebExStudio.UI.ViewModels;
using Xunit;

namespace WebExStudio.UI.Tests;

public class RunValidationGateTests
{
    [Fact]
    public async Task RunAsync_AbortsAndReportsIssues_WhenFlowInvalid()
    {
        var vm = new MainWindowViewModel();
        vm.NewFlow();
        // goto ohne URL → Pflichtfeld fehlt → Validierungsfehler.
        var node = vm.FlowEditor.AddNode("goto", 0, 0);

        await vm.RunAsync();

        // Der Lauf wurde gar nicht erst gestartet.
        Assert.False(vm.IsRunning);
        Assert.Contains("abgebrochen", vm.StatusText);

        // Die Befunde landen im Protokoll-Panel …
        Assert.Contains(vm.TracePanel.Entries, e => e.ActionType == "Validierung");

        // … und der fehlerhafte Node wird rot markiert.
        Assert.Equal(ExecutionStatusUi.Error, node.Status);
    }
}
