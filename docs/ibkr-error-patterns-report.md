# IBKR Client Portal Web API - Error Response Patterns Report

Research date: 2026-04-02
Source: `/workspace/ibkr-conduit/docs/ibkr_api.md` (12,666 lines)

---

## 1. Standard Error Schema

The IBKR Client Portal Web API **does not have a consistent, unified error schema** across all endpoints. There is no single error envelope or standard HTTP error contract. Instead, errors manifest in at least five distinct formats depending on the endpoint family and the nature of the error.

The most common pattern is a JSON object with an `error` string field:

```json
{"error": "description"}
```

However, this is far from universal. Many endpoints that encounter errors return HTTP 200 with domain-specific error indicators embedded in the success response body (e.g., `"success": false`, `"error": null` vs populated, `"fail": "reason"`).

---

## 2. Per-Endpoint Error Documentation

### 2.1 Dynamic Account Endpoints (DYNACCT)

| Detail | Value |
|---|---|
| **Endpoints** | `GET /iserver/account/search/{searchPattern}`, `POST /iserver/dynaccount` |
| **HTTP Status** | 503 |
| **Format** | `{"error": "...", "statusCode": 503}` |
| **Specific Message** | `"Details currently unavailable. Please try again later and contact client services if the issue persists."` |

This is the only place in the documentation where the error response includes both `error` and `statusCode` fields together. Notably, this is returned when a non-DYNACCT account calls a DYNACCT-only endpoint -- effectively a 403 Forbidden scenario being returned as 503.

### 2.2 Alert Activation/Deletion

| Detail | Value |
|---|---|
| **Endpoints** | `POST /iserver/account/{accountId}/alert/activate`, `DELETE /iserver/account/{accountId}/alert/{alertId}` |
| **HTTP Status** | 200 |
| **Format** | `{"request_id": null, "order_id": ..., "success": true/false, "text": "...", "failure_list": null/"..."}` |

Errors are communicated via `"success": false` with details in `failure_list`. This is a 200 OK response with embedded error semantics.

### 2.3 Contract Rules

| Detail | Value |
|---|---|
| **Endpoint** | `POST /iserver/contract/rules`, `GET /iserver/contract/{conid}/info-and-rules` |
| **HTTP Status** | 200 |
| **Format** | `"error"` field within the `rules` object of the response |

The `error` field within the rules response object is documented as: "If rules information can not be received for any reason, it will be expressed here." In the success case, `"error": null` is returned as part of the rules object.

### 2.4 Historical Market Data

| Detail | Value |
|---|---|
| **Endpoint** | `GET /iserver/marketdata/history` |
| **HTTP Status** | 500 (system error), 429 (rate limit) |
| **Format** | `{"error": "description"}` |

This is the only endpoint that explicitly documents both a 500 error and a 429 rate-limiting error. Both use the same `{"error": "description"}` format.

### 2.5 Market Data Unsubscribe (Single)

| Detail | Value |
|---|---|
| **Endpoint** | `POST /iserver/marketdata/unsubscribe` |
| **HTTP Status** | 500 |
| **Format** | `{"error": "unknown"}` |

A 500 is returned when attempting to unsubscribe from a market data feed that is not currently open. This is functionally a 404 (resource not found) or 400 (bad request) condition returned as a 500.

### 2.6 Place Order

| Detail | Value |
|---|---|
| **Endpoint** | `POST /iserver/account/{accountId}/orders` |
| **HTTP Status** | 200 (all cases!) |
| **Formats** | Multiple distinct shapes |

This endpoint has **three different 200 OK response shapes**:

**Success Response:**
```json
[{"order_id": "1234567890", "order_status": "Submitted", "encrypt_message": "1"}]
```

**Confirmation Required (Alternate Response):**
```json
[{
  "id": "uuid",
  "message": ["warning text"],
  "isSuppressed": false,
  "messageIds": ["o163"]
}]
```

**Order Reject (Error as 200):**
```json
{"error": "We cannot accept an order at the limit price..."}
```

The documentation explicitly states: "In the event an order is placed that can not be completed based on account details such as trading permissions or funds, customers will receive a 200 OK response along with an error message explaining the issue."

It also notes: "This is unique from the 200 response used in the Alternate Response Object, or a potential 500 error resulting from invalid request content."

### 2.7 Place Order Reply

| Detail | Value |
|---|---|
| **Endpoint** | `POST /iserver/reply/{replyId}` |
| **HTTP Status** | 503 (timeout) |
| **Format** | Not explicitly documented |

The documentation states: "attempts to confirm invalid replies will result in a timeout (503)." This 503 occurs when the order has been invalidated by other requests being sent before confirming.

### 2.8 Cancel Order

| Detail | Value |
|---|---|
| **Endpoint** | `DELETE /iserver/account/{accountId}/order/{orderId}` |
| **HTTP Status** | Not specified (likely 200) |
| **Format** | `{"error": "OrderID 1 doesn't exist"}` |

The error response uses the standard `{"error": "..."}` pattern.

### 2.9 Modify Order

| Detail | Value |
|---|---|
| **Endpoint** | `POST /iserver/account/{accountId}/order/{orderId}` |
| **HTTP Status** | 200 |
| **Format** | Same alternate response as Place Order |

Returns the confirmation/warning message pattern identical to Place Order.

### 2.10 WhatIf / Preview Order

| Detail | Value |
|---|---|
| **Endpoint** | `POST /iserver/account/{accountId}/orders/whatif` |
| **HTTP Status** | 200 |
| **Format** | `"warn"` and `"error"` fields within the response object |

The response includes dedicated `warn` and `error` string fields. Both return `null` when no warning/error exists. Example warning: `"21/You are trying to submit an order without having market data..."`.

### 2.11 Order Status

| Detail | Value |
|---|---|
| **Endpoint** | `GET /iserver/account/order/status/{orderId}` |
| **HTTP Status** | 503 |
| **Format** | Not explicitly documented |

The documentation states two conditions result in 503:
- Multi-account structures: calling without first switching to the affiliated account.
- Querying status of a cancelled/filled order with no cached information.

### 2.12 Session / Auth Status

| Detail | Value |
|---|---|
| **Endpoint** | `POST /iserver/auth/status` |
| **HTTP Status** | 200 |
| **Format** | `{"authenticated": false, "competing": false, "connected": false, ...}` |

Authentication failure is not an error response per se. The response includes a `fail` field that "Returns the reason for failing to retrieve authentication status" and a `message` field for status messages.

### 2.13 Initialize Brokerage Session

| Detail | Value |
|---|---|
| **Endpoint** | `POST /iserver/auth/ssodh/init` |
| **HTTP Status** | 500 |
| **Format** | Not explicitly documented |

Documentation states: "Users should always pass true [for publish]. Otherwise, a '500' response will be returned."

### 2.14 Tickle / Ping

| Detail | Value |
|---|---|
| **Endpoint** | `POST /tickle` |
| **HTTP Status** | 200 |
| **Format** | Contains `"hmds": {"error": "no bridge"}` |

The `hmds` sub-object contains an `error` field that returns `"no bridge"` when historical data is not being currently requested. This is informational rather than a true error, but uses the error field pattern.

### 2.15 Server Notification Response

| Detail | Value |
|---|---|
| **Endpoint** | `POST /iserver/notification` |
| **HTTP Status** | 200 |
| **Format** | Plain text `"Success"` |

This is the only endpoint that returns a plain text string rather than JSON.

### 2.16 Delete Device (FYI)

| Detail | Value |
|---|---|
| **Endpoint** | `DELETE /fyi/deliveryoptions/{deviceId}` |
| **HTTP Status** | 200 |
| **Format** | Empty string |

Returns an empty string with 200 OK on success. No error format is documented.

### 2.17 Suppress Messages

| Detail | Value |
|---|---|
| **Endpoint** | `POST /iserver/questions/suppress` |
| **HTTP Status** | Not documented |
| **Error Trigger** | "The array supports up to 51 messages... Any additional values will result in a system error." |

No explicit error format is documented, but "system error" implies a 500 response.

### 2.18 HMDS Scanner

| Detail | Value |
|---|---|
| **Endpoint** | `POST /hmds/scanner` |
| **HTTP Status** | 404 |
| **Format** | Not documented |

"Developers should first call the /hmds/auth/init endpoint prior to their request to avoid an initial 404 rejection."

### 2.19 OAuth Consumer Key Registration

| Detail | Value |
|---|---|
| **Context** | OAuth Consumer Key registration via Self-Service Portal |
| **HTTP Status** | 401 |
| **Error** | "Invalid Consumer" |

"Attempting to use the new Consumer Key prior to the reset will result in an error (you may receive a 401 Invalid Consumer error)."

---

## 3. Error Patterns Summary

### 3.1 Common Patterns

| Pattern | Format | Endpoints Using It |
|---|---|---|
| **`{"error": "message"}`** | JSON object with `error` string | Dynamic Account, Market Data History, Market Data Unsubscribe, Cancel Order, Place Order (reject), Tickle (hmds sub-object), Contract Rules (within response) |
| **`{"success": true/false, ...}`** | JSON with boolean success indicator | Alert activate/deactivate, Alert delete, Allocation group CRUD, Allocation presets |
| **Order confirmation/warning** | `[{"id": "uuid", "message": [...], "isSuppressed": false, "messageIds": [...]}]` | Place Order, Modify Order |
| **Auth status fields** | `{"authenticated": bool, "fail": "...", "message": "..."}` | Auth Status, Init Brokerage Session |

### 3.2 Endpoints That Return Errors as 200 OK

This is the most critical finding for an error normalization layer. The following endpoints return HTTP 200 with error content in the body:

| Endpoint | Error Indicator |
|---|---|
| `POST /iserver/account/{accountId}/orders` (order reject) | `{"error": "..."}` returned as 200 |
| `POST /iserver/account/{accountId}/orders` (confirmation needed) | `[{"id": "...", "message": [...]}]` returned as 200 |
| `POST /iserver/account/{accountId}/orders/whatif` | `"error"` and `"warn"` fields within 200 response |
| Alert activate/delete | `"success": false` within 200 response |
| Contract rules | `"error"` field within the rules response object |

### 3.3 HTTP 500 Used for Non-500 Conditions

| Endpoint | Actual Condition | Should Be |
|---|---|---|
| `POST /iserver/marketdata/unsubscribe` | Feed not currently open | 404 Not Found or 400 Bad Request |
| `POST /iserver/auth/ssodh/init` (publish=false) | Invalid request parameter | 400 Bad Request |
| `GET /iserver/marketdata/history` | Generic system error | Depends on cause |

### 3.4 HTTP 503 Used for Various Conditions

| Endpoint | Actual Condition | Should Be |
|---|---|---|
| Dynamic Account endpoints | Account type not supported | 403 Forbidden |
| `GET /iserver/account/order/status/{orderId}` | Wrong account context | 400/403 |
| `GET /iserver/account/order/status/{orderId}` | No cached data | 404 Not Found |
| `POST /iserver/reply/{replyId}` | Reply invalidated by timeout | 410 Gone or 408 Timeout |

### 3.5 Rate Limiting

| Detail | Value |
|---|---|
| **Endpoint** | `GET /iserver/marketdata/history` |
| **HTTP Status** | 429 |
| **Format** | `{"error": "description"}` |
| **General Pacing** | 10 requests per second (mentioned for market data snapshot) |

The 429 response is only explicitly documented for the historical market data endpoint. The general pacing limit of 10 requests per second is mentioned for the market data snapshot but no explicit 429 documentation exists for other endpoints.

### 3.6 Outlier Patterns

| Pattern | Endpoint | Description |
|---|---|---|
| Plain text response | `POST /iserver/notification` | Returns `"Success"` as plain text, not JSON |
| Empty string response | `DELETE /fyi/deliveryoptions/{deviceId}` | Returns empty string with 200 OK |
| XML error response | Flex Web Service | Uses XML with `<Status>Fail</Status>`, `<ErrorCode>`, `<ErrorMessage>` |
| `{"error": "...", "statusCode": N}` | DYNACCT endpoints | Only place where statusCode is included in the error JSON |

---

## 4. Flex Web Service Error Codes

The Flex Web Service uses XML responses and has its own error code system, completely separate from the REST API JSON patterns.

### Error Response Format (XML)

```xml
<FlexStatementResponse timestamp="28 August, 2012 10:37 AM EDT">
    <Status>Fail</Status>
    <ErrorCode>1012</ErrorCode>
    <ErrorMessage>Token has expired.</ErrorMessage>
</FlexStatementResponse>
```

### Error Codes Table

| ErrorCode | ErrorMessage |
|---|---|
| 1001 | Statement could not be generated at this time. Please try again shortly. |
| 1003 | Statement is not available. |
| 1004 | Statement is incomplete at this time. Please try again shortly. |
| 1005 | Settlement data is not ready at this time. Please try again shortly. |
| 1006 | FIFO P/L data is not ready at this time. Please try again shortly. |
| 1007 | MTM P/L data is not ready at this time. Please try again shortly. |
| 1008 | MTM and FIFO P/L data is not ready at this time. Please try again shortly. |
| 1009 | The server is under heavy load. Statement could not be generated at this time. Please try again shortly. |
| 1010 | Legacy Flex Queries are no longer supported. Please convert over to Activity Flex. |
| 1011 | Service account is inactive. |
| 1012 | Token has expired. |
| 1013 | IP restriction. |
| 1014 | Query is invalid. |
| 1015 | Token is invalid. |
| 1016 | Account in invalid. |
| 1017 | Reference code is invalid. |
| 1018 | Too many requests have been made from this token. Please try again shortly. Limited to one request per second, 10 requests per minute (per token). |
| 1019 | Statement generation in progress. Please try again shortly. |
| 1020 | Invalid request or unable to validate request. |
| 1021 | Statement could not be retrieved at this time. Please try again shortly. |

### Flex Error Categories

- **Transient / Retry-able:** 1001, 1004, 1005, 1006, 1007, 1008, 1009, 1018, 1019, 1021
- **Client Error (permanent):** 1010, 1011, 1013, 1014, 1015, 1016, 1017, 1020
- **Data Unavailable:** 1003
- **Auth/Token Errors:** 1011, 1012, 1015

---

## 5. Recommendations for an Error Normalization Layer

Based on the documented patterns, an error normalization `DelegatingHandler` (or set of handlers) needs to address the following concerns:

### 5.1 HTTP Status Code Remapping

The handler must inspect response bodies even on non-error HTTP status codes and remap when appropriate:

| Current Behavior | Normalized Behavior |
|---|---|
| 500 for "feed not open" on unsubscribe | Map to 404 or throw `IbkrResourceNotFoundException` |
| 500 for invalid `publish` param on ssodh/init | Map to 400 or throw `IbkrBadRequestException` |
| 503 for "wrong account type" on DYNACCT | Map to 403 or throw `IbkrForbiddenException` |
| 503 for "no cached data" on order status | Map to 404 or throw `IbkrResourceNotFoundException` |
| 200 with `{"error": "..."}` on order placement | Inspect body and throw `IbkrOrderRejectedException` |

### 5.2 Body-Level Error Detection on 200 OK Responses

The most challenging aspect. The handler must deserialize 200 OK response bodies and check for error indicators:

1. **`{"error": "..."}` pattern:** Check for top-level `error` field that is non-null. This is the order reject pattern.
2. **`[{"id": "...", "message": [...]}]` pattern:** Check for array of objects with `id` and `message` fields. This is the order confirmation/warning pattern -- this should NOT be treated as an error but rather surfaced as a domain-specific result type that the caller must handle.
3. **`{"success": false, ...}` pattern:** Check for `success` field set to `false`.
4. **`"warn"` and `"error"` fields in WhatIf responses:** These are embedded within a larger response object and are informational for the caller rather than system errors.

### 5.3 Error Response Deserialization Strategy

Given the inconsistent schemas, the normalization layer needs a flexible approach:

```
IbkrErrorResponse
  - Error: string?           // from {"error": "..."}
  - StatusCode: int?         // from {"statusCode": N} (rare, only DYNACCT)
  - Success: bool?           // from {"success": true/false}
  - FailureList: string?     // from alert endpoints
  - Message: string?         // from auth status
  - Fail: string?            // from auth status
```

The handler should attempt to deserialize every non-2xx response (and selected 200 responses from known-problematic endpoints) into this union type.

### 5.4 Rate Limiting

- Implement a 429 detection handler that reads the `{"error": "description"}` body.
- Apply proactive rate limiting (10 req/sec for market data) to avoid hitting the server-side limit.
- The Flex Web Service has its own rate limit: 1 req/sec, 10 req/min per token (error code 1018).

### 5.5 Endpoint-Specific Quirks Requiring Special Handling

| Quirk | Handler Behavior |
|---|---|
| Plain text `"Success"` from `/iserver/notification` | Accept `text/plain` content type |
| Empty string from `DELETE /fyi/deliveryoptions/{deviceId}` | Treat empty 200 as success |
| `"hmds": {"error": "no bridge"}` in tickle response | Ignore -- this is informational, not an error |
| XML from Flex Web Service | Separate handler/client needed; cannot share JSON error handling |
| Order confirmation messages requiring `/reply` | Surface as a distinct result type, not an error |

### 5.6 Recommended Exception Hierarchy

```
IbkrException (base)
  IbkrApiException (HTTP-level errors with error body)
    IbkrBadRequestException (400 / remapped from 500)
    IbkrUnauthorizedException (401 -- OAuth failures)
    IbkrForbiddenException (403 / remapped from 503)
    IbkrNotFoundException (404 / remapped from 500/503)
    IbkrRateLimitException (429)
    IbkrServerException (500 -- genuine server errors)
    IbkrServiceUnavailableException (503 -- genuine unavailability)
  IbkrOrderRejectedException (200 with error body)
  IbkrSessionException (auth/session failures)
    IbkrSessionExpiredException
    IbkrCompetingSessionException
```

### 5.7 Implementation Priority

1. **High:** 200-with-error-body detection for order endpoints (safety-critical for trading)
2. **High:** 500-to-appropriate-status remapping (affects retry logic -- retrying a true 400 condition is wasteful and potentially dangerous)
3. **Medium:** 503 remapping (affects error reporting accuracy)
4. **Medium:** Rate limit detection and proactive throttling
5. **Low:** Flex Web Service XML error handling (separate subsystem)
6. **Low:** Plain text / empty response handling (few endpoints affected)
