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
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Plugins VOR dem ViewModel laden, damit die Palette die zusätzlichen Nodes kennt.
            NodePluginLoader.LoadAndRegister(
                Path.Combine(AppContext.BaseDirectory, "plugins"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WebExStudio", "plugins"));

            var vm = new MainWindowViewModel();
            AppSettings.Load(vm.RunConfig);
            AppSettings.LoadAi(vm.AiOptions);
            vm.InitSuggestionsEnabled(AppSettings.LoadSuggestionsEnabled());
            vm.InitRecentFiles(AppSettings.LoadRecentFiles());
            desktop.MainWindow = new MainWindow { DataContext = vm };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
