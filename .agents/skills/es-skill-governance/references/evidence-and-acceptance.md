# Evidence and acceptance

Use the S0-S6 contract in `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/项目最高警告_P0_AI交付声明与责任契约_AI协作警告.md`.

| Level | Proves | Does not prove |
|---|---|---|
| S0 | Design, constraints and plan | Implementation or usability |
| S1 | Source and entry point exist | Compilation or runtime |
| S2 | Specified static build/check passes | Unity import, reload or interaction |
| S3 | Unity import, reload and Console scope passes | Real interaction or release |
| S4 | Specified Editor interaction passes | PlayMode, Player, performance or release |
| S5 | Runtime/EditMode/PlayMode behavior passes | Player, IL2CPP or release outside scope |
| S6 | Player/IL2CPP, resources, performance and release scope passes | Claims beyond tested range |

## Independent status axes

```text
Tier: Workflow
Maturity: Verifying
Delivery: Implemented-Unverified
Current evidence: S2 (static checks passed; Unity interaction not run)
```

`Blocked` and `Failed` are delivery conclusions, not maturity levels. `Stable` requires the tier acceptance bar and reproducible evidence.

## Required tests

Every Skill needs: positive, invalid input, denied expansion, and repeat/idempotency cases.

Workflow and Engineering additionally need interruption/recovery and a scale note covering item count, batching, first-run versus steady-state cost, concurrency/re-entry and bottlenecks.

Engineering additionally needs a boundary matrix mapping external systems, permissions, compatibility promises and release artifacts to owners and evidence levels, plus an acceptance replay another agent can reproduce.

## Delivery report

```text
目标：
Skill：
等级：SmallTool / Workflow / Engineering
成熟度：Proposed / Scaffolded / Implementing / Integrating / Verifying / Stable / Deprecated / Archived
交付结论：Designed / Implemented-Unverified / Blocked / Failed / Accepted / Released
当前等级：S0-S6（平台、入口和范围）
实际修改：
已验证：命令、输入、输出和证据路径
未验证：
阻断原因：
影响范围：
下一步：
```

Do not write transient Console errors, warning counts or build logs into `CurrentStatus`; point to a re-readable receipt instead.
