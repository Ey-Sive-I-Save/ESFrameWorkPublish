# Step 07 — HTML materialization

## AI analysis

AI reviews the frozen design and directs static generation, checking each region has a concrete semantic element and each interaction has a DOM hook.

## Execution

`Invoke-ESWebPageStudioStatic.ps1` must consume `DesignSpecPath`, emit every capability region and required data attributes, and write HTML/CSS/manifest/robots/sitemap.

## Return

Return `static-generated` with artifact paths, deterministic hashes, region markers, strategy IDs and `nonClaims`; missing a required marker is `blocked.materialization.missing-region`.
