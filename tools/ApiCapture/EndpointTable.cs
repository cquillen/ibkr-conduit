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
        new("Accounts", "SignaturesAndOwners_Success", HttpMethod.Get,
            "/v1/api/acesws/{accountId}/signatures-and-owners", 200),

        // Accounts — Summary sub-endpoints
        new("Accounts", "GetSummary_Success", HttpMethod.Get,
            "/v1/api/iserver/account/{accountId}/summary", 200),
        new("Accounts", "GetSummaryAvailableFunds_Success", HttpMethod.Get,
            "/v1/api/iserver/account/{accountId}/summary/available_funds", 200),
        new("Accounts", "GetSummaryBalances_Success", HttpMethod.Get,
            "/v1/api/iserver/account/{accountId}/summary/balances", 200),
        new("Accounts", "GetSummaryMargins_Success", HttpMethod.Get,
            "/v1/api/iserver/account/{accountId}/summary/margins", 200),
        new("Accounts", "GetSummaryMarketValue_Success", HttpMethod.Get,
            "/v1/api/iserver/account/{accountId}/summary/market_value", 200),

        // Accounts — Summary failures (IBKR returns 400 for invalid account IDs on summary endpoints)
        new("Accounts", "GetSummary_InvalidAccount", HttpMethod.Get,
            "/v1/api/iserver/account/INVALID999/summary", 400),
        new("Accounts", "GetSummaryBalances_InvalidAccount", HttpMethod.Get,
            "/v1/api/iserver/account/INVALID999/summary/balances", 400),

        // Accounts — DYNACCT endpoints (account doesn't have DYNACCT feature)
        new("Accounts", "SearchDynamicAccount_NoDynacct", HttpMethod.Get,
            "/v1/api/iserver/account/search/DU", 503),
        new("Accounts", "SetDynamicAccount_NoDynacct", HttpMethod.Post,
            "/v1/api/iserver/dynaccount", 401,
            """{"acctId":"DU1234567"}"""),

        // Accounts — Additional failures
        new("Accounts", "SignaturesAndOwners_InvalidAccount", HttpMethod.Get,
            "/v1/api/acesws/INVALID999/signatures-and-owners", 401),

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

        // Contracts — New endpoints (previously missing)
        new("Contracts", "GetInfoAndRules_AAPL", HttpMethod.Get,
            "/v1/api/iserver/contract/265598/info-and-rules?isBuy=true", 200),
        new("Contracts", "GetInfoAndRules_SPY_Sell", HttpMethod.Get,
            "/v1/api/iserver/contract/756733/info-and-rules?isBuy=false", 200),
        new("Contracts", "GetAlgos_AAPL", HttpMethod.Get,
            "/v1/api/iserver/contract/265598/algos?addDescription=1&addParams=1", 200),
        new("Contracts", "GetBondFilters_IBKR", HttpMethod.Get,
            "/v1/api/iserver/secdef/bond-filters?symbol=BOND&issuerId=e1400715", 200),
        new("Contracts", "SearchBySymbolPost_AAPL", HttpMethod.Post,
            "/v1/api/iserver/secdef/search", 200,
            """{"symbol":"AAPL"}"""),
        new("Contracts", "SearchBySymbolPost_Bond", HttpMethod.Post,
            "/v1/api/iserver/secdef/search", 200,
            """{"symbol":"IBKR","secType":"BOND"}"""),
        new("Contracts", "GetTradingScheduleNew_SPY", HttpMethod.Get,
            "/v1/api/contract/trading-schedule?conid=756733", 200),

        // Contracts — New endpoint failures
        new("Contracts", "GetInfoAndRules_InvalidConid", HttpMethod.Get,
            "/v1/api/iserver/contract/999999999/info-and-rules?isBuy=true", 400),
        new("Contracts", "GetAlgos_InvalidConid", HttpMethod.Get,
            "/v1/api/iserver/contract/999999999/algos", 503),
        new("Contracts", "GetBondFilters_InvalidIssuer", HttpMethod.Get,
            "/v1/api/iserver/secdef/bond-filters?symbol=BOND&issuerId=INVALID", 200),
        new("Contracts", "GetTradingScheduleNew_InvalidConid", HttpMethod.Get,
            "/v1/api/contract/trading-schedule?conid=0", 400),

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
        // Alerts — Success
        // ---------------------------------------------------------------
        // KNOWN LIMITATION: Alert creation (POST /alert) returns 403 on paper
        // accounts with OAuth authentication. This is a confirmed IBKR server-side
        // restriction — the request is well-formed but the server rejects it for
        // paper/demo accounts. As a result, all dependent operations (get detail,
        // deactivate, delete by captured alertId) are excluded since they require
        // a successfully created alert.
        // ---------------------------------------------------------------

        // 1. List alerts — may be empty initially
        new("Alerts", "ListAlerts_Initial", HttpMethod.Get,
            "/v1/api/iserver/account/{accountId}/alerts", 200),

        // 2. Get MTA alert — should always exist
        new("Alerts", "GetMtaAlert_Success", HttpMethod.Get,
            "/v1/api/iserver/account/mta", 200),

        // 3. Create alert — 403 on paper accounts (see comment block above)
        new("Alerts", "CreateAlert_SPY_Price", HttpMethod.Post,
            "/v1/api/iserver/account/{accountId}/alert", 403,
            """{"alertName":"CaptureTest","alertMessage":"API capture test alert","alertRepeatable":0,"outsideRth":0,"tif":"GTC","conditions":[{"conidex":"756733@SMART","logicBind":"n","operator":"<=","triggerMethod":"0","type":1,"value":"1.00"}]}"""),

        // 4. List alerts again — still empty since create returned 403
        new("Alerts", "ListAlerts_AfterCreate", HttpMethod.Get,
            "/v1/api/iserver/account/{accountId}/alerts", 200),

        // Alerts — Failures
        new("Alerts", "CreateAlert_EmptyBody", HttpMethod.Post,
            "/v1/api/iserver/account/{accountId}/alert", 400,
            """{}"""),
        new("Alerts", "CreateAlert_MissingRequiredFields", HttpMethod.Post,
            "/v1/api/iserver/account/{accountId}/alert", 500,
            """{"alertName":"Incomplete"}"""),
        // IBKR returns 200 with error body "MTA alert tool ID is wrong =0" for nonexistent alert IDs
        new("Alerts", "GetAlertDetail_NonExistent", HttpMethod.Get,
            "/v1/api/iserver/account/alert/0?type=Q", 200),
        new("Alerts", "ActivateAlert_NonExistent", HttpMethod.Post,
            "/v1/api/iserver/account/{accountId}/alert/activate", 503,
            """{"alertId":0,"alertActive":1}"""),
        new("Alerts", "DeleteAlert_NonExistent", HttpMethod.Delete,
            "/v1/api/iserver/account/{accountId}/alert/0", 503),

        // ---------------------------------------------------------------
        // Watchlists — Success (create, list, detail, delete)
        // ---------------------------------------------------------------
        // Create a watchlist with SPY and AAPL (id must be numeric)
        new("Watchlists", "CreateWatchlist_Success", HttpMethod.Post,
            "/v1/api/iserver/watchlist", 200,
            """{"id":"99999","name":"Capture Test","rows":[{"C":756733},{"C":265598}]}"""),
        new("Watchlists", "GetWatchlists_Success", HttpMethod.Get,
            "/v1/api/iserver/watchlists?SC=USER_WATCHLIST", 200),
        // First call returns limited fields (C, conid, name)
        new("Watchlists", "GetWatchlist_ById_FirstCall", HttpMethod.Get,
            "/v1/api/iserver/watchlist?id=99999", 200),
        // Second call returns full contract info (assetClass, ticker, chineseName, etc.)
        new("Watchlists", "GetWatchlist_ById_SecondCall", HttpMethod.Get,
            "/v1/api/iserver/watchlist?id=99999", 200),
        // Create duplicate — same ID again to see if it overwrites or errors
        new("Watchlists", "CreateWatchlist_DuplicateId", HttpMethod.Post,
            "/v1/api/iserver/watchlist", 200,
            """{"id":"99999","name":"Duplicate Test","rows":[{"C":8314}]}"""),
        // Verify duplicate overwrote: should show "Duplicate Test" with IBM (8314), not original
        new("Watchlists", "GetWatchlist_AfterDuplicate", HttpMethod.Get,
            "/v1/api/iserver/watchlist?id=99999", 200),
        // Clean up
        new("Watchlists", "DeleteWatchlist_Success", HttpMethod.Delete,
            "/v1/api/iserver/watchlist?id=99999", 200),

        // Watchlists — Edge cases
        new("Watchlists", "CreateWatchlist_EmptyRows", HttpMethod.Post,
            "/v1/api/iserver/watchlist", 200,
            """{"id":"88880","name":"Empty Rows Test","rows":[]}"""),
        new("Watchlists", "DeleteWatchlist_EmptyRows", HttpMethod.Delete,
            "/v1/api/iserver/watchlist?id=88880", 200),
        new("Watchlists", "CreateWatchlist_NoRowsField", HttpMethod.Post,
            "/v1/api/iserver/watchlist", 200,
            """{"id":"88881","name":"No Rows Field Test"}"""),
        new("Watchlists", "DeleteWatchlist_NoRowsField", HttpMethod.Delete,
            "/v1/api/iserver/watchlist?id=88881", 200),
        new("Watchlists", "CreateWatchlist_InvalidConid", HttpMethod.Post,
            "/v1/api/iserver/watchlist", 200,
            """{"id":"88882","name":"Invalid Conid Test","rows":[{"C":0},{"C":999999999}]}"""),
        new("Watchlists", "DeleteWatchlist_InvalidConid", HttpMethod.Delete,
            "/v1/api/iserver/watchlist?id=88882", 200),

        // Watchlists — Failures
        new("Watchlists", "CreateWatchlist_EmptyBody", HttpMethod.Post,
            "/v1/api/iserver/watchlist", 400,
            """{}"""),
        new("Watchlists", "CreateWatchlist_MissingName", HttpMethod.Post,
            "/v1/api/iserver/watchlist", 503,
            """{"id":"88888","rows":[{"C":756733}]}"""),
        new("Watchlists", "CreateWatchlist_NonNumericId", HttpMethod.Post,
            "/v1/api/iserver/watchlist", 503,
            """{"id":"not_a_number","name":"Bad ID Test","rows":[{"C":756733}]}"""),
        new("Watchlists", "GetWatchlist_NonExistent", HttpMethod.Get,
            "/v1/api/iserver/watchlist?id=77777777", 503),
        // NOTE: IBKR returns 200 for deleting nonexistent watchlists when
        // other watchlist calls have been made in the same session.
        // Returns 503 only on a cold session.
        new("Watchlists", "DeleteWatchlist_NonExistent", HttpMethod.Delete,
            "/v1/api/iserver/watchlist?id=77777777", 200),

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

        // ---------------------------------------------------------------
        // Orders — Success (stateful: place -> status -> modify -> cancel)
        // ---------------------------------------------------------------

        // Prime the live orders endpoint (IBKR quirk: first call returns empty)
        new("Orders", "GetLiveOrders_Prime", HttpMethod.Get,
            "/v1/api/iserver/account/orders", 200),

        // Wait and get actual orders
        new("Orders", "GetLiveOrders_Success", HttpMethod.Get,
            "/v1/api/iserver/account/orders", 200),

        // Place a limit order at $1.00 (won't fill) — may go direct or need confirmation
        // When direct: response has order_id. When confirmation needed: response has id (replyId).
        new("Orders", "PlaceOrder_LimitSPY", HttpMethod.Post,
            "/v1/api/iserver/account/{accountId}/orders", 200,
            """{"orders":[{"conid":756733,"side":"BUY","quantity":1,"orderType":"LMT","price":1.00,"tif":"GTC"}]}""",
            CaptureAs: "orderId", CaptureJsonPath: "$[0].order_id"),

        // WhatIf preview
        new("Orders", "WhatIfOrder_SPY", HttpMethod.Post,
            "/v1/api/iserver/account/{accountId}/orders/whatif", 200,
            """{"orders":[{"conid":756733,"side":"BUY","quantity":1,"orderType":"LMT","price":1.00,"tif":"GTC"}]}"""),

        // Get status of placed order
        new("Orders", "GetOrderStatus_Placed", HttpMethod.Get,
            "/v1/api/iserver/account/order/status/{orderId}", 200),

        // Modify the placed order (change price from $1.00 to $1.01)
        // Note: unlike place order, modify takes the order object directly, not wrapped in {"orders":[...]}
        new("Orders", "ModifyOrder_ChangePrice", HttpMethod.Post,
            "/v1/api/iserver/account/{accountId}/order/{orderId}", 200,
            """{"conid":756733,"side":"BUY","quantity":1,"orderType":"LMT","price":1.01,"tif":"GTC"}"""),

        // Get trades (may be empty if order hasn't filled)
        new("Orders", "GetTrades_Success", HttpMethod.Get,
            "/v1/api/iserver/account/trades", 200),

        // Cancel the placed order
        new("Orders", "CancelOrder_Placed", HttpMethod.Delete,
            "/v1/api/iserver/account/{accountId}/order/{orderId}", 200),

        // Orders — Failures
        new("Orders", "GetOrderStatus_NonExistent", HttpMethod.Get,
            "/v1/api/iserver/account/order/status/000000000", 400),

        new("Orders", "CancelOrder_NonExistent", HttpMethod.Delete,
            "/v1/api/iserver/account/{accountId}/order/000000000", 400),

        new("Orders", "WhatIfOrder_InvalidConid", HttpMethod.Post,
            "/v1/api/iserver/account/{accountId}/orders/whatif", 400,
            """{"orders":[{"conid":0,"side":"BUY","quantity":1,"orderType":"LMT","price":1.00,"tif":"GTC"}]}"""),

        // ---------------------------------------------------------------
        // EventContracts — Forecast/prediction market endpoints
        // Category tree has no params. Other endpoints need conids from
        // the tree response. Using known ForecastEx conids:
        // - FF (Federal Funds Rate) underlying conid varies by market
        // - We'll capture the tree first, then use captured conids
        // ---------------------------------------------------------------
        new("EventContracts", "GetCategoryTree_Success", HttpMethod.Get,
            "/v1/api/forecast/category/tree", 200),

        // US Fed Funds Target Rate: underlying conid=658663572
        new("EventContracts", "GetMarket_FedFunds", HttpMethod.Get,
            "/v1/api/forecast/contract/market?underlyingConid=658663572", 200),

        // Contract rules, details, schedules — use specific contract conid from market response
        // Fed Funds "Above 3.125%" Jan 2027: conid=722489372
        new("EventContracts", "GetContractRules_FedFunds", HttpMethod.Get,
            "/v1/api/forecast/contract/rules?conid=722489372", 200),
        new("EventContracts", "GetContractDetails_FedFunds", HttpMethod.Get,
            "/v1/api/forecast/contract/details?conid=722489372", 200),
        new("EventContracts", "GetContractSchedules_FedFunds", HttpMethod.Get,
            "/v1/api/forecast/contract/schedules?conid=722489372", 200),

        // Failures
        new("EventContracts", "GetMarket_InvalidConid", HttpMethod.Get,
            "/v1/api/forecast/contract/market?underlyingConid=0", 500),
        new("EventContracts", "GetContractRules_InvalidConid", HttpMethod.Get,
            "/v1/api/forecast/contract/rules?conid=0", 500),

        // ---------------------------------------------------------------
        // ComboTest — Place a stock combo (SPY+QQQ spread), capture combo
        // positions, then close the position.
        // US Stock spread_conid = 28812380
        // SPY conid = 756733, QQQ conid = 320227571
        // Format: {spread_conid};;;{leg1_conid}/{ratio},{leg2_conid}/{ratio}
        // Positive ratio = Buy, Negative ratio = Sell
        // ---------------------------------------------------------------

        // 1. Place combo spread: Buy 1 SPY, Sell 1 QQQ at market
        new("ComboTest", "PlaceCombo_SpyQqq", HttpMethod.Post,
            "/v1/api/iserver/account/{accountId}/orders", 200,
            """{"orders":[{"conidex":"28812380;;;756733/1,320227571/-1","orderType":"MKT","side":"BUY","tif":"DAY","quantity":1}]}""",
            CaptureAs: "comboOrderId", CaptureJsonPath: "$[0].order_id"),

        // 2. If confirmation needed, confirm it
        new("ComboTest", "ReplyConfirm_Combo", HttpMethod.Post,
            "/v1/api/iserver/reply/{comboOrderId}", 200,
            """{"confirmed":true}"""),

        // 3. Check combo positions (may need a moment to settle)
        new("ComboTest", "GetComboPositions_AfterPlace", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/combo/positions", 200),

        // 4. Check combo positions again (warm-up may be needed)
        new("ComboTest", "GetComboPositions_SecondCall", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/combo/positions", 200),

        // 5. Close the combo: Sell 1 SPY, Buy 1 QQQ (reverse the original)
        new("ComboTest", "CloseCombo_SpyQqq", HttpMethod.Post,
            "/v1/api/iserver/account/{accountId}/orders", 200,
            """{"orders":[{"conidex":"28812380;;;756733/-1,320227571/1","orderType":"MKT","side":"SELL","tif":"DAY","quantity":1}]}""",
            CaptureAs: "closeOrderId", CaptureJsonPath: "$[0].order_id"),

        // 6. Confirm close if needed
        new("ComboTest", "ReplyConfirm_Close", HttpMethod.Post,
            "/v1/api/iserver/reply/{closeOrderId}", 200,
            """{"confirmed":true}"""),

        // ---------------------------------------------------------------
        // Test — Exploratory captures for undocumented/new endpoints
        // ---------------------------------------------------------------
        new("Test", "AccountBalances_Success", HttpMethod.Get,
            "/v1/api/iserver/account/{accountId}/summary/balances", 200),

        // ---------------------------------------------------------------
        // WatchlistBug — Validate that delete-nonexistent returns 200
        // after a warm-up call vs 503 on a cold session.
        // Run this category in isolation to test cold-vs-warm behavior.
        // ---------------------------------------------------------------
        // ---------------------------------------------------------------
        // Investigate401 — Side-by-side valid vs invalid account on
        // portfolio endpoints that return 401 for bad account IDs.
        // Purpose: verify response headers and body to distinguish
        // "fake 401" (bad account) from real auth 401.
        // ---------------------------------------------------------------

        // Valid account — should return 200
        new("Investigate401", "Positions_ValidAccount", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/positions/0", 200),
        new("Investigate401", "Summary_ValidAccount", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/summary", 200),
        new("Investigate401", "Ledger_ValidAccount", HttpMethod.Get,
            "/v1/api/portfolio/{accountId}/ledger", 200),

        // Invalid account — returns 401 with empty body
        new("Investigate401", "Positions_InvalidAccount", HttpMethod.Get,
            "/v1/api/portfolio/INVALID999/positions/0", 401),
        new("Investigate401", "Summary_InvalidAccount", HttpMethod.Get,
            "/v1/api/portfolio/INVALID999/summary", 401),
        new("Investigate401", "Ledger_InvalidAccount", HttpMethod.Get,
            "/v1/api/portfolio/INVALID999/ledger", 401),

        // Signatures endpoint — also returns 401 with empty body for invalid account
        new("Investigate401", "Signatures_ValidAccount", HttpMethod.Get,
            "/v1/api/acesws/{accountId}/signatures-and-owners", 200),
        new("Investigate401", "Signatures_InvalidAccount", HttpMethod.Get,
            "/v1/api/acesws/INVALID999/signatures-and-owners", 401),

        new("WatchlistBug", "WarmUp_GetAllWatchlists", HttpMethod.Get,
            "/v1/api/iserver/watchlists?SC=USER_WATCHLIST", 200),
        new("WatchlistBug", "DeleteNonExistent_AfterWarmUp", HttpMethod.Delete,
            "/v1/api/iserver/watchlist?id=77777777", 200),
    ];
}
