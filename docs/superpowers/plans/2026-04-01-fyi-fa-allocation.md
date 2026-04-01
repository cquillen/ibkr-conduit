# Plan: FYI/Notifications and FA Allocation Endpoints

**Date:** 2026-04-01
**Branch:** `feat/fyi-notifications-fa-allocation`
**Goal:** Add the final 20 endpoints (12 FYI + 8 FA Allocation) to complete full API surface coverage.

---

## Category 1: FYI/Notifications (12 endpoints)

New namespace `IbkrConduit.Fyi` with `IIbkrFyiApi` Refit interface.
Exposed on the facade as `client.Notifications` via `IFyiOperations`.

### Endpoints

| # | Method | Path | Description |
|---|--------|------|-------------|
| 1 | GET | `/fyi/unreadnumber` | Unread bulletin count |
| 2 | GET | `/fyi/settings` | Notification settings/subscriptions |
| 3 | POST | `/fyi/settings/{typecode}` | Enable/disable subscription |
| 4 | GET | `/fyi/disclaimer/{typecode}` | Get disclaimer for typecode |
| 5 | PUT | `/fyi/disclaimer/{typecode}` | Mark disclaimer as read |
| 6 | GET | `/fyi/deliveryoptions` | Get delivery options |
| 7 | PUT | `/fyi/deliveryoptions/email` | Enable/disable email delivery |
| 8 | POST | `/fyi/deliveryoptions/device` | Register device |
| 9 | DELETE | `/fyi/deliveryoptions/{deviceId}` | Remove device |
| 10 | GET | `/fyi/notifications` | Get notifications list |
| 11 | GET | `/fyi/notifications/more` | Get more notifications (pagination) |
| 12 | PUT | `/fyi/notifications/{notificationId}` | Mark notification read |

### Models

- `UnreadBulletinCountResponse` - `BN` (int)
- `FyiSettingItem` - `FC`, `FN`, `FD`, `H`, `A`
- `FyiSettingUpdateRequest` - `enabled` (bool)
- `FyiAcknowledgementResponse` - `V`, `T`
- `FyiDisclaimerResponse` - `FC`, `DT`
- `FyiDeliveryOptionsResponse` - `M` (int), `E` (array of devices)
- `FyiDeviceInfo` - `NM`, `I`, `UI`, `A`
- `FyiDeviceRequest` - `deviceName`, `deviceId`, `uiName`, `enabled`
- `FyiNotification` - `R`, `D`, `MS`, `MD`, `ID`, `HT`, `FC`
- `FyiNotificationReadResponse` - `V`, `T`, `P`

---

## Category 2: FA Allocation (8 endpoints)

New namespace `IbkrConduit.Allocation` with `IIbkrAllocationApi` Refit interface.
Exposed on the facade as `client.Allocations` via `IAllocationOperations`.

### Endpoints

| # | Method | Path | Description |
|---|--------|------|-------------|
| 1 | GET | `/iserver/account/allocation/accounts` | Allocatable sub-accounts |
| 2 | GET | `/iserver/account/allocation/group` | List allocation groups |
| 3 | POST | `/iserver/account/allocation/group` | Add allocation group |
| 4 | POST | `/iserver/account/allocation/group/single` | Get single group |
| 5 | POST | `/iserver/account/allocation/group/delete` | Delete group |
| 6 | PUT | `/iserver/account/allocation/group` | Modify group |
| 7 | GET | `/iserver/account/allocation/presets` | Get presets |
| 8 | POST | `/iserver/account/allocation/presets` | Set presets |

### Models

- `AllocationAccountsResponse` - `accounts` (array of account data)
- `AllocationAccountData` - `name`, `data` (array of key-value)
- `AllocationAccountDataEntry` - `key`, `value`
- `AllocationGroupListResponse` - `data` (array of group summaries)
- `AllocationGroupSummary` - `name`, `allocation_method`, `size`
- `AllocationGroupDetail` - `name`, `accounts`, `default_method`
- `AllocationGroupAccount` - `name`, `amount`
- `AllocationGroupRequest` - same fields for add/modify
- `AllocationGroupNameRequest` - `name` (for single/delete)
- `AllocationPresetsResponse` - 5 typed fields
- `AllocationSuccessResponse` - `success`

---

## Implementation Steps

1. **FYI Refit interface** (`Fyi/IIbkrFyiApi.cs`)
2. **FYI models** (`Fyi/IIbkrFyiApiModels.cs`)
3. **FYI operations interface** (`Client/IFyiOperations.cs`)
4. **FYI operations implementation** (`Client/FyiOperations.cs`)
5. **Allocation Refit interface** (`Allocation/IIbkrAllocationApi.cs`)
6. **Allocation models** (`Allocation/IIbkrAllocationApiModels.cs`)
7. **Allocation operations interface** (`Client/IAllocationOperations.cs`)
8. **Allocation operations implementation** (`Client/AllocationOperations.cs`)
9. **Update `IIbkrClient`** - add `Notifications` and `Allocations` properties
10. **Update `IbkrClient`** - add constructor params and properties
11. **Update DI registration** - register new Refit clients and operations
12. **Unit tests** - model deserialization tests for both categories
13. **Integration tests** - WireMock endpoint tests for both categories
14. **Update audit** - mark all 20 endpoints as done

## TDD Approach

Write failing unit tests for model deserialization first, then implement models.
Write failing integration tests for Refit endpoints, then implement interfaces.
Write failing build for operations/facade, then implement operations and wiring.
