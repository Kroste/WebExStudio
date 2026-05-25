using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class ChatWindow : Window
{
    private ChatViewModel? Vm => DataContext as ChatViewModel;

    public ChatWindow() => InitializeComponent();

    public ChatWindow(ChatViewModel vm) : this()
    {
        DataContext = vm;
        vm.Messages.CollectionChanged += (_, _) => ScrollToEnd();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnResizePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (sender is Control { Tag: string edgeName } && Enum.TryParse<WindowEdge>(edgeName, out var edge))
            BeginResizeDrag(edge, e);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private async void OnSend(object? sender, RoutedEventArgs e) => await SendAsync();

    private async void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        // Enter = senden; Umschalt+Enter = neue Zeile.
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        if (Vm is null) return;
        await Vm.SendAsync();
        ScrollToEnd();
    }

    private void OnLoadFlow(object? sender, RoutedEventArgs e)
    {
        if (Vm is not null && sender is Control { DataContext: ChatTurnViewModel turn })
            Vm.LoadFlow(turn);
    }

    private void OnLoadLatestFlow(object? sender, RoutedEventArgs e) => Vm?.LoadLatestFlow();

    private void ScrollToEnd() => Dispatcher.UIThread.Post(() => Scroller.ScrollToEnd());
}
