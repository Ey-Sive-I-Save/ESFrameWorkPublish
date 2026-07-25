using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public enum ESWeaponTemplateFireKind
    {
        [InspectorName("射线检测")]
        HitScan,
        [InspectorName("投射物")]
        Shot,
        [InspectorName("混合模式")]
        Hybrid,
        [InspectorName("自定义")]
        Custom
    }

    /// <summary>
    /// Scene-side weapon template marker.
    /// This component only stores hierarchy references and design notes. It does not fire, reload or apply damage.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ES/武器/通用武器场景模板")]
    public sealed class ESWeaponSceneTemplate : MonoBehaviour
    {
        public const string RuntimeRootName = "00_运行根";
        public const string MountRootName = "10_挂载与握持";
        public const string BallisticRootName = "20_射击与弹道";
        public const string PresentationRootName = "30_表现资源";
        public const string DebugRootName = "40_调试占位";

        [Serializable]
        public sealed class IdentitySection
        {
            [LabelText("武器ID")] public string weaponId = "weapon.template.rifle";
            [LabelText("显示名称")] public string displayName = "通用步枪模板";
            [LabelText("模板类型")] public ESWeaponTemplateFireKind fireKind = ESWeaponTemplateFireKind.Hybrid;
            [TextArea(2, 4), LabelText("设计备注")] public string designNote = "仅作为场景结构模板，不包含真实枪械逻辑。";
        }

        [Serializable]
        public sealed class RuntimeBridgeSection
        {
            [LabelText("Item根节点")] public Item itemRoot;
            [LabelText("拥有者实体")] public Entity ownerEntity;
            [LabelText("IK驱动")] public StateFinalIKDriver finalIKDriver;
            [LabelText("状态机入口说明")] public string stateMachineNote = "由拥有者实体的状态表现域接入，不在武器模板上序列化 StateMachine。";
            [LabelText("Op入口说明")] public string opSupportNote = "运行时由 Item/技能/实体创建 OpSupport，模板只保留结构挂点。";
        }

        [Serializable]
        public sealed class MountSection
        {
            [LabelText("挂载根")] public Transform mountRoot;
            [LabelText("持有挂点")] public Transform holdSocket;
            [LabelText("背挂挂点")] public Transform backSocket;
            [LabelText("右手握把")] public Transform rightHandGrip;
            [LabelText("左手握把")] public Transform leftHandGrip;
            [LabelText("瞄准参考")] public Transform aimReference;
            [LabelText("后坐力轴心")] public Transform recoilPivot;
        }

        [Serializable]
        public sealed class BallisticSection
        {
            [LabelText("射击根")] public Transform ballisticRoot;
            [LabelText("枪口")] public Transform muzzle;
            [LabelText("弹壳出口")] public Transform shellEject;
            [LabelText("弹匣位置")] public Transform magazine;
            [LabelText("膛室位置")] public Transform chamber;
            [LabelText("射线起点")] public Transform rayOrigin;
            [LabelText("投射物出生点")] public Transform shotSpawn;
        }

        [Serializable]
        public sealed class PresentationSection
        {
            [LabelText("表现根")] public Transform presentationRoot;
            [LabelText("模型根")] public Transform modelRoot;
            [LabelText("碰撞根")] public Transform colliderRoot;
            [LabelText("特效根")] public Transform vfxRoot;
            [LabelText("音效根")] public Transform audioRoot;
            [LabelText("动画根")] public Transform animationRoot;
        }

        [Serializable]
        public sealed class DebugSection
        {
            [LabelText("调试根")] public Transform debugRoot;
            [LabelText("显示挂点Gizmos")] public bool drawSockets = true;
            [LabelText("显示射击方向")] public bool drawFireDirection = true;
            [LabelText("Gizmos尺寸"), MinValue(0.01f)] public float gizmoSize = 0.06f;
        }

        [TitleGroup("武器模板")]
        [HideLabel, SerializeField] public IdentitySection identity = new IdentitySection();

        [FoldoutGroup("武器模板/运行桥接", Expanded = true)]
        [HideLabel, SerializeField] public RuntimeBridgeSection runtimeBridge = new RuntimeBridgeSection();

        [FoldoutGroup("武器模板/挂载与握持", Expanded = true)]
        [HideLabel, SerializeField] public MountSection mount = new MountSection();

        [FoldoutGroup("武器模板/射击与弹道", Expanded = true)]
        [HideLabel, SerializeField] public BallisticSection ballistic = new BallisticSection();

        [FoldoutGroup("武器模板/表现资源", Expanded = false)]
        [HideLabel, SerializeField] public PresentationSection presentation = new PresentationSection();

        [FoldoutGroup("武器模板/调试显示", Expanded = false)]
        [HideLabel, SerializeField] public DebugSection debug = new DebugSection();

        [Button("按标准命名自动绑定子节点"), FoldoutGroup("武器模板/调试显示")]
        [ContextMenu("ES/按标准命名自动绑定子节点")]
        public void AutoBindByStandardNames()
        {
            runtimeBridge.itemRoot = runtimeBridge.itemRoot != null ? runtimeBridge.itemRoot : GetComponent<Item>();

            mount.mountRoot = FindDeepChild(MountRootName);
            mount.holdSocket = FindDeepChild("HoldSocket");
            mount.backSocket = FindDeepChild("BackSocket");
            mount.rightHandGrip = FindDeepChild("RightHandGrip");
            mount.leftHandGrip = FindDeepChild("LeftHandGrip");
            mount.aimReference = FindDeepChild("AimReference");
            mount.recoilPivot = FindDeepChild("RecoilPivot");

            ballistic.ballisticRoot = FindDeepChild(BallisticRootName);
            ballistic.muzzle = FindDeepChild("Muzzle");
            ballistic.shellEject = FindDeepChild("ShellEject");
            ballistic.magazine = FindDeepChild("Magazine");
            ballistic.chamber = FindDeepChild("Chamber");
            ballistic.rayOrigin = FindDeepChild("RayOrigin");
            ballistic.shotSpawn = FindDeepChild("ShotSpawn");

            presentation.presentationRoot = FindDeepChild(PresentationRootName);
            presentation.modelRoot = FindDeepChild("ModelRoot");
            presentation.colliderRoot = FindDeepChild("ColliderRoot");
            presentation.vfxRoot = FindDeepChild("VFXRoot");
            presentation.audioRoot = FindDeepChild("AudioRoot");
            presentation.animationRoot = FindDeepChild("AnimationRoot");

            debug.debugRoot = FindDeepChild(DebugRootName);
        }

        public bool HasRequiredAuthoringSockets()
        {
            return mount.rightHandGrip != null
                   && mount.leftHandGrip != null
                   && mount.aimReference != null
                   && ballistic.muzzle != null
                   && ballistic.rayOrigin != null;
        }

        private Transform FindDeepChild(string childName)
        {
            if (string.IsNullOrEmpty(childName))
                return null;

            var transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == childName)
                    return transforms[i];
            }
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            if (debug == null)
                return;

            float size = Mathf.Max(0.01f, debug.gizmoSize);
            if (debug.drawSockets)
            {
                DrawSocket(mount != null ? mount.rightHandGrip : null, Color.green, size);
                DrawSocket(mount != null ? mount.leftHandGrip : null, Color.cyan, size);
                DrawSocket(mount != null ? mount.aimReference : null, Color.yellow, size);
                DrawSocket(mount != null ? mount.recoilPivot : null, new Color(1f, 0.45f, 0.1f), size);
                DrawSocket(ballistic != null ? ballistic.muzzle : null, Color.red, size);
                DrawSocket(ballistic != null ? ballistic.shellEject : null, new Color(0.7f, 0.7f, 1f), size);
            }

            if (debug.drawFireDirection && ballistic != null && ballistic.muzzle != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(ballistic.muzzle.position, ballistic.muzzle.position + ballistic.muzzle.forward * 0.6f);
            }
        }

        private static void DrawSocket(Transform socket, Color color, float size)
        {
            if (socket == null)
                return;

            Gizmos.color = color;
            Gizmos.DrawWireSphere(socket.position, size);
        }
    }
}
