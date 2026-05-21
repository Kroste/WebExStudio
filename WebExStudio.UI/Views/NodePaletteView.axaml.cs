using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using WebExStudio.Core.Models;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class NodePaletteView : UserControl
{
    private string _search = string.Empty;

    public NodePaletteView()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private MainWindowViewModel? MainVm => DataContext as MainWindowViewModel;

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _search = SearchBox.Text ?? string.Empty;
        Refresh();
    }

    private void Refresh()
    {
        PaletteContent.Children.Clear();

        var filtered = NodeCatalog.All
            .Where(d => string.IsNullOrEmpty(_search) ||
                        d.DisplayName.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                        d.Type.Contains(_search, StringComparison.OrdinalIgnoreCase));

        foreach (var group in filtered.GroupBy(d => d.Category))
        {
            // Category header
            PaletteContent.Children.Add(new TextBlock
            {
                Text = group.Key,
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#607D8B")),
                FontSize = 11,
                Margin = new Avalonia.Thickness(4, 8, 4, 2),
            });

            foreach (var def in group)
            {
                var d = def;
                var item = BuildPaletteItem(d);
                item.PointerPressed += (_, e) =>
                {
                    if (e.GetCurrentPoint(item).Properties.IsLeftButtonPressed)
                        MainVm?.FlowEditor.AddNode(d.Type, 200, 80 + MainVm.FlowEditor.Nodes.Count * 90.0);
                };
                PaletteContent.Children.Add(item);
            }
        }
    }

    private static Border BuildPaletteItem(NodeDefinition def)
    {
        var accent = Color.Parse(def.Color);
        var border = new Border
        {
            Margin = new Avalonia.Thickness(2, 1),
            Padding = new Avalonia.Thickness(8, 6),
            CornerRadius = new Avalonia.CornerRadius(6),
            Cursor = new Avalonia.Input.Cursor(StandardCursorType.Hand),
            Background = new SolidColorBrush(Color.FromArgb(40, accent.R, accent.G, accent.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, accent.R, accent.G, accent.B)),
            BorderThickness = new Avalonia.Thickness(1),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = def.Icon,
                        FontSize = 16,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = def.DisplayName,
                                Foreground = Brushes.White,
                                FontSize = 12,
                            },
                            new TextBlock
                            {
                                Text = def.Description,
                                Foreground = new SolidColorBrush(Color.Parse("#607D8B")),
                                FontSize = 10,
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                                MaxWidth = 155,
                            },
                        }
                    }
                }
            }
        };

        border.PointerEntered += (_, _) =>
            border.Background = new SolidColorBrush(Color.FromArgb(80, accent.R, accent.G, accent.B));
        border.PointerExited += (_, _) =>
            border.Background = new SolidColorBrush(Color.FromArgb(40, accent.R, accent.G, accent.B));

        return border;
    }
}
