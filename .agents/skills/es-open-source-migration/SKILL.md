---
name: es-open-source-migration
description: >-
  Plan and execute a bounded migration from a pinned open-source framework into
  ES naming and assembly conventions. Use when a freshly checked-out external
  repository must be renamed in place across code, paths, metadata, docs and
  UI text. The default one-shot mode targets that external checkout directly;
  an isolated output remains an explicit compatibility mode.
---

# ES Open Source Migration

## Overview

建立“源框架 → ES 命名化 checkout”的可重放迁移计划：固定 revision/license，生成持久化
映射表，再对刚拉下来的外部 checkout 做一次性原地整体替换。默认不建立最终外部副本，
当前 ESFramework 项目本体始终受保护；隔离输出仅作为显式兼容模式。

## Hard boundaries

The protected-target boundary and deterministic partitioning are explicit acceptance assertions.

- **External target**：源树必须位于当前项目根之外；禁止使用当前项目根、`Assets/`、
  `Packages/`、`ProjectSettings/` 或 `.agents/` 作为源树目录。
- **Scope**：默认 `external-checkout-in-place`。允许修改用户明确指定的外部 checkout，
  但不得修改当前 ESFramework 项目源码、资产、asmdef、Git、历史、审计状态、发布物或 Unity 状态。
- **Already downloaded**：源树已在外部目录时直接以该 checkout 为目标；若源树误落入当前项目，
  仍必须拒绝扫描和写入，交由人工处理。
- **Manual substitution**：人工可替代下载、解压、许可证核对、文件搬运或某个验证步骤，但
  必须记录 `performedBy=human`、输入、输出和未证实项。
- **Agent gate**：先完成方案、输入清单和持久映射表，并将 `status=mapping-approved` 后，
  才能启动多 Agent。Agent 按不重叠的程序集/领域分区，只能写自己的隔离输出；合并由单一
  owner 负责。
- **Fail closed**：URL、固定版本、目标仓库、许可证或迁移范围缺失时返回 `NeedsInputs`，
  不猜测源仓库、不自动联网、不扩大路径。

## Persistent map

并行工作前必须创建或更新：

`ES/AISpace/Public/es-open-source-migration/migration-map.json`

该文件是唯一共享协调面。每行映射记录源相对路径、目标相对路径（外部只读时为 `null`）、
`adopt/adapt/rewrite/exclude/defer` 分类、owner、状态、源/目标哈希、兼容影响和回滚策略。
不得写入凭据或可变的源绝对路径。

## Workflow

1. **Frame**：收集 URL、固定 tag/commit、license、目标项目根、范围、兼容窗口、owner、批次、
   重试、超时和停止条件；验证隔离路径在目标根之外。
2. **Baseline**：只读检查目标 branch/HEAD/worktree。仅哈希有界权威文件和源清单，记录脏树边界。
3. **Source inventory**：盘点源 namespace、assembly/package、公开 API、序列化身份、GUID/元数据、
   测试和构建入口。网络与 Git 写入始终是独立动作，默认关闭。
4. **Mapping design**：分类每个候选并填充持久映射表；审查冲突后才能启动 Agent。
5. **Human substitution**：人工代做步骤必须记录替代者、输入、输出、时间和未证实项；不能冒充
   机器步骤已执行。
6. **Parallel phase**：仅当 `agentStartGate=mapping-approved`；按程序集/领域分区，声明 `maxAgents`，
   禁止共享写目标，要求确定性结果包。
7. **Dry-run batches**：先产出差异和回执，不改受保护目标。重复批次必须幂等，或因 map revision/hash
   冲突而明确拒绝。
8. **Acceptance**：执行 namespace/type 唯一性、asmdef 方向/循环、严格 UTF-8、身份/meta 保留、
   `git diff --check` 和迁移计划验证器。Unity、Runtime、Player、IL2CPP、发布证据另行授权。
9. **Recovery**：取消、超时、哈希漂移或部分失败时隔离当前批次、保护目标、标记映射 stale，
   只从最后一个已接受的映射版本恢复。

## ES mapping rules

- 跨域稳定协议映射到 `Assets/Plugins/ES/0_Stand/BaseDefine_Law`；有充分理由时使用
  `INTER_<InterfaceName>.cs`。
- Attribute/声明元数据映射到 `0_Stand/Attributes`；领域接口留在对应 Runtime；Editor 扩展留在
  Editor 程序集。
- 保留 `.meta` GUID 和序列化身份；协议迁移不得留下重复定义或未决兼容别名。
- 遵守 `ES_Stand → ES_Design → ES_Logic → ES_Editor` 依赖方向；`ESFramework.AITest` Runtime
  耦合必须单独审查。
- 第三方代码和许可证义务在 provenance 证实前只能归为 `exclude` 或 `defer`。

## Evidence and failure modes

静态证据只证明源码、配置和确定性检查；`runtime-not-run` 不是失败证明。必须覆盖缺失输入、
目标根越界、源树已落入目标、权限扩张、重复并行启动、哈希漂移、取消和中断恢复等负例。

## Engineering controls

- 执行前声明目标路径、源文件/字节上限、批次、重试、超时、并发和停止条件。
- 外部 checkout、目标写入、网络、Git、删除/重命名和 Runtime 是独立权限；缺少或过期证据时
  fail closed。
- 记录 positive、invalid-input、denied-expansion、repeat/idempotency 和 interruption/recovery
  的机器可读回执。

## Skill 使用披露

遵循项目根 `AGENTS.md` 和 `.agents/README.md` 的“Skill 使用披露”规范。实际使用本 Skill 时，
首次进度必须说明它用于隔离外部源树和建立 ES 映射；最终答复列出本轮实际影响工作的 Skill 与作用。
披露不等于授权、网络访问、Agent 启动或验收证据。

## Resources

- `references/migration-map.schema.json`：持久化映射表结构。
- `references/static-replay-adapter.md`：StaticDeepReplay 适配与证据边界。
- `static-replay.manifest.json`：确定性回放用例。
- `scripts/Test-es-open-source-migration-StaticReplay.ps1`：Skill 本地静态回放入口。

## Transparent ES identity remap

对于中小仓库，可以先用 `scripts/New-ESTransparentSymbolMap.ps1` 自动生成候选映射，再交给
重写器执行。它从 `package.json`/源目录名和非 `src/pro` 文件中的本地声明提取标识符，生成
`ES<Identifier>` 透明身份；声明发现只读取代码扩展名，并先屏蔽注释、字符串和模糊的
`import type` 语法。已有 `ES` 冲突、路径/字节预算、非法 UTF-8 或许可证边界会 fail-closed，
不会创建兼容别名或把第三方符号误认为 ES 自有实现。

```powershell
& '.agents/skills/es-open-source-migration/scripts/New-ESTransparentSymbolMap.ps1' `
  -SourceRoot 'F:\aaProject\Dyad.External' `
  -OutputMapPath 'F:\aaProject\Dyad.ESMigration.Work\esified\symbols\auto-map.json' `
  -SourceRevision '<fixed-revision>'
```

自动生成的 map 仍须保留 `source.treeSha256`、`source.revision`、`collisionPolicy` 和
`licensePolicy`；它是命名化输入，不是许可证批准、编译通过或 Runtime 接入授权。

当目标是“让刚拉下来的外部仓库整体看起来由 ES 主导”，使用
`scripts/Invoke-ESTransparentNamespaceRemap.ps1` 的原地模式。它只接受项目根之外的源树，
按显式 `symbols[].source -> symbols[].es` 和 `textReplacements` 做严格 UTF-8 的整体替换，
默认同时重命名路径段和文件名，并将控制回执放在源树的 `.es-migration` 目录；不会生成最终
外部副本。原地提交采用临时事务树、源漂移复扫、逐文件哈希和中断恢复，成功后删除事务备份。
`src/pro/**`、`.git`、`LICENSE*`、`NOTICE*` 保持保护边界；已接受的原地结果只有在 manifest、
receipt、逐文件哈希和文件集合全部一致时才允许幂等回放，否则 fail-closed。

原地一键入口的默认调用：

```powershell
& '.agents/skills/es-open-source-migration/scripts/Invoke-ESAutoTransparentNamespaceRemap.ps1' `
  -SourceRoot 'F:\aaProject\Dyad.External' -SourceTextTokens @('Legacy Author')
```

若必须保留旧的隔离输出行为，显式传入 `-CopyToOutput -OutputRoot <external-output>`；该兼容
模式仍默认只改代码标识符，除非同时指定 `-WholeRepository` 给底层重写器。`-DryRun` 在原地
默认下只使用临时 map，不写入源 checkout。

示例：

```powershell
& '.agents/skills/es-open-source-migration/scripts/Invoke-ESTransparentNamespaceRemap.ps1' `
  -SourceRoot 'F:\aaProject\Dyad.External' `
  -OutputRoot 'F:\aaProject\Dyad.ESMigration.Work\esified\namespace-remap' `
  -MappingPath 'F:\aaProject\Dyad.ESMigration.Work\esified\symbols\symbol-map.json' `
  -SourceRevision '<fixed-revision>' -RenamePathSegments
```

若希望中小仓库直接走完整链路，可使用一键入口；它会先做所有路径越界预检，再生成 map 并
调用同一重写器。默认不传 `-OutputRoot` 即直接改写 `SourceRoot`，map、manifest、receipt 和
恢复日志保存在 `SourceRoot/.es-migration`；不会写入当前 ESFramework 项目，也不会建立最终
外部副本。只有显式传入 `-CopyToOutput -OutputRoot` 才切换到隔离输出。

```powershell
& '.agents/skills/es-open-source-migration/scripts/Invoke-ESAutoTransparentNamespaceRemap.ps1' `
  -SourceRoot 'F:\aaProject\Dyad.External' `
  -SourceRevision '<fixed-revision>' -SourceTextTokens @('Legacy Author')
```

该工具的原地输出是整体身份重映射结果，不是 AST/编译/语义等价证明，也不是 Unity/Runtime/Player/IL2CPP
或许可证清权；默认不重写 `.git` 历史，也不改 `LICENSE/NOTICE`。
