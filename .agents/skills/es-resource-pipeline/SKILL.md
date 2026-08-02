---
name: es-resource-pipeline
description: Implement, diagnose, or audit the ESFramework resource pipeline across ESAssetLibrary, ESAssetBook, catalogs, ResourcePlan, manifests, runtime providers, ESAssetScope, downloading, and release output. Use for asset collection, preview, export, dependency analysis, runtime loading, provider changes, or resource publishing tasks.
---

# Work on the ES Resource Pipeline

Treat editor authoring, generated plans, release artifacts, runtime providers, and scope ownership as separate layers.

## Workflow

1. Read the AIWarnings start files and `references/project-map.md`.
2. Select one matching AICommand. A read-only analysis command does not authorize a pipeline write.
3. Identify the affected stage: library/book authoring, catalog, reference graph, build plan, bundle manifest, release manifest, provider, downloader, or scope.
4. Trace identifiers and hashes end to end. Verify which file or object is authoritative at each stage.
5. Make the smallest coherent change without creating a second export, preview, manifest, or loading path.
6. Validate dependency closure, duplicate inclusion, unused assets, stable identities, scope disposal, cancellation, and provider replacement as applicable.
7. Use `$es-unity-compile` for editor import and tests. Run real export, Player, IL2CPP, network, or release checks only when required and available.
8. Label every result by its actual evidence layer and run `$es-utf8-guard` for changed text.

## Required boundaries

- Keep `ESAssetLibrary` and `ESAssetBook` as authoring structures, not runtime provider substitutes.
- Keep Library editor-owned and Manifest/Table runtime-owned where the current warnings require that split.
- Do not make runtime code scan the AssetDatabase, editor libraries, or project folders.
- Do not leak `ESAssetScope`; ownership and disposal must be explicit.
- Do not invent fallback loads that bypass the provider or release manifest.
- Do not claim release success from preview, catalog generation, `.csproj` compilation, or Editor-only tests.

## Delivery

Report the affected pipeline stage, authority chain, changed artifacts, dependency and scope checks, evidence labels, and untested publishing layers.
