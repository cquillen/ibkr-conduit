using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Diagnostics;

/// <summary>
/// Per-provider singleton carrying the tenant identity used to tag telemetry
/// (metrics, spans, log scopes) so multiple tenants in one process are
/// distinguishable. Seeded once per child provider with the explicit tenant id.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record TenantContext(string TenantId);
