using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace ES
{
    public sealed class ESGameSaveMigrationContext
    {
        public string SlotId { get; private set; }
        public int TargetArchiveVersion { get; private set; }
        public string GameVersion { get; private set; }
        public string ConfigVersion { get; private set; }

        public ESGameSaveMigrationContext(string slotId, int targetArchiveVersion, string gameVersion, string configVersion)
        {
            SlotId = slotId;
            TargetArchiveVersion = targetArchiveVersion;
            GameVersion = gameVersion;
            ConfigVersion = configVersion;
        }
    }

    [Serializable]
    [TypeRegistryItem("保存系统/迁移规则基类")]
    public abstract class ESGameSaveMigrationRule
    {
        [HorizontalGroup("Version", Width = 0.5f), LabelText("从版本")]
        public int fromArchiveVersion;

        [HorizontalGroup("Version"), LabelText("到版本")]
        public int toArchiveVersion;

        [LabelText("说明"), TextArea(2, 4)]
        public string note;

        public bool CanMigrate(int currentVersion)
        {
            return fromArchiveVersion == currentVersion && toArchiveVersion > fromArchiveVersion;
        }

        public abstract string MigrateArchiveJson(string oldArchiveJson, ESGameSaveMigrationContext context);
    }

    [Serializable]
    [TypeRegistryItem("保存系统/示例迁移规则/不修改Json")]
    public sealed class ESGameSaveNoChangeMigrationRule : ESGameSaveMigrationRule
    {
        public override string MigrateArchiveJson(string oldArchiveJson, ESGameSaveMigrationContext context)
        {
            ESGameSaveArchive archive = UnityEngine.JsonUtility.FromJson<ESGameSaveArchive>(oldArchiveJson);
            if (archive != null)
            {
                archive.archiveVersion = toArchiveVersion;
                return UnityEngine.JsonUtility.ToJson(archive, true);
            }

            return oldArchiveJson;
        }
    }
}
