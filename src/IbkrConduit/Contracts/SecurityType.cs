using System.Runtime.Serialization;

namespace IbkrConduit.Contracts;

/// <summary>
/// IBKR security types used for contract search and definition queries.
/// </summary>
/// <remarks>
/// Different endpoints accept different subsets of these values:
/// <list type="bullet">
///   <item><c>SearchBySymbolAsync</c>: STK, IND, BOND</item>
///   <item><c>GetOptionStrikesAsync</c>: OPT, FOP, WAR</item>
///   <item><c>GetSecurityDefinitionInfoAsync</c>: all values</item>
/// </list>
/// The API will return an error if an unsupported value is used on a given endpoint.
/// </remarks>
public enum SecurityType
{
    /// <summary>Stock.</summary>
    [EnumMember(Value = "STK")]
    Stock,

    /// <summary>Option.</summary>
    [EnumMember(Value = "OPT")]
    Option,

    /// <summary>Future.</summary>
    [EnumMember(Value = "FUT")]
    Future,

    /// <summary>Futures option.</summary>
    [EnumMember(Value = "FOP")]
    FuturesOption,

    /// <summary>Warrant.</summary>
    [EnumMember(Value = "WAR")]
    Warrant,

    /// <summary>Cash / Forex.</summary>
    [EnumMember(Value = "CASH")]
    Cash,

    /// <summary>Index.</summary>
    [EnumMember(Value = "IND")]
    Index,

    /// <summary>Bond.</summary>
    [EnumMember(Value = "BOND")]
    Bond,

    /// <summary>Contract for Difference.</summary>
    [EnumMember(Value = "CFD")]
    Cfd,

    /// <summary>Fund / Mutual Fund.</summary>
    [EnumMember(Value = "FUND")]
    Fund,

    /// <summary>Issuer option (structured product).</summary>
    [EnumMember(Value = "IOPT")]
    IssuerOption,

    /// <summary>Commodity.</summary>
    [EnumMember(Value = "CMDTY")]
    Commodity,

    /// <summary>Cryptocurrency.</summary>
    [EnumMember(Value = "CRYPTO")]
    Crypto,

    /// <summary>Event / prediction contract.</summary>
    [EnumMember(Value = "PDC")]
    EventContract,
}
