using System;

namespace ES
{
    public static class ESGameCoreConfigKeyJsonKinds
    {
        public const string Buff = "Buff";
        public const string Shot = "Shot";
        public const string Monster = "Monster";
        public const string Npc = "Npc";
        public const string Weapon = "Weapon";
        public const string Skill = "Skill";
    }

    [Serializable]
    public class ESGameCoreConfigKeyJsonRecord
    {
        public string kind;
        public int enumKey;
        public string stringKey;
        public string displayName;
        public string prefabAddress;
        public string sourcePackage;
        public string version;
        public string sharedDataJson;
        public string variableDataJson;
        public string sharedDataJson2;
        public string variableDataJson2;
        public string sharedDataJson3;
        public string variableDataJson3;
        public string extraJson;

        public ESGameCoreConfigJsonSource ToSource()
        {
            return new ESGameCoreConfigJsonSource
            {
                displayName = displayName,
                prefabAddress = prefabAddress,
                sourcePackage = sourcePackage,
                version = version,
                sharedDataJson = sharedDataJson,
                variableDataJson = variableDataJson,
                sharedDataJson2 = sharedDataJson2,
                variableDataJson2 = variableDataJson2,
                sharedDataJson3 = sharedDataJson3,
                variableDataJson3 = variableDataJson3,
                extraJson = extraJson
            };
        }
    }

    [Serializable]
    public sealed class ESGameCoreConfigKeyJsonBatch
    {
        public ESGameCoreConfigKeyJsonRecord[] records;
    }
}
