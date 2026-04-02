namespace ApiCapture;

/// <summary>
/// Static table of all IBKR API endpoints to capture.
/// </summary>
public static class EndpointTable
{
    /// <summary>
    /// All endpoint entries grouped by category.
    /// URLs and bodies use <c>{accountId}</c> for runtime substitution.
    /// </summary>
    public static readonly EndpointEntry[] Entries =
    [
        // ---------------------------------------------------------------
        // Session
        // ---------------------------------------------------------------
        new("Session", "InitBrokerageSession", HttpMethod.Post,
            "/v1/api/iserver/auth/ssodh/init", 200,
            """{"publish":true,"compete":true}"""),
        new("Session", "Tickle", HttpMethod.Post,
            "/v1/api/tickle", 200, ""),
        new("Session", "GetAuthStatus", HttpMethod.Get,
            "/v1/api/iserver/auth/status", 200),
        new("Session", "SuppressQuestions", HttpMethod.Post,
            "/v1/api/iserver/questions/suppress", 200,
            """{"messageIds":["o163"]}"""),
        new("Session", "ResetSuppressedQuestions", HttpMethod.Post,
            "/v1/api/iserver/questions/suppress/reset", 200, ""),

        // ---------------------------------------------------------------
        // Accounts
        // ---------------------------------------------------------------
        new("Accounts", "GetIserverAccounts", HttpMethod.Get,
            "/v1/api/iserver/accounts", 200),
        new("Accounts", "SwitchAccount", HttpMethod.Post,
            "/v1/api/iserver/account", 200,
            """{"acctId":"{accountId}"}"""),

        // ---------------------------------------------------------------
        // Portfolio
        // ---------------------------------------------------------------
        new("Portfolio", "GetAccounts", HttpMethod.Get,
            "/v1/api/portfolio/accounts", 200),
        new("Portfolio", "GetPositions", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/positions/0", 200),
        new("Portfolio", "GetSummary", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/summary", 200),
        new("Portfolio", "GetLedger", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/ledger", 200),
        new("Portfolio", "GetMeta", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/meta", 200),
        new("Portfolio", "GetAllocation", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/allocation", 200),
        new("Portfolio", "GetPositionByConid", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/position/756733", 200),
        new("Portfolio", "GetPositionCrossAccount", HttpMethod.Get,
            "/v1/api/portfolio/positions/756733", 200),
        new("Portfolio", "GetComboPositions", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/combo/positions", 200),
        new("Portfolio", "GetRealTimePositions", HttpMethod.Get,
            "/v1/api/portfolio2/{accountId}/positions", 200),
        new("Portfolio", "GetSubAccounts", HttpMethod.Get,
            "/v1/api/portfolio/subaccounts", 200),
        new("Portfolio", "GetSubAccountsPaged", HttpMethod.Get,
            "/v1/api/portfolio/subaccounts2", 200),
        new("Portfolio", "GetPartitionedPnl", HttpMethod.Get,
            "/v1/api/iserver/account/pnl/partitioned", 200),
        new("Portfolio", "InvalidateCache", HttpMethod.Post,
            "/v1/api/portfolio/{accountId}/positions/invalidate", 200, ""),
        new("Portfolio", "GetPerformance", HttpMethod.Post,
            "/v1/api/pa/performance", 200,
            """{"acctIds":["{accountId}"],"period":"1M"}"""),
        new("Portfolio", "GetTransactions", HttpMethod.Post,
            "/v1/api/pa/transactions", 200,
            """{"acctIds":["{accountId}"],"conids":["756733"],"currency":"USD","days":30}"""),
        new("Portfolio", "GetConsolidatedAllocation", HttpMethod.Post,
            "/v1/api/portfolio/allocation", 200,
            """{"acctIds":["{accountId}"]}"""),
        new("Portfolio", "GetAllPeriodsPerformance", HttpMethod.Post,
            "/v1/api/pa/allperiods", 200,
            """{"acctIds":["{accountId}"]}"""),
    ];
}
