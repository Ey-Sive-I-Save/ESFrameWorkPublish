# Feishu identity and messaging contract

Authority: target contract bound to the registered local identity and message TaskContracts.
Scope: guided diagnostics, personal role ownership, bot ownership metadata, role resolution and one-recipient plain-text messages.
StaleWhen: AICommand, TaskContract, Schema, local store, Worker, Feishu IM API/SDK or identity policy changes.
Evidence: current source plus fresh Static and Runtime receipts; source and DryRun do not prove remote authentication or delivery.

## Fixed bindings

| Command | TaskContract | Operations | Side effect |
|---|---|---|---|
| `feishu.identity.manage` | `es.feishu.identity.claim@1` | `setup-status`, `list-claims`, `claim-role`, `release-role` | Machine-local state only |
| `feishu.message.send` | `es.feishu.message.send@1` | `send-text` | One remote message |

Long-lived identity state is stored only under the Git-ignored `ES/Automation/Runs/FeishuIdentity/` root. A resolved member or recipient may appear only in the fixed Worker's private request envelope under the separately Git-ignored `ES/Automation/Runs/FeishuTasks/` root. Raw IDs must be supplied through the bounded local claim input and never repeated in normal results, reports, Knowledge or AI-visible summaries; results expose stable binding hashes and booleans. Neither root may contain an App Secret, token, cookie or authorization header.

## Ownership and consent

A claim is partitioned by the current `ES_FEISHU_APP_ID` hash and binds:

```text
roleId + Windows local principal hash + AIBrain actor hash + app identity hash
```

Only the owner may replace or release it. Role names do not create authority. Other local actors may resolve the role only for permissions the owner explicitly enabled:

- `allowTaskAssignment=true` permits task-member resolution.
- `allowDirectMessage=true` permits one-recipient message resolution.
- `claimBotOwnership=true` claims a unique bot alias for local governance only.

Bot ownership does not create an application, enable bot capability, grant tenant scopes or prove authentication. Windows shared accounts cannot provide person-level isolation; commercial deployment must use separate OS identities or a future enterprise identity broker.

## Guided setup

`setup-status` is local and never calls Feishu. It returns only whether managed Node, AppId and App Secret are configured, the app hash when available, owned/team role counts and stable next-action codes. Recommended order:

```text
configure managed Node -> configure AppId/Secret in Unity's managed environment
-> DryRun/claim personal role -> separately authorize auth-status
-> DryRun task assignment or message -> separately authorize Live
```

Opening the Feishu desktop client is unrelated to Open Platform credentials. Credential values are never requested through task input or chat.

## Role-based assignment

Task dispatch/transition may accept `claimedRoles` entries containing only `roleId` and the Feishu task role. C# resolves each role under the current app hash before creating the Worker request. `members` and `claimedRoles` cannot be mixed. A changed or released claim changes the normalized input and invalidates the previous DryRun.

## Message contract

`send-text` accepts exactly one `roleId` and 1–1000 characters of plain text. C# resolves the recipient; arbitrary recipient IDs are not part of the public schema. The Worker fixes `msg_type=text`, JSON-encodes the text, and supplies a deterministic `uuid` to `im.v1.message.create`.

No broadcast, webhook, rich text, card, attachment, mention injection, edit, recall, chat creation or delivery/read-status claim is allowed. Transient retries remain bounded by the same UUID. If no terminal response is obtained, return `UNCERTAIN_REMOTE_RESULT` and stop; do not generate a fresh invocation to guess whether resending is safe.

## Workflow composition

Automatic assistance is orchestration, not a distributed transaction:

```text
assignment child run -> independent notification child run -> reconciliation summary
```

If assignment succeeds and notification fails, report `assignment=completed`, `notification=failed/uncertain`, retain both RunIds and do not roll back or pretend full success. The inverse is blocked by planning order: notification should not run until the target task identity exists.
