# ADR-0007 — Session-path error classification: uniform HTTP-status taxonomy

**Status:** Accepted · **Date:** 2026-07-09
**Relates to:** PVR-14 (`ClassifySuppressFailure`, the suppress-path classifier this generalizes); ADR-0003/ADR-0004 (the order-outcome and competing-session sides of the same `IbkrError` taxonomy); design doc §7.8 (new), §9.9. **Implemented by:** FO-3 (spec `docs/superpowers/specs/2026-07-09-fo-3-session-error-classification.md`).

## Context

Two session-path failure classifiers diverged:

- **`ClassifySuppressFailure`** (PVR-14) applies a finer HTTP-status split on the question-suppression path: Refit `ApiException` 429 or 5xx → `IbkrTransientException`; other `ApiException` (4xx) → `IbkrConfigurationException` (pointing at `SuppressMessageIds`); transport/other → `WrapCredentialException`.
- **`WrapCredentialException`** (ssodh/init + LST acquisition) classifies a **raw `HttpRequestException`** correctly (5xx/429 → transient, 401/403 → config), but a probe on 2026-07-09 confirmed that the type actually thrown by the ssodh/init raw-`Task<T>` path — Refit's **`ApiException`** — does **not** match the `HttpRequestException` branch in Refit 12.1.0 (its base `HttpRequestException.StatusCode` is left unset and the type isn't matched as an `HttpRequestException` by the switch), so it falls through to the `_ => IbkrConfigurationException` fallback.

**Consequence of the gap:** every ssodh/init HTTP error — including transient **5xx** and **429** rate-limits — is currently reported as a permanent `IbkrConfigurationException` ("verify your credentials"), when the consumer (RTOS) should retry/back off. `IbkrConfigurationException` and `IbkrTransientException` are unrelated `Exception` subclasses (no common base), so a consumer that catches the configuration type around init genuinely never sees the transient case. This is a real, behaviorally-observable misclassification (empirically pinned; not the falsified ERR-1 premise — see that finding's retraction).

## Decision

One session-path error taxonomy, keyed on HTTP status, applied **uniformly** across question-suppression, ssodh/init, and LST acquisition:

1. **5xx or 429 → `IbkrTransientException`** — retryable; the server or a rate limiter is the cause, not the consumer's configuration.
2. **401 or 403 → `IbkrConfigurationException`** — credential/authorization failure (bad or expired `ConsumerKey`/`AccessToken`).
3. **Any other 4xx, and all non-HTTP failures** (crypto, DH, JSON, timeout→transient, unknown) → keep today's classification and path-specific hints (LST-validation/crypto branches keep their credential-field guidance; suppress 4xx keeps the `SuppressMessageIds` hint).
4. **Refit `ApiException` is classified by its own `.StatusCode`** (never the base `HttpRequestException.StatusCode`, which Refit 12 leaves unset), via a shared status→category helper that both `WrapCredentialException` and `ClassifySuppressFailure` call — so the classification no longer depends on a Refit internal.
5. A **401/403 suppression** failure logs a status-specific Warning (authorization, not the misleading "verify `SuppressMessageIds`").

## Alternatives considered

- **Minimal patch** (add only an `ApiException` 5xx→transient arm to `WrapCredentialException`): fixes the reported symptom but leaves the two classifiers' logic duplicated and free to drift again. Rejected in favor of one shared helper.
- **Keep init 5xx as configuration (status quo):** tells the consumer to fix credentials during an IBKR outage and defeats retry/backoff. Rejected.
- **Introduce a common `IbkrError` base so the reclassification isn't source-breaking:** a larger taxonomy change beyond this story's scope; can be a future additive ADR. Rejected here — the base-`Exception` catch already covers both, so the break is the safe direction.

## Consequences

- 📦 **Breaking-behavioral (`feat!:`):** a session-path **5xx/429** now throws `IbkrTransientException` instead of `IbkrConfigurationException`. A consumer catching `IbkrConfigurationException` around session init to handle transient server errors will no longer catch them — the **safe** direction (they should retry; `catch (Exception)` and `catch (IbkrTransientException)` both still work). Folds into the **0.9.0 breaking train** (before release-please #241 is cut).
- **401/403 init/LST failures keep classifying as configuration** (correct — bad credentials), so the *only* behavioral change is 5xx/429 → transient. No change for other 4xx.
- The session-path taxonomy becomes uniform and unit-testable; whether a Refit `ApiException` happens to populate the base `HttpRequestException.StatusCode` no longer silently determines the outcome.

## Relationships

Design doc §7.8 (new); §9.9 (order-outcome side of the taxonomy); ADR-0003 (order-POST replay/ambiguity), ADR-0004 (competing-session/health side); PVR-14 (the suppress classifier this generalizes); error taxonomy `src/IbkrConduit/Errors/`. Implemented by FO-3.
