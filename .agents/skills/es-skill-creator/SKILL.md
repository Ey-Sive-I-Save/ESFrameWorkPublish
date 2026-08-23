---
name: es-skill-creator
description: Create, validate, and forward-test ESFramework project Skills with clear triggers, references, deterministic scripts, permission boundaries, UTF-8 safety, and scalable maintenance. Use when creating, upgrading, reviewing, classifying, or retiring a Skill under F:/aaProject/ESFrameWorkPublish/.agents/skills.
---

## Verification boundary

- **Static**: source, configuration, contracts, hashes, and deterministic scripts.
- **Runtime**: Unity, process, display, timing, layout-engine, or serialization behavior.
- `runtime-not-run` means runtime evidence is absent; it does not mean Static failed. It blocks only the selected RuntimeAcceptance/ReleaseAcceptance profile.
- Details: `.agents/skills/es-skill-governance/references/verification-semantics.md`

# Skill Creator

## StaticDeepReplay-first creation standard

Creation and upgrade must first be reproducible without Runtime: replay source/configuration contracts, deterministic scripts, boundary rules, negative inputs, idempotency, interruption recovery, and cache/manifest behavior. Runtime execution is opt-in only after explicit developer approval, an AIBrain plan, the matching AICommand/TaskContract, and a bounded evidence budget. A Skill must declare `staticWeight >= 0.5`, `staticDeepReplayRequired: true`, and `runtimeAuthorizationRequired: true` in `governance.json`.

Every formal Skill must also ship three discoverable StaticDeepReplay artifacts: `static-replay.manifest.json`, `references/static-replay-adapter.md`, and a `scripts/*-StaticReplay.ps1` runner delegating to `es-static-deep-replay`. The manifest fixes the seven replay cases and separates `staticClaims` from `runtimeClaimsNotProven`; absence of any artifact is a creation failure, not a reason to invent an ad-hoc validator.

## Resource composition

- Load the [Skill Resource Index](../../SKILL_RESOURCE_INDEX.yaml) before selecting references, scripts, MCP capabilities, or evidence.
- Load the [Skill Catalog](../../SKILL_CATALOG.yaml) to select the family, route keys, lifecycle state, owner and current hash baseline. A new or changed Skill is not accepted until its single catalog record is refreshed.
- Read [the evidence receipt contract](references/evidence-receipt-contract.md) and run [the evidence validator](scripts/Test-ESSkillEvidence.ps1) against every execution receipt.
- MCP is optional and deny-by-default; capability visibility never grants permission. Use AIBrain `planTask`, the matching AICommand, and the current TaskContract before any write or external operation.

This skill provides guidance for creating effective skills.

## ESFramework Project Authority

This project copy is the authority for Skills under `F:/aaProject/ESFrameWorkPublish/.agents/skills`.

- New project Skills must be direct children of `.agents/skills`, use lowercase `es-` names, and contain only the official Skill structure plus necessary resources.
- Before creating or changing a Skill, read `.agents/README.md`, the relevant AIWarnings routes, and the current target Skill; do not copy AIWarnings, AICommands, session history, Unity assemblies, or generated output into a Skill.
- Use the bundled `scripts/init_skill.py`, `scripts/generate_openai_yaml.py`, and `scripts/quick_validate.py` from this project copy. Pass an explicit project output path; do not silently write to global Skill directories.
- After creating or changing a Skill, register it with `python scripts/Build-ESSkillCatalog.py --project-root F:/aaProject/ESFrameWorkPublish --write`, then run `scripts/Test-ESSkillCatalog.ps1`. Registration is an explicit project write and must be included in the change budget.
- Before acceptance, invoke `$es-skill-validator` (read-only) for Structural, Governance, Catalog and Security profiles; behavioral cases require authoritative receipts and cannot be inferred from a green frontmatter check.
- Before acceptance, invoke `$es-skill-validator -Profile StaticDeepReplay` and run the Skill-local `*-StaticReplay.ps1`; this is the default static fast path and must pass before any Runtime request is considered.
- All bundled Python tools must read and write UTF-8 explicitly. Run the project UTF-8 guard and the Skill's own tests after changes.
- A Skill is not accepted because frontmatter validates: record script behavior, positive and negative examples, write scope, evidence level, and forward-test results before calling it production-ready.

## Skill 使用披露合同

每个新建或升级的项目 Skill 必须在 `SKILL.md` 中保留“Skill 使用披露”段，并引用项目根 `AGENTS.md` 与 `.agents/README.md` 的同名规范。使用该 Skill 的 AI 必须：

1. 在首次用户可见的进度更新中说明实际使用了该 Skill，以及它与当前任务的关系；
2. 在最终答复列出本轮实际影响工作的 Skill 与作用；
3. 不列出仅可用、仅读取元数据或未执行的 Skill；
4. 不把披露本身表述为授权、脚本执行、外部调用或验收证据。

这是一项可观察性合同。它不改变 Skill 的权限、证据等级或 AICommand/TaskContract 门禁。

## Governance contract

Use `$es-skill-governance` as the governing contract for classification and acceptance. Read these project-authoritative references before creating, upgrading or retiring a Skill:

```text
.agents/skills/es-skill-governance/SKILL.md
.agents/skills/es-skill-governance/references/tier-matrix.md
.agents/skills/es-skill-governance/references/evidence-and-acceptance.md
.agents/skills/es-skill-governance/references/scale-patterns.md
.agents/skills/es-skill-governance/scripts/Test-ESSkillContract.ps1
```

Every formal Project Skill must include a `governance.json` beside `SKILL.md` using schema version 1. The file records `tier`, `maturity`, `delivery`, `evidenceLevel`, `riskClass`, `executionMode`, `requiresBrainPlan`, `allowDirectExecution`, and `writePolicy`; Creator must create or update it together with the Skill and run `Test-ESSkillContract.ps1 -RequireGovernanceMetadata`. AIBrain reads this metadata through `KnowledgeIndex` before planning, so missing, stale, malformed, or permission-expanding metadata is a hard validation failure.

Keep these axes independent:

- Tier: `SmallTool`, `Workflow`, or `Engineering`.
- Maturity: `Proposed` through `Archived`.
- Delivery: `Designed`, `Implemented-Unverified`, `Blocked`, `Failed`, `Accepted`, or `Released`.

Tier never grants permission. AIWarnings remain the long-lived constraint authority, AICommand remains the per-task authorization contract, and this Creator only scaffolds or validates Skill artifacts within the explicitly authorized project path. A Skill must fail closed for missing prerequisites, denied expansion, malformed input, interrupted execution and unsafe reruns. Frontmatter or `quick_validate.py` alone is at most structural evidence; use the relevant S0-S6 evidence level and do not claim `Stable` without representative positive, invalid-input, denial, repeat/idempotency and recovery evidence.

## Specialized static acceptance

- Guidance: `references/static-specialized-acceptance.md`
- Acceptance ID: `creator-pipeline`
- Required cases: `scaffold-contract, invalid-name-rejection, resource-composition, registration-idempotency, catalog-refresh`
- Static assertions: explicit output path; registration is idempotent; quick validation; catalog hash refresh; UTF-8
- This contract is responsibility-specific and remains distinct from Runtime proof.

## Responsibility-specific static acceptance

- Profile: `governance`
- Custom checks: `authority-routing, permission-boundary, deterministic-replay, evidence-contract`
- These checks are responsibility-specific static proof; they do not claim Runtime behavior.

## Engineering controls

- **Owners**: ESFramework Skill maintainers own templates and scripts; a designated maintainer accepts formal Skill imports.
- **Permission matrix**: inspect and generate candidates by default; formal `.agents/skills` writes require explicit authorization; source, Git, Unity, release, network and external AI permissions remain separate.
- **Change budget**: declare target Skill, allowed files, resource folders, maximum generated files, timeout, retry limit and stop condition before generation.
- **Risk register**: prevent trigger collision and permission expansion during design; detect them with governance and forward tests; isolate candidates outside formal discovery; recover by rejecting import and preserving the prior Skill.
- **Scale and compatibility**: create or upgrade one bounded Skill per run unless a batch count is explicit. Schema, frontmatter, generator or authority changes require validator and Knowledge hash refresh.
- **Acceptance replay**: run `quick_validate.py`, `Test-ESSkillContract.ps1 -RequireGovernanceMetadata`, strict UTF-8, positive, invalid-input, denied-expansion, repeat/idempotency and interruption/recovery cases before acceptance.

## About Skills

Skills are modular, self-contained folders that extend Codex's capabilities by providing
specialized knowledge, workflows, and tools. Think of them as "onboarding guides" for specific
domains or tasks—they transform Codex from a general-purpose agent into a specialized agent
equipped with procedural knowledge that no model can fully possess.

### What Skills Provide

1. Specialized workflows - Multi-step procedures for specific domains
2. Tool integrations - Instructions for working with specific file formats or APIs
3. Domain expertise - Company-specific knowledge, schemas, business logic
4. Bundled resources - Scripts, references, and assets for complex and repetitive tasks

## Core Principles

### Concise is Key

The context window is a public good. Skills share the context window with everything else Codex needs: system prompt, conversation history, other Skills' metadata, and the actual user request.

**Default assumption: Codex is already very smart.** Only add context Codex doesn't already have. Challenge each piece of information: "Does Codex really need this explanation?" and "Does this paragraph justify its token cost?"

Prefer concise examples over verbose explanations.

### Set Appropriate Degrees of Freedom

Match the level of specificity to the task's fragility and variability:

**High freedom (text-based instructions)**: Use when multiple approaches are valid, decisions depend on context, or heuristics guide the approach.

**Medium freedom (pseudocode or scripts with parameters)**: Use when a preferred pattern exists, some variation is acceptable, or configuration affects behavior.

**Low freedom (specific scripts, few parameters)**: Use when operations are fragile and error-prone, consistency is critical, or a specific sequence must be followed.

Think of Codex as exploring a path: a narrow bridge with cliffs needs specific guardrails (low freedom), while an open field allows many routes (high freedom).

### Protect Validation Integrity

You may use subagents during iteration to validate whether a skill works on realistic tasks or whether a suspected problem is real. This is most useful when you want an independent pass on the skill's behavior, outputs, or failure modes after a revision.  Only do this when it is possible to start new subagents.

When using subagents for validation, treat that as an evaluation surface. The goal is to learn whether the skill generalizes, not whether another agent can reconstruct the answer from leaked context.

Prefer raw artifacts such as example prompts, outputs, diffs, logs, or traces. Give the minimum task-local context needed to perform the validation. Avoid passing the intended answer, suspected bug, intended fix, or your prior conclusions unless the validation explicitly requires them.

### Anatomy of a Skill

Every skill consists of a required SKILL.md file and optional bundled resources:

```
skill-name/
├── SKILL.md (required)
│   ├── YAML frontmatter metadata (required)
│   │   ├── name: (required)
│   │   └── description: (required)
│   └── Markdown instructions (required)
├── agents/ (recommended)
│   └── openai.yaml - UI metadata for skill lists and chips
└── Bundled Resources (optional)
    ├── scripts/          - Executable code (Python/Bash/etc.)
    ├── references/       - Documentation intended to be loaded into context as needed
    └── assets/           - Files used in output (templates, icons, fonts, etc.)
```

#### SKILL.md (required)

Every SKILL.md consists of:

- **Frontmatter** (YAML): Contains `name` and `description` fields. These are the only fields that Codex reads to determine when the skill gets used, thus it is very important to be clear and comprehensive in describing what the skill is, and when it should be used.
- **Body** (Markdown): Instructions and guidance for using the skill. Only loaded AFTER the skill triggers (if at all).

#### Agents metadata (recommended)

- UI-facing metadata for skill lists and chips
- Read references/openai_yaml.md before generating values and follow its descriptions and constraints
- Create: human-facing `display_name`, `short_description`, and `default_prompt` by reading the skill
- Generate deterministically by passing the values as `--interface key=value` to `scripts/generate_openai_yaml.py` or `scripts/init_skill.py`
- On updates: validate `agents/openai.yaml` still matches SKILL.md; regenerate if stale
- Only include other optional interface fields (icons, brand color) if explicitly provided
- See references/openai_yaml.md for field definitions and examples

#### Bundled Resources (optional)

##### Scripts (`scripts/`)

Executable code (Python/Bash/etc.) for tasks that require deterministic reliability or are repeatedly rewritten.

- **When to include**: When the same code is being rewritten repeatedly or deterministic reliability is needed
- **Example**: `scripts/rotate_pdf.py` for PDF rotation tasks
- **Benefits**: Token efficient, deterministic, may be executed without loading into context
- **Note**: Scripts may still need to be read by Codex for patching or environment-specific adjustments

##### References (`references/`)

Documentation and reference material intended to be loaded as needed into context to inform Codex's process and thinking.

- **When to include**: For documentation that Codex should reference while working
- **Examples**: `references/finance.md` for financial schemas, `references/mnda.md` for company NDA template, `references/policies.md` for company policies, `references/api_docs.md` for API specifications
- **Use cases**: Database schemas, API documentation, domain knowledge, company policies, detailed workflow guides
- **Benefits**: Keeps SKILL.md lean, loaded only when Codex determines it's needed
- **Best practice**: If files are large (>10k words), include grep search patterns in SKILL.md
- **Avoid duplication**: Information should live in either SKILL.md or references files, not both. Prefer references files for detailed information unless it's truly core to the skill—this keeps SKILL.md lean while making information discoverable without hogging the context window. Keep only essential procedural instructions and workflow guidance in SKILL.md; move detailed reference material, schemas, and examples to references files.

##### Assets (`assets/`)

Files not intended to be loaded into context, but rather used within the output Codex produces.

- **When to include**: When the skill needs files that will be used in the final output
- **Examples**: `assets/logo.png` for brand assets, `assets/slides.pptx` for PowerPoint templates, `assets/frontend-template/` for HTML/React boilerplate, `assets/font.ttf` for typography
- **Use cases**: Templates, images, icons, boilerplate code, fonts, sample documents that get copied or modified
- **Benefits**: Separates output resources from documentation, enables Codex to use files without loading them into context

#### What to Not Include in a Skill

A skill should only contain essential files that directly support its functionality. Do NOT create extraneous documentation or auxiliary files, including:

- README.md
- INSTALLATION_GUIDE.md
- QUICK_REFERENCE.md
- CHANGELOG.md
- etc.

The skill should only contain the information needed for an AI agent to do the job at hand. It should not contain auxiliary context about the process that went into creating it, setup and testing procedures, user-facing documentation, etc. Creating additional documentation files just adds clutter and confusion.

### Progressive Disclosure Design Principle

Skills use a three-level loading system to manage context efficiently:

1. **Metadata (name + description)** - Always in context (~100 words)
2. **SKILL.md body** - When skill triggers (<5k words)
3. **Bundled resources** - As needed by Codex (Unlimited because scripts can be executed without reading into context window)

#### Progressive Disclosure Patterns

Keep SKILL.md body to the essentials and under 500 lines to minimize context bloat. Split content into separate files when approaching this limit. When splitting out content into other files, it is very important to reference them from SKILL.md and describe clearly when to read them, to ensure the reader of the skill knows they exist and when to use them.

**Key principle:** When a skill supports multiple variations, frameworks, or options, keep only the core workflow and selection guidance in SKILL.md. Move variant-specific details (patterns, examples, configuration) into separate reference files.

**Pattern 1: High-level guide with references**

```markdown
# PDF Processing

## Quick start

Extract text with pdfplumber:
[code example]

## Advanced features

- **Form filling**: See [FORMS.md](FORMS.md) for complete guide
- **API reference**: See [REFERENCE.md](REFERENCE.md) for all methods
- **Examples**: See [EXAMPLES.md](EXAMPLES.md) for common patterns
```

Codex loads FORMS.md, REFERENCE.md, or EXAMPLES.md only when needed.

**Pattern 2: Domain-specific organization**

For Skills with multiple domains, organize content by domain to avoid loading irrelevant context:

```
bigquery-skill/
├── SKILL.md (overview and navigation)
└── reference/
    ├── finance.md (revenue, billing metrics)
    ├── sales.md (opportunities, pipeline)
    ├── product.md (API usage, features)
    └── marketing.md (campaigns, attribution)
```

When a user asks about sales metrics, Codex only reads sales.md.

Similarly, for skills supporting multiple frameworks or variants, organize by variant:

```
cloud-deploy/
├── SKILL.md (workflow + provider selection)
└── references/
    ├── aws.md (AWS deployment patterns)
    ├── gcp.md (GCP deployment patterns)
    └── azure.md (Azure deployment patterns)
```

When the user chooses AWS, Codex only reads aws.md.

**Pattern 3: Conditional details**

Show basic content, link to advanced content:

```markdown
# DOCX Processing

## Creating documents

Use docx-js for new documents. See [DOCX-JS.md](DOCX-JS.md).

## Editing documents

For simple edits, modify the XML directly.

**For tracked changes**: See [REDLINING.md](REDLINING.md)
**For OOXML details**: See [OOXML.md](OOXML.md)
```

Codex reads REDLINING.md or OOXML.md only when the user needs those features.

**Important guidelines:**

- **Avoid deeply nested references** - Keep references one level deep from SKILL.md. All reference files should link directly from SKILL.md.
- **Structure longer reference files** - For files longer than 100 lines, include a table of contents at the top so Codex can see the full scope when previewing.

## Skill Creation Process

Skill creation involves these steps:

1. Understand the skill with concrete examples
2. Plan reusable skill contents (scripts, references, assets)
3. Initialize the skill (run init_skill.py)
4. Edit the skill (implement resources and write SKILL.md)
5. Validate the skill with `quick_validate.py --require-skill-disclosure`
6. Register or refresh the Skill Catalog record and validate its hashes.
7. Iterate based on real usage and forward-test complex skills.

During step 4, retain the initializer's `## Skill 使用披露` section and make any task-specific reporting language consistent with the project-wide contract.

Follow these steps in order, skipping only if there is a clear reason why they are not applicable.

### Skill Naming

- Use lowercase letters, digits, and hyphens only; normalize user-provided titles to hyphen-case (e.g., "Plan Mode" -> `plan-mode`).
- When generating names, generate a name under 64 characters (letters, digits, hyphens).
- Prefer short, verb-led phrases that describe the action.
- Namespace by tool when it improves clarity or triggering (e.g., `gh-address-comments`, `linear-address-issue`).
- Name the skill folder exactly after the skill name.

### Step 1: Understanding the Skill with Concrete Examples

Skip this step only when the skill's usage patterns are already clearly understood. It remains valuable even when working with an existing skill.

To create an effective skill, clearly understand concrete examples of how the skill will be used. This understanding can come from either direct user examples or generated examples that are validated with user feedback.

For example, when building an image-editor skill, relevant questions include:

- "What functionality should the image-editor skill support? Editing, rotating, anything else?"
- "Can you give some examples of how this skill would be used?"
- "I can imagine users asking for things like 'Remove the red-eye from this image' or 'Rotate this image'. Are there other ways you imagine this skill being used?"
- "What would a user say that should trigger this skill?"
- "Where should I create this project skill? Unless you explicitly choose another authorized project, place it under `F:/aaProject/ESFrameWorkPublish/.agents/skills` and use an `es-` name so Codex can discover it from the project root."

To avoid overwhelming users, avoid asking too many questions in a single message. Start with the most important questions and follow up as needed for better effectiveness.

Conclude this step when there is a clear sense of the functionality the skill should support.

### Step 2: Planning the Reusable Skill Contents

To turn concrete examples into an effective skill, analyze each example by:

1. Considering how to execute on the example from scratch
2. Identifying what scripts, references, and assets would be helpful when executing these workflows repeatedly

Example: When building a `pdf-editor` skill to handle queries like "Help me rotate this PDF," the analysis shows:

1. Rotating a PDF requires re-writing the same code each time
2. A `scripts/rotate_pdf.py` script would be helpful to store in the skill

Example: When designing a `frontend-webapp-builder` skill for queries like "Build me a todo app" or "Build me a dashboard to track my steps," the analysis shows:

1. Writing a frontend webapp requires the same boilerplate HTML/React each time
2. An `assets/hello-world/` template containing the boilerplate HTML/React project files would be helpful to store in the skill

Example: When building a `big-query` skill to handle queries like "How many users have logged in today?" the analysis shows:

1. Querying BigQuery requires re-discovering the table schemas and relationships each time
2. A `references/schema.md` file documenting the table schemas would be helpful to store in the skill

To establish the skill's contents, analyze each concrete example to create a list of the reusable resources to include: scripts, references, and assets.

### Step 3: Initializing the Skill

At this point, it is time to actually create the skill.

Skip this step only if the skill being developed already exists. In this case, continue to the next step.

Before running `init_skill.py`, ask where the user wants the skill created. For this project, default to `F:/aaProject/ESFrameWorkPublish/.agents/skills`; only use a global Skill directory when the user explicitly requests a separate global Skill.

When creating a new skill from scratch, always run the `init_skill.py` script. The script conveniently generates a new template skill directory that automatically includes everything a skill requires, making the skill creation process much more efficient and reliable.

Usage:

```bash
scripts/init_skill.py <skill-name> --path <output-directory> [--resources scripts,references,assets] [--examples]
```

Examples:

```bash
python scripts/init_skill.py es-example-skill --path "F:/aaProject/ESFrameWorkPublish/.agents/skills" --resources scripts,references
python scripts/init_skill.py es-example-skill --path "F:/aaProject/ESFrameWorkPublish/.agents/skills" --resources scripts,references,assets
python scripts/init_skill.py es-example-skill --path "F:/aaProject/ESFrameWorkPublish/.agents/skills" --resources scripts --examples
```

The script:

- Creates the skill directory at the specified path
- Generates a SKILL.md template with proper frontmatter and TODO placeholders
- Creates `agents/openai.yaml` using agent-generated `display_name`, `short_description`, and `default_prompt` passed via `--interface key=value`
- Optionally creates resource directories based on `--resources`
- Optionally adds example files when `--examples` is set

After initialization, customize the SKILL.md and add resources as needed. If you used `--examples`, replace or delete placeholder files.

Generate `display_name`, `short_description`, and `default_prompt` by reading the skill, then pass them as `--interface key=value` to `init_skill.py` or regenerate with:

```bash
scripts/generate_openai_yaml.py <path/to/skill-folder> --interface key=value
```

Only include other optional interface fields when the user explicitly provides them. For full field descriptions and examples, see references/openai_yaml.md.

### Step 4: Edit the Skill

When editing the (newly-generated or existing) skill, remember that the skill is being created for another instance of Codex to use. Include information that would be beneficial and non-obvious to Codex. Consider what procedural knowledge, domain-specific details, or reusable assets would help another Codex instance execute these tasks more effectively.

After substantial revisions, or if the skill is particularly tricky, you should use subagents to forward-test the skill on realistic tasks or artifacts. When doing so, pass the artifact under validation rather than your diagnosis of what is wrong, and keep the prompt generic enough that success depends on transferable reasoning rather than hidden ground truth.

#### Start with Reusable Skill Contents

To begin implementation, start with the reusable resources identified above: `scripts/`, `references/`, and `assets/` files. Note that this step may require user input. For example, when implementing a `brand-guidelines` skill, the user may need to provide brand assets or templates to store in `assets/`, or documentation to store in `references/`.

Added scripts must be tested by actually running them to ensure there are no bugs and that the output matches what is expected. If there are many similar scripts, only a representative sample needs to be tested to ensure confidence that they all work while balancing time to completion.

If you used `--examples`, delete any placeholder files that are not needed for the skill. Only create resource directories that are actually required.

#### Update SKILL.md

**Writing Guidelines:** Always use imperative/infinitive form.

##### Frontmatter

Write the YAML frontmatter with `name` and `description`:

- `name`: The skill name
- `description`: This is the primary triggering mechanism for your skill, and helps Codex understand when to use the skill.
  - Include both what the Skill does and specific triggers/contexts for when to use it.
  - Include all "when to use" information here - Not in the body. The body is only loaded after triggering, so "When to Use This Skill" sections in the body are not helpful to Codex.
  - Example description for a `docx` skill: "Comprehensive document creation, editing, and analysis with support for tracked changes, comments, formatting preservation, and text extraction. Use when Codex needs to work with professional documents (.docx files) for: (1) Creating new documents, (2) Modifying or editing content, (3) Working with tracked changes, (4) Adding comments, or any other document tasks"

Do not include any other fields in YAML frontmatter.

##### Body

Write instructions for using the skill and its bundled resources.

### Step 5: Validate the Skill

Once development of the skill is complete, validate the skill folder to catch basic issues early:

```bash
scripts/quick_validate.py <path/to/skill-folder>
```

The validation script checks YAML frontmatter format, required fields, and naming rules. If validation fails, fix the reported issues and run the command again.

### Step 6: Iterate

After testing the skill, you may detect the skill is complex enough that it requires forward-testing; or users may request improvements.

User testing often this happens right after using the skill, with fresh context of how the skill performed.

**Forward-testing and iteration workflow:**

1. Use the skill on real tasks
2. Notice struggles or inefficiencies
3. Identify how SKILL.md or bundled resources should be updated
4. Implement changes and test again
5. Forward-test if it is reasonable and appropriate

## Forward-testing

To forward-test, launch subagents as a way to stress test the skill with minimal context.
Subagents should *not* know that they are being asked to test the skill.  They should be treated as
an agent asked to perform a task by the user.  Prompts to subagents should look like:
  `Use $skill-x at /path/to/skill-x to solve problem y`
Not:
  `Review the skill at /path/to/skill-x; pretend a user asks you to...`

Decision rule for forward-testing:
  - Err on the side of forward-testing
  - Ask for approval if you think there's a risk that forward-testing would:
    * take a long time,
    * require additional approvals from the user, or
    * modify live production systems

  In these cases, show the user your proposed prompt and request (1) a yes/no decision, and
  (2) any suggested modifictions.

Considerations when forward-testing:
   - use fresh threads for independent passes
   - pass the skill, and a request in a similar way the user would.
   - pass raw artifacts, not your conclusions
   - avoid showing expected answers or intended fixes
   - rebuild context from source artifacts after each iteration
   - review the subagent's output and reasoning and emitted artifacts
   - avoid leaving artifacts the agent can find on disk between iterations;
     clean up subagents' artifacts to avoid additional contamination.

If forward-testing only succeeds when subagents see leaked context, tighten the skill or the
forward-testing setup before trusting the result.


## Specialized static acceptance

Acceptance ID: `creator-pipeline`

Responsibility-specific static assertions (these are source-level requirements, not Runtime claims):
- explicit output path
- registration is idempotent
- quick validation
- catalog hash refresh
- UTF-8

Required specialized cases: `scaffold-contract, invalid-name-rejection, resource-composition, registration-idempotency, catalog-refresh`
Guidance: `references/static-specialized-acceptance.md`
