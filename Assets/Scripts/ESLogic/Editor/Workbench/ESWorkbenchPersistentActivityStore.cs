#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ES
{
    internal enum ESWorkbenchActivityChannel : byte
    {
        History,
        Log,
        Task
    }

    [Serializable]
    internal sealed class ESWorkbenchActivityRecord
    {
        public string recordId = string.Empty;
        public string workbenchId = string.Empty;
        public string channel = string.Empty;
        public string status = string.Empty;
        public string message = string.Empty;
        public string artifactPath = string.Empty;
        public string createdUtc = string.Empty;
        public string updatedUtc = string.Empty;
    }

    [Serializable]
    internal sealed class ESWorkbenchActivityRecordCollection
    {
        public int schemaVersion = 1;
        public List<ESWorkbenchActivityRecord> records = new List<ESWorkbenchActivityRecord>();
    }

    /// <summary>项目级、限量、按需加载的编辑器活动记录。不会保存 Unity 对象、委托或窗口实例。</summary>
    internal static class ESWorkbenchPersistentActivityStore
    {
        private const int MaximumRecords = 500;
        private static readonly object Gate = new object();
        private static string StorePath => Path.Combine(
            Directory.GetCurrentDirectory(), "Library", "ESWorkbench", "activity-v1.json");

        public static IReadOnlyList<ESWorkbenchActivityRecord> Query(
            string workbenchId,
            ESWorkbenchActivityChannel channel,
            int limit = 100)
        {
            lock (Gate)
            {
                ESWorkbenchActivityRecordCollection collection = Read();
                return collection.records
                    .Where(record => record != null
                        && string.Equals(record.workbenchId, workbenchId ?? string.Empty, StringComparison.Ordinal)
                        && string.Equals(record.channel, channel.ToString(), StringComparison.Ordinal))
                    .OrderByDescending(record => record.updatedUtc, StringComparer.Ordinal)
                    .Take(Mathf.Clamp(limit, 1, MaximumRecords))
                    .Select(Clone)
                    .ToArray();
            }
        }

        public static void Append(
            string workbenchId,
            ESWorkbenchActivityChannel channel,
            string status,
            string message,
            string artifactPath = null)
        {
            Upsert(Guid.NewGuid().ToString("N"), workbenchId, channel, status, message, artifactPath, false);
        }

        public static void UpsertTask(
            string taskId,
            string workbenchId,
            string status,
            string message,
            string artifactPath = null)
        {
            Upsert(taskId, workbenchId, ESWorkbenchActivityChannel.Task, status, message, artifactPath, true);
        }

        private static void Upsert(
            string recordId,
            string workbenchId,
            ESWorkbenchActivityChannel channel,
            string status,
            string message,
            string artifactPath,
            bool replace)
        {
            if (string.IsNullOrWhiteSpace(recordId) || string.IsNullOrWhiteSpace(workbenchId)) return;
            lock (Gate)
            {
                ESWorkbenchActivityRecordCollection collection = Read();
                string now = DateTime.UtcNow.ToString("O");
                ESWorkbenchActivityRecord record = replace
                    ? collection.records.FirstOrDefault(value => value != null
                        && value.recordId == recordId
                        && value.workbenchId == workbenchId
                        && value.channel == channel.ToString())
                    : null;
                if (!replace)
                {
                    ESWorkbenchActivityRecord latest = collection.records
                        .Where(value => value != null
                            && value.workbenchId == workbenchId
                            && value.channel == channel.ToString())
                        .OrderByDescending(value => value.updatedUtc, StringComparer.Ordinal)
                        .FirstOrDefault();
                    if (latest != null && latest.status == (status?.Trim() ?? string.Empty)
                        && latest.message == (message?.Trim() ?? string.Empty)
                        && DateTime.TryParse(latest.updatedUtc, null,
                            System.Globalization.DateTimeStyles.RoundtripKind, out DateTime latestUtc)
                        && (DateTime.UtcNow - latestUtc.ToUniversalTime()).TotalMilliseconds < 250d)
                        return;
                }
                if (record == null)
                {
                    record = new ESWorkbenchActivityRecord
                    {
                        recordId = recordId,
                        workbenchId = workbenchId,
                        channel = channel.ToString(),
                        createdUtc = now
                    };
                    collection.records.Add(record);
                }
                record.status = status?.Trim() ?? string.Empty;
                record.message = message?.Trim() ?? string.Empty;
                record.artifactPath = artifactPath?.Trim() ?? string.Empty;
                record.updatedUtc = now;
                collection.records = collection.records
                    .Where(value => value != null)
                    .OrderByDescending(value => value.updatedUtc, StringComparer.Ordinal)
                    .Take(MaximumRecords)
                    .ToList();
                try { Write(collection); }
                catch (Exception exception)
                {
                    Debug.LogWarning("[ESWorkbench] 持久活动记录写入失败：" + exception.Message);
                }
            }
        }

        private static ESWorkbenchActivityRecordCollection Read()
        {
            try
            {
                if (!File.Exists(StorePath)) return new ESWorkbenchActivityRecordCollection();
                string json = File.ReadAllText(StorePath, Encoding.UTF8);
                ESWorkbenchActivityRecordCollection value =
                    JsonUtility.FromJson<ESWorkbenchActivityRecordCollection>(json);
                value ??= new ESWorkbenchActivityRecordCollection();
                value.records ??= new List<ESWorkbenchActivityRecord>();
                return value;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ESWorkbench] 持久活动记录读取失败，已使用空快照：" + exception.Message);
                return new ESWorkbenchActivityRecordCollection();
            }
        }

        private static void Write(ESWorkbenchActivityRecordCollection collection)
        {
            string directory = Path.GetDirectoryName(StorePath);
            if (string.IsNullOrEmpty(directory)) return;
            Directory.CreateDirectory(directory);
            string temporary = StorePath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(collection, true), new UTF8Encoding(false));
            if (File.Exists(StorePath)) File.Replace(temporary, StorePath, null);
            else File.Move(temporary, StorePath);
        }

        private static ESWorkbenchActivityRecord Clone(ESWorkbenchActivityRecord source)
        {
            return new ESWorkbenchActivityRecord
            {
                recordId = source.recordId,
                workbenchId = source.workbenchId,
                channel = source.channel,
                status = source.status,
                message = source.message,
                artifactPath = source.artifactPath,
                createdUtc = source.createdUtc,
                updatedUtc = source.updatedUtc
            };
        }
    }
}
#endif
