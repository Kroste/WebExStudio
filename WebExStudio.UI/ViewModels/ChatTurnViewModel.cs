using Avalonia.Layout;
using Avalonia.Media;
using ReactiveUI;
using WebExStudio.AI;
using WebExStudio.Core.Localization;
using WebExStudio.Core.Models;

namespace WebExStudio.UI.ViewModels;

/// <summary>Eine Nachricht im Chat-Verlauf (Nutzer, KI oder ein System-Hinweis).</summary>
public sealed class ChatTurnViewModel : ViewModelBase
{
    public ChatRole Role { get; }
    private readonly bool _notice;

    private string _content;
    public string Content
    {
        get => _content;
        set => this.RaiseAndSetIfChanged(ref _content, value);
    }

    public ChatTurnViewModel(ChatRole role, string content, bool notice = false)
    {
        Role = role;
        _content = content;
        _notice = notice;
    }

    public static ChatTurnViewModel Notice(string text) =>
        new(ChatRole.Assistant, text, notice: true);

    public bool IsUser => Role == ChatRole.User;
    public string RoleLabel => Loc.T(_notice ? "Chat_RoleNotice" : IsUser ? "Chat_RoleUser" : "Chat_RoleAi");
    public HorizontalAlignment Align => IsUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public IBrush Bubble => new SolidColorBrush(Color.Parse(
        _notice ? "#0D3A4A" : IsUser ? "#1B3A2E" : "#1A1A2E"));
    public IBrush Border => new SolidColorBrush(Color.Parse(
        _notice ? "#29B6F6" : IsUser ? "#2E7D32" : "#2E2E4E"));

    /// <summary>Textfarbe — Hinweise hell/auffällig, sonst neutral.</summary>
    public IBrush TextBrush => new SolidColorBrush(Color.Parse(_notice ? "#E1F5FE" : "#ECEFF1"));

    /// <summary>Hinweise etwas größer/fetter darstellen.</summary>
    public bool IsNotice => _notice;

    /// <summary>Echte KI-Antwort (nicht Nutzer, kein System-Hinweis) — kann als Hinweis gemerkt werden.</summary>
    public bool CanRemember => Role == ChatRole.Assistant && !_notice;

    // ── Erkannter Flow (falls die Antwort einen enthält) ───────────────────────
    private bool _hasFlow;
    public bool HasFlow
    {
        get => _hasFlow;
        private set => this.RaiseAndSetIfChanged(ref _hasFlow, value);
    }

    private string _flowNote = string.Empty;
    public string FlowNote
    {
        get => _flowNote;
        private set => this.RaiseAndSetIfChanged(ref _flowNote, value);
    }

    public FlowDocument2? Flow { get; private set; }

    /// <summary>Prüft, ob der Antworttext einen Flow enthält, und merkt ihn fürs Laden vor.</summary>
    public void DetectFlow()
    {
        var result = FlowGenerator.Parse(Content);
        if (result.Document is not { Nodes.Count: > 0 } doc) return;

        Flow = doc;
        HasFlow = true;
        FlowNote = result.Validation!.IsValid
            ? string.Format(Loc.T("Chat_FlowDetected"), doc.Nodes.Count)
            : string.Format(Loc.T("Chat_FlowDetectedErr"), doc.Nodes.Count, result.Validation.Errors.Count());
    }
}
