using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace IbkrConduit.Contracts;

/// <summary>
/// A contract search result from the /iserver/secdef/search endpoint.
/// </summary>
/// <param name="Conid">The IBKR contract identifier.</param>
/// <param name="CompanyHeader">The company header text.</param>
/// <param name="CompanyName">The company name.</param>
/// <param name="Description">A description of the contract.</param>
/// <param name="Symbol">The ticker symbol.</param>
/// <param name="ExtendedConid">The extended contract identifier.</param>
/// <param name="SecurityType">The security type (e.g., "STK", "OPT").</param>
/// <param name="ListingExchange">The primary listing exchange.</param>
/// <param name="Sections">Optional list of contract sections (e.g., for derivatives).</param>
[ExcludeFromCodeCoverage]
public record ContractSearchResult(
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int Conid,
    [property: JsonPropertyName("companyHeader")] string CompanyHeader,
    [property: JsonPropertyName("companyName")] string CompanyName,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("conidEx")] string ExtendedConid,
    [property: JsonPropertyName("secType")] string SecurityType,
    [property: JsonPropertyName("listingExchange")] string ListingExchange,
    [property: JsonPropertyName("sections")] List<ContractSection>? Sections);

/// <summary>
/// A section within a contract search result, representing a derivative type or sub-instrument.
/// </summary>
/// <param name="SecurityType">The security type of this section.</param>
/// <param name="Months">Available contract months, if applicable.</param>
/// <param name="Symbol">The symbol for this section, if different from the parent.</param>
/// <param name="Exchange">The exchange for this section.</param>
/// <param name="Conid">The contract ID for this section, if applicable.</param>
[ExcludeFromCodeCoverage]
public record ContractSection(
    [property: JsonPropertyName("secType")] string SecurityType,
    [property: JsonPropertyName("months")] string? Months,
    [property: JsonPropertyName("symbol")] string? Symbol,
    [property: JsonPropertyName("exchange")] string? Exchange,
    [property: JsonPropertyName("conid")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int? Conid);

/// <summary>
/// Detailed contract information from the /iserver/contract/{conid}/info endpoint.
/// </summary>
/// <param name="Conid">The IBKR contract identifier.</param>
/// <param name="Symbol">The ticker symbol.</param>
/// <param name="CompanyName">The company name.</param>
/// <param name="Exchange">The primary exchange.</param>
/// <param name="ListingExchange">The listing exchange.</param>
/// <param name="Currency">The trading currency.</param>
/// <param name="InstrumentType">The instrument type (e.g., "STK").</param>
/// <param name="ValidExchanges">Comma-separated list of valid exchanges.</param>
[ExcludeFromCodeCoverage]
public record ContractDetails(
    [property: JsonPropertyName("con_id")]
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    int Conid,
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("company_name")] string CompanyName,
    [property: JsonPropertyName("exchange")] string Exchange,
    [property: JsonPropertyName("listing_exchange")] string ListingExchange,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("instrument_type")] string InstrumentType,
    [property: JsonPropertyName("valid_exchanges")] string ValidExchanges);
