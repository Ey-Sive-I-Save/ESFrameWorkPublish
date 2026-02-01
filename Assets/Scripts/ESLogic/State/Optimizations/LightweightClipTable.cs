using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using System.Linq;

namespace ES.Optimizations
{
    /// <summary>4字节int vs 20+字节string
    /// 轻量级Clip引用 - 使用int索引而非string
    /// 性能提升: O(1)数组访问 vs O(n)字典查找
    /// 内存节省: 
    /// </summary>
    [Serializable]
    public struct ClipReference
    {
        [LabelText("Clip ID")]
        public int clipId;
        
        [NonSerialized]
        private AnimationClip _cachedClip;
        
        public AnimationClip GetClip(OptimizedClipTable table)
        {
            if (_cachedClip != null)
                return _cachedClip;
            
            _cachedClip = table.GetClipById(clipId);
            return _cachedClip;
        }
        
        public void ClearCache()
        {
            _cachedClip = null;
        }
        
        public static implicit operator ClipReference(int id) => new ClipReference { clipId = id };
        public static implicit operator int(ClipReference r) => r.clipId;
    }
    
    /// <summary>
    /// 优化的Clip配置表 - 支持int索引快速访问
    /// </summary>
    [CreateAssetMenu(fileName = "NewOptimizedClipTable", menuName = "ES/Animation/Optimized Clip Table")]
    public class OptimizedClipTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [LabelText("ID")]
            [ReadOnly]
            public int id;
            
            [LabelText("Key")]
            public string key;
            
            [LabelText("Clip")]
            [Required]
            public AnimationClip clip;
            
            [LabelText("Tags")]
            public List<string> tags = new List<string>();
        }
        
        [LabelText("Entries")]
        [TableList(AlwaysExpanded = true, ShowIndexLabels = true)]
        public List<Entry> entries = new List<Entry>();
        
        // 快速访问缓存
        [NonSerialized]
        private AnimationClip[] _idToClipArray;
        
        [NonSerialized]
        private Dictionary<string, int> _keyToId;
        
        [NonSerialized]
        private bool _isInitialized;
        
        private void OnEnable()
        {
            BuildCache();
        }
        
        /// <summary>
        /// 构建快速访问缓存
        /// </summary>
        public void BuildCache()
        {
            if (entries == null || entries.Count == 0)
            {
                _idToClipArray = Array.Empty<AnimationClip>();
                _keyToId = new Dictionary<string, int>();
                _isInitialized = true;
                return;
            }
            
            // 找到最大ID
            int maxId = 0;
            foreach (var entry in entries)
            {
                if (entry.id > maxId)
                    maxId = entry.id;
            }
            
            // 创建数组 (稀疏数组也可接受，因为访问O(1))
            _idToClipArray = new AnimationClip[maxId + 1];
            _keyToId = new Dictionary<string, int>(entries.Count);
            
            foreach (var entry in entries)
            {
                if (entry.id >= 0 && entry.id < _idToClipArray.Length)
                {
                    _idToClipArray[entry.id] = entry.clip;
                }
                
                if (!string.IsNullOrEmpty(entry.key))
                {
                    _keyToId[entry.key] = entry.id;
                }
            }
            
            _isInitialized = true;
        }
        
        /// <summary>
        /// O(1) 通过ID获取Clip - 零GC
        /// </summary>
        public AnimationClip GetClipById(int id)
        {
            if (!_isInitialized)
                BuildCache();
            
            if (id >= 0 && id < _idToClipArray.Length)
                return _idToClipArray[id];
            
            return null;
        }
        
        /// <summary>
        /// O(1) 通过字符串获取ID
        /// </summary>
        public int GetIdByKey(string key)
        {
            if (!_isInitialized)
                BuildCache();
            
            return _keyToId.TryGetValue(key, out int id) ? id : -1;
        }
        
        /// <summary>
        /// O(1) 通过字符串获取Clip (兼容旧API)
        /// </summary>
        public AnimationClip GetClipByKey(string key)
        {
            int id = GetIdByKey(key);
            return id >= 0 ? GetClipById(id) : null;
        }
        
        [Button("自动分配ID", ButtonSizes.Medium)]
        [PropertySpace(10)]
        private void AutoAssignIds()
        {
            if (entries == null) return;
            
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].id = i;
            }
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"已为 {entries.Count} 个条目自动分配ID");
#endif
        }
        
        [Button("验证Clip表", ButtonSizes.Medium)]
        private void Validate()
        {
            if (entries == null || entries.Count == 0)
            {
                Debug.LogWarning($"Clip表 '{name}' 为空!");
                return;
            }
            
            var idSet = new HashSet<int>();
            var keySet = new HashSet<string>();
            int duplicateIds = 0;
            int duplicateKeys = 0;
            int nullClips = 0;
            
            foreach (var entry in entries)
            {
                if (!idSet.Add(entry.id))
                    duplicateIds++;
                
                if (!string.IsNullOrEmpty(entry.key) && !keySet.Add(entry.key))
                    duplicateKeys++;
                
                if (entry.clip == null)
                    nullClips++;
            }
            
            if (duplicateIds > 0 || duplicateKeys > 0 || nullClips > 0)
            {
                Debug.LogWarning($"验证失败:\n" +
                    $"- 重复ID: {duplicateIds}\n" +
                    $"- 重复Key: {duplicateKeys}\n" +
                    $"- 空Clip: {nullClips}");
            }
            else
            {
                Debug.Log($"✓ Clip表 '{name}' 验证通过 ({entries.Count} 个条目)");
            }
        }
    }
    
    /// <summary>
    /// Clip ID生成器 - 编辑器工具
    /// </summary>
#if UNITY_EDITOR
    public static class ClipIdCodeGenerator
    {
        [UnityEditor.MenuItem("ES/Generate Clip ID Constants")]
        public static void GenerateClipIdConstants()
        {
            var tables = UnityEditor.AssetDatabase.FindAssets("t:OptimizedClipTable")
                .Select(guid => UnityEditor.AssetDatabase.LoadAssetAtPath<OptimizedClipTable>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guid)))
                .Where(t => t != null)
                .ToList();
            
            if (tables.Count == 0)
            {
                Debug.LogWarning("未找到OptimizedClipTable资源");
                return;
            }
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// Auto-generated - DO NOT EDIT");
            sb.AppendLine("// Generated at: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("namespace ES.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// 自动生成的Clip ID常量");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class ClipIds");
            sb.AppendLine("    {");
            
            var allIds = new Dictionary<string, int>();
            
            foreach (var table in tables)
            {
                if (table.entries == null) continue;
                
                foreach (var entry in table.entries)
                {
                    if (string.IsNullOrEmpty(entry.key)) continue;
                    
                    string constName = ToConstantName(entry.key);
                    
                    if (allIds.TryGetValue(constName, out int existingId))
                    {
                        if (existingId != entry.id)
                        {
                            Debug.LogWarning($"冲突的Key: {entry.key} (ID: {existingId} vs {entry.id})");
                        }
                    }
                    else
                    {
                        allIds[constName] = entry.id;
                    }
                }
            }
            
            foreach (var kvp in allIds.OrderBy(k => k.Value))
            {
                sb.AppendLine($"        public const int {kvp.Key} = {kvp.Value};");
            }
            
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Clip名称常量");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class ClipNames");
            sb.AppendLine("    {");
            
            var allNames = new HashSet<string>();
            foreach (var table in tables)
            {
                if (table.entries == null) continue;
                
                foreach (var entry in table.entries)
                {
                    if (string.IsNullOrEmpty(entry.key)) continue;
                    
                    if (allNames.Add(entry.key))
                    {
                        string constName = ToConstantName(entry.key);
                        sb.AppendLine($"        public const string {constName} = \"{entry.key}\";");
                    }
                }
            }
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            string outputPath = "Assets/Plugins/ES/Generated/ClipIds.cs";
            string directory = System.IO.Path.GetDirectoryName(outputPath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
            
            System.IO.File.WriteAllText(outputPath, sb.ToString());
            UnityEditor.AssetDatabase.Refresh();
            
            Debug.Log($"✓ 已生成 {allIds.Count} 个Clip ID常量到: {outputPath}");
        }
        
        private static string ToConstantName(string key)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                key.ToUpper(), 
                @"[^A-Z0-9]", 
                "_"
            );
        }
    }
#endif
}
