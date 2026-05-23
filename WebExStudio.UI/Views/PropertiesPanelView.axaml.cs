using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using WebExStudio.Core.Models;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Views;

public partial class PropertiesPanelView : UserControl
{
    private NodeViewModel? _currentNode;
    private FlowEditorViewModel? _flowEditor;

    public PropertiesPanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_flowEditor is not null)
            _flowEditor.PropertyChanged -= OnFlowEditorPropertyChanged;

        _flowEditor = DataContext as FlowEditorViewModel;

        if (_flowEditor is not null)
            _flowEditor.PropertyChanged += OnFlowEditorPropertyChanged;

        _currentNode = _flowEditor?.SelectedNode;
        RebuildForm();
    }

    private void OnFlowEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FlowEditorViewModel.SelectedNode))
        {
            _currentNode = _flowEditor?.SelectedNode;
            RebuildForm();
        }
    }

    private void RebuildForm()
    {
        PropertiesPanel.Children.Clear();

        if (_currentNode is null)
        {
            PropertiesPanel.Children.Add(new TextBlock
            {
                Text = "Kein Node ausgewählt",
                Foreground = new SolidColorBrush(Color.Parse("#546E7A")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Avalonia.Thickness(0, 20, 0, 0),
            });
            return;
        }

        // Node type header
        PropertiesPanel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse(_currentNode.Color + "44")),
            CornerRadius = new Avalonia.CornerRadius(6),
            Padding = new Avalonia.Thickness(10, 6),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = _currentNode.Icon, FontSize = 20 },
                    new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = _currentNode.DisplayName, FontWeight = FontWeight.Bold, Foreground = Brushes.White },
                            new TextBlock { Text = _currentNode.ActionType, FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#90A4AE")) },
                        }
                    }
                }
            }
        });

        // Property fields from definition
        foreach (var prop in _currentNode.Definition.Properties)
            PropertiesPanel.Children.Add(BuildField(prop));
    }

    private Control BuildField(PropertyDefinition prop)
    {
        var config = _currentNode?.Model.Config;
        var currentValue = string.Empty;

        if (config is not null)
        {
            config.TryGetValue(prop.Key, out currentValue);
            if (string.IsNullOrEmpty(currentValue) && prop.Aliases is not null)
            {
                foreach (var alias in prop.Aliases)
                {
                    if (config.TryGetValue(alias, out currentValue) && !string.IsNullOrEmpty(currentValue))
                        break;
                }
            }
        }
        if (string.IsNullOrEmpty(currentValue)) currentValue = prop.DefaultValue ?? string.Empty;

        var label = new TextBlock
        {
            Text = prop.Label + (prop.Required ? " *" : ""),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#B0BEC5")),
            Margin = new Avalonia.Thickness(0, 0, 0, 2),
        };

        Control editor = prop.Kind switch
        {
            PropertyKind.Boolean => new CheckBox
            {
                IsChecked = currentValue == "true",
                Content = prop.Label,
                Foreground = Brushes.White,
            },
            PropertyKind.MultilineText or PropertyKind.Code => new TextBox
            {
                Text = currentValue,
                AcceptsReturn = true,
                MinHeight = 80,
                FontFamily = prop.Kind == PropertyKind.Code ? new FontFamily("Monospace") : FontFamily.Default,
                Watermark = prop.Placeholder,
            },
            PropertyKind.Number => new NumericUpDown
            {
                Value = decimal.TryParse(currentValue, out var d) ? d : 0,
                Increment = 1,
            },
            _ => new TextBox
            {
                Text = currentValue,
                Watermark = prop.Placeholder,
            }
        };

        WireEditorChange(editor, prop.Key, prop.Kind);

        return new StackPanel
        {
            Spacing = 2,
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
            Children = { label, editor }
        };
    }

    private void WireEditorChange(Control editor, string key, PropertyKind kind)
    {
        if (editor is TextBox tb)
        {
            tb.TextChanged += (_, _) =>
            {
                if (_currentNode is not null)
                    _currentNode.Model.Config[key] = tb.Text ?? string.Empty;
            };
        }
        else if (editor is CheckBox cb)
        {
            cb.IsCheckedChanged += (_, _) =>
            {
                if (_currentNode is not null)
                    _currentNode.Model.Config[key] = (cb.IsChecked == true).ToString().ToLowerInvariant();
            };
        }
        else if (editor is NumericUpDown nud)
        {
            nud.ValueChanged += (_, _) =>
            {
                if (_currentNode is not null)
                    _currentNode.Model.Config[key] = ((long)(nud.Value ?? 0)).ToString();
            };
        }
    }
}
