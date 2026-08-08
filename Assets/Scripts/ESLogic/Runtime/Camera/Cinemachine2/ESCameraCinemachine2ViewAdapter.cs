using System;
using System.Collections.Generic;
using Cinemachine;
using Unity.Profiling;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// CM2 的唯一写入边界。除这个类型外，ES 业务代码不得改写正式 VCam 的
    /// Priority、Follow、LookAt 或 Axis。
    /// </summary>
    internal sealed class ESCameraCinemachine2ViewAdapter : IESCameraViewAdapter, IDisposable
    {
        private const int ActivePriority = 100;
        private const int StandbyPriority = 0;
        private static readonly ProfilerMarker ApplyMarker = new ProfilerMarker("ES.Camera.CM2.Apply");
        private static readonly ProfilerMarker WarmupMarker = new ProfilerMarker("ES.Camera.CM2.Warmup");

        private readonly Camera outputCamera;
        private readonly CinemachineBrain brain;
        private readonly ESCameraViewDefinitionCatalog definitionCatalog;
        private readonly ESCameraSceneRigRegistry rigs;
        private readonly ESInputSchemeResolver inputSchemeResolver;
        private readonly HashSet<string> reportedDefinitionErrors = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<CinemachineVirtualCameraBase, RigModifierBaseline> modifierBaselines = new Dictionary<CinemachineVirtualCameraBase, RigModifierBaseline>(4);

        private CinemachineVirtualCameraBase activeRig;
        private CinemachinePOV activePov;
        private CinemachineCollider activeObstruction;
        private ESCameraViewDefinition activeDefinition;
        private ESCameraDefinitionRuntimeHandle activeDefinitionHandle;
        private ESCameraResolvedModifiers appliedModifiers;
        private bool hasAppliedModifiers;
        private bool lookInputUsesRate;
        private bool disposed;

        public ESCameraCinemachine2ViewAdapter(
            Camera outputCamera,
            CinemachineBrain brain,
            ESCameraViewDefinitionCatalog definitionCatalog,
            ESCameraRigCatalog rigCatalog,
            Transform rigRoot)
        {
            this.outputCamera = outputCamera;
            this.brain = brain;
            this.definitionCatalog = definitionCatalog;
            rigs = new ESCameraSceneRigRegistry(rigCatalog, rigRoot);
            inputSchemeResolver = ESGameManager.InputModule?.SchemeResolver;
            UpdateLookInputMode(inputSchemeResolver != null ? inputSchemeResolver.ActiveSchemeId : null);
            if (inputSchemeResolver != null)
                inputSchemeResolver.SchemeChanged += OnInputSchemeChanged;
        }

        public bool IsReady
        {
            get
            {
                return !disposed
                       && outputCamera != null
                       && brain != null
                       && brain.gameObject == outputCamera.gameObject
                       && definitionCatalog != null
                       && definitionCatalog.IsValid
                       && rigs.IsReady;
            }
        }

        public Transform OutputTransform => outputCamera != null ? outputCamera.transform : null;

        public bool TryResolveDefinition(ESCameraDefinitionReference reference, out ESCameraDefinitionRuntimeHandle handle)
        {
            handle = default;
            return IsReady && definitionCatalog.TryResolve(reference, out handle);
        }

        public bool Apply(in ESCameraResolvedView resolved)
        {
            using (ApplyMarker.Auto())
            {
            if (!IsReady || !resolved.hasWinner)
            {
                Clear();
                return false;
            }

            ESCameraViewDefinition definition = activeDefinition;
            CinemachineVirtualCameraBase rig = activeRig;
            if (resolved.configurationChanged)
            {
                bool definitionChanged = activeDefinition == null
                                      || activeDefinitionHandle != resolved.definitionHandle;
                if (definitionChanged)
                {
                    if (!definitionCatalog.TryGet(resolved.definitionHandle, out definition))
                    {
                        ReportDefinitionError(resolved.definition.ToString(), "Definition 引用未能解析到当前 Catalog 生命周期。");
                        Clear();
                        return false;
                    }

                    if (!rigs.TryGetRig(definition.rigKey, out rig))
                    {
                        ReportDefinitionError(resolved.definition.ToString(), $"RigKey '{definition.rigKey}' 未能解析为唯一 Cinemachine Virtual Camera。");
                        Clear();
                        return false;
                    }
                }

                if (activeRig != rig)
                {
                    Deactivate(activeRig, activeObstruction);
                    activeRig = rig;
                    RigModifierBaseline baseline = GetOrCreateModifierBaseline(activeRig);
                    activePov = baseline.pov;
                    activeObstruction = baseline.obstruction;
                    activeRig.PreviousStateIsValid = false;
                    activeRig.Priority = ActivePriority;
                    hasAppliedModifiers = false;
                }

                // A Rig can be shared by several view definitions. Obstruction and input configuration are
                // definition policy, so a winner change must refresh them even when the VCam instance is unchanged.
                if (definitionChanged)
                {
                    ConfigureRigForDirector(activeRig, activePov, activeObstruction, definition);
                    activeDefinition = definition;
                    activeDefinitionHandle = resolved.definitionHandle;
                    hasAppliedModifiers = false;
                }

                if (activeRig.Follow != resolved.follow)
                    activeRig.Follow = resolved.follow;
                if (activeRig.LookAt != resolved.lookAt)
                    activeRig.LookAt = resolved.lookAt;

                if (!hasAppliedModifiers || !ModifiersEqual(appliedModifiers, resolved.modifiers))
                {
                    ApplyModifiers(activeRig, definition, resolved.modifiers);
                    appliedModifiers = resolved.modifiers;
                    hasAppliedModifiers = true;
                }
            }

            if (resolved.hasLookInput && activeRig != null && definition != null)
                ApplyLook(activeRig, activePov, resolved.lookInput, definition);
            return activeRig != null && activeDefinition != null;
            }
        }

        public void Clear()
        {
            Deactivate(activeRig, activeObstruction);
            activeRig = null;
            activePov = null;
            activeObstruction = null;
            activeDefinition = null;
            activeDefinitionHandle = default;
            appliedModifiers = ESCameraResolvedModifiers.Identity;
            hasAppliedModifiers = false;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            if (inputSchemeResolver != null)
                inputSchemeResolver.SchemeChanged -= OnInputSchemeChanged;
            Clear();
            rigs.Dispose();
            reportedDefinitionErrors.Clear();
            modifierBaselines.Clear();
        }

        public void Warmup()
        {
            if (!IsReady)
                return;

            using (WarmupMarker.Auto())
                rigs.Warmup(PrimeModifierBaseline);
        }

        /// <summary>
        /// 预热不仅实例化 Rig，也建立 Modifier 基线。这样首个真实镜头请求不会因为
        /// 首次读取 Orbit/Offset/Noise 基线而产生额外工作或遗漏制作期结构错误。
        /// </summary>
        private void PrimeModifierBaseline(CinemachineVirtualCameraBase rig)
        {
            if (rig != null)
                GetOrCreateModifierBaseline(rig);
        }

        private void ReportDefinitionError(string definitionKey, string detail)
        {
            string key = string.IsNullOrWhiteSpace(definitionKey) ? "<empty>" : definitionKey;
            if (reportedDefinitionErrors.Add(key))
                Debug.LogError($"[ESCamera] View '{outputCamera.name}' 无法应用相机定义 '{key}'：{detail}", outputCamera);
        }

        private static void Deactivate(CinemachineVirtualCameraBase rig, CinemachineCollider obstruction)
        {
            if (rig == null)
                return;

            rig.Priority = StandbyPriority;
            rig.Follow = null;
            rig.LookAt = null;
            if (obstruction != null)
                obstruction.enabled = false;
        }

        private static void ConfigureRigForDirector(
            CinemachineVirtualCameraBase rig,
            CinemachinePOV pov,
            CinemachineCollider obstruction,
            ESCameraViewDefinition definition)
        {
            if (rig is CinemachineFreeLook freeLook)
            {
                DisableLegacyInput(ref freeLook.m_XAxis);
                DisableLegacyInput(ref freeLook.m_YAxis);
                ConfigureObstruction(rig, obstruction, definition);
                return;
            }

            if (pov != null)
            {
                DisableLegacyInput(ref pov.m_HorizontalAxis);
                DisableLegacyInput(ref pov.m_VerticalAxis);
            }

            ConfigureObstruction(rig, obstruction, definition);
        }

        private static void ConfigureObstruction(
            CinemachineVirtualCameraBase rig,
            CinemachineCollider obstruction,
            ESCameraViewDefinition definition)
        {
            if (definition == null || !definition.enableObstruction)
            {
                if (obstruction != null)
                    obstruction.enabled = false;
                return;
            }

            if (obstruction == null)
            {
                Debug.LogError(
                    "[ESCamera] 相机定义 '" + definition.Definition + "' 启用了避障，但 Rig '" + rig.name
                    + "' 缺少 CinemachineCollider。请在制作期补齐 Rig。",
                    rig);
                return;
            }

            obstruction.enabled = true;
            obstruction.m_CollideAgainst = definition.obstructionMask;
            obstruction.m_CameraRadius = Mathf.Max(0.01f, definition.obstructionCameraRadius);
            obstruction.m_MinimumDistanceFromTarget = Mathf.Max(0.01f, definition.obstructionMinimumDistance);
            obstruction.m_MaximumEffort = Mathf.Clamp(definition.obstructionMaximumEffort, 1, 8);
            obstruction.m_Damping = Mathf.Max(0f, definition.obstructionDamping);
            obstruction.m_DampingWhenOccluded = Mathf.Max(0f, definition.obstructionDampingWhenOccluded);
        }

        private static void DisableLegacyInput(ref AxisState axis)
        {
            axis.m_InputAxisName = string.Empty;
            axis.m_InputAxisValue = 0f;
            axis.SetInputAxisProvider(0, null);
        }

        private void OnInputSchemeChanged(string _, string current)
        {
            UpdateLookInputMode(current);
        }

        private void UpdateLookInputMode(string schemeId)
        {
            lookInputUsesRate = string.IsNullOrEmpty(schemeId)
                                || string.Equals(schemeId, ESInputSchemeIds.Gamepad, StringComparison.Ordinal);
        }

        private void ApplyLook(
            CinemachineVirtualCameraBase rig,
            CinemachinePOV pov,
            Vector2 lookInput,
            ESCameraViewDefinition definition)
        {
            float inputStep = ResolveLookInputStep(definition);
            float verticalSign = definition.invertVerticalLook ? 1f : -1f;
            if (rig is CinemachineFreeLook freeLook)
            {
                ApplyAxisDelta(ref freeLook.m_XAxis, lookInput.x * definition.freeLookSensitivity.x * inputStep);
                ApplyAxisDelta(ref freeLook.m_YAxis, lookInput.y * definition.freeLookSensitivity.y * verticalSign * inputStep);
                return;
            }

            if (pov == null)
                return;

            ApplyAxisDelta(ref pov.m_HorizontalAxis, lookInput.x * definition.povLookSensitivity.x * inputStep);
            ApplyAxisDelta(ref pov.m_VerticalAxis, lookInput.y * definition.povLookSensitivity.y * verticalSign * inputStep);
        }

        private float ResolveLookInputStep(ESCameraViewDefinition definition)
        {
            return lookInputUsesRate
                ? Time.deltaTime
                : Mathf.Max(0.0001f, definition.pointerLookScale);
        }

        private static bool ModifiersEqual(ESCameraResolvedModifiers left, ESCameraResolvedModifiers right)
        {
            return ScalarCompositionEqual(left.fieldOfView, right.fieldOfView)
                   && ScalarCompositionEqual(left.distanceScale, right.distanceScale)
                   && VectorCompositionEqual(left.shoulderOffset, right.shoulderOffset)
                   && ScalarCompositionEqual(left.shakeAmplitude, right.shakeAmplitude);
        }

        private static bool ScalarCompositionEqual(ESCameraScalarComposition left, ESCameraScalarComposition right)
        {
            return left.hasOverride == right.hasOverride
                   && left.overrideValue == right.overrideValue
                   && left.additiveValue == right.additiveValue
                   && left.multiplier == right.multiplier;
        }

        private static bool VectorCompositionEqual(ESCameraVectorComposition left, ESCameraVectorComposition right)
        {
            return left.hasOverride == right.hasOverride
                   && left.overrideValue == right.overrideValue
                   && left.additiveValue == right.additiveValue;
        }

        private static void ApplyAxisDelta(ref AxisState axis, float delta)
        {
            float value = axis.Value + delta;
            float range = axis.m_MaxValue - axis.m_MinValue;
            if (axis.m_Wrap && range > Mathf.Epsilon)
                axis.Value = Mathf.Repeat(value - axis.m_MinValue, range) + axis.m_MinValue;
            else
                axis.Value = Mathf.Clamp(value, axis.m_MinValue, axis.m_MaxValue);
        }

        private void ApplyModifiers(
            CinemachineVirtualCameraBase rig,
            ESCameraViewDefinition definition,
            ESCameraResolvedModifiers modifiers)
        {
            RigModifierBaseline baseline = GetOrCreateModifierBaseline(rig);
            float fieldOfView = Mathf.Clamp(modifiers.fieldOfView.Apply(definition.baseFieldOfView), 1f, 179f);
            float distanceScale = Mathf.Max(0.01f, modifiers.distanceScale.Apply(definition.baseDistanceScale));
            Vector3 shoulderOffset = modifiers.shoulderOffset.Apply(definition.baseShoulderOffset);
            float shakeAmplitude = Mathf.Max(0f, modifiers.shakeAmplitude.Apply(definition.baseShakeAmplitude));

            if (rig is CinemachineFreeLook freeLook)
            {
                LensSettings lens = freeLook.m_Lens;
                lens.FieldOfView = fieldOfView;
                freeLook.m_Lens = lens;
                ApplyFreeLookDistance(freeLook, baseline, distanceScale);
            }
            else if (rig is CinemachineVirtualCamera virtualCamera)
            {
                LensSettings lens = virtualCamera.m_Lens;
                lens.FieldOfView = fieldOfView;
                virtualCamera.m_Lens = lens;
            }

            if (baseline.cameraOffset != null)
                baseline.cameraOffset.m_Offset = baseline.cameraOffsetBase + shoulderOffset;
            if (baseline.noise != null)
                baseline.noise.m_AmplitudeGain = baseline.noiseAmplitudeBase + shakeAmplitude;
        }

        private RigModifierBaseline GetOrCreateModifierBaseline(CinemachineVirtualCameraBase rig)
        {
            if (modifierBaselines.TryGetValue(rig, out RigModifierBaseline baseline))
                return baseline;

            baseline = new RigModifierBaseline(true);
            baseline.pov = rig is CinemachineFreeLook ? null : rig.GetComponent<CinemachinePOV>();
            baseline.obstruction = rig.GetComponent<CinemachineCollider>();
            baseline.cameraOffset = rig.GetComponent<CinemachineCameraOffset>();
            if (baseline.cameraOffset == null)
            {
                Debug.LogError(
                    "[ESCamera] Rig '" + rig.name
                    + "' 缺少 CinemachineCameraOffset；Rig 必须在制作期补齐，运行时不会修改 Rig 结构。",
                    rig);
            }
            baseline.cameraOffsetBase = baseline.cameraOffset != null
                ? baseline.cameraOffset.m_Offset
                : Vector3.zero;

            baseline.noise = rig.GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (baseline.noise != null)
                baseline.noiseAmplitudeBase = baseline.noise.m_AmplitudeGain;

            if (rig is CinemachineFreeLook freeLook && freeLook.m_Orbits != null)
            {
                int count = Mathf.Min(freeLook.m_Orbits.Length, 3);
                for (int i = 0; i < count; i++)
                {
                    baseline.SetOrbit(i, freeLook.m_Orbits[i].m_Height, freeLook.m_Orbits[i].m_Radius);
                }
                baseline.orbitCount = count;
            }

            modifierBaselines.Add(rig, baseline);
            return baseline;
        }

        private static void ApplyFreeLookDistance(CinemachineFreeLook freeLook, RigModifierBaseline baseline, float distanceScale)
        {
            if (baseline.orbitCount == 0 || freeLook.m_Orbits == null)
                return;

            int count = Mathf.Min(baseline.orbitCount, freeLook.m_Orbits.Length);
            for (int i = 0; i < count; i++)
            {
                CinemachineFreeLook.Orbit orbit = freeLook.m_Orbits[i];
                orbit.m_Height = baseline.GetOrbitHeight(i);
                orbit.m_Radius = Mathf.Max(0.01f, baseline.GetOrbitRadius(i) * distanceScale);
                freeLook.m_Orbits[i] = orbit;
            }
        }

        private struct RigModifierBaseline
        {
            public CinemachinePOV pov;
            public CinemachineCollider obstruction;
            public CinemachineCameraOffset cameraOffset;
            public Vector3 cameraOffsetBase;
            public CinemachineBasicMultiChannelPerlin noise;
            public float noiseAmplitudeBase;
            public int orbitCount;
            private float orbitHeight0;
            private float orbitHeight1;
            private float orbitHeight2;
            private float orbitRadius0;
            private float orbitRadius1;
            private float orbitRadius2;

            public RigModifierBaseline(bool initialize)
            {
                pov = null;
                obstruction = null;
                cameraOffset = null;
                cameraOffsetBase = Vector3.zero;
                noise = null;
                noiseAmplitudeBase = 0f;
                orbitCount = 0;
                orbitHeight0 = 0f;
                orbitHeight1 = 0f;
                orbitHeight2 = 0f;
                orbitRadius0 = 0f;
                orbitRadius1 = 0f;
                orbitRadius2 = 0f;
            }

            public void SetOrbit(int index, float height, float radius)
            {
                switch (index)
                {
                    case 0:
                        orbitHeight0 = height;
                        orbitRadius0 = radius;
                        break;
                    case 1:
                        orbitHeight1 = height;
                        orbitRadius1 = radius;
                        break;
                    case 2:
                        orbitHeight2 = height;
                        orbitRadius2 = radius;
                        break;
                }
            }

            public float GetOrbitHeight(int index)
            {
                switch (index)
                {
                    case 0: return orbitHeight0;
                    case 1: return orbitHeight1;
                    case 2: return orbitHeight2;
                    default: return 0f;
                }
            }

            public float GetOrbitRadius(int index)
            {
                switch (index)
                {
                    case 0: return orbitRadius0;
                    case 1: return orbitRadius1;
                    case 2: return orbitRadius2;
                    default: return 0f;
                }
            }
        }
    }

    /// <summary>
    /// 仅属于一个 SceneBinding 的 Rig 实例仓。Catalog 只给 Prefab；这里才会创建当前
    /// 场景实例，因此 Catalog 永远不会持有跨场景 VCam 引用。
    /// </summary>
    internal sealed class ESCameraSceneRigRegistry : IDisposable
    {
        private readonly ESCameraRigCatalog catalog;
        private readonly Transform rigRoot;
        private readonly Dictionary<string, CinemachineVirtualCameraBase> instances = new Dictionary<string, CinemachineVirtualCameraBase>(StringComparer.Ordinal);
        private bool disposed;

        public ESCameraSceneRigRegistry(ESCameraRigCatalog catalog, Transform rigRoot)
        {
            this.catalog = catalog;
            this.rigRoot = rigRoot;
        }

        public bool IsReady => !disposed && catalog != null && catalog.IsValid && rigRoot != null;

        public bool TryGetRig(string rigKey, out CinemachineVirtualCameraBase rig)
        {
            rig = null;
            if (!IsReady || string.IsNullOrWhiteSpace(rigKey))
                return false;

            if (instances.TryGetValue(rigKey, out rig) && rig != null)
                return true;

            instances.Remove(rigKey);
            if (!catalog.TryGetPrefab(rigKey, out GameObject prefab))
                return false;

            GameObject instance = UnityEngine.Object.Instantiate(prefab, rigRoot);
            instance.name = rigKey;
            // A catalog Rig is one logical VCam hosted on the prefab root.  CinemachineFreeLook
            // internally creates Top/Middle/Bottom child rigs which also derive from
            // CinemachineVirtualCameraBase; those implementation details must not become
            // separate ES Camera Rigs.
            CinemachineVirtualCameraBase[] cameras = instance.GetComponents<CinemachineVirtualCameraBase>();
            if (cameras.Length != 1 || cameras[0] == null)
            {
                Debug.LogError(
                    $"[ESCamera] Rig '{rigKey}' 的 Prefab 根节点必须且只能挂载一个 CinemachineVirtualCameraBase。"
                    + "CinemachineFreeLook 的内部子 Rig 不计入此契约。",
                    instance);
                UnityEngine.Object.Destroy(instance);
                return false;
            }

            rig = cameras[0];
            rig.Priority = 0;
            rig.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Never;
            rig.PreviousStateIsValid = false;
            rig.Follow = null;
            rig.LookAt = null;
            instances.Add(rigKey, rig);
            return true;
        }

        public void Warmup(Action<CinemachineVirtualCameraBase> onRigWarmed = null)
        {
            if (!IsReady)
                return;

            int count = catalog.EntryCount;
            for (int i = 0; i < count; i++)
            {
                if (catalog.TryGetEntry(i, out string rigKey, out _)
                    && TryGetRig(rigKey, out CinemachineVirtualCameraBase rig))
                {
                    onRigWarmed?.Invoke(rig);
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            foreach (CinemachineVirtualCameraBase rig in instances.Values)
            {
                if (rig != null)
                    UnityEngine.Object.Destroy(rig.gameObject);
            }

            instances.Clear();
        }
    }
}
