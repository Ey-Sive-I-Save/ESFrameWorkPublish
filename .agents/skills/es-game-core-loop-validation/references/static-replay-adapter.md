Responsibility profile: engineering

本 Skill 的 StaticDeepReplay 入口为 `scripts/Test-es-game-core-loop-validation-StaticReplay.ps1`。它逐项执行核心回归，并通过 `references/bindings/static-claim.coverage.json` 拒绝无证据声明；证据 Join 必须匹配 `planHash` 与 `sourceSnapshotHash`。项目机制稳定性专项入口为 `scripts/Test-ESGameCoreLoopProjectMechanismStability.ps1`，用于连续回放 ES 平台证据、TaskContext Adapter 与跨进程 CAS。所有入口只验证静态/子进程合同，不启动 Unity、不写入项目、不产生发布结论。
