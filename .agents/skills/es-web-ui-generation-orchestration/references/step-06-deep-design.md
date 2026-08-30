# Step 06 — Deep design acceptance

## AI analysis

Design regions, component boundaries, data shape, state transitions, responsive profiles, visual tokens, loading/empty/error states, and progressive-enhancement behavior. Every region must explain its purpose and relation to the objective.

## Execution

Run `Invoke-ESWebPageStudioDeepDesign.ps1` with the accepted capability compilation and prompt plan. Require `designStatus=accepted` before HTML generation.

## Return

Return a `WebPageStudioDeepDesignSpec` containing `regions`, `interactions`, `visualSystem`, `responsiveProfiles`, `states`, `dataContract`, `htmlDirectives`, `executionPlan`, and hashes. Script-self-accepted designs are invalid: `blocked.design.independent-acceptance-missing`.

