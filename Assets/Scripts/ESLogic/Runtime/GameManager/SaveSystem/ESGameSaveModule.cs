using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace ES
{
    [Serializable]
    [TypeRegistryItem("保存系统")]
    public sealed class ESGameSaveModule : ESSystemModule
    {
        [TabGroup("总览", TabLayouting = TabLayouting.MultiRow)]
        [InfoBox("保存系统采用：业务数据 -> 分区Json -> Archive整包Json -> ES3加密/压缩文件。业务侧不要直接散写 ES3 key。")]
        [ShowInInspector, ReadOnly, LabelText("说明")]
        private string Summary => "整包保存 / 加密 / 压缩 / 备份 / 版本迁移 / Odin调试";

        [TabGroup("设置", TabLayouting = TabLayouting.MultiRow), LabelText("保存目录")]
        public string saveFolder = ESGameSaveKeys.DefaultSaveFolder;

        [TabGroup("设置", TabLayouting = TabLayouting.MultiRow), LabelText("默认槽位")]
        public string defaultSlotId = ESGameSaveKeys.DefaultSlotId;

        [TabGroup("设置", TabLayouting = TabLayouting.MultiRow), LabelText("Archive当前版本"), MinValue(1)]
        public int currentArchiveVersion = ESGameSaveKeys.CurrentArchiveVersion;

        [TabGroup("设置", TabLayouting = TabLayouting.MultiRow), LabelText("游戏版本")]
        public string gameVersion = "0.0.1";

        [TabGroup("设置", TabLayouting = TabLayouting.MultiRow), LabelText("配置版本")]
        public string configVersion = "0";

        [TabGroup("稳定性", TabLayouting = TabLayouting.MultiRow), LabelText("保存前创建备份")]
        public bool createBackupBeforeReplace = true;

        [TabGroup("稳定性", TabLayouting = TabLayouting.MultiRow), LabelText("写入后验证")]
        public bool verifyAfterWrite = true;

        [TabGroup("稳定性", TabLayouting = TabLayouting.MultiRow), LabelText("迁移失败时禁止读取")]
        public bool blockLoadWhenMigrationMissing = true;

        [TabGroup("稳定性", TabLayouting = TabLayouting.MultiRow), LabelText("保留失败临时文件")]
        public bool keepFailedTempFile = false;

        [TabGroup("加密", TabLayouting = TabLayouting.MultiRow), LabelText("启用加密")]
        public bool enableEncryption = true;

        [TabGroup("加密", TabLayouting = TabLayouting.MultiRow), LabelText("启用压缩")]
        public bool enableCompression = true;

        [TabGroup("加密", TabLayouting = TabLayouting.MultiRow), LabelText("加密密码")]
        [PropertyTooltip("正式项目不要用默认密码；建议由设备信息、账号信息或项目密钥组合派生。")]
        public string encryptionPassword = "ESFrameWork_Save_ChangeMe";

        [TabGroup("迁移", TabLayouting = TabLayouting.MultiRow)]
        [SerializeReference, ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "note")]
        [LabelText("Archive迁移规则")]
        public List<ESGameSaveMigrationRule> migrationRules = new List<ESGameSaveMigrationRule>();

        [TabGroup("调试", TabLayouting = TabLayouting.MultiRow), LabelText("调试槽位")]
        public string debugSlotId = ESGameSaveKeys.DefaultSlotId;

        [TabGroup("调试", TabLayouting = TabLayouting.MultiRow), LabelText("调试分区")]
        public string debugSectionKey = "player.snapshot";

        [TabGroup("调试", TabLayouting = TabLayouting.MultiRow), LabelText("调试快照")]
        public ESGameSaveDebugPlayerSnapshot debugSnapshot = new ESGameSaveDebugPlayerSnapshot();

        [TabGroup("状态", TabLayouting = TabLayouting.MultiRow), ShowInInspector, ReadOnly, LabelText("最近操作")]
        public ESGameSaveOperationReport LastReport { get; private set; } = new ESGameSaveOperationReport();

        [TabGroup("状态", TabLayouting = TabLayouting.MultiRow), ShowInInspector, ReadOnly, LabelText("默认槽位存在")]
        public bool DefaultSlotExists => Has(defaultSlotId);

        [TabGroup("状态", TabLayouting = TabLayouting.MultiRow), ShowInInspector, ReadOnly, LabelText("当前缓存槽位")]
        public string CurrentSlotId => cachedSlotId;

        [TabGroup("状态", TabLayouting = TabLayouting.MultiRow), ShowInInspector, ReadOnly, LabelText("缓存显示名")]
        public string CurrentDisplayName => cachedDisplayName;

        [TabGroup("状态", TabLayouting = TabLayouting.MultiRow), ShowInInspector, ReadOnly, LabelText("缓存已修改")]
        public bool IsDirty => isDirty;

        [TabGroup("状态", TabLayouting = TabLayouting.MultiRow), ShowInInspector, ReadOnly, LabelText("缓存分区数")]
        public int CachedSectionCount => cachedArchive != null && cachedArchive.sections != null ? cachedArchive.sections.Count : 0;

        [NonSerialized]
        private ESGameSaveArchive cachedArchive;

        [NonSerialized]
        private string cachedSlotId;

        [NonSerialized]
        private string cachedDisplayName;

        [NonSerialized]
        private bool isDirty;

        [NonSerialized]
        private readonly HashSet<string> dirtySections = new HashSet<string>();

        [OnInspectorGUI]
        private void DrawInspectorHint()
        {
            GUILayout.Space(4);
            GUILayout.Label("建议：保存玩家进度时只保存可还原数据，不要直接保存 Entity / StateMachine / SkillRuntime 整对象。");
        }

        [TabGroup("调试", TabLayouting = TabLayouting.MultiRow), Button("保存调试快照", ButtonSizes.Medium), GUIColor(0.35f, 0.9f, 0.55f)]
        public void DebugSaveSnapshot()
        {
            if (debugSnapshot == null)
                debugSnapshot = new ESGameSaveDebugPlayerSnapshot();

            debugSnapshot.sceneName = SceneManager.GetActiveScene().name;
            Set(debugSlotId, "调试存档", debugSectionKey, debugSnapshot);
            Save(debugSlotId);
        }

        [TabGroup("调试", TabLayouting = TabLayouting.MultiRow), Button("读取调试快照", ButtonSizes.Medium), GUIColor(0.45f, 0.75f, 1f)]
        public void DebugLoadSnapshot()
        {
            if (Load(debugSlotId) && Get(debugSectionKey, out ESGameSaveDebugPlayerSnapshot snapshot))
                debugSnapshot = snapshot;
        }

        [TabGroup("调试", TabLayouting = TabLayouting.MultiRow), Button("删除调试槽位", ButtonSizes.Medium), GUIColor(1f, 0.45f, 0.35f)]
        public void DebugDeleteSlot()
        {
            Delete(debugSlotId);
        }

        public void Set<T>(string sectionKey, T data)
        {
            Set(defaultSlotId, defaultSlotId, sectionKey, data);
        }

        public void Set<T>(string slotId, string sectionKey, T data)
        {
            Set(slotId, slotId, sectionKey, data);
        }

        public void Set<T>(string slotId, string displayName, string sectionKey, T data)
        {
            slotId = NormalizeSlotId(slotId);
            EnsureCacheForSlot(slotId, displayName, loadExisting: true);
            cachedArchive.UpsertSection(ESGameSaveSectionPacket.FromData(sectionKey, data, 1, false));
            dirtySections.Add(sectionKey);
            isDirty = true;
        }

        public bool Get<T>(string sectionKey, out T value)
        {
            value = default;
            EnsureCacheForSlot(defaultSlotId, defaultSlotId, loadExisting: false);
            return TryReadCachedSection(sectionKey, out value);
        }

        public bool Get<T>(string slotId, string sectionKey, out T value)
        {
            value = default;
            EnsureCacheForSlot(NormalizeSlotId(slotId), slotId, loadExisting: false);
            return TryReadCachedSection(sectionKey, out value);
        }

        public bool Save()
        {
            return Save(GetActiveSlotOrDefault());
        }

        public bool Save(string slotId)
        {
            slotId = NormalizeSlotId(slotId);
            EnsureCacheForSlot(slotId, slotId, loadExisting: false);

            if (!isDirty)
            {
                LastReport = BuildReport(true, slotId, "没有修改，不需要保存", 0, GetFileSize(BuildSettings(BuildSlotPath(slotId))));
                return true;
            }

            bool success = SavePack(slotId, string.IsNullOrWhiteSpace(cachedDisplayName) ? slotId : cachedDisplayName, cachedArchive);
            if (success)
            {
                isDirty = false;
                dirtySections.Clear();
            }

            return success;
        }

        public bool Load()
        {
            return Load(defaultSlotId);
        }

        public bool Load(string slotId)
        {
            slotId = NormalizeSlotId(slotId);
            if (!LoadPack(slotId, out ESGameSaveArchive archive))
                return false;

            cachedArchive = archive;
            cachedSlotId = slotId;
            cachedDisplayName = string.IsNullOrWhiteSpace(archive.displayName) ? slotId : archive.displayName;
            isDirty = false;
            dirtySections.Clear();
            return true;
        }

        public bool Has()
        {
            return Has(defaultSlotId);
        }

        public bool Has(string slotId)
        {
            ES3Settings settings = BuildSettings(BuildSlotPath(NormalizeSlotId(slotId)));
            return ES3.FileExists(settings);
        }

        public bool Delete()
        {
            return Delete(defaultSlotId);
        }

        public bool Delete(string slotId)
        {
            slotId = NormalizeSlotId(slotId);
            bool deleted = false;

            DeleteFileIfExists(BuildSlotPath(slotId), ref deleted);
            DeleteFileIfExists(BuildTempPath(slotId), ref deleted);
            DeleteFileIfExists(BuildBackupPath(slotId), ref deleted);

            if (string.Equals(cachedSlotId, slotId, StringComparison.Ordinal))
                ClearCache();

            LastReport = BuildReport(deleted, slotId, deleted ? "删除完成" : "没有可删除的存档", 0, 0);
            return deleted;
        }

        public ESGameSaveSlotInfo Info()
        {
            return Info(defaultSlotId);
        }

        public ESGameSaveSlotInfo Info(string slotId)
        {
            slotId = NormalizeSlotId(slotId);
            if (LoadPack(slotId, out ESGameSaveArchive archive))
                return archive.ToSlotInfo(GetFileSize(BuildSettings(BuildSlotPath(slotId))));

            return null;
        }

        public void ClearCache()
        {
            cachedArchive = null;
            cachedSlotId = null;
            cachedDisplayName = null;
            isDirty = false;
            dirtySections.Clear();
        }

        private bool TryReadCachedSection<T>(string sectionKey, out T value)
        {
            value = default;
            if (cachedArchive == null)
                return false;

            ESGameSaveSectionPacket packet = cachedArchive.FindSection(sectionKey);
            return packet != null && packet.TryRead(out value);
        }

        private void EnsureCacheForSlot(string slotId, string displayName, bool loadExisting)
        {
            slotId = NormalizeSlotId(slotId);
            if (cachedArchive != null && string.Equals(cachedSlotId, slotId, StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(displayName))
                    cachedDisplayName = displayName;
                return;
            }

            if (loadExisting && LoadPack(slotId, out ESGameSaveArchive archive))
            {
                cachedArchive = archive;
                cachedDisplayName = string.IsNullOrWhiteSpace(displayName) ? archive.displayName : displayName;
            }
            else
            {
                cachedArchive = new ESGameSaveArchive();
                cachedDisplayName = string.IsNullOrWhiteSpace(displayName) ? slotId : displayName;
            }

            cachedSlotId = slotId;
            isDirty = false;
            dirtySections.Clear();
        }

        private string GetActiveSlotOrDefault()
        {
            return string.IsNullOrWhiteSpace(cachedSlotId) ? defaultSlotId : cachedSlotId;
        }

        private bool SavePack(string slotId, string displayName, ESGameSaveArchive archive)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            slotId = NormalizeSlotId(slotId);

            try
            {
                if (archive == null)
                    archive = new ESGameSaveArchive();

                archive.PrepareBeforeSave(slotId, displayName, currentArchiveVersion, gameVersion, configVersion);
                string archiveJson = JsonUtility.ToJson(archive, true);
                bool success = SaveArchiveJsonAtomic(slotId, archiveJson, out string message, out long fileSize);

                stopwatch.Stop();
                LastReport = new ESGameSaveOperationReport
                {
                    success = success,
                    slotId = slotId,
                    message = message,
                    elapsedMs = stopwatch.Elapsed.TotalMilliseconds,
                    fileSizeBytes = fileSize
                };

                return success;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                LastReport = new ESGameSaveOperationReport
                {
                    success = false,
                    slotId = slotId,
                    message = exception.Message,
                    elapsedMs = stopwatch.Elapsed.TotalMilliseconds
                };
                Debug.LogException(exception);
                return false;
            }
        }

        private bool LoadPack(string slotId, out ESGameSaveArchive archive)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            archive = null;
            slotId = NormalizeSlotId(slotId);

            try
            {
                ES3Settings settings = BuildSettings(BuildSlotPath(slotId));
                if (!ES3.FileExists(settings) || !ES3.KeyExists(ESGameSaveKeys.ArchiveKey, settings))
                {
                    stopwatch.Stop();
                    LastReport = BuildReport(false, slotId, "存档不存在", stopwatch.Elapsed.TotalMilliseconds, 0);
                    return false;
                }

                string archiveJson = ES3.Load<string>(ESGameSaveKeys.ArchiveKey, settings);
                if (!TryMigrateArchiveJson(slotId, ref archiveJson, out string migrateMessage))
                {
                    stopwatch.Stop();
                    LastReport = BuildReport(false, slotId, migrateMessage, stopwatch.Elapsed.TotalMilliseconds, GetFileSize(settings));
                    return false;
                }

                archive = JsonUtility.FromJson<ESGameSaveArchive>(archiveJson);
                if (archive == null)
                {
                    stopwatch.Stop();
                    LastReport = BuildReport(false, slotId, "Json解析失败", stopwatch.Elapsed.TotalMilliseconds, GetFileSize(settings));
                    return false;
                }

                if (archive.sections == null)
                    archive.sections = new List<ESGameSaveSectionPacket>();

                stopwatch.Stop();
                LastReport = BuildReport(true, slotId, string.IsNullOrEmpty(migrateMessage) ? "读取成功" : migrateMessage, stopwatch.Elapsed.TotalMilliseconds, GetFileSize(settings));
                return true;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                LastReport = BuildReport(false, slotId, exception.Message, stopwatch.Elapsed.TotalMilliseconds, 0);
                Debug.LogException(exception);
                return false;
            }
        }

        private bool SaveArchiveJsonAtomic(string slotId, string archiveJson, out string message, out long fileSizeBytes)
        {
            string mainPath = BuildSlotPath(slotId);
            string tempPath = BuildTempPath(slotId);
            string backupPath = BuildBackupPath(slotId);

            ES3Settings mainSettings = BuildSettings(mainPath);
            ES3Settings tempSettings = BuildSettings(tempPath);
            ES3Settings backupSettings = BuildSettings(backupPath);

            fileSizeBytes = 0;
            message = string.Empty;

            try
            {
                if (ES3.FileExists(tempSettings))
                    ES3.DeleteFile(tempSettings);

                ES3.Save(ESGameSaveKeys.ArchiveKey, archiveJson, tempSettings);

                if (verifyAfterWrite)
                {
                    string checkJson = ES3.Load<string>(ESGameSaveKeys.ArchiveKey, tempSettings);
                    if (!string.Equals(checkJson, archiveJson, StringComparison.Ordinal))
                    {
                        message = "写入验证失败";
                        if (!keepFailedTempFile)
                            ES3.DeleteFile(tempSettings);
                        return false;
                    }
                }

                if (createBackupBeforeReplace && ES3.FileExists(mainSettings))
                {
                    if (ES3.FileExists(backupSettings))
                        ES3.DeleteFile(backupSettings);

                    ES3.CopyFile(mainSettings, backupSettings);
                }

                if (ES3.FileExists(mainSettings))
                    ES3.DeleteFile(mainSettings);

                ES3.RenameFile(tempSettings, mainSettings);
                fileSizeBytes = GetFileSize(mainSettings);
                message = "保存成功";
                return true;
            }
            catch (Exception exception)
            {
                message = exception.Message;

                try
                {
                    if (!ES3.FileExists(mainSettings) && ES3.FileExists(backupSettings))
                        ES3.CopyFile(backupSettings, mainSettings);
                }
                catch (Exception restoreException)
                {
                    Debug.LogException(restoreException);
                }

                Debug.LogException(exception);
                return false;
            }
        }

        private bool TryMigrateArchiveJson(string slotId, ref string archiveJson, out string message)
        {
            message = string.Empty;
            ESGameSaveArchive header = JsonUtility.FromJson<ESGameSaveArchive>(archiveJson);
            if (header == null)
            {
                message = "无法读取存档头";
                return false;
            }

            int version = header.archiveVersion;
            if (version == currentArchiveVersion)
                return true;

            if (version > currentArchiveVersion)
            {
                message = $"存档版本过新：{version} > {currentArchiveVersion}";
                return false;
            }

            ESGameSaveMigrationContext context = new ESGameSaveMigrationContext(slotId, currentArchiveVersion, gameVersion, configVersion);
            int guard = 0;
            while (version < currentArchiveVersion && guard++ < 32)
            {
                ESGameSaveMigrationRule rule = FindMigrationRule(version);
                if (rule == null)
                {
                    message = $"缺少存档迁移规则：{version} -> ?";
                    return !blockLoadWhenMigrationMissing;
                }

                archiveJson = rule.MigrateArchiveJson(archiveJson, context);
                version = rule.toArchiveVersion;
            }

            message = $"已迁移到版本 {version}";
            return version == currentArchiveVersion;
        }

        private ESGameSaveMigrationRule FindMigrationRule(int fromVersion)
        {
            if (migrationRules == null)
                return null;

            for (int i = 0; i < migrationRules.Count; i++)
            {
                ESGameSaveMigrationRule rule = migrationRules[i];
                if (rule != null && rule.CanMigrate(fromVersion))
                    return rule;
            }

            return null;
        }

        private ES3Settings BuildSettings(string path)
        {
            ES3Settings settings = new ES3Settings(path);
            settings.location = ES3.Location.File;
            settings.directory = ES3.Directory.PersistentDataPath;
            settings.format = ES3.Format.JSON;
            settings.encryptionType = enableEncryption ? ES3.EncryptionType.AES : ES3.EncryptionType.None;
            settings.encryptionPassword = string.IsNullOrEmpty(encryptionPassword) ? "password" : encryptionPassword;
            settings.compressionType = enableCompression ? ES3.CompressionType.Gzip : ES3.CompressionType.None;
            settings.prettyPrint = false;
            settings.safeReflection = true;
            return settings;
        }

        private string BuildSlotPath(string slotId)
        {
            return NormalizeFolder(saveFolder) + "/" + NormalizeSlotId(slotId) + ESGameSaveKeys.FileExtension;
        }

        private string BuildTempPath(string slotId)
        {
            return BuildSlotPath(slotId) + ESGameSaveKeys.TempSuffix;
        }

        private string BuildBackupPath(string slotId)
        {
            return BuildSlotPath(slotId) + ESGameSaveKeys.BackupSuffix;
        }

        private static string NormalizeFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return ESGameSaveKeys.DefaultSaveFolder;

            return folder.Replace('\\', '/').Trim().Trim('/');
        }

        private static string NormalizeSlotId(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
                return ESGameSaveKeys.DefaultSlotId;

            string result = slotId.Trim();
            char[] invalidChars = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidChars.Length; i++)
                result = result.Replace(invalidChars[i], '_');

            return result;
        }

        private static long GetFileSize(ES3Settings settings)
        {
            try
            {
                string fullPath = settings.FullPath;
                return File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
            }
            catch
            {
                return 0;
            }
        }

        private void DeleteFileIfExists(string path, ref bool deleted)
        {
            ES3Settings settings = BuildSettings(path);
            if (!ES3.FileExists(settings))
                return;

            ES3.DeleteFile(settings);
            deleted = true;
        }

        private static ESGameSaveOperationReport BuildReport(bool success, string slotId, string message, double elapsedMs, long fileSizeBytes)
        {
            return new ESGameSaveOperationReport
            {
                success = success,
                slotId = slotId,
                message = message,
                elapsedMs = elapsedMs,
                fileSizeBytes = fileSizeBytes
            };
        }
    }
}
