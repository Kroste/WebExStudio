using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.LayoutRenderers.Wrappers;

namespace WebExStudio.Core.Logging;

/// <summary>
/// NLog-Wrapper-Renderer <c>${masked:inner=...}</c>: schickt den gerenderten Innen-Text durch
/// <see cref="SecretMasker"/>, damit Passwörter/Tokens/API-Keys NIE im Klartext ins Log gelangen —
/// auch dann nicht, wenn eine einzelne Log-Stelle das manuelle Maskieren vergisst. Damit wird die
/// Maskierung zur Pipeline-Eigenschaft statt zur Bringschuld jedes Aufrufers.
///
/// Die Registrierung läuft über einen <see cref="ModuleInitializerAttribute"/> und damit beim
/// Laden dieses Assemblys — garantiert vor dem ersten Logger. Ein Aufruf aus <c>Program.Main</c>
/// würde nur den GUI-Prozess abdecken: Testprozesse und die CLI haben ein anderes bzw. gar kein
/// eigenes <c>Main</c>, dort kennt NLog das <c>${masked}</c> dann nicht — und verschluckt nicht
/// etwa nur die Maskierung, sondern den kompletten Message-Text.
/// </summary>
[LayoutRenderer("masked")]
[ThreadAgnostic]
public sealed class MaskedLayoutRenderer : WrapperLayoutRendererBase
{
    protected override string Transform(string text) => SecretMasker.Mask(text);

    /// <summary>
    /// Registriert den <c>${masked}</c>-Renderer global (idempotent). Läuft automatisch beim Laden
    /// des Assemblys; ein manueller Aufruf schadet nicht.
    /// </summary>
    /// <remarks>
    /// Im Test muss der Modulkonstruktor erzwungen werden — ein bloßes <c>typeof(...)</c> lädt nur
    /// das Typ-Token und löst ihn NICHT aus:
    /// <c>RuntimeHelpers.RunModuleConstructor(typeof(MaskedLayoutRenderer).Module.ModuleHandle);</c>
    /// </remarks>
    [ModuleInitializer]
    [SuppressMessage("Usage", "CA2255",
        Justification = "Muss vor dem ersten Logger laufen — auch in Prozessen ohne eigenes Main (Tests, CLI).")]
    public static void Register() =>
        LogManager.Setup().SetupExtensions(s => s.RegisterLayoutRenderer<MaskedLayoutRenderer>("masked"));
}
