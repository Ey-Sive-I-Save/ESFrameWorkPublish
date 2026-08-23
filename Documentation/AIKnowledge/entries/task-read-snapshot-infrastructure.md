# Task Read Snapshot 基础工程能力

状态：现行基础工程路由；已实现，待 Unity/AIBrain 运行验收。

`KnowledgeId`: `es.engineering.task-read-snapshot.v1`
`Authority`: `Source + Skill contract`
`EvidenceLevel`: `S2`
`RouteKeys`: `task`, `read`, `snapshot`, `cache`, `hash`, `stale`, `consistency`, `parser`, `projection`, `binary`
`ContentHash`: `7c793cc10b7b88aa4938d56777ab01ae725bb00812d5fad7c5091a8030dcba95`
`StaleWhen`: AIBrain 目标分类、Task Read Snapshot 合同、Parser Registry、ProjectionPacket 或缓存键/失效规则变化。

`SourceRefs`:

- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`9735b55bf6b2df8758050f2b84b053aabc0438ddf633c3c61ba43e4d684349d9`)
- `.agents/skills/es-task-read-snapshot/SKILL.md` (`2aba9af6d3adf5d3437ab6afd893dbe2dd5fc909c5b8b3bd23864c1f43b60cea`)
- `.agents/skills/es-task-read-snapshot/governance.json` (`024fe8412bcf7f570d8de08c214c6fc7ea32e4cb7ec58a410ba20ac01f94f29a`)
- `.agents/skills/es-task-read-snapshot/references/task-read-snapshot-contract.md` (`b19ddfcf4ecb2eeb9af82a5b2ae6a844ca636ee26ccf83995d1f33f216cf348f`)
- `ES/Output/FileProjectionParsers.json` (`e70211e6c84af94ada6f844009da3c7bd45fb95575a8726b34c3345382a3d817`)

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
