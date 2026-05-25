using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WebExStudio.AI;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class NodeSuggestionDialog : Window
{
    private readonly MainWindowViewModel _vm;
    private readonly NodeViewModel _anchor;
    private NodeSuggestion? _suggestion;

    public NodeSuggestionDialog() : this(new MainWindowViewModel(), null!) { }

    public NodeSuggestionDialog(MainWindowViewModel vm, NodeViewModel anchor)
    {
        InitializeComponent();
        _vm = vm;
        _anchor = anchor;
        Opened += async (_, _) => await FetchAsync();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private async Task FetchAsync()
    {
        if (!_vm.AiOptions.IsConfigured)
        {
            ShowError("Keine KI-Anbindung konfiguriert. Bitte in den Einstellungen (Tab „KI“) "
                + "einen API-Schlüssel hinterlegen oder Ollama als Anbieter wählen.");
            return;
        }

        StatusText.Text = $"Frage {_vm.AiOptions.Provider}…";
        NodeSuggestionResult result;
        try
        {
            result = await _vm.SuggestNextNodeAsync(_anchor);
        }
        catch (System.Exception ex)
        {
            ShowError($"Unerwarteter Fehler: {ex.Message}");
            return;
        }

        StatusText.Text = string.Empty;
        if (!result.Success || result.Suggestion is null)
        {
            ShowError(result.Error ?? "Kein Vorschlag erhalten.");
            return;
        }

        _suggestion = result.Suggestion;
        TypeText.Text = _suggestion.Type;
        LabelText.Text = string.IsNullOrWhiteSpace(_suggestion.Label) ? "—" : _suggestion.Label;
        ConfigText.Text = _suggestion.Config.Count == 0
            ? "(keine)"
            : string.Join("\n", _suggestion.Config.Select(kv => $"{kv.Key} = {kv.Value}"));
        ReasonText.Text = _suggestion.Reason;
        ResultPanel.IsVisible = true;
        AddButton.IsEnabled = true;
    }

    private void ShowError(string message)
    {
        StatusText.Text = string.Empty;
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        if (_suggestion is not null)
            _vm.ApplySuggestion(_anchor, _suggestion);
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
