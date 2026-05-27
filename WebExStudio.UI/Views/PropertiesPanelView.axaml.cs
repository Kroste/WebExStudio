using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using WebExStudio.Core.Localization;
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
        if (e.PropertyName is nameof(FlowEditorViewModel.SelectedNode)
            or nameof(FlowEditorViewModel.PreviewDefinition))
        {
            _currentNode = _flowEditor?.SelectedNode;
            RebuildForm();
        }
    }

    private void RebuildForm()
    {
        PropertiesPanel.Children.Clear();

        // Palette-Vorschau hat Vorrang: nur lesen, kein Bearbeiten (noch kein Node im Flow).
        if (_flowEditor?.PreviewDefinition is { } preview)
        {
            BuildPreview(preview);
            return;
        }

        if (_currentNode is null)
        {
            PropertiesPanel.Children.Add(new TextBlock
            {
                Text = Loc.T("Prop_NoNode"),
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

        // Universelle Fehlerbehandlung (für jeden Node): Wiederholversuche bei Fehlern.
        // BuildField bindet automatisch an Model.Config[Key].
        PropertiesPanel.Children.Add(new TextBlock
        {
            Text = Loc.T("Prop_ErrorHandling"),
            FontWeight = FontWeight.SemiBold,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#607D8B")),
            Margin = new Avalonia.Thickness(0, 10, 0, 2),
        });
        PropertiesPanel.Children.Add(BuildField(new PropertyDefinition
        {
            Key = "retry", Label = Loc.T("Prop_Retry"), Kind = PropertyKind.Number, DefaultValue = "0",
        }));
        PropertiesPanel.Children.Add(BuildField(new PropertyDefinition
        {
            Key = "retry_delay_ms", Label = Loc.T("Prop_RetryDelay"), Kind = PropertyKind.Number, DefaultValue = "0",
        }));

        // Description + example box (under the properties)
        PropertiesPanel.Children.Add(BuildInfoBox(_currentNode.Definition));
    }

    /// <summary>Nur-Lese-Vorschau eines Palette-Nodes: Kopf, Eigenschaften, Beschreibung/Beispiel.</summary>
    private void BuildPreview(NodeDefinition def)
    {
        PropertiesPanel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse(def.Color + "44")),
            CornerRadius = new Avalonia.CornerRadius(6),
            Padding = new Avalonia.Thickness(10, 6),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = def.Icon, FontSize = 20 },
                    new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = def.DisplayName, FontWeight = FontWeight.Bold, Foreground = Brushes.White },
                            new TextBlock { Text = def.Type, FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#90A4AE")) },
                        }
                    }
                }
            }
        });

        PropertiesPanel.Children.Add(new Border
        {
            Margin = new Avalonia.Thickness(0, 8, 0, 4),
            Padding = new Avalonia.Thickness(8, 6),
            CornerRadius = new Avalonia.CornerRadius(4),
            Background = new SolidColorBrush(Color.Parse("#1B2A1B")),
            Child = new TextBlock
            {
                Text = "👁 Vorschau — zum Bearbeiten per Drag & Drop in den Flow ziehen.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.Parse("#A5D6A7")),
                TextWrapping = TextWrapping.Wrap,
            }
        });

        if (def.Properties.Count > 0)
        {
            PropertiesPanel.Children.Add(new TextBlock
            {
                Text = "Eigenschaften",
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#607D8B")),
                Margin = new Avalonia.Thickness(0, 6, 0, 2),
            });
            foreach (var prop in def.Properties)
                PropertiesPanel.Children.Add(BuildPreviewField(prop));
        }

        PropertiesPanel.Children.Add(BuildInfoBox(def));
    }

    private static Control BuildPreviewField(PropertyDefinition prop)
    {
        var name = new TextBlock
        {
            Text = prop.Label + (prop.Required ? " *" : ""),
            FontSize = 12,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
        };
        var bits = new List<string> { prop.Key, KindName(prop.Kind) };
        if (!string.IsNullOrEmpty(prop.DefaultValue)) bits.Add("Standard: " + Shorten(prop.DefaultValue!));
        else if (!string.IsNullOrEmpty(prop.Placeholder)) bits.Add("z. B. " + prop.Placeholder);
        var detail = new TextBlock
        {
            Text = string.Join("  ·  ", bits),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#78909C")),
            TextWrapping = TextWrapping.Wrap,
        };
        return new StackPanel { Spacing = 1, Margin = new Avalonia.Thickness(0, 0, 0, 6), Children = { name, detail } };
    }

    private static string KindName(PropertyKind k) => k switch
    {
        PropertyKind.Boolean => "ja/nein",
        PropertyKind.Number => "Zahl",
        PropertyKind.Selector => "Selektor",
        PropertyKind.Url => "URL",
        PropertyKind.FilePath => "Pfad",
        PropertyKind.Dropdown => "Auswahl",
        PropertyKind.MultilineText or PropertyKind.Code => "Text (mehrzeilig)",
        _ => "Text",
    };

    private static string Shorten(string s) =>
        (s.Length > 40 ? s[..40] + "…" : s).Replace("\n", " ");

    private Control BuildLabelField()
    {
        var label = new TextBlock
        {
            Text = Loc.T("Prop_LabelField"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#B0BEC5")),
            Margin = new Avalonia.Thickness(0, 0, 0, 2),
        };
        var box = new TextBox
        {
            Text = _currentNode?.Label ?? string.Empty,
            PlaceholderText  = Loc.T("Prop_LabelPlaceholder"),
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
            Text = Loc.T("Prop_Description"),
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
                Text = Loc.T("Prop_Example"),
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

        var stack = new StackPanel
        {
            Spacing = 2,
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
            Children = { label, editor }
        };

        // Secret-Picker für Felder, in denen {secret[..]} aufgelöst wird.
        if (editor is TextBox secretTarget && IsSecretField(_currentNode?.ActionType, prop.Key))
            stack.Children.Add(BuildSecretPicker(secretTarget));

        return stack;
    }

    /// <summary>Felder, in denen Secrets sinnvoll sind (= die Handler, die {secret[..]} auflösen).</summary>
    private static bool IsSecretField(string? actionType, string key) => (actionType, key) switch
    {
        ("send_keys", "value") => true,
        ("goto", "url") => true,
        ("select_option", "value") => true,
        ("click", "text") => true,
        _ => false,
    };

    /// <summary>Dropdown, das einen {secret[name].field}-Platzhalter ins Zielfeld einfügt.</summary>
    private Control BuildSecretPicker(TextBox target)
    {
        var secrets = _flowEditor?.AvailableSecrets?.Invoke() ?? [];
        if (secrets.Count == 0)
            return new TextBlock
            {
                Text = "🔐 Keine Secrets verfügbar — im Tresor (🔐) anlegen/entsperren.",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.Parse("#607D8B")),
                TextWrapping = TextWrapping.Wrap,
            };

        var items = new List<string> { "🔐 Secret einfügen…" };
        items.AddRange(secrets);
        var combo = new ComboBox
        {
            ItemsSource = items,
            SelectedIndex = 0,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex <= 0 || combo.SelectedItem is not string placeholder) return;
            var text = target.Text ?? string.Empty;
            var caret = Math.Clamp(target.CaretIndex, 0, text.Length);
            target.Text = text.Insert(caret, placeholder); // TextChanged aktualisiert die Config
            target.CaretIndex = caret + placeholder.Length;
            combo.SelectedIndex = 0; // zurücksetzen
        };
        return combo;
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
