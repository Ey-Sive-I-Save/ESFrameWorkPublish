using System;
using System.Collections.Generic;

namespace ES
{
    public enum ESContentRegistrationAction : byte
    {
        Inspect = 0,
        RegisterAsset = 1,
        RegisterGameCore = 2,
        Synchronize = 3,
        Validate = 4,
        Bake = 5,
        Status = 6,
        UpdateAssetKey = 7,
        RegisterGameCoreRoot = 8
    }

    public enum ESContentStableKeyMode : byte
    {
        Auto = 0,
        StringOnly = 1,
        EnumOnly = 2,
        DualAlias = 3
    }

    [Serializable]
    public sealed class ESContentRegistrationRequest
    {
        public ESContentRegistrationAction action;
        public string requestId = string.Empty;
        public bool commit;

        public string assetPath = string.Empty;
        public string libraryPath = string.Empty;
        public string expectedGuid = string.Empty;
        public long expectedLocalFileId;
        public string expectedLibraryRevision = string.Empty;
        public string assetKind = string.Empty;

        public string dataInfoPath = string.Empty;
        public string gameCorePath = string.Empty;
        public string groupPath = string.Empty;
        public string consumerPath = string.Empty;
        public string groupKey = string.Empty;
        public string expectedSourceGuid = string.Empty;
        public string expectedGroupGuid = string.Empty;
        public string expectedConsumerGuid = string.Empty;
        public string expectedSourceRevision = string.Empty;
        public string expectedGroupRevision = string.Empty;
        public string expectedConsumerRevision = string.Empty;
        public int expectedCurrentEnumKey;
        public string expectedCurrentStringKey = string.Empty;
        public bool hasExpectedCurrentKey;

        public ESContentStableKeyMode keyMode;
        public int enumKey;
        public string stringKey = string.Empty;
        public int itemEnumKey;
        public string itemStringKey = string.Empty;
        public int expectedCurrentItemEnumKey;
        public string expectedCurrentItemStringKey = string.Empty;
        public bool hasExpectedCurrentItemKey;
        public string gameCoreRoute = string.Empty;

        public string runId = string.Empty;
    }

    [Serializable]
    public sealed class ESContentRegistrationResult
    {
        public bool success;
        public bool changed;
        public bool dryRun;
        public bool idempotent;
        public string status = string.Empty;
        public string action = string.Empty;
        public string requestId = string.Empty;
        public string runId = string.Empty;
        public string message = string.Empty;

        public string assetPath = string.Empty;
        public string guid = string.Empty;
        public string libraryGuid = string.Empty;
        public string sourceGuid = string.Empty;
        public string groupGuid = string.Empty;
        public string consumerGuid = string.Empty;
        public long localFileId;
        public string assetKind = string.Empty;
        public int enumKey;
        public string stringKey = string.Empty;
        public int currentEnumKey;
        public string currentStringKey = string.Empty;
        public int itemEnumKey;
        public string itemStringKey = string.Empty;
        public int currentItemEnumKey;
        public string currentItemStringKey = string.Empty;
        public string groupKey = string.Empty;

        public string targetRevision = string.Empty;
        public string sourceRevision = string.Empty;
        public string groupRevision = string.Empty;
        public string consumerRevision = string.Empty;

        public List<string> changedPaths = new List<string>();
        public List<string> warnings = new List<string>();
        public List<string> errors = new List<string>();

        public static ESContentRegistrationResult Failure(
            ESContentRegistrationRequest request,
            string status,
            string message)
        {
            var result = Create(request);
            result.status = status ?? "failed";
            result.message = message ?? string.Empty;
            if (!string.IsNullOrEmpty(message))
                result.errors.Add(message);
            return result;
        }

        public static ESContentRegistrationResult Create(ESContentRegistrationRequest request)
        {
            return new ESContentRegistrationResult
            {
                action = request != null ? request.action.ToString() : string.Empty,
                requestId = request?.requestId ?? string.Empty,
                dryRun = request == null || !request.commit
            };
        }
    }
}
