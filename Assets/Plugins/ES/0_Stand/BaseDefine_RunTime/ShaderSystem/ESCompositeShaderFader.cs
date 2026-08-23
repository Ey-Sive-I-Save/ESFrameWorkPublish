using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ES
{
    public enum ESCompositeShaderFadeValueType
    {
        Float,
        Vector,
        Color
    }

    [Serializable]
    public sealed class ESCompositeShaderFadeTrack
    {
        [SerializeField] private string propertyName = "_FadeProgress";
        [SerializeField] private ESCompositeShaderFadeValueType valueType;
        [SerializeField] private float fromFloat;
        [SerializeField] private float toFloat = 1f;
        [SerializeField] private Vector4 fromVector;
        [SerializeField] private Vector4 toVector = Vector4.one;
        [SerializeField] private Color fromColor = Color.white;
        [SerializeField] private Color toColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float startTime;
        [SerializeField, Range(0f, 1f)] private float endTime = 1f;

        private int propertyId;
        private string cachedPropertyName;

        internal int PropertyId
        {
            get
            {
                if (propertyId == 0 || !string.Equals(cachedPropertyName, propertyName, StringComparison.Ordinal))
                {
                    cachedPropertyName = propertyName;
                    propertyId = string.IsNullOrWhiteSpace(propertyName) ? 0 : Shader.PropertyToID(propertyName);
                }
                return propertyId;
            }
        }

        internal float Evaluate(float progress)
        {
            float from = Mathf.Min(startTime, endTime);
            float to = Mathf.Max(startTime, endTime);
            if (Mathf.Approximately(from, to))
                return progress >= to ? 1f : 0f;
            return Mathf.Clamp01((progress - from) / (to - from));
        }

        internal void Apply(MaterialPropertyBlock block, float progress)
        {
            int id = PropertyId;
            if (block == null || id == 0)
                return;

            float value = Evaluate(progress);
            switch (valueType)
            {
                case ESCompositeShaderFadeValueType.Vector:
                    block.SetVector(id, Vector4.LerpUnclamped(fromVector, toVector, value));
                    break;
                case ESCompositeShaderFadeValueType.Color:
                    block.SetColor(id, Color.LerpUnclamped(fromColor, toColor, value));
                    break;
                default:
                    block.SetFloat(id, Mathf.LerpUnclamped(fromFloat, toFloat, value));
                    break;
            }
        }

        internal void Apply(Material material, float progress)
        {
            int id = PropertyId;
            if (material == null || id == 0 || !material.HasProperty(id))
                return;

            float value = Evaluate(progress);
            switch (valueType)
            {
                case ESCompositeShaderFadeValueType.Vector:
                    material.SetVector(id, Vector4.LerpUnclamped(fromVector, toVector, value));
                    break;
                case ESCompositeShaderFadeValueType.Color:
                    material.SetColor(id, Color.LerpUnclamped(fromColor, toColor, value));
                    break;
                default:
                    material.SetFloat(id, Mathf.LerpUnclamped(fromFloat, toFloat, value));
                    break;
            }
        }
    }

    /// <summary>
    /// 对 Renderer 使用 MaterialPropertyBlock，对 UI Graphic 使用受管材质实例。
    /// 可驱动任意 float、vector 或 color Shader 属性，不污染共享材质。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("【ES】/相机与表现/Composite Shader Fader")]
    public sealed class ESCompositeShaderFader : MonoBehaviour
    {
        [SerializeField] private bool collectChildren = true;
        [SerializeField] private bool includeInactive = true;
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();
        [SerializeField] private Graphic[] graphics = Array.Empty<Graphic>();
        [SerializeField] private ESCompositeShaderFadeTrack[] tracks =
        {
            new ESCompositeShaderFadeTrack()
        };
        [SerializeField, Range(0f, 1f)] private float progress;
        [SerializeField, Min(0.001f)] private float duration = 0.35f;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private bool playOnEnable;
        [SerializeField] private bool playForwardOnEnable = true;
        [SerializeField] private AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));

        private readonly List<ESCompositeMaterialInstance> graphicInstances = new List<ESCompositeMaterialInstance>();
        private MaterialPropertyBlock propertyBlock;
        private float startProgress;
        private float targetProgress;
        private float elapsed;
        private bool playing;

        public float Progress => progress;
        public bool IsPlaying => playing;

        private void OnEnable()
        {
            RefreshTargets();
            Apply();
            if (playOnEnable)
                PlayTo(playForwardOnEnable ? 1f : 0f);
        }

        private void Update()
        {
            if (!playing)
                return;

            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
            float evaluated = curve == null ? normalized : curve.Evaluate(normalized);
            progress = Mathf.LerpUnclamped(startProgress, targetProgress, evaluated);
            Apply();
            if (normalized >= 1f)
            {
                progress = targetProgress;
                playing = false;
                Apply();
            }
        }

        private void OnValidate()
        {
            duration = Mathf.Max(0.001f, duration);
            progress = Mathf.Clamp01(progress);
            if (Application.isPlaying && isActiveAndEnabled)
                Apply();
        }

        public void FadeIn()
        {
            PlayTo(0f);
        }

        public void FadeOut()
        {
            PlayTo(1f);
        }

        public void PlayForward()
        {
            PlayTo(1f);
        }

        public void PlayBackward()
        {
            PlayTo(0f);
        }

        public void PlayTo(float value)
        {
            startProgress = progress;
            targetProgress = Mathf.Clamp01(value);
            elapsed = 0f;
            playing = !Mathf.Approximately(startProgress, targetProgress);
            if (!playing)
                Apply();
        }

        public void SetProgress(float value)
        {
            playing = false;
            progress = Mathf.Clamp01(value);
            Apply();
        }

        public void RefreshTargets()
        {
            if (collectChildren)
            {
                renderers = GetComponentsInChildren<Renderer>(includeInactive);
                graphics = GetComponentsInChildren<Graphic>(includeInactive);
            }

            graphicInstances.Clear();
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null || !ESCompositeMaterialInstance.IsCompositeMaterial(graphic.material))
                    continue;

                ESCompositeMaterialInstance instance = graphic.GetComponent<ESCompositeMaterialInstance>();
                if (instance == null)
                    instance = graphic.gameObject.AddComponent<ESCompositeMaterialInstance>();
                instance.Configure(graphic);
                graphicInstances.Add(instance);
            }
        }

        public void Apply()
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer target = renderers[i];
                if (target == null)
                    continue;

                Material[] materials = target.sharedMaterials;
                if (materials == null)
                    continue;

                if (materials.Length == 1)
                {
                    if (!ESCompositeMaterialInstance.IsCompositeMaterial(materials[0]))
                        continue;

                    target.GetPropertyBlock(propertyBlock);
                    ApplyTracks(propertyBlock);
                    target.SetPropertyBlock(propertyBlock);
                    propertyBlock.Clear();
                    continue;
                }

                for (int slot = 0; slot < materials.Length; slot++)
                {
                    if (!ESCompositeMaterialInstance.IsCompositeMaterial(materials[slot]))
                        continue;

                    target.GetPropertyBlock(propertyBlock, slot);
                    ApplyTracks(propertyBlock);
                    target.SetPropertyBlock(propertyBlock, slot);
                    propertyBlock.Clear();
                }
            }

            for (int i = 0; i < graphicInstances.Count; i++)
            {
                ESCompositeMaterialInstance instance = graphicInstances[i];
                if (instance == null)
                    continue;
                Material material = instance.Acquire();
                ApplyTracks(material);
            }
        }

        private void ApplyTracks(MaterialPropertyBlock block)
        {
            if (tracks == null)
                return;
            for (int i = 0; i < tracks.Length; i++)
                if (tracks[i] != null)
                    tracks[i].Apply(block, progress);
        }

        private void ApplyTracks(Material material)
        {
            if (material == null || tracks == null)
                return;
            for (int i = 0; i < tracks.Length; i++)
                if (tracks[i] != null)
                    tracks[i].Apply(material, progress);
        }

    }
}
