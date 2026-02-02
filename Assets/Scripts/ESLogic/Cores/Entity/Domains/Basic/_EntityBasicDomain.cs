using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable, TypeRegistryItem("基础域")]
    public class EntityBasicDomain : Domain<Entity, EntityBasicModuleBase>
    {
        [Title("默认地面参数")]
        [LabelText("启用默认地面参数")]
        public bool applyGroundDefaults = true;

        [InlineProperty, HideLabel]
        public GroundedDefaults groundedDefaults = GroundedDefaults.Default;

        public override void _AwakeRegisterAllModules()
        {
            base._AwakeRegisterAllModules();
            ApplyGroundDefaults();
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
