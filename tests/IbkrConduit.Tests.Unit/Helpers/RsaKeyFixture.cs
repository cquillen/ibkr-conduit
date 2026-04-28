using System;
using System.Security.Cryptography;

namespace IbkrConduit.Tests.Unit.Helpers;

public sealed class RsaKeyFixture : IDisposable
{
    public RSA SignatureKey { get; } = RSA.Create(2048);
    public RSA EncryptionKey { get; } = RSA.Create(2048);

    public void Dispose()
    {
        SignatureKey.Dispose();
        EncryptionKey.Dispose();
    }
}
