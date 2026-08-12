# LoadAssetsTests

> **Public-repository boundary.** This reference intentionally documents generic source structure only. Do not add customer-specific context, internal architecture rationale, deployment topology, credentials, or private cross-repository contracts here.


| Field | Source-grounded value |
|---|---|
| Repository | `Essentials` |
| Source file | [`src/PepperDash.Essentials.Tests/ControlSystem/LoadAssetsTests.cs`](../../../src/PepperDash.Essentials.Tests/ControlSystem/LoadAssetsTests.cs) |
| Language | C# |
| Declaration | `class LoadAssetsTests` with declared base/contract list `IDisposable` |
| Accessibility | `public` |
| Namespace/module | `PepperDash.Essentials.Tests.ControlSystem` |

## What

`LoadAssetsTests` is a test-support or verification type that protects a defined behavior from regression. This description is grounded in its source declaration and declared inheritance rather than inferred product behavior.

## Why

The type exists to provide a named boundary in the codebase. Its inheritance, implemented contracts, and public members define what surrounding code may rely on. Preserve that boundary unless a deliberate repository-wide compatibility change is intended.

## How it works

Preserve the declared inheritance/contract relationship: `IDisposable`. Public methods declared in this source file include: `Dispose`, `LoadAssets_EmptyApplicationDirectory_DoesNotThrow`, `LoadAssets_MultipleAssetsZips_ThrowsException`, `LoadAssets_SingleAssetsZip_ExtractsFileToFilePathPrefix`, `LoadAssets_SingleAssetsZip_FileContentsArePreserved`, `LoadAssets_SingleAssetsZip_ZipIsDeletedAfterExtraction`, `LoadAssets_AssetsZipWithDirectoryEntry_CreatesDirectory`, `LoadAssets_AssetsZipWithPathTraversal_ThrowsInvalidOperationException`, `LoadAssets_MultipleHtmlAssetsZips_ThrowsException`, `LoadAssets_SingleHtmlAssetsZip_ExtractsToHtmlDirectory`, `LoadAssets_SingleHtmlAssetsZip_ZipIsDeletedAfterExtraction`, `LoadAssets_HtmlAssetsZipWithPathTraversal_ThrowsInvalidOperationException`. Use repository search to identify callers, implementers, serializers, tests, and configuration references before changing a public name or shape.

## When to modify it

Edit when the protected behavior changes intentionally or the test environment changes. Keep tests independent of unavailable platform services where possible.

## AI-agent change protocol

Before proposing a change, read this declaration, its full source file, all repository references to `LoadAssetsTests`, and its test coverage. Do not invent configuration keys, payload fields, interface members, or lifecycle ordering. Report the affected source files, tests, and consumer boundaries with any proposed change.

## Source authority

The source file linked above is authoritative. This generated reference is an index and decision aid; update it after a declaration, inheritance list, or public member contract changes.
