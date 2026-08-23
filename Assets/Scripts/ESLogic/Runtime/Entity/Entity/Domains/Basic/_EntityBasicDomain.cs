using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("基础域")]
    public class EntityBasicDomain : Domain<Entity, EntityBasicModuleBase>
    {
        [Title("脚贴合（台阶/地面）")]
        [LabelText("自动确保脚贴合模块"), Tooltip("开启后：运行时如果基础域没有添加‘基础台阶脚贴合模块’，会自动创建并加入（不需要额外脚本/组件）。\n注意：这会改变未配置该模块的实体的默认行为。")]
        public bool autoEnsureFootPlacementModule = false;

        [Title("RuntimeWatch")]
        [LabelText("自动确保RuntimeWatch验证模块"), Tooltip("开启后：运行时如果基础域没有添加 RuntimeWatch 验证模块，会自动创建并加入。建议仅在调试/验证链路时开启。")]
        public bool autoEnsureRuntimeWatchModule = false;

        public override void _AwakeRegisterAllModules()
        {
            if (autoEnsureFootPlacementModule)
            {
                EnsureFootPlacementModuleExists(applyRecommendedDefaults: true);
            }

            if (autoEnsureRuntimeWatchModule)
            {
                EnsureRuntimeWatchModuleExists();
            }

            base._AwakeRegisterAllModules();
        }

        public void NotifyPoolSpawned()
        {
            FindMyModule<EntityBasicHealthModule>()?.OnPoolSpawned();
            FindMyModule<EntityBasicCombatModule>()?.OnPoolSpawned();
            FindMyModule<EntityBasicInteractionModule>()?.OnPoolSpawned();
        }

        public void NotifyPoolDespawned()
        {
            FindMyModule<EntityBasicCombatModule>()?.OnPoolDespawned();
            FindMyModule<EntityBasicHealthModule>()?.OnPoolDespawned();
            FindMyModule<EntityBasicInteractionModule>()?.OnPoolDespawned();
        }

        [Button("确保脚贴合模块存在"), PropertyOrder(-10)]
        public void EnsureFootPlacementModuleExists(bool applyRecommendedDefaults = true)
        {
            // 这里既可编辑器点按钮用，也可运行时自动装载用
            var module = FindFootPlacementModule();
            if (module == null)
            {
                module = new EntityBasicFootPlacementModule();
                MyModules.Add(module);
                MyModules.ApplyBuffers(true);
            }

            if (applyRecommendedDefaults)
            {
                module.ApplyRecommendedDefaults();
            }
        }

        [Button("应用脚贴合推荐参数"), PropertyOrder(-9)]
        public void ApplyFootPlacementRecommendedDefaults()
        {
            var module = FindFootPlacementModule();
            if (module == null)
            {
                Debug.LogWarning("[EntityBasicDomain] 未找到‘基础台阶脚贴合模块’：请先添加模块，或点击‘确保脚贴合模块存在’。");
                return;
            }

            module.ApplyRecommendedDefaults();
        }

        [Button("确保RuntimeWatch验证模块存在"), PropertyOrder(-8)]
        public void EnsureRuntimeWatchModuleExists()
        {
            var module = FindRuntimeWatchModule();
            if (module != null)
                return;

            module = new EntityBasicRuntimeWatchModule();
            MyModules.Add(module);
            MyModules.ApplyBuffers(true);
        }

#if UNITY_EDITOR
        [Button("确保完整运动原型模块存在"), PropertyOrder(-7)]
        public void EnsureMotionPrototypeModulesExist()
        {
            EnsurePrototypeModule<EntityBasicMoveRotateModule>();
            EnsurePrototypeModule<EntityBasicFlyModule>();
            EnsurePrototypeModule<EntityBasicSwimModule>();
            EnsurePrototypeModule<EntityBasicClimbModule>();
            EnsurePrototypeModule<EntityBasicMountModule>();
            EnsurePrototypeModule<EntityBasicRootMotionModule>();
            MyModules.ApplyBuffers(true);
        }

        [Button("检查完整运动原型"), PropertyOrder(-6)]
        public void ValidateMotionPrototype()
        {
            int moveCount = 0;
            int flyCount = 0;
            int swimCount = 0;
            int climbCount = 0;
            int mountCount = 0;
            int rootMotionCount = 0;
            int expectedBeforeCount = 0;
            int expectedRotationCount = 0;
            int expectedVelocityCount = 0;

            int count = MyModules != null && MyModules.ValuesNow != null ? MyModules.ValuesNow.Count : 0;
            for (int i = 0; i < count; i++)
            {
                EntityBasicModuleBase module = MyModules.ValuesNow[i];
                if (module is EntityBasicMoveRotateModule) moveCount++;
                else if (module is EntityBasicFlyModule) flyCount++;
                else if (module is EntityBasicSwimModule) swimCount++;
                else if (module is EntityBasicClimbModule) climbCount++;
                else if (module is EntityBasicMountModule) mountCount++;
                else if (module is EntityBasicRootMotionModule) rootMotionCount++;

                if (module is IEntityKCCBeforeMotion) expectedBeforeCount++;
                if (module is IEntityKCCRotationMotion) expectedRotationCount++;
                if (module is IEntityKCCVelocityMotion) expectedVelocityCount++;
            }

            bool moduleCountsValid = moveCount == 1
                && flyCount == 1
                && swimCount == 1
                && climbCount == 1
                && mountCount == 1
                && rootMotionCount == 1;
            bool referencesValid = MyCore != null
                && MyCore.animator != null
                && MyCore.kcc != null
                && MyCore.kcc.motor != null
                && MyCore.stateDomain != null
                && MyCore.stateDomain.stateMachine != null;

            if (!moduleCountsValid || !referencesValid)
            {
                Debug.LogError(
                    $"[Entity运动原型检查] 未通过 | 引用完整={referencesValid} | " +
                    $"Move={moveCount}, Fly={flyCount}, Swim={swimCount}, Climb={climbCount}, Mount={mountCount}, RootMotion={rootMotionCount}");
                return;
            }

            if (Application.isPlaying)
            {
                bool schedulerValid = MyCore.kcc.RegisteredBeforeMotionCount == expectedBeforeCount
                    && MyCore.kcc.RegisteredRotationMotionCount == expectedRotationCount
                    && MyCore.kcc.RegisteredVelocityMotionCount == expectedVelocityCount;
                if (!schedulerValid)
                {
                    Debug.LogError(
                        $"[Entity运动原型检查] 调度器数量异常 | " +
                        $"Before={MyCore.kcc.RegisteredBeforeMotionCount}/{expectedBeforeCount}, " +
                        $"Rotation={MyCore.kcc.RegisteredRotationMotionCount}/{expectedRotationCount}, " +
                        $"Velocity={MyCore.kcc.RegisteredVelocityMotionCount}/{expectedVelocityCount}");
                    return;
                }
            }

            Debug.Log("[Entity运动原型检查] 通过：核心引用、六类运动模块及运行时调度器结构有效。", MyCore);
        }

        private void EnsurePrototypeModule<T>() where T : EntityBasicModuleBase, new()
        {
            int count = MyModules != null && MyModules.ValuesNow != null ? MyModules.ValuesNow.Count : 0;
            for (int i = 0; i < count; i++)
            {
                if (MyModules.ValuesNow[i] is T)
                    return;
            }

            MyModules.Add(new T());
        }
#endif

        private EntityBasicFootPlacementModule FindFootPlacementModule()
        {
            if (MyModules == null || MyModules.ValuesNow == null) return null;
            int count = MyModules.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                if (MyModules.ValuesNow[i] is EntityBasicFootPlacementModule m)
                {
                    return m;
                }
            }

            return null;
        }

        private EntityBasicRuntimeWatchModule FindRuntimeWatchModule()
        {
            if (MyModules == null || MyModules.ValuesNow == null) return null;
            int count = MyModules.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                if (MyModules.ValuesNow[i] is EntityBasicRuntimeWatchModule m)
                {
                    return m;
                }
            }

            return null;
        }

    }
}
