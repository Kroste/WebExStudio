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
/// Muss EINMALIG vor dem ersten Logger-Aufruf registriert werden (<see cref="Register"/>) — sonst
/// wirft NLog beim Parsen eines <c>${masked}</c>-Layouts "unknown type-alias 'masked'".
/// </summary>
[LayoutRenderer("masked")]
[ThreadAgnostic]
public sealed class MaskedLayoutRenderer : WrapperLayoutRendererBase
{
    protected override string Transform(string text) => SecretMasker.Mask(text);

    /// <summary>Registriert den <c>${masked}</c>-Renderer global (idempotent).</summary>
    public static void Register() =>
        LogManager.Setup().SetupExtensions(s => s.RegisterLayoutRenderer<MaskedLayoutRenderer>("masked"));
}
