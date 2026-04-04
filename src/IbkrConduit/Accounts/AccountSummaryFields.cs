using System.Diagnostics.CodeAnalysis;

namespace IbkrConduit.Accounts;

/// <summary>
/// Known dictionary key constants for account summary sub-endpoint responses.
/// These endpoints return dynamic dictionaries; use these constants for type-safe key access.
/// </summary>
[ExcludeFromCodeCoverage]
public static class AccountSummaryFields
{
    /// <summary>
    /// Segment names used as top-level keys in available_funds, balances, and margins responses.
    /// </summary>
    public static class Segments
    {
        /// <summary>Total across all segments.</summary>
        public const string Total = "total";

        /// <summary>Crypto at Paxos segment.</summary>
        public const string CryptoAtPaxos = "Crypto at Paxos";

        /// <summary>Commodities segment.</summary>
        public const string Commodities = "commodities";

        /// <summary>Securities segment.</summary>
        public const string Securities = "securities";
    }

    /// <summary>
    /// Field names within the balances sub-endpoint response segments.
    /// </summary>
    public static class Balances
    {
        /// <summary>Net liquidation value.</summary>
        public const string NetLiquidation = "net_liquidation";

        /// <summary>Net liquidation uncertainty from after-hours price changes.</summary>
        public const string NetLiquidationUncertainty = "Nt Lqdtn Uncrtnty";

        /// <summary>Equity with loan value.</summary>
        public const string EquityWithLoan = "equity_with_loan";

        /// <summary>Previous day equity with loan value.</summary>
        public const string PreviousDayEquityWithLoan = "Prvs Dy Eqty Wth Ln Vl";

        /// <summary>Reg T equity with loan value.</summary>
        public const string RegTEquityWithLoan = "Rg T Eqty Wth Ln Vl";

        /// <summary>Securities gross position value.</summary>
        public const string SecGrossPosVal = "sec_gross_pos_val";

        /// <summary>Cash balance.</summary>
        public const string Cash = "cash";

        /// <summary>Month-to-date interest.</summary>
        public const string MtdInterest = "MTD Interest";

        /// <summary>Pending debit card charges.</summary>
        public const string PendingDebitCardCharges = "Pndng Dbt Crd Chrgs";
    }

    /// <summary>
    /// Field names within the available_funds sub-endpoint response segments.
    /// </summary>
    public static class AvailableFunds
    {
        /// <summary>Current available funds.</summary>
        public const string CurrentAvailable = "current_available";

        /// <summary>Current excess liquidity.</summary>
        public const string CurrentExcess = "current_excess";

        /// <summary>Predicted post-expiry excess.</summary>
        public const string PredictedPostExpiryExcess = "Prdctd Pst-xpry Excss";

        /// <summary>Simple moving average (SMA).</summary>
        public const string Sma = "SMA";

        /// <summary>Look-ahead available funds.</summary>
        public const string LookAheadAvailableFunds = "Lk Ahd Avlbl Fnds";

        /// <summary>Look-ahead excess liquidity.</summary>
        public const string LookAheadExcessLiquidity = "Lk Ahd Excss Lqdty";

        /// <summary>Overnight available funds.</summary>
        public const string OvernightAvailable = "overnight_available";

        /// <summary>Overnight excess liquidity.</summary>
        public const string OvernightExcess = "overnight_excess";

        /// <summary>Buying power.</summary>
        public const string BuyingPower = "buying_power";

        /// <summary>Leverage.</summary>
        public const string Leverage = "leverage";

        /// <summary>Look-ahead next change time.</summary>
        public const string LookAheadNextChange = "Lk Ahd Nxt Chng";

        /// <summary>Day trades remaining.</summary>
        public const string DayTradesLeft = "day_trades_left";
    }

    /// <summary>
    /// Field names within the margins sub-endpoint response segments.
    /// </summary>
    public static class Margins
    {
        /// <summary>Regulation T margin.</summary>
        public const string RegTMargin = "RegT Margin";

        /// <summary>Current initial margin.</summary>
        public const string CurrentInitial = "current_initial";

        /// <summary>Predicted post-expiry margin at open.</summary>
        public const string PredictedPostExpiryMarginAtOpen = "Prdctd Pst-xpry Mrgn @ Opn";

        /// <summary>Current maintenance margin.</summary>
        public const string CurrentMaint = "current_maint";

        /// <summary>Projected liquid initial margin.</summary>
        public const string ProjectedLiquidityInitialMargin = "projected_liquidity_inital_margin";

        /// <summary>Projected look-ahead maintenance margin.</summary>
        public const string ProjectedLookAheadMaintenanceMargin = "Prjctd Lk Ahd Mntnnc Mrgn";

        /// <summary>Projected overnight initial margin.</summary>
        public const string ProjectedOvernightInitialMargin = "projected_overnight_initial_margin";

        /// <summary>Projected overnight maintenance margin.</summary>
        public const string ProjectedOvernightMaintenanceMargin = "Prjctd Ovrnght Mntnnc Mrgn";
    }

    /// <summary>
    /// Field names within the market_value sub-endpoint response (per-currency).
    /// </summary>
    public static class MarketValue
    {
        /// <summary>Total cash value.</summary>
        public const string TotalCash = "total_cash";

        /// <summary>Settled cash.</summary>
        public const string SettledCash = "settled_cash";

        /// <summary>Month-to-date interest.</summary>
        public const string MtdInterest = "MTD Interest";

        /// <summary>Stock market value.</summary>
        public const string Stock = "stock";

        /// <summary>Options market value.</summary>
        public const string Options = "options";

        /// <summary>Futures market value.</summary>
        public const string Futures = "futures";

        /// <summary>Future options market value.</summary>
        public const string FutureOptions = "future_options";

        /// <summary>Funds market value.</summary>
        public const string Funds = "funds";

        /// <summary>Dividends receivable.</summary>
        public const string DividendsReceivable = "dividends_receivable";

        /// <summary>Mutual funds market value.</summary>
        public const string MutualFunds = "mutual_funds";

        /// <summary>Money market value.</summary>
        public const string MoneyMarket = "money_market";

        /// <summary>Bonds market value.</summary>
        public const string Bonds = "bonds";

        /// <summary>Government bonds market value.</summary>
        public const string GovtBonds = "Govt Bonds";

        /// <summary>Treasury bills market value.</summary>
        public const string TBills = "t_bills";

        /// <summary>Warrants market value.</summary>
        public const string Warrants = "warrants";

        /// <summary>Issuer options market value.</summary>
        public const string IssuerOption = "issuer_option";

        /// <summary>Commodity market value.</summary>
        public const string Commodity = "commodity";

        /// <summary>Notional CFD market value.</summary>
        public const string NotionalCfd = "Notional CFD";

        /// <summary>CFD market value.</summary>
        public const string Cfd = "cfd";

        /// <summary>Cryptocurrency market value.</summary>
        public const string Cryptocurrency = "Cryptocurrency";

        /// <summary>Net liquidation value.</summary>
        public const string NetLiquidation = "net_liquidation";

        /// <summary>Unrealized profit and loss.</summary>
        public const string UnrealizedPnl = "unrealized_pnl";

        /// <summary>Realized profit and loss.</summary>
        public const string RealizedPnl = "realized_pnl";

        /// <summary>Exchange rate to base currency.</summary>
        public const string ExchangeRate = "Exchange Rate";
    }
}
