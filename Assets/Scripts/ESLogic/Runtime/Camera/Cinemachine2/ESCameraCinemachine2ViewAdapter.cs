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
        private readonly ESCameraProfileCatalog profiles;
        private readonly ESCameraSceneRigRegistry rigs;
        private readonly HashSet<string> reportedProfileErrors = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<CinemachineVirtualCameraBase, RigModifierBaseline> modifierBaselines = new Dictionary<CinemachineVirtualCameraBase, RigModifierBaseline>(4);

        private CinemachineVirtualCameraBase activeRig;
        private string activeProfileKey;
        private bool disposed;

        public ESCameraCinemachine2ViewAdapter(
            Camera outputCamera,
            CinemachineBrain brain,
            ESCameraProfileCatalog profiles,
            ESCameraRigCatalog rigCatalog,
            Transform rigRoot)
        {
            this.outputCamera = outputCamera;
            this.brain = brain;
            this.profiles = profiles;
            rigs = new ESCameraSceneRigRegistry(rigCatalog, rigRoot);
        }

        public bool IsReady
        {
            get
            {
                return !disposed
                       && outputCamera != null
                       && brain != null
                       && brain.gameObject == outputCamera.gameObject
                       && profiles != null
                       && rigs.IsReady;
            }
        }

        public Transform OutputTransform => outputCamera != null ? outputCamera.transform : null;

        public void Apply(in ESCameraResolvedView resolved)
        {
            using (ApplyMarker.Auto())
            {
            if (!IsReady || !resolved.hasWinner)
            {
                Clear();
                return;
            }

            if (!profiles.TryGet(resolved.profileKey, out ESCameraProfile profile))
            {
                ReportProfileError(resolved.profileKey, "ProfileKey 未被当前 View 的 ProfileCatalog 收录。");
                Clear();
                return;
            }

            if (!rigs.TryGetRig(profile.rigKey, out CinemachineVirtualCameraBase rig))
            {
                ReportProfileError(resolved.profileKey, $"RigKey '{profile.rigKey}' 未能解析为唯一 Cinemachine Virtual Camera。");
                Clear();
                return;
            }

            if (activeRig != rig)
            {
                Deactivate(activeRig);
                activeRig = rig;
                activeRig.Priority = ActivePriority;
            }

            // A Rig can be shared by several Profiles. Obstruction and input configuration are
            // Profile policy, so a winner change must refresh them even when the VCam instance is unchanged.
            if (!string.Equals(activeProfileKey, profile.profileKey, StringComparison.Ordinal))
            {
                ConfigureRigForDirector(activeRig, profile);
                activeProfileKey = profile.profileKey;
            }

            activeRig.Follow = resolved.follow;
            activeRig.LookAt = resolved.lookAt;
            ApplyModifiers(activeRig, profile, resolved.modifiers);

            if (resolved.hasLookInput)
                ApplyLook(activeRig, resolved.lookInput, profile);
            }
        }

        public void Clear()
        {
            Deactivate(activeRig);
            activeRig = null;
            activeProfileKey = null;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Clear();
            rigs.Dispose();
            reportedProfileErrors.Clear();
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

        private void ReportProfileError(string profileKey, string detail)
        {
            string key = string.IsNullOrWhiteSpace(profileKey) ? "<empty>" : profileKey;
            if (reportedProfileErrors.Add(key))
                Debug.LogError($"[ESCamera] View '{outputCamera.name}' 无法应用 Profile '{key}'：{detail}", outputCamera);
        }

        private static void Deactivate(CinemachineVirtualCameraBase rig)
        {
            if (rig == null)
                return;

            rig.Priority = StandbyPriority;
            rig.Follow = null;
            rig.LookAt = null;
            CinemachineCollider obstruction = rig.GetComponent<CinemachineCollider>();
            if (obstruction != null)
                obstruction.enabled = false;
        }

        private static void ConfigureRigForDirector(CinemachineVirtualCameraBase rig, ESCameraProfile profile)
        {
            if (rig is CinemachineFreeLook freeLook)
            {
                DisableLegacyInput(ref freeLook.m_XAxis);
                DisableLegacyInput(ref freeLook.m_YAxis);
                ConfigureObstruction(rig, profile);
                return;
            }

            CinemachinePOV pov = rig.GetComponent<CinemachinePOV>();
            if (pov != null)
            {
                DisableLegacyInput(ref pov.m_HorizontalAxis);
                DisableLegacyInput(ref pov.m_VerticalAxis);
            }

            ConfigureObstruction(rig, profile);
        }

        private static void ConfigureObstruction(CinemachineVirtualCameraBase rig, ESCameraProfile profile)
        {
            CinemachineCollider obstruction = rig.GetComponent<CinemachineCollider>();
            if (profile == null || !profile.enableObstruction)
            {
                if (obstruction != null)
                    obstruction.enabled = false;
                return;
            }

            if (obstruction == null)
            {
                Debug.LogError(
                    "[ESCamera] Profile '" + profile.profileKey + "' 启用了避障，但 Rig '" + rig.name
                    + "' 缺少 CinemachineCollider。请在制作期补齐 Rig。",
                    rig);
                return;
            }

            obstruction.enabled = true;
            obstruction.m_CollideAgainst = profile.obstructionMask;
            obstruction.m_CameraRadius = Mathf.Max(0.01f, profile.obstructionCameraRadius);
            obstruction.m_MinimumDistanceFromTarget = Mathf.Max(0.01f, profile.obstructionMinimumDistance);
            obstruction.m_MaximumEffort = Mathf.Clamp(profile.obstructionMaximumEffort, 1, 8);
            obstruction.m_Damping = Mathf.Max(0f, profile.obstructionDamping);
            obstruction.m_DampingWhenOccluded = Mathf.Max(0f, profile.obstructionDampingWhenOccluded);
        }

        private static void DisableLegacyInput(ref AxisState axis)
        {
            axis.m_InputAxisName = string.Empty;
            axis.m_InputAxisValue = 0f;
            axis.SetInputAxisProvider(0, null);
        }

        private static void ApplyLook(CinemachineVirtualCameraBase rig, Vector2 lookInput, ESCameraProfile profile)
        {
            float deltaTime = Time.deltaTime;
            float verticalSign = profile.invertVerticalLook ? 1f : -1f;
            if (rig is CinemachineFreeLook freeLook)
            {
                ApplyAxisDelta(ref freeLook.m_XAxis, lookInput.x * profile.freeLookSensitivity.x * deltaTime);
                ApplyAxisDelta(ref freeLook.m_YAxis, lookInput.y * profile.freeLookSensitivity.y * verticalSign * deltaTime);
                return;
            }

            CinemachinePOV pov = rig.GetComponent<CinemachinePOV>();
            if (pov == null)
                return;

            ApplyAxisDelta(ref pov.m_HorizontalAxis, lookInput.x * profile.povLookSensitivity.x * deltaTime);
            ApplyAxisDelta(ref pov.m_VerticalAxis, lookInput.y * profile.povLookSensitivity.y * verticalSign * deltaTime);
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
            ESCameraProfile profile,
            ESCameraResolvedModifiers modifiers)
        {
            RigModifierBaseline baseline = GetOrCreateModifierBaseline(rig);
            float fieldOfView = Mathf.Clamp(modifiers.fieldOfView.Apply(profile.baseFieldOfView), 1f, 179f);
            float distanceScale = Mathf.Max(0.01f, modifiers.distanceScale.Apply(profile.baseDistanceScale));
            Vector3 shoulderOffset = modifiers.shoulderOffset.Apply(profile.baseShoulderOffset);
            float shakeAmplitude = Mathf.Max(0f, modifiers.shakeAmplitude.Apply(profile.baseShakeAmplitude));

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

        public bool IsReady => !disposed && catalog != null && rigRoot != null;

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
            CinemachineVirtualCameraBase[] cameras = instance.GetComponentsInChildren<CinemachineVirtualCameraBase>(true);
            if (cameras.Length != 1 || cameras[0] == null)
            {
                Debug.LogError($"[ESCamera] Rig '{rigKey}' 必须且只能包含一个 CinemachineVirtualCameraBase。", instance);
                UnityEngine.Object.Destroy(instance);
                return false;
            }

            rig = cameras[0];
            rig.Priority = 0;
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
