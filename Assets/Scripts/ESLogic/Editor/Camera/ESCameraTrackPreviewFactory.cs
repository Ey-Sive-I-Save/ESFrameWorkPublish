using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 域加载时安装相机轨道预览实现。Runtime 只看到 ICameraTrackPreviewFactory；所有
    /// UnityEditor、Cinemachine 编辑器预览和对象生命周期都严格留在 Editor/Camera。
    /// </summary>
    /// <summary>相机轨道预览只在 ES AssemblyStream 阶段安装，不占用普通 InitializeOnLoad。</summary>
    public sealed class ESCameraTrackPreviewBootstrap : EditorInvoker_Level0
    {
        private readonly ESCameraTrackPreviewFactory factory = new ESCameraTrackPreviewFactory();
        private static ESCameraTrackPreviewFactory activeFactory;

        public override void InitInvoke()
        {
            activeFactory = factory;
            ESCameraTrackPreviewFactoryRegistry.Install(factory);
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
        }

        private static void BeforeAssemblyReload()
        {
            ESCameraTrackPreviewFactoryRegistry.Clear(activeFactory);
            activeFactory = null;
        }
    }

    internal sealed class ESCameraTrackPreviewFactory : ICameraTrackPreviewFactory
    {
        public List<IEditorTimeSampler> CreateSamplers(in ESCameraTrackPreviewRequest request)
        {
            var result = new List<IEditorTimeSampler>();
            if (request.track == null)
                return result;

            if (!ESCameraTrackPreviewCatalogResolver.TryResolveSharedCatalogs(
                    request.clips,
                    out ESCameraViewDefinitionCatalog definitionCatalog,
                    out ESCameraRigCatalog rigCatalog,
                    out string error))
            {
                Debug.LogWarning("[ESCamera Preview] " + error);
                result.Add(new TrackEditorSampler(request.track, request.editorTarget, request.ownsEditorTarget));
                return result;
            }

            ESCameraTrackPreviewSession session = null;
            try
            {
                session = new ESCameraTrackPreviewSession(
                    request.track.DisplayName,
                    request.editorTarget,
                    definitionCatalog,
                    rigCatalog);
                if (!session.IsReady)
                    throw new InvalidOperationException("独立 Preview View 未能初始化。");

                result.Add(new CameraTrackEditorSampler(
                    request.track,
                    request.editorTarget,
                    request.ownsEditorTarget,
                    session));

                if (request.clips == null)
                    return result;

                for (int i = 0; i < request.clips.Count; i++)
                {
                    ESCameraTrackPreviewClip clip = request.clips[i];
                    if (!clip.IsValid)
                        continue;

                    var sampler = new CameraClipEditorSampler(session, clip);
                    result.Add(new TrackClipEditorSampler(clip.sourceClip, sampler));
                }

                return result;
            }
            catch
            {
                session?.Dispose();
                if (request.ownsEditorTarget
                    && request.editorTarget is IPoolableAuto poolable
                    && !poolable.IsRecycled)
                {
                    try
                    {
                        poolable.TryAutoPushedToPool();
                    }
                    catch (Exception cleanupException)
                    {
                        Debug.LogException(new InvalidOperationException(
                            "Camera Track Preview 构建失败，owned EditorTarget 回收失败。",
                            cleanupException));
                    }
                }
                throw;
            }
        }
    }

    /// <summary>
    /// 一个 Skill Camera Track 对应一个独立的编辑器 Preview View。它不调用
    /// ESGameManager.Camera，不复用 PlayMode SceneBinding，也不会写入制作场景的 VCam。
    /// </summary>
    public sealed class ESCameraTrackPreviewSession : IDisposable
    {
        private static int nextSessionId;

        private readonly ESEditorPreviewRenderContext renderContext;
        private readonly GameObject rigRootObject;
        private readonly ESCameraPreviewView previewView;
        private readonly ESRuntimeTargetPack target;
        private readonly HashSet<ESCameraLease> leases = new HashSet<ESCameraLease>();
        private readonly HashSet<string> reportedTargetErrors = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<int> visibleTargetInstanceIds = new HashSet<int>();
        private bool disposed;

        internal ESCameraTrackPreviewSession(
            string trackName,
            ESRuntimeTargetPack target,
            ESCameraViewDefinitionCatalog definitionCatalog,
            ESCameraRigCatalog rigCatalog,
            ESEditorPreviewEnhancerSet enhancerSet = ESEditorPreviewEnhancerSet.HighQualityLighting)
        {
            this.target = target;
            int sessionId = ++nextSessionId;
            string owner = "ES Camera Track Preview " + sessionId;
            renderContext = new ESEditorPreviewRenderContext(
                owner,
                ESEditorPreviewSceneMode.HiddenObjectsInActiveScene,
                ESEditorPreviewUtility.DefaultPreviewLayer,
                enhancerSet);
            try
            {
                renderContext.Ensure();
                if (!renderContext.IsReady || renderContext.Camera == null)
                    return;

                Camera outputCamera = renderContext.Camera;
                CinemachineBrain brain = outputCamera.GetComponent<CinemachineBrain>();
                if (brain == null)
                    brain = outputCamera.gameObject.AddComponent<CinemachineBrain>();

                // 预览只由 Sample() 显式 ManualUpdate，防止 Editor Update 与采样顺序争抢。
                brain.enabled = false;
                rigRootObject = ESEditorPreviewUtility.CreatePreviewGameObject(
                    string.IsNullOrWhiteSpace(trackName) ? "ES Camera Preview Rigs" : trackName + " Camera Preview Rigs");
                if (!ESEditorPreviewUtility.TryMarkPreviewObject(
                        rigRootObject, owner, "Camera track preview rig root.", out string markerStatus))
                    throw new InvalidOperationException("相机轨道预览根对象未能登记公共预览所有权：" + markerStatus);
                rigRootObject.transform.SetParent(outputCamera.transform, false);
                previewView = new ESCameraPreviewView(
                    new ESCameraViewId("EditorTrackPreview." + sessionId),
                    sessionId,
                    outputCamera,
                    brain,
                    definitionCatalog,
                    rigCatalog,
                    rigRootObject.transform);

                if (previewView.IsReady)
                    ESEditorPreviewLifecycleHub.RegisterScope(this);
            }
            catch
            {
                // The factory cannot receive a session reference when this
                // constructor throws. Roll back every resource created so far.
                try { ESEditorPreviewLifecycleHub.UnregisterScope(this); }
                catch (Exception cleanupException) { Debug.LogException(cleanupException); }
                try { previewView?.Dispose(); }
                catch (Exception cleanupException) { Debug.LogException(cleanupException); }
                try { ESEditorPreviewUtility.DestroyObject(rigRootObject); }
                catch (Exception cleanupException) { Debug.LogException(cleanupException); }
                try { renderContext?.Dispose(); }
                catch (Exception cleanupException) { Debug.LogException(cleanupException); }
                throw;
            }
        }

        public bool IsReady => !disposed && previewView != null && previewView.IsReady;

        public ESCameraLease Push(in ESCameraRequest request)
        {
            if (!IsReady)
                return ESCameraLease.Invalid;

            ESCameraLease lease = previewView.Push(request);
            if (lease.IsValid)
                leases.Add(lease);
            return lease;
        }

        public bool Release(ESCameraLease lease)
        {
            if (!lease.IsValid)
                return false;

            leases.Remove(lease);
            return IsReady && previewView.Release(lease);
        }

        public bool TryBuildShotRequest(in ESCameraTrackPreviewClip clip, out ESCameraRequest request)
        {
            request = default;
            if (!IsReady || !clip.IsValid)
                return false;

            Entity entity = clip.targetSource == ESCameraTrackPreviewTargetSource.MainTarget
                ? target?.GetEntityMainTarget()
                : target?.GetUserEntity();
            if (entity == null)
            {
                ReportTargetError(clip.definition.ToString(), "预览目标为空，无法解析 CameraTarget。");
                return false;
            }

            EnsureTargetLayersVisible(entity);

            EntityTransformMapping mapping = entity.TransformMapping;
            Transform follow = mapping != null ? mapping.Resolve("CameraTarget") : null;
            if (follow == null && mapping != null)
                follow = mapping.Resolve(DefaultTransformKey.Camera);
            if (follow == null)
                follow = entity.transform;

            Transform lookAt = mapping != null ? mapping.Resolve("CameraAimTarget") : null;
            if (lookAt == null)
                lookAt = follow;

            request = ESCameraRequest.CreateShot(
                previewView.ViewId,
                clip.definition,
                clip.priority,
                rigRootObject,
                follow,
                lookAt);
            return true;
        }

        public void Sample()
        {
            if (IsReady)
                previewView.Sample();
        }

        /// <summary>
        /// 供未来 TrackViewWindow 的相机面板直接绘制。RenderContext 保持 CM 已解算的
        /// Camera Transform，不会再套一层 Inspector 自由预览相机。
        /// </summary>
        public bool RenderGUI(Rect rect, ESEditorPreviewRenderOptions options)
        {
            if (!IsReady)
                return false;

            Sample();
            return renderContext.RenderCurrentCameraGUI(rect, options);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            ESEditorPreviewLifecycleHub.UnregisterScope(this);
            foreach (ESCameraLease lease in leases)
            {
                try { previewView?.Release(lease); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            leases.Clear();
            try { previewView?.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception); }
            try { ESEditorPreviewUtility.DestroyObject(rigRootObject); }
            catch (Exception exception) { Debug.LogException(exception); }
            try { renderContext?.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception); }
            reportedTargetErrors.Clear();
            visibleTargetInstanceIds.Clear();
        }

        private void ReportTargetError(string definitionKey, string detail)
        {
            string key = string.IsNullOrWhiteSpace(definitionKey) ? "<empty>" : definitionKey;
            if (reportedTargetErrors.Add(key))
                Debug.LogWarning("[ESCamera Preview] 相机定义 '" + key + "' " + detail);
        }

        private void EnsureTargetLayersVisible(Entity entity)
        {
            if (entity == null || renderContext == null || renderContext.Camera == null)
                return;

            int instanceId = entity.GetInstanceID();
            if (!visibleTargetInstanceIds.Add(instanceId))
                return;

            Transform[] transforms = entity.GetComponentsInChildren<Transform>(true);
            int mask = renderContext.Camera.cullingMask;
            for (int i = 0; i < transforms.Length; i++)
                mask |= 1 << transforms[i].gameObject.layer;

            renderContext.Camera.cullingMask = mask;
        }
    }

    /// <summary>轨道级 Sampler 持有 Session，负责停止时释放 View、Rig、Brain 和 Target。</summary>
    public sealed class CameraTrackEditorSampler : TrackEditorSampler
    {
        private readonly ESCameraTrackPreviewSession session;

        public ESCameraTrackPreviewSession Session => session;

        internal CameraTrackEditorSampler(
            ITrackItem track,
            ESRuntimeTargetPack editorTarget,
            bool ownsEditorTarget,
            ESCameraTrackPreviewSession session)
            : base(track, editorTarget, ownsEditorTarget)
        {
            this.session = session;
        }

        public override void OnEditorPreviewStart()
        {
            session?.Sample();
        }

        public override void SampleTime(float time)
        {
            session?.Sample();
        }

        public override void OnEditorPreviewStop()
        {
            try
            {
                session?.Dispose();
            }
            finally
            {
                base.OnEditorPreviewStop();
            }
        }
    }

    internal sealed class CameraClipEditorSampler : EditorTimeSamplerBase
    {
        private readonly ESCameraTrackPreviewSession session;
        private readonly ESCameraTrackPreviewClip clip;
        private ESCameraLease lease;

        public CameraClipEditorSampler(ESCameraTrackPreviewSession session, ESCameraTrackPreviewClip clip)
        {
            this.session = session;
            this.clip = clip;
        }

        public override void SampleTime(float time)
        {
            bool active = clip.sourceClip != null
                          && time >= clip.sourceClip.StartTime
                          && time < clip.sourceClip.StartTime + clip.sourceClip.DurationTime;
            if (!active)
            {
                ReleaseLease();
                session?.Sample();
                return;
            }

            if (!lease.IsValid && session != null && session.TryBuildShotRequest(clip, out ESCameraRequest request))
                lease = session.Push(request);

            session?.Sample();
        }

        public override void OnEditorPreviewStop()
        {
            ReleaseLease();
        }

        private void ReleaseLease()
        {
            if (!lease.IsValid)
                return;

            session?.Release(lease);
            lease = ESCameraLease.Invalid;
        }
    }

    internal static class ESCameraTrackPreviewCatalogResolver
    {
        public static bool TryResolveSharedCatalogs(
            IReadOnlyList<ESCameraTrackPreviewClip> clips,
            out ESCameraViewDefinitionCatalog definitionCatalog,
            out ESCameraRigCatalog rigCatalog,
            out string error)
        {
            definitionCatalog = null;
            rigCatalog = null;
            error = string.Empty;
            if (clips == null || clips.Count == 0)
            {
                error = "相机轨道没有可预览的有效片段。";
                return false;
            }

            for (int i = 0; i < clips.Count; i++)
            {
                ESCameraTrackPreviewClip clip = clips[i];
                if (!clip.IsValid)
                    continue;

                if (!TryResolveDefinitionCatalog(clip.definition, out ESCameraViewDefinitionCatalog currentDefinitionCatalog, out ESCameraViewDefinition definition, out error)
                    || !TryResolveRigCatalog(definition.rigKey, out ESCameraRigCatalog currentRigCatalog, out error))
                {
                    return false;
                }

                if (definitionCatalog == null)
                {
                    definitionCatalog = currentDefinitionCatalog;
                    rigCatalog = currentRigCatalog;
                    continue;
                }

                if (definitionCatalog != currentDefinitionCatalog || rigCatalog != currentRigCatalog)
                {
                    error = "同一相机轨道的片段必须来自同一对 DefinitionCatalog/RigCatalog；请拆分轨道或统一内容目录。";
                    return false;
                }
            }

            if (definitionCatalog != null && rigCatalog != null)
                return true;

            error = "相机轨道没有可预览的有效 Definition。";
            return false;
        }

        private static bool TryResolveDefinitionCatalog(
            ESCameraDefinitionReference definitionReference,
            out ESCameraViewDefinitionCatalog resolvedCatalog,
            out ESCameraViewDefinition resolvedDefinition,
            out string error)
        {
            resolvedCatalog = null;
            resolvedDefinition = null;
            error = string.Empty;
            List<string> paths = FindAssetPaths<ESCameraViewDefinitionCatalog>();
            for (int i = 0; i < paths.Count; i++)
            {
                ESCameraViewDefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<ESCameraViewDefinitionCatalog>(paths[i]);
                if (catalog == null
                    || !catalog.TryResolve(definitionReference, out ESCameraDefinitionRuntimeHandle handle)
                    || !catalog.TryGet(handle, out ESCameraViewDefinition definition))
                    continue;

                if (resolvedCatalog != null && resolvedCatalog != catalog)
                {
                    error = "Definition '" + definitionReference + "' 同时存在于多个 DefinitionCatalog，编辑器预览拒绝猜测来源。";
                    return false;
                }

                resolvedCatalog = catalog;
                resolvedDefinition = definition;
            }

            if (resolvedCatalog != null)
                return true;

            error = "找不到 Definition '" + definitionReference + "' 对应的 ESCameraViewDefinitionCatalog。";
            return false;
        }

        private static bool TryResolveRigCatalog(string rigKey, out ESCameraRigCatalog resolvedCatalog, out string error)
        {
            resolvedCatalog = null;
            error = string.Empty;
            List<string> paths = FindAssetPaths<ESCameraRigCatalog>();
            for (int i = 0; i < paths.Count; i++)
            {
                ESCameraRigCatalog catalog = AssetDatabase.LoadAssetAtPath<ESCameraRigCatalog>(paths[i]);
                if (catalog == null || !catalog.TryGetPrefab(rigKey, out _))
                    continue;

                if (resolvedCatalog != null && resolvedCatalog != catalog)
                {
                    error = "RigKey '" + rigKey + "' 同时存在于多个 RigCatalog，编辑器预览拒绝猜测来源。";
                    return false;
                }

                resolvedCatalog = catalog;
            }

            if (resolvedCatalog != null)
                return true;

            error = "找不到 RigKey '" + rigKey + "' 对应的 ESCameraRigCatalog。";
            return false;
        }

        private static List<string> FindAssetPaths<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            var paths = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
                paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }
    }
}
