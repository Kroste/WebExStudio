using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class ChatWindow : ChromeWindow
{
    private ChatViewModel? Vm => DataContext as ChatViewModel;

    public ChatWindow() => InitializeComponent();

    public ChatWindow(ChatViewModel vm) : this()
    {
        DataContext = vm;
        vm.Messages.CollectionChanged += (_, _) => ScrollToEnd();
    }


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

    private void OnRememberHint(object? sender, RoutedEventArgs e)
    {
        if (Vm is not null && sender is Control { DataContext: ChatTurnViewModel turn })
            Vm.RememberAsHint(turn);
    }

    private void ScrollToEnd() => Dispatcher.UIThread.Post(() => Scroller.ScrollToEnd());
}
