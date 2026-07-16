# Player Architecture Collaboration Notes

This note is for AI agents working on the player-object/model architecture rebuild. Keep it factual and update it when the code changes.

## Responsibility

当前职责：协助重构玩家对象的整体模型与运行架构，使其从“功能堆叠型 Entity 模块集合”逐步演进为商业项目可维护的玩家模型。

本文件关注：

- 玩家对象模型边界：哪些职责属于通用 `Entity`，哪些职责应上升到玩家层。
- 玩家运行链路：输入、意图、移动、战斗、交互、相机、状态机、IK、KCC 的协作关系。
- 重构安全线：哪些现有系统不能误删、不能绕过、不能继续扩大耦合。
- 后续 AI 协作上下文：让其他 AI 快速知道当前重构目标和已确认事实。

本文件不负责：

- 直接定义最终产品玩法规则。
- 替代源码核查或编译验证。
- 证明某个旧模块已经可以删除。

## Verified Project Shape

- This is a Unity project. The requested workspace is `Assets/Plugins`, but the player/entity runtime is mostly outside it under `Assets/Scripts/ESLogic`.
- `Assets/Scripts/ESPlayer` currently exists but is mostly an empty shell. Do not assume it contains the active player implementation.
- The active runtime entry for player-like characters is `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`.
- `Entity` inherits `Core` and implements `KinematicCharacterController.ICharacterController`.
- `Entity` directly registers four serialized domains: `EntityBasicDomain`, `EntityAIDomain`, `EntityBuffDomain`, and `EntityStateDomain`.
- The underlying framework pattern is `Core -> Domain -> Module`.
- `Core.Update()` updates registered domains. `Domain` updates its modules. `Module.TableKeyType` writes into `Core.ModuleTables`.
- `ESGameManager` now lives under `Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.cs`, not under the deleted `Assets/Plugins/ES/2_Feature/ESGameCore` tree.
- The current worktree contains many user/uncommitted changes. Do not revert or overwrite unrelated edits.

## Current Risk Hotspots

- `Entity.cs` combines generic entity hosting, KCC motion callbacks, state-machine bridging, IK driver lookup, and gameplay-facing motion API. Treat it as a high-risk file.
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityBasicModules.cs` is very large and mixes movement, combat, weapon handling, camera, quick stop, root motion, skill test code, and shared structs. Avoid adding more player-specific behavior there unless explicitly migrating.
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/EntityAIModules.cs` contains both input collection and input dispatch. It is not purely AI.
- There are two input paths:
  - Global/service path: `ESInputModule`, `ESInputRuntime`, `ESInputService`, `ESInputActionId`.
  - Entity-local path: `EntityAIInputSystemModule` with `InputActionProperty` fields and `InputSnapshot`.
- Because of the two input paths, do not hardwire new player code directly to Unity `InputActionProperty` unless the task explicitly targets the old entity-local path.
- `EntityBasicCombatModule` contains weapon mounting, aim/peek, firing, and animation parameter duties. It is not only "combat".
- Buff domain currently looks skeletal compared with Basic/AI/State. Do not assume a full buff architecture already exists.

## Architecture Direction

The safer rebuild direction is to keep `Entity` as a generic runtime body and build player-specific orchestration beside it.

Recommended target layering:

1. `Entity`: generic body, KCC adapter, domain host, state/IK bridge.
2. `PlayerActor` or equivalent facade: the player-owned aggregate that references an `Entity`.
3. `PlayerIntent`: frame-level intent data, independent of keyboard, gamepad, AI, replay, or network source.
4. `PlayerInputAdapter`: converts `ESInputService`, AI, replay, or network packets into `PlayerIntent`.
5. `PlayerLocomotionController`: owns player movement decisions and delegates raw motion to `Entity`/KCC.
6. `PlayerCombatController`, `PlayerWeaponController`, `PlayerInteractionController`, `PlayerCameraBinding`: separated gameplay controllers.

The important boundary is: input source produces intent; intent drives player controllers; controllers call stable entity APIs. Do not let UI/input/replay/network systems directly manipulate random Basic modules.

## Migration Rules For Future Agents

- Read the relevant files before editing; names are not reliable enough in this codebase.
- Prefer adding thin adapter/facade classes over rewriting `Entity.cs` immediately.
- Avoid mass refactors while the worktree is dirty. Keep changes small and reversible.
- Preserve Odin serialization and Unity `.meta` files when moving or creating assets.
- Do not delete the old `EntityAIInputSystemModule` path until all prefabs/scenes/configs using it are identified.
- When introducing player-specific files, prefer `Assets/Scripts/ESPlayer` or a clearly named runtime folder instead of expanding `EntityBasicModules.cs`.
- If splitting `EntityBasicModules.cs`, first do mechanical class-per-file extraction with no behavior change. Functional redesign should be a separate step.
- Maintain compatibility with existing `Core.ModuleTables` lookup. Some systems call `TryGetValue(typeof(SomeModule))`, so changing `TableKeyType` can break runtime behavior.
- Treat KCC callbacks as high-frequency and allocation-sensitive. Avoid LINQ, reflection, hierarchy search, or string work inside movement callbacks.
- `EntityTransformMapping` already exists for stable transform lookup. Prefer it over repeated deep `Transform.Find`.
- Any new player architecture should support at least local player, AI-controlled entity, editor preview, and future replay/network input without duplicating gameplay logic.

## Known Paths Worth Reading First

- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/_EntityBasicDomain.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityBasicModules.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/EntityAIModules.cs`
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/State/_EntityStateDomain.cs`
- `Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.cs`
- `Assets/Scripts/ESLogic/Runtime/GameManager/Modules/Runtime/MODULE_ESInputModule.cs`
- `Assets/Plugins/ES/1_Design/Input`
- `Assets/Plugins/ES/1_Design/Core_Domain_Module`
