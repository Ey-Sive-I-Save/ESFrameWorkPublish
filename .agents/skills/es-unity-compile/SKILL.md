---
name: es-unity-compile
description: Verify ESFramework Unity compilation and build provenance while separating generated .csproj, Unity CLI, Console, domain reload, Test Runner, PlayMode, Profiler, Player, IL2CPP, and release evidence. Use for compile errors, asmdef changes, ReloadDomain checks, Unity command-line compile/test requests, build input fingerprints, build receipts, artifact freshness, or claims that code or a Player is ready in Unity.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Verify ES Unity Compilation

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and capability visibility never grants AI-initiated authority. The current explicit user request authorizes its bounded action under `.agents/skills/es-skill-governance/references/user-directed-action-authority.md`; AIBrain, AICommand and TaskContract are protocol inputs only when their managed channel is selected. Reject inferred expansion, not user-directed paths.
- External process boundary: build identity Capture invokes only the exact `git` executable with an internal allowlist of read-only `status`, `rev-parse HEAD`, and `branch --show-current` arguments; it accepts no caller-supplied Git command.
- Unity CLI is an external-run side effect. Before `Compile`, `EditModeTests` or `PlayModeTests`, require the external execution contract in `references/external-unity-execution-contract.md`; `Status` remains read-only.

## Workflow controls

- Scope and authority are checked before execution; stale or missing evidence blocks the task.
- A current user request directly authorizes bounded source and Assets changes. Unity compilation/reload/runtime, external-process and Git actions must be explicitly named; when a managed Unity channel is selected, its plan and command are protocol inputs rather than secondary approval.
- Record evidence for positive, invalid-input, denied-expansion, repeat-idempotency, and interruption-recovery cases.

Produce evidence at the layer actually tested. Never promote a lower-level result into a Unity or release claim.

## Workflow

1. Read the AIWarnings start files and the rules routed by `规则索引（RuleIndex）.md`.
2. Inspect the worktree and identify affected `.asmdef`, runtime, editor, and test assemblies.
3. Record `ProjectSettings/ProjectVersion.txt` and whether UnityMCP is connected to the intended project instance.
4. Use UnityMCP to read editor state and Console before changing anything.
5. For changed scripts, trigger an explicit Unity import or refresh, wait for compilation/domain reload to finish, then read Console again.
6. Run only the relevant generated-project builds with `scripts/Invoke-ESDotnetBuild.ps1`. Treat these as static compilation evidence only.
   使用 [Unity 证据包验证器](scripts/Test-ESUnityEvidencePacket.ps1) 检查 dotnet-build、Unity CLI、Editor Compile、Test Runner 与运行证据的分层，禁止低层结果升级。
7. Run EditMode or PlayMode tests when the task requires them and the test assembly is available. Report job status and failures exactly.
8. Run Profiler, IL2CPP Player, provider, or release checks only when explicitly required and actually available.

## Build identity and artifact provenance

Use the three scripts as one bounded protocol. They do not start Unity, execute a build, delete output, publish, or prove Runtime behavior.

1. Before a build, run `scripts/New-ESUnityBuildIdentitySnapshot.ps1`. It captures HEAD, the complete dirty-worktree manifest except the declared output roots, Unity/project/package/scene/configuration identity, HybridCLR package identity, and the exact build intent. Write receipts only under `ES/Output/BuildIdentity`; build output must remain under `ES/Output/Builds`.
2. Run the separately authorized Unity build without changing the captured inputs. Keep its BuildReport/log and outputs under the declared build output path.
3. Run `scripts/Complete-ESUnityBuildIdentityReceipt.ps1`. It re-captures the inputs, refuses a caller-supplied `passed` result without a hashed `Unity.exe` and `build-log` or `build-report`, hashes every declared artifact, and exits `2` with `input-drifted` when the before/after fingerprints differ.
4. Before consuming, comparing, or releasing an artifact, run `scripts/Test-ESUnityBuildIdentityReceipt.ps1`. It validates the current contract hash, receipt shape and internal hashes, re-hashes artifacts, and re-captures current inputs by default. Exit `0` means the static identity receipt is current, `1` means invalid/tampered/missing evidence, and `2` means stale or input-drifted.

Receipts are immutable: choose distinct input and finalized receipt paths. A failed write may leave its uniquely named temporary file for diagnosis; no script silently deletes recovery evidence. `-SkipCurrentInputCheck` is only a forensic structural check and cannot establish freshness.

## Unity CLI

Use `scripts/Invoke-ESUnityCli.ps1` as a project launcher for the official Unity `Unity.exe` command-line interface. The PowerShell file is not a replacement Unity CLI; it only resolves the official executable, applies the project safety gate, passes official arguments, and reports the official process result:

```powershell
# Read-only discovery; safe while the project is open.
& .agents/skills/es-unity-compile/scripts/Invoke-ESUnityCli.ps1 -Mode Status -Json

# These modes require the same project to be closed in Unity.
& .agents/skills/es-unity-compile/scripts/Invoke-ESUnityCli.ps1 -Mode Compile -Json
& .agents/skills/es-unity-compile/scripts/Invoke-ESUnityCli.ps1 -Mode EditModeTests -TestFilter 'ES.Tests.ESRingBufferTests' -Json
```

The launcher resolves the exact version from `ProjectSettings/ProjectVersion.txt`, refuses to start batchmode while the project Editor or lock is active, and writes logs/results under `Temp/ESUnityCLI` by default. Test modes require a parseable result XML with at least one executed test. It does not accept arbitrary Unity arguments or `-executeMethod`.

The underlying official commands are the normal Unity arguments, for example `Unity.exe -batchmode -projectPath <project> ...` and `Unity.exe -runTests -testPlatform EditMode -testResults <xml> ...`; the JSON report includes the exact argument array passed to `Unity.exe`.

Standard Unity CLI starts a separate Editor process; it cannot attach to an already-open Editor. Use UnityMCP or the registered `ESAutomationAiBridge` actions for the active Editor instance.

External CLI authorization binds the exact Unity executable hash/version, ProjectRoot, log/results paths, TaskContract, PlanHash, time budget, timeout and stop condition. A zero process exit code never bypasses evidence classification.

## Evidence labels

- `source-present`: the source exists.
- `dotnet-build`: a generated `.csproj` compiled.
- `unity-cli-batchmode`: a guarded Unity CLI process ran; inspect its log or Test Runner XML before promoting the candidate evidence.
- `unity-editor-compile`: Unity imported scripts and Console has no compile errors.
- `unity-test-runner`: named EditMode or PlayMode tests completed.
- `runtime-observation`: behavior was observed in PlayMode.
- `profiler`: measurements came from Profiler evidence.
- `player-build`: a real Player build completed.
- `release-validation`: external provider or publishing workflow actually ran.

Never merge these labels.

## Failure handling

- Distinguish task-related failures from unrelated existing failures with exact file and line evidence.
- Do not edit generated `.csproj` files to make Unity appear to include a source file.
- Do not clear Console before capturing the existing baseline unless the user asks.
- If UnityMCP is unavailable, report that Unity evidence is blocked and provide only the lower evidence layers actually obtained.

## Script

`scripts/Invoke-ESDotnetBuild.ps1` builds explicit project files and emits a structured summary. It never claims Unity compilation.

`scripts/Invoke-ESUnityCli.ps1` reports Unity CLI status or starts guarded batchmode compile/EditMode/PlayMode tests. Status is read-only; execution writes only caller-selected logs/results, defaulting to `Temp/ESUnityCLI`.

`scripts/New-ESUnityBuildIdentitySnapshot.ps1`, `scripts/Complete-ESUnityBuildIdentityReceipt.ps1`, and `scripts/Test-ESUnityBuildIdentityReceipt.ps1` implement the build identity protocol defined by `ES/Automation/Contracts/es-unity-build-identity-receipt-v1.schema.json`. Their S1 identity evidence never upgrades a build status into Unity, Player, IL2CPP, or release acceptance.

## 受管 AI Bridge 编译控制

当用户要求 AI 打开/关闭自动 Unity 编译或触发编译时，使用项目 `ESAutomationAiBridge` 的固定 Editor 主线程动作，不执行任意命令：

- `getUnityCompilationState`
- `setUnityAutoCompilation`，payload 为 `{ "enabled": true|false }`
- `triggerUnityCompilation`，payload 为 `{ "forceRefresh": true|false }`

自动编译开关是本次 Editor 会话的受管策略，不等于修改 Unity 全局偏好；触发请求只证明已提交编译请求，必须再次读取 Console、`EditorApplication.isCompiling` 和 ReloadDomain 结果才能报告 `unity-editor-compile`。

场景修改/保存不属于普通编译动作。它必须走 `es-editor-tooling` 规定的 `modifyActiveScene` 白名单入口，使用 `dryRun`、Undo、Dirty 标记和显式 `save`；不得通过 Worker、任意脚本或文件写入修改 `.unity` YAML。


## Specialized static acceptance

Acceptance ID: `unity-compile-static`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- asmdef
- compile log
- zero errors
- stale receipt
- project identity
- build input fingerprint
- artifact re-hash
- input drift

Required specialized cases: `project-identity, asmdef-closure, compile-log-classification, error-zero-contract, stale-receipt, build-input-fingerprint, artifact-rehash, input-drift`
Guidance: `references/static-specialized-acceptance.md`

External execution contract: `references/external-unity-execution-contract.md`
