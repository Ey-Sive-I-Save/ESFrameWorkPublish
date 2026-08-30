using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 相机业务来源。来源只决定权限与默认优先级，不会把 VCam、Rig 或仲裁槽暴露给业务。
    /// </summary>
    public enum ESCameraControlSource : byte
    {
        LocalPlayer = 0,
        Demo = 1,
        SmallMode = 2,
        Vehicle = 3,
        Skill = 4,
        Story = 5,
        Timeline = 6,
    }

    /// <summary>高频接入失败的稳定分类；详细日志留在 Camera 模块，不污染业务 API。</summary>
    public enum ESCameraFailureReason : byte
    {
        None = 0,
        ModuleUnavailable = 1,
        ScopeInvalid = 2,
        OwnerNotLocal = 3,
        StoryNotForeground = 4,
        RuntimeModeDenied = 5,
        ViewUnavailable = 6,
        DefinitionInvalid = 7,
        TargetInvalid = 8,
        ModifierInvalid = 9,
        PriorityInvalid = 10,
        RequestRejected = 11,
        SourceNotAllowed = 12,
    }

    /// <summary>
    /// 不携带 Unity 对象的轻量失败回执。业务通常只需判断 bool；需要可观测性时读取
    /// Reason/Code 即可，不必解析相机内部日志或暴露 Director。
    /// </summary>
    public readonly struct ESCameraFailure
    {
        public static readonly ESCameraFailure None = new ESCameraFailure(ESCameraFailureReason.None);

        public readonly ESCameraFailureReason Reason;

        internal ESCameraFailure(ESCameraFailureReason reason)
        {
            Reason = reason;
        }

        public bool IsSuccess => Reason == ESCameraFailureReason.None;

        public string Code
        {
            get
            {
                switch (Reason)
                {
                    case ESCameraFailureReason.None: return "OK";
                    case ESCameraFailureReason.ModuleUnavailable: return "CAMERA_MODULE_UNAVAILABLE";
                    case ESCameraFailureReason.ScopeInvalid: return "CAMERA_SCOPE_INVALID";
                    case ESCameraFailureReason.OwnerNotLocal: return "CAMERA_OWNER_NOT_LOCAL";
                    case ESCameraFailureReason.StoryNotForeground: return "CAMERA_STORY_NOT_FOREGROUND";
                    case ESCameraFailureReason.RuntimeModeDenied: return "CAMERA_RUNTIME_MODE_DENIED";
                    case ESCameraFailureReason.ViewUnavailable: return "CAMERA_VIEW_UNAVAILABLE";
                    case ESCameraFailureReason.DefinitionInvalid: return "CAMERA_DEFINITION_INVALID";
                    case ESCameraFailureReason.TargetInvalid: return "CAMERA_TARGET_INVALID";
                    case ESCameraFailureReason.ModifierInvalid: return "CAMERA_MODIFIER_INVALID";
                    case ESCameraFailureReason.PriorityInvalid: return "CAMERA_PRIORITY_INVALID";
                    case ESCameraFailureReason.RequestRejected: return "CAMERA_REQUEST_REJECTED";
                    case ESCameraFailureReason.SourceNotAllowed: return "CAMERA_SOURCE_NOT_ALLOWED";
                    default: return "CAMERA_UNKNOWN_FAILURE";
                }
            }
        }

        public string Message
        {
            get
            {
                switch (Reason)
                {
                    case ESCameraFailureReason.None: return string.Empty;
                    case ESCameraFailureReason.ModuleUnavailable: return "相机模块尚未就绪。";
                    case ESCameraFailureReason.ScopeInvalid: return "相机控制 Scope 已失效或已释放。";
                    case ESCameraFailureReason.OwnerNotLocal: return "相机控制者不是当前本地实体。";
                    case ESCameraFailureReason.StoryNotForeground: return "只有前台 Story 才能取得剧情相机控制。";
                    case ESCameraFailureReason.RuntimeModeDenied: return "当前 RuntimeMode 不允许相机输入。";
                    case ESCameraFailureReason.ViewUnavailable: return "MainView 尚未注册或执行器未就绪。";
                    case ESCameraFailureReason.DefinitionInvalid: return "相机定义未配置或未通过 Catalog 解析。";
                    case ESCameraFailureReason.TargetInvalid: return "相机跟随目标为空、失活或不稳定。";
                    case ESCameraFailureReason.ModifierInvalid: return "相机 Modifier 未配置或包含非法数值。";
                    case ESCameraFailureReason.PriorityInvalid: return "相机优先级偏移超出允许范围。";
                    case ESCameraFailureReason.RequestRejected: return "相机请求被当前仲裁边界拒绝。";
                    case ESCameraFailureReason.SourceNotAllowed: return "该相机来源没有受信控制入口。";
                    default: return "相机请求失败。";
                }
            }
        }

        public override string ToString()
        {
            return IsSuccess ? Code : Code + ":" + Message;
        }
    }

    /// <summary>
    /// 业务侧唯一需要持有的相机控制句柄。一个 Scope 可以批量拥有 Shot/Modifier Lease，
    /// 离开 Demo、技能、剧情或 Timeline 时 Dispose 即可完成幂等清理。
    /// </summary>
    public sealed class ESCameraControlScope : IDisposable
    {
        private readonly ESCameraModule host;
        private readonly int generation;
        private readonly ESCameraControlSource source;
        private readonly ESCameraViewId viewId;
        private readonly UnityEngine.Object requestOwner;
        private readonly ESStoryInstance storyAuthority;
        private readonly List<ESCameraLease> leases = new List<ESCameraLease>(4);
        private bool released;

        internal ESCameraControlScope(
            ESCameraModule host,
            int generation,
            ESCameraControlSource source,
            ESCameraViewId viewId,
            UnityEngine.Object requestOwner,
            ESStoryInstance storyAuthority)
        {
            this.host = host;
            this.generation = generation;
            this.source = source;
            this.viewId = viewId;
            this.requestOwner = requestOwner;
            this.storyAuthority = storyAuthority;
        }

        public ESCameraControlSource Source => source;
        public ESCameraViewId ViewId => viewId;
        public bool IsValid => !released && host != null && host.IsScopeValid(this);

        /// <summary>以来源默认优先级播放镜头；业务不需要填写 viewKey/priority。</summary>
        public bool TryPlayShot(
            ESCameraDefinitionReference definition,
            Transform subject,
            out ESCameraLease lease)
        {
            return TryPlayShot(definition, subject, null, 0, out lease, out _);
        }

        public bool TryPlayShot(
            ESCameraDefinitionReference definition,
            Transform subject,
            Transform lookAt,
            out ESCameraLease lease)
        {
            return TryPlayShot(definition, subject, lookAt, 0, out lease, out _);
        }

        /// <summary>仅在确有竞品镜头时使用相对默认优先级的偏移，避免裸优先级散落。</summary>
        public bool TryPlayShot(
            ESCameraDefinitionReference definition,
            Transform subject,
            Transform lookAt,
            int priorityOffset,
            out ESCameraLease lease,
            out ESCameraFailure failure)
        {
            lease = ESCameraLease.Invalid;
            failure = ESCameraFailure.None;
            return host != null && host.TryScopePlayShot(
                this, definition, subject, lookAt, priorityOffset, out lease, out failure);
        }

        public bool TryPlayModifier(
            ESCameraModifier modifier,
            out ESCameraLease lease)
        {
            return TryPlayModifier(modifier, default, 0, out lease, out _);
        }

        public bool TryPlayModifier(
            ESCameraModifier modifier,
            ESCameraDefinitionReference compatibleDefinition,
            int priorityOffset,
            out ESCameraLease lease,
            out ESCameraFailure failure)
        {
            lease = ESCameraLease.Invalid;
            failure = ESCameraFailure.None;
            return host != null && host.TryScopePlayModifier(
                this, modifier, compatibleDefinition, priorityOffset, out lease, out failure);
        }

        public bool TryUpdateTarget(
            ESCameraLease lease,
            Transform subject,
            Transform lookAt = null)
        {
            return TryUpdateTarget(lease, subject, lookAt, out _);
        }

        public bool TryUpdateTarget(
            ESCameraLease lease,
            Transform subject,
            Transform lookAt,
            out ESCameraFailure failure)
        {
            failure = ESCameraFailure.None;
            return host != null && host.TryScopeUpdateTarget(this, lease, subject, lookAt, out failure);
        }

        public bool TrySetLook(Vector2 lookInput)
        {
            return TrySetLook(lookInput, out _);
        }

        public bool TrySetLook(Vector2 lookInput, out ESCameraFailure failure)
        {
            failure = ESCameraFailure.None;
            return host != null && host.TryScopeSetLook(this, lookInput, out failure);
        }

        public bool TryStop(ESCameraLease lease)
        {
            return host != null && host.TryReleaseScopeLease(this, lease);
        }

        public void Dispose()
        {
            if (released)
                return;

            released = true;
            host?.DisposeScope(this);
        }

        internal bool Matches(ESCameraModule expectedHost, int expectedGeneration)
        {
            return !released && ReferenceEquals(host, expectedHost) && generation == expectedGeneration;
        }

        internal bool Owns(ESCameraLease lease)
        {
            for (int i = 0; i < leases.Count; i++)
                if (leases[i] == lease)
                    return true;
            return false;
        }

        internal bool HasLeases => leases.Count > 0;

        internal void Track(ESCameraLease lease)
        {
            if (lease.IsValid)
                leases.Add(lease);
        }

        internal void Untrack(ESCameraLease lease)
        {
            for (int i = leases.Count - 1; i >= 0; i--)
                if (leases[i] == lease)
                    leases.RemoveAt(i);
        }

        internal void ReleaseAll(ESCameraModule expectedHost)
        {
            for (int i = leases.Count - 1; i >= 0; i--)
                expectedHost.Release(leases[i]);
            leases.Clear();
        }

        internal bool IsStoryAuthority(ESStoryInstance instance)
        {
            return ReferenceEquals(storyAuthority, instance);
        }

        internal ESStoryInstance StoryAuthority => storyAuthority;

        internal bool TrySetLook(ESCameraModule expectedHost, Vector2 lookInput)
        {
            for (int i = leases.Count - 1; i >= 0; i--)
                if (expectedHost.TrySetLook(leases[i], lookInput))
                    return true;
            return false;
        }

        internal void InvalidateFromHost()
        {
            if (released)
                return;

            released = true;
            host?.DisposeScope(this);
        }

        internal UnityEngine.Object RequestOwner => requestOwner;
        internal int Generation => generation;
    }

    /// <summary>不暴露 Scope 生命周期的三行式 Demo/玩家入口。</summary>
    public static class ESCameraFacade
    {
        public static bool TryPlayLocalShot(
            ESCameraDefinitionReference definition,
            Transform subject,
            out ESCameraLease lease)
        {
            return TryPlayLocalShot(definition, subject, null, out lease, out _);
        }

        public static bool TryPlayLocalShot(
            ESCameraDefinitionReference definition,
            Transform subject,
            Transform lookAt,
            out ESCameraLease lease,
            out ESCameraFailure failure)
        {
            lease = ESCameraLease.Invalid;
            failure = ESCameraFailure.None;
            ESCameraModule camera = ESGameManager.Camera;
            if (camera == null)
            {
                failure = new ESCameraFailure(ESCameraFailureReason.ModuleUnavailable);
                return false;
            }

            if (!camera.TryOpenLocalScope(out ESCameraControlScope scope, out failure))
                return false;

            bool accepted = scope.TryPlayShot(definition, subject, lookAt, out lease, out failure);
            if (!accepted)
                scope.Dispose();
            return accepted;
        }
    }

    /// <summary>
    /// 统一的实体相机挂点解析。正式运行、技能和编辑器预览都使用同一优先级：
    /// 稳定 CameraTarget 别名，其次 DefaultTransformKey.Camera；Aim 可选。
    /// </summary>
    public static class ESCameraTargetResolver
    {
        public static bool TryResolve(
            Entity entity,
            out Transform follow,
            out Transform lookAt)
        {
            follow = null;
            lookAt = null;
            EntityTransformMapping mapping = entity != null ? entity.TransformMapping : null;
            if (mapping == null)
                return false;

            follow = mapping.Resolve("CameraTarget");
            if (follow == null)
                follow = mapping.Resolve(DefaultTransformKey.Camera);
            lookAt = mapping.Resolve("CameraAimTarget");
            return IsUsable(follow) && (lookAt == null || IsUsable(lookAt));
        }

        private static bool IsUsable(Transform target)
        {
            return target != null && target.gameObject != null && target.gameObject.activeInHierarchy;
        }
    }
}
