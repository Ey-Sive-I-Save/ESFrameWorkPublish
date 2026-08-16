# Player Architecture Collaboration Notes

This note is for AI agents working on the player-object/model architecture rebuild. Keep it factual and update it when the code changes.

> 2026-08-16 current correction: `EntityEquipmentDomain` is wired as the fifth serialized domain. Inventory / Slot / Attachment / Effect now have a minimum runtime slice, but PlayMode gameplay and release acceptance remain open. Use `装备定义与装配推进路线_AI协作说明.md` as the current contract.

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

配套必读：

- `角色通用架构验证_MMO开放世界角色切换剧情RPG战斗_AI协作说明.md`：用 MMO、开放世界、角色切换、剧情和 RPG 战斗压力测试玩家/角色模型重构边界。

## Verified Project Shape

- This is a Unity project. The requested workspace is `Assets/Plugins`, but the player/entity runtime is mostly outside it under `Assets/Scripts/ESLogic`.
- `Assets/Scripts/ESPlayer` 是当前保留的空壳程序集和历史测试材料；不要把它当作玩家实现入口。它暂不移动，因为项目文件夹图标工具缓存了该绝对路径。
- The active runtime entry for player-like characters is `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Entity.cs`.
- `Entity` inherits `Core` and implements `KinematicCharacterController.ICharacterController`.
- `Entity` directly registers five serialized domains: `EntityBasicDomain`, `EntityAIDomain`, `EntityBuffDomain`, `EntityEquipmentDomain`, and `EntityStateDomain`.
- The underlying framework pattern is `Core -> Domain -> Module`.
- `Core.Update()` updates registered domains. `Domain` updates its modules. `Module.TableKeyType` writes into `Core.ModuleTables`.
- `ESGameManager` now lives under `Assets/Scripts/ESLogic/Runtime/GameManager/-GameManager_Core/ESGameManager.cs`, not under the deleted `Assets/Plugins/ES/2_Feature/ESGameCore` tree.
- The current worktree contains many user/uncommitted changes. Do not revert or overwrite unrelated edits.

## Current Risk Hotspots

- `Entity.cs` combines generic entity hosting, KCC motion callbacks, state-machine bridging, IK driver lookup, and gameplay-facing motion API. Treat it as a high-risk file.
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Basic/EntityBasicModules.cs` is very large and mixes movement, combat, weapon handling, camera, quick stop, root motion, skill test code, and shared structs. Avoid adding more player-specific behavior there unless explicitly migrating.
- `Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/AI/EntityAIModules.cs` contains input collection modules and the partial implementation of the `EntityAIDomain` input executor. It is not purely AI.
- 当前输入主链是 `ESInputModule -> ESInputService -> EntityPlayerInputWriteModule -> EntityAIDomain.inputState -> EntityAIDomain`。`inputState` 是 Awake 时创建的纯运行态，不进入 Prefab/Scene 序列化。不要在新玩家代码中直接绑定 Unity `InputActionProperty`，也不要恢复已删除的旧实体输入类型。
- `EntityEquipmentDomain` owns inventory, weapon slots, attachment transitions and equipment-effect sources. `EntityBasicCombatModule` consumes the active equipment slot and executes attack/fire; it must not regain equipment ownership.
- Buff 域已有运行时实例、叠层、ValueChange、Permit 和 OpSupport 链路；但仍需按当前源码确认具体能力，不能把它误当作已经完成全部商业 Buff 功能。

## Architecture Direction

The current direction is to keep `Entity` as the generic runtime body and preserve one control path through its existing domains. Do not add a parallel player aggregate or a second controller stack.

Recommended target layering:

1. `Entity`: generic body, KCC adapter, five-domain host, state/IK bridge.
2. `EntityCharacterIdentity`: the sole character Prefab identity and DataInfo binding entry.
3. `ESInputService -> EntityPlayerInputWriteModule -> EntityAIDomain`: the current player intent write and execution chain.
4. `EntityEquipmentDomain`: inventory, slot, attachment and equipment-effect ownership.
5. `EntityBasicDomain`: motion, interaction and combat execution modules; primary attack enters through `EntityBasicCombatModule.TryExecutePrimaryAttack()`.
6. `EntityStateDomain` and presentation drivers: animation, state and final IK consumption without owning gameplay facts.

The important boundary is: input sources write intent; `EntityAIDomain` consumes it; Equipment owns equipment facts; Basic modules execute body/combat behavior. Do not let UI/input/replay/network systems manipulate Basic modules, KCC, weapon Rigidbody or root Transform directly.

## Migration Rules For Future Agents

- Read the relevant files before editing; names are not reliable enough in this codebase.
- Do not add `PlayerActor`, `CharacterActor`, composition facades or synonymous parallel roots. Extend the existing Entity/Domain/Module contracts only where a verified responsibility is missing.
- Avoid mass refactors while the worktree is dirty. Keep changes small and reversible.
- Preserve Odin serialization and Unity `.meta` files when moving or creating assets.
- 已删除的 `EntityAIInputSystemModule` 与 `EntityInputStateModule` 不得恢复；若旧场景或 Prefab 出现 Missing Type，清理其序列化坏引用。
- 玩家专属能力不得以第二套输入、运动、战斗或装备控制器绕过现有 Domain；实验材料只能放入 `Tests~` 或 `Samples~`。
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
