# Boundary decision contract

`es-skill-validator` is a project semantic gate. Structural validity is necessary but never sufficient.

The `Boundary` profile must fail closed when any of the following is observed:

- a write, Unity/MCP, process, network or external capability has no matching AICommand and current TaskContract;
- Skill text presents an executable route around AIWarnings, AICommand, AIBrain, governance or TaskContract;
- scripts read credentials, leave the project root, use destructive commands without a declared scope, or silently swallow exceptions;
- a Skill declares a broader write mode than the matched command permits;
- static Skill text claims Unity, runtime, Player, IL2CPP or release verification without bound evidence.

The validator reports a stable code, project-relative file, line and remediation detail. A prohibition such as “禁止绕过 AIWarnings” is not itself a violation; executable content is scanned separately. This profile is read-only and does not execute the Skill, AICommand, Unity, network or release workflow.
