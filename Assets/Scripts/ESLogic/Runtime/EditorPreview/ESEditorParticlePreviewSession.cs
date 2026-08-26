#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;

namespace ES
{
    /// <summary>
    /// ParticleSystem 编辑器预览的公共会话。负责完整根对象复制、引用重映射、
    /// 确定性采样、播放时钟、预算和生命周期；业务窗口只负责选源、改参数和绘制 UI。
    /// </summary>
    public sealed class ESEditorParticlePreviewSession : IDisposable
    {
        public const int DefaultMaximumParticleSystems = 128;
        public const int DefaultMaximumParticleCapacity = 250000;
        public const float DefaultSampleRate = 30f;
        public const float MinimumDuration = 0.05f;
        public const float MaximumDuration = 120f;

        private readonly string owner;
        private readonly int maximumParticleSystems;
        private readonly int maximumParticleCapacity;
        private readonly List<GameObject> sourceRoots = new List<GameObject>(8);
        private readonly List<ESEditorPreviewModelHandle> modelHandles = new List<ESEditorPreviewModelHandle>(8);
        private readonly List<GameObject> previewRoots = new List<GameObject>(8);
        private readonly List<ParticleSystem> sourceSystems = new List<ParticleSystem>(32);
        private readonly List<ParticleSystem> previewSystems = new List<ParticleSystem>(32);
        private readonly List<ParticleSystem> simulationRoots = new List<ParticleSystem>(16);
        private readonly List<ParticleSystemRenderer> previewRenderers = new List<ParticleSystemRenderer>(32);
        private readonly Dictionary<int, UnityEngine.Object> sourceToPreview = new Dictionary<int, UnityEngine.Object>(128);
        private readonly HashSet<int> controlledSourceIds = new HashSet<int>();
        private readonly HashSet<int> drivenSourceIds = new HashSet<int>();
        private readonly Action repaintRequested;

        private ESEditorPreviewRenderContext renderContext;
        private Action<ParticleSystem, ParticleSystem, bool> configurePreviewSystem;
        private bool disposed;
        private bool playing;
        private bool loop = true;
        private float currentTime;
        private float duration = 10f;
        private float playbackSpeed = 1f;
        private float requestedSampleRate;
        private float sampleInterval;
        private float pendingSimulationTime;
        private double lastEditorTime;
        private int randomSeed = 12345;
        private int unresolvedReferenceCount;
        private int skippedComponentCount;
        private long sourceParticleCapacity;
        private string lastError = string.Empty;
        private Vector3 sourceWorldAnchor;
        private static readonly ProfilerMarker SimulationMarker = new ProfilerMarker("ES.Editor.ParticlePreview.Simulate");

        public ESEditorParticlePreviewSession(
            string owner,
            Action repaintRequested = null,
            int maximumParticleSystems = DefaultMaximumParticleSystems,
            float sampleRate = DefaultSampleRate,
            int maximumParticleCapacity = DefaultMaximumParticleCapacity)
        {
            this.owner = string.IsNullOrWhiteSpace(owner) ? "ES Particle Preview" : owner.Trim();
            this.repaintRequested = repaintRequested;
            this.maximumParticleSystems = Mathf.Max(1, maximumParticleSystems);
            this.maximumParticleCapacity = Mathf.Max(1, maximumParticleCapacity);
            requestedSampleRate = Mathf.Clamp(sampleRate, 1f, 120f);
            sampleInterval = 1f / requestedSampleRate;
            ESEditorPreviewLifecycleHub.RegisterScope(this);
        }

        public ESEditorPreviewRenderContext RenderContext => renderContext;
        public bool IsReady => !disposed && renderContext != null && renderContext.IsReady && previewSystems.Count > 0;
        public bool IsPlaying => IsReady && playing;
        public bool IsDisposed => disposed;
        public int ParticleSystemCount => previewSystems.Count;
        public int SourceRootCount => sourceRoots.Count;
        public int SimulationRootCount => simulationRoots.Count;
        public int UnresolvedReferenceCount => unresolvedReferenceCount;
        public int SkippedComponentCount => skippedComponentCount;
        public int MaximumParticleSystems => maximumParticleSystems;
        public int MaximumParticleCapacity => maximumParticleCapacity;
        public long SourceParticleCapacity => sourceParticleCapacity;
        public float EffectiveSampleRate => 1f / sampleInterval;
        public float CurrentTime => currentTime;
        public float Duration => duration;
        public string LastError => lastError;
        public Vector3 SourceWorldAnchor => sourceWorldAnchor;

        public Vector3 SourceWorldToPreviewLocalPoint(Vector3 sourceWorldPoint)
        {
            return sourceWorldPoint - sourceWorldAnchor;
        }

        public Vector3 PreviewLocalToSourceWorldPoint(Vector3 previewLocalPoint)
        {
            return sourceWorldAnchor + previewLocalPoint;
        }

        public bool Rebuild(
            IList<GameObject> requestedRoots,
            IList<ParticleSystem> controlledSystems,
            Action<ParticleSystem, ParticleSystem, bool> configureSystem,
            int deterministicSeed,
            float playbackDuration,
            bool shouldLoop,
            out string error)
        {
            ThrowIfDisposed();
            ReleasePreviewContent();
            lastError = string.Empty;
            error = string.Empty;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                error = "粒子编辑器预览仅允许在 EditMode 创建。请退出 PlayMode 后重试。";
                lastError = error;
                return false;
            }

            if (controlledSystems == null || controlledSystems.Count == 0)
            {
                error = "没有可预览的 ParticleSystem。";
                lastError = error;
                return false;
            }

            configurePreviewSystem = configureSystem;
            randomSeed = deterministicSeed;
            duration = Mathf.Clamp(playbackDuration, MinimumDuration, MaximumDuration);
            loop = shouldLoop;

            for (int i = 0; i < controlledSystems.Count; i++)
            {
                ParticleSystem source = controlledSystems[i];
                if (source != null)
                    controlledSourceIds.Add(source.GetInstanceID());
            }

            if (requestedRoots != null)
            {
                for (int i = 0; i < requestedRoots.Count; i++)
                    AddMinimalSourceRoot(requestedRoots[i]);
            }

            for (int i = 0; i < controlledSystems.Count; i++)
            {
                ParticleSystem source = controlledSystems[i];
                if (source != null && !IsInsideAnySourceRoot(source.transform))
                    AddMinimalSourceRoot(source.gameObject);
            }

            ExpandExternalSubEmitterRoots();
            CollectSourceSystems();
            if (sourceSystems.Count == 0)
            {
                error = "预览根对象中没有 ParticleSystem。";
                lastError = error;
                ReleasePreviewContent();
                return false;
            }

            if (sourceSystems.Count > maximumParticleSystems)
            {
                error = "粒子预览包含 " + sourceSystems.Count + " 个系统，超过硬上限 "
                    + maximumParticleSystems + "。请缩小选区或拆分效果后再预览。";
                lastError = error;
                ReleasePreviewContent();
                return false;
            }

            sourceParticleCapacity = 0L;
            for (int i = 0; i < sourceSystems.Count; i++)
                sourceParticleCapacity += Mathf.Max(0, sourceSystems[i].main.maxParticles);
            if (sourceParticleCapacity > maximumParticleCapacity)
            {
                error = "粒子预览声明容量 " + sourceParticleCapacity.ToString("N0")
                    + "，超过硬预算 " + maximumParticleCapacity.ToString("N0")
                    + "。请降低 Max Particles、拆分效果或使用专用性能验收场景。";
                lastError = error;
                ReleasePreviewContent();
                return false;
            }

            RefreshBudgetedSampleRate();

            try
            {
                renderContext = new ESEditorPreviewRenderContext(owner, ESEditorPreviewSceneMode.PreviewScene);
                renderContext.Ensure();
                if (renderContext.Camera != null)
                {
                    renderContext.Camera.fieldOfView = 35f;
                    renderContext.Camera.backgroundColor = new Color(0.16f, 0.18f, 0.21f, 1f);
                }

                CloneRootsAndBuildMap();
                RemapParticleObjectReferences();
                BuildSimulationRoots();
                PrepareFromBeginning();
                ActivatePreviewRoots();
                repaintRequested?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                lastError = error;
                ReleasePreviewContent();
                return false;
            }
        }

        public bool MatchesControlledSources(IList<ParticleSystem> systems)
        {
            if (!IsReady || systems == null || systems.Count != controlledSourceIds.Count)
                return false;

            for (int i = 0; i < systems.Count; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null || !controlledSourceIds.Contains(system.GetInstanceID()))
                    return false;
            }

            return true;
        }

        public bool TryGetPreviewSystem(ParticleSystem source, out ParticleSystem preview)
        {
            preview = null;
            if (source == null)
                return false;

            if (!sourceToPreview.TryGetValue(source.GetInstanceID(), out UnityEngine.Object mapped))
                return false;

            preview = mapped as ParticleSystem;
            return preview != null;
        }

        public void SetPlayback(float playbackDuration, bool shouldLoop, float speed = 1f)
        {
            duration = Mathf.Clamp(playbackDuration, MinimumDuration, MaximumDuration);
            loop = shouldLoop;
            playbackSpeed = Mathf.Clamp(speed, 0.05f, 8f);
            if (currentTime > duration)
                Seek(duration);
        }

        public void SetSampleRate(float samplesPerSecond)
        {
            requestedSampleRate = Mathf.Clamp(samplesPerSecond, 1f, 120f);
            RefreshBudgetedSampleRate();
        }

        public void SetRandomSeed(int deterministicSeed)
        {
            randomSeed = deterministicSeed;
        }

        public void PlayFromBeginning()
        {
            EnsureReady();
            PrepareFromBeginning();
            ActivatePreviewRoots();
            // Camera.Render 在启动按钮同一帧执行时，ParticleSystem 仍处于严格的 t=0，
            // 对 rate-over-time 和延迟发射效果会得到完全空的画面。先暖机一个采样步，
            // 不改变公开时间轴的 currentTime，只保证首帧已有可渲染粒子。
            WarmStartVisibleFrame();
            playing = true;
            pendingSimulationTime = 0f;
            lastEditorTime = EditorApplication.timeSinceStartup;
            RegisterUpdate();
            repaintRequested?.Invoke();
        }

        public void Pause()
        {
            if (disposed)
                return;

            playing = false;
            UnregisterUpdate();
            for (int i = 0; i < simulationRoots.Count; i++)
                if (simulationRoots[i] != null) simulationRoots[i].Pause(true);
            repaintRequested?.Invoke();
        }

        public void Resume()
        {
            EnsureReady();
            if (playing)
                return;

            if (currentTime >= duration - sampleInterval * 0.5f)
            {
                PrepareFromBeginning();
                ActivatePreviewRoots();
                WarmStartVisibleFrame();
            }

            playing = true;
            pendingSimulationTime = 0f;
            lastEditorTime = EditorApplication.timeSinceStartup;
            RegisterUpdate();
            repaintRequested?.Invoke();
        }

        public void Stop()
        {
            if (disposed)
                return;

            playing = false;
            UnregisterUpdate();
            if (IsReady)
                PrepareFromBeginning();
            repaintRequested?.Invoke();
        }

        public void Seek(float time)
        {
            EnsureReady();
            bool resumeAfterSeek = playing;
            UnregisterUpdate();
            playing = false;
            PrepareFromBeginning();
            currentTime = Mathf.Clamp(time, 0f, duration);
            if (currentTime > 0f)
            {
                for (int i = 0; i < simulationRoots.Count; i++)
                {
                    ParticleSystem system = simulationRoots[i];
                    if (system != null)
                        system.Simulate(currentTime, true, true, true);
                }
            }

            for (int i = 0; i < simulationRoots.Count; i++)
                if (simulationRoots[i] != null) simulationRoots[i].Pause(true);

            if (resumeAfterSeek)
                Resume();
            else
                repaintRequested?.Invoke();
        }

        public bool TryCalculateBounds(out Bounds combinedBounds)
        {
            combinedBounds = default;
            bool hasBounds = false;
            for (int i = 0; i < previewRenderers.Count; i++)
            {
                ParticleSystemRenderer renderer = previewRenderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                Bounds bounds = renderer.bounds;
                if (!IsFinite(bounds.center) || !IsFinite(bounds.extents) || bounds.extents.sqrMagnitude <= 0.0001f)
                    continue;

                if (!hasBounds)
                {
                    combinedBounds = bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(bounds);
                }
            }

            return hasBounds;
        }

        /// <summary>
        /// 对一次性爆发、延迟发射和非循环特效取代表性边界。仅用于首次构图，
        /// 会恢复调用前的时间与播放状态，不把取景采样变成运行时播放。
        /// </summary>
        public bool TryCalculateRepresentativeBounds(out Bounds combinedBounds)
        {
            combinedBounds = default;
            if (!IsReady)
                return false;

            bool wasPlaying = playing;
            float savedTime = currentTime;
            if (wasPlaying)
                Pause();

            bool hasBounds = false;
            try
            {
                float[] sampleTimes =
                {
                    0f,
                    Mathf.Min(duration, 0.1f),
                    Mathf.Min(duration, 0.25f),
                    Mathf.Min(duration, 0.5f),
                    Mathf.Min(duration, 1f),
                    Mathf.Min(duration, duration * 0.5f)
                };
                for (int i = 0; i < sampleTimes.Length; i++)
                {
                    Seek(sampleTimes[i]);
                    if (!TryCalculateBounds(out Bounds sample))
                        continue;
                    if (!hasBounds)
                    {
                        combinedBounds = sample;
                        hasBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(sample);
                    }
                }
            }
            finally
            {
                Seek(savedTime);
                if (wasPlaying)
                    Resume();
            }
            return hasBounds;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            ESEditorPreviewLifecycleHub.UnregisterScope(this);
            ReleasePreviewContent();
        }

        private void CloneRootsAndBuildMap()
        {
            sourceWorldAnchor = sourceRoots.Count > 0 && sourceRoots[0] != null
                ? sourceRoots[0].transform.position
                : Vector3.zero;

            for (int i = 0; i < sourceRoots.Count; i++)
            {
                GameObject sourceRoot = sourceRoots[i];
                if (sourceRoot == null)
                    continue;

                GameObject safeClone = CreateSafeCloneRoot(sourceRoot, out Transform previewContentRoot);
                ESEditorPreviewModelHandle handle = null;
                try
                {
                    handle = renderContext.AdoptModelGroup(
                        sourceRoot,
                        safeClone,
                        "__ESParticlePreview__" + sourceRoot.name,
                        samplingTarget: false,
                        copyRendererState: false,
                        disableRuntimeBehaviours: true,
                        ensureRenderersEnabled: false,
                        activateInstance: false,
                        moveToGroupOrigin: false);
                }
                catch
                {
                    if (handle == null && safeClone != null)
                        ESEditorPreviewUtility.DestroyObject(safeClone);
                    throw;
                }
                GameObject previewRoot = handle?.Instance;
                if (previewRoot == null)
                {
                    if (safeClone != null)
                        ESEditorPreviewUtility.DestroyObject(safeClone);
                    throw new InvalidOperationException("无法复制粒子预览根对象：" + sourceRoot.name);
                }

                modelHandles.Add(handle);
                previewRoots.Add(previewRoot);
                previewRoot.transform.position += renderContext.GroupOrigin - sourceWorldAnchor;
                MapHierarchy(sourceRoot.transform, previewContentRoot);
            }

            for (int i = 0; i < sourceSystems.Count; i++)
            {
                ParticleSystem source = sourceSystems[i];
                if (!TryGetMappedObject(source, out ParticleSystem preview))
                    throw new InvalidOperationException("粒子副本映射失败：" + source.name);

                previewSystems.Add(preview);
                ParticleSystemRenderer renderer = preview.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                    previewRenderers.Add(renderer);
            }

            foreach (int controlledId in controlledSourceIds)
            {
                if (!sourceToPreview.ContainsKey(controlledId))
                    throw new InvalidOperationException("受控粒子不在任何预览根对象内。InstanceId=" + controlledId);
            }
        }

        private void MapHierarchy(Transform source, Transform preview)
        {
            if (source == null || preview == null)
                return;

            sourceToPreview[source.gameObject.GetInstanceID()] = preview.gameObject;
            sourceToPreview[source.GetInstanceID()] = preview;
            MapComponents(source.gameObject, preview.gameObject);

            int childCount = Mathf.Min(source.childCount, preview.childCount);
            for (int i = 0; i < childCount; i++)
                MapHierarchy(source.GetChild(i), preview.GetChild(i));
        }

        private void MapComponents(GameObject source, GameObject preview)
        {
            Component[] sourceComponents = source.GetComponents<Component>();
            Component[] previewComponents = preview.GetComponents<Component>();
            for (int i = 0; i < sourceComponents.Length; i++)
            {
                Component sourceComponent = sourceComponents[i];
                if (sourceComponent == null || sourceComponent is Transform)
                    continue;

                Type type = sourceComponent.GetType();
                int ordinal = 0;
                for (int before = 0; before < i; before++)
                    if (sourceComponents[before] != null && sourceComponents[before].GetType() == type) ordinal++;

                int previewOrdinal = 0;
                for (int candidateIndex = 0; candidateIndex < previewComponents.Length; candidateIndex++)
                {
                    Component candidate = previewComponents[candidateIndex];
                    if (candidate == null || candidate.GetType() != type)
                        continue;
                    if (previewOrdinal++ != ordinal)
                        continue;

                    sourceToPreview[sourceComponent.GetInstanceID()] = candidate;
                    break;
                }
            }
        }

        private void RemapParticleObjectReferences()
        {
            unresolvedReferenceCount = 0;
            drivenSourceIds.Clear();
            for (int i = 0; i < sourceSystems.Count; i++)
            {
                ParticleSystem source = sourceSystems[i];
                ParticleSystem preview = previewSystems[i];
                RemapMainModule(source, preview);
                RemapShapeModule(source, preview);
                RemapLightsModule(source, preview);
                RemapCollisionModule(source, preview);
                RemapTriggerModule(source, preview);
                RemapSubEmitters(source, preview);
            }
        }

        private void RemapMainModule(ParticleSystem source, ParticleSystem preview)
        {
            ParticleSystem.MainModule sourceMain = source.main;
            if (sourceMain.customSimulationSpace == null)
                return;

            ParticleSystem.MainModule previewMain = preview.main;
            previewMain.customSimulationSpace = ResolveMappedReference(sourceMain.customSimulationSpace);
        }

        private void RemapShapeModule(ParticleSystem source, ParticleSystem preview)
        {
            ParticleSystem.ShapeModule sourceShape = source.shape;
            ParticleSystem.ShapeModule previewShape = preview.shape;
            if (sourceShape.meshRenderer != null)
                previewShape.meshRenderer = ResolveMappedReference(sourceShape.meshRenderer);
            if (sourceShape.skinnedMeshRenderer != null)
                previewShape.skinnedMeshRenderer = ResolveMappedReference(sourceShape.skinnedMeshRenderer);
            if (sourceShape.spriteRenderer != null)
                previewShape.spriteRenderer = ResolveMappedReference(sourceShape.spriteRenderer);
        }

        private void RemapLightsModule(ParticleSystem source, ParticleSystem preview)
        {
            ParticleSystem.LightsModule sourceLights = source.lights;
            if (sourceLights.light == null)
                return;

            ParticleSystem.LightsModule previewLights = preview.lights;
            previewLights.light = ResolveMappedReference(sourceLights.light);
        }

        private void RemapCollisionModule(ParticleSystem source, ParticleSystem preview)
        {
            ParticleSystem.CollisionModule sourceCollision = source.collision;
            ParticleSystem.CollisionModule previewCollision = preview.collision;
            for (int i = 0; i < sourceCollision.planeCount; i++)
                previewCollision.SetPlane(i, ResolveMappedReference(sourceCollision.GetPlane(i)));
        }

        private void RemapTriggerModule(ParticleSystem source, ParticleSystem preview)
        {
            ParticleSystem.TriggerModule sourceTrigger = source.trigger;
            ParticleSystem.TriggerModule previewTrigger = preview.trigger;
            for (int i = 0; i < sourceTrigger.colliderCount; i++)
                previewTrigger.SetCollider(i, ResolveMappedReference(sourceTrigger.GetCollider(i)));
        }

        private void RemapSubEmitters(ParticleSystem source, ParticleSystem preview)
        {
            ParticleSystem.SubEmittersModule sourceSubEmitters = source.subEmitters;
            ParticleSystem.SubEmittersModule previewSubEmitters = preview.subEmitters;
            for (int i = previewSubEmitters.subEmittersCount - 1; i >= 0; i--)
                previewSubEmitters.RemoveSubEmitter(i);

            for (int i = 0; i < sourceSubEmitters.subEmittersCount; i++)
            {
                ParticleSystem sourceSubEmitter = sourceSubEmitters.GetSubEmitterSystem(i);
                ParticleSystem previewSubEmitter = ResolveMappedReference(sourceSubEmitter);
                if (previewSubEmitter == null)
                    continue;

                previewSubEmitters.AddSubEmitter(
                    previewSubEmitter,
                    sourceSubEmitters.GetSubEmitterType(i),
                    sourceSubEmitters.GetSubEmitterProperties(i));
                drivenSourceIds.Add(sourceSubEmitter.GetInstanceID());
            }
        }

        private T ResolveMappedReference<T>(T source) where T : UnityEngine.Object
        {
            if (source == null)
                return null;
            if (sourceToPreview.TryGetValue(source.GetInstanceID(), out UnityEngine.Object mapped))
                return mapped as T;

            unresolvedReferenceCount++;
            return null;
        }

        private bool TryGetMappedObject<T>(T source, out T mapped) where T : UnityEngine.Object
        {
            mapped = null;
            if (source == null || !sourceToPreview.TryGetValue(source.GetInstanceID(), out UnityEngine.Object value))
                return false;
            mapped = value as T;
            return mapped != null;
        }

        private void BuildSimulationRoots()
        {
            simulationRoots.Clear();
            for (int i = 0; i < sourceSystems.Count; i++)
            {
                ParticleSystem source = sourceSystems[i];
                if (drivenSourceIds.Contains(source.GetInstanceID()))
                    continue;

                Transform parent = source.transform.parent;
                bool hasParticleAncestor = false;
                while (parent != null && IsInsideAnySourceRoot(parent))
                {
                    ParticleSystem parentSystem = parent.GetComponent<ParticleSystem>();
                    if (parentSystem != null && sourceToPreview.ContainsKey(parentSystem.GetInstanceID()))
                    {
                        hasParticleAncestor = true;
                        break;
                    }
                    parent = parent.parent;
                }

                if (!hasParticleAncestor)
                    simulationRoots.Add(previewSystems[i]);
            }

            if (simulationRoots.Count == 0 && previewSystems.Count > 0)
                simulationRoots.Add(previewSystems[0]);
        }

        private void PrepareFromBeginning()
        {
            for (int i = 0; i < simulationRoots.Count; i++)
            {
                ParticleSystem root = simulationRoots[i];
                if (root != null)
                    root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            for (int i = 0; i < previewSystems.Count; i++)
            {
                ParticleSystem source = sourceSystems[i];
                ParticleSystem preview = previewSystems[i];
                if (preview == null)
                    continue;

                preview.useAutoRandomSeed = false;
                preview.randomSeed = unchecked((uint)(randomSeed + i * 486187739));
                configurePreviewSystem?.Invoke(
                    source,
                    preview,
                    controlledSourceIds.Contains(source.GetInstanceID()));
            }

            currentTime = 0f;
            pendingSimulationTime = 0f;
        }

        private void WarmStartVisibleFrame()
        {
            float warmup = Mathf.Clamp(sampleInterval, 1f / 120f, 0.1f);
            for (int i = 0; i < simulationRoots.Count; i++)
            {
                ParticleSystem root = simulationRoots[i];
                if (root == null)
                    continue;

                root.Simulate(warmup, true, true, true);
                root.Pause(true);
            }
        }

        private void ActivatePreviewRoots()
        {
            for (int i = 0; i < previewRoots.Count; i++)
            {
                GameObject previewRoot = previewRoots[i];
                if (previewRoot != null)
                    previewRoot.SetActive(true);
            }
        }

        private void OnEditorUpdate()
        {
            if (!playing || !IsReady)
            {
                UnregisterUpdate();
                return;
            }

            try
            {
                double now = EditorApplication.timeSinceStartup;
                float elapsed = Mathf.Clamp((float)(now - lastEditorTime), 0f, 0.1f) * playbackSpeed;
                lastEditorTime = now;
                pendingSimulationTime += elapsed;
                if (pendingSimulationTime < sampleInterval)
                    return;

                float step = Mathf.Min(pendingSimulationTime, 0.1f);
                pendingSimulationTime = 0f;
                float nextTime = currentTime + step;
                if (nextTime >= duration)
                {
                    if (!loop)
                    {
                        Seek(duration);
                        playing = false;
                        UnregisterUpdate();
                        return;
                    }

                    PrepareFromBeginning();
                    ActivatePreviewRoots();
                    WarmStartVisibleFrame();
                    nextTime = Mathf.Repeat(nextTime, duration);
                }

                currentTime = nextTime;
                using (SimulationMarker.Auto())
                {
                    for (int i = 0; i < simulationRoots.Count; i++)
                    {
                        ParticleSystem system = simulationRoots[i];
                        if (system != null)
                            system.Simulate(step, true, false, false);
                    }
                }

                EditorApplication.QueuePlayerLoopUpdate();
                repaintRequested?.Invoke();
            }
            catch (Exception exception)
            {
                lastError = exception.GetType().Name + ": " + exception.Message;
                playing = false;
                UnregisterUpdate();
                Debug.LogException(exception);
                repaintRequested?.Invoke();
            }
        }

        private void RegisterUpdate()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void UnregisterUpdate()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void AddMinimalSourceRoot(GameObject candidate)
        {
            if (candidate == null)
                return;

            Transform candidateTransform = candidate.transform;
            for (int i = 0; i < sourceRoots.Count; i++)
            {
                GameObject existing = sourceRoots[i];
                if (existing == null)
                    continue;
                if (candidate == existing || candidateTransform.IsChildOf(existing.transform))
                    return;
            }

            for (int i = sourceRoots.Count - 1; i >= 0; i--)
            {
                GameObject existing = sourceRoots[i];
                if (existing == null || existing.transform.IsChildOf(candidateTransform))
                    sourceRoots.RemoveAt(i);
            }
            sourceRoots.Add(candidate);
        }

        private bool IsInsideAnySourceRoot(Transform candidate)
        {
            if (candidate == null)
                return false;
            for (int i = 0; i < sourceRoots.Count; i++)
            {
                GameObject root = sourceRoots[i];
                if (root != null && (candidate == root.transform || candidate.IsChildOf(root.transform)))
                    return true;
            }
            return false;
        }

        private void ExpandExternalSubEmitterRoots()
        {
            int scanIndex = 0;
            CollectSourceSystems();
            while (scanIndex < sourceSystems.Count && sourceSystems.Count <= maximumParticleSystems)
            {
                ParticleSystem source = sourceSystems[scanIndex++];
                ParticleSystem.SubEmittersModule subEmitters = source.subEmitters;
                bool rootAdded = false;
                for (int i = 0; i < subEmitters.subEmittersCount; i++)
                {
                    ParticleSystem dependency = subEmitters.GetSubEmitterSystem(i);
                    if (dependency == null || IsInsideAnySourceRoot(dependency.transform))
                        continue;
                    AddMinimalSourceRoot(dependency.gameObject);
                    rootAdded = true;
                }

                if (rootAdded)
                {
                    CollectSourceSystems();
                    scanIndex = 0;
                }
            }
        }

        private void CollectSourceSystems()
        {
            sourceSystems.Clear();
            var seen = new HashSet<int>();
            for (int i = 0; i < sourceRoots.Count; i++)
            {
                GameObject root = sourceRoots[i];
                if (root == null)
                    continue;
                ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
                for (int systemIndex = 0; systemIndex < systems.Length; systemIndex++)
                {
                    ParticleSystem system = systems[systemIndex];
                    if (system != null && seen.Add(system.GetInstanceID()))
                        sourceSystems.Add(system);
                }
            }
        }

        private void ReleasePreviewContent()
        {
            playing = false;
            UnregisterUpdate();
            if (renderContext != null)
            {
                try { renderContext.Dispose(); }
                catch (Exception exception) { Debug.LogException(exception); }
                finally { renderContext = null; }
            }

            modelHandles.Clear();
            previewRoots.Clear();
            sourceRoots.Clear();
            sourceSystems.Clear();
            previewSystems.Clear();
            simulationRoots.Clear();
            previewRenderers.Clear();
            sourceToPreview.Clear();
            controlledSourceIds.Clear();
            drivenSourceIds.Clear();
            configurePreviewSystem = null;
            currentTime = 0f;
            pendingSimulationTime = 0f;
            unresolvedReferenceCount = 0;
            skippedComponentCount = 0;
            sourceParticleCapacity = 0L;
            sourceWorldAnchor = Vector3.zero;
        }

        private GameObject CreateSafeCloneRoot(GameObject sourceRoot, out Transform previewContentRoot)
        {
            previewContentRoot = null;
            var ancestors = new List<Transform>(8);
            Transform ancestor = sourceRoot != null ? sourceRoot.transform.parent : null;
            while (ancestor != null)
            {
                ancestors.Add(ancestor);
                ancestor = ancestor.parent;
            }

            GameObject carrierRoot = null;
            try
            {
                Transform carrierParent = null;
                for (int i = ancestors.Count - 1; i >= 0; i--)
                {
                    Transform sourceAncestor = ancestors[i];
                    var carrier = new GameObject("__ESParticleSpace__" + sourceAncestor.name);
                    if (carrierRoot == null)
                    {
                        carrierRoot = carrier;
                        carrierRoot.SetActive(false);
                    }
                    else
                    {
                        carrier.transform.SetParent(carrierParent, false);
                    }

                    CopyLocalTransform(sourceAncestor, carrier.transform);
                    sourceToPreview[sourceAncestor.gameObject.GetInstanceID()] = carrier;
                    sourceToPreview[sourceAncestor.GetInstanceID()] = carrier.transform;
                    carrierParent = carrier.transform;
                }

                GameObject cloneRoot = CloneHierarchyInactive(sourceRoot.transform, carrierParent);
                if (cloneRoot == null)
                    throw new InvalidOperationException("无法安全复制粒子根对象：" + sourceRoot.name);
                previewContentRoot = cloneRoot.transform;
                if (carrierRoot == null)
                    return cloneRoot;

                cloneRoot.SetActive(true);
                return carrierRoot;
            }
            catch
            {
                if (carrierRoot != null)
                    ESEditorPreviewUtility.DestroyObject(carrierRoot);
                throw;
            }
        }

        private GameObject CloneHierarchyInactive(Transform source, Transform previewParent)
        {
            if (source == null)
                return null;

            GameObject preview = new GameObject(source.gameObject.name);
            try
            {
                preview.SetActive(false);
                if (previewParent != null)
                    preview.transform.SetParent(previewParent, false);
                CopyLocalTransform(source, preview.transform);
                CloneSafeComponents(source.gameObject, preview);

                for (int i = 0; i < source.childCount; i++)
                {
                    Transform sourceChild = source.GetChild(i);
                    GameObject previewChild = CloneHierarchyInactive(sourceChild, preview.transform);
                    if (previewChild != null)
                        previewChild.SetActive(sourceChild.gameObject.activeSelf);
                }

                return preview;
            }
            catch
            {
                ESEditorPreviewUtility.DestroyObject(preview);
                throw;
            }
        }

        private static void CopyLocalTransform(Transform source, Transform preview)
        {
            preview.localPosition = source.localPosition;
            preview.localRotation = source.localRotation;
            preview.localScale = source.localScale;
        }

        private void CloneSafeComponents(GameObject source, GameObject preview)
        {
            Component[] sourceComponents = source.GetComponents<Component>();
            for (int i = 0; i < sourceComponents.Length; i++)
            {
                Component sourceComponent = sourceComponents[i];
                if (sourceComponent == null || sourceComponent is Transform)
                    continue;
                if (sourceComponent is MonoBehaviour || sourceComponent is Camera || sourceComponent is AudioListener)
                {
                    skippedComponentCount++;
                    continue;
                }

                Type type = sourceComponent.GetType();
                int ordinal = 0;
                for (int before = 0; before < i; before++)
                {
                    Component previous = sourceComponents[before];
                    if (previous != null && previous.GetType() == type)
                        ordinal++;
                }

                Component[] candidates = preview.GetComponents(type);
                Component previewComponent = ordinal < candidates.Length ? candidates[ordinal] : null;
                if (previewComponent == null)
                {
                    try
                    {
                        previewComponent = preview.AddComponent(type);
                    }
                    catch
                    {
                        skippedComponentCount++;
                        continue;
                    }
                }

                try
                {
                    EditorUtility.CopySerialized(sourceComponent, previewComponent);
                }
                catch
                {
                    ESEditorPreviewUtility.DestroyObject(previewComponent);
                    skippedComponentCount++;
                }
            }
        }

        private void RefreshBudgetedSampleRate()
        {
            float budgetedRate = requestedSampleRate;
            if (sourceSystems.Count > 64 || sourceParticleCapacity > 150000L)
                budgetedRate = Mathf.Min(budgetedRate, 15f);
            else if (sourceSystems.Count > 32 || sourceParticleCapacity > 75000L)
                budgetedRate = Mathf.Min(budgetedRate, 20f);
            sampleInterval = 1f / Mathf.Max(1f, budgetedRate);
        }

        private void EnsureReady()
        {
            ThrowIfDisposed();
            if (!IsReady)
                throw new InvalidOperationException("粒子预览会话尚未完成构建。" + (string.IsNullOrEmpty(lastError) ? string.Empty : " " + lastError));
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ESEditorParticlePreviewSession));
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
#endif
