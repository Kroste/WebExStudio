using WebExStudio.Core.Models;
using WebExStudio.Core.Validation;

namespace WebExStudio.AI;

/// <summary>Ergebnis einer KI-Flow-Generierung.</summary>
public sealed class FlowGenerationResult
{
    /// <summary>Das geparste Dokument (null, wenn das Parsen fehlschlug).</summary>
    public FlowDocument2? Document { get; init; }

    /// <summary>Validierungsergebnis des geparsten Dokuments (null, wenn nicht geparst).</summary>
    public FlowValidationResult? Validation { get; init; }

    /// <summary>Die rohe Modellantwort (für Diagnose/Anzeige).</summary>
    public string RawResponse { get; init; } = string.Empty;

    /// <summary>Fehlermeldung, falls die Generierung scheiterte (Netzwerk/Parsen).</summary>
    public string? Error { get; init; }

    /// <summary>True, wenn ein gültiges (fehlerfreies) Dokument erzeugt wurde.</summary>
    public bool Success => Error is null && Document is not null && (Validation?.IsValid ?? false);

    public static FlowGenerationResult Failed(string error, string raw = "") =>
        new() { Error = error, RawResponse = raw };
}
