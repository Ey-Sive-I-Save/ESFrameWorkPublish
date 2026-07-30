#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>按需检查并修复资源验收集；禁止在域重载或程序集流期间自动写入资产。</summary>
    internal static class ESLevelAssetValidationAutoGenerator
    {
        private const string GameCorePath = "Assets/ESNormalAssets/ESValidation/LevelAssetFlow/Data/ESLevelAssetValidationGameCore.asset";
        private const string LibraryPath = "Assets/ESNormalAssets/ESValidation/LevelAssetFlow/Data/ESLevelAssetValidationLibrary.asset";
        private const string ConsumerPath = "Assets/ESNormalAssets/ESValidation/LevelAssetFlow/Data/ESLevelAssetValidationConsumer.asset";
        private const string MaterialsRoot = "Assets/ESNormalAssets/ESValidation/LevelAssetFlow/Materials";
        private const string EntryScenePath = "Assets/ESNormalAssets/ESValidation/LevelAssetFlow/Scenes/ESLevelAssetValidation.unity";

        [MenuItem("【ES】/示例与测试/资源卸载验收/检查并修复关卡资源验收集", false, 1)]
        private static void RepairIfNeededFromMenu()
        {
            RepairIfNeeded();
        }

        private static void RepairIfNeeded()
        {
            if (EditorApplication.isCompiling) return;
            ESLevelAssetValidationGameCore gameCore = AssetDatabase.LoadAssetAtPath<ESLevelAssetValidationGameCore>(GameCorePath);
            if (gameCore == null || RequiresResourceLayoutMigration(gameCore))
            {
                ESLevelAssetValidationGenerator.Generate();
                return;
            }

            // 旧版本曾把控制器和 GameCore 写在同一 .cs，Unity 会将该组件序列化为
            // 临时内嵌 MonoScript，重开场景后即变为 Missing Script。仅检测到此旧格式时重建入口。
            if (File.Exists(EntryScenePath)
                && !File.ReadAllText(EntryScenePath).Contains("componentTypeName: ES.ESLevelAssetValidationSceneController, ES_Logic"))
                ESLevelAssetValidationGenerator.RebuildEntrySceneForControllerFix();
            else
                Debug.Log("[ESValidation] 关卡资源验收集已完整，无需修复。");
        }

        private static bool RequiresResourceLayoutMigration(ESLevelAssetValidationGameCore gameCore)
        {
            ESAssetLibrary library = AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(LibraryPath);
            ESAssetLibraryConsumer consumer = AssetDatabase.LoadAssetAtPath<ESAssetLibraryConsumer>(ConsumerPath);
            if (library == null || consumer == null)
                return true;

            bool gameCoreMissingFromAssetTable = !library.GetAllUseableBooks()
                .Any(book => book?.pages != null && book.pages.Any(page => page?.OB == gameCore));
            if (gameCoreMissingFromAssetTable)
                return true;

            int generatedMaterialCount = AssetDatabase.FindAssets("t:Material", new[] { MaterialsRoot }).Length;
            int registeredMaterialCount = library.GetPagesByKind(ESAssetReferKind.Material)
                .Count(page => page?.OB is Material && AssetDatabase.GetAssetPath(page.OB).StartsWith(MaterialsRoot, System.StringComparison.Ordinal));
            if (generatedMaterialCount == 0 || registeredMaterialCount != generatedMaterialCount)
                return true;

            const string prefabRoot = "Assets/ESNormalAssets/ESValidation/LevelAssetFlow/Prefabs/Level01_Blocks";
            string[] colorNames = { "Red", "Green", "Blue", "Yellow", "Magenta", "Cyan" };
            for (int index = 0; index < colorNames.Length; index++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabRoot + "/Level01_Blocks_" + colorNames[index] + ".prefab");
                if (prefab == null) return true;
                string expectedTypeName = (index % 3) switch
                {
                    0 => "ES.ESHotUpdateLoopMoveHorizontal",
                    1 => "ES.ESHotUpdateLoopMoveVertical",
                    _ => "ES.ESHotUpdateLoopMoveDepth"
                };
                System.Type expectedType = System.Type.GetType(expectedTypeName + ", ES_Logic", false);
                if (expectedType == null) return true;
                bool hasExpectedMotion = prefab.GetComponent(expectedType) != null;
                if (!hasExpectedMotion) return true;
            }

            ESAssetIdentity gameCoreIdentity = new ESAssetIdentity(AssetDatabase.AssetPathToGUID(GameCorePath), 0);
            return consumer.GameCoreAssets == null
                || !consumer.GameCoreAssets.Any(item => item != null && item.AssetIdentity.Equals(gameCoreIdentity));
        }
    }
}
#endif
