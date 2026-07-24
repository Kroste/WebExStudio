using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WebExStudio.Engine.Plugins;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI;

public partial class App : Application
{
    /// <summary>GC-Referenz auf den Tray-Controller — sonst räumt der GC das TrayIcon still weg.</summary>
    private TrayController? _tray;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Gespeicherte UI-Sprache anwenden (vor dem Fenster, damit Texte gleich stimmen).
            WebExStudio.Core.Localization.Loc.Instance.SetLanguage(AppSettings.LoadLanguage());

            // Plugins VOR dem ViewModel laden, damit die Palette die zusätzlichen Nodes kennt.
            var disabled = new HashSet<string>(AppSettings.LoadDisabledPlugins(), StringComparer.OrdinalIgnoreCase);
            NodePluginLoader.IsDisabled = disabled.Contains;
            NodePluginLoader.LoadAndRegister(AppSettings.PluginDirs);

            var vm = new MainWindowViewModel();
            AppSettings.Load(vm.RunConfig);
            AppSettings.LoadAi(vm.AiOptions);
            vm.InitSuggestionsEnabled(AppSettings.LoadSuggestionsEnabled());
            vm.InitRecentFiles(AppSettings.LoadRecentFiles());
            var window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;

            // Tray NACH der MainWindow-Erzeugung; Instanz als Feld halten (GC-Referenz).
            // Minimieren legt ins Tray, Schließen (✕) beendet regulär.
            _tray = new TrayController(this, desktop, window);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
