namespace ES
{
    public sealed partial class ESResManager
    {
        private ESRuntimeReleaseDownloader lastReleaseDownloader;
        private string lastBootstrapError = string.Empty;

        public ESRuntimeReleaseDownloader DiagnosticReleaseDownloader => bootstrapRun?.Downloader ?? lastReleaseDownloader;
        public string LastBootstrapError => lastBootstrapError ?? string.Empty;
    }
}
