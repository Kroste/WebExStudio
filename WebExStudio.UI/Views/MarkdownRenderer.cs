using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace WebExStudio.UI.Views;

/// <summary>Ein Inline-Textsegment mit Formatierungs-Flags.</summary>
public readonly record struct InlineSeg(string Text, bool Bold, bool Code);

/// <summary>
/// Reine (UI-freie) Markdown-Hilfsfunktionen — für Tests gut zugänglich.
/// </summary>
public static class SimpleMarkdown
{
    /// <summary>Wandelt Markdown-Links <c>[Text](url)</c> in reinen Text um.</summary>
    public static string StripLinks(string s) =>
        Regex.Replace(s, @"\[([^\]]*)\]\([^)]*\)", "$1");

    /// <summary>Zerlegt eine Tabellenzeile <c>| a | b |</c> in getrimmte Zellen.</summary>
    public static string[] SplitTableRow(string line)
    {
        var t = line.Trim();
        if (t.StartsWith('|')) t = t[1..];
        if (t.EndsWith('|')) t = t[..^1];
        var cells = t.Split('|');
        for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim();
        return cells;
    }

    /// <summary>Ist die Zeile eine Tabellen-Trennzeile (<c>|---|:--|</c>)?</summary>
    public static bool IsTableSeparator(string line)
    {
        var t = line.Trim();
        if (!t.Contains('|') || !t.Contains('-')) return false;
        foreach (var ch in t)
            if (ch is not ('|' or '-' or ':' or ' ')) return false;
        return true;
    }

    /// <summary>Parst **fett** und `code` zu Segmenten (Links vorher mit StripLinks entfernen).</summary>
    public static List<InlineSeg> ParseInline(string text)
    {
        var segs = new List<InlineSeg>();
        var sb = new StringBuilder();
        bool bold = false, code = false;
        void Flush()
        {
            if (sb.Length > 0) { segs.Add(new InlineSeg(sb.ToString(), bold, code)); sb.Clear(); }
        }
        for (int i = 0; i < text.Length; i++)
        {
            if (!code && i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*') { Flush(); bold = !bold; i++; continue; }
            if (text[i] == '`') { Flush(); code = !code; continue; }
            sb.Append(text[i]);
        }
        Flush();
        return segs;
    }
}

/// <summary>Rendert (vereinfachtes) Markdown in Avalonia-Controls für das Hilfefenster.</summary>
public static class MarkdownRenderer
{
    private static readonly IBrush Body = new SolidColorBrush(Color.Parse("#B0BEC5"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#78909C"));
    private static readonly IBrush Accent = new SolidColorBrush(Color.Parse("#4FC3F7"));
    private static readonly IBrush Accent2 = new SolidColorBrush(Color.Parse("#80CBC4"));
    private static readonly IBrush Line = new SolidColorBrush(Color.Parse("#2E2E4E"));
    private static readonly IBrush HeaderBg = new SolidColorBrush(Color.Parse("#1E2A38"));
    private static readonly FontFamily Mono = new("Monospace");

    public static Control Build(string markdown)
    {
        var root = new StackPanel { Spacing = 3 };
        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        int i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            // Code-Block ```
            if (trimmed.StartsWith("```"))
            {
                var code = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```")) code.AppendLine(lines[i++]);
                i++; // schließendes ```
                root.Children.Add(CodeBlock(code.ToString().TrimEnd('\n')));
                continue;
            }

            // Tabelle: aktuelle Zeile mit '|' und nächste Zeile ist Trennzeile
            if (trimmed.StartsWith('|') && i + 1 < lines.Length && SimpleMarkdown.IsTableSeparator(lines[i + 1]))
            {
                var header = SimpleMarkdown.SplitTableRow(line);
                i += 2; // Kopf + Trennzeile
                var rows = new List<string[]>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith('|'))
                    rows.Add(SimpleMarkdown.SplitTableRow(lines[i++]));
                root.Children.Add(Table(header, rows));
                continue;
            }

            // Bilder überspringen (nicht eingebettet)
            if (trimmed.StartsWith("!["))
            {
                i++;
                continue;
            }

            // Horizontale Linie
            if (trimmed is "---" or "***" or "___")
            {
                root.Children.Add(new Border { Height = 1, Background = Line, Margin = new Thickness(0, 8) });
                i++;
                continue;
            }

            // Überschriften
            if (trimmed.StartsWith('#'))
            {
                int level = 0;
                while (level < trimmed.Length && trimmed[level] == '#') level++;
                var text = trimmed[level..].Trim();
                root.Children.Add(Heading(text, level));
                i++;
                continue;
            }

            // Blockzitat
            if (trimmed.StartsWith("> "))
            {
                root.Children.Add(new Border
                {
                    BorderBrush = Accent2,
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Padding = new Thickness(8, 2, 0, 2),
                    Margin = new Thickness(0, 2),
                    Child = Inline(trimmed[2..], 12, Muted),
                });
                i++;
                continue;
            }

            // Aufzählung
            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                root.Children.Add(Bullet(trimmed[2..]));
                i++;
                continue;
            }

            // Leerzeile → kleiner Abstand
            if (trimmed.Length == 0)
            {
                root.Children.Add(new Control { Height = 4 });
                i++;
                continue;
            }

            // Absatz
            root.Children.Add(Inline(trimmed, 12, Body));
            i++;
        }

        return root;
    }

    private static TextBlock Heading(string text, int level)
    {
        var (size, brush) = level switch
        {
            1 => (20.0, Accent),
            2 => (16.0, Accent),
            3 => (13.5, Accent2),
            _ => (12.5, new SolidColorBrush(Color.Parse("#90A4AE"))),
        };
        var tb = Inline(text, size, brush);
        tb.FontWeight = FontWeight.Bold;
        tb.Margin = new Thickness(0, level <= 2 ? 12 : 8, 0, 3);
        return tb;
    }

    private static Control Bullet(string text)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(6, 0, 0, 2) };
        var dot = new TextBlock { Text = "•  ", FontSize = 12, Foreground = Accent2 };
        var body = Inline(text, 12, Body);
        Grid.SetColumn(dot, 0);
        Grid.SetColumn(body, 1);
        grid.Children.Add(dot);
        grid.Children.Add(body);
        return grid;
    }

    private static Border CodeBlock(string code) => new()
    {
        Background = new SolidColorBrush(Color.Parse("#12121F")),
        BorderBrush = Line,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(8, 6),
        Margin = new Thickness(0, 3),
        Child = new TextBlock { Text = code, FontFamily = Mono, FontSize = 11, Foreground = Accent2, TextWrapping = TextWrapping.Wrap },
    };

    private static Control Table(string[] header, List<string[]> rows)
    {
        var cols = header.Length;
        foreach (var r in rows) cols = System.Math.Max(cols, r.Length);

        var grid = new Grid();
        for (int c = 0; c < cols; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int r = 0; r <= rows.Count; r++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        void Cell(string text, int row, int col, bool head)
        {
            var content = Inline(text, head ? 11.5 : 11, head ? Brushes.White : Body);
            if (head) content.FontWeight = FontWeight.Bold;
            var border = new Border
            {
                Background = head ? HeaderBg : null,
                BorderBrush = Line,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(6, 4),
                Child = content,
            };
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            grid.Children.Add(border);
        }

        for (int c = 0; c < cols; c++) Cell(c < header.Length ? header[c] : "", 0, c, true);
        for (int r = 0; r < rows.Count; r++)
            for (int c = 0; c < cols; c++)
                Cell(c < rows[r].Length ? rows[r][c] : "", r + 1, c, false);

        return new Border
        {
            BorderBrush = Line,
            BorderThickness = new Thickness(1, 1, 0, 0),
            Margin = new Thickness(0, 4),
            Child = grid,
        };
    }

    private static TextBlock Inline(string text, double size, IBrush brush)
    {
        var tb = new TextBlock { FontSize = size, Foreground = brush, TextWrapping = TextWrapping.Wrap };
        foreach (var seg in SimpleMarkdown.ParseInline(SimpleMarkdown.StripLinks(text)))
        {
            var run = new Run(seg.Text);
            if (seg.Bold) run.FontWeight = FontWeight.Bold;
            if (seg.Code) { run.FontFamily = Mono; run.Foreground = Accent2; }
            tb.Inlines!.Add(run);
        }
        return tb;
    }
}
