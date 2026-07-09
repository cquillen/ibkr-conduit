# FO-3 — Unify session-path Refit error classification

**Spec date:** 2026-07-09 · **Story:** FO-3 · **Risk:** high · **Semver:** `feat!:` (breaking-behavioral)
**Decision of record:** [ADR-0007](../../adr/0007-session-path-error-classification.md) · **Design doc:** §7.8

## Problem (empirically pinned)

The session-establishment path has two classifiers that disagree:

- `ClassifySuppressFailure` (PVR-14, `SessionManager.cs`) splits Refit `ApiException` 429/5xx → `IbkrTransientException`, other 4xx → `IbkrConfigurationException`.
- `WrapCredentialException` (ssodh/init + LST acquisition) classifies a **raw `HttpRequestException`** correctly (500/503/429 → transient, 401/403 → config), but the ssodh/init raw-`Task<T>` path throws Refit **`ApiException`**, and a probe (2026-07-09) confirmed a Refit 12.1.0 `ApiException` does **not** match the `HttpRequestException` branches — it falls through to `_ => IbkrConfigurationException`.

**Result:** every ssodh/init HTTP error, including transient **5xx** and **429**, is reported as a permanent `IbkrConfigurationException`. `IbkrConfigurationException` and `IbkrTransientException` are unrelated `Exception` subclasses, so this is observable: a consumer that catches the configuration type around init never sees the transient case and cannot drive retry/backoff off it.

## Design (per ADR-0007)

Introduce **one shared status→category helper** and call it from both classifiers:

```
IbkrError ClassifyHttpStatus(HttpStatusCode status, Exception inner, <path-specific hint context>)
  429 or >=500        → IbkrTransientException(inner)          // retryable
  401 or 403          → IbkrConfigurationException(<credential hint>, inner)
  other 4xx           → IbkrConfigurationException(<path hint>, inner)
```

- `WrapCredentialException` gains an **`ApiException` arm** that reads `ApiException.StatusCode` (Refit's own property — never the base `HttpRequestException.StatusCode`, which Refit 12 leaves unset) and routes through the shared helper. Its existing arms (`LiveSessionTokenValidationException`, `CryptographicException`, raw `HttpRequestException`, `TaskCanceledException`, `FormatException`/`InvalidOperationException`/`JsonException`, fallback) are **unchanged**.
- `ClassifySuppressFailure` is refactored to call the same helper for its `ApiException` case, preserving its `SuppressMessageIds` hint on non-auth 4xx.
- **401/403 suppression** logs a status-specific Warning ("suppression rejected — authorization failure (HTTP 401/403)"), not the misleading "verify `SuppressMessageIds`".
- Path-specific hints are preserved: LST/crypto branches keep their credential-field guidance; suppress 4xx keeps `SuppressMessageIds`; init 4xx keeps `ConsumerKey, AccessToken`.

**Only behavioral change:** session-path **5xx/429** now → `IbkrTransientException` (was `IbkrConfigurationException`). 401/403 and other 4xx are unchanged.

## TDD steps

1. **Red:** extend `SessionManagerWrapCredentialExceptionTests` with Refit `ApiException` cases (build via the existing `ApiException.Create(...)` helper): `ApiException_500`/`_503`/`_429` → `IbkrTransientException`; `ApiException_401`/`_403` → `IbkrConfigurationException`; `ApiException_400`/`_404` → `IbkrConfigurationException`. Run — the 5xx/429 cases fail (currently return config). *(These are exactly the probe assertions, now permanent.)*
2. **Green:** add the shared helper + the `ApiException` arm to `WrapCredentialException`; refactor `ClassifySuppressFailure` onto the helper. Run — all pass.
3. **Red:** a suppress-path test asserting a 401 suppression failure logs an authorization-worded Warning (not the `SuppressMessageIds` string). Implement the status-specific message. Verify.
4. **Refactor:** confirm the raw-`HttpRequestException` tests (existing) still pass unchanged; confirm no other session call site changed classification for 401/403/other-4xx.
5. **Regression guard:** an integration test (WireMock) where `ssodh/init` returns 503 asserts `EnsureInitializedAsync` throws `IbkrTransientException` (not `IbkrConfigurationException`).

## Done when

A session-path failure classifies identically regardless of whether it arrives as a raw `HttpRequestException` or a Refit `ApiException`: 5xx/429 → `IbkrTransientException`, 401/403 → `IbkrConfigurationException`, other 4xx/non-HTTP → configuration with the path-appropriate hint; and a 401/403 suppression failure logs an authorization-specific Warning. `ssodh/init` returning 503 surfaces as transient.

## Semver / sequencing

`feat!:` — folds into the **0.9.0 breaking train**; must land **before release-please #241 is cut**. RTOS is a live consumer: the reclassification is the safe direction (retry vs. spurious "fix credentials"), and `catch (Exception)` still catches both types.
