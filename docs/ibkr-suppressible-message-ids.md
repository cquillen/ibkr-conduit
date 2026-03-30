# IBKR Suppressible Message IDs

Reference list of message IDs that can be passed to `POST /iserver/questions/suppress` to auto-accept order confirmation prompts. Compiled from the [IBKR official documentation](https://www.interactivebrokers.com/campus/ibkr-api-page/cpapi-v1/#suppressible-id) and cross-referenced against the [ibind](https://github.com/Voyz/ibind) reference implementation.

## Usage

Pass desired IDs via `IbkrClientOptions.SuppressMessageIds` when registering the IBKR client:

```csharp
services.AddIbkrClient<IIbkrPortfolioApi>(credentials, new IbkrClientOptions
{
    SuppressMessageIds = new List<string> { "o163", "o354", "o451" },
});
```

The `SessionManager` calls `/iserver/questions/suppress` with these IDs after every brokerage session initialization. Suppressions are per-session — they must be re-applied after each session init.

**Important:** Suppress messages on an as-needed basis. Over-suppressing can lead to unexpected order submissions. The API supports up to 51 message IDs per request.

## Complete List

Sources: IBKR official docs, ibind `_MESSAGE_ID_TO_QUESTION_TYPE` (`ibind/client/ibkr_utils.py:178-200`)

| Message ID | Description | ibind QuestionType |
|---|---|---|
| `o163` | The following order exceeds the price percentage limit | `PRICE_PERCENTAGE_CONSTRAINT` |
| `o354` | You are submitting an order without market data. We strongly recommend against this as it may result in erroneous and unexpected trades. Are you sure you want to submit this order? | `MISSING_MARKET_DATA` |
| `o382` | The following value exceeds the tick size limit | `TICK_SIZE_LIMIT` |
| `o383` | The following order size exceeds the Size Limit. Are you sure you want to submit this order? | `ORDER_SIZE_LIMIT` |
| `o403` | This order will most likely trigger and fill immediately. Are you sure you want to submit this order? | `TRIGGER_AND_FILL` |
| `o451` | The following order value estimate exceeds the Total Value Limit. Are you sure you want to submit this order? | `ORDER_VALUE_LIMIT` |
| `o2136` | Mixed allocation order warning | — |
| `o2137` | Cross side order warning | — |
| `o2165` | Warns that instrument does not support trading in fractions outside regular trading hours | — |
| `o10082` | Called Bond warning | — |
| `o10138` | The following order size modification exceeds the size modification limit | `SIZE_MODIFICATION_LIMIT` |
| `o10151` | Warns about risks with Market Orders | — |
| `o10152` | Warns about risks associated with stop orders once they become active | — |
| `o10153` | Confirm Mandatory Cap Price — IB may set a cap/floor price to avoid trading inconsistent with a fair and orderly market | `MANDATORY_CAP_PRICE` |
| `o10164` | Traders are responsible for understanding cash quantity details, which are provided on a best efforts basis only | `CASH_QUANTITY` |
| `o10223` | Cash Quantity Order Confirmation — orders that express size using a monetary value are provided on a non-guaranteed basis | `CASH_QUANTITY_ORDER` |
| `o10288` | Warns about risks associated with market orders for Crypto | — |
| `o10331` | You are about to submit a stop order. Please be aware of the various stop order types available and the risks associated with each one | `STOP_ORDER_RISKS` |
| `o10332` | OSL Digital Securities LTD Crypto Order Warning | — |
| `o10333` | Option Exercise at the Money warning | — |
| `o10334` | Warns that order will be placed into current omnibus account instead of currently selected global account | — |
| `o10335` | Serves internal Rapid Entry window | — |
| `o10336` | This security has limited liquidity — heightened risk | — |
| `p6` | This order will be distributed over multiple accounts — familiarize yourself with allocation methods | — |

## Recommended Suppressions for Automated Trading

For automated/algorithmic trading, consider suppressing these common prompts that would block order flow:

```csharp
var automatedTradingSuppressions = new List<string>
{
    "o163",   // Price percentage constraint
    "o354",   // No market data warning
    "o382",   // Tick size limit
    "o383",   // Order size limit
    "o403",   // Trigger and fill immediately
    "o451",   // Order value limit
    "o10138", // Size modification limit
    "o10151", // Market order risks
    "o10152", // Stop order risks
    "o10153", // Mandatory cap price
    "o10331", // Stop order type risks
};
```

**Note:** The `o` prefix on most IDs corresponds to TWS API Error Codes. The `p` prefix (e.g., `p6`) is for allocation-related warnings.

## Discovery

Additional message IDs may be encountered during live trading. When an order triggers an unrecognized prompt, the `/iserver/reply` response includes `messageIds` in the response body. Add newly discovered IDs to this list and to your suppression configuration as appropriate.
