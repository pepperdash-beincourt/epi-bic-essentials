# AI Documentation Coverage — Essentials

> **Public-repository boundary.** This reference intentionally documents generic source structure only. Do not add customer-specific context, internal architecture rationale, deployment topology, credentials, or private cross-repository contracts here.


| Measure | Count |
|---|---:|
| Tracked C# and TypeScript source files scanned | 719 |
| Declared types indexed | 1180 |
| Per-type reference files | 1176 |

## Coverage semantics

A declaration is included when it matches a C# class, interface, enum, or record declaration, or a TypeScript class, interface, enum, or type alias declaration. This catalog does not duplicate source bodies. It gives an agent the source file, declared relationships, role classification, and safe change protocol, then directs the agent back to source for operational details.

## Deliberate exclusions

The catalog excludes generated dependency folders, secrets, configuration values, raw code bodies, and network-specific details. On public repositories, it also excludes internal cross-repository rationale and product-context information.
