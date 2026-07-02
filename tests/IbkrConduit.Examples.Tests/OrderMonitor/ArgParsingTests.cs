using System;
using IbkrConduit.Examples.OrderMonitor;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace IbkrConduit.Examples.Tests.OrderMonitor;

public class ArgParsingTests
{
    private static bool Parse(string[] args, out MonitorArgs parsed, out string error)
    {
        var ok = Program.TryParseArgs(
            args, out var realtimeOnly, out var days, out var duration,
            out _, out var logFilePath, out var logLevel, out error);
        parsed = new MonitorArgs(realtimeOnly, days, duration, logFilePath, logLevel);
        return ok;
    }

    private record MonitorArgs(
        bool RealtimeOnly, int Days, TimeSpan? Duration, string? LogFilePath, LogLevel LogLevel);

    [Fact]
    public void TryParseArgs_NoArgs_ReturnsDefaults()
    {
        Parse(Array.Empty<string>(), out var p, out _).ShouldBeTrue();
        p.RealtimeOnly.ShouldBeFalse();
        p.Days.ShouldBe(1);
        p.Duration.ShouldBeNull();
        p.LogLevel.ShouldBe(LogLevel.Debug);
    }

    [Fact]
    public void TryParseArgs_RealtimeOnly_SetsFlag()
    {
        Parse(new[] { "--realtime-only" }, out var p, out _).ShouldBeTrue();
        p.RealtimeOnly.ShouldBeTrue();
    }

    [Fact]
    public void TryParseArgs_Days_ParsesValue()
    {
        Parse(new[] { "--days", "5" }, out var p, out _).ShouldBeTrue();
        p.Days.ShouldBe(5);
    }

    [Fact]
    public void TryParseArgs_DaysNonNumeric_ReturnsError()
    {
        Parse(new[] { "--days", "abc" }, out _, out var error).ShouldBeFalse();
        error.ShouldContain("--days");
    }

    [Fact]
    public void TryParseArgs_Duration_ParsesShorthand()
    {
        Parse(new[] { "--duration", "30s" }, out var p, out _).ShouldBeTrue();
        p.Duration.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void TryParseArgs_LogLevel_ParsesValue()
    {
        Parse(new[] { "--log-level", "Warning" }, out var p, out _).ShouldBeTrue();
        p.LogLevel.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public void TryParseArgs_UnknownArgument_ReturnsError()
    {
        Parse(new[] { "--bogus" }, out _, out var error).ShouldBeFalse();
        error.ShouldContain("--bogus");
    }
}
