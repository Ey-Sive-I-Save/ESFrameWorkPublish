using System;
using System.Collections.Generic;
using ES.Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    [TypeRegistryItem("输入模块")]
    public sealed class ESInputModule : ESSystemModule
    {
        [LabelText("输入配置")]
        public ESInputConfig inputConfig;

        [LabelText("覆盖档案层")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = false)]
        public List<ESInputBindingProfile> profileLayers = new List<ESInputBindingProfile>();

        [LabelText("初始化时加载玩家档案")]
        public bool loadPlayerProfileOnInitialize = true;

        [LabelText("玩家档案路径")]
        [PropertyTooltip("留空使用 persistentDataPath/Input/input_profile.json。")]
        public string playerProfilePath;

        [LabelText("启用时自动构建")]
        public bool buildOnEnable = true;

        [LabelText("构建后自动启用输入")]
        public bool enableAfterBuild = true;

        [NonSerialized]
        private readonly ESInputRuntime runtime = new ESInputRuntime();

        [NonSerialized]
        private ESInputBindingProfile effectiveProfile;

        [NonSerialized]
        private ESInputBindingProfile playerProfile;

        [NonSerialized]
        private ESRuntimeModeService modeService;

        [NonSerialized]
        private IESInputRuntimeConfigSource configSource;

        [NonSerialized]
        private ESInputConfig runtimeDefaultConfig;

        [NonSerialized]
        private ESInputRuntimeBuildResult currentBuild;

        [NonSerialized]
        private readonly List<ESInputBindingProfile> effectiveBuildLayers = new List<ESInputBindingProfile>(4);

        [NonSerialized]
        private bool runtimeBuilt;

        [NonSerialized]
        private bool inputEnabled;

        [ShowInInspector, ReadOnly, LabelText("已构建")]
        public bool IsBuilt
        {
            get { return runtimeBuilt; }
        }

        [ShowInInspector, ReadOnly, LabelText("输入已启用")]
        public bool IsInputEnabled
        {
            get { return inputEnabled; }
        }

        public ESInputRuntime RuntimeInstance
        {
            get { return runtime; }
        }

        public ESInputService Service
        {
            get { return runtime.Service; }
        }

        public ESInputVirtualSource VirtualSource
        {
            get { return runtime.VirtualSource; }
        }

        public ESInputSchemeResolver SchemeResolver
        {
            get { return runtime.SchemeResolver; }
        }

        public ESRuntimeModeService ModeService
        {
            get { return EnsureModeService(); }
        }

        public ESInputBindingProfile EffectiveProfile
        {
            get { return effectiveProfile; }
        }

        public ESInputBindingProfile PlayerProfile
        {
            get { return playerProfile; }
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (buildOnEnable && !runtimeBuilt && ResolveConfigSource() != null)
                InitializeInput();
            else if (runtimeBuilt && enableAfterBuild)
                EnableInput();
        }

        protected override void OnDisable()
        {
            DisableInput();
            base.OnDisable();
        }

        protected override void Update()
        {
            if (!inputEnabled)
                return;

            runtime.Update(Time.time);
        }

        public override void OnDestroy()
        {
            DisposeRuntime();
            base.OnDestroy();
        }

        public void Configure(
            IESInputRuntimeConfigSource config,
            IList<ESInputBindingProfile> profiles = null,
            ESRuntimeModeService externalModeService = null,
            bool rebuildNow = true)
        {
            configSource = config;
            inputConfig = config as ESInputConfig;
            ReleaseRuntimeDefaultConfig();
            SetProfileLayers(profiles);

            if (externalModeService != null)
                modeService = externalModeService;

            if (rebuildNow)
                InitializeInput();
        }

        public void SetProfileLayers(IList<ESInputBindingProfile> profiles)
        {
            if (profileLayers == null)
                profileLayers = new List<ESInputBindingProfile>();

            profileLayers.Clear();
            if (profiles == null)
                return;

            for (int i = 0; i < profiles.Count; i++)
            {
                if (profiles[i] != null)
                    profileLayers.Add(profiles[i]);
            }
        }

        [Button("初始化输入系统")]
        public bool InitializeInput()
        {
            if (loadPlayerProfileOnInitialize)
                LoadPlayerProfile(playerProfilePath, false);

            return RebuildRuntime();
        }

        public bool InitializeInput(
            IESInputRuntimeConfigSource config,
            IList<ESInputBindingProfile> profiles = null,
            ESRuntimeModeService externalModeService = null,
            bool loadPlayerProfile = true,
            string profilePath = null)
        {
            configSource = config;
            inputConfig = config as ESInputConfig;
            SetProfileLayers(profiles);

            if (externalModeService != null)
                modeService = externalModeService;

            loadPlayerProfileOnInitialize = loadPlayerProfile;
            if (!string.IsNullOrEmpty(profilePath))
                playerProfilePath = profilePath;

            return InitializeInput();
        }

        public ESInputBindingProfile LoadPlayerProfile(string filePath = null, bool rebuildNow = true)
        {
            playerProfile = ESInputUtility.LoadProfileOrDefault(ResolvePlayerProfilePath(filePath));
            if (rebuildNow)
                RebuildRuntime();

            return playerProfile;
        }

        public void SetPlayerProfile(ESInputBindingProfile profile, bool rebuildNow = true)
        {
            playerProfile = profile;
            if (playerProfile != null)
                playerProfile.Normalize();

            if (rebuildNow)
                RebuildRuntime();
        }

        public void SavePlayerProfile(string filePath = null)
        {
            ESInputUtility.SaveProfile(playerProfile ?? ESInputUtility.CreateDefaultProfile(), ResolvePlayerProfilePath(filePath));
        }

        public void ApplyPlayerProfile(ESInputBindingProfile profile, bool saveNow = false, bool rebuildNow = true, string savePath = null)
        {
            SetPlayerProfile(profile, false);

            if (saveNow)
                SavePlayerProfile(savePath);

            if (rebuildNow)
                RebuildRuntime();
        }

        public ESInputBindingOverride ApplyPlayerPathOverride(
            string bindingId,
            ESInputActionId actionId,
            string schemeId,
            string bindingName,
            string originalPath,
            string overridePath,
            bool saveNow = false,
            bool rebuildNow = true)
        {
            EnsurePlayerProfile();
            ESInputBindingOverride result = playerProfile.SetPathOverride(
                bindingId,
                actionId,
                schemeId,
                bindingName,
                originalPath,
                overridePath);

            ApplyRuntimeChange(saveNow, rebuildNow);
            return result;
        }

        public ESInputBindingOverride ApplyPlayerVirtualOverride(
            string bindingId,
            ESInputActionId actionId,
            string schemeId,
            string bindingName,
            string virtualControlId,
            bool saveNow = false,
            bool rebuildNow = true)
        {
            EnsurePlayerProfile();
            ESInputBindingOverride result = playerProfile.SetVirtualControlOverride(
                bindingId,
                actionId,
                schemeId,
                bindingName,
                virtualControlId);

            ApplyRuntimeChange(saveNow, rebuildNow);
            return result;
        }

        public ESInputBindingOverride ApplyPlayerInputSystemOverride(
            string bindingId,
            ESInputActionId actionId,
            string schemeId,
            string bindingName,
            string originalPath,
            string overridePath,
            string overrideInteractions = "",
            string overrideProcessors = "",
            string overrideBindingName = "",
            bool overrideIsComposite = false,
            bool overrideIsPartOfComposite = false,
            bool saveNow = false,
            bool rebuildNow = true)
        {
            EnsurePlayerProfile();
            ESInputBindingOverride result = playerProfile.SetInputSystemBindingOverride(
                bindingId,
                actionId,
                schemeId,
                bindingName,
                originalPath,
                overridePath,
                overrideInteractions,
                overrideProcessors,
                overrideBindingName,
                overrideIsComposite,
                overrideIsPartOfComposite);

            ApplyRuntimeChange(saveNow, rebuildNow);
            return result;
        }

        public bool RemovePlayerOverride(string bindingId, bool saveNow = false, bool rebuildNow = true)
        {
            EnsurePlayerProfile();
            bool removed = playerProfile.RemoveOverride(bindingId);
            if (removed)
                ApplyRuntimeChange(saveNow, rebuildNow);

            return removed;
        }

        public void ApplyScheme(string schemeId, bool saveNow = false, bool rebuildNow = true)
        {
            EnsurePlayerProfile();
            playerProfile.activeSchemeId = string.IsNullOrEmpty(schemeId) ? GetDefaultSchemeId() : schemeId;
            ApplyRuntimeChange(saveNow, rebuildNow);
        }

        public bool ResetPlayerBindingToDefault(string bindingId, bool saveNow = false, bool rebuildNow = true)
        {
            return RemovePlayerOverride(bindingId, saveNow, rebuildNow);
        }

        public void ResetAllPlayerOverrides(bool saveNow = false, bool rebuildNow = true)
        {
            EnsurePlayerProfile();
            ESInputUtility.ResetAllBindingsToDefault(playerProfile);
            ApplyRuntimeChange(saveNow, rebuildNow);
        }

        [Button("重置玩家键位")]
        public void ResetPlayerProfile(bool rebuildNow = true, bool saveNow = false)
        {
            playerProfile = ESInputUtility.CreateDefaultProfile();
            if (saveNow)
                SavePlayerProfile();

            if (rebuildNow)
                RebuildRuntime();
        }

        public string PlayerProfileToJson(bool prettyPrint = false)
        {
            return ESInputUtility.ToJson(playerProfile ?? ESInputUtility.CreateDefaultProfile(), prettyPrint);
        }

        public bool ApplyPlayerProfileJson(string json, bool saveNow = false, bool rebuildNow = true)
        {
            if (string.IsNullOrEmpty(json))
                return false;

            playerProfile = ESInputUtility.FromJsonOrDefault(json);
            ApplyRuntimeChange(saveNow, rebuildNow);
            return true;
        }

        [Button("重建输入运行时")]
        public bool RebuildRuntime()
        {
            IESInputRuntimeConfigSource source = ResolveConfigSource();
            if (source == null)
                return false;

            bool shouldEnable = inputEnabled || enableAfterBuild;
            ClearRuntimeBuild();

            ESInputUtility.EnsureConfigBindingIds(source);
            effectiveProfile = BuildEffectiveProfile();
            ESInputRuntimeBuildResult build = ESInputRuntimeBuilder.Build(source, effectiveProfile, GetDefaultSchemeId());
            currentBuild = build;

            runtime.Initialize(build, EnsureModeService());
            runtimeBuilt = true;
            inputEnabled = false;

            if (shouldEnable)
                EnableInput();

            return true;
        }

        public void RebuildRuntime(IList<ESInputBindingProfile> profiles)
        {
            SetProfileLayers(profiles);
            RebuildRuntime();
        }

        public void EnableInput()
        {
            if (!runtimeBuilt)
                return;

            if (inputEnabled)
                return;

            runtime.Enable();
            inputEnabled = true;
        }

        public void DisableInput()
        {
            if (!inputEnabled)
                return;

            runtime.Disable();
            inputEnabled = false;
        }

        public void ClearRuntimeBuild()
        {
            DisableInput();
            runtime.Initialize(null, EnsureModeService());
            currentBuild = null;
            effectiveProfile = null;
            runtimeBuilt = false;
            inputEnabled = false;
        }

        public void DisposeRuntime()
        {
            if (runtimeBuilt || inputEnabled)
                runtime.Disable();

            runtime.Dispose();
            ReleaseRuntimeDefaultConfig();
            currentBuild = null;
            effectiveProfile = null;
            runtimeBuilt = false;
            inputEnabled = false;
        }

        public void SetModeService(ESRuntimeModeService service)
        {
            modeService = service;
            if (runtimeBuilt)
                RebuildRuntime();
        }

        public int GetRuntimeBindings(ESInputActionId id, List<ESInputCompiledBinding> results, bool activeSchemeOnly = true)
        {
            if (results == null)
                return 0;

            results.Clear();
            if (currentBuild == null || currentBuild.bindings == null)
                return 0;

            string activeSchemeId = currentBuild.activeSchemeId;
            for (int i = 0; i < currentBuild.bindingCount; i++)
            {
                ESInputCompiledBinding binding = currentBuild.bindings[i];
                if (binding.actionId != id)
                    continue;

                if (activeSchemeOnly
                    && !string.IsNullOrEmpty(activeSchemeId)
                    && !string.Equals(binding.schemeId, activeSchemeId, StringComparison.Ordinal))
                    continue;

                results.Add(binding);
            }

            return results.Count;
        }

        public bool TryGetFirstRuntimeBinding(ESInputActionId id, out ESInputCompiledBinding binding, bool activeSchemeOnly = true)
        {
            if (currentBuild != null && currentBuild.bindings != null)
            {
                string activeSchemeId = currentBuild.activeSchemeId;
                for (int i = 0; i < currentBuild.bindingCount; i++)
                {
                    ESInputCompiledBinding item = currentBuild.bindings[i];
                    if (item.actionId != id)
                        continue;

                    if (activeSchemeOnly
                        && !string.IsNullOrEmpty(activeSchemeId)
                        && !string.Equals(item.schemeId, activeSchemeId, StringComparison.Ordinal))
                        continue;

                    binding = item;
                    return true;
                }
            }

            binding = default;
            return false;
        }

        public ESInputBindingOverride ApplyPlayerPathOverride(
            ESInputCompiledBinding binding,
            string overridePath,
            bool saveNow = false,
            bool rebuildNow = true)
        {
            return ApplyPlayerPathOverride(
                binding.bindingId,
                binding.actionId,
                binding.schemeId,
                binding.name,
                binding.originalPath,
                overridePath,
                saveNow,
                rebuildNow);
        }

        public ESInputBindingOverride ApplyPlayerVirtualOverride(
            ESInputCompiledBinding binding,
            string virtualControlId,
            bool saveNow = false,
            bool rebuildNow = true)
        {
            return ApplyPlayerVirtualOverride(
                binding.bindingId,
                binding.actionId,
                binding.schemeId,
                binding.name,
                virtualControlId,
                saveNow,
                rebuildNow);
        }

        public bool ResetPlayerBindingToDefault(ESInputCompiledBinding binding, bool saveNow = false, bool rebuildNow = true)
        {
            return ResetPlayerBindingToDefault(binding.bindingId, saveNow, rebuildNow);
        }

        public bool WasPressed(ESInputActionId id)
        {
            return runtime.Service.WasPressed(id);
        }

        public bool ConsumePressed(ESInputActionId id)
        {
            return runtime.Service.ConsumePressed(id);
        }

        public bool ConsumeClick(ESInputActionId id)
        {
            return runtime.Service.ConsumePressed(id);
        }

        public bool IsHeld(ESInputActionId id)
        {
            return runtime.Service.IsHeld(id);
        }

        public bool WasReleased(ESInputActionId id)
        {
            return runtime.Service.WasReleased(id);
        }

        public bool ConsumeReleased(ESInputActionId id)
        {
            return runtime.Service.ConsumeReleased(id);
        }

        public bool WasLongPressed(ESInputActionId id)
        {
            return runtime.Service.WasLongPressed(id);
        }

        public bool ConsumeLongPressed(ESInputActionId id)
        {
            return runtime.Service.ConsumeLongPressed(id);
        }

        public bool ConsumeLongPress(ESInputActionId id)
        {
            return runtime.Service.ConsumeLongPressed(id);
        }

        public bool WasDoublePressed(ESInputActionId id)
        {
            return runtime.Service.WasDoublePressed(id);
        }

        public bool ConsumeDoublePressed(ESInputActionId id)
        {
            return runtime.Service.ConsumeDoublePressed(id);
        }

        public bool WasTriggered(ESInputActionId id)
        {
            return runtime.Service.WasTriggered(id);
        }

        public bool ConsumeTrigger(ESInputActionId id)
        {
            return runtime.Service.ConsumeTrigger(id);
        }

        public float ReadAxis(ESInputActionId id)
        {
            return runtime.Service.ReadAxis(id);
        }

        public Vector2 ReadVector2(ESInputActionId id)
        {
            return runtime.Service.ReadVector2(id);
        }

        public float GetHoldTime(ESInputActionId id)
        {
            return runtime.Service.GetHoldTime(id);
        }

        public void UISetButton(ESInputActionId id, bool held)
        {
            runtime.VirtualSource.SetButton(id, held);
        }

        public void UIPressButton(ESInputActionId id)
        {
            runtime.VirtualSource.SetButton(id, true);
        }

        public void UIReleaseButton(ESInputActionId id)
        {
            runtime.VirtualSource.SetButton(id, false);
        }

        public void UIPulseButton(ESInputActionId id)
        {
            runtime.VirtualSource.PulseButton(id);
        }

        public void UITriggerButton(ESInputActionId id)
        {
            runtime.VirtualSource.PulseButton(id);
        }

        public void UITriggerInteract()
        {
            runtime.VirtualSource.PulseButton(ESInputActionId.Interact);
        }

        public void UIClearButton(ESInputActionId id)
        {
            runtime.VirtualSource.ClearButton(id);
        }

        public void UISetAxis(ESInputActionId id, float value)
        {
            runtime.VirtualSource.SetAxis(id, value);
        }

        public void UIClearAxis(ESInputActionId id)
        {
            runtime.VirtualSource.ClearAxis(id);
        }

        public void UISetVector2(ESInputActionId id, Vector2 value)
        {
            runtime.VirtualSource.SetVector2(id, value);
        }

        public void UIClearVector2(ESInputActionId id)
        {
            runtime.VirtualSource.ClearVector2(id);
        }

        public void UISetButton(string virtualControlId, bool held)
        {
            runtime.VirtualSource.SetButton(virtualControlId, held);
        }

        public void UIPulseButton(string virtualControlId)
        {
            runtime.VirtualSource.PulseButton(virtualControlId);
        }

        public void UITriggerButton(string virtualControlId)
        {
            runtime.VirtualSource.PulseButton(virtualControlId);
        }

        public void UIClearButton(string virtualControlId)
        {
            runtime.VirtualSource.ClearButton(virtualControlId);
        }

        public void UISetAxis(string virtualControlId, float value)
        {
            runtime.VirtualSource.SetAxis(virtualControlId, value);
        }

        public void UIClearAxis(string virtualControlId)
        {
            runtime.VirtualSource.ClearAxis(virtualControlId);
        }

        public void UISetVector2(string virtualControlId, Vector2 value)
        {
            runtime.VirtualSource.SetVector2(virtualControlId, value);
        }

        public void UIClearVector2(string virtualControlId)
        {
            runtime.VirtualSource.ClearVector2(virtualControlId);
        }

        public void UIClearAll()
        {
            runtime.VirtualSource.ClearAll();
        }

        private ESRuntimeModeService EnsureModeService()
        {
            return modeService ?? ESGameManager.RuntimeMode;
        }

        private IESInputRuntimeConfigSource ResolveConfigSource()
        {
            if (configSource != null)
                return configSource;

            configSource = inputConfig;
            if (configSource != null)
                return configSource;

            if (runtimeDefaultConfig == null)
            {
                runtimeDefaultConfig = CreateRuntimeDefaultConfig();
                inputConfig = runtimeDefaultConfig;
            }

            configSource = runtimeDefaultConfig;
            return configSource;
        }

        private ESInputBindingProfile BuildEffectiveProfile()
        {
            effectiveBuildLayers.Clear();

            if (profileLayers != null)
            {
                for (int i = 0; i < profileLayers.Count; i++)
                {
                    if (profileLayers[i] != null)
                        effectiveBuildLayers.Add(profileLayers[i]);
                }
            }

            if (playerProfile != null)
                effectiveBuildLayers.Add(playerProfile);

            return ESInputUtility.BakeProfiles(effectiveBuildLayers);
        }

        private string ResolvePlayerProfilePath(string filePath)
        {
            return string.IsNullOrEmpty(filePath) ? playerProfilePath : filePath;
        }

        private string GetDefaultSchemeId()
        {
            return inputConfig != null && !string.IsNullOrEmpty(inputConfig.defaultSchemeId)
                ? inputConfig.defaultSchemeId
                : ESInputSchemeIds.KeyboardMouse;
        }

        private void EnsurePlayerProfile()
        {
            if (playerProfile == null)
                playerProfile = ESInputUtility.CreateDefaultProfile();
        }

        private void ApplyRuntimeChange(bool saveNow, bool rebuildNow)
        {
            if (saveNow)
                SavePlayerProfile();

            if (rebuildNow)
                RebuildRuntime();
        }

        private static ESInputConfig CreateRuntimeDefaultConfig()
        {
            ESInputConfig config = ScriptableObject.CreateInstance<ESInputConfig>();
            config.ResetDefaultGameplayConfig();
            config.hideFlags = HideFlags.DontSave;
            return config;
        }

        private void ReleaseRuntimeDefaultConfig()
        {
            if (runtimeDefaultConfig == null)
                return;

            ESInputConfig config = runtimeDefaultConfig;
            if (inputConfig == config)
                inputConfig = null;

            runtimeDefaultConfig = null;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(config);
            else
                UnityEngine.Object.DestroyImmediate(config);
        }
    }
}
