using System.Reflection;
using Refit;

namespace IbkrConduit.Serialization;

/// <summary>
/// URL parameter formatter for every IBKR Refit client. Serializes <see cref="bool"/> query values as
/// lowercase <c>true</c>/<c>false</c> — the form the IBKR CP Web API documents (e.g. the live-orders
/// <c>force=true</c> cache-clear, docs/ibkr-web-api-spec.md §Live Orders). Refit's default formatter
/// emits <c>True</c>/<c>False</c> via <c>string.Format("{0}", value)</c>, which the documented
/// <c>force=true</c> follow-up (§10.6) must not rely on. All other types defer to the base formatter.
/// </summary>
internal sealed class IbkrUrlParameterFormatter : DefaultUrlParameterFormatter
{
    /// <inheritdoc />
    public override string? Format(object? value, ICustomAttributeProvider attributeProvider, Type type) =>
        value is bool b
            ? (b ? "true" : "false")
            : base.Format(value, attributeProvider, type);
}
