using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using WebExStudio.Core.Serialization;
using WebExStudio.UI.ViewModels;
using WebExStudio.UI.Views;

namespace WebExStudio.UI;

public partial class MainWindow : Window
{
    private MainWindowViewModel Vm => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        // Doppelklick auf den Tresor-Node öffnet die Verwaltung.
        FlowEditorView.CredentialVaultRequested += async (_, _) => await OpenVaultAsync();
    }

    // ── Custom title bar ──────────────────────────────────────────────────────

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e) =>
        ToggleMaximize();

    private void OnMinimize(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnToggleMaximize(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        ToggleMaximize();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseWindow(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Close();

    private async void OnAbout(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await new AboutWindow(LoadExampleFlow).ShowDialog(this);

    private async void OnHelp(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await new HelpWindow(LoadExampleFlow).ShowDialog(this);

    /// <summary>Lädt ein Beispiel-JSON aus der Hilfe in den Editor (ersetzt den aktuellen Flow).</summary>
    private void LoadExampleFlow(string json)
    {
        try
        {
            Vm.Vault.Lock();
            var doc = FlowSerializer2.Deserialize(json);
            Vm.FlowEditor.LoadDocument(doc);
            Vm.FlowEditor.MarkDirty();
            Vm.StatusText = "Beispiel aus der Hilfe geladen — bitte prüfen und speichern";
        }
        catch (System.Exception ex)
        {
            Vm.StatusText = $"Beispiel konnte nicht geladen werden: {ex.Message}";
        }
    }

    private async void OnSettings(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(Vm.RunConfig, Vm.AiOptions);
        await dlg.ShowDialog(this);
        if (dlg.Saved)
        {
            AppSettings.Save(Vm.RunConfig);
            AppSettings.SaveAi(Vm.AiOptions);
            Vm.NotifyAiSettingsChanged();
        }
    }

    private void OnNewFlow(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.NewFlow();

    private async void OnGenerateFlow(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await new AiFlowDialog(Vm).ShowDialog(this);

    private ChatWindow? _chatWindow;

    private void OnChat(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ShowChat();

    private async void OnExplainFlow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowChat();
        await Vm.Chat.ExplainCurrentFlowAsync();
    }

    private async void OnSuggestNode(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!Vm.SuggestionsEnabled) return;
        if (Vm.FlowEditor.SelectedNode is not { } anchor)
        {
            Vm.StatusText = "Bitte zuerst einen Node auswählen (Anker für den Vorschlag).";
            return;
        }
        await new NodeSuggestionDialog(Vm, anchor).ShowDialog(this);
    }

    /// <summary>Springt im Editor zu einem Node (aus dem Ausführungsprotokoll aufgerufen).</summary>
    public void FocusNode(string nodeId) => FlowEditorView.FocusNode(nodeId);

    /// <summary>Öffnet den KI-Chat und legt den übergebenen Text ins Eingabefeld (zum Absenden).</summary>
    public void ShowChatWithText(string text)
    {
        ShowChat();
        Vm.Chat.Input = text;
    }

    /// <summary>Öffnet (oder aktiviert) das nicht-modale Chat-Fenster; Verlauf lebt im ViewModel.</summary>
    private void ShowChat()
    {
        if (_chatWindow is null)
        {
            _chatWindow = new ChatWindow(Vm.Chat);
            _chatWindow.Closed += (_, _) => _chatWindow = null;
            _chatWindow.Show(this);
        }
        else
        {
            _chatWindow.Activate();
        }
    }

    private async void OnOpenFlow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Flow-Datei öffnen",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Flow JSON") { Patterns = ["*.json"] }],
        });
        if (files.Count > 0)
            await Vm.OpenFlowAsync(files[0].Path.LocalPath);
    }

    private async void OnSaveFlow(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Vm.FlowEditor.Document?.FilePath is { } existing)
        {
            await Vm.SaveFlowAsync(existing);
            return;
        }
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Flow speichern",
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("Flow JSON") { Patterns = ["*.json"] }],
        });
        if (file is not null)
            await Vm.SaveFlowAsync(file.Path.LocalPath);
    }

    private bool _closing;

    private async void OnRun(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await RunFlowAsync();

    /// <summary>F5 startet den Flow (wie der ▶-Button); F11 schaltet Vollbild um.</summary>
    protected override async void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            e.Handled = true;
            await RunFlowAsync();
            return;
        }
        // F11: Vollbild umschalten. Eigener Pfad (FullScreen statt Maximized) – hilft, wenn der
        // Fenstermanager das Maximieren randloser Fenster nicht zuverlässig umsetzt
        // (z. B. unter manchen Wayland-Compositors).
        if (e.Key == Key.F11)
        {
            WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>Startet den Flow; entsperrt vorher bei Bedarf den Tresor (Secrets im Flow).</summary>
    private async Task RunFlowAsync()
    {
        if (!Vm.CanRun) return;
        if (Vm.CurrentFlowUsesSecrets() && !Vm.Vault.IsUnlocked)
        {
            var dlg = new PasswordDialog("Tresor entsperren",
                "Dieser Flow nutzt Anmeldedaten. Master-Passwort zum Entsperren des Tresors:");
            await dlg.ShowDialog(this);
            if (!dlg.Confirmed) { Vm.StatusText = "Lauf abgebrochen — Tresor nicht entsperrt."; return; }
            try { Vm.Vault.Unlock(dlg.Password); }
            catch { Vm.StatusText = "Tresor: falsches Master-Passwort."; return; }
        }
        Vm.StartRun();
    }

    private async void OnVault(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await OpenVaultAsync();

    private async Task OpenVaultAsync()
    {
        await new CredentialVaultWindow(Vm.Vault).ShowDialog(this);
        // Falls der Tresor jetzt entsperrt ist, den Secret-Picker im Eigenschaften-Panel aktualisieren.
        Vm.FlowEditor.RefreshProperties();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (!_closing)
        {
            _closing = true;

            // Cancel a running flow and give it a moment to release Playwright.
            if (Vm.CanStop)
            {
                e.Cancel = true;
                Vm.StopRun();
                if (Vm.RunTask is { } task)
                    await Task.WhenAny(task, Task.Delay(5000));
            }

            // Playwright spawns foreground driver/browser threads that keep the
            // process alive even after the window closes. Force-terminate to avoid
            // a lingering process that has to be killed manually.
            Environment.Exit(0);
            return;
        }
        base.OnClosing(e);
    }

    private void OnStop(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.StopRun();

    private void OnPause(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.Pause();

    private void OnStep(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.Step();

    private void OnResume(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.Resume();

    private void OnSnapToGrid(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.FlowEditor.SnapAllToGrid();

    private void OnAutoLayout(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.FlowEditor.AutoLayoutActiveTab();

    private void OnUndo(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.FlowEditor.Undo();

    private void OnRedo(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Vm.FlowEditor.Redo();

    private void OnSearchNode(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        FlowEditorView.OpenSearch();

    private void OnRecent(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var flyout = new MenuFlyout();
        if (Vm.RecentFiles.Count == 0)
        {
            flyout.Items.Add(new MenuItem { Header = "(noch keine)", IsEnabled = false });
        }
        else
        {
            foreach (var path in Vm.RecentFiles)
            {
                var p = path;
                var item = new MenuItem { Header = System.IO.Path.GetFileName(p), };
                ToolTip.SetTip(item, p);
                item.Click += async (_, _) => await Vm.OpenFlowAsync(p);
                flyout.Items.Add(item);
            }
        }
        flyout.ShowAt(btn);
    }

    private async void OnConvert(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Alten Flow-Projektordner wählen (Python)",
            AllowMultiple = false,
        });
        if (folders.Count == 0) return;
        var dir = folders[0].Path.LocalPath;
        try
        {
            Vm.Vault.Lock();
            var doc = await Task.Run(() => LegacyImporter.Convert(dir));
            Vm.FlowEditor.LoadDocument(doc);
            Vm.FlowEditor.MarkDirty();
            Vm.StatusText = $"Konvertiert: {doc.Tabs.Count} Tabs, {doc.Nodes.Count} Nodes — bitte prüfen und speichern";
        }
        catch (System.Exception ex)
        {
            Vm.StatusText = $"Konvertierung fehlgeschlagen: {ex.Message}";
        }
    }

    private void OnFitView(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        (this.FindControl<FlowEditorView>("FlowEditorView"))?.FitToView();

    private void OnResetView(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        (this.FindControl<FlowEditorView>("FlowEditorView"))?.ResetView();
}
