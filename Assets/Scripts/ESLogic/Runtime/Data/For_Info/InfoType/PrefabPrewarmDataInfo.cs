using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [ESCreatePath("数据信息", "Prefab预热配置")]
    public sealed class PrefabPrewarmDataInfo : SoDataInfo
    {
        [Title("说明")]
        [ShowInInspector, ReadOnly, LabelText("用途")]
        private string Summary => "关卡/玩法打开前集中预热 Prefab，供 ESGameObjectPoolModule 使用。";

        [Title("场景支持")]
        [LabelText("支持所有场景")]
        public bool supportAllScenes = true;

        [HideIf(nameof(supportAllScenes))]
        [LabelText("支持场景")]
        public List<string> supportedScenes = new List<string>(4);

        [Title("Space")]
        [LabelText("支持所有Space")]
        public bool supportAllSpaces = true;

        [HideIf(nameof(supportAllSpaces))]
        [LabelText("支持Space")]
        public List<string> supportedSpaces = new List<string>(4);

        [Title("预热列表")]
        [LabelText("Prefab")]
        public List<PrefabPrewarmEntry> entries = new List<PrefabPrewarmEntry>(16);

        public bool SupportsScene(string sceneName)
        {
            if (supportAllScenes)
                return true;

            if (string.IsNullOrEmpty(sceneName) || supportedScenes == null)
                return false;

            int count = supportedScenes.Count;
            for (int i = 0; i < count; i++)
            {
                if (string.Equals(supportedScenes[i], sceneName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public bool SupportsSpace(string spaceName)
        {
            if (supportAllSpaces)
                return true;

            if (string.IsNullOrEmpty(spaceName) || supportedSpaces == null)
                return false;

            int count = supportedSpaces.Count;
            for (int i = 0; i < count; i++)
            {
                if (string.Equals(supportedSpaces[i], spaceName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public bool Supports(string sceneName, string spaceName)
        {
            return SupportsScene(sceneName) && SupportsSpace(spaceName);
        }
    }

    [Serializable]
    public sealed class PrefabPrewarmEntry
    {
        [LabelText("启用")]
        public bool enabled = true;

        [LabelText("Key")]
        public string key;

        [LabelText("Prefab")]
        public GameObject prefab;

        [LabelText("预热数量")]
        public int prewarmCount = 8;

        [LabelText("使用独立配置")]
        public bool useCustomConfig;

        [ShowIf(nameof(useCustomConfig))]
        [HideLabel]
        public ESGameObjectPoolConfig config = new ESGameObjectPoolConfig();
    }
}
