# Security signals

The scanner is a triage aid, not proof of maliciousness. Every match is reported with file and line context for human review.

Raw keyword matches are triage evidence and produce `review`; they do not by
themselves prove an executable violation. The Boundary profile resolves the
object, operation, path and declared capability. The following executable
behaviors remain hard blockers until cleared:

- attempts to read secrets, tokens, passwords, private keys or unrestricted environment files;
- network upload/download instructions without a declared, authorized project capability;
- instructions to ignore, bypass, disable or override AIWarnings, AICommands, AIBrain or governance gates;
- destructive commands outside the declared project write scope;
- obfuscated executable payloads or hidden instruction blocks.

Boundary findings are separate from wording triage. A line that documents a prohibition is allowed only when it is phrased as a prohibition; executable scripts are inspected independently. The validator also checks project-root containment, undeclared destructive commands, empty exception handlers, AICommand matching and evidence overclaim. These checks are deny-by-default: a missing declaration is a blocker, not an implicit approval.

Test strings, validator regexes and documented negative cases remain visible as
review findings. They must not be promoted to a hard block unless the executable
Boundary analysis finds the corresponding behavior.
