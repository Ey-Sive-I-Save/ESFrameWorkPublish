# External Unity execution contract

All accepted input and output paths use a project-relative path contract; absolute paths and paths that escape the approved ProjectRoot are rejected before execution.

`Invoke-ESUnityCli.ps1` is an ES safety wrapper around an external Unity process. It is not permission to start Unity by itself.

## Required authorization

Execution modes (`Compile`, `EditModeTests`, `PlayModeTests`) require a one-time developer authorization bound to:

- TaskContractId and AIBrain PlanHash;
- exact Unity executable path, project version and executable hash;
- ProjectRoot and approved log/results paths;
- mode, test filter/category, time budget, timeout and stop condition;
- the matching AICommand id/hash and fresh receipt destination.

`Status` is the only mode that is read-only and may run without external process authorization.

## Safety gates

- Reject executable/project/log/result paths outside their approved roots or through reparse points.
- Refuse batchmode when the same Project is open or has an active Unity lock.
- Do not accept arbitrary Unity arguments, `-executeMethod`, shell fragments or unbounded environment expansion.
- Preserve the exact argument array, process exit code, log path and Test Runner result path in the receipt.

## Acceptance separation

`unity-cli-batchmode` proves only that the guarded Unity process and requested layer ran. It does not prove Console cleanliness, domain reload success, PlayMode behavior, Profiler, Player, IL2CPP or release acceptance without their own evidence.
