using System.Collections.Generic;
using UnityEngine;

namespace ES.AIPreview.Mod
{
    /// <summary>
    /// Mod 定义资产：
    /// - 描述一个 Mod 的基础信息与包含的内容集合；
    /// - 内容本身通过其他 ScriptableObject（角色、物品、任务等）引用。
    /// </summary>
    [CreateAssetMenu(menuName = "ES/Preview/Mod/ModDefinition")]
    public class ModDefinition : ScriptableObject
    {
        [Header("基础信息")]
        public string ModId;
        public string DisplayName;
        [TextArea]
        public string Description;

        [Header("启用状态")]
        public bool Enabled = true;

        [Header("内容资产引用")]
        public List<ScriptableObject> Characters = new List<ScriptableObject>();
        public List<ScriptableObject> Items = new List<ScriptableObject>();
        public List<ScriptableObject> Quests = new List<ScriptableObject>();
    }

    /// <summary>
    /// 运行时 Mod 管理器原型：
    /// - 扫描所有 ModDefinition；
    /// - 按 Id 建立 Mod 表；
    /// - 提供简单的启用/禁用与遍历接口。
    /// </summary>
    public class ModManager : MonoBehaviour
    {
        [Tooltip("可在 Inspector 中手动指定要加载的 Mod；为空则运行时自动扫描 Resources 或 Addressables")] 
        public List<ModDefinition> Mods = new List<ModDefinition>();

        private readonly Dictionary<string, ModDefinition> _modMap = new Dictionary<string, ModDefinition>();

        private void Awake()
        {
            BuildMap();
        }

        private void BuildMap()
        {
            _modMap.Clear();
            foreach (var mod in Mods)
            {
                if (mod == null || string.IsNullOrEmpty(mod.ModId)) continue;
                _modMap[mod.ModId] = mod;
            }
        }

        public IEnumerable<ModDefinition> GetEnabledMods()
        {
            foreach (var kv in _modMap)
            {
                if (kv.Value != null && kv.Value.Enabled)
                    yield return kv.Value;
            }
        }

        public bool TryGetMod(string modId, out ModDefinition mod)
        {
            return _modMap.TryGetValue(modId, out mod);
        }
    }
}
