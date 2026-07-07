using System.Text.Json;
using IbkrConduit.Portfolio;
using Shouldly;
using Xunit;

namespace IbkrConduit.Tests.Unit.Serialization;

/// <summary>
/// Covers <see cref="IbkrConduit.Serialization.SubAccountsPageJsonConverter"/> — the converter that
/// normalizes IBKR's two documented shapes for <c>/portfolio/subaccounts2</c> (a bare JSON array of
/// sub-accounts, or an object wrapper <c>{metadata, subaccounts}</c>) into one paged DTO. The bare
/// array form must yield <see cref="SubAccountsPage.Metadata"/> = <see langword="null"/>.
/// </summary>
public class SubAccountsPageJsonConverterTests
{
    private const string _oneSubAccount =
        """{ "id": "U1", "accountId": "U1", "accountTitle": "Title", "type": "DEMO", "desc": "U1" }""";

    private static SubAccountsPage Read(string json) =>
        JsonSerializer.Deserialize<SubAccountsPage>(json)!;

    [Fact]
    public void Read_BareArray_ReturnsSubaccountsWithNullMetadata()
    {
        var page = Read($"[ {_oneSubAccount} ]");

        page.Metadata.ShouldBeNull();
        page.Subaccounts.Count.ShouldBe(1);
        page.Subaccounts[0].Id.ShouldBe("U1");
        page.Subaccounts[0].AccountType.ShouldBe("DEMO");
    }

    [Fact]
    public void Read_WrapperObject_ReturnsSubaccountsWithMetadata()
    {
        var page = Read(
            $$"""
            {
              "metadata": { "total": 42, "pageSize": 20, "pageNum": 3 },
              "subaccounts": [ {{_oneSubAccount}} ]
            }
            """);

        page.Metadata.ShouldNotBeNull();
        page.Metadata!.Total.ShouldBe(42);
        page.Metadata.PageSize.ShouldBe(20);
        page.Metadata.PageNum.ShouldBe(3);
        page.Subaccounts.Count.ShouldBe(1);
        page.Subaccounts[0].Id.ShouldBe("U1");
    }

    [Fact]
    public void Read_EmptyArray_ReturnsEmptyPageWithNullMetadata()
    {
        var page = Read("[]");

        page.Metadata.ShouldBeNull();
        page.Subaccounts.ShouldBeEmpty();
    }

    [Fact]
    public void Read_WrapperWithoutMetadata_ReturnsNullMetadata()
    {
        var page = Read($$"""{ "subaccounts": [ {{_oneSubAccount}} ] }""");

        page.Metadata.ShouldBeNull();
        page.Subaccounts.Count.ShouldBe(1);
    }

    [Fact]
    public void Read_NullBody_ReturnsEmptyPage()
    {
        var page = Read("null");

        page.Metadata.ShouldBeNull();
        page.Subaccounts.ShouldBeEmpty();
    }
}
