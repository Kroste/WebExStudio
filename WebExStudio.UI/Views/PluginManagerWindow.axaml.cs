using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using WebExStudio.Engine.Plugins;

namespace WebExStudio.UI.Views;

public partial class PluginManagerWindow : Window
{
    private readonly HashSet<string> _disabled;

    public PluginManagerWindow()
    {
        InitializeComponent();
        _disabled = new HashSet<string>(AppSettings.LoadDisabledPlugins(), StringComparer.OrdinalIgnoreCase);
        DirsText.Text = "Ordner: " + string.Join("  ·  ", AppSettings.PluginDirs);
        BuildList();
    }

    private void BuildList()
    {
        PluginsHost.Children.Clear();
        var plugins = NodePluginLoader.Plugins;
        if (plugins.Count == 0)
        {
            PluginsHost.Children.Add(new TextBlock
            {
                Text = "Keine Plugins gefunden. DLL in einen der unten genannten Ordner legen und neu starten.",
                Foreground = new SolidColorBrush(Color.Parse("#90A4AE")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(2, 8, 2, 0),
            });
            return;
        }

        foreach (var p in plugins.OrderBy(p => p.File, StringComparer.OrdinalIgnoreCase))
        {
            var toggle = new CheckBox
            {
                IsChecked = !_disabled.Contains(p.File),
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTip.SetTip(toggle, "Aktiv (Häkchen) / deaktiviert — wirkt nach Neustart");
            var file = p.File;
            toggle.IsCheckedChanged += (_, _) =>
            {
                if (toggle.IsChecked == true) _disabled.Remove(file);
                else _disabled.Add(file);
                AppSettings.SaveDisabledPlugins([.. _disabled]);
            };

            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children =
            {
                new TextBlock { Text = p.File, Foreground = Brushes.White, FontSize = 12 },
                new TextBlock { Text = p.Status, Foreground = new SolidColorBrush(Color.Parse("#78909C")), FontSize = 10 },
            }};

            var row = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#16162A")),
                BorderBrush = new SolidColorBrush(Color.Parse("#2A2A4E")),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(6),
                Padding = new Avalonia.Thickness(10, 6),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { toggle, texts } },
            };
            PluginsHost.Children.Add(row);
        }
    }

    private async void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        // Konfig-Plugin-Ordner (zweiter Eintrag) anlegen und öffnen.
        var dir = AppSettings.PluginDirs.Last();
        Directory.CreateDirectory(dir);
        if (TopLevel.GetTopLevel(this) is { } top)
            await top.Launcher.LaunchUriAsync(new Uri(dir));
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
