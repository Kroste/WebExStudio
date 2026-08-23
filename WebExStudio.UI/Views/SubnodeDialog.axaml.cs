using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace WebExStudio.UI.Views;

public partial class SubnodeDialog : ChromeWindow
{
    public bool Confirmed { get; private set; }
    public string SubnodeName => NameBox.Text?.Trim() ?? string.Empty;
    public string SubnodeLabel => LabelBox.Text?.Trim() ?? string.Empty;

    public SubnodeDialog() : this("Neuer Subnode", "", "") { }

    public SubnodeDialog(string header, string name, string label)
    {
        InitializeComponent();
        WindowTitleBar.Title = header;
        NameBox.Text = name;
        LabelBox.Text = label;
    }


    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SubnodeName)) return;
        Confirmed = true;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
