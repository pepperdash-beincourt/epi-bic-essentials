# DebugServiceTests

> **Public-repository boundary.** This reference intentionally documents generic source structure only. Do not add customer-specific context, internal architecture rationale, deployment topology, credentials, or private cross-repository contracts here.


| Field | Source-grounded value |
|---|---|
| Repository | `Essentials` |
| Source file | [`src/PepperDash.Core.Tests/Logging/DebugServiceTests.cs`](../../../src/PepperDash.Core.Tests/Logging/DebugServiceTests.cs) |
| Language | C# |
| Declaration | `class DebugServiceTests` |
| Accessibility | `public` |
| Namespace/module | `PepperDash.Core.Tests.Logging` |

## What

`DebugServiceTests` is a test-support or verification type that protects a defined behavior from regression. This description is grounded in its source declaration and declared inheritance rather than inferred product behavior.

## Why

The type exists to provide a named boundary in the codebase. Its inheritance, implemented contracts, and public members define what surrounding code may rely on. Preserve that boundary unless a deliberate repository-wide compatibility change is intended.

## How it works

Public methods declared in this source file include: `DataStore_InitStore_SetsInitializedFlag`, `DataStore_SetAndGetLocalInt_RoundTrips`, `DataStore_TryGetLocalInt_ReturnsFalse_WhenKeyAbsent`, `DataStore_SetAndGetLocalBool_RoundTrips`, `DataStore_TryGetLocalBool_ReturnsFalse_WhenKeyAbsent`, `DataStore_SetLocalUint_CanBeReadBackAsInt`, `DataStore_Seed_AllowsTestSetupOfReadPaths`, `ServiceRegistration_Register_StoresAllThreeServices`, `ServiceRegistration_Register_AcceptsNullsWithoutThrowing`, `FakeEnvironment_DefaultsToAppliance`, `FakeEnvironment_RaiseProgramStatus_FiresEvent`, `FakeEnvironment_RaiseEthernetEvent_FiresEvent`. Use repository search to identify callers, implementers, serializers, tests, and configuration references before changing a public name or shape.

## When to modify it

Edit when the protected behavior changes intentionally or the test environment changes. Keep tests independent of unavailable platform services where possible.

## AI-agent change protocol

Before proposing a change, read this declaration, its full source file, all repository references to `DebugServiceTests`, and its test coverage. Do not invent configuration keys, payload fields, interface members, or lifecycle ordering. Report the affected source files, tests, and consumer boundaries with any proposed change.

## Source authority

The source file linked above is authoritative. This generated reference is an index and decision aid; update it after a declaration, inheritance list, or public member contract changes.
