using System;

namespace ES
{
    [ESEnumScript("Assets/Scripts/ESLogic/Data/GameCoreConfigKey/VFX/ESVfxConfigKeyData.cs")]
    public enum ESVfxEnumKey : ushort
    {
        None = 0,
        Custom = 1
    }

    [Serializable]
    public sealed class ESVfxKey : ESGameCoreConfigKey<ESVfxEnumKey>
    {
        public static implicit operator ESVfxKey(string value) => new ESVfxKey { stringKey = value };
        public static implicit operator ESVfxKey(ESVfxEnumKey value) => new ESVfxKey { enumKey = value };
    }

    [Serializable]
    public sealed class ESVfxRuntimeData : ESGameCoreRuntimeData
    {
        public string keyName;
        public string displayName;
        public ESVfxInfo source;

        protected override void ReleaseRuntimePayload()
        {
            keyName = null;
            displayName = null;
            source = null;
        }
    }

    public sealed class ESVfxConfigKeyTable : ESGameCoreConfigKeyTable<ESVfxRuntimeData>
    {
        public ESVfxConfigKeyTable(int capacity = 128) : base(capacity, "GameCore.VFX") { }
    }
}
