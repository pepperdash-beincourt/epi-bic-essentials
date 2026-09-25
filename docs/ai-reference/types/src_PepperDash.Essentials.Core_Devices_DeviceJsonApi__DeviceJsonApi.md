# DeviceJsonApi

> **Public-repository boundary.** This reference intentionally documents generic source structure only. Do not add customer-specific context, internal architecture rationale, deployment topology, credentials, or private cross-repository contracts here.


| Field | Source-grounded value |
|---|---|
| Repository | `Essentials` |
| Source file | [`src/PepperDash.Essentials.Core/Devices/DeviceJsonApi.cs`](../../../src/PepperDash.Essentials.Core/Devices/DeviceJsonApi.cs) |
| Language | C# |
| Declaration | `class DeviceJsonApi` |
| Accessibility | `public` |
| Namespace/module | `PepperDash.Essentials.Core` |

## What

`DeviceJsonApi` is a device-model type that owns lifecycle, controls, feedback, or communication responsibilities. This description is grounded in its source declaration and declared inheritance rather than inferred product behavior.

## Why

The type exists to provide a named boundary in the codebase. Its inheritance, implemented contracts, and public members define what surrounding code may rely on. Preserve that boundary unless a deliberate repository-wide compatibility change is intended.

## How it works

Public methods declared in this source file include: `DoDeviceActionWithJson`, `DoDeviceAction`, `DoDeviceActionAsync`, `GetProperties`, `GetPropertyByName`, `GetMethods`, `GetApiMethods`, `FindObjectOnPath`, `SetProperty`. Use repository search to identify callers, implementers, serializers, tests, and configuration references before changing a public name or shape.

## When to modify it

Edit when the device lifecycle, communications, control behavior, or feedback model changes. Confirm activation ordering before issuing hardware work.

## AI-agent change protocol

Before proposing a change, read this declaration, its full source file, all repository references to `DeviceJsonApi`, and its test coverage. Do not invent configuration keys, payload fields, interface members, or lifecycle ordering. Report the affected source files, tests, and consumer boundaries with any proposed change.

## Source authority

The source file linked above is authoritative. This generated reference is an index and decision aid; update it after a declaration, inheritance list, or public member contract changes.
