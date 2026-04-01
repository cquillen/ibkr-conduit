using System;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Http;

namespace IbkrConduit.Tests.Integration.Recording;

/// <summary>
/// Injects <see cref="RecordingDelegatingHandler"/> into all HttpClient pipelines
/// when response recording is enabled via IBKR_RECORD_RESPONSES environment variable.
/// </summary>
public sealed class RecordingHandlerFilter : IHttpMessageHandlerBuilderFilter
{
    private readonly RecordingContext _context;
    private readonly bool _enabled;
    private readonly string _outputDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingHandlerFilter"/> class.
    /// </summary>
    public RecordingHandlerFilter(RecordingContext context, string? outputDirectory = null)
        : this(context, IsRecordingEnabled(), outputDirectory)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingHandlerFilter"/> class
    /// with an explicit enabled flag (for testability).
    /// </summary>
    public RecordingHandlerFilter(RecordingContext context, bool enabled, string? outputDirectory = null)
    {
        _context = context;
        _enabled = enabled;
        _outputDirectory = outputDirectory
            ?? Path.Combine(AppContext.BaseDirectory, "Recordings");
    }

    /// <inheritdoc/>
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        if (!_enabled)
        {
            return next;
        }

        return builder =>
        {
            next(builder);
            builder.AdditionalHandlers.Insert(0,
                new RecordingDelegatingHandler(_context, _outputDirectory));
        };
    }

    private static bool IsRecordingEnabled() =>
        string.Equals(
            Environment.GetEnvironmentVariable("IBKR_RECORD_RESPONSES"),
            "true", StringComparison.OrdinalIgnoreCase);
}
