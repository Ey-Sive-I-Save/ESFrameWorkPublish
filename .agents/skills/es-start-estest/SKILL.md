---
name: es-start-estest
description: Start, monitor, or safely cancel the ESFramework ESAITest/ESTEST runtime through the existing Unity Editor menu, Player command line, or public bootstrap API. Use when the user says “启动 ESTEST”, “运行 ESAITest”, “直接开始 AI 测试”, “中断 ESTEST”, asks AI to launch the built-in ESTEST baseline, or supplies an ESAITest plan to execute.
---

# Start ES ESTEST

Use the existing ESAITest Runner. Do not create another runner, input source, report writer, or editor initializer.

## Workflow

1. Read `Assets/Plugins/ES/AICommands/ESAITest_直接启动ESTEST_AI命令.md` as the authorization contract.
2. Confirm the repository root, branch, HEAD, worktree, Unity version, and intended Unity project instance.
3. Choose one execution surface:
   - Unity Editor: invoke `【ES】/自动化/ESAITest/直接启动 ESTEST` through UnityMCP.
   - Player/CI: launch the explicitly supplied Player executable with `-esTest`; add `-esAITestQuit` only when requested.
   - Runtime C#: call `ESAITestPlayerBootstrap.TryStartESTEST(...)` when the host already provides an authorized code-execution bridge.
4. Do not start a second Run when `ESAITestPlayerBootstrap.ActiveRunner` is non-null. Report the existing Run as busy.
5. Observe the Runtime Dashboard and collect the Run report from `Application.persistentDataPath/ESAITest/<runId>/`.
6. For cancellation, invoke `【ES】/自动化/ESAITest/中断当前 ESTEST` or call `ESAITestPlayerBootstrap.RequestCancel()` through the same authorized bridge.
7. Report the exact execution surface, RunId, status, report paths, and evidence level.

## Boundaries

- Starting ESTEST does not authorize source edits, scene edits, builds, release, uploads, Git writes, or audit-state writes.
- Never launch an arbitrary executable or infer a Player path. Require an explicit path or a verified current Unity instance.
- Never claim startup succeeded from source presence or compilation alone. Require Runtime Dashboard, Console, process exit, or report evidence.
- Preserve the Owner/Token/Generation input lease and single-Run race protection.
- If UnityMCP is unavailable and no Player path or authorized runtime bridge exists, report the startup blocker and provide the exact menu or command line; do not fake execution.

## Delivery

Report whether ESTEST actually started, whether it completed or was cancelled, the RunId and report directory, and any missing PlayMode, Player, Profiler, or IL2CPP evidence.
