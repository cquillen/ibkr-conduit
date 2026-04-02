namespace ApiCapture;

/// <summary>
/// Static table of all IBKR API endpoints to capture.
/// </summary>
public static class EndpointTable
{
    /// <summary>
    /// All endpoint entries grouped by category.
    /// URLs and bodies use <c>{accountId}</c> for runtime substitution.
    /// Names ending in _Invalid* or _Missing* are intentional failure scenarios.
    /// </summary>
    public static readonly EndpointEntry[] Entries =
    [
        // ---------------------------------------------------------------
        // Session — Success
        // ---------------------------------------------------------------
        new("Session", "InitBrokerageSession_Success", HttpMethod.Post,
            "/v1/api/iserver/auth/ssodh/init", 200,
            """{"publish":true,"compete":true}"""),
        new("Session", "Tickle_Success", HttpMethod.Post,
            "/v1/api/tickle", 200, ""),
        new("Session", "GetAuthStatus_Success", HttpMethod.Get,
            "/v1/api/iserver/auth/status", 200),
        new("Session", "SuppressQuestions_Success", HttpMethod.Post,
            "/v1/api/iserver/questions/suppress", 200,
            """{"messageIds":["o163"]}"""),
        new("Session", "ResetSuppressedQuestions_Success", HttpMethod.Post,
            "/v1/api/iserver/questions/suppress/reset", 200, ""),

        // Session — Edge cases
        new("Session", "InitBrokerageSession_PublishFalse", HttpMethod.Post,
            "/v1/api/iserver/auth/ssodh/init", 200,
            """{"publish":false}"""),
        new("Session", "SuppressQuestions_EmptyIds", HttpMethod.Post,
            "/v1/api/iserver/questions/suppress", 500,
            """{"messageIds":[]}"""),
        new("Session", "SuppressQuestions_InvalidId", HttpMethod.Post,
            "/v1/api/iserver/questions/suppress", 200,
            """{"messageIds":["ZZZZZ"]}"""),

        // ---------------------------------------------------------------
        // Accounts — Success
        // ---------------------------------------------------------------
        new("Accounts", "GetIserverAccounts_Success", HttpMethod.Get,
            "/v1/api/iserver/accounts", 200),
        new("Accounts", "SwitchAccount_Success", HttpMethod.Post,
            "/v1/api/iserver/account", 200,
            """{"acctId":"{accountId}"}"""),

        // Accounts — Failures
        new("Accounts", "SwitchAccount_InvalidId", HttpMethod.Post,
            "/v1/api/iserver/account", 500,
            """{"acctId":"INVALID999"}"""),
        new("Accounts", "SwitchAccount_MissingBody", HttpMethod.Post,
            "/v1/api/iserver/account", 400,
            """{}"""),

        // ---------------------------------------------------------------
        // Portfolio — Success
        // ---------------------------------------------------------------
        new("Portfolio", "GetAccounts_Success", HttpMethod.Get,
            "/v1/api/portfolio/accounts", 200),
        new("Portfolio", "GetPositions_Success", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/positions/0", 200),
        new("Portfolio", "GetSummary_Success", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/summary", 200),
        new("Portfolio", "GetLedger_Success", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/ledger", 200),
        new("Portfolio", "GetMeta_Success", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/meta", 200),
        new("Portfolio", "GetAllocation_Success", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/allocation", 200),
        new("Portfolio", "GetPositionByConid_Success", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/position/756733", 200),
        new("Portfolio", "GetPositionCrossAccount_Success", HttpMethod.Get,
            "/v1/api/portfolio/positions/756733", 200),
        new("Portfolio", "GetComboPositions_Success", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/combo/positions", 200),
        new("Portfolio", "GetRealTimePositions_Success", HttpMethod.Get,
            "/v1/api/portfolio2/{accountId}/positions", 200),
        new("Portfolio", "GetSubAccounts_Success", HttpMethod.Get,
            "/v1/api/portfolio/subaccounts", 200),
        new("Portfolio", "GetSubAccountsPaged_Success", HttpMethod.Get,
            "/v1/api/portfolio/subaccounts2", 200),
        new("Portfolio", "GetPartitionedPnl_Success", HttpMethod.Get,
            "/v1/api/iserver/account/pnl/partitioned", 200),
        new("Portfolio", "InvalidateCache_Success", HttpMethod.Post,
            "/v1/api/portfolio/{accountId}/positions/invalidate", 200, ""),
        new("Portfolio", "GetPerformance_Success", HttpMethod.Post,
            "/v1/api/pa/performance", 200,
            """{"acctIds":["{accountId}"],"period":"1M"}"""),
        new("Portfolio", "GetTransactions_Success", HttpMethod.Post,
            "/v1/api/pa/transactions", 200,
            """{"acctIds":["{accountId}"],"conids":["756733"],"currency":"USD","days":30}"""),
        new("Portfolio", "GetConsolidatedAllocation_Success", HttpMethod.Post,
            "/v1/api/portfolio/allocation", 200,
            """{"acctIds":["{accountId}"]}"""),
        new("Portfolio", "GetAllPeriodsPerformance_Success", HttpMethod.Post,
            "/v1/api/pa/allperiods", 200,
            """{"acctIds":["{accountId}"]}"""),

        // Portfolio — Failures: Invalid account (IBKR returns 401 for unknown accounts)
        new("Portfolio", "GetPositions_InvalidAccount", HttpMethod.Get,
            "/v1/api/portfolio/INVALID999/positions/0", 401),
        new("Portfolio", "GetSummary_InvalidAccount", HttpMethod.Get,
            "/v1/api/portfolio/INVALID999/summary", 401),
        new("Portfolio", "GetLedger_InvalidAccount", HttpMethod.Get,
            "/v1/api/portfolio/INVALID999/ledger", 401),
        new("Portfolio", "GetMeta_InvalidAccount", HttpMethod.Get,
            "/v1/api/portfolio/INVALID999/meta", 401),
        new("Portfolio", "GetAllocation_InvalidAccount", HttpMethod.Get,
            "/v1/api/portfolio/INVALID999/allocation", 401),

        // Portfolio — Failures: Invalid conid returns 200 with empty body (IBKR quirk)
        new("Portfolio", "GetPositionByConid_NonExistent", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/position/999999999", 200),
        new("Portfolio", "GetPositionCrossAccount_NonExistent", HttpMethod.Get,
            "/v1/api/portfolio/positions/999999999", 200),

        // Portfolio — Failures: Invalid POST inputs
        new("Portfolio", "GetPerformance_InvalidPeriod", HttpMethod.Post,
            "/v1/api/pa/performance", 400,
            """{"acctIds":["{accountId}"],"period":"INVALID"}"""),
        new("Portfolio", "GetTransactions_MissingConids", HttpMethod.Post,
            "/v1/api/pa/transactions", 400,
            """{"acctIds":["{accountId}"],"conids":[],"currency":"USD","days":30}"""),
        new("Portfolio", "GetTransactions_InvalidAccount", HttpMethod.Post,
            "/v1/api/pa/transactions", 500,
            """{"acctIds":["INVALID999"],"conids":["756733"],"currency":"USD","days":30}"""),
        new("Portfolio", "GetPerformance_MissingAccounts", HttpMethod.Post,
            "/v1/api/pa/performance", 400,
            """{"acctIds":[],"period":"1M"}"""),
        new("Portfolio", "GetAllPeriodsPerformance_InvalidAccount", HttpMethod.Post,
            "/v1/api/pa/allperiods", 500,
            """{"acctIds":["INVALID999"]}"""),
    ];
}
