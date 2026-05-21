using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Controls;

/// <summary>
/// Visual representation of a single node on the canvas.
/// Renders as a rounded rectangle with icon, title, status indicator,
/// and an expand/collapse button for nodes that have sub-actions.
/// </summary>
public sealed class NodeControl : Border
{
    public NodeViewModel ViewModel { get; }
    public event EventHandler<NodeViewModel>? DeleteRequested;

    private readonly Border _statusIndicator;
    private readonly TextBlock _titleLabel;
    private readonly Border _header;
    private readonly Button? _expandBtn;

    public NodeControl(NodeViewModel vm)
    {
        ViewModel = vm;
        Width = vm.Width;
        Height = vm.Height;
        CornerRadius = new CornerRadius(8);
        BorderThickness = new Thickness(2);
        Cursor = new Cursor(StandardCursorType.SizeAll);

        // ── Status indicator (left strip) ───────────────────────────────────
        _statusIndicator = new Border
        {
            Width = 4,
            VerticalAlignment = VerticalAlignment.Stretch,
            CornerRadius = new CornerRadius(6, 0, 0, 6),
        };

        // ── Expand/collapse button (only for nodes with sub-actions) ─────────
        if (vm.HasSubActions)
        {
            _expandBtn = new Button
            {
                Content = vm.IsExpanded ? "▼" : "▶",
                Width = 22,
                Height = 22,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 10,
                Background = new SolidColorBrush(Color.Parse("#00000040")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            _expandBtn.Click += (_, e) =>
            {
                vm.IsExpanded = !vm.IsExpanded;
                _expandBtn.Content = vm.IsExpanded ? "▼" : "▶";
                e.Handled = true;
            };
        }

        // ── Title label ──────────────────────────────────────────────────────
        _titleLabel = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        // ── Header with icon + title (+ optional expand button) ──────────────
        var headerContent = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };
        var iconAndTitle = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = vm.Icon,
                    FontSize = 16,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                _titleLabel,
            }
        };
        Grid.SetColumn(iconAndTitle, 0);
        headerContent.Children.Add(iconAndTitle);

        if (_expandBtn is not null)
        {
            Grid.SetColumn(_expandBtn, 1);
            headerContent.Children.Add(_expandBtn);
        }

        _header = new Border
        {
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            Padding = new Thickness(8, 0, 4, 0),
            Child = headerContent,
        };

        Child = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("4,*"),
            Children =
            {
                _statusIndicator,
                new DockPanel
                {
                    [Grid.ColumnProperty] = 1,
                    Children =
                    {
                        _header,
                        new TextBlock
                        {
                            [DockPanel.DockProperty] = Dock.Bottom,
                            Text = vm.ActionType,
                            FontSize = 10,
                            Foreground = new SolidColorBrush(Color.Parse("#90A4AE")),
                            Margin = new Thickness(8, 2, 8, 4),
                        }
                    }
                }
            }
        };

        ContextMenu = BuildContextMenu();
        vm.PropertyChanged += OnVmPropertyChanged;
        UpdateVisuals();
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeViewModel.IsExpanded) && _expandBtn is not null)
            _expandBtn.Content = ViewModel.IsExpanded ? "▼" : "▶";
        else if (e.PropertyName is nameof(NodeViewModel.IsSelected)
            or nameof(NodeViewModel.IsActive)
            or nameof(NodeViewModel.Status)
            or nameof(NodeViewModel.StatusColor)
            or nameof(NodeViewModel.Color))
        {
            UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        var baseColor = Color.Parse(ViewModel.Color);
        var statusColor = Color.Parse(ViewModel.StatusColor);

        Background = new SolidColorBrush(Color.FromArgb(220,
            (byte)(baseColor.R / 3),
            (byte)(baseColor.G / 3),
            (byte)(baseColor.B / 3)));

        _header.Background = new SolidColorBrush(Color.FromArgb(200,
            baseColor.R, baseColor.G, baseColor.B));

        _statusIndicator.Background = new SolidColorBrush(statusColor);
        _titleLabel.Text = ViewModel.DisplayName;

        if (ViewModel.IsActive)
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#FFC107"));
            BoxShadow = new BoxShadows(new BoxShadow
            {
                Blur = 12, Color = Color.Parse("#FFC107"),
                OffsetX = 0, OffsetY = 0, IsInset = false,
            });
        }
        else if (ViewModel.IsSelected)
        {
            BorderBrush = new SolidColorBrush(Colors.White);
            BoxShadow = BoxShadows.Parse("0 0 8 #FFFFFF44");
        }
        else
        {
            BorderBrush = new SolidColorBrush(Color.Parse(ViewModel.Color));
            BoxShadow = BoxShadows.Parse("0 2 6 #00000066");
        }
    }

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();

        var deleteItem = new MenuItem { Header = "🗑 Node löschen" };
        deleteItem.Click += (_, _) => DeleteRequested?.Invoke(this, ViewModel);
        menu.Items.Add(deleteItem);

        return menu;
    }
}
