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

        [Title("默认地面参数")]
        [LabelText("启用默认地面参数")]
        public bool applyGroundDefaults = true;

        [InlineProperty, HideLabel]
        public GroundedDefaults groundedDefaults = GroundedDefaults.Default;

        public override void _AwakeRegisterAllModules()
        {
            if (autoEnsureFootPlacementModule)
            {
                EnsureFootPlacementModuleExists(applyRecommendedDefaults: true);
            }
            base._AwakeRegisterAllModules();
            ApplyGroundDefaults();
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

        [Button("应用默认地面参数")]
        public void ApplyGroundDefaults()
        {
            if (!applyGroundDefaults || MyCore == null) return;
            var kcc = MyCore.kcc;
            kcc.maxStableMoveSpeed = groundedDefaults.maxStableMoveSpeed;
            kcc.stableMovementSharpness = groundedDefaults.stableMovementSharpness;
            kcc.jumpSpeed = groundedDefaults.jumpSpeed;
            kcc.orientationSharpness = groundedDefaults.orientationSharpness;
        }
    }

    [Serializable]
    public struct GroundedDefaults
    {
        [LabelText("最大地面速度")]
        public float maxStableMoveSpeed;

        [LabelText("地面响应")]
        public float stableMovementSharpness;

        [LabelText("跳跃速度")]
        public float jumpSpeed;

        [LabelText("转向锐度")]
        public float orientationSharpness;

        public static GroundedDefaults Default => new GroundedDefaults
        {
            maxStableMoveSpeed = 8f,
            stableMovementSharpness = 15f,
            jumpSpeed = 8f,
            orientationSharpness = 10f
        };
    }
}
