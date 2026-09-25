# TestDevice

> **Public-repository boundary.** This reference intentionally documents generic source structure only. Do not add customer-specific context, internal architecture rationale, deployment topology, credentials, or private cross-repository contracts here.


| Field | Source-grounded value |
|---|---|
| Repository | `Essentials` |
| Source file | [`src/PepperDash.Core.Tests/Devices/DeviceTests.cs`](../../../src/PepperDash.Core.Tests/Devices/DeviceTests.cs) |
| Language | C# |
| Declaration | `class TestDevice` with declared base/contract list `Device` |
| Accessibility | `private` |
| Namespace/module | `PepperDash.Core.Tests.Devices` |

## What

`TestDevice` is a test-support or verification type that protects a defined behavior from regression. This description is grounded in its source declaration and declared inheritance rather than inferred product behavior.

## Why

The type exists to provide a named boundary in the codebase. Its inheritance, implemented contracts, and public members define what surrounding code may rely on. Preserve that boundary unless a deliberate repository-wide compatibility change is intended.

## How it works

Preserve the declared inheritance/contract relationship: `Device`. Public methods declared in this source file include: `Constructor_SingleArg_SetsKey`, `Constructor_SingleArg_SetsNameToEmpty`, `Constructor_TwoArg_SetsKeyAndName`, `Constructor_KeyWithDot_StillSetsKey`, `ToString_WithName_FormatsKeyDashName`, `ToString_WithoutName_UsesDashPlaceholder`, `DefaultDevice_IsNotNull`, `DefaultDevice_HasKeyDefault`, `CustomActivate_DefaultReturnTrue`, `Deactivate_DefaultReturnsTrue`, `Activate_CallsCustomActivate_AndReturnsItsResult`, `Activate_TrueWhenCustomActivateReturnsTrue`. Use repository search to identify callers, implementers, serializers, tests, and configuration references before changing a public name or shape.

## When to modify it

Edit when the protected behavior changes intentionally or the test environment changes. Keep tests independent of unavailable platform services where possible.

## AI-agent change protocol

Before proposing a change, read this declaration, its full source file, all repository references to `TestDevice`, and its test coverage. Do not invent configuration keys, payload fields, interface members, or lifecycle ordering. Report the affected source files, tests, and consumer boundaries with any proposed change.

## Source authority

The source file linked above is authoritative. This generated reference is an index and decision aid; update it after a declaration, inheritance list, or public member contract changes.
