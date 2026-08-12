using UnityEngine;

namespace ES
{
    public partial class Entity
    {
        [System.NonSerialized] private ESCameraLease defaultCameraLease;

        /// <summary>
        /// 输入模块唯一可用的相机入口。Entity 只把 Look 意图交给 Director；它不能取得
        /// VCam，也不能修改 Priority、Follow、LookAt 或 Axis。
        /// </summary>
        public bool TrySetCameraLook(Vector2 lookInput)
        {
            return ESGameManager.LocalControl != null
                   && ESGameManager.LocalControl.IsLocallyControlled(this)
                   && defaultCameraLease.TrySetLook(lookInput);
        }

        /// <summary>
        /// 默认 Base 只属于当前本地控制 Entity。此方法由生命周期、LocalControl 切换与
        /// SceneBinding 注册共同调用，普通 NPC 即使配置了 Profile 也不会进入 MainView 仲裁。
        /// </summary>
        internal void RefreshDefaultCameraRequest()
        {
            ReleaseDefaultCameraRequest();

            EntityCharacterIdentity profile = GetComponent<EntityCharacterIdentity>();
            ESCameraModule camera = ESGameManager.Camera;
            if (profile == null
                || camera == null
                || ESGameManager.LocalControl == null
                || !ESGameManager.LocalControl.IsLocallyControlled(this))
                return;

            EntityTransformMapping mapping = EnsureTransformMapping();
            if (!profile.TryCreateDefaultCameraRequest(this, mapping, out ESCameraRequest request))
                return;

            defaultCameraLease = camera.Push(request);
        }

        internal void ReleaseDefaultCameraRequest()
        {
            if (!defaultCameraLease.IsValid)
                return;

            defaultCameraLease.Dispose();
            defaultCameraLease = ESCameraLease.Invalid;
        }
    }
}
