# Agent Artifact Generation Contract

## Request authority

`generation-request.json` is the request authority. It contains:

- Goal: intended outcome and context.
- Reference: live project files that must be read.
- Constraint: required, forbidden, permission, and quality boundaries.
- OutputArtifact: allowed artifact kind and formal target path.
- Validation: required static validation and approval gates.
- Relations: stable Edge/Node/Port identities describing how context flows into requirements, artifacts, and approval.

Only declared OutputArtifact targets are eligible for the candidate Manifest.

## Mind-map semantics

The request is a requirement mind map, not a gameplay or command runtime graph:

```text
Context     Goal / Reference
   ↓
Requirement Constraint
   ↓
Artifact    AICommand Output / Agent Skill Output
   ↓
Approval    Validation
```

For every OutputArtifact:

1. trace incoming `relations` back to the Goal;
2. collect only connected References and Constraints;
3. preserve Required, Forbidden, Permission, and Quality distinctions;
4. use rationale and verification fields when explaining or validating requirements;
5. reject disconnected, cyclic, missing, or cross-stage relationships;
6. never interpret relations as runtime scheduling or execution authority.

The generated AICommand or Agent Skill must remain understandable without opening Unity. Convert the connected graph into explicit sections for purpose, inputs/triggers, mandatory reads, permissions, prohibitions, workflow, validation, delivery, and human approval.

## Candidate layout

```text
ES/Automation/Candidates/AgentAuthoring/<request-id>/
├── generation-request.json
├── generation-prompt.md
├── candidate-manifest.json
├── validation-report.md
└── candidate/
    └── generated files
```

`candidateRelativePath` is relative to the request directory and must start with `candidate/`. It may not be absolute or contain `..`.

## Allowed formal targets

AICommand:

```text
Assets/Plugins/ES/AICommands/**/*.md
```

Agent Skill:

```text
.agents/skills/es-*/SKILL.md
.agents/skills/es-*/agents/openai.yaml
.agents/skills/es-*/references/*
.agents/skills/es-*/scripts/*
.agents/skills/es-*/assets/*
```

The skill directory must be a direct child of `.agents/skills` and use lowercase letters, digits, and hyphens.

## Approval boundary

Candidate generation never grants formal write authority. The Unity review window must:

1. load the Manifest;
2. validate candidate and target paths;
3. show a Diff;
4. require explicit human approval;
5. back up overwritten files;
6. import candidates;
7. run existing AICommand and UTF-8 validators;
8. roll back when formal validation fails.

Official Skill `quick_validate.py` remains separate evidence. If unavailable, report it as not run.
