# ES acceptance evidence matrix

Read these rule areas first:

- `Assets/Plugins/ES/AIWarnings/50_验证与发布（ValidationRelease）`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/构建与IL2CPP（BuildIL2CPP）`
- `Assets/Plugins/ES/AIWarnings/10_P0最高约束（P0Guardrails）/资源运行时与发布（RuntimeAssets）` for resource changes.

Use the following rows and mark each `passed`, `failed`, `blocked`, `not-required`, or `not-run`:

| Evidence row | Minimum proof |
| --- | --- |
| Source integrity | Scoped diff review, strict UTF-8, no unintended rewrites |
| Generated project | Exact `.csproj` command and result; label only `dotnet-build` |
| Unity editor compile | Correct project instance, import/refresh, domain reload complete, Console result |
| EditMode | Named test assembly/filter, completed job, counts and failures |
| PlayMode | Named test or reproducible runtime scenario and observed result |
| Profiler | Captured metric, scene/scenario, duration, target and threshold |
| Player build | Unity version, target, development flags, output and build result |
| IL2CPP | Target platform, backend, build output and native-stage result |
| Resource plan | Catalog/plan/manifest generation, validation and artifact hashes |
| Provider/download | Actual provider path, download, hash verification, load, unload and rollback |
| Release | Real publishing target, uploaded artifacts, verification and rollback evidence |

Useful commands include `编译与ReloadDomain内存_检查_AI命令.md`, resource export checks, and subsystem-specific commands. A command supplies scope; it does not replace test evidence.
