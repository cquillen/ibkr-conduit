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

        // Portfolio — Pagination (sort param required for paging to work)
        new("Portfolio", "GetPositions_Page0_Sorted", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/positions/0?sort=position&direction=d", 200),
        new("Portfolio", "GetPositions_Page999_Sorted_Empty", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/positions/999?sort=position&direction=d", 200),

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

        // ---------------------------------------------------------------
        // Contracts — Success
        // ---------------------------------------------------------------
        new("Contracts", "SearchBySymbol_SPY", HttpMethod.Get,
            "/v1/api/iserver/secdef/search?symbol=SPY", 200),
        new("Contracts", "SearchBySymbol_AAPL", HttpMethod.Get,
            "/v1/api/iserver/secdef/search?symbol=AAPL", 200),
        new("Contracts", "GetContractDetails_SPY", HttpMethod.Get,
            "/v1/api/iserver/contract/756733/info", 200),
        new("Contracts", "GetSecurityDefinitionInfo_SPY_OPT", HttpMethod.Get,
            "/v1/api/iserver/secdef/info?conid=756733&sectype=OPT&month=202701", 400),
        new("Contracts", "GetOptionStrikes_SPY", HttpMethod.Get,
            "/v1/api/iserver/secdef/strikes?conid=756733&sectype=OPT&month=202701", 200),
        new("Contracts", "GetTradingRules_SPY", HttpMethod.Post,
            "/v1/api/iserver/contract/rules", 200,
            """{"conid":756733,"isBuy":true}"""),
        new("Contracts", "GetSecurityDefinitionsByConid_SPY", HttpMethod.Get,
            "/v1/api/trsrv/secdef?conids=756733", 200),
        new("Contracts", "GetAllConidsByExchange_NASDAQ", HttpMethod.Get,
            "/v1/api/trsrv/all-conids?exchange=NASDAQ", 200),
        new("Contracts", "GetFuturesBySymbol_ES", HttpMethod.Get,
            "/v1/api/trsrv/futures?symbols=ES", 200),
        new("Contracts", "GetStocksBySymbol_AAPL", HttpMethod.Get,
            "/v1/api/trsrv/stocks?symbols=AAPL", 200),
        new("Contracts", "GetTradingSchedule_SPY", HttpMethod.Get,
            "/v1/api/trsrv/secdef/schedule?assetClass=STK&symbol=SPY&conid=756733", 400),
        new("Contracts", "GetCurrencyPairs_USD", HttpMethod.Get,
            "/v1/api/iserver/currency/pairs?currency=USD", 200),
        new("Contracts", "GetExchangeRate_USD_EUR", HttpMethod.Get,
            "/v1/api/iserver/exchangerate?source=USD&target=EUR", 200),

        // Contracts — Failures
        new("Contracts", "SearchBySymbol_NonExistent", HttpMethod.Get,
            "/v1/api/iserver/secdef/search?symbol=ZZZZNOTREAL99", 200),
        new("Contracts", "GetContractDetails_InvalidConid", HttpMethod.Get,
            "/v1/api/iserver/contract/999999999/info", 400),
        new("Contracts", "GetOptionStrikes_InvalidMonth", HttpMethod.Get,
            "/v1/api/iserver/secdef/strikes?conid=756733&sectype=OPT&month=999999", 200),
        new("Contracts", "GetTradingRules_InvalidConid", HttpMethod.Post,
            "/v1/api/iserver/contract/rules", 400,
            """{"conid":0,"isBuy":true}"""),
        new("Contracts", "GetFuturesBySymbol_NonExistent", HttpMethod.Get,
            "/v1/api/trsrv/futures?symbols=ZZZZNOTREAL", 200),
        new("Contracts", "GetStocksBySymbol_NonExistent", HttpMethod.Get,
            "/v1/api/trsrv/stocks?symbols=ZZZZNOTREAL", 200),
        new("Contracts", "GetExchangeRate_SameCurrency", HttpMethod.Get,
            "/v1/api/iserver/exchangerate?source=USD&target=USD", 200),

        // ---------------------------------------------------------------
        // MarketData — Success
        // ---------------------------------------------------------------
        new("MarketData", "GetSnapshot_SPY", HttpMethod.Get,
            "/v1/api/iserver/marketdata/snapshot?conids=756733&fields=31,84,86", 200),
        new("MarketData", "GetHistory_SPY_1D", HttpMethod.Get,
            "/v1/api/iserver/marketdata/history?conid=756733&period=1d&bar=1min", 200),
        new("MarketData", "GetRegulatorySnapshot_SPY", HttpMethod.Get,
            "/v1/api/md/regsnapshot?conid=756733", 400),
        new("MarketData", "GetScannerParams", HttpMethod.Get,
            "/v1/api/iserver/scanner/params", 200),
        new("MarketData", "RunScanner_TopGainers", HttpMethod.Post,
            "/v1/api/iserver/scanner/run", 200,
            """{"instrument":"STK","type":"TOP_PERC_GAIN","location":"STK.US.MAJOR","filter":[]}"""),
        new("MarketData", "RunHmdsScanner_TopGainers", HttpMethod.Post,
            "/v1/api/hmds/scanner", 404,
            """{"instrument":"STK","locations":"STK.US.MAJOR","scanCode":"TOP_PERC_GAIN","secType":"STK","maxItems":10,"filters":[]}"""),
        new("MarketData", "UnsubscribeAll", HttpMethod.Get,
            "/v1/api/iserver/marketdata/unsubscribeall", 200),
        new("MarketData", "Unsubscribe_NotSubscribed", HttpMethod.Post,
            "/v1/api/iserver/marketdata/unsubscribe", 500,
            """{"conid":756733}"""),

        // MarketData — Failures
        new("MarketData", "GetSnapshot_InvalidConid", HttpMethod.Get,
            "/v1/api/iserver/marketdata/snapshot?conids=0&fields=31", 200),
        new("MarketData", "GetHistory_InvalidPeriod", HttpMethod.Get,
            "/v1/api/iserver/marketdata/history?conid=756733&period=INVALID&bar=1min", 200),
        new("MarketData", "RunScanner_InvalidType", HttpMethod.Post,
            "/v1/api/iserver/scanner/run", 500,
            """{"instrument":"STK","type":"TOTALLY_INVALID","location":"STK.US.MAJOR","filter":[]}"""),
        new("MarketData", "Unsubscribe_NeverSubscribed", HttpMethod.Post,
            "/v1/api/iserver/marketdata/unsubscribe", 500,
            """{"conid":999999999}"""),

        // ---------------------------------------------------------------
        // Alerts — Success (create, list, detail, delete)
        // ---------------------------------------------------------------
        new("Alerts", "CreateAlert_SPY_Price", HttpMethod.Post,
            "/v1/api/iserver/account/{accountId}/alert", 403,
            """{"alertName":"CaptureTest","alertMessage":"Test alert","outsideRth":0,"alertRepeatable":0,"conditions":[{"type":1,"conidex":"756733","operator":">=","triggerMethod":"0","value":"999999"}]}"""),
        new("Alerts", "GetAlerts_Success", HttpMethod.Get,
            "/v1/api/iserver/account/mta", 200),
        new("Alerts", "GetAlertDetail_0", HttpMethod.Get,
            "/v1/api/iserver/account/alert/0", 400),

        // Alerts — Failures
        new("Alerts", "DeleteAlert_NonExistent", HttpMethod.Delete,
            "/v1/api/iserver/account/{accountId}/alert/0", 503),

        // ---------------------------------------------------------------
        // Watchlists — Success (create, list, detail, delete)
        // ---------------------------------------------------------------
        new("Watchlists", "CreateWatchlist_Success", HttpMethod.Post,
            "/v1/api/iserver/watchlist", 503,
            """{"id":"capture_test","rows":[{"C":756733,"H":"SPY"}]}"""),
        new("Watchlists", "GetWatchlists_Success", HttpMethod.Get,
            "/v1/api/iserver/watchlists", 200),
        new("Watchlists", "GetWatchlist_ById", HttpMethod.Get,
            "/v1/api/iserver/watchlist?id=capture_test", 503),
        new("Watchlists", "DeleteWatchlist_Success", HttpMethod.Delete,
            "/v1/api/iserver/watchlist?id=capture_test", 500),

        // Watchlists — Failures
        new("Watchlists", "GetWatchlist_NonExistent", HttpMethod.Get,
            "/v1/api/iserver/watchlist?id=NONEXISTENT99999", 503),
        new("Watchlists", "DeleteWatchlist_NonExistent", HttpMethod.Delete,
            "/v1/api/iserver/watchlist?id=NONEXISTENT99999", 500),

        // ---------------------------------------------------------------
        // Fyi — Success
        // ---------------------------------------------------------------
        new("Fyi", "GetUnreadCount_Success", HttpMethod.Get,
            "/v1/api/fyi/unreadnumber", 200),
        new("Fyi", "GetSettings_Success", HttpMethod.Get,
            "/v1/api/fyi/settings", 200),
        new("Fyi", "GetDeliveryOptions_Success", HttpMethod.Get,
            "/v1/api/fyi/deliveryoptions", 200),
        new("Fyi", "GetNotifications_Success", HttpMethod.Get,
            "/v1/api/fyi/notifications", 200),
        new("Fyi", "GetDisclaimer_BA", HttpMethod.Get,
            "/v1/api/fyi/disclaimer/BA", 200),

        // Fyi — Mutations (toggle setting, mark disclaimer read)
        new("Fyi", "UpdateSetting_BA", HttpMethod.Post,
            "/v1/api/fyi/settings/BA", 200,
            """{"enabled":true}"""),
        new("Fyi", "MarkDisclaimerRead_BA", HttpMethod.Put,
            "/v1/api/fyi/disclaimer/BA", 200, ""),

        // Fyi — Failures
        new("Fyi", "GetDisclaimer_NonExistent", HttpMethod.Get,
            "/v1/api/fyi/disclaimer/NONEXISTENT", 200),
        new("Fyi", "UpdateSetting_NonExistent", HttpMethod.Post,
            "/v1/api/fyi/settings/NONEXISTENT", 200,
            """{"enabled":true}"""),
        new("Fyi", "MarkNotificationRead_NonExistent", HttpMethod.Put,
            "/v1/api/fyi/notifications/0", 200, ""),
        new("Fyi", "DeleteDevice_NonExistent", HttpMethod.Delete,
            "/v1/api/fyi/deliveryoptions/NONEXISTENT99", 404),
    ];
}
