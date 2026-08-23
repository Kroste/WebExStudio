using NLog;

namespace WebExStudio.Core.Storage;

/// <summary>
/// Gemeinsame Datei-Primitiven für die JSON-Ablagen der App (Einstellungen, Zustände, …).
///
/// Zwei Regeln, die ein nacktes <c>File.WriteAllText</c>/<c>ReadAllText</c> verletzt:
///
/// 1. <b>Atomar schreiben.</b> Ein Schreiben direkt auf die Zieldatei lässt bei Absturz oder
///    Stromausfall mitten im Vorgang eine halbe Datei zurück. Stattdessen erst nach
///    <c>&lt;datei&gt;.tmp</c>, dann <c>File.Move(tmp, ziel, overwrite: true)</c> — das Move
///    ist atomar.
///
/// 2. <b>Defekte Daten nicht stillschweigend verlieren.</b> Ließ sich die Datei nicht
///    deserialisieren, wurde bisher einfach leer weitergestartet — der nächste Speichervorgang
///    hat die kaputte Datei dann endgültig überschrieben. Jetzt wandert sie nach
///    <c>&lt;datei&gt;.broken</c> und bleibt für Diagnose/Rettung erhalten.
///
/// Bewusst NICHT quarantänisiert wird bei IO-Fehlern (Datei gesperrt, Netzlaufwerk kurz weg):
/// dort ist der Inhalt in Ordnung, nur gerade nicht lesbar. Ein Verschieben würde intakte
/// Daten aus dem Weg räumen — genau der Verlust, den die Regel verhindern soll.
/// </summary>
public static class JsonFileStore
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Schreibt <paramref name="json"/> atomar nach <paramref name="path"/>.
    /// Legt das Zielverzeichnis an, falls nötig.
    /// </summary>
    public static void WriteAtomic(string path, string json)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        try
        {
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            // Halb geschriebene .tmp nicht liegen lassen — sie würde beim nächsten Versuch
            // zwar überschrieben, aber Restmüll im Konfig-Ordner verwirrt bei der Fehlersuche.
            TryDeleteTemp(tmp);
            throw;
        }
    }

    /// <summary>
    /// Verschiebt eine nicht deserialisierbare Datei nach <c>&lt;datei&gt;.broken</c>.
    /// Schlägt das fehl (z. B. Datei gesperrt), wird nur geloggt — der Aufrufer startet
    /// in jedem Fall leer weiter.
    /// </summary>
    public static void Quarantine(string path)
    {
        var broken = path + ".broken";
        try
        {
            File.Move(path, broken, overwrite: true);
            Log.Error("Defekte Datei nach {0} gesichert. Es wird leer weitergestartet.", broken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Defekte Datei {0} konnte nicht nach {1} gesichert werden.", path, broken);
        }
    }

    private static void TryDeleteTemp(string tmp)
    {
        try
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Temporäre Datei {0} konnte nicht aufgeräumt werden.", tmp);
        }
    }
}
