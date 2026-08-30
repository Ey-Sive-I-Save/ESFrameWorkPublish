`KnowledgeId`: `es.codex.app-server.integration.v1`
`Authority`: `Derived from current ES Codex App Server Worker, TaskContract, AICommand, AIBrain binding, AIWarnings and RunRecord contract`
`RouteKeys`: `automation, worker, external-agent, codex, app-server, harness, authority, evidence, editor, session, thread, turn`
`ContentHash`: `9c200d9f4835458943b8a2ebf0f2113305d947b3e2cd8a03d0c68eaea070cbda`
`EvidenceLevel`: `S1`
`StaleWhen`: `Codex App Server protocol/version, ES Worker identity, TaskContract, AICommand, AIBrain binding, authority consumer registry, AIWarnings Automation/Editor rules, session Skill contract or any SourceRef hash changes`

`ProviderDeclaration`: `es-codex`
`RuntimeStatus`: `runtime-not-run`
`ExternalCalibration`: `Deferred`; official Codex documentation was consulted for current analysis but no external page is a durable project SourceRef. Version-sensitive behavior must be rechecked against the installed CLI and an authorized project-local snapshot before promotion.

## SourceRefs

- `ES/Automation/Contracts/es-codex-app-server-integration-declaration-v1.json` (`33b31a2b8253eca5c097825d5cbc205a43771d25129e8a93729992174ec47387`)
- `ES/Automation/Contracts/es-codex-app-server-v1.schema.json` (`466dd186db60bf3f1271f7e76c7b786b9b3f421edc2da1320f00c962427d2315`)
- `ES/Automation/Workers/PowerShell/Invoke-ESCodexAppServerWorker.ps1` (`cfb2d21f0dc523c6c52fed941daa7b2fdb0922ed0559ab7e4c219b2820ca837f`)
- `ES/Automation/Workers/PowerShell/Test-ESCodexAppServerContract.ps1` (`c204ba3ba9569fa24b3c69e6e582951e531040357fcb0b9b74961b40968d1e72`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESCodexAppServerAutomation.cs` (`b5b63acfb29858e57b8b635aab19b11db3d89e5a12c762e59785dffd654ff3f6`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESCodexAppServerAutomation.cs.meta` (`5960bff0750a6204e74588ebda843c0c864801299d654c74ff595f22f5ff09b6`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationCenter.cs` (`662d4407ec83db1f47487fdb5e893a01011ad0e7e88d6514709dec5c425d03fa`)
- `Assets/Plugins/ES/AICommands/CodexAppServerHarness受管开发_AI命令.md` (`803aeb307a40c0025691e11de9adc71b8bddec7df4bc5fe302db442a55cc9b92`)
- `Assets/Plugins/ES/AICommands/CodexAppServerHarness受管开发_AI命令.md.meta` (`b1335b5792444e2fc40e9dd160092cf2ce4232792c5e442794f7819e78d6393b`)
- `Assets/Plugins/ES/AICommands/AICommandCatalog.json` (`c9f6e741ceeb006cf8389c21cac2ba022421a9f095332d226f6a704d24b5f46e`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAutomationAiBridge.cs` (`5d1167739703d02da69a0ba4edc1a8a750f45220dc8acfc04ebfe0238336f536`)
- `Assets/Plugins/ES/Editor/ESAutomation/ESAIBrainCoordinator.cs` (`16fb6736c1195ace0fb8e396387f47e72429183a20902063d315c3e1fefb9ce8`)
- `ES/Automation/Contracts/es-authority-consumer-registry-v1.json` (`e27ca590efc178d5da83d6942bc6d8523490df74f7e16990a5a3422167890825`)
- `ES/Automation/Contracts/es-external-agent-adapter-registry-v1.json` (`62863e0be97f7452d4cd5f5937e237a81b9600c23b098dbde1fcee3455ce7040`)
- `ES/Automation/Contracts/es-external-agent-capability-matrix-v1.json` (`096e2469e43ceb58d2333b1980e90841feeabe1942e8805cee8067b6b4648da3`)
- `ES/Automation/Contracts/es-external-agent-feature-catalog-v1.json` (`69a4cd39f7595c10a41ee85a564dd5c430cedf9ad6bf0ae0e7eaf6174d55ac63`)
- `ES/Automation/Contracts/es-external-agent-supply-chain-v1.json` (`6f497a82247eb564297d07df5065f0a18b9eebf420f6de2d4df5b5d0a9cfca14`)
- `Assets/Plugins/ES/AIWarnings/20_架构现状（Architecture）/跨系统核心语义（CoreSemantics）/ESAutomationCenter与受管Worker治理_AI协作警告.md` (`6f7998bac62c988384030ea434dc1166d0b5fa11c05f880baf6705321ea27485`)
- `.agents/skills/es-codex-session-bootstrap/SKILL.md` (`280a835617e8216f1a0192f3f46aeecb6c9426cf74dd62fafed62910704a189`)
- `.agents/skills/es-codex-session-bootstrap/SKILL.md` (`280a835617e8216f1a0192f3f46aeecb6c9426cf74dd62fafed62910704a189`)

## RequiredReads

- `AGENTS.md`
- `ES/AISpace/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/README.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/当前状态（CurrentStatus）.md`
- `Assets/Plugins/ES/AIWarnings/00_开始阅读（Start）/规则索引（RuleIndex）.md`
- `Documentation/AIKnowledge/AIBRAIN_ENTRY.md`
- `Documentation/AIKnowledge/KnowledgeIndex.yaml`
- `.agents/skills/es-codex-session-bootstrap/SKILL.md`
- `ES/Automation/Contracts/es-codex-app-server-integration-declaration-v1.json`
- `ES/Automation/Contracts/es-codex-app-server-v1.schema.json`
- `ES/Automation/Workers/PowerShell/Test-ESCodexAppServerContract.ps1`
- `Assets/Plugins/ES/AICommands/CodexAppServerHarness受管开发_AI命令.md`
- `Assets/Plugins/ES/Editor/ESAutomation/ESCodexAppServerAutomation.cs`

## Verified project facts

- ES registers `es.codex.app-server@1` as a PowerShell Worker and exposes it through the single `codex.appserver.execute` AICommand. AIBrain binds that task to the same command and requires the L3, `external-run`, `editor-tooling` profile.
- The Facade declares the project bootstrap/authority reads (`AGENTS.md`, `ES/AISpace/README.md`, AIWarnings Start/CurrentStatus/RuleIndex, AIBRAIN_ENTRY, KnowledgeIndex and this entry) before a managed invocation; the declaration repeats the same normalized required-read set.
- The real Run entry rechecks the current Worker/Schema hashes and refuses external execution when any declared ES authority read is missing or crosses a reparse point; managed Run, result, and RunRecord paths are rechecked for reparse points before use.
- The input contract allows only `operation`, `prompt`, exact `threadId`, and an optional character-whitelisted `model`. The caller cannot select an executable, working directory, sandbox, permission policy, output path, script, URL, or arbitrary command line.
- The Worker reads the request only from `ES/Automation/Runs/CodexAppServer`, writes its result only below that root, and pins the project root, task identity, provider declaration, Worker version, and input SHA-256.
- Every real execution request and result also binds the AIBrain `brainPlanHash`, fixed AICommand id/hash, TaskContract stable hash, exact `invocationId`, and optional idempotency key; mismatches are rejected before candidate output is exposed.
- The fixed launcher is `codex.cmd app-server --stdio`; the launcher path must be absolute, named `codex.cmd`, non-reparse, and free of shell expansion characters. The Worker performs `initialize`/`initialized`, then `thread/start` or exact `thread/resume`, and then `turn/start`.
- `thread/start` uses the App Server protocol's legacy `sandbox: "read-only"` mode, while `turn/start` applies the structured `sandboxPolicy` with a restricted readable project root; both use `approvalPolicy=never`. Approval, permission, user-input, and elicitation requests fail closed and produce a blocked result; they are never auto-accepted.
- The Worker drains and bounds Codex stderr, caps streamed events at 200 items and 800,000 event bytes, bounds final/error text, redacts known provider secrets, and creates the result file without overwrite.
- DryRun and `check-local` results keep `networkCalled=false`; `start-thread`/`turn` set it only when the first provider-facing thread request is sent. This records an external call attempt, not provider success or ES acceptance.
- ES `ProcessRunner` owns process creation, stdout/stderr draining, timeout, process-tree termination, Editor lifecycle handling, RunRecord updates, result identity checks, and output hashes. The adapter does not call `Process.Start` directly.
- RunRecord keeps the Worker PID in `processId` and the provider launcher PID observed by the Worker in `codexProcessId`; these identities are not collapsed. The field is not a claim about a deeper descendant PID.
- Codex output is recorded as candidate evidence. The adapter exposes `candidate-only-not-final-acceptance`; ES `CompletionDecision` and business acceptance remain outside the Worker. DeepSeek remains a distinct `es-deepseek` provider and task identity.
- The Codex Editor adapter and AICommand both carry Unity `.meta` identities, so their asset identities remain stable across imports.
- ES registers `es.codex.app-server.receipt` as a side-effect-free verifier for a fresh, runtime-scoped, SHA-256-bound receipt; this verifier never creates or infers a `CompletionDecision`.

## Allowed route and execution boundary

Use this entry for requests involving Codex App Server threads, turns, streamed candidate messages, exact thread resume, external-agent adapter contracts, or evidence/authority boundaries. Start with `dry-run` or `check-local`; only a user-authorized AIBrain plan may select `start-thread` or `turn`. Re-plan when the command body, plan, TaskContract, Worker/Schema hash, input hash, or exact thread identity changes.

The managed write surface is limited to the current RunId's request, RunRecord, result, and temporary evidence under `ES/Automation/Runs/CodexAppServer`. It is not a source-asset materializer. Candidate code or content must return to an ES-owned proposal, review, and domain-specific acceptance path before any separate authorized write.

## Failure-surface matrix

| failureId | severity | erroneousBehavior | triggerAndSymptom | preventionCheck | correctAction | recoveryAction | evidencePresent | evidenceMissing | sourceRefs |
|---|---|---|---|---|---|---|---|---|---|
| codex-authority-inversion | identity/authority | Treat Codex `Passed`, a completed turn, or text as ES business acceptance | External result appears successful while no ES CompletionDecision exists | Candidate-only marker, authority consumer coverage, no asset/publish capability | Keep status at orchestration/candidate layer and require ES evidence | Re-plan through the domain owner and re-evaluate CompletionDecision | Static governance and source assertions | Unity/domain runtime acceptance | adapter, AICommand, AIWarnings |
| codex-permission-expansion | irreversible | Auto-approve a file, command, MCP, or user-input request | App Server emits an approval/permission/elicitation request | Fixed approval policy and fail-closed method matching | Stop the Worker and write `Blocked` with the request count | Review the requested capability as a new ES contract; do not resume implicitly | Worker source and static replay | Live approval roundtrip | Worker, TaskContract, AICommand |
| codex-path-or-shell-expansion | irreversible | Read or write outside the declared project/run roots or execute a caller-selected binary | Malformed input contains executable/cwd/sandbox/output path, or launcher path expands in cmd | Exact input allowlist, root containment, non-reparse checks, fixed `codex.cmd` and shell-character rejection | Reject before process start | Correct the contract/input and create a new RunId | Worker parser, hash and contract validators | Windows/Unity runtime filesystem behavior | schema, Worker, ProcessRunner |
| codex-thread-identity-drift | identity/authority | Resume a different conversation or silently guess a recent thread | `threadId`, session, turn, or input hash does not match RunRecord | Exact threadId requirement and result identity/hash checks | Return stale/rejected and require re-planning | Bind a new explicit thread under a new RunId | Source assertions and RunRecord contract | Remote server state after restart | adapter, declaration, session Skill |
| codex-process-interruption | lifecycle/partial | Leave a process or RunRecord falsely running after timeout, cancel, reload, or Editor quit | Worker disappears or exceeds the bounded timeout | ProcessRunner process-tree ownership, cancellation, lifecycle hooks, conservative recovery | Terminate and mark `Failed`/`Cancelled` only when observed | Re-plan; never infer remote completion or retry automatically | Adapter lifecycle code and Worker packet | Live interruption timing | adapter, Worker, RunRecord |
| codex-result-tampering-or-loss | identity/authority | Consume an unbound, missing, oversized, or malformed result | Result file absent, invalid JSON, mismatched task/provider/hash, or output identity drift | Required result identity fields, bounded events/final text, output SHA-256 | Reject and record failure; do not promote text | Inspect the existing RunId, then create a fresh run after source revalidation | Result checks and UTF-8/static validators | Adversarial runtime tamper test | schema, Worker, adapter |

## Completion and non-claims

Static contract, authority, path, encoding, and registry checks are current for the listed SourceRefs. `runtime-not-run` remains the correct label until an explicitly authorized Codex App Server process, installed CLI version, Unity import/compile, and domain acceptance test produce fresh receipts. This entry does not prove Provider credentials, network success, Unity compilation, PlayMode, Player, IL2CPP, performance, release, or business acceptance.

Codex App Server is an external execution plane, not an ES authority source. Any future external documentation snapshot must be project-local, bounded, versioned, hash-bound, and separately authorized; a live URL or successful probe cannot replace current ES source or Runtime evidence.
