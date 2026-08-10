using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 游戏运行时相机门面。它持有 Director，并把“哪个 Owner 可以影响本地 View”
    /// 收口在模块边界；Director 本身只处理请求集合与仲裁，不认识玩家、AI 或载具。
    /// </summary>
    [Serializable, TypeRegistryItem("系统模块/相机")]
    public sealed class ESCameraModule : ESSystemModule
    {
        [NonSerialized] private ESCameraDirector director;
        [NonSerialized] private ESLocalControlService subscribedLocalControl;

        /// <summary>
        /// 相机运行时是否已创建。业务只能经由本模块请求、更新和释放 Lease，
        /// 不获得 Director 或 CM2 对象。
        /// </summary>
        public bool IsReady => director != null && Signal_IsActiveAndEnable;

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureDirector();
            BindLocalControl();
            ESGameManager.LocalControl?.ControlledEntity?.RefreshDefaultCameraRequest();
        }

        protected override void OnDisable()
        {
            UnbindLocalControl();
            DisposeDirector();
            base.OnDisable();
        }

        public override void OnDestroy()
        {
            UnbindLocalControl();
            DisposeDirector();
            base.OnDestroy();
        }

        /// <summary>
        /// 本地 View 的唯一业务入口。当前版本只允许 LocalControl 的当前 Entity；
        /// 回放、观战、剧情的受信 Bridge 尚未交付，因此没有外部提权入口。
        /// </summary>
        public ESCameraLease Push(in ESCameraRequest request)
        {
            return IsCurrentLocalEntity(request.owner) && director != null
                ? director.Push(request)
                : ESCameraLease.Invalid;
        }

        public bool Update(ESCameraLease lease, in ESCameraRequest request)
        {
            return IsCurrentLocalEntity(request.owner)
                   && director != null
                   && director.Update(lease, request);
        }

        /// <summary>释放只验证 generation，不要求 Owner 仍然拥有本地控制权，保证切人/回池可清理旧 Lease。</summary>
        public bool Release(ESCameraLease lease)
        {
            return director != null && director.Release(lease);
        }

        public int ReleaseOwnedBy(UnityEngine.Object owner)
        {
            return director != null ? director.ReleaseOwnedBy(owner) : 0;
        }

        /// <summary>
        /// 唯一允许角色移动、瞄准和射击读取的相机出口。它只暴露输出 Transform，绝不泄漏
        /// Brain、VCam、Rig 或 Cinemachine 轴。
        /// </summary>
        public bool TryGetOutputTransform(ESCameraViewId viewId, out Transform outputTransform)
        {
            outputTransform = null;
            return director != null && director.TryGetOutputTransform(viewId, out outputTransform);
        }

        /// <summary>Lease 语义化 Look 写入入口。当前本地观测 Owner 之外的调用一律拒绝。</summary>
        public bool TrySetLook(ESCameraLease lease, Vector2 lookInput)
        {
            if (director == null || !director.TryGetOwner(lease, out UnityEngine.Object owner))
                return false;

            return IsCurrentLocalEntity(owner) && director.TrySetLook(lease, lookInput);
        }

        /// <summary>Lease 语义化目标更新入口；业务仍不接触 Rig、VCam 或 CM2 Axis。</summary>
        public bool TrySetTarget(ESCameraLease lease, Transform follow, Transform lookAt = null)
        {
            if (director == null || !director.TryGetOwner(lease, out UnityEngine.Object owner))
                return false;

            return IsCurrentLocalEntity(owner) && director.TrySetTarget(lease, follow, lookAt);
        }

        /// <summary>供 SceneBinding 使用的 View 注册边界，不向角色/技能/载具暴露 Adapter。</summary>
        internal bool RegisterView(ESCameraViewId viewId, int sceneEpoch, IESCameraViewAdapter adapter)
        {
            // SceneBinding normally registers after this module's OnEnable, but explicit scene
            // bootstrap and tests may reach this boundary first. Keep Director ownership inside
            // the module instead of requiring callers to know its lifecycle ordering.
            if (!Signal_IsActiveAndEnable)
                return false;

            EnsureDirector();

            bool registered = director.RegisterView(viewId, sceneEpoch, adapter);
            if (registered)
                ESGameManager.LocalControl?.ControlledEntity?.RefreshDefaultCameraRequest();

            return registered;
        }

        internal void UnregisterView(ESCameraViewId viewId, int sceneEpoch, IESCameraViewAdapter adapter)
        {
            director?.UnregisterView(viewId, sceneEpoch, adapter);
        }

        /// <summary>ESGameManager 的唯一正常 LateUpdate 提交点。</summary>
        public void LateTick()
        {
            director?.LateTick();
        }

        /// <summary>仅明确剧情切镜边界允许调用。</summary>
        public bool FlushNow(ESCameraViewId viewId)
        {
            return director != null && director.FlushNow(viewId);
        }

        private void EnsureDirector()
        {
            director ??= new ESCameraDirector();
        }

        private void DisposeDirector()
        {
            director?.Dispose();
            director = null;
        }

        private static bool IsCurrentLocalEntity(UnityEngine.Object owner)
        {
            return owner is Entity entity
                   && ESGameManager.LocalControl != null
                   && ESGameManager.LocalControl.IsLocallyControlled(entity);
        }

        private void BindLocalControl()
        {
            ESLocalControlService localControl = ESGameManager.LocalControl;
            if (ReferenceEquals(subscribedLocalControl, localControl))
                return;

            UnbindLocalControl();
            subscribedLocalControl = localControl;
            if (subscribedLocalControl != null)
                subscribedLocalControl.OnControlledEntityChanged += HandleControlledEntityChanged;
        }

        private void UnbindLocalControl()
        {
            if (subscribedLocalControl != null)
                subscribedLocalControl.OnControlledEntityChanged -= HandleControlledEntityChanged;

            subscribedLocalControl = null;
        }

        private void HandleControlledEntityChanged(Entity previous, Entity current)
        {
            OnControlledEntityChanged(previous, current);
        }

        internal void OnControlledEntityChanged(Entity previous, Entity current)
        {
            if (previous != null)
                ReleaseOwnedBy(previous);
            previous?.ReleaseDefaultCameraRequest();
            current?.RefreshDefaultCameraRequest();
        }
    }
}
