using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public static class ESGameSaveKeys
    {
        public const string ArchiveKey = "ES.GameSave.ArchiveJson";
        public const string DefaultSaveFolder = "ESGameSaves";
        public const string DefaultSlotId = "slot_001";
        public const string FileExtension = ".es3";
        public const string TempSuffix = ".tmp";
        public const string BackupSuffix = ".bak";
        public const int CurrentArchiveVersion = 1;
    }

    [Serializable]
    public sealed class ESGameSaveSlotInfo
    {
        [HorizontalGroup("Line", Width = 0.25f), LabelText("槽位")]
        public string slotId;

        [HorizontalGroup("Line", Width = 0.35f), LabelText("显示名")]
        public string displayName;

        [HorizontalGroup("Line"), LabelText("保存时间")]
        public long savedUtcTicks;

        [LabelText("游戏版本")]
        public string gameVersion;

        [LabelText("配置版本")]
        public string configVersion;

        [LabelText("Archive版本")]
        public int archiveVersion;

        [LabelText("文件大小")]
        public long fileSizeBytes;

        [ShowInInspector, ReadOnly, LabelText("保存时间(本地)")]
        public string SavedLocalTimeText
        {
            get
            {
                if (savedUtcTicks <= 0)
                    return "<未保存>";

                return new DateTime(savedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
    }

    [Serializable]
    public sealed class ESGameSaveSectionPacket
    {
        [HorizontalGroup("Top", Width = 0.34f), LabelText("分区Key")]
        public string sectionKey;

        [HorizontalGroup("Top", Width = 0.22f), LabelText("结构版本")]
        public int schemaVersion = 1;

        [HorizontalGroup("Top"), LabelText("类型")]
        public string typeName;

        [TextArea(4, 16), LabelText("Json")]
        public string json;

        public static ESGameSaveSectionPacket FromData<T>(string sectionKey, T data, int schemaVersion = 1, bool prettyPrint = false)
        {
            return new ESGameSaveSectionPacket
            {
                sectionKey = sectionKey,
                schemaVersion = schemaVersion,
                typeName = typeof(T).FullName,
                json = JsonUtility.ToJson(data, prettyPrint)
            };
        }

        public bool TryRead<T>(out T value)
        {
            value = default;
            if (string.IsNullOrEmpty(json))
                return false;

            try
            {
                value = JsonUtility.FromJson<T>(json);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }
    }

    [Serializable]
    public sealed class ESGameSaveArchive
    {
        [TabGroup("摘要"), LabelText("槽位")]
        public string slotId = ESGameSaveKeys.DefaultSlotId;

        [TabGroup("摘要"), LabelText("显示名")]
        public string displayName = "默认存档";

        [TabGroup("摘要"), LabelText("Archive版本")]
        public int archiveVersion = ESGameSaveKeys.CurrentArchiveVersion;

        [TabGroup("摘要"), LabelText("游戏版本")]
        public string gameVersion = "0.0.1";

        [TabGroup("摘要"), LabelText("配置版本")]
        public string configVersion = "0";

        [TabGroup("摘要"), LabelText("保存UTC Ticks")]
        public long savedUtcTicks;

        [TabGroup("内容"), ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "sectionKey", DraggableItems = false)]
        [LabelText("保存分区")]
        public List<ESGameSaveSectionPacket> sections = new List<ESGameSaveSectionPacket>();

        public void PrepareBeforeSave(string useSlotId, string useDisplayName, int useArchiveVersion, string useGameVersion, string useConfigVersion)
        {
            slotId = string.IsNullOrWhiteSpace(useSlotId) ? ESGameSaveKeys.DefaultSlotId : useSlotId.Trim();
            displayName = string.IsNullOrWhiteSpace(useDisplayName) ? slotId : useDisplayName.Trim();
            archiveVersion = useArchiveVersion;
            gameVersion = useGameVersion ?? string.Empty;
            configVersion = useConfigVersion ?? string.Empty;
            savedUtcTicks = DateTime.UtcNow.Ticks;
            if (sections == null)
                sections = new List<ESGameSaveSectionPacket>();
        }

        public ESGameSaveSectionPacket FindSection(string sectionKey)
        {
            if (sections == null || string.IsNullOrEmpty(sectionKey))
                return null;

            for (int i = 0; i < sections.Count; i++)
            {
                ESGameSaveSectionPacket section = sections[i];
                if (section != null && string.Equals(section.sectionKey, sectionKey, StringComparison.Ordinal))
                    return section;
            }

            return null;
        }

        public void UpsertSection(ESGameSaveSectionPacket packet)
        {
            if (packet == null || string.IsNullOrEmpty(packet.sectionKey))
                return;

            if (sections == null)
                sections = new List<ESGameSaveSectionPacket>();

            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i] != null && string.Equals(sections[i].sectionKey, packet.sectionKey, StringComparison.Ordinal))
                {
                    sections[i] = packet;
                    return;
                }
            }

            sections.Add(packet);
        }

        public ESGameSaveSlotInfo ToSlotInfo(long fileSizeBytes)
        {
            return new ESGameSaveSlotInfo
            {
                slotId = slotId,
                displayName = displayName,
                savedUtcTicks = savedUtcTicks,
                gameVersion = gameVersion,
                configVersion = configVersion,
                archiveVersion = archiveVersion,
                fileSizeBytes = fileSizeBytes
            };
        }
    }

    [Serializable]
    public sealed class ESGameSaveOperationReport
    {
        [LabelText("成功")]
        public bool success;

        [LabelText("槽位")]
        public string slotId;

        [LabelText("信息"), TextArea(2, 5)]
        public string message;

        [LabelText("耗时ms")]
        public double elapsedMs;

        [LabelText("文件大小")]
        public long fileSizeBytes;
    }

    [Serializable]
    public sealed class ESGameSaveDebugPlayerSnapshot
    {
        public string sceneName;
        public Vector3 position;
        public Vector3 eulerAngles;
        public int level = 1;
        public int hp = 100;
    }
}
