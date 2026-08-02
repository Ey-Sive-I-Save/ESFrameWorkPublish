namespace ES
{
    /// <summary>
    /// One Entity stat row for inspector and runtime diagnostic tools. It keeps stable identity for
    /// display and only exposes RuntimeKey as a current-process diagnostic.
    /// </summary>
    public struct ESFloatStatDebugEntry
    {
        public ushort enumKey;
        public string stringKey;
        public string displayName;
        public int runtimeKey;
        public ESKeyStoragePolicy storagePolicy;
        public bool isMaterialized;
        public ESFloatStatSnapshot stat;
    }
}
