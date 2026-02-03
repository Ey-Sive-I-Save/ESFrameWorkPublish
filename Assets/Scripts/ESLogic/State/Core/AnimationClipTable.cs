using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// 动画Clip配置表 - 独立于状态机的Clip资源管理
    /// 支持复用、替换和动态加载
    /// </summary>
    [CreateAssetMenu(fileName = "NewClipTable", menuName = "ES/Animation/Clip Table", order = 1)]
    public class AnimationClipTable : ScriptableObject
    {
        [InfoBox("动画Clip配置表 - 按键查找Clip,支持复用和替换")]
        
        [TabGroup("Clips")]
        [LabelText("Clip映射")]
        [TableList(AlwaysExpanded = true, ShowIndexLabels = true)]
        public List<ClipEntry> clipEntries = new List<ClipEntry>();

        [TabGroup("Settings")]
        [LabelText("允许运行时修改")]
        [Tooltip("是否允许在运行时动态添加/替换Clip")]
        public bool allowRuntimeModification = true;

        [TabGroup("Settings")]
        [LabelText("预加载所有Clip")]
        [Tooltip("在初始化时预加载所有Clip到内存")]
        public bool preloadAllClips = false;

        // 运行时Clip缓存
        [NonSerialized]
        private Dictionary<string, AnimationClip> _clipCache;

        private void OnEnable()
        {
            InitializeCache();
        }

        private void InitializeCache()
        {
            if (_clipCache == null)
            {
                _clipCache = new Dictionary<string, AnimationClip>();
            }
            else
            {
                _clipCache.Clear();
            }

            if (clipEntries != null)
            {
                foreach (var entry in clipEntries)
                {
                    if (!string.IsNullOrEmpty(entry.key) && entry.clip != null)
                    {
                        _clipCache[entry.key] = entry.clip;
                    }
                }
            }
        }

        /// <summary>
        /// 根据键获取Clip
        /// </summary>
        public AnimationClip GetClip(string key)
        {
            if (_clipCache == null)
                InitializeCache();

            return _clipCache.TryGetValue(key, out var clip) ? clip : null;
        }

        /// <summary>
        /// 根据键获取Clip配置
        /// </summary>
        public StateAnimationConfigData GetClipConfiguration(string key, float stateEnterTime = 0f)
        {
            var entry = clipEntries?.Find(e => e.key == key);
            if (entry != null && entry.configuration != null)
            {
                return entry.configuration;
            }

            // 如果没有配置,返回默认配置
            if (_clipCache != null && _clipCache.TryGetValue(key, out var clip))
            {
                return new SimpleClipConfiguration { clip = clip };
            }

            return null;
        }

        /// <summary>
        /// 动态添加或替换Clip
        /// </summary>
        public void SetClip(string key, AnimationClip clip)
        {
            if (!allowRuntimeModification)
            {
                Debug.LogWarning($"Runtime modification is not allowed for ClipTable: {name}");
                return;
            }

            if (_clipCache == null)
                InitializeCache();

            _clipCache[key] = clip;
        }

        /// <summary>
        /// 移除Clip
        /// </summary>
        public void RemoveClip(string key)
        {
            if (!allowRuntimeModification)
            {
                Debug.LogWarning($"Runtime modification is not allowed for ClipTable: {name}");
                return;
            }

            if (_clipCache != null)
            {
                _clipCache.Remove(key);
            }
        }

        /// <summary>
        /// 检查Clip是否存在
        /// </summary>
        public bool HasClip(string key)
        {
            if (_clipCache == null)
                InitializeCache();

            return _clipCache.ContainsKey(key);
        }

        /// <summary>
        /// 获取所有Clip键
        /// </summary>
        public IEnumerable<string> GetAllKeys()
        {
            if (_clipCache == null)
                InitializeCache();

            return _clipCache.Keys;
        }

        /// <summary>
        /// 合并另一个ClipTable
        /// </summary>
        public void MergeFrom(AnimationClipTable other, bool overwrite = false)
        {
            if (other == null || other.clipEntries == null)
                return;

            if (_clipCache == null)
                InitializeCache();

            foreach (var entry in other.clipEntries)
            {
                if (string.IsNullOrEmpty(entry.key) || entry.clip == null)
                    continue;

                if (overwrite || !_clipCache.ContainsKey(entry.key))
                {
                    _clipCache[entry.key] = entry.clip;
                }
            }
        }

        [Button("验证Clip表", ButtonSizes.Medium)]
        [PropertySpace(10)]
        private void ValidateTable()
        {
            if (clipEntries == null || clipEntries.Count == 0)
            {
                Debug.LogWarning($"ClipTable '{name}' is empty!");
                return;
            }

            int validCount = 0;
            int invalidCount = 0;
            HashSet<string> keys = new HashSet<string>();

            foreach (var entry in clipEntries)
            {
                if (string.IsNullOrEmpty(entry.key))
                {
                    Debug.LogWarning($"Found entry with empty key in ClipTable '{name}'");
                    invalidCount++;
                    continue;
                }

                if (keys.Contains(entry.key))
                {
                    Debug.LogWarning($"Duplicate key '{entry.key}' found in ClipTable '{name}'");
                    invalidCount++;
                    continue;
                }

                if (entry.clip == null)
                {
                    Debug.LogWarning($"Entry with key '{entry.key}' has null clip in ClipTable '{name}'");
                    invalidCount++;
                    continue;
                }

                keys.Add(entry.key);
                validCount++;
            }

            Debug.Log($"ClipTable '{name}' validation complete: {validCount} valid, {invalidCount} invalid");
        }
    }

    /// <summary>
    /// Clip表条目
    /// </summary>
    [Serializable]
    public class ClipEntry
    {
        [LabelText("键")]
        [TableColumnWidth(150)]
        public string key;

        [LabelText("动画Clip")]
        [TableColumnWidth(200)]
        [AssetsOnly]
        public AnimationClip clip;

        [LabelText("配置")]
        [TableColumnWidth(150)]
        [InlineProperty]
        [SerializeReference]
        public StateAnimationConfigData configuration;

        [LabelText("标签")]
        [TableColumnWidth(100)]
        public List<string> tags = new List<string>();
    }

    /// <summary>
    /// 简单Clip配置 - 完整播放单个Clip
    /// </summary>
    [Serializable]
    public class SimpleClipConfiguration : StateAnimationConfigData
    {
        public AnimationClip clip;

        public override (AnimationClip clip, float normalizedTime) GetClipAndTime(StateMachineContext context)
        {
            return (clip, 0f);
        }
    }

    /// <summary>
    /// 随机Clip配置 - 从多个Clip中随机选择
    /// </summary>
    [Serializable]
    public class RandomClipConfiguration : StateAnimationConfigData
    {
        public List<AnimationClip> clips = new List<AnimationClip>();
        public List<float> weights = new List<float>(); // 权重(可选)

        [NonSerialized]
        private AnimationClip _selectedClip;

        public override (AnimationClip clip, float normalizedTime) GetClipAndTime(StateMachineContext context)
        {
            if (_selectedClip == null)
            {
                _selectedClip = SelectRandomClip();
            }
            return (_selectedClip, 0f);
        }

        private AnimationClip SelectRandomClip()
        {
            if (clips == null || clips.Count == 0)
                return null;

            if (weights == null || weights.Count == 0 || weights.Count != clips.Count)
            {
                // 无权重,均匀随机
                return clips[UnityEngine.Random.Range(0, clips.Count)];
            }

            // 加权随机
            float totalWeight = 0f;
            foreach (float w in weights)
                totalWeight += w;

            float random = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < clips.Count; i++)
            {
                cumulative += weights[i];
                if (random <= cumulative)
                    return clips[i];
            }

            return clips[clips.Count - 1];
        }
    }

    /// <summary>
    /// 序列Clip配置 - 按顺序播放多个Clip
    /// </summary>
    [Serializable]
    public class SequentialClipConfiguration : StateAnimationConfigData
    {
        public List<AnimationClip> clips = new List<AnimationClip>();
        
        [NonSerialized]
        private int _currentIndex = 0;

        public override (AnimationClip clip, float normalizedTime) GetClipAndTime(StateMachineContext context)
        {
            if (clips == null || clips.Count == 0)
                return (null, 0f);

            var clip = clips[_currentIndex % clips.Count];
            _currentIndex = (_currentIndex + 1) % clips.Count;

            return (clip, 0f);
        }
    }

    /// <summary>
    /// 参数驱动Clip配置 - 根据上下文参数选择Clip
    /// </summary>
    [Serializable]
    public class ParameterDrivenClipConfiguration : StateAnimationConfigData
    {
        public string parameterName;
        public List<ClipParameterMapping> mappings = new List<ClipParameterMapping>();

        public override (AnimationClip clip, float normalizedTime) GetClipAndTime(StateMachineContext context)
        {
            if (mappings != null && mappings.Count > 0)
            {
                // 尝试根据 context 中的参数选择映射（示例：使用 parameterName 作为 float/int/key）
                if (context != null)
                {
                    // 优先尝试 float
                    float f = context.GetFloat(parameterName, float.NaN);
                    if (!float.IsNaN(f))
                    {
                        // 简单匹配：找到第一个 mapping 参数值等于 (int)f
                        foreach (var m in mappings)
                        {
                            if (Mathf.Approximately(m.parameterValue, (int)f) && m.clip != null) return (m.clip, 0f);
                        }
                    }
                }

                return (mappings[0].clip, 0f);
            }
            return (null, 0f);
        }
    }

    [Serializable]
    public class ClipParameterMapping
    {
        public int parameterValue;
        public AnimationClip clip;
    }
}
