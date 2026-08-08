using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class ESProfileHeader
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField, ReadOnly, LabelText("Profile Key")]
        [Tooltip("供其他配置稳定引用的 Profile Key。由 Profile 在 OnValidate/Awake 自动补齐。")]
        private string definitionKey;

        [SerializeField, ReadOnly, LabelText("Schema 版本")]
        [Tooltip("只允许由显式 Profile 迁移事务更新；OnValidate/Awake 不会静默升级。")]
        private int schemaVersion = CurrentSchemaVersion;

        [SerializeField, LabelText("启用 Profile")]
        private bool profileEnabled = true;

        [SerializeField, LabelText("显示名称")]
        private string displayName = "Generic Profile";

        [SerializeField, TextArea, LabelText("摘要")]
        private string summary;

        public string DefinitionKey => definitionKey;
        public int SchemaVersion => schemaVersion;
        public bool IsSchemaCurrent => schemaVersion == CurrentSchemaVersion;
        public bool RequiresMigration => schemaVersion >= 0 && schemaVersion < CurrentSchemaVersion;
        public bool HasUnsupportedFutureSchema => schemaVersion > CurrentSchemaVersion;
        public bool ProfileEnabled => profileEnabled;
        public string DisplayName => displayName;
        public string Summary => summary;

        internal void EnsureDefinitionKey()
        {
            if (string.IsNullOrWhiteSpace(definitionKey))
                definitionKey = Guid.NewGuid().ToString("N");
        }
    }
}
