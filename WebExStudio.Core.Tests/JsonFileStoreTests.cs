using WebExStudio.Core.Storage;
using Xunit;

namespace WebExStudio.Core.Tests;

public class JsonFileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "webex-jsonstore-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* Aufräumen darf den Test nicht rot machen */ }
        GC.SuppressFinalize(this);
    }

    private string Path_(string name) => Path.Combine(_dir, name);

    [Fact]
    public void WriteAtomic_LegtDateiUndVerzeichnisAn()
    {
        var path = Path_("settings.json");

        JsonFileStore.WriteAtomic(path, """{ "a": 1 }""");

        Assert.Equal("""{ "a": 1 }""", File.ReadAllText(path));
    }

    [Fact]
    public void WriteAtomic_UeberschreibtBestehendeDatei()
    {
        var path = Path_("settings.json");
        JsonFileStore.WriteAtomic(path, "alt");

        JsonFileStore.WriteAtomic(path, "neu");

        Assert.Equal("neu", File.ReadAllText(path));
    }

    [Fact]
    public void WriteAtomic_LaesstKeineTempDateiZurueck()
    {
        var path = Path_("settings.json");

        JsonFileStore.WriteAtomic(path, "inhalt");

        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Quarantine_VerschiebtDefekteDateiNachBroken()
    {
        var path = Path_("settings.json");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(path, "{ das ist kein json");

        JsonFileStore.Quarantine(path);

        Assert.False(File.Exists(path));
        Assert.Equal("{ das ist kein json", File.ReadAllText(path + ".broken"));
    }

    [Fact]
    public void Quarantine_UeberschreibtEineAeltereBrokenDatei()
    {
        var path = Path_("settings.json");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(path + ".broken", "älterer Schaden");
        File.WriteAllText(path, "neuer Schaden");

        JsonFileStore.Quarantine(path);

        Assert.Equal("neuer Schaden", File.ReadAllText(path + ".broken"));
    }

    [Fact]
    public void Quarantine_OhneDateiWirftNicht()
    {
        // Darf beim Start ohne vorhandene Konfiguration nicht knallen.
        JsonFileStore.Quarantine(Path_("gibtesnicht.json"));
    }
}
