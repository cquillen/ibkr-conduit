# Flex Web Service Setup

The IBKR Flex Web Service provides on-demand access to historical account data -- trade confirmations, cash transactions, open positions, corporate actions, and more -- via XML-based queries. Unlike the real-time Client Portal API, Flex queries return settled historical data and are the authoritative source for trade confirmations and account activity.

## Prerequisites

- An Interactive Brokers account (live or paper)
- Flex Query access enabled on the account (most account types have this by default)
- IbkrConduit configured with OAuth credentials (see [Credential Setup Guide](credential-setup.md))

## Step 1: Create a Flex Query Template

1. Log in to [IBKR Account Management](https://www.interactivebrokers.com/sso/Login) or [Client Portal](https://portal.interactivebrokers.com/)
2. Navigate to **Reports** (or **Performance & Reports**) > **Flex Queries**
3. Under **Activity Flex Query** or **Trade Confirmation Flex Query**, click the **+** (Create) button
4. Configure the query:
   - **Query Name** -- a descriptive name (e.g., "Cash Transactions - Last 30 Days")
   - **Date Period** -- the default date range (e.g., "Last 30 Calendar Days")
   - **Sections** -- select the data sections to include:
     - For cash transactions: enable **Cash Transactions**
     - For trade confirmations: enable **Trade Confirms**, **Symbol Summary**, and **Orders**
   - **Format** -- leave as XML (required by IbkrConduit)
   - **Breakout by Day** -- set to **No** for best performance (IbkrConduit handles both shapes, but consolidated responses are 10x smaller)
5. Click **Save** (or **Continue** and **Create**)
6. Note the **Query ID** -- the numeric ID shown next to the query name in the list

Repeat for each report type you need. Common configurations:

| Report Type | Flex Query Type | Key Sections |
|-------------|-----------------|--------------|
| Cash Transactions | Activity Flex | Cash Transactions |
| Trade Confirmations | Trade Confirmation Flex | Trade Confirms, Symbol Summary, Orders |

## Step 2: Get the Flex Token

1. On the **Flex Queries** page, click **Flex Web Service Configuration** (or the gear icon)
2. Click **Generate Token** to create a new Flex Web Service token
3. Copy the token and store it securely -- it will not be shown again
4. Note the IP restriction settings if applicable to your deployment

The Flex token is separate from your OAuth credentials. It authenticates requests to the Flex Web Service endpoint specifically.

## Step 3: Configure IbkrConduit

Pass the Flex token and query IDs when registering the client:

```csharp
services.AddIbkrClient(opts =>
{
    opts.Credentials = creds;
    opts.FlexToken = "your-flex-token";
    opts.FlexQueries.CashTransactionsQueryId = "1464458";        // from Step 1
    opts.FlexQueries.TradeConfirmationsQueryId = "1454602";      // from Step 1
});
```

## Step 4: Run Queries

### Typed Methods

Typed methods provide strongly-typed results for common report types:

```csharp
// Cash transactions (uses the query template's configured period)
var cashResult = await client.Flex.GetCashTransactionsAsync();
foreach (var tx in cashResult.Value.CashTransactions)
{
    Console.WriteLine($"{tx.SettleDate} {tx.Type} {tx.Amount:C} {tx.Description}");
}

// Trade confirmations (with date range override)
var tradesResult = await client.Flex.GetTradeConfirmationsAsync(
    new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 9));
foreach (var tc in tradesResult.Value.TradeConfirmations)
{
    Console.WriteLine($"{tc.TradeDate} {tc.BuySell} {tc.Quantity} {tc.Symbol} @ {tc.Price}");
}
```

### Generic Method

For query types without a dedicated typed method, use the generic escape hatch:

```csharp
var result = await client.Flex.ExecuteQueryAsync("your-query-id");
Console.WriteLine($"Query: {result.Value.QueryName} ({result.Value.QueryType})");

// With date range override (works reliably for Trade Confirmation Flex queries)
var result = await client.Flex.ExecuteQueryAsync("your-query-id", "20260401", "20260409");
```

**Date override caveat:** Runtime date overrides work reliably for Trade Confirmation Flex (TCF) queries but can hang server-side for Activity Flex (AF) queries with multi-day ranges. For AF queries, configure the date period in the query template instead.

## Troubleshooting

### Error Codes

Flex Web Service errors are returned as `IbkrFlexError` with a numeric `ErrorCode`. The library classifies errors as retryable (transient) or permanent (configuration issue) and handles retries automatically for transient errors.

| Code | Description | Retryable | Fix |
|------|-------------|-----------|-----|
| 1001 | Statement could not be generated at this time | Yes | Automatic retry -- no action needed |
| 1003 | Statement is not available | No | Check query ID and date range |
| 1004 | Statement is incomplete at this time | Yes | Automatic retry -- no action needed |
| 1005 | Settlement data is not ready at this time | Yes | Automatic retry -- no action needed |
| 1006 | FIFO P/L data is not ready at this time | Yes | Automatic retry -- no action needed |
| 1007 | MTM P/L data is not ready at this time | Yes | Automatic retry -- no action needed |
| 1008 | MTM and FIFO P/L data is not ready at this time | Yes | Automatic retry -- no action needed |
| 1009 | Server is under heavy load | Yes | Automatic retry -- no action needed |
| 1010 | Legacy Flex Queries are no longer supported | No | Convert to Activity Flex in the IBKR portal |
| 1011 | Service account is inactive | No | Contact IBKR support |
| 1012 | Token has expired | No | Generate a new token in the IBKR portal (Step 2) |
| 1013 | IP restriction | No | Update IP whitelist in Flex Web Service Configuration, or remove the restriction |
| 1014 | Query is invalid | No | Check the query ID -- it may have been deleted or modified |
| 1015 | Token is invalid | No | Generate a new token in the IBKR portal (Step 2) |
| 1016 | Account is invalid | No | Verify the account associated with the query |
| 1017 | Reference code is invalid | No | Internal error -- the poll reference code was rejected |
| 1018 | Too many requests | Yes | Automatic retry -- rate limited to 1 req/sec, 10 req/min per token |
| 1019 | Statement generation in progress | Yes | Automatic retry -- no action needed |
| 1020 | Invalid request or unable to validate request | No | Check query parameters and token |
| 1021 | Statement could not be retrieved at this time | Yes | Automatic retry -- no action needed |

### Common Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| `InvalidOperationException`: no Flex token configured | `FlexToken` not set in options | Set `opts.FlexToken` in `AddIbkrClient` |
| `InvalidOperationException`: query ID not set | Typed method called without corresponding query ID | Set the query ID in `opts.FlexQueries` |
| Error 1015: Token is invalid | Token was regenerated or never set correctly | Copy the current token from the IBKR portal |
| Error 1012: Token has expired | Flex tokens expire periodically | Generate a new token in the IBKR portal |
| Error 1013: IP restriction | Request came from an IP not on the whitelist | Update the IP whitelist or remove the restriction |
| Error 1014: Query is invalid | Query ID does not exist or was deleted | Verify the query exists in the IBKR portal and copy the correct numeric ID |
| Query hangs or times out | Activity Flex query with runtime date override | Use the query template's configured period instead of runtime date overrides for AF queries |
| Empty results | No data for the configured date range | Widen the date range or verify the account has activity in that period |

## Rate Limits

The Flex Web Service enforces its own rate limits separate from the Client Portal API:

- **1 request per second** per token
- **10 requests per minute** per token

IbkrConduit enforces these limits client-side and retries automatically when error 1018 (too many requests) is returned.

## Adding New Report Types

To add typed support for additional Flex report types (corporate actions, open positions, etc.), see the [Flex Report Types contributor guide](flex-report-types.md).
