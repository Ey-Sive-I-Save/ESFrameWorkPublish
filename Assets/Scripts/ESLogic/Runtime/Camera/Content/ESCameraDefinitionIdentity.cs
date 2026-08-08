using System;

namespace ES
{
    /// <summary>
    /// Camera.ViewDefinition 的代码别名。数值一旦发布不得重排；自定义内容可只使用 StringKey。
    /// </summary>
    public enum ESCameraDefinitionEnumKey : ushort
    {
        None = 0,
        PlayerThirdPerson = 1,
        VehicleChase = 2,
    }

    /// <summary>
    /// 可序列化的相机定义稳定引用。它只描述内容身份，不能引用 Rig、VCam、场景 View 或
    /// RuntimeKey。Camera Definition Catalog 是唯一的解析权威。
    /// </summary>
    [Serializable]
    public struct ESCameraDefinitionReference : IEquatable<ESCameraDefinitionReference>
    {
        public const string Scope = "Camera.ViewDefinition";

        public ESCameraDefinitionEnumKey enumKey;
        public string stringKey;

        public ESCameraDefinitionReference(ESCameraDefinitionEnumKey enumKey, string stringKey)
        {
            this.enumKey = enumKey;
            this.stringKey = stringKey;
        }

        public bool IsConfigured => ESConfigKeyMatch.IsConfigured((int)enumKey, stringKey);
        public ESStableKey ToStableKey() => new ESStableKey(Scope, (ushort)enumKey, stringKey);

        public bool Equals(ESCameraDefinitionReference other)
        {
            return enumKey == other.enumKey
                   && string.Equals(stringKey, other.stringKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is ESCameraDefinitionReference other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)enumKey * 397) ^ (stringKey != null ? StringComparer.Ordinal.GetHashCode(stringKey) : 0);
            }
        }

        public override string ToString() => ESStableKey.Describe(Scope, (ushort)enumKey, stringKey);
        public static bool operator ==(ESCameraDefinitionReference left, ESCameraDefinitionReference right) => left.Equals(right);
        public static bool operator !=(ESCameraDefinitionReference left, ESCameraDefinitionReference right) => !left.Equals(right);
    }

    /// <summary>
    /// 只在当前 Catalog 生命周期内有效的运行时句柄。禁止序列化到 Prefab、SO、存档、网络
    /// 或回放数据；Catalog 重建后旧句柄必定被拒绝。
    /// </summary>
    public readonly struct ESCameraDefinitionRuntimeHandle : IEquatable<ESCameraDefinitionRuntimeHandle>
    {
        internal ESCameraDefinitionRuntimeHandle(int catalogIdentity, int catalogGeneration, int runtimeKey, string keySchemaHash)
        {
            this.catalogIdentity = catalogIdentity;
            this.catalogGeneration = catalogGeneration;
            this.runtimeKey = runtimeKey;
            this.keySchemaHash = keySchemaHash;
        }

        internal readonly int catalogIdentity;
        internal readonly int catalogGeneration;
        internal readonly int runtimeKey;
        internal readonly string keySchemaHash;

        public bool IsValid => catalogIdentity > 0 && catalogGeneration > 0 && runtimeKey > 0 && !string.IsNullOrEmpty(keySchemaHash);

        public bool Equals(ESCameraDefinitionRuntimeHandle other)
        {
            return catalogIdentity == other.catalogIdentity
                   && catalogGeneration == other.catalogGeneration
                   && runtimeKey == other.runtimeKey
                   && string.Equals(keySchemaHash, other.keySchemaHash, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is ESCameraDefinitionRuntimeHandle other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = catalogIdentity;
                hash = (hash * 397) ^ catalogGeneration;
                hash = (hash * 397) ^ runtimeKey;
                return (hash * 397) ^ (keySchemaHash != null ? StringComparer.Ordinal.GetHashCode(keySchemaHash) : 0);
            }
        }

        public static bool operator ==(ESCameraDefinitionRuntimeHandle left, ESCameraDefinitionRuntimeHandle right) => left.Equals(right);
        public static bool operator !=(ESCameraDefinitionRuntimeHandle left, ESCameraDefinitionRuntimeHandle right) => !left.Equals(right);
    }
}
