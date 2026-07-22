using System;
using UnityEngine;

namespace ES
{
    public enum ESSkillEnumKey : ushort
    {
        None = 0,
        Custom = 1
    }

    [Serializable]
    public sealed class ESSkillConfigKey : ESGameCoreConfigKey<ESSkillEnumKey> { }

    [Serializable]
    public sealed class ESSkillRuntimeData
    {
        public int runtimeKey;
        public string keyName;
        public string displayName;
        public string sourcePackage;
        public string version;
        public SkillDefinitionDataInfo soSource;
        public SkillTrackProcessInfo trackProcess;
        public StateAniDataInfo baseStateInfo;
        public GameObject prefab;
        public UnityEngine.Object extraAsset;
        public ESGameCoreConfigJsonSource jsonSource;
    }

    [Serializable]
    public sealed class ESSkillConfigKeyJsonRecord : ESGameCoreConfigKeyJsonRecord { }
}
