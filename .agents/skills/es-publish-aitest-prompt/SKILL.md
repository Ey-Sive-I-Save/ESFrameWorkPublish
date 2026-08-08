---
name: es-publish-aitest-prompt
description: Quickly publish a one-time prioritized message to the running ESFramework ESAITest AI through the external prompt inbox. Use when the user says “你快告诉测试AI……”, “告诉测试 AI……”, “给测试AI发消息/提示”, asks to immediately notify the testing AI, or invokes $es-publish-aitest-prompt. Do not trigger merely to explain Publish or edit its implementation.
---

# Publish an ESAITest AI prompt

Extract the message after the trigger phrase and send it through `scripts/Send-ESAITestPrompt.ps1`.

## Workflow

1. Require a non-empty message.
2. Use an explicit P0–P4 from the user when present.
3. Otherwise use P1 for words such as “快”, “立即”, “马上” or “紧急”; use P2 for an ordinary notification.
4. Run from the project root:

   ```powershell
   & '.agents\skills\es-publish-aitest-prompt\scripts\Send-ESAITestPrompt.ps1' `
     -Message '<message>' -Priority P1 -Source 'codex-chat' -WaitForPickupSeconds 2
   ```

5. Report the returned `promptId`, priority and status.

## Status meaning

- `picked_up`: the running ESAITest runtime moved the envelope into its prompt queue. This does not prove the AI has consumed it; `attention.snapshot` supplies consumption evidence.
- `queued`: the envelope was written atomically, but no active runtime picked it up during the short wait. It remains available until its TTL expires.

Do not modify a plan, create another Runner, write Git/history/audit state, or claim delivery beyond the returned evidence.
