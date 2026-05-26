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

        // Free-text display name (shown on the node), available for every node type.
        PropertiesPanel.Children.Add(BuildLabelField());

        // Property fields from definition
        foreach (var prop in _currentNode.Definition.Properties)
            PropertiesPanel.Children.Add(BuildField(prop));

        // Description + example box (under the properties)
        PropertiesPanel.Children.Add(BuildInfoBox(_currentNode.Definition));
    }

    private Control BuildLabelField()
    {
        var label = new TextBlock
        {
            Text = "Bezeichnung (Anzeige am Node)",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#B0BEC5")),
            Margin = new Avalonia.Thickness(0, 0, 0, 2),
        };
        var box = new TextBox
        {
            Text = _currentNode?.Label ?? string.Empty,
            PlaceholderText  = "z. B. Login-Button",
        };
        box.TextChanged += (_, _) =>
        {
            if (_currentNode is not null)
            {
                _currentNode.Label = box.Text ?? string.Empty;
                _flowEditor?.MarkDirty();
            }
        };
        return new StackPanel
        {
            Spacing = 2,
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
            Children = { label, box }
        };
    }

    private static Control BuildInfoBox(WebExStudio.Core.Models.NodeDefinition def)
    {
        var panel = new StackPanel { Spacing = 6 };

        panel.Children.Add(new TextBlock
        {
            Text = "ℹ Beschreibung",
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#607D8B")),
        });

        if (!string.IsNullOrEmpty(def.Description))
            panel.Children.Add(new TextBlock
            {
                Text = def.Description,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#B0BEC5")),
                TextWrapping = TextWrapping.Wrap,
            });

        if (!string.IsNullOrEmpty(def.Example))
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Beispiel",
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#607D8B")),
                Margin = new Avalonia.Thickness(0, 4, 0, 0),
            });
            panel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.Parse("#12121F")),
                CornerRadius = new Avalonia.CornerRadius(4),
                Padding = new Avalonia.Thickness(8, 6),
                Child = new TextBlock
                {
                    Text = def.Example,
                    FontSize = 11,
                    FontFamily = new FontFamily("Monospace"),
                    Foreground = new SolidColorBrush(Color.Parse("#80CBC4")),
                    TextWrapping = TextWrapping.Wrap,
                }
            });
        }

        return new Border
        {
            Margin = new Avalonia.Thickness(0, 12, 0, 0),
            Padding = new Avalonia.Thickness(10, 8),
            CornerRadius = new Avalonia.CornerRadius(6),
            Background = new SolidColorBrush(Color.Parse("#16162A")),
            BorderBrush = new SolidColorBrush(Color.Parse("#2A2A4E")),
            BorderThickness = new Avalonia.Thickness(1),
            Child = panel,
        };
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

        // Special case: ai_query.provider → dropdown der gängigen KI-Anbieter (leer = Einstellungen).
        if (_currentNode?.ActionType == "ai_query" && prop.Key == "provider")
        {
            var providers = new List<string> { "", "anthropic", "openai", "gemini", "perplexity", "ollama" };
            if (!string.IsNullOrEmpty(currentValue) && !providers.Contains(currentValue))
                providers.Insert(1, currentValue);
            var combo = new ComboBox { ItemsSource = providers, SelectedItem = currentValue, HorizontalAlignment = HorizontalAlignment.Stretch };
            combo.SelectionChanged += (_, _) =>
            {
                if (_currentNode is not null && combo.SelectedItem is string s)
                {
                    _currentNode.Model.Config[prop.Key] = s;
                    _flowEditor?.MarkDirty();
                }
            };
            return new StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(0, 0, 0, 4), Children = { label, combo } };
        }

        // Special case: call.target → dropdown of available subnode names.
        if (_currentNode?.ActionType == "call" && prop.Key == "target")
        {
            var names = _flowEditor?.SubnodeNames.ToList() ?? [];
            if (!string.IsNullOrEmpty(currentValue) && !names.Contains(currentValue))
                names.Insert(0, currentValue);
            var combo = new ComboBox { ItemsSource = names, SelectedItem = currentValue, HorizontalAlignment = HorizontalAlignment.Stretch };
            combo.SelectionChanged += (_, _) =>
            {
                if (_currentNode is not null && combo.SelectedItem is string s)
                {
                    _currentNode.Model.Config[prop.Key] = s;
                    _currentNode.RaiseTitleChanged();
                    _flowEditor?.MarkDirty();
                }
            };
            return new StackPanel { Spacing = 2, Margin = new Avalonia.Thickness(0, 0, 0, 4), Children = { label, combo } };
        }

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
                PlaceholderText = prop.Placeholder,
            },
            PropertyKind.Number => new NumericUpDown
            {
                Value = decimal.TryParse(currentValue, out var d) ? d : 0,
                Increment = 1,
            },
            _ => new TextBox
            {
                Text = currentValue,
                PlaceholderText  = prop.Placeholder,
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
                if (_currentNode is null) return;
                _currentNode.Model.Config[key] = tb.Text ?? string.Empty;
                _flowEditor?.MarkDirty();
            };
        }
        else if (editor is CheckBox cb)
        {
            cb.IsCheckedChanged += (_, _) =>
            {
                if (_currentNode is null) return;
                _currentNode.Model.Config[key] = (cb.IsChecked == true).ToString().ToLowerInvariant();
                _flowEditor?.MarkDirty();
            };
        }
        else if (editor is NumericUpDown nud)
        {
            nud.ValueChanged += (_, _) =>
            {
                if (_currentNode is null) return;
                _currentNode.Model.Config[key] = ((long)(nud.Value ?? 0)).ToString();
                _flowEditor?.MarkDirty();
            };
        }
    }
}
