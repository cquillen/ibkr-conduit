using System;
using System.Security.Cryptography;

namespace IbkrConduit.Tests.Unit.Client;

/// <summary>
/// A minimal <see cref="RSA"/> that counts how many times it is disposed, so a test can
/// assert credentials are disposed <em>exactly once</em> on a given <c>AddAsync</c> failure
/// path (a double-dispose surfaces as a count of 2, a leak as 0). The exercised ownership
/// paths never sign or export, so the crypto operations are intentionally unsupported.
/// </summary>
internal sealed class CountingRsa : RSA
{
    /// <summary>Number of times <see cref="IDisposable.Dispose"/> has run on this instance.</summary>
    public int DisposeCount { get; private set; }

    public override RSAParameters ExportParameters(bool includePrivateParameters) =>
        throw new NotSupportedException();

    public override void ImportParameters(RSAParameters parameters) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCount++;
        }

        base.Dispose(disposing);
    }
}
