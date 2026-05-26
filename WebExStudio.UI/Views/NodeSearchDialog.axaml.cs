using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class NodeSearchDialog : Window
{
    /// <summary>Ein Suchtreffer: Anzeige + Node-ID zum Anspringen.</summary>
    public sealed record Item(string Display, string Sub, string NodeId, string Haystack);

    private readonly List<Item> _all = [];

    /// <summary>Gewählter Node (null = abgebrochen).</summary>
    public string? SelectedNodeId { get; private set; }

    public NodeSearchDialog() => InitializeComponent();

    public NodeSearchDialog(FlowEditorViewModel editor) : this()
    {
        foreach (var tab in editor.Tabs)
            foreach (var n in tab.Nodes)
            {
                var title = string.IsNullOrWhiteSpace(n.Label) ? n.Title : n.Label;
                _all.Add(new Item($"{title}  ·  {n.ActionType}", tab.Label, n.Id,
                    $"{title} {n.ActionType} {tab.Label}".ToLowerInvariant()));
            }
        Apply("");
        SearchBox.Focus();
    }

    private void Apply(string filter)
    {
        var f = filter.Trim().ToLowerInvariant();
        ResultList.ItemsSource = string.IsNullOrEmpty(f)
            ? _all
            : _all.Where(i => i.Haystack.Contains(f)).ToList();
        if (ResultList.ItemCount > 0) ResultList.SelectedIndex = 0;
    }

    private void OnFilter(object? sender, TextChangedEventArgs e) => Apply(SearchBox.Text ?? "");

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Accept(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        else if (e.Key == Key.Down && ResultList.ItemCount > 0)
        {
            ResultList.SelectedIndex = System.Math.Min(ResultList.SelectedIndex + 1, ResultList.ItemCount - 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Up && ResultList.ItemCount > 0)
        {
            ResultList.SelectedIndex = System.Math.Max(ResultList.SelectedIndex - 1, 0);
            e.Handled = true;
        }
    }

    private void OnAccept(object? sender, TappedEventArgs e) => Accept();

    private void Accept()
    {
        if (ResultList.SelectedItem is Item item)
        {
            SelectedNodeId = item.NodeId;
            Close();
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
