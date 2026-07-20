using System;
using System.Collections.Generic;
using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Playables;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    public partial class EntityStateDomain
    {
#if UNITY_EDITOR
        // 缓存各层级折叠状态，避免每次 OnGUI 重置。
        private static Dictionary<StateLayerType, bool> layerFoldouts = new Dictionary<StateLayerType, bool>();
        private sealed class PreviewStateTrace
        {
            public StateBase state;
            public StateLayerType layerType;
            public string displayName;
            public float lastWeight;
            public int lastSlot;
            public double lastSeenTime;
            public double firstSeenTime;
            public StateRuntimePhase lastPhase;
            public bool wasSuppressed;
        }

        [NonSerialized] private Dictionary<StateBase, PreviewStateTrace> _previewStateTraces = new Dictionary<StateBase, PreviewStateTrace>(64);
        [NonSerialized] private List<StateBase> _previewTraceRemoveBuffer = new List<StateBase>(16);
        private static bool _previewStateTraceEnabled = true;
        private const double PreviewStateTraceHoldSeconds = 1.5d;
        private const double PreviewStateTraceCleanupSeconds = 4d;
        private const int PreviewStateTraceMaxCount = 96;
        [NonSerialized] private IEditorTrackSupport_GetSequence _previewTrackContainer;
        [NonSerialized] private float _previewTrackTime;
        [NonSerialized] private bool _previewTrackAutoScanned;
        [NonSerialized] private GameObject _previewCameraRoot;
        [NonSerialized] private Camera _previewCamera;
        [NonSerialized] private Light _previewKeyLight;
        [NonSerialized] private Light _previewFillLight;
        [NonSerialized] private RenderTexture _previewRenderTexture;
        [NonSerialized] private GameObject _previewRenderRoot;
        [NonSerialized] private Entity _previewRenderEntity;
        [NonSerialized] private EditorSequencePlayer _previewRenderPlayer;
        [NonSerialized] private ESEditorPreviewResourceScope _previewResourceScope;
        [NonSerialized] private bool _previewRenderPlaying;
        [NonSerialized] private bool _previewRenderCompleted;
        [NonSerialized] private bool _previewRenderUpdateRegistered;
        [NonSerialized] private double _previewRenderLastTime;
        [NonSerialized] private Vector2 _previewRenderOrbit = new Vector2(25f, -20f);
        [NonSerialized] private float _previewZoom = 1f;
        [NonSerialized] private bool _previewAutoFitView = false;
        [NonSerialized] private Vector3 _previewViewCenter = Vector3.up;
        [NonSerialized] private float _previewViewRadius = 1f;
        [NonSerialized] private float _previewPlaybackSpeed = 1f;
        private static bool _entityTrackPreviewFoldout = true;
        private static bool _suppressAutoPreviewRebuildAfterManualCleanup;
        private const float PreviewRenderHeight = 260f;
        private const float PreviewRenderTextureMaxSize = 2048f;
        private const float PreviewRenderScale = 2f;
        private const int PreviewRenderLayer = 31;
        private const string PreviewRootSuffix = "_ESPreview";
        private const string PreviewCameraName = "ES_URP_EntityPreviewCamera";
        private const string PreviewKeyLightName = "ES_URP_EntityPreviewKeyLight";
        private const string PreviewFillLightName = "ES_URP_EntityPreviewFillLight";
        private const string PreviewResourceOwner = nameof(EntityStateDomain);

        public bool IsSingleArea => true;
        public bool CanPreview => stateMachine != null || !Application.isPlaying;
        public bool EditorPreviewCanPreviewNonPlay => true;
        public PreviewAreaMode PreviewAreaMode => PreviewAreaMode.Large;
        public bool WantsPreviewEditorUpdate => _previewRenderPlaying && _previewRenderPlayer != null && _previewRenderRoot != null;

        public void OnPreviewEnable()
        {
        }

        public void OnPreviewDisable()
        {
            DisposePreviewRender();
        }

        public void DisposePreview()
        {
            DisposePreviewRender();
        }

        public void OnPreviewEditorUpdate(float deltaTime)
        {
            UpdatePreviewPlayback(deltaTime, repaintAllViews: false);
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void CleanupEntityPreviewObjectsOnEditorLoad()
        {
            UnityEditor.EditorApplication.delayCall -= CleanupLingeringPreviewObjects;
            UnityEditor.EditorApplication.delayCall += CleanupLingeringPreviewObjects;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= CleanupLingeringPreviewObjects;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += CleanupLingeringPreviewObjects;
            UnityEditor.EditorApplication.playModeStateChanged -= CleanupPreviewObjectsOnPlayModeChange;
            UnityEditor.EditorApplication.playModeStateChanged += CleanupPreviewObjectsOnPlayModeChange;
            UnityEditor.EditorApplication.quitting -= CleanupLingeringPreviewObjects;
            UnityEditor.EditorApplication.quitting += CleanupLingeringPreviewObjects;
        }

        private static void CleanupPreviewObjectsOnPlayModeChange(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingEditMode
                || state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                CleanupLingeringPreviewObjects();
        }

        [UnityEditor.MenuItem(MenuItemPathDefine.PREVIEW_CLEANUP_PATH + "清理实体预览残留对象", false, 10)]
        private static void CleanupEntityPreviewObjectsMenu()
        {
            int removed = CleanupLingeringPreviewObjectsInternal();
            _suppressAutoPreviewRebuildAfterManualCleanup = true;
            Debug.Log($"[EntityStateDomain Preview] 已清理实体预览残留对象：{removed}");
        }

        public void DrawPreviewGUIPlaying() => EditorPreviewDrawPreviewGUIImpl();
        public void EditorPreviewDrawPreviewGUINonPlay()
        {
            EditorPreviewDrawTrackPreviewNonPlay();
        }

        private void EditorPreviewDrawTrackPreviewNonPlay()
        {
            _entityTrackPreviewFoldout = UnityEditor.EditorGUILayout.Foldout(_entityTrackPreviewFoldout, "实体轨道预览", true);
            if (!_entityTrackPreviewFoldout)
            {
                DisposePreviewRender();
                return;
            }

            Entity entity = ResolveEditorPreviewEntity();
            if (entity == null)
            {
                DisposePreviewRender();
                UnityEditor.EditorGUILayout.HelpBox("没有找到 Entity。请选择带 Entity 的对象，或检查当前 Inspector 目标。", UnityEditor.MessageType.Warning);
                return;
            }

            EnsurePreviewTrackAutoSelected();

            UnityEditor.EditorGUILayout.BeginHorizontal();
            string selectedName = _previewTrackContainer != null
                ? $"{_previewTrackContainer.trackName} / {_previewTrackContainer.Sequence?.Name ?? "<无序列>"}"
                : "未选择轨道序列";
            UnityEditor.EditorGUILayout.LabelField("当前序列", selectedName);

            if (GUILayout.Button("自动", GUILayout.Width(52)))
            {
                _suppressAutoPreviewRebuildAfterManualCleanup = false;
                _previewTrackAutoScanned = false;
                EnsurePreviewTrackAutoSelected(forceProjectScan: true);
                Entity autoEntity = ResolveEditorPreviewEntity();
                if (_previewTrackContainer != null && autoEntity != null)
                    RebuildPreviewRender(autoEntity, 0f);
            }

            if (GUILayout.Button("选择", GUILayout.Width(64)))
            {
                Rect rect = GUILayoutUtility.GetLastRect();
                IEditorTrackSupport_GetSequence.ShowDynamicMenu(rect, OnPreviewTrackSelected);
            }
            UnityEditor.EditorGUILayout.EndHorizontal();

            if (_previewTrackContainer == null || _previewTrackContainer.Sequence == null)
            {
                DisposePreviewRender();
                UnityEditor.EditorGUILayout.HelpBox("没有可用的轨道序列。请选择 SKillDataInfo 资源，或点击“自动”扫描项目技能数据。", UnityEditor.MessageType.None);
                return;
            }

            float duration = _previewRenderPlayer != null ? Mathf.Max(0.01f, _previewRenderPlayer.Duration) : Mathf.Max(0.01f, GetSequenceDuration(_previewTrackContainer.Sequence));
            float current = _previewRenderPlayer != null ? _previewRenderPlayer.CurrentTime : Mathf.Clamp(_previewTrackTime, 0f, duration);

            UnityEditor.EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("重建", GUILayout.Width(72)))
            {
                _suppressAutoPreviewRebuildAfterManualCleanup = false;
                RebuildPreviewRender(entity, current);
            }

            if (GUILayout.Button("播放", GUILayout.Width(48)))
            {
                _suppressAutoPreviewRebuildAfterManualCleanup = false;
                StartPreviewPlayback(entity, duration);
            }

            if (GUILayout.Button("暂停", GUILayout.Width(52)))
            {
                _previewRenderPlayer?.Pause();
                _previewRenderPlaying = false;
            }

            if (GUILayout.Button("停止", GUILayout.Width(48)))
            {
                StopPreviewPlaybackToIdle(resetTime: true, completed: false);
            }
            UnityEditor.EditorGUILayout.EndHorizontal();

            UnityEditor.EditorGUILayout.BeginHorizontal();
            UnityEditor.EditorGUILayout.LabelField("速度", GUILayout.Width(44));
            UnityEditor.EditorGUI.BeginChangeCheck();
            _previewPlaybackSpeed = UnityEditor.EditorGUILayout.Slider(_previewPlaybackSpeed, 0.05f, 3f, GUILayout.MaxWidth(220));
            if (UnityEditor.EditorGUI.EndChangeCheck() && _previewRenderPlayer != null)
                _previewRenderPlayer.Speed = _previewPlaybackSpeed;
            UnityEditor.EditorGUILayout.LabelField($"{_previewPlaybackSpeed:F2}x", GUILayout.Width(42));
            UnityEditor.EditorGUILayout.EndHorizontal();

            UnityEditor.EditorGUILayout.BeginHorizontal();
            _previewAutoFitView = UnityEditor.EditorGUILayout.ToggleLeft("自动适配视距", _previewAutoFitView, GUILayout.Width(120));
            UnityEditor.EditorGUILayout.LabelField("视距", GUILayout.Width(36));
            _previewZoom = UnityEditor.EditorGUILayout.Slider(_previewZoom, 0.25f, 4f, GUILayout.MaxWidth(180));
            UnityEditor.EditorGUILayout.EndHorizontal();

            UnityEditor.EditorGUI.BeginChangeCheck();
            UnityEditor.EditorGUILayout.BeginHorizontal();
            UnityEditor.EditorGUILayout.LabelField("时间", GUILayout.Width(44));
            float newTime = UnityEditor.EditorGUILayout.Slider(current, 0f, duration, GUILayout.MaxWidth(320));
            UnityEditor.EditorGUILayout.LabelField($"{current:F2}s / {duration:F2}s", GUILayout.Width(110));
            UnityEditor.EditorGUILayout.EndHorizontal();
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                _previewTrackTime = newTime;
                if (!_suppressAutoPreviewRebuildAfterManualCleanup)
                {
                    EnsurePreviewRender(entity);
                    SetPreviewRenderTime(newTime);
                }
            }

            if (_suppressAutoPreviewRebuildAfterManualCleanup && _previewRenderPlayer == null)
            {
                UnityEditor.EditorGUILayout.HelpBox("已手动清理实体预览对象，当前不会自动重建。需要预览时请点击“重建”或“播放”。", UnityEditor.MessageType.Info);
                return;
            }

            EnsurePreviewRender(entity);
            DrawPreviewRenderArea(entity);
        }

        private void OnPreviewTrackSelected(object userData)
        {
            _previewTrackContainer = userData as IEditorTrackSupport_GetSequence;
            _previewTrackTime = 0f;
            _suppressAutoPreviewRebuildAfterManualCleanup = false;

            Entity entity = ResolveEditorPreviewEntity();
            if (_previewTrackContainer != null && entity != null)
                RebuildPreviewRender(entity, 0f);
        }

        private void EnsurePreviewTrackAutoSelected(bool forceProjectScan = false)
        {
            if (_previewTrackContainer != null && _previewTrackContainer.Sequence != null && !forceProjectScan)
                return;

            if (TryResolveTrackContainerFromSelection(out var selected))
            {
                _previewTrackContainer = selected;
                _previewTrackTime = 0f;
                return;
            }

            if (_previewTrackAutoScanned && !forceProjectScan)
                return;

            _previewTrackAutoScanned = true;
            if (TryResolveFirstTrackContainerInProject(out var projectOne))
            {
                _previewTrackContainer = projectOne;
                _previewTrackTime = 0f;
            }
        }

        private static bool TryResolveTrackContainerFromSelection(out IEditorTrackSupport_GetSequence result)
        {
            result = null;
            UnityEngine.Object selected = UnityEditor.Selection.activeObject;

            if (selected is IEditorTrackSupport_GetSequence direct && IsUsableTrackContainer(direct))
            {
                result = direct;
                return true;
            }

            if (selected is GameObject go)
            {
                var components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] is IEditorTrackSupport_GetSequence support && IsUsableTrackContainer(support))
                    {
                        result = support;
                        return true;
                    }
                }
            }

            if (selected is Component component && component is IEditorTrackSupport_GetSequence componentSupport && IsUsableTrackContainer(componentSupport))
            {
                result = componentSupport;
                return true;
            }

            return false;
        }

        private static bool TryResolveFirstTrackContainerInProject(out IEditorTrackSupport_GetSequence result)
        {
            result = null;
            List<IEditorTrackSupport_GetSequence> all = null;
            try
            {
                all = ESDesignUtility.SafeEditor.FindAllSOAssets<IEditorTrackSupport_GetSequence>();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (all == null)
                return false;

            for (int i = 0; i < all.Count; i++)
            {
                if (!IsUsableTrackContainer(all[i]))
                    continue;

                result = all[i];
                return true;
            }

            return false;
        }

        private static bool IsUsableTrackContainer(IEditorTrackSupport_GetSequence container)
        {
            return container != null && container.Sequence != null;
        }

        private Entity ResolveEditorPreviewEntity()
        {
            if (MyCore != null)
            {
                EditorRememberedEntityTarget.StatePreview.Remember(MyCore);
                return MyCore;
            }

            GameObject selected = UnityEditor.Selection.activeGameObject;
            if (selected == null && UnityEditor.Selection.activeObject is Component component)
                selected = component.gameObject;

            if (selected == null)
                return EditorRememberedEntityTarget.StatePreview.ResolveOrSceneFallback();

            Entity entity = selected.GetComponent<Entity>();
            if (entity != null)
            {
                EditorRememberedEntityTarget.StatePreview.Remember(entity);
                return entity;
            }

            entity = selected.GetComponentInParent<Entity>();
            if (entity != null)
            {
                EditorRememberedEntityTarget.StatePreview.Remember(entity);
                return entity;
            }

            entity = selected.GetComponentInChildren<Entity>(true);
            if (entity != null)
            {
                EditorRememberedEntityTarget.StatePreview.Remember(entity);
                return entity;
            }

            return EditorRememberedEntityTarget.StatePreview.ResolveOrSceneFallback();
        }

        private void EnsurePreviewRender(Entity sourceEntity)
        {
            if (_suppressAutoPreviewRebuildAfterManualCleanup)
                return;

            if (_previewCamera != null
                && _previewRenderRoot != null
                && _previewRenderEntity != null
                && _previewRenderPlayer != null)
                return;

            RebuildPreviewRender(sourceEntity, _previewTrackTime);
        }

        private void RebuildPreviewRender(Entity sourceEntity, float startTime)
        {
            _suppressAutoPreviewRebuildAfterManualCleanup = false;
            StopPreviewPlaybackState();
            DisposePreviewRender();
            CleanupLingeringPreviewObjects();

            if (sourceEntity == null || _previewTrackContainer == null || _previewTrackContainer.Sequence == null)
                return;

            _previewResourceScope = new ESEditorPreviewResourceScope(PreviewResourceOwner, "Entity editor preview temporary resource.");
            CreateUrpPreviewCameraRig();

            _previewRenderRoot = UnityEngine.Object.Instantiate(sourceEntity.gameObject);
            _previewRenderRoot.name = $"{sourceEntity.name}{PreviewRootSuffix}";
            _previewResourceScope.RegisterGameObject(_previewRenderRoot, recursiveHideFlags: true);
            _previewRenderRoot.SetActive(true);
            NormalizePreviewRootTransform(_previewRenderRoot.transform);
            SetLayerRecursive(_previewRenderRoot.transform, PreviewRenderLayer);

            _previewRenderEntity = _previewRenderRoot.GetComponent<Entity>();
            if (_previewRenderEntity == null)
                _previewRenderEntity = _previewRenderRoot.GetComponentInChildren<Entity>(true);

            CopyPreviewRendererState(sourceEntity.gameObject, _previewRenderRoot);
            DisablePreviewBehaviours(_previewRenderRoot);
            EnsurePreviewAnimatorEnabled(_previewRenderRoot);
            EnsurePreviewRenderers(_previewRenderRoot);
            ApplyPreviewIdlePose(_previewRenderRoot);
            AlignPreviewRootToGround(_previewRenderRoot);

            _previewRenderPlayer = CreatePreviewSequencePlayer(_previewRenderEntity, _previewTrackContainer.Sequence, _previewTrackContainer.trackName);
            _previewRenderPlayer.StartAllSamplers();
            _previewTrackTime = Mathf.Clamp(startTime, 0f, Mathf.Max(0.01f, _previewRenderPlayer.Duration));
            _previewRenderCompleted = false;
            _previewRenderPlayer.SetPreviewIdleWeight(1f);
            AlignPreviewRootToGround(_previewRenderRoot);
            CachePreviewViewBounds(_previewRenderRoot);
        }

        private void DrawPreviewRenderArea(Entity sourceEntity)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, PreviewRenderHeight, GUILayout.ExpandWidth(true));
            if (_previewCamera == null || _previewRenderRoot == null || _previewRenderEntity == null)
            {
                UnityEditor.EditorGUI.HelpBox(rect, "URP 预览相机没有创建。请点击“重建”。", UnityEditor.MessageType.Info);
                return;
            }

            if (!_previewRenderPlaying)
                MaintainPreviewIdlePlayable();

            HandlePreviewInput(rect);
            Bounds bounds = CalculatePreviewBounds(_previewRenderRoot);
            if (_previewAutoFitView)
                CachePreviewViewBounds(bounds);
            DrawPreviewTexture(rect, bounds);
        }

        private void UpdatePreviewPlayback()
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            float deltaTime = Mathf.Min(0.1f, (float)(now - _previewRenderLastTime));
            _previewRenderLastTime = now;
            UpdatePreviewPlayback(deltaTime, repaintAllViews: true);
        }

        private void UpdatePreviewPlayback(float deltaTime, bool repaintAllViews)
        {
            if (!_previewRenderPlaying || _previewRenderPlayer == null)
                return;

            float nextTime = _previewRenderPlayer.CurrentTime + deltaTime * Mathf.Max(0.01f, _previewRenderPlayer.Speed);
            if (nextTime >= _previewRenderPlayer.Duration)
            {
                nextTime = _previewRenderPlayer.Duration;
                _previewTrackTime = nextTime;
                StopPreviewPlaybackToIdle(resetTime: false, completed: true);
                if (repaintAllViews)
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                return;
            }

            SetPreviewRenderTime(nextTime);
            if (repaintAllViews)
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private void SetPreviewRenderTime(float time)
        {
            if (_previewRenderPlayer == null)
                return;

            _previewTrackTime = Mathf.Clamp(time, 0f, Mathf.Max(0.01f, _previewRenderPlayer.Duration));
            _previewRenderPlayer.SetTime(_previewTrackTime);
            AlignPreviewRootToGround(_previewRenderRoot);
        }

        private void MaintainPreviewIdlePlayable()
        {
            if (_previewRenderPlayer == null || _previewRenderRoot == null)
                return;

            _previewRenderPlayer.SetPreviewIdleWeight(1f);
            AlignPreviewRootToGround(_previewRenderRoot);
        }

        private void StartPreviewPlayback(Entity entity, float duration)
        {
            float startTime = _previewTrackTime;
            if (_previewRenderCompleted
                || startTime >= Mathf.Max(0f, duration - 0.0001f)
                || (_previewRenderPlayer != null && _previewRenderPlayer.CurrentTime >= Mathf.Max(0f, duration - 0.0001f)))
                startTime = 0f;

            RebuildPreviewRender(entity, startTime);
            if (_previewRenderPlayer == null)
                return;

            StopPreviewAnimationMode();

            _previewRenderCompleted = false;
            _previewRenderPlayer.UsePreviewIdleAutoBlend();
            _previewRenderPlayer.SetTime(_previewTrackTime);
            _previewRenderPlayer.Play();
            _previewRenderPlaying = true;
            _previewRenderLastTime = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private void StopPreviewPlaybackToIdle(bool resetTime, bool completed)
        {
            _previewRenderPlaying = false;
            _previewRenderCompleted = completed;
            UnregisterPreviewRenderUpdate();

            if (_previewRenderPlayer != null)
            {
                _previewRenderPlayer.Pause();
                _previewRenderPlayer.StopAllSamplers();
                if (resetTime)
                    _previewTrackTime = 0f;
            }

            ApplyPreviewIdlePose(_previewRenderRoot);
            AlignPreviewRootToGround(_previewRenderRoot);
        }

        private void RegisterPreviewRenderUpdate()
        {
            if (_previewRenderUpdateRegistered)
                return;

            UnityEditor.EditorApplication.update -= OnPreviewRenderEditorUpdate;
            UnityEditor.EditorApplication.update += OnPreviewRenderEditorUpdate;
            _previewRenderUpdateRegistered = true;
        }

        private void UnregisterPreviewRenderUpdate()
        {
            UnityEditor.EditorApplication.update -= OnPreviewRenderEditorUpdate;
            _previewRenderUpdateRegistered = false;
        }

        private void OnPreviewRenderEditorUpdate()
        {
            if (!_previewRenderPlaying || _previewRenderPlayer == null || _previewRenderRoot == null)
            {
                UnregisterPreviewRenderUpdate();
                return;
            }

            UpdatePreviewPlayback();
        }

        private void StopPreviewPlaybackState()
        {
            _previewRenderPlaying = false;
            _previewRenderCompleted = false;
            UnregisterPreviewRenderUpdate();

            if (_previewRenderPlayer == null)
                return;

            _previewRenderPlayer.Pause();
            _previewRenderPlayer.StopAllSamplers();
        }

        private static void StopPreviewAnimationMode()
        {
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
        }

        private EditorSequencePlayer CreatePreviewSequencePlayer(Entity previewEntity, ITrackSequence sequence, string sequenceName)
        {
            var seqPlayer = new EditorSequencePlayer
            {
                Name = string.IsNullOrEmpty(sequenceName) ? "实体预览序列" : sequenceName,
                Duration = Mathf.Max(0.01f, GetSequenceDuration(sequence)),
                Speed = Mathf.Max(0.05f, _previewPlaybackSpeed)
            };

            EditorRememberedEntityTarget.StatePreview.FillPreviewTarget(seqPlayer.PreviewTarget, previewEntity);

            if (sequence != null && sequence.Tracks != null)
            {
                foreach (var track in sequence.Tracks)
                {
                    if (track == null || !track.Enabled)
                        continue;

                    List<IEditorTimeSampler> samplers = null;
                    try
                    {
                        samplers = track.CreateEditorSamplers(sequence, seqPlayer.PreviewTarget);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[EntityStateDomain Preview Render] CreateEditorSamplers failed. Track={track.DisplayName}, Type={track.GetType().Name}");
                        Debug.LogException(e);
                    }

                    if (samplers == null)
                        continue;

                    for (int i = 0; i < samplers.Count; i++)
                        seqPlayer.RegisterSampler(samplers[i]);
                }
            }

            seqPlayer.OnTimeUpdated += time => _previewTrackTime = time;
            return seqPlayer;
        }

        private void DrawPreviewTexture(Rect rect, Bounds bounds)
        {
            if (!TryBuildPreviewRects(rect, out Rect displayRect, out Rect renderRect))
            {
                UnityEditor.EditorGUI.HelpBox(rect, "预览区域尺寸无效，本帧跳过渲染。", UnityEditor.MessageType.Warning);
                return;
            }

            Camera camera = _previewCamera;
            if (camera == null)
                return;

            if (!EnsurePreviewRenderTexture(renderRect))
            {
                UnityEditor.EditorGUI.HelpBox(displayRect, "URP 预览 RenderTexture 创建失败。", UnityEditor.MessageType.Warning);
                return;
            }

            Vector3 center = _previewAutoFitView ? bounds.center : _previewViewCenter;
            float radius = _previewAutoFitView ? Mathf.Max(0.5f, bounds.extents.magnitude) : Mathf.Max(0.5f, _previewViewRadius);
            Quaternion rotation = Quaternion.Euler(_previewRenderOrbit.x, _previewRenderOrbit.y, 0f);
            float halfFov = Mathf.Max(1f, camera.fieldOfView) * 0.5f * Mathf.Deg2Rad;
            float fitDistance = radius / Mathf.Sin(halfFov);
            float distance = fitDistance * 1.15f * Mathf.Clamp(_previewZoom, 0.25f, 4f);
            Vector3 cameraOffset = rotation * (Vector3.back * distance);

            camera.transform.position = center + cameraOffset;
            camera.transform.LookAt(center, Vector3.up);
            camera.nearClipPlane = Mathf.Max(0.001f, distance - radius * 2.2f);
            camera.farClipPlane = distance + radius * 2.2f;
            camera.cullingMask = 1 << PreviewRenderLayer;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            camera.targetTexture = _previewRenderTexture;

            camera.Render();
            GUI.DrawTexture(displayRect, _previewRenderTexture, ScaleMode.StretchToFill, false);
        }

        private static bool TryBuildPreviewRects(Rect layoutRect, out Rect displayRect, out Rect renderRect)
        {
            displayRect = layoutRect;
            renderRect = default;

            if (!IsFinite(layoutRect.x) || !IsFinite(layoutRect.y) || !IsFinite(layoutRect.width) || !IsFinite(layoutRect.height))
                return false;

            if (layoutRect.width < 2f || layoutRect.height < 2f)
                return false;

            float viewWidth = UnityEditor.EditorGUIUtility.currentViewWidth;
            float safeDisplayWidth = IsFinite(viewWidth) && viewWidth > 32f
                ? Mathf.Min(layoutRect.width, Mathf.Max(32f, viewWidth - layoutRect.x - 8f))
                : Mathf.Min(layoutRect.width, PreviewRenderTextureMaxSize);

            displayRect.width = Mathf.Clamp(safeDisplayWidth, 16f, PreviewRenderTextureMaxSize);
            displayRect.height = Mathf.Clamp(layoutRect.height, 16f, PreviewRenderHeight);
            renderRect = new Rect(0f, 0f, displayRect.width, displayRect.height);
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void CreateUrpPreviewCameraRig()
        {
            if (_previewCameraRoot != null && _previewCamera != null)
                return;

            _previewCameraRoot = new GameObject(PreviewCameraName);
            if (_previewResourceScope == null)
                _previewResourceScope = new ESEditorPreviewResourceScope(PreviewResourceOwner, "Entity editor preview camera rig.");

            _previewResourceScope.RegisterGameObject(_previewCameraRoot);

            _previewCamera = _previewCameraRoot.AddComponent<Camera>();
            _previewCamera.enabled = false;
            _previewCamera.fieldOfView = 30f;
            _previewCamera.nearClipPlane = 0.01f;
            _previewCamera.farClipPlane = 1000f;
            _previewCamera.clearFlags = CameraClearFlags.Color;
            _previewCamera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            _previewCamera.cullingMask = 1 << PreviewRenderLayer;
            _previewCamera.allowHDR = true;
            _previewCamera.allowMSAA = true;
            _previewCamera.renderingPath = RenderingPath.Forward;

            AddAndConfigureUniversalCameraData(_previewCamera);

            _previewKeyLight = CreatePreviewDirectionalLight(PreviewKeyLightName, 1.8f, Quaternion.Euler(35f, 35f, 0f));
            _previewFillLight = CreatePreviewDirectionalLight(PreviewFillLightName, 1.0f, Quaternion.Euler(340f, 218f, 177f));
        }

        private static void AddAndConfigureUniversalCameraData(Camera camera)
        {
            if (camera == null)
                return;

            Type cameraDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (cameraDataType == null)
            {
                Debug.LogWarning("[EntityStateDomain Preview] URP package is required for this preview camera.");
                return;
            }

            Component cameraData = camera.GetComponent(cameraDataType);
            if (cameraData == null)
                cameraData = camera.gameObject.AddComponent(cameraDataType);

            SetBoolPropertyIfExists(cameraData, "renderPostProcessing", false);
            SetBoolPropertyIfExists(cameraData, "renderShadows", true);
            SetEnumPropertyIfExists(cameraData, "renderType", "Base");
            SetEnumPropertyIfExists(cameraData, "requiresDepthOption", "Off");
            SetEnumPropertyIfExists(cameraData, "requiresColorOption", "Off");
            SetEnumPropertyIfExists(cameraData, "antialiasing", "None");
        }

        private static void SetBoolPropertyIfExists(object target, string propertyName, bool value)
        {
            if (target == null)
                return;

            var property = target.GetType().GetProperty(propertyName);
            if (property == null || !property.CanWrite || property.PropertyType != typeof(bool))
                return;

            property.SetValue(target, value, null);
        }

        private static void SetEnumPropertyIfExists(object target, string propertyName, string enumName)
        {
            if (target == null)
                return;

            var property = target.GetType().GetProperty(propertyName);
            if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
                return;

            try
            {
                property.SetValue(target, Enum.Parse(property.PropertyType, enumName), null);
            }
            catch
            {
                // URP enum names can vary by version. Keep the package default if a value is missing.
            }
        }

        private Light CreatePreviewDirectionalLight(string name, float intensity, Quaternion rotation)
        {
            GameObject lightRoot = new GameObject(name);
            if (_previewResourceScope == null)
                _previewResourceScope = new ESEditorPreviewResourceScope(PreviewResourceOwner, "Entity editor preview light.");

            _previewResourceScope.RegisterGameObject(lightRoot);
            lightRoot.transform.rotation = rotation;

            Light light = lightRoot.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            return light;
        }

        private bool EnsurePreviewRenderTexture(Rect renderRect)
        {
            int width = Mathf.Clamp(Mathf.CeilToInt(renderRect.width * PreviewRenderScale), 16, (int)PreviewRenderTextureMaxSize);
            int height = Mathf.Clamp(Mathf.CeilToInt(renderRect.height * PreviewRenderScale), 16, (int)PreviewRenderTextureMaxSize);
            if (_previewRenderTexture != null
                && _previewRenderTexture.width == width
                && _previewRenderTexture.height == height)
                return _previewRenderTexture.IsCreated() || _previewRenderTexture.Create();

            ReleasePreviewRenderTexture();

            _previewRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "ES_URP_EntityPreviewRT",
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };
            _previewResourceScope?.RegisterRenderTexture(_previewRenderTexture);
            return _previewRenderTexture.Create();
        }

        private void ReleasePreviewRenderTexture()
        {
            if (_previewRenderTexture == null)
                return;

            if (_previewCamera != null && _previewCamera.targetTexture == _previewRenderTexture)
                _previewCamera.targetTexture = null;

            _previewRenderTexture.Release();
            UnityEngine.Object.DestroyImmediate(_previewRenderTexture);
            _previewRenderTexture = null;
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            if (root == null)
                return;

            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }

        private void HandlePreviewInput(Rect rect)
        {
            Event evt = Event.current;
            if (!rect.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.MouseDrag && evt.button == 0)
            {
                _previewRenderOrbit.y += evt.delta.x;
                _previewRenderOrbit.x = Mathf.Clamp(_previewRenderOrbit.x + evt.delta.y, -80f, 80f);
                evt.Use();
            }
            else if (evt.type == EventType.ScrollWheel)
            {
                _previewZoom = Mathf.Clamp(_previewZoom * (1f + evt.delta.y * 0.08f), 0.25f, 4f);
                evt.Use();
            }
        }

        private void CachePreviewViewBounds(GameObject root)
        {
            if (root == null)
                return;

            CachePreviewViewBounds(CalculatePreviewBounds(root));
        }

        private void CachePreviewViewBounds(Bounds bounds)
        {
            if (!IsFinite(bounds.center) || !IsFinite(bounds.extents))
                return;

            _previewViewCenter = bounds.center;
            _previewViewRadius = Mathf.Max(0.5f, bounds.extents.magnitude);
        }

        private static Bounds CalculatePreviewBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return new Bounds(root.transform.position + Vector3.up, Vector3.one * 2f);

            if (TryCalculatePreviewBounds(renderers, renderer => renderer is SkinnedMeshRenderer || renderer is MeshRenderer, out Bounds meshBounds))
                return meshBounds;

            if (TryCalculatePreviewBounds(renderers, renderer => true, out Bounds rendererBounds))
                return rendererBounds;

            return new Bounds(root.transform.position + Vector3.up, Vector3.one * 2f);
        }

        private static bool TryCalculatePreviewBounds(Renderer[] renderers, Predicate<Renderer> filter, out Bounds bounds)
        {
            bool hasValidRenderer = false;
            bounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (filter != null && !filter(renderer))
                    continue;

                Bounds rendererBounds = renderer.bounds;
                if (!IsFinite(rendererBounds.center) || !IsFinite(rendererBounds.extents))
                    continue;

                if (!hasValidRenderer)
                {
                    bounds = rendererBounds;
                    hasValidRenderer = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasValidRenderer;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
                   && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        }

        private void DisposePreviewRender()
        {
            UnregisterPreviewRenderUpdate();
            _previewRenderPlaying = false;
            _previewRenderCompleted = false;
            StopPreviewAnimationMode();

            if (_previewRenderPlayer != null)
            {
                _previewRenderPlayer.Pause();
                _previewRenderPlayer.StopAllSamplers();
                _previewRenderPlayer.DisposeEditorPreviewTarget();
                _previewRenderPlayer = null;
            }

            if (_previewCamera != null && _previewCamera.targetTexture == _previewRenderTexture)
                _previewCamera.targetTexture = null;

            _previewResourceScope?.Dispose();
            _previewResourceScope = null;
            _previewRenderRoot = null;
            _previewRenderEntity = null;
            _previewRenderTexture = null;
            _previewCameraRoot = null;
            _previewCamera = null;
            _previewKeyLight = null;
            _previewFillLight = null;

            CleanupLingeringPreviewObjects();
        }

        private static void CleanupLingeringPreviewObjects()
        {
            CleanupLingeringPreviewObjectsInternal();
        }

        private static int CleanupLingeringPreviewObjectsInternal()
        {
            int removed = 0;
            var objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject obj = objects[i];
                if (!IsLingeringPreviewObject(obj))
                    continue;

                if (DestroyPreviewObjectImmediate(obj))
                    removed++;
            }

            return removed;
        }

        private static bool IsLingeringPreviewObject(GameObject obj)
        {
            if (obj == null || UnityEditor.EditorUtility.IsPersistent(obj))
                return false;

            if (obj.GetComponent<EditorPreviewGameObjectSign>() != null)
                return true;

            string objectName = obj.name;
            if (IsPreviewObjectName(objectName))
                return true;

            return obj.layer == PreviewRenderLayer && HasPreviewHideFlags(obj.hideFlags);
        }

        private static bool DestroyPreviewObjectImmediate(GameObject obj)
        {
            if (obj == null)
                return false;

            Transform root = obj.transform;
            while (root.parent != null && IsLingeringPreviewObject(root.parent.gameObject))
                root = root.parent;

            if (root == null || root.gameObject == null)
                return false;

            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return true;
        }

        private static bool IsPreviewObjectName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;

            return objectName == PreviewCameraName
                   || objectName == PreviewKeyLightName
                   || objectName == PreviewFillLightName
                   || objectName.StartsWith(PreviewCameraName + " ", StringComparison.Ordinal)
                   || objectName.StartsWith(PreviewKeyLightName + " ", StringComparison.Ordinal)
                   || objectName.StartsWith(PreviewFillLightName + " ", StringComparison.Ordinal)
                   || objectName.Contains(PreviewRootSuffix);
        }

        private static bool HasPreviewHideFlags(HideFlags hideFlags)
        {
            return (hideFlags & HideFlags.HideInHierarchy) != 0
                   || (hideFlags & HideFlags.HideInInspector) != 0
                   || (hideFlags & HideFlags.DontSaveInEditor) != 0
                   || (hideFlags & HideFlags.DontSaveInBuild) != 0
                   || (hideFlags & HideFlags.DontUnloadUnusedAsset) != 0;
        }

        private static void MarkPreviewObject(GameObject obj)
        {
            if (obj == null)
                return;

            ESEditorPreviewResourceScope.MarkPreviewObject(obj, PreviewResourceOwner, "Entity editor preview temporary object.");
        }

        private static void SetHideFlagsRecursive(Transform root, HideFlags flags)
        {
            if (root == null)
                return;

            root.gameObject.hideFlags = flags;
            for (int i = 0; i < root.childCount; i++)
                SetHideFlagsRecursive(root.GetChild(i), flags);
        }

        private static void NormalizePreviewRootTransform(Transform root)
        {
            if (root == null)
                return;

            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        private static void AlignPreviewRootToGround(GameObject root)
        {
            if (root == null)
                return;

            Bounds bounds = CalculatePreviewBounds(root);
            if (!IsFinite(bounds.center) || !IsFinite(bounds.extents))
                return;

            float yOffset = -bounds.min.y;
            if (Mathf.Abs(yOffset) <= 0.0001f)
                return;

            root.transform.position += Vector3.up * yOffset;
        }

        private void ApplyPreviewIdlePose(GameObject root)
        {
            if (root == null)
                return;

            if (_previewRenderPlayer != null)
            {
                _previewRenderPlayer.SetPreviewIdleWeight(1f);
                return;
            }

            var animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                    continue;

                animator.Rebind();
                animator.Update(0f);
            }

            AnimationClip idleClip = null;
            StateMachineConfig config = StateMachineConfig.Instance;
            if (config != null)
                idleClip = config.previewIdleClip;

            if (idleClip == null)
                return;

            _previewRenderPlayer?.SetPreviewIdleWeight(1f);

            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null)
                    animators[i].Update(0f);
            }
        }

        private static void DisablePreviewBehaviours(GameObject root)
        {
            if (root == null)
                return;

            var behaviours = root.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || behaviour is Animator)
                    continue;

                behaviour.enabled = false;
            }
        }

        private static void EnsurePreviewAnimatorEnabled(GameObject root)
        {
            if (root == null)
                return;

            var animators = root.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] == null)
                    continue;

                animators[i].enabled = true;
                animators[i].cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        private static void EnsurePreviewRenderers(GameObject root)
        {
            if (root == null)
                return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Material[] sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials != null && sharedMaterials.Length > 0)
                    renderer.sharedMaterials = sharedMaterials;
            }
        }

        private static void CopyPreviewRendererState(GameObject sourceRoot, GameObject previewRoot)
        {
            if (sourceRoot == null || previewRoot == null)
                return;

            var sourceRenderers = sourceRoot.GetComponentsInChildren<Renderer>(true);
            var previewRenderers = previewRoot.GetComponentsInChildren<Renderer>(true);
            if (sourceRenderers == null || previewRenderers == null)
                return;

            var previewMap = new Dictionary<string, Renderer>(previewRenderers.Length);
            for (int i = 0; i < previewRenderers.Length; i++)
            {
                Renderer previewRenderer = previewRenderers[i];
                if (previewRenderer == null)
                    continue;

                string key = GetRendererPathKey(previewRoot.transform, previewRenderer);
                if (!previewMap.ContainsKey(key))
                    previewMap.Add(key, previewRenderer);
            }

            var propertyBlock = new MaterialPropertyBlock();
            for (int i = 0; i < sourceRenderers.Length; i++)
            {
                Renderer sourceRenderer = sourceRenderers[i];
                if (sourceRenderer == null)
                    continue;

                string key = GetRendererPathKey(sourceRoot.transform, sourceRenderer);
                if (!previewMap.TryGetValue(key, out Renderer previewRenderer) || previewRenderer == null)
                    continue;

                previewRenderer.enabled = sourceRenderer.enabled;
                previewRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                previewRenderer.receiveShadows = sourceRenderer.receiveShadows;
                previewRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
                previewRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
                previewRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

                sourceRenderer.GetPropertyBlock(propertyBlock);
                previewRenderer.SetPropertyBlock(propertyBlock);
                propertyBlock.Clear();
            }
        }

        private static string GetRendererPathKey(Transform root, Renderer renderer)
        {
            if (root == null || renderer == null)
                return string.Empty;

            Transform current = renderer.transform;
            string path = renderer.GetType().FullName;
            while (current != null && current != root)
            {
                path = current.GetSiblingIndex() + "/" + current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static float GetSequenceDuration(ITrackSequence sequence)
        {
            float duration = 0f;
            if (sequence == null || sequence.Tracks == null)
                return 0.01f;

            foreach (var track in sequence.Tracks)
            {
                if (track == null || track.Clips == null)
                    continue;

                foreach (var clip in track.Clips)
                {
                    if (clip == null)
                        continue;

                    duration = Mathf.Max(duration, clip.StartTime + Mathf.Max(0f, clip.DurationTime));
                }
            }

            return Mathf.Max(0.01f, duration);
        }
        private void EditorPreviewDrawPreviewGUIImpl()
        {
            var sm = stateMachine;
            if (sm == null)
            {
                UnityEditor.EditorGUILayout.HelpBox("没有 StateMachine", UnityEditor.MessageType.Warning);
                return;
            }

            double now = UnityEditor.EditorApplication.timeSinceStartup;
            CleanupPreviewStateTraces(now);
            UnityEditor.EditorGUILayout.LabelField("状态机运行时预览", UnityEditor.EditorStyles.boldLabel);
            var layers = sm.LayerRuntimes;
            if (layers == null) return;

            int layerCount = 0;
            int enabledLayerCount = 0;
            int totalRunningCount = 0;
            int totalConnectedCount = 0;
            foreach (var layer in layers)
            {
                if (layer == null) continue;
                layerCount++;
                if (layer.isEnabled) enabledLayerCount++;
                totalRunningCount += layer.runningStates != null ? layer.runningStates.Count : 0;
                totalConnectedCount += layer.stateToSlotMap != null ? layer.stateToSlotMap.Count : 0;
            }

            UnityEditor.EditorGUILayout.BeginVertical(UnityEditor.EditorStyles.helpBox);
            UnityEditor.EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"注册状态: {sm.RegisteredStateCount}", UnityEditor.EditorStyles.miniBoldLabel, GUILayout.Width(95));
            GUILayout.Label($"运行状态: {totalRunningCount}", UnityEditor.EditorStyles.miniBoldLabel, GUILayout.Width(95));
            GUILayout.Label($"动画节点: {totalConnectedCount}", UnityEditor.EditorStyles.miniBoldLabel, GUILayout.Width(95));
            GUILayout.Label($"启用层级: {enabledLayerCount}/{layerCount}", UnityEditor.EditorStyles.miniBoldLabel, GUILayout.Width(105));
            GUILayout.Label($"压制关系: {sm.WeakInterruptRelationCount}", UnityEditor.EditorStyles.miniBoldLabel);
            UnityEditor.EditorGUILayout.EndHorizontal();
            UnityEditor.EditorGUILayout.BeginHorizontal();
            bool traceEnabled = UnityEditor.EditorGUILayout.ToggleLeft(
                $"状态留痕 {PreviewStateTraceHoldSeconds:F1}s",
                _previewStateTraceEnabled,
                GUILayout.Width(120));
            if (traceEnabled != _previewStateTraceEnabled)
            {
                _previewStateTraceEnabled = traceEnabled;
                if (!_previewStateTraceEnabled)
                    _previewStateTraces.Clear();
            }
            GUILayout.Label($"留痕缓存: {(_previewStateTraceEnabled ? _previewStateTraces.Count : 0)}/{PreviewStateTraceMaxCount}", UnityEditor.EditorStyles.miniLabel);
            UnityEditor.EditorGUILayout.EndHorizontal();
            if (sm.WeakInterruptRelationCount > 0)
                UnityEditor.EditorGUILayout.HelpBox(sm.GetWeakInterruptSummary(), UnityEditor.MessageType.None);
            UnityEditor.EditorGUILayout.EndVertical();

            foreach (var layer in layers)
            {
                if (layer == null) continue;

                if (!layerFoldouts.ContainsKey(layer.layerType))
                    layerFoldouts[layer.layerType] = true;

                int runningCount = layer.runningStates.Count;
                int slotCount = layer.stateToSlotMap.Count;
                bool foldout = UnityEditor.EditorGUILayout.Foldout(
                    layerFoldouts[layer.layerType],
                    $"层级: {layer.layerType}    层权重: {layer.weight:F2}    运行: {runningCount}/{slotCount}",
                    true);
                layerFoldouts[layer.layerType] = foldout;
                if (!foldout) continue;

                UnityEditor.EditorGUILayout.BeginVertical(UnityEditor.EditorStyles.helpBox);

                if (slotCount == 0)
                {
                    UnityEditor.EditorGUILayout.LabelField("当前层级没有已连接状态", UnityEditor.EditorStyles.miniLabel);
                    UnityEditor.EditorGUILayout.EndVertical();
                    continue;
                }

                var headerStyle = new GUIStyle(UnityEditor.EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.62f, 0.62f, 0.62f) },
                    fontStyle = FontStyle.Bold
                };
                UnityEditor.EditorGUILayout.BeginHorizontal();
                GUILayout.Space(4);
                GUILayout.Label("状态", headerStyle, GUILayout.Width(145));
                GUILayout.Label("阶段", headerStyle, GUILayout.Width(55));
                GUILayout.Label("权重", headerStyle, GUILayout.Width(35));
                GUILayout.Label("混合权重", headerStyle, GUILayout.MinWidth(90), GUILayout.ExpandWidth(true));
                GUILayout.Label("状态", headerStyle, GUILayout.Width(48));
                GUILayout.Label("过渡", headerStyle, GUILayout.Width(35));
                UnityEditor.EditorGUILayout.EndHorizontal();

                foreach (var kvp in layer.stateToSlotMap)
                {
                    var state = kvp.Key;
                    int slot = kvp.Value;
                    if (state == null) continue;

                    float weight = 0f;
                    if (layer.mixer.IsValid() && slot >= 0 && slot < layer.mixer.GetInputCount())
                        weight = layer.mixer.GetInputWeight(slot);

                    bool isActive = layer.runningStates.Contains(state);
                    bool isFadingIn = layer.fadeInStates.ContainsKey(state);
                    bool isFadingOut = layer.fadeOutStates.ContainsKey(state);
                    bool isSuppressed = sm.IsStateWeakSuppressed(state);

                    if (_previewStateTraceEnabled)
                        UpdatePreviewStateTrace(state, layer.layerType, slot, weight, isSuppressed, now);
                    DrawPreviewStateRow(
                        state,
                        state.strKey ?? state.GetType().Name,
                        state.RuntimePhase,
                        slot,
                        weight,
                        isActive,
                        isFadingIn,
                        isFadingOut,
                        isSuppressed,
                        false,
                        0f);
                }

                if (_previewStateTraceEnabled)
                {
                    foreach (var trace in _previewStateTraces.Values)
                    {
                        if (trace == null || trace.state == null || trace.layerType != layer.layerType)
                            continue;

                        if (layer.stateToSlotMap.ContainsKey(trace.state))
                            continue;

                        double age = now - trace.lastSeenTime;
                        if (age < 0d || age > PreviewStateTraceHoldSeconds)
                            continue;

                        float fadedWeight = trace.lastWeight * Mathf.Clamp01(1f - (float)(age / PreviewStateTraceHoldSeconds));
                        DrawPreviewStateRow(
                            trace.state,
                            trace.displayName,
                            trace.lastPhase,
                            trace.lastSlot,
                            fadedWeight,
                            false,
                            false,
                            false,
                            trace.wasSuppressed,
                            true,
                            (float)age);
                    }
                }

                UnityEditor.EditorGUILayout.EndVertical();
            }
        }

        private void UpdatePreviewStateTrace(StateBase state, StateLayerType layerType, int slot, float weight, bool isSuppressed, double now)
        {
            if (state == null)
                return;

            if (!_previewStateTraces.TryGetValue(state, out var trace) || trace == null)
            {
                TrimPreviewStateTracesToLimit();
                trace = new PreviewStateTrace
                {
                    state = state,
                    firstSeenTime = now
                };
                _previewStateTraces[state] = trace;
            }

            trace.layerType = layerType;
            trace.displayName = state.strKey ?? state.GetType().Name;
            trace.lastWeight = weight;
            trace.lastSlot = slot;
            trace.lastSeenTime = now;
            trace.lastPhase = state.RuntimePhase;
            trace.wasSuppressed = isSuppressed;
        }

        private void TrimPreviewStateTracesToLimit()
        {
            if (_previewStateTraces.Count < PreviewStateTraceMaxCount)
                return;

            StateBase oldestState = null;
            double oldestTime = double.MaxValue;
            foreach (var kvp in _previewStateTraces)
            {
                var trace = kvp.Value;
                double lastSeen = trace != null ? trace.lastSeenTime : double.MinValue;
                if (lastSeen >= oldestTime)
                    continue;

                oldestTime = lastSeen;
                oldestState = kvp.Key;
            }

            if (oldestState != null)
                _previewStateTraces.Remove(oldestState);
        }

        private void CleanupPreviewStateTraces(double now)
        {
            if (!_previewStateTraceEnabled)
                return;

            _previewTraceRemoveBuffer.Clear();
            foreach (var kvp in _previewStateTraces)
            {
                var trace = kvp.Value;
                if (trace == null || trace.state == null || now - trace.lastSeenTime > PreviewStateTraceCleanupSeconds)
                    _previewTraceRemoveBuffer.Add(kvp.Key);
            }

            for (int i = 0; i < _previewTraceRemoveBuffer.Count; i++)
                _previewStateTraces.Remove(_previewTraceRemoveBuffer[i]);
        }

        private static void DrawPreviewStateRow(
            StateBase state,
            string displayName,
            StateRuntimePhase phase,
            int slot,
            float weight,
            bool isActive,
            bool isFadingIn,
            bool isFadingOut,
            bool isSuppressed,
            bool isTrace,
            float traceAge)
        {
            float alpha = isTrace ? Mathf.Clamp01(1f - traceAge / (float)PreviewStateTraceHoldSeconds) * 0.72f : 1f;
            var nameStyle = new GUIStyle(UnityEditor.EditorStyles.label);
            if (isActive) nameStyle.fontStyle = FontStyle.Bold;
            if (isTrace)
                nameStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, alpha);
            else if (isFadingIn)
                nameStyle.normal.textColor = new Color(0.55f, 0.9f, 0.62f);
            else if (isFadingOut)
                nameStyle.normal.textColor = new Color(1f, 0.58f, 0.52f);
            else if (isSuppressed)
                nameStyle.normal.textColor = new Color(0.62f, 0.82f, 1f);

            UnityEditor.EditorGUILayout.BeginHorizontal();

            var markerRect = UnityEditor.EditorGUILayout.GetControlRect(GUILayout.Width(4), GUILayout.Height(18));
            Color markerColor = isTrace
                ? new Color(0.6f, 0.6f, 0.6f, alpha)
                : isSuppressed
                    ? new Color(0.18f, 0.55f, 0.95f)
                    : isActive
                        ? new Color(0.32f, 0.78f, 0.42f)
                        : new Color(0.25f, 0.25f, 0.25f);
            UnityEditor.EditorGUI.DrawRect(markerRect, markerColor);

            string tooltip = state != null
                ? $"激活时间: {state.activationTime:F2}\n持续时间: {state.hasEnterTime:F2}s\n槽位: {slot}"
                : $"槽位: {slot}";
            if (isTrace)
                tooltip += $"\n预览留痕: {traceAge:F1}s";
            GUILayout.Label(new GUIContent(displayName, tooltip), nameStyle, GUILayout.Width(145));

            Color phaseColor = phase switch
            {
                StateRuntimePhase.Pre => new Color(0.45f, 0.78f, 1f, alpha),
                StateRuntimePhase.Main => new Color(0.42f, 0.9f, 0.48f, alpha),
                StateRuntimePhase.Wait => new Color(1f, 0.78f, 0.28f, alpha),
                StateRuntimePhase.Released => new Color(0.55f, 0.55f, 0.55f, alpha),
                _ => new Color(1f, 1f, 1f, alpha)
            };
            var phaseStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = phaseColor },
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label(phase.ToString(), phaseStyle, GUILayout.Width(55));

            UnityEditor.EditorGUILayout.LabelField($"{weight:F2}", GUILayout.Width(35));

            var barRect = UnityEditor.EditorGUILayout.GetControlRect(
                GUILayout.MinWidth(90), GUILayout.Height(18), GUILayout.ExpandWidth(true));
            var trackRect = new Rect(barRect.x, barRect.y + 3, barRect.width, 12);
            UnityEditor.EditorGUI.DrawRect(trackRect, new Color(0.12f, 0.12f, 0.12f, alpha));

            float fillFactor = Mathf.Clamp01(weight);
            var fillRect = new Rect(trackRect.x, trackRect.y, trackRect.width * fillFactor, trackRect.height);
            Color fillColor = isTrace
                ? new Color(0.45f, 0.45f, 0.45f, alpha)
                : isSuppressed
                    ? Color.Lerp(new Color(0.16f, 0.28f, 0.42f), new Color(0.25f, 0.58f, 0.92f), fillFactor)
                    : Color.Lerp(new Color(0.18f, 0.32f, 0.2f), new Color(0.38f, 0.82f, 0.42f), fillFactor);
            UnityEditor.EditorGUI.DrawRect(fillRect, fillColor);

            if (isSuppressed && !isTrace)
            {
                var capRect = new Rect(trackRect.xMax - 3, trackRect.y, 3, trackRect.height);
                UnityEditor.EditorGUI.DrawRect(capRect, new Color(0.35f, 0.68f, 1f));
            }

            Color borderColor = isTrace
                ? new Color(0.42f, 0.42f, 0.42f, alpha)
                : isSuppressed ? new Color(0.38f, 0.62f, 0.9f) : new Color(0.36f, 0.36f, 0.36f);
            UnityEditor.EditorGUI.DrawRect(new Rect(trackRect.x, trackRect.y, trackRect.width, 1), borderColor);
            UnityEditor.EditorGUI.DrawRect(new Rect(trackRect.x, trackRect.yMax - 1, trackRect.width, 1), borderColor);
            UnityEditor.EditorGUI.DrawRect(new Rect(trackRect.x, trackRect.y, 1, trackRect.height), borderColor);
            UnityEditor.EditorGUI.DrawRect(new Rect(trackRect.xMax - 1, trackRect.y, 1, trackRect.height), borderColor);

            GUIStyle percentStyle = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.92f, 0.92f, 0.92f, alpha) },
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(trackRect, $"{weight:P0}", percentStyle);

            var badgeStyle = new GUIStyle(UnityEditor.EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            if (isTrace)
            {
                badgeStyle.normal.textColor = new Color(0.78f, 0.78f, 0.78f, alpha);
                GUILayout.Label("刚退出", badgeStyle, GUILayout.Width(48));
            }
            else if (isSuppressed)
            {
                badgeStyle.normal.textColor = new Color(0.58f, 0.78f, 1f);
                GUILayout.Label("被压制", badgeStyle, GUILayout.Width(48));
            }
            else if (isActive)
            {
                badgeStyle.normal.textColor = new Color(0.58f, 0.9f, 0.62f);
                GUILayout.Label("运行", badgeStyle, GUILayout.Width(48));
            }
            else
            {
                GUILayout.Space(48);
            }

            if (isFadingIn)
                GUILayout.Label("淡入", UnityEditor.EditorStyles.miniLabel, GUILayout.Width(35));
            else if (isFadingOut)
                GUILayout.Label("淡出", UnityEditor.EditorStyles.miniLabel, GUILayout.Width(35));
            else if (isTrace)
                GUILayout.Label($"{traceAge:F1}s", UnityEditor.EditorStyles.miniLabel, GUILayout.Width(35));
            else
                GUILayout.Space(35);

            UnityEditor.EditorGUILayout.EndHorizontal();
        }
#endif

    }
}
