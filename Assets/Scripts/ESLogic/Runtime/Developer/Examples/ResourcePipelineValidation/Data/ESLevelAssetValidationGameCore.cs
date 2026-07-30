using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class ESLevelAssetValidationLevel
    {
        [LabelText("关卡名")] public string levelName;
        [LabelText("关卡场景"), SerializeReference] public ESAssetReferScene scene = new ESAssetReferScene();
        [LabelText("资源计划")] public ESResourcePlanInfo resourcePlan;
    }

    /// <summary>三关几何资源验收的 GameCore 根。构建时应放入 Consumer 的启动 GameCore 列表。</summary>
    [CreateAssetMenu(fileName = "ESLevelAssetValidationGameCore", menuName = "【ES】/示例与测试/资源卸载验收/关卡 GameCore")]
    public sealed class ESLevelAssetValidationGameCore : ScriptableObject, IGameCoreSO
    {
        public const string TableKey = "es_level_asset_validation";
        public List<ESLevelAssetValidationLevel> levels = new List<ESLevelAssetValidationLevel>();

        public void InjectGameCoreTables() => ESLevelAssetValidationGameCoreTable.Register(this);
    }

    /// <summary>用独立表证明验收关卡的数据来自 Consumer GameCore，而非场景直引。</summary>
    public static class ESLevelAssetValidationGameCoreTable
    {
        private static ESLevelAssetValidationGameCore current;
        public static bool TryGet(out ESLevelAssetValidationGameCore gameCore)
        {
            gameCore = current;
            return gameCore != null;
        }

        public static void Register(ESLevelAssetValidationGameCore gameCore)
        {
            if (gameCore == null) throw new ArgumentNullException(nameof(gameCore));
            current = gameCore;
        }
    }

}
