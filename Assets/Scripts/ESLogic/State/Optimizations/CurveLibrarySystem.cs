using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ES.Optimizations
{
    /// <summary>
    /// 曲线类型
    /// </summary>
    public enum CurveType
    {
        Custom,         // 自定义
        Linear,         // 线性
        EaseIn,         // 缓入
        EaseOut,        // 缓出
        EaseInOut,      // 缓入缓出
        Bounce,         // 弹跳
        Elastic,        // 弹性
        Overshoot,      // 超调
        Sigmoid         // S型
    }
    
    /// <summary>
    /// 曲线预设配置
    /// </summary>
    [Serializable]
    public class CurvePreset
    {
        [LabelText("名称")]
        public string name;
        
        [LabelText("类型")]
        public CurveType type;
        
        [LabelText("曲线")]
        [ShowIf("type", CurveType.Custom)]
        public AnimationCurve customCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        [LabelText("强度")]
        [Range(0.1f, 5f)]
        [HideIf("type", CurveType.Custom)]
        public float intensity = 1f;
        
        [NonSerialized]
        private AnimationCurve _cachedCurve;
        
        /// <summary>
        /// 获取或生成曲线
        /// </summary>
        public AnimationCurve GetCurve()
        {
            if (type == CurveType.Custom)
                return customCurve;
            
            if (_cachedCurve != null)
                return _cachedCurve;
            
            _cachedCurve = GenerateCurve(type, intensity);
            return _cachedCurve;
        }
        
        /// <summary>
        /// 生成预设曲线
        /// </summary>
        public static AnimationCurve GenerateCurve(CurveType type, float intensity)
        {
            switch (type)
            {
                case CurveType.Linear:
                    return AnimationCurve.Linear(0, 0, 1, 1);
                
                case CurveType.EaseIn:
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 0),
                        new Keyframe(1, 1, intensity * 2, intensity * 2)
                    );
                
                case CurveType.EaseOut:
                    return new AnimationCurve(
                        new Keyframe(0, 0, intensity * 2, intensity * 2),
                        new Keyframe(1, 1, 0, 0)
                    );
                
                case CurveType.EaseInOut:
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 0),
                        new Keyframe(0.5f, 0.5f, intensity * 2, intensity * 2),
                        new Keyframe(1, 1, 0, 0)
                    );
                
                case CurveType.Bounce:
                {
                    var curve = new AnimationCurve();
                    int bounces = Mathf.RoundToInt(3 * intensity);
                    float decay = 0.5f;
                    
                    for (int i = 0; i <= bounces; i++)
                    {
                        float t = i / (float)bounces;
                        float height = Mathf.Pow(decay, i);
                        curve.AddKey(t, 1f - height);
                        
                        if (i < bounces)
                        {
                            float tMid = (i + 0.5f) / bounces;
                            curve.AddKey(tMid, 1f);
                        }
                    }
                    
                    return curve;
                }
                
                case CurveType.Elastic:
                {
                    var curve = new AnimationCurve();
                    int samples = 20;
                    float frequency = 4f * intensity;
                    
                    for (int i = 0; i <= samples; i++)
                    {
                        float t = i / (float)samples;
                        float value = 1f - Mathf.Pow(2, -10 * t) * Mathf.Sin((t - 0.1f) * frequency * Mathf.PI * 2f);
                        curve.AddKey(t, value);
                    }
                    
                    return curve;
                }
                
                case CurveType.Overshoot:
                {
                    float overshoot = 0.2f * intensity;
                    return new AnimationCurve(
                        new Keyframe(0, 0, 0, 0),
                        new Keyframe(0.8f, 1f + overshoot, 2, 0),
                        new Keyframe(1, 1, 0, 0)
                    );
                }
                
                case CurveType.Sigmoid:
                {
                    var curve = new AnimationCurve();
                    int samples = 10;
                    
                    for (int i = 0; i <= samples; i++)
                    {
                        float t = i / (float)samples;
                        float x = (t - 0.5f) * 12f * intensity;
                        float value = 1f / (1f + Mathf.Exp(-x));
                        curve.AddKey(t, value);
                    }
                    
                    return curve;
                }
                
                default:
                    return AnimationCurve.Linear(0, 0, 1, 1);
            }
        }
    }
    
    /// <summary>
    /// 曲线库 - 集中管理所有曲线
    /// </summary>
    [CreateAssetMenu(fileName = "NewCurveLibrary", menuName = "ES/Animation/Curve Library")]
    public class CurveLibrary : ScriptableObject
    {
        [LabelText("曲线预设")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "name")]
        public List<CurvePreset> presets = new List<CurvePreset>();
        
        [NonSerialized]
        private Dictionary<string, CurvePreset> _nameToPreset;
        
        private void OnEnable()
        {
            BuildCache();
        }
        
        private void BuildCache()
        {
            _nameToPreset = new Dictionary<string, CurvePreset>();
            
            if (presets != null)
            {
                foreach (var preset in presets)
                {
                    if (!string.IsNullOrEmpty(preset.name))
                    {
                        _nameToPreset[preset.name] = preset;
                    }
                }
            }
        }
        
        /// <summary>
        /// 获取曲线
        /// </summary>
        public AnimationCurve GetCurve(string presetName)
        {
            if (_nameToPreset == null)
                BuildCache();
            
            if (_nameToPreset.TryGetValue(presetName, out var preset))
            {
                return preset.GetCurve();
            }
            
            Debug.LogWarning($"曲线预设 '{presetName}' 未找到");
            return AnimationCurve.Linear(0, 0, 1, 1);
        }
        
        /// <summary>
        /// 创建曲线混合
        /// </summary>
        public AnimationCurve BlendCurves(string preset1, string preset2, float blend)
        {
            var curve1 = GetCurve(preset1);
            var curve2 = GetCurve(preset2);
            
            return BlendCurves(curve1, curve2, blend);
        }
        
        /// <summary>
        /// 混合两条曲线
        /// </summary>
        public static AnimationCurve BlendCurves(AnimationCurve a, AnimationCurve b, float t)
        {
            t = Mathf.Clamp01(t);
            
            var result = new AnimationCurve();
            int samples = 10;
            
            for (int i = 0; i <= samples; i++)
            {
                float time = i / (float)samples;
                float valueA = a.Evaluate(time);
                float valueB = b.Evaluate(time);
                float blended = Mathf.Lerp(valueA, valueB, t);
                
                result.AddKey(time, blended);
            }
            
            return result;
        }
        
        [Button("添加默认预设", ButtonSizes.Medium)]
        [PropertySpace(10)]
        private void AddDefaultPresets()
        {
            presets.Clear();
            
            presets.Add(new CurvePreset 
            { 
                name = "Linear", 
                type = CurveType.Linear 
            });
            
            presets.Add(new CurvePreset 
            { 
                name = "EaseIn", 
                type = CurveType.EaseIn, 
                intensity = 1f 
            });
            
            presets.Add(new CurvePreset 
            { 
                name = "EaseOut", 
                type = CurveType.EaseOut, 
                intensity = 1f 
            });
            
            presets.Add(new CurvePreset 
            { 
                name = "EaseInOut", 
                type = CurveType.EaseInOut, 
                intensity = 1f 
            });
            
            presets.Add(new CurvePreset 
            { 
                name = "Bounce", 
                type = CurveType.Bounce, 
                intensity = 1f 
            });
            
            presets.Add(new CurvePreset 
            { 
                name = "Elastic", 
                type = CurveType.Elastic, 
                intensity = 1f 
            });
            
            presets.Add(new CurvePreset 
            { 
                name = "Overshoot", 
                type = CurveType.Overshoot, 
                intensity = 1.5f 
            });
            
            presets.Add(new CurvePreset 
            { 
                name = "Sigmoid", 
                type = CurveType.Sigmoid, 
                intensity = 1f 
            });
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"已添加 {presets.Count} 个默认预设");
#endif
        }
    }
    
    /// <summary>
    /// 运行时曲线修改器
    /// </summary>
    public class RuntimeCurveModifier
    {
        private AnimationCurve _baseCurve;
        private AnimationCurve _modifiedCurve;
        
        public RuntimeCurveModifier(AnimationCurve baseCurve)
        {
            _baseCurve = baseCurve;
            _modifiedCurve = new AnimationCurve(baseCurve.keys);
        }
        
        /// <summary>
        /// 缩放曲线Y值
        /// </summary>
        public void Scale(float scaleY)
        {
            var keys = _baseCurve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value *= scaleY;
                keys[i].inTangent *= scaleY;
                keys[i].outTangent *= scaleY;
            }
            
            _modifiedCurve = new AnimationCurve(keys);
        }
        
        /// <summary>
        /// 偏移曲线
        /// </summary>
        public void Offset(float offsetX, float offsetY)
        {
            var keys = _baseCurve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].time += offsetX;
                keys[i].value += offsetY;
            }
            
            _modifiedCurve = new AnimationCurve(keys);
        }
        
        /// <summary>
        /// 反转曲线
        /// </summary>
        public void Invert()
        {
            var keys = _baseCurve.keys;
            var invertedKeys = new Keyframe[keys.Length];
            
            for (int i = 0; i < keys.Length; i++)
            {
                int reverseIndex = keys.Length - 1 - i;
                invertedKeys[i] = new Keyframe(
                    keys[reverseIndex].time,
                    keys[reverseIndex].value,
                    -keys[reverseIndex].outTangent,
                    -keys[reverseIndex].inTangent
                );
            }
            
            _modifiedCurve = new AnimationCurve(invertedKeys);
        }
        
        /// <summary>
        /// 获取修改后的曲线
        /// </summary>
        public AnimationCurve GetModifiedCurve() => _modifiedCurve;
        
        /// <summary>
        /// 重置为基础曲线
        /// </summary>
        public void Reset()
        {
            _modifiedCurve = new AnimationCurve(_baseCurve.keys);
        }
    }
}
