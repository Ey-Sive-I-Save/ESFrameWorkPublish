#if UNITY_EDITOR
using UnityEngine;

namespace ES
{
    /// <summary>World 只声明工作台槽位语义，注册流程由 ESWorkbench 基础层执行。</summary>
    internal static class ESWorldMapWorkbenchSlots
    {
        public static ESWorkbenchAssetRegistrationSlot Material(string libraryPath, int index)
        {
            return new ESWorkbenchAssetRegistrationSlot(
                "world.material." + index,
                "材质资源",
                "拖入 Material，预检后提交到统一 ES 资源注册入口。",
                "definition.materialLayers.Array.data[" + index + "].materialKey",
                libraryPath,
                typeof(Material),
                ESAssetReferKind.Material,
                "绑定 ES 地形材质",
                "world.material-registration");
        }

        public static ESWorkbenchAssetRegistrationSlot Vegetation(string libraryPath, int index)
        {
            return new ESWorkbenchAssetRegistrationSlot(
                "world.vegetation." + index,
                "植被 Prefab",
                "拖入已准备作为植被来源的 Prefab；提交后只绑定稳定资源 Key。",
                "definition.vegetationLayers.Array.data[" + index + "].prefabSetKey",
                libraryPath,
                typeof(GameObject),
                ESAssetReferKind.Prefab,
                "绑定 ES 植被 Prefab",
                "world.vegetation-registration");
        }

        public static ESWorkbenchAssetRegistrationSlot Scatter(string libraryPath, int index)
        {
            return new ESWorkbenchAssetRegistrationSlot(
                "world.scatter." + index,
                "散布 Prefab",
                "拖入散布 Prefab；预览放置和正式输出仍是互相隔离的阶段。",
                "definition.scatterLayers.Array.data[" + index + "].prefabSetKey",
                libraryPath,
                typeof(GameObject),
                ESAssetReferKind.Prefab,
                "绑定 ES 散布 Prefab",
                "world.scatter-registration");
        }
    }
}
#endif
