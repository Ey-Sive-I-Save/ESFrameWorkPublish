#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ES.TestAssets
{
    /// <summary>
    /// Binds the authored ES Composite Shader UGUI prefab to the test animator.
    /// This component never creates UI objects; it only finds serialized prefab nodes,
    /// wires callbacks, and projects runtime state into existing controls.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ESCompositeShaderUGUIBinder : MonoBehaviour
    {
        [SerializeField] private ESCompositeShaderTestAnimator owner;
        private readonly List<Button> caseButtons = new List<Button>();
        private readonly List<int> caseButtonIndices = new List<int>();
        private readonly List<Slider> controlSliders = new List<Slider>();
        private readonly List<int> controlSliderIndices = new List<int>();
        private readonly List<TMP_Text> controlLabels = new List<TMP_Text>();
        private readonly List<Toggle> toggles = new List<Toggle>();
        private readonly List<int> toggleIndices = new List<int>();
        private TMP_Text selectedCase;
        private TMP_Text status;
        private TMP_Text previewState;
        private TMP_Text hostBadge;
        private TMP_Text usageGuide;
        private TMP_Text diagnosticInfo;
        private string diagnosticOverride;
        private bool bound;

        public void Bind(ESCompositeShaderTestAnimator animator)
        {
            owner = animator;
            CacheAuthoredNodes();
            WireCallbacks();
            bound = true;
            Refresh();
        }

        public void Refresh()
        {
            if (!bound || owner == null)
                return;
            diagnosticOverride = null;

            for (int i = 0; i < caseButtons.Count; i++)
            {
                int caseIndex = caseButtonIndices[i];
                bool active = caseIndex >= 0 && caseIndex < owner.UGUICaseCount;
                caseButtons[i].gameObject.SetActive(active);
                if (!active)
                    continue;
                TMP_Text label = caseButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = (caseIndex == owner.UGUISelectedCaseIndex ? ">  " : "    ") + owner.GetUGUICaseName(caseIndex);
                ColorBlock colors = caseButtons[i].colors;
                colors.normalColor = caseIndex == owner.UGUISelectedCaseIndex
                    ? new Color(0.28f, 0.85f, 0.91f, 1f)
                    : new Color(0.10f, 0.20f, 0.28f, 1f);
                caseButtons[i].colors = colors;
            }

            for (int i = 0; i < controlSliders.Count; i++)
            {
                int controlIndex = controlSliderIndices[i];
                bool active = controlIndex >= 0 && controlIndex < owner.UGUIControlCount;
                controlSliders[i].gameObject.SetActive(active);
                if (!active)
                    continue;
                controlSliders[i].SetValueWithoutNotify(owner.GetUGUIControlValue(controlIndex));
                if (i < controlLabels.Count && controlLabels[i] != null)
                    controlLabels[i].text = owner.GetUGUIControlName(controlIndex);
            }

            for (int i = 0; i < toggles.Count; i++)
            {
                int toggleIndex = toggleIndices[i];
                bool active = toggleIndex < 2;
                toggles[i].gameObject.SetActive(active);
                if (active)
                    toggles[i].SetIsOnWithoutNotify(toggleIndex == 0 ? owner.UGUIAutoAnimate : owner.UGUISoloSelection);
            }

            if (selectedCase != null)
                selectedCase.text = owner.UGUICaseCount == 0
                    ? "无可用 Shader 案例"
                    : owner.GetUGUICaseName(owner.UGUISelectedCaseIndex);
            if (previewState != null)
                previewState.text = "LIVE PREVIEW · UGUI";
            if (hostBadge != null)
                hostBadge.text = "HOST · Canvas / Graphic";
            if (usageGuide != null)
                usageGuide.text = owner.GetUGUIUsageGuide();
            if (diagnosticInfo != null)
                diagnosticInfo.text = diagnosticOverride ?? owner.GetUGUIDiagnostics();
            if (status != null)
                status.text = "UGUI 已绑定 · 参数仅作用于运行时实例";
        }

        public void SetDiagnosticStatus(string value)
        {
            diagnosticOverride = value;
            if (diagnosticInfo != null)
                diagnosticInfo.text = value ?? string.Empty;
            if (status != null && !string.IsNullOrEmpty(value))
                status.text = value;
        }

        /// <summary>
        /// Deterministic PlayMode smoke path for the authored event wiring. It invokes
        /// existing Slider/Toggle UnityEvents, then restores the selected case. No UI
        /// object is created and this method is only called by an explicit editor
        /// diagnostic request.
        /// </summary>
        public bool RunSmokeValidation()
        {
            if (!bound || owner == null)
                return false;
            bool sliderOk = false;
            if (controlSliders.Count > 0 && owner.UGUIControlCount > 0)
            {
                float before = owner.GetUGUIControlValue(controlSliderIndices[0]);
                float next = Mathf.Abs(before - 0.37f) < 0.001f ? 0.63f : 0.37f;
                controlSliders[0].onValueChanged.Invoke(next);
                sliderOk = Mathf.Abs(owner.GetUGUIControlValue(controlSliderIndices[0]) - next) < 0.001f;
            }
            bool toggleOk = toggles.Count >= 2;
            if (toggleOk)
            {
                toggles[0].onValueChanged.Invoke(false);
                toggles[1].onValueChanged.Invoke(true);
                toggleOk = !owner.UGUIAutoAnimate && owner.UGUISoloSelection;
            }
            owner.UGUIResetSelected();
            Refresh();
            bool passed = sliderOk && toggleOk;
            Debug.Log(string.Format("[ES Composite Shader] Authored UGUI smoke: slider={0}, toggles={1}, result={2}", sliderOk, toggleOk, passed ? "PASS" : "FAIL"), this);
            return passed;
        }

        private void CacheAuthoredNodes()
        {
            caseButtons.Clear();
            caseButtonIndices.Clear();
            controlSliders.Clear();
            controlSliderIndices.Clear();
            controlLabels.Clear();
            toggles.Clear();
            toggleIndices.Clear();
            var profileCaseCounters = new Dictionary<Transform, int>();
            var profileControlCounters = new Dictionary<Transform, int>();
            foreach (Button button in GetComponentsInChildren<Button>(true))
            {
                if (button.name.StartsWith("case-", StringComparison.Ordinal))
                {
                    caseButtons.Add(button);
                    Transform profile = FindProfile(button.transform);
                    int index = NextIndex(profileCaseCounters, profile);
                    caseButtonIndices.Add(index);
                }
                string n = button.name;
                if (n == "reset") button.onClick.AddListener(owner.UGUIResetSelected);
                else if (n == "save") button.onClick.AddListener(owner.UGUISavePreview);
                else if (n == "apply") button.onClick.AddListener(owner.UGUIApplyPreview);
                else if (n == "preset-subtle") button.onClick.AddListener(() => owner.UGUIApplyPreset(0.65f, "Subtle"));
                else if (n == "preset-standard") button.onClick.AddListener(() => owner.UGUIApplyPreset(1f, "Standard"));
                else if (n == "preset-hero") button.onClick.AddListener(() => owner.UGUIApplyPreset(1.25f, "Hero"));
                else if (n == "camera-focus") button.onClick.AddListener(owner.UGUIFocusCamera);
                else if (n == "camera-reset") button.onClick.AddListener(owner.UGUIResetCamera);
                else if (n == "copy-usage") button.onClick.AddListener(owner.UGUICopyUsageGuide);
                else if (n == "run-diagnostics") button.onClick.AddListener(owner.UGUIRunDiagnostics);
                else if (n == "nav-overview") button.onClick.AddListener(() => owner.UGUINavigate(0));
                else if (n == "nav-2d") button.onClick.AddListener(() => owner.UGUINavigate(1));
                else if (n == "nav-ui") button.onClick.AddListener(() => owner.UGUINavigate(2));
                else if (n == "nav-lit") button.onClick.AddListener(() => owner.UGUINavigate(3));
                else if (n == "nav-vfx") button.onClick.AddListener(() => owner.UGUINavigate(4));
            }
            foreach (Slider slider in GetComponentsInChildren<Slider>(true))
            {
                controlSliders.Add(slider);
                Transform profile = FindProfile(slider.transform);
                controlSliderIndices.Add(NextIndex(profileControlCounters, profile));
                TMP_Text label = slider.transform.parent == null
                    ? null
                    : slider.transform.parent.GetComponentInChildren<TMP_Text>(true);
                controlLabels.Add(label);
            }
            var profileToggleCounters = new Dictionary<Transform, int>();
            foreach (Toggle toggle in GetComponentsInChildren<Toggle>(true))
            {
                toggles.Add(toggle);
                Transform profile = FindProfile(toggle.transform);
                toggleIndices.Add(NextIndex(profileToggleCounters, profile));
            }
            selectedCase = FindText("preview-title");
            status = FindText("status");
            previewState = FindText("preview-state");
            hostBadge = FindText("host-badge");
            usageGuide = FindText("usage-guide");
            diagnosticInfo = FindText("diagnostic-info");
        }

        private void WireCallbacks()
        {
            for (int i = 0; i < caseButtons.Count; i++)
            {
                int captured = caseButtonIndices[i];
                caseButtons[i].onClick.AddListener(() => owner.UGUISelectCase(captured));
            }
            for (int i = 0; i < controlSliders.Count; i++)
            {
                int captured = controlSliderIndices[i];
                controlSliders[i].onValueChanged.AddListener(value => owner.UGUISetControl(captured, value));
            }
            for (int i = 0; i < toggles.Count; i++)
            {
                int captured = toggleIndices[i];
                toggles[i].onValueChanged.AddListener(value => owner.UGUISetToggle(captured, value));
            }
        }

        private static int NextIndex(Dictionary<Transform, int> counters, Transform profile)
        {
            if (!counters.TryGetValue(profile, out int index))
                index = 0;
            counters[profile] = index + 1;
            return index;
        }

        private static Transform FindProfile(Transform node)
        {
            Transform current = node;
            while (current != null)
            {
                if (current.name == "Wide" || current.name == "Narrow")
                    return current;
                current = current.parent;
            }
            return null;
        }

        private TMP_Text FindText(string objectName)
        {
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
                if (text.name == objectName)
                    return text;
            return null;
        }
    }
}
#endif
