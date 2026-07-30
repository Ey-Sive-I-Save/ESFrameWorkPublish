using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace ES
{
    /// <summary>Consumer 的发布版本只在新版 AB 构建开始时统一递增。</summary>
    internal static class ESAssetConsumerBuildRevision
    {
        internal static IReadOnlyList<ESAssetLibraryConsumer> IncrementAllForBuild()
        {
            var consumers = ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>()
                .Where(item => item != null)
                .OrderBy(item => item.ConsumerId, StringComparer.Ordinal)
                .ToList();

            foreach (var consumer in consumers)
            {
                consumer.IncrementBuildRevision();
                EditorUtility.SetDirty(consumer);
            }

            AssetDatabase.SaveAssets();
            return consumers;
        }
    }
}
