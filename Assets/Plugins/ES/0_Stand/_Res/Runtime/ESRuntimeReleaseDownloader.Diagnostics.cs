namespace ES
{
    public sealed partial class ESRuntimeReleaseDownloader
    {
        private bool hasDiagnosticProgress;
        private ESRuntimeReleaseDownloadProgress lastDiagnosticProgress;
        private bool hasDiagnosticSnapshot;
        private ESRuntimeReleaseDownloadSnapshot lastDiagnosticSnapshot;

        public string DiagnosticCacheRoot => cacheRoot;
        public string DiagnosticPlatform => platform;
        public ESAssetRunMode DiagnosticRunMode => runMode;
        public bool DiagnosticUsesLocalReleaseSource => useLocalReleaseSource;
        public string DiagnosticVerifiedReleaseVersion => verifiedReleaseVersion ?? string.Empty;
        public int DiagnosticVerifiedFileCount => verified.Count;
        public bool HasDiagnosticProgress => hasDiagnosticProgress;
        public ESRuntimeReleaseDownloadProgress LastDiagnosticProgress => lastDiagnosticProgress;
        public bool HasDiagnosticSnapshot => hasDiagnosticSnapshot;
        public ESRuntimeReleaseDownloadSnapshot LastDiagnosticSnapshot => lastDiagnosticSnapshot;

        private void RecordDiagnosticProgress(ESRuntimeReleaseDownloadProgress progress)
        {
            lastDiagnosticProgress = progress;
            hasDiagnosticProgress = true;
        }

        private void RecordDiagnosticSnapshot(ESRuntimeReleaseDownloadSnapshot snapshot)
        {
            lastDiagnosticSnapshot = snapshot;
            hasDiagnosticSnapshot = true;
        }
    }
}
