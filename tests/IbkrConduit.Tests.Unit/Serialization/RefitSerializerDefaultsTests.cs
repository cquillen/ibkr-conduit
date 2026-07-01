using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Refit;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Serialization;

/// <summary>
/// Guards the JSON number-handling behavior the library depends on. IbkrConduit registers every
/// Refit client via <c>AddRefitClient&lt;TApi&gt;()</c> with no custom <see cref="RefitSettings"/>,
/// so it relies on Refit's default serializer. Refit 12 changed that default to
/// <c>JsonNumberHandling.AllowReadingFromString</c>, which matters because IBKR frequently returns
/// numeric fields as JSON strings. If a future Refit release or a custom serializer removes that
/// default, this test fails instead of silently breaking deserialization at runtime.
/// </summary>
public class RefitSerializerDefaultsTests
{
    private sealed record NumberModel(int Count, decimal Ratio);

    [Fact]
    public async Task DefaultRefitSerializer_ReadsNumbersFromJsonStrings_WithoutPerPropertyAnnotation()
    {
        // The default RefitSettings is exactly what AddRefitClient<TApi>() uses in the library.
        var serializer = new RefitSettings().ContentSerializer;
        using var content = new StringContent(
            """{"Count":"42","Ratio":"3.14"}""",
            Encoding.UTF8,
            "application/json");

        var result = await serializer.FromHttpContentAsync<NumberModel>(
            content,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Count.ShouldBe(42);
        result.Ratio.ShouldBe(3.14m);
    }
}
