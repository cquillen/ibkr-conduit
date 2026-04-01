using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.MarketData;

/// <summary>
/// Constants for all documented IBKR market data field IDs.
/// Use these with <see cref="IIbkrMarketDataApi.GetSnapshotAsync"/> and
/// <see cref="MarketDataSnapshot.AllFields"/> for field lookup.
/// </summary>
[ExcludeFromCodeCoverage]
public static class MarketDataFields
{
    /// <summary>Last traded price.</summary>
    public const string LastPrice = "31";

    /// <summary>Symbol name.</summary>
    public const string Symbol = "55";

    /// <summary>Text description.</summary>
    public const string Text = "58";

    /// <summary>Session high.</summary>
    public const string High = "70";

    /// <summary>Session low.</summary>
    public const string Low = "71";

    /// <summary>Market value of the position.</summary>
    public const string MarketValue = "73";

    /// <summary>Average price.</summary>
    public const string AvgPrice = "74";

    /// <summary>Unrealized profit and loss.</summary>
    public const string UnrealizedPnl = "75";

    /// <summary>Formatted position quantity.</summary>
    public const string FormattedPosition = "76";

    /// <summary>Formatted unrealized PnL.</summary>
    public const string FormattedUnrealizedPnl = "77";

    /// <summary>Daily profit and loss.</summary>
    public const string DailyPnl = "78";

    /// <summary>Realized profit and loss.</summary>
    public const string RealizedPnl = "79";

    /// <summary>Unrealized PnL as a percentage.</summary>
    public const string UnrealizedPnlPercent = "80";

    /// <summary>Change from prior close.</summary>
    public const string Change = "82";

    /// <summary>Change percent from prior close.</summary>
    public const string ChangePercent = "83";

    /// <summary>Best bid price.</summary>
    public const string BidPrice = "84";

    /// <summary>Ask size.</summary>
    public const string AskSize = "85";

    /// <summary>Best ask price.</summary>
    public const string AskPrice = "86";

    /// <summary>Volume.</summary>
    public const string Volume = "87";

    /// <summary>Bid size.</summary>
    public const string BidSize = "88";

    /// <summary>Option right (C/P).</summary>
    public const string Right = "201";

    /// <summary>Exchange.</summary>
    public const string Exchange = "6004";

    /// <summary>Contract ID.</summary>
    public const string Conid = "6008";

    /// <summary>Security type.</summary>
    public const string SecType = "6070";

    /// <summary>Option months.</summary>
    public const string Months = "6072";

    /// <summary>Regular expiry date.</summary>
    public const string RegularExpiry = "6073";

    /// <summary>Server ID.</summary>
    public const string ServerId = "6119";

    /// <summary>Underlying contract ID.</summary>
    public const string UnderlyingConid = "6457";

    /// <summary>Service parameters.</summary>
    public const string ServiceParams = "6508";

    /// <summary>Market data availability status.</summary>
    public const string MarketDataAvailability = "6509";

    /// <summary>Company name.</summary>
    public const string CompanyName = "7051";

    /// <summary>Ask exchange.</summary>
    public const string AskExchange = "7057";

    /// <summary>Last trade exchange.</summary>
    public const string LastExchange = "7058";

    /// <summary>Last trade size.</summary>
    public const string LastSize = "7059";

    /// <summary>Bid exchange.</summary>
    public const string BidExchange = "7068";

    /// <summary>Implied volatility / historical volatility ratio.</summary>
    public const string ImpliedVolHistVolRatio = "7084";

    /// <summary>Put/call open interest.</summary>
    public const string PutCallInterest = "7085";

    /// <summary>Put/call volume.</summary>
    public const string PutCallVolume = "7086";

    /// <summary>Historical volatility.</summary>
    public const string HistoricalVolatility = "7087";

    /// <summary>Historical volatility (close).</summary>
    public const string HistoricalVolatilityClose = "7088";

    /// <summary>Option volume.</summary>
    public const string OptionVolume = "7089";

    /// <summary>Contract ID and exchange.</summary>
    public const string ConidExchange = "7094";

    /// <summary>Whether the contract can be traded.</summary>
    public const string CanBeTraded = "7184";

    /// <summary>Contract description.</summary>
    public const string ContractDescription = "7219";

    /// <summary>Contract description (secondary).</summary>
    public const string ContractDescription2 = "7220";

    /// <summary>Listing exchange.</summary>
    public const string ListingExchange = "7221";

    /// <summary>Industry classification.</summary>
    public const string Industry = "7280";

    /// <summary>Category classification.</summary>
    public const string Category = "7281";

    /// <summary>Average daily volume.</summary>
    public const string AverageVolume = "7282";

    /// <summary>Option implied volatility.</summary>
    public const string OptionImpliedVolatility = "7283";

    /// <summary>Historical volatility (deprecated).</summary>
    public const string HistoricalVolatilityDeprecated = "7284";

    /// <summary>Put/call ratio.</summary>
    public const string PutCallRatio = "7285";

    /// <summary>Cost basis.</summary>
    public const string CostBasis = "7292";

    /// <summary>52-week high.</summary>
    public const string FiftyTwoWeekHigh = "7293";

    /// <summary>52-week low.</summary>
    public const string FiftyTwoWeekLow = "7294";

    /// <summary>Opening price.</summary>
    public const string Open = "7295";

    /// <summary>Closing price.</summary>
    public const string Close = "7296";

    /// <summary>Option delta.</summary>
    public const string Delta = "7308";

    /// <summary>Option gamma.</summary>
    public const string Gamma = "7309";

    /// <summary>Option theta.</summary>
    public const string Theta = "7310";

    /// <summary>Option vega.</summary>
    public const string Vega = "7311";

    /// <summary>Option volume change percent.</summary>
    public const string OptionVolumeChangePercent = "7607";

    /// <summary>Implied volatility.</summary>
    public const string ImpliedVolatility = "7633";

    /// <summary>Mark price.</summary>
    public const string Mark = "7635";

    /// <summary>Number of shortable shares.</summary>
    public const string ShortableShares = "7636";

    /// <summary>Fee rate for borrowing shares.</summary>
    public const string FeeRate = "7637";

    /// <summary>Option open interest.</summary>
    public const string OptionOpenInterest = "7638";

    /// <summary>Percent of mark value.</summary>
    public const string PercentOfMarkValue = "7639";

    /// <summary>Shortable availability.</summary>
    public const string Shortable = "7644";

    /// <summary>Dividends.</summary>
    public const string Dividends = "7671";

    /// <summary>Dividends trailing twelve months.</summary>
    public const string DividendsTtm = "7672";

    /// <summary>200-day exponential moving average.</summary>
    public const string Ema200 = "7674";

    /// <summary>100-day exponential moving average.</summary>
    public const string Ema100 = "7675";

    /// <summary>50-day exponential moving average.</summary>
    public const string Ema50 = "7676";

    /// <summary>20-day exponential moving average.</summary>
    public const string Ema20 = "7677";

    /// <summary>Price relative to 200-day EMA.</summary>
    public const string PriceToEma200 = "7678";

    /// <summary>Price relative to 100-day EMA.</summary>
    public const string PriceToEma100 = "7679";

    /// <summary>Price relative to 50-day EMA.</summary>
    public const string PriceToEma50 = "7724";

    /// <summary>Price relative to 20-day EMA.</summary>
    public const string PriceToEma20 = "7681";

    /// <summary>Change since market open.</summary>
    public const string ChangeSinceOpen = "7682";

    /// <summary>Upcoming event description.</summary>
    public const string UpcomingEvent = "7683";

    /// <summary>Upcoming event date.</summary>
    public const string UpcomingEventDate = "7684";

    /// <summary>Upcoming analyst meeting.</summary>
    public const string UpcomingAnalystMeeting = "7685";

    /// <summary>Upcoming earnings date.</summary>
    public const string UpcomingEarnings = "7686";

    /// <summary>Upcoming miscellaneous event.</summary>
    public const string UpcomingMiscEvent = "7687";

    /// <summary>Recent analyst meeting.</summary>
    public const string RecentAnalystMeeting = "7688";

    /// <summary>Recent earnings event.</summary>
    public const string RecentEarnings = "7689";

    /// <summary>Recent miscellaneous event.</summary>
    public const string RecentMiscEvent = "7690";

    /// <summary>Probability of maximum return.</summary>
    public const string ProbabilityOfMaxReturn = "7694";

    /// <summary>Break-even price.</summary>
    public const string BreakEven = "7695";

    /// <summary>SPX delta.</summary>
    public const string SpxDelta = "7696";

    /// <summary>Futures open interest.</summary>
    public const string FuturesOpenInterest = "7697";

    /// <summary>Last yield.</summary>
    public const string LastYield = "7698";

    /// <summary>Bid yield.</summary>
    public const string BidYield = "7699";

    /// <summary>Probability of maximum return (secondary).</summary>
    public const string ProbabilityOfMaxReturn2 = "7700";

    /// <summary>Probability of maximum loss.</summary>
    public const string ProbabilityOfMaxLoss = "7702";

    /// <summary>Profit probability.</summary>
    public const string ProfitProbability = "7703";

    /// <summary>Organization type.</summary>
    public const string OrganizationType = "7704";

    /// <summary>Debt class.</summary>
    public const string DebtClass = "7705";

    /// <summary>Credit ratings.</summary>
    public const string Ratings = "7706";

    /// <summary>Bond state code.</summary>
    public const string BondStateCode = "7707";

    /// <summary>Bond type.</summary>
    public const string BondType = "7708";

    /// <summary>Last trading date.</summary>
    public const string LastTradingDate = "7714";

    /// <summary>Issue date.</summary>
    public const string IssueDate = "7715";

    /// <summary>Ask yield.</summary>
    public const string AskYield = "7720";

    /// <summary>Prior close price.</summary>
    public const string PriorClose = "7741";

    /// <summary>Volume as long integer.</summary>
    public const string VolumeLong = "7762";

    /// <summary>Whether the user has trading permissions for this contract.</summary>
    public const string HasTradingPermissions = "7768";

    /// <summary>Daily PnL (raw).</summary>
    public const string DailyPnlRaw = "7920";

    /// <summary>Cost basis (raw).</summary>
    public const string CostBasisRaw = "7921";
}
