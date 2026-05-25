using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WebExStudio.AI;
using WebExStudio.Core.Models;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class AiFlowDialog : Window
{
    private readonly MainWindowViewModel _vm;
    private FlowDocument2? _lastDocument;

    public AiFlowDialog() : this(new MainWindowViewModel()) { }

    public AiFlowDialog(MainWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private async void OnGenerate(object? sender, RoutedEventArgs e)
    {
        var prompt = PromptBox.Text?.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            ShowResult("Bitte zuerst eine Beschreibung eingeben.", canLoad: false);
            return;
        }

        if (!_vm.AiOptions.IsConfigured)
        {
            ShowResult("Keine KI-Anbindung konfiguriert. Bitte in den Einstellungen einen "
                + "API-Schlüssel hinterlegen (bzw. Ollama als Anbieter wählen).", canLoad: false);
            return;
        }

        SetBusy(true);
        StatusText.Text = $"Generiere mit {_vm.AiOptions.Provider}…";
        ResultPanel.IsVisible = false;
        LoadAnywayButton.IsVisible = false;
        _lastDocument = null;

        FlowGenerationResult result;
        try
        {
            result = await _vm.GenerateFlowAsync(prompt);
        }
        catch (System.Exception ex)
        {
            SetBusy(false);
            ShowResult($"Unerwarteter Fehler: {ex.Message}", canLoad: false);
            return;
        }
        SetBusy(false);

        if (result.Success)
        {
            Close(); // Dokument wurde bereits in den Editor geladen.
            return;
        }

        _lastDocument = result.Document;

        if (result.Error is not null)
        {
            ShowResult(result.Error, canLoad: _lastDocument is not null);
        }
        else
        {
            var errors = result.Validation?.Errors.Select(i => $"• {i.Message}") ?? [];
            ShowResult("Der erzeugte Flow ist ungültig:\n" + string.Join("\n", errors),
                canLoad: _lastDocument is not null);
        }
    }

    private void OnLoadAnyway(object? sender, RoutedEventArgs e)
    {
        if (_lastDocument is null) return;
        _vm.FlowEditor.LoadDocument(_lastDocument);
        _vm.FlowEditor.MarkDirty();
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void SetBusy(bool busy)
    {
        GenerateButton.IsEnabled = !busy;
        PromptBox.IsEnabled = !busy;
        if (busy) StatusText.Text = "…";
    }

    private void ShowResult(string message, bool canLoad)
    {
        StatusText.Text = string.Empty;
        ResultText.Text = message;
        ResultPanel.IsVisible = true;
        LoadAnywayButton.IsVisible = canLoad;
    }
}
