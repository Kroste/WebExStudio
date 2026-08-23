using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using WebExStudio.UI.ViewModels;
using WebExStudio.UI.Views;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(WebExStudio.UI.Tests.HeadlessAppBuilder))]

namespace WebExStudio.UI.Tests;

/// <summary>Minimale App für die Headless-Tests — lädt App.axaml (und damit Palette + Styles).</summary>
public static class HeadlessAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

/// <summary>
/// Lädt jedes Fenster wirklich (XAML-Parse, Styles, Resource-Lookups) — ohne Bildschirm.
///
/// WARUM: Ein XAML-Fehler in einem Fenster, das erst auf Klick aufgeht (Einstellungen, Hilfe,
/// Tresor), fällt sonst weder im Build noch in den ViewModel-Tests auf, sondern erst beim Nutzer.
/// Genau das ist beim Umbau auf ChromeWindow/TitleBar das Risiko: die Fenster werden nicht
/// gerendert, nur ihre Konstruktoren liefen bisher nirgends.
/// </summary>
public class WindowSmokeTests
{
    private static MainWindowViewModel NewMainVm() => new();

    [AvaloniaFact]
    public void MainWindow_LaedtUndHatEigeneTitelleiste()
    {
        var window = new MainWindow { DataContext = NewMainVm() };
        window.Show();

        Assert.IsAssignableFrom<ChromeWindow>(window);
        // Kroste-Chrome: BorderOnly (nicht None — sonst fehlen die nativen Resize-Griffe).
        Assert.Equal(WindowDecorations.BorderOnly, window.WindowDecorations);
        Assert.True(window.ExtendClientAreaToDecorationsHint);
        Assert.True(window.CanResize);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Dialoge))]
    public void JedesFenster_LaedtUndIstGroessenveraenderbar(string name, Func<Window> factory)
    {
        var window = factory();
        window.Show();

        Assert.IsAssignableFrom<ChromeWindow>(window);
        Assert.True(window.CanResize, $"{name} ist nicht größenveränderbar (Kroste-Standard: alle Fenster).");
        Assert.Equal(WindowDecorations.BorderOnly, window.WindowDecorations);
        Assert.True(window.ExtendClientAreaToDecorationsHint,
            $"{name} ohne ExtendClientArea — die OS-Caption-Zone würde die eigene Titelleiste tot machen.");
    }

    /// <summary>
    /// Alle Fenster über ihre parameterlosen Konstruktoren — die delegieren an die echten und
    /// setzen sinnvolle Vorgaben, sodass hier kein Testdoppel je Fenster nötig ist.
    /// </summary>
    public static TheoryData<string, Func<Window>> Dialoge() => new()
    {
        { "AboutWindow", () => new AboutWindow() },
        { "HelpWindow", () => new HelpWindow() },
        { "SettingsWindow", () => new SettingsWindow() },
        { "PasswordDialog", () => new PasswordDialog() },
        { "CredentialVaultWindow", () => new CredentialVaultWindow() },
        { "SubnodeDialog", () => new SubnodeDialog() },
        { "ChatWindow", () => new ChatWindow() },
        { "AiFlowDialog", () => new AiFlowDialog() },
        { "NodeSearchDialog", () => new NodeSearchDialog() },
        { "NodeSuggestionDialog", () => new NodeSuggestionDialog() },
    };
}
