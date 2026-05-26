using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace WebExStudio.UI.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var rawVer = Assembly.GetExecutingAssembly()
                             .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                             ?.InformationalVersion ?? "—";
        VersionText.Text = $"Version {rawVer.Split('+')[0]}";
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnTitleClose(object? _, RoutedEventArgs e) => Close();

    private async void OnHelp(object? _, RoutedEventArgs e) => await new HelpWindow().ShowDialog(this);
}
