---
name: es-unity-compile
description: Verify ESFramework Unity compilation and separate evidence from generated .csproj builds, Console state, domain reload, Unity Test Runner, PlayMode, Profiler, IL2CPP, and release validation. Use for Unity compile errors, asmdef changes, ReloadDomain checks, Console inspection, or claims that code is ready in Unity.
---

# Verify ES Unity Compilation

Produce evidence at the layer actually tested. Never promote a lower-level result into a Unity or release claim.

## Workflow

1. Read the AIWarnings start files and the rules routed by `规则索引（RuleIndex）.md`.
2. Inspect the worktree and identify affected `.asmdef`, runtime, editor, and test assemblies.
3. Record `ProjectSettings/ProjectVersion.txt` and whether UnityMCP is connected to the intended project instance.
4. Use UnityMCP to read editor state and Console before changing anything.
5. For changed scripts, trigger an explicit Unity import or refresh, wait for compilation/domain reload to finish, then read Console again.
6. Run only the relevant generated-project builds with `scripts/Invoke-ESDotnetBuild.ps1`. Treat these as static compilation evidence only.
7. Run EditMode or PlayMode tests when the task requires them and the test assembly is available. Report job status and failures exactly.
8. Run Profiler, IL2CPP Player, provider, or release checks only when explicitly required and actually available.

## Evidence labels

- `source-present`: the source exists.
- `dotnet-build`: a generated `.csproj` compiled.
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

## 受管 AI Bridge 编译控制

当用户要求 AI 打开/关闭自动 Unity 编译或触发编译时，使用项目 `ESAutomationAiBridge` 的固定 Editor 主线程动作，不执行任意命令：

- `getUnityCompilationState`
- `setUnityAutoCompilation`，payload 为 `{ "enabled": true|false }`
- `triggerUnityCompilation`，payload 为 `{ "forceRefresh": true|false }`

自动编译开关是本次 Editor 会话的受管策略，不等于修改 Unity 全局偏好；触发请求只证明已提交编译请求，必须再次读取 Console、`EditorApplication.isCompiling` 和 ReloadDomain 结果才能报告 `unity-editor-compile`。

场景修改/保存不属于普通编译动作。它必须走 `es-editor-tooling` 规定的 `modifyActiveScene` 白名单入口，使用 `dryRun`、Undo、Dirty 标记和显式 `save`；不得通过 Worker、任意脚本或文件写入修改 `.unity` YAML。
