# Security signals

The scanner is a triage aid, not proof of maliciousness. Every match is reported with file and line context for human review.

High-risk signals that block acceptance until cleared:

- attempts to read secrets, tokens, passwords, private keys or unrestricted environment files;
- network upload/download instructions without a declared, authorized project capability;
- instructions to ignore, bypass, disable or override AIWarnings, AICommands, AIBrain or governance gates;
- destructive commands outside the declared project write scope;
- obfuscated executable payloads or hidden instruction blocks.

Boundary findings are separate from wording triage. A line that documents a prohibition is allowed only when it is phrased as a prohibition; executable scripts are inspected independently. The validator also checks project-root containment, undeclared destructive commands, empty exception handlers, AICommand matching and evidence overclaim. These checks are deny-by-default: a missing declaration is a blocker, not an implicit approval.

Low-risk matches are warnings when the Skill explicitly documents an authorized test or migration scenario.
