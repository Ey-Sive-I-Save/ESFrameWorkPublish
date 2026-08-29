# P0 UTF-8 编码边界：保真迁移条目

`KnowledgeId`: `es.aiwarning.p0.utf8-encoding-boundary.v1`  
`Authority`: `AIWarnings` 原文与项目 UTF-8 验证规则  
`RouteKeys`: `aiwarnings`, `p0`, `utf8`, `encoding`, `text-integrity`, `evidence-boundary`  
`HashSchema`: `v2`  
`ContentHash`: `32d3d54dbb3b4bf9c65dcfe6ac461b834027f76170bef39e9640110f951f9b4e`
`SourceSetHash`: `32d3d54dbb3b4bf9c65dcfe6ac461b834027f76170bef39e9640110f951f9b4e`
`EntryBodyHash`: `70065395ec2454f4e3a84a66079ccac3e2f50bc68a08000009bde888d13d6690`
`EvidenceLevel`: `S1`  
`RuntimeEvidence`: `runtime-not-run`  
`StaleWhen`: 原 Warning、Start 链、UTF-8 Skill/验证器或任一 SourceRef 哈希变化。

## 迁移说明

Warning 本体只保留长期 P0 边界、禁止事项、权限和证据边界；本条目接纳完整详细规则与原文快照。Knowledge 是导航与保真存档，不授予写入、Runtime、Git、发布或其他权限。

## 迁移后详细规则

- 严格 UTF-8 是唯一文本编码；乱码按数据损坏处理，发现即停止扩散并保留现场。
- 禁止 PowerShell 默认编码、ANSI、GBK、`-Encoding Default`、隐式区域设置写入，禁止 `Get-Content file | Set-Content file`。
- 禁止未经确认的整文件互转、乱码复制、猜测式批量替换和无差别目录重写。
- 修改优先 `apply_patch`；其他写入须确认 UTF-8 行为；疑似乱码先严格解码再判断来源。
- 修改后严格 UTF-8 解码、扫描 `U+FFFD`/典型乱码、执行 `git diff --check` 并检查 diff；Unity/C# 证据另行验证。
- 历史乱码只恢复可由语义、相邻代码、版本记录或权威配置确认的内容，无法确认则保持不动并报告。

## 原文保真快照

迁移前原 Warning 的完整内容已按原顺序保存在下方；此快照与迁移台账中的原始 SHA-256 绑定。

（原 Warning 全文快照：见 `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` 对应条目；该 ledger 保存原始路径、原始行数、原始 UTF-8 字节数和原始 SHA-256。当前 Warning 仍可通过 Git 工作树追溯，未删除原始事实。）

## 原文完整快照（迁移前）

```markdown
# P0：UTF-8 唯一编码，禁止默认代码页覆写与机械转码

> 级别：P0（违反即停止当前修改并优先恢复文件）  
> 适用范围：项目内所有源码、配置、文档、Shader、JSON、YAML、CSV 及其他文本文件。  
> 适用对象：所有 AI、自动化脚本和人工批处理操作。

## 最高结论

本项目所有文本文件统一使用 UTF-8。任何工具都不得凭系统区域设置、PowerShell 默认编码、ANSI、GBK 或其他本地代码页读取后再覆写文件。

乱码属于数据损坏，不是格式问题。发现乱码时必须停止扩散，保留现场并按上下文逐处恢复；禁止用未经验证的整文件转码掩盖损坏。

## P0 禁令

1. 禁止使用 PowerShell 默认编码读写含中文或编码未知的文件。
2. 禁止通过管道读取后直接覆写原文件，例如：

   ```powershell
   Get-Content file | Set-Content file
   ```

3. 禁止使用 `-Encoding Default`、ANSI、系统活动代码页或隐式编码覆写项目文本。
4. 禁止机械执行 GBK/ANSI/Default Encoding 与 UTF-8 之间的整文件互转。
5. 禁止把已经出现的乱码文本复制到其他源码、注释、日志或文档中。
6. 禁止在无法确认原文时猜测并批量替换。必须结合字段语义、相邻代码、版本记录或权威配置逐处恢复。
7. 禁止为了“统一编码”无差别重写整个目录；仅修改任务需要的文件和内容。

## AI 修改规范

1. 修改现有文本优先使用 `apply_patch`，保持未涉及字节和用户改动不变。
2. 使用其他写入工具前，必须显式确认其 UTF-8 行为；不能确认时不得使用。
3. 如果命令通道会污染中文，C# 字符串可暂用 `\uXXXX` 表示，保证运行时和 Unity Inspector 显示正确；不得写入已经损坏的中文字符。
4. 遇到疑似乱码，先严格按 UTF-8 解码检查，再判断是历史损坏还是当前操作造成。
5. 当前操作一旦造成乱码，立即停止后续编辑；只恢复本次操作造成的内容，不得回滚其他人的工作。

## 每次修改后的强制验证

涉及文本文件的修改完成后，至少执行以下检查：

1. 对修改文件进行严格 UTF-8 解码验证；解码失败即视为 P0 失败。
2. 扫描 Unicode 替换字符 `U+FFFD`。
3. 扫描典型乱码组合；为避免本文自身成为扫描误报，规则可使用对应的 Unicode 码点或由检查脚本维护，命中后必须人工复核，不能直接批量删除或转码。
4. 执行 `git diff --check`，确认不存在空白和补丁结构问题。
5. 在条件允许时执行 Unity/C# 编译验证；若被无关既有错误阻断，必须明确记录阻断位置，不能声称完整验证通过。
6. 查看目标文件 diff，确认没有整文件异常改写、换行风格漂移或任务外内容变化。

## 允许的安全示例

PowerShell 仅读取时也应显式声明 UTF-8：

```powershell
Get-Content -LiteralPath $path -Encoding UTF8
```

严格 UTF-8 验证应使用会对非法字节抛出异常的解码器，不能只以“终端看起来正常”为依据。终端字体、代码页和显示结果都不能代替字节验证。

## 发现历史乱码时

1. 缩小到具体文件和具体字段。
2. 查找同一语义的类型名、变量名、Tooltip、旧版本或调用方。
3. 人工恢复能够确认的原文。
4. 无法确认的内容保持不动并报告，不得猜测。
5. 修复后执行本警告规定的全部验证。

## 验收标准

- 修改文件可被严格 UTF-8 解码。
- 不新增 `U+FFFD` 字符或典型乱码。
- 不发生默认代码页覆写。
- 不发生未经确认的整文件转码。
- Diff 只包含任务要求的变更。

本警告优先级为 P0。任何开发效率、批处理便利或工具默认行为都不能覆盖本规则。
```

## SourceRefs

- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/编码与文本（Encoding）/项目最高警告_P0_UTF8唯一编码_禁止AI默认代码页覆写与机械转码_AI协作警告.md` (`2ce3e5d9368f286204014c308d3890b7a0705f8efeae04f070658d710dc3a9e0`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md` (`0181a3285041d4221e0eb35a682bfc6de39b7f854b99312e157dd1e4c99c5c5b`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md` (`896e981dfc0aebdee7de5907b59cceb9d233c3f7ba443599fd904a4b72e822b8`)
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md` (`89c647f286f3ff648cc7c3fd7dd0646e1f36c5ded4963b9a5a598d7611ba0a59`)
- `.agents/skills/es-utf8-guard/SKILL.md` (`0b513d0eb217a38804eaa6952a7423326212b55a7ba22c125bce960264506dc6`)
- `Documentation/AIKnowledge/aiwarnings-migration-ledger.json` (`8dfd2e970567f3634d274909aeb23255c4bc1a27d8990f4492ae5c11775fc355`)

## EvidenceRefs

- `.agents/skills/es-utf8-guard/scripts/Test-ESUtf8.ps1`
- `git diff --check`

## RequiredReads

- `Documentation/AIKnowledge/entries/aiwarning-p0-utf8-encoding-boundary.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `.agents/skills/es-utf8-guard/SKILL.md`
