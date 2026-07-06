using NLog;
using NLog.Config;
using NLog.Targets;
using WebExStudio.UI;
using Xunit;

namespace WebExStudio.UI.Tests;

/// <summary>
/// Tests für <see cref="GlobalExceptionHandler"/>. Der Dialog-Anteil braucht eine
/// laufende Avalonia-App und ist hier nicht testbar — die Logging-Logik schon.
/// Nicht parallel zu anderen Tests, da die globale NLog-Konfiguration ersetzt wird.
/// </summary>
[Collection("NLogGlobal")]
public class GlobalExceptionHandlerTests : IDisposable
{
    private readonly LoggingConfiguration? _previous = LogManager.Configuration;
    private readonly MemoryTarget _memory;

    public GlobalExceptionHandlerTests()
    {
        _memory = new MemoryTarget("mem") { Layout = "${level}|${message}|${exception:format=Type,Message}" };
        var cfg = new LoggingConfiguration();
        cfg.AddRuleForAllLevels(_memory);
        LogManager.Configuration = cfg;
    }

    public void Dispose() => LogManager.Configuration = _previous;

    [Fact]
    public void LogFatal_SchreibtFatalEintragMitQuelleUndException()
    {
        GlobalExceptionHandler.LogFatal(new InvalidOperationException("Boom"), "TestQuelle", isTerminating: false);

        var entry = Assert.Single(_memory.Logs, l => l.StartsWith("Fatal|"));
        Assert.Contains("TestQuelle", entry);
        Assert.Contains("InvalidOperationException", entry);
        Assert.Contains("Boom", entry);
    }

    [Fact]
    public void LogFatal_MitNullException_WirftNicht()
    {
        var ex = Record.Exception(() => GlobalExceptionHandler.LogFatal(null, "AppDomain", isTerminating: true));

        Assert.Null(ex);
        Assert.Contains(_memory.Logs, l => l.StartsWith("Fatal|"));
    }

    [Fact]
    public void LogFatal_OhneNLogKonfiguration_WirftNicht()
    {
        LogManager.Configuration = null!;

        var ex = Record.Exception(() => GlobalExceptionHandler.LogFatal(new Exception("x"), "Test", isTerminating: false));

        Assert.Null(ex);
    }
}

[CollectionDefinition("NLogGlobal", DisableParallelization = true)]
public class NLogGlobalCollection { }
