# Task Read Snapshot 基础工程能力

状态：现行基础工程路由；已实现，待 Unity/AIBrain 运行验收。

`KnowledgeId`: `es.engineering.task-read-snapshot.v1`
`Authority`: `Source + Skill contract`
`EvidenceLevel`: `S2`
`RouteKeys`: `task`, `read`, `snapshot`, `cache`, `hash`, `stale`, `consistency`, `parser`, `projection`, `binary`
`ContentHash`: `ea5d8854f9316d8c5249680456c7f9d51f567b4a65c3bf909bae03cd6c2580cf`
`StaleWhen`: AIBrain 目标分类、Task Read Snapshot 合同、Parser Registry、ProjectionPacket 或缓存键/失效规则变化。

`SourceRefs`:

- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`edc33e609c4fefd2dbaf832043dade36e7ca07beedab1db09703947fa7cb9a19`)
- `.agents/skills/es-task-read-snapshot/SKILL.md` (`16702d156f3ee81bb11925c5f0ef4a8bb8f532eb500e8fdf0908f6241b0e79f5`)
- `.agents/skills/es-task-read-snapshot/governance.json` (`024fe8412bcf7f570d8de08c214c6fc7ea32e4cb7ec58a410ba20ac01f94f29a`)
- `.agents/skills/es-task-read-snapshot/references/task-read-snapshot-contract.md` (`b19ddfcf4ecb2eeb9af82a5b2ae6a844ca636ee26ccf83995d1f33f216cf348f`)
- `ES/Output/FileProjectionParsers.json` (`e70211e6c84af94ada6f844009da3c7bd45fb95575a8726b34c3345382a3d817`)
- `.agents/skills/es-task-read-snapshot/agents/openai.yaml` (`7e296be26b8a4eecc9c77845ffa7ce5a59e70c4a23ef4335f3ee499c64e00f22`)
- `.agents/skills/es-task-read-snapshot/static-replay.manifest.json` (`727d5e0569f479beb7ac9232067a7daf0cea82f2e972da1a680d17d25734f897`)
- `.agents/skills/es-task-read-snapshot/references/evidence-receipt-contract.md` (`573ae3290ef250f3f3aebda0f39547f4fa3b200cca05ed0e1c562c530d50ee6f`)
- `.agents/skills/es-task-read-snapshot/references/static-replay-adapter.md` (`87357b36d77934aeded6d121043a841dea3ba72d41c7968d313dfee822643ad2`)
- `.agents/skills/es-task-read-snapshot/references/static-specialized-acceptance.md` (`07c826ccb94bfe19e5fe26c2fc90a0cc78ed5628499f9700381a44f379d41bb7`)
- `.agents/skills/es-task-read-snapshot/scripts/Invoke-ESProjectionCache.ps1` (`fec6f8816a6b91ada7b36a8b2e1357d86c977d29a627bacadf33b602c55311f7`)
- `.agents/skills/es-task-read-snapshot/scripts/Invoke-ESProjectionPipeline.ps1` (`2bec9e597b5febcbf6661157ecd80fd7736fc57b6d1c2e637ae96f7c65bc485d`)
- `.agents/skills/es-task-read-snapshot/scripts/Invoke-ESTaskReadSnapshot.ps1` (`45a1b72d9976e0a72badf33c44cbe74eda2f98552dce89ee6d082dc630c2dff7`)
- `.agents/skills/es-task-read-snapshot/scripts/Test-es-task-read-snapshot-StaticReplay.ps1` (`f8c34f26b5842ba5e03ad064d27f8c39e08006f5a3cff86de55fa9330ecef06f`)
- `.agents/skills/es-task-read-snapshot/scripts/Test-ESProjectionPacket.ps1` (`2fa0b8fa1dcb81b72756e62a301728883765ef9ab4d8677b4fda752975cbc863`)
- `.agents/skills/es-task-read-snapshot/scripts/Test-ESProjectionRegistry.ps1` (`716901938f6469705e52ca64922af17062160916ea2932a8f1e71962266aeb7f`)
- `.agents/skills/es-task-read-snapshot/scripts/Test-ESSkillEvidence.ps1` (`517812931891e035004f5807932a277cb50538ea60f2ccf81ff7d929fb377909`)

`EvidenceRefs`: Skill S2 receipts、Parser Registry/ProjectionPacket 静态验证和 Portfolio 结果；尚无 Unity Editor 中 `planTask` 自动分类回放证据。

## 职责

`es-task-read-snapshot` 是跨领域读取一致性基础设施，不要求用户记住或点名 Skill。AIBrain 在目标包含多文件一致性、重复读取、快照、源文件哈希、Parser/Projection、二进制解析或文件缓存漂移时追加 `consistency` 路由，并通过本条目的 `relatedSkills` 选择该能力。

它提供两条互补链路：

```text
普通项目文件
  -> Task Read Snapshot Build
  -> 同一任务复用路径/哈希/ParserVersion
  -> 输出结论前 Verify
  -> 漂移时 stale 并重新规划

大型或二进制文件
  -> Parser Registry Resolve
  -> 外部授权 Parser
  -> ProjectionPacket 校验
  -> Projection Cache
  -> 源文件或 Parser 变化后失效
```

## 分类边界

- 自动触发：多文件/多 Skill 使用同一输入状态、重复读取、昂贵或二进制解析、源文件哈希一致性、Projection 缓存复用与漂移验证。
- 不自动触发：单文件一次性读取、Unity 运行时缓存、对象池、GC、帧预算、普通业务缓存。
- `cache` 单词本身不能触发本能力；必须同时出现文件、读取、解析、快照、哈希或投影语义。
- 自动发现只选择工作流，不授予写入、外部 Parser 执行、Unity、Git、网络或发布权限。

## 失败与恢复

没有注册 Parser、ProjectionPacket 与注册项不匹配、源文件哈希漂移、路径越界、缓存损坏或快照 stale 时必须阻断复用。恢复方式是重新规划、重新读取或由已授权 Parser 重新生成并提交投影，禁止静默接受旧结果。
