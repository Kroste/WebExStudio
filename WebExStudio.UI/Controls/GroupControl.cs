using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using WebExStudio.UI.ViewModels;

namespace WebExStudio.UI.Controls;

/// <summary>
/// A labeled, colored box drawn behind a group's member nodes. Only the header strip is
/// interactive (right-click → menu, double-click → rename, left-drag → move the group);
/// the translucent body lets clicks pass through to the canvas and nodes beneath it.
/// </summary>
public sealed class GroupControl : Panel
{
    public GroupViewModel Group { get; }

    private readonly Border _body;
    private readonly Border _header;
    private readonly TextBlock _label;

    /// <summary>Right-click on the header — caller shows the context menu.</summary>
    public event EventHandler<GroupViewModel>? MenuRequested;
    /// <summary>Double-click on the header — caller shows the rename dialog.</summary>
    public event EventHandler<GroupViewModel>? RenameRequested;
    /// <summary>Left-press on the header — caller begins a move-drag of the member nodes.</summary>
    public event EventHandler<(GroupViewModel Group, PointerPressedEventArgs Args)>? MoveStarted;

    public GroupControl(GroupViewModel group)
    {
        Group = group;
        ZIndex = -10; // behind node controls

        var fill = Color.Parse(group.Color);
        _body = new Border
        {
            Background = new SolidColorBrush(fill, 0.12),
            BorderBrush = new SolidColorBrush(fill, 0.85),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(8),
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        _label = new TextBlock
        {
            Text = group.Label,
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 10, 0),
        };
        _header = new Border
        {
            Background = new SolidColorBrush(fill, 0.9),
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            Height = 22,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = new Cursor(StandardCursorType.SizeAll),
            Child = _label,
        };

        Children.Add(_body);
        Children.Add(_header);

        _header.PointerPressed += OnHeaderPressed;
    }

    /// <summary>Position and size the box (world coordinates of the canvas).</summary>
    public void SetBounds(Rect b)
    {
        Canvas.SetLeft(this, b.X);
        Canvas.SetTop(this, b.Y);
        Width = b.Width;
        Height = b.Height;
    }

    public void RefreshLabel() => _label.Text = Group.Label;

    private void OnHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsRightButtonPressed)
        {
            MenuRequested?.Invoke(this, Group);
            e.Handled = true;
        }
        else if (props.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2)
                RenameRequested?.Invoke(this, Group);
            else
                MoveStarted?.Invoke(this, (Group, e));
            e.Handled = true;
        }
    }
}
