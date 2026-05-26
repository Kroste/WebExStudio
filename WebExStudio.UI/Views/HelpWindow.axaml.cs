using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace WebExStudio.UI.Views;

public partial class HelpWindow : Window
{
    public HelpWindow() => InitializeComponent();

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnTitleClose(object? _, RoutedEventArgs e) => Close();
}
