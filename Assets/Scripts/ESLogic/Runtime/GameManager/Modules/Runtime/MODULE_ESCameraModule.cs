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
        private const int ScopePriorityOffsetLimit = 1000;
        private const int LocalPlayerPriority = 0;
        private const int DemoPriority = 20;
        private const int SmallModePriority = 40;
        private const int VehiclePriority = 80;
        private const int SkillPriority = 120;
        private const int StoryPriority = 220;
        private const int TimelinePriority = 320;

        [NonSerialized] private ESCameraDirector director;
        [NonSerialized] private ESLocalControlService subscribedLocalControl;
        [NonSerialized] private readonly List<ESCameraControlScope> scopes = new List<ESCameraControlScope>(4);
        [NonSerialized] private int nextScopeGeneration;
        [NonSerialized] private bool disposingScope;

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
            DisposeScopes();
            DisposeDirector();
            base.OnDisable();
        }

        public override void OnDestroy()
        {
            UnbindLocalControl();
            DisposeScopes();
            DisposeDirector();
            base.OnDestroy();
        }

        /// <summary>
        /// 为 Demo/小模式/玩家代码取得低暴露控制句柄。Scope 只绑定当前本地实体，
        /// 离开控制权或场景时由模块统一失效，业务无需复制清理代码。
        /// </summary>
        public bool TryOpenLocalScope(out ESCameraControlScope scope, out ESCameraFailure failure)
        {
            return TryOpenScope(ESCameraControlSource.LocalPlayer, null, null, out scope, out failure);
        }

        public bool TryOpenDemoScope(out ESCameraControlScope scope, out ESCameraFailure failure)
        {
            return TryOpenScope(ESCameraControlSource.Demo, null, null, out scope, out failure);
        }

        public bool TryOpenSmallModeScope(out ESCameraControlScope scope, out ESCameraFailure failure)
        {
            return TryOpenScope(ESCameraControlSource.SmallMode, null, null, out scope, out failure);
        }

        public bool TryOpenVehicleScope(Entity driver, out ESCameraControlScope scope, out ESCameraFailure failure)
        {
            return TryOpenScope(ESCameraControlSource.Vehicle, driver, null, out scope, out failure);
        }

        public bool TryOpenSkillScope(Entity skillOwner, out ESCameraControlScope scope, out ESCameraFailure failure)
        {
            return TryOpenScope(ESCameraControlSource.Skill, skillOwner, null, out scope, out failure);
        }

        /// <summary>仅由 ESStoryModule 调用；任意业务对象不能伪造剧情权限。</summary>
        internal bool TryOpenStoryScope(ESStoryInstance story, out ESCameraControlScope scope, out ESCameraFailure failure)
        {
            return TryOpenScope(ESCameraControlSource.Story, null, story, out scope, out failure);
        }

        /// <summary>仅由受信 Timeline 集成调用；普通 Timeline 数据仍只能走本地 Push。</summary>
        internal bool TryOpenTimelineScope(Entity owner, out ESCameraControlScope scope, out ESCameraFailure failure)
        {
            return TryOpenScope(ESCameraControlSource.Timeline, owner, null, out scope, out failure);
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

        internal bool IsScopeValid(ESCameraControlScope scope)
        {
            if (scope == null || director == null || !Signal_IsActiveAndEnable
                || !scopes.Contains(scope)
                || !scope.Matches(this, scope.Generation))
                return false;

            if (!IsCurrentLocalEntity(scope.RequestOwner))
                return false;

            if (scope.Source == ESCameraControlSource.Story)
            {
                if (!ESGameManager.TryGetModule(out ESStoryModule storyModule)
                    || !storyModule.IsForeground(scope.StoryAuthority))
                    return false;
            }

            if (scope.Source == ESCameraControlSource.Timeline
                && (ESGameManager.RuntimeMode == null
                    || ESGameManager.RuntimeMode.CurrentMode != ESRuntimeMode.Cutscene))
                return false;

            return true;
        }

        internal bool TryScopePlayShot(
            ESCameraControlScope scope,
            ESCameraDefinitionReference definition,
            Transform subject,
            Transform lookAt,
            int priorityOffset,
            out ESCameraLease lease,
            out ESCameraFailure failure)
        {
            lease = ESCameraLease.Invalid;
            failure = ValidateScope(scope);
            if (!failure.IsSuccess)
                return false;
            if (!definition.IsConfigured)
            {
                failure = new ESCameraFailure(ESCameraFailureReason.DefinitionInvalid);
                return false;
            }
            if (!IsUsableTarget(subject) || (lookAt != null && !IsUsableTarget(lookAt)))
            {
                failure = new ESCameraFailure(ESCameraFailureReason.TargetInvalid);
                return false;
            }
            if (!TryResolvePriority(scope.Source, priorityOffset, out int priority))
            {
                failure = new ESCameraFailure(ESCameraFailureReason.PriorityInvalid);
                return false;
            }

            ESCameraRequest request = ESCameraRequest.CreateShot(
                scope.ViewId, definition, priority, scope.RequestOwner, subject, lookAt);
            lease = Push(request);
            if (!lease.IsValid)
            {
                failure = new ESCameraFailure(ESCameraFailureReason.ViewUnavailable);
                return false;
            }

            scope.Track(lease);
            return true;
        }

        internal bool TryScopePlayModifier(
            ESCameraControlScope scope,
            ESCameraModifier modifier,
            ESCameraDefinitionReference compatibleDefinition,
            int priorityOffset,
            out ESCameraLease lease,
            out ESCameraFailure failure)
        {
            lease = ESCameraLease.Invalid;
            failure = ValidateScope(scope);
            if (!failure.IsSuccess)
                return false;
            if (!modifier.IsValid)
            {
                failure = new ESCameraFailure(ESCameraFailureReason.ModifierInvalid);
                return false;
            }
            if (!TryResolvePriority(scope.Source, priorityOffset, out int priority))
            {
                failure = new ESCameraFailure(ESCameraFailureReason.PriorityInvalid);
                return false;
            }

            ESCameraRequest request = ESCameraRequest.CreateModifier(
                scope.ViewId, priority, scope.RequestOwner, modifier, compatibleDefinition);
            lease = Push(request);
            if (!lease.IsValid)
            {
                failure = new ESCameraFailure(ESCameraFailureReason.ViewUnavailable);
                return false;
            }

            scope.Track(lease);
            return true;
        }

        internal bool TryScopeUpdateTarget(
            ESCameraControlScope scope,
            ESCameraLease lease,
            Transform subject,
            Transform lookAt,
            out ESCameraFailure failure)
        {
            failure = ValidateScope(scope);
            if (!failure.IsSuccess)
                return false;
            if (!scope.Owns(lease))
            {
                failure = new ESCameraFailure(ESCameraFailureReason.ScopeInvalid);
                return false;
            }
            if (!IsUsableTarget(subject) || (lookAt != null && !IsUsableTarget(lookAt)))
            {
                failure = new ESCameraFailure(ESCameraFailureReason.TargetInvalid);
                return false;
            }

            if (!TrySetTarget(lease, subject, lookAt))
            {
                failure = new ESCameraFailure(ESCameraFailureReason.RequestRejected);
                return false;
            }

            return true;
        }

        internal bool TryScopeSetLook(
            ESCameraControlScope scope,
            Vector2 lookInput,
            out ESCameraFailure failure)
        {
            failure = ValidateScope(scope);
            if (!failure.IsSuccess)
                return false;
            if (!IsFinite(lookInput))
            {
                failure = new ESCameraFailure(ESCameraFailureReason.RequestRejected);
                return false;
            }
            if (ESGameManager.RuntimeMode != null && !ESGameManager.RuntimeMode.CurrentPolicy.allowCameraLook)
            {
                failure = new ESCameraFailure(ESCameraFailureReason.RuntimeModeDenied);
                return false;
            }

            if (!TrySetLookForScope(scope, lookInput))
            {
                failure = new ESCameraFailure(ESCameraFailureReason.RequestRejected);
                return false;
            }

            return true;
        }

        internal bool TryReleaseScopeLease(ESCameraControlScope scope, ESCameraLease lease)
        {
            if (scope == null || !scope.Owns(lease))
                return false;

            bool released = Release(lease);
            if (released)
                scope.Untrack(lease);
            return released;
        }

        internal void DisposeScope(ESCameraControlScope scope)
        {
            if (scope == null)
                return;

            bool previous = disposingScope;
            disposingScope = true;
            scope.ReleaseAll(this);
            disposingScope = previous;
            scopes.Remove(scope);
        }

        private bool TryOpenScope(
            ESCameraControlSource source,
            Entity requestedOwner,
            ESStoryInstance storyAuthority,
            out ESCameraControlScope scope,
            out ESCameraFailure failure)
        {
            scope = null;
            failure = ESCameraFailure.None;
            if (!IsReady)
            {
                failure = new ESCameraFailure(ESCameraFailureReason.ModuleUnavailable);
                return false;
            }

            Entity local = ESGameManager.LocalControl != null
                ? ESGameManager.LocalControl.ControlledEntity
                : null;
            Entity owner = requestedOwner ?? local;
            if (owner == null || !IsCurrentLocalEntity(owner))
            {
                failure = new ESCameraFailure(ESCameraFailureReason.OwnerNotLocal);
                return false;
            }

            if (source == ESCameraControlSource.Story)
            {
                if (storyAuthority == null
                    || !ESGameManager.TryGetModule(out ESStoryModule storyModule)
                    || !storyModule.IsForeground(storyAuthority))
                {
                    failure = new ESCameraFailure(ESCameraFailureReason.StoryNotForeground);
                    return false;
                }
            }

            if (source == ESCameraControlSource.Timeline
                && (ESGameManager.RuntimeMode == null
                    || ESGameManager.RuntimeMode.CurrentMode != ESRuntimeMode.Cutscene))
            {
                failure = new ESCameraFailure(ESCameraFailureReason.RuntimeModeDenied);
                return false;
            }

            int generation = nextScopeGeneration == int.MaxValue ? 1 : nextScopeGeneration + 1;
            nextScopeGeneration = generation;
            scope = new ESCameraControlScope(this, generation, source, ESCameraViewId.Main, owner, storyAuthority);
            scopes.Add(scope);
            return true;
        }

        private ESCameraFailure ValidateScope(ESCameraControlScope scope)
        {
            return IsScopeValid(scope)
                ? ESCameraFailure.None
                : new ESCameraFailure(ESCameraFailureReason.ScopeInvalid);
        }

        private bool TrySetLookForScope(ESCameraControlScope scope, Vector2 lookInput)
        {
            // A Scope deliberately does not expose its owner; this path can safely call the
            // existing module boundary after validity has been checked.
            for (int i = 0; i < scopes.Count; i++)
            {
                if (ReferenceEquals(scopes[i], scope))
                    return scope.TrySetLook(this, lookInput);
            }

            return false;
        }

        private bool TryResolvePriority(ESCameraControlSource source, int offset, out int priority)
        {
            priority = GetDefaultPriority(source);
            if (offset < -ScopePriorityOffsetLimit || offset > ScopePriorityOffsetLimit)
                return false;

            long candidate = (long)priority + offset;
            if (candidate < int.MinValue || candidate > int.MaxValue)
                return false;
            priority = (int)candidate;
            return true;
        }

        private static int GetDefaultPriority(ESCameraControlSource source)
        {
            switch (source)
            {
                case ESCameraControlSource.Demo: return DemoPriority;
                case ESCameraControlSource.SmallMode: return SmallModePriority;
                case ESCameraControlSource.Vehicle: return VehiclePriority;
                case ESCameraControlSource.Skill: return SkillPriority;
                case ESCameraControlSource.Story: return StoryPriority;
                case ESCameraControlSource.Timeline: return TimelinePriority;
                default: return LocalPlayerPriority;
            }
        }

        private void DisposeScopes()
        {
            while (scopes.Count > 0)
            {
                ESCameraControlScope scope = scopes[scopes.Count - 1];
                scope.InvalidateFromHost();
            }
            nextScopeGeneration = 0;
        }

        private static bool IsUsableTarget(Transform target)
        {
            return target != null && target.gameObject != null && target.gameObject.activeInHierarchy;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                   && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        }

        /// <summary>释放只验证 generation，不要求 Owner 仍然拥有本地控制权，保证切人/回池可清理旧 Lease。</summary>
        public bool Release(ESCameraLease lease)
        {
            bool released = director != null && director.Release(lease);
            if (released)
            {
                for (int i = scopes.Count - 1; i >= 0; i--)
                {
                    scopes[i].Untrack(lease);
                }
                if (!disposingScope)
                    PruneEmptyScopes();
            }
            return released;
        }

        private void PruneEmptyScopes()
        {
            for (int i = scopes.Count - 1; i >= 0; i--)
            {
                if (scopes[i].HasLeases)
                    continue;
                scopes[i].InvalidateFromHost();
            }
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

        /// <summary>只读诊断出口；将 Director 快照投影为可序列化回执，不暴露内部对象。</summary>
        public bool TryGetDiagnosticReceipt(ESCameraViewId viewId, int frame, out ESCameraDiagnosticReceipt receipt)
        {
            receipt = default;
            if (director == null || !director.TryGetDiagnosticSnapshot(viewId, out ESCameraDiagnosticSnapshot snapshot))
                return false;

            receipt = ESCameraDiagnosticReceipt.FromSnapshot(snapshot, frame);
            return true;
        }

        /// <summary>Lease 语义化 Look 写入入口。当前本地观测 Owner 之外的调用一律拒绝。</summary>
        public bool TrySetLook(ESCameraLease lease, Vector2 lookInput)
        {
            if (director == null || !director.TryGetOwner(lease, out UnityEngine.Object owner))
                return false;

            if (float.IsNaN(lookInput.x) || float.IsInfinity(lookInput.x)
                || float.IsNaN(lookInput.y) || float.IsInfinity(lookInput.y))
                return false;

            if (ESGameManager.RuntimeMode != null && !ESGameManager.RuntimeMode.CurrentPolicy.allowCameraLook)
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
            DisposeScopes();
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
            DisposeScopes();
            if (previous != null)
                ReleaseOwnedBy(previous);
            previous?.ReleaseDefaultCameraRequest();
            current?.RefreshDefaultCameraRequest();
        }
    }
}
