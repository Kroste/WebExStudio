using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WebExStudio.Core.Localization;

namespace WebExStudio.UI.Views;

public partial class PasswordDialog : Window
{
    private readonly bool _confirm;

    public string Password { get; private set; } = string.Empty;
    public bool Confirmed { get; private set; }

    public PasswordDialog() : this(Loc.T("Pw_DefaultTitle"), Loc.T("Pw_DefaultPrompt")) { }

    public PasswordDialog(string title, string prompt, bool confirm = false)
    {
        InitializeComponent();
        _confirm = confirm;
        Title = title;
        TitleText.Text = title;
        PromptText.Text = prompt;
        PwBox2.IsVisible = confirm;
        Opened += (_, _) => PwBox.Focus();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        var pw = PwBox.Text ?? string.Empty;
        if (pw.Length == 0) { ShowError(Loc.T("Pw_EmptyError")); return; }
        if (_confirm && pw != (PwBox2.Text ?? string.Empty)) { ShowError(Loc.T("Pw_MismatchError")); return; }
        Password = pw;
        Confirmed = true;
        Close();
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.IsVisible = true;
    }

    private void OnKeyDownBox(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { OnOk(sender, e); e.Handled = true; }
        else if (e.Key == Key.Escape) { OnCancel(sender, e); e.Handled = true; }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
