namespace ES.EditorInternal
{
    public sealed class ESCodexSessionLaunchResult
    {
        public string terminalMode;
        public string terminalWindowName;
        public string tabTitle;
        public string responsibilityKey;
        public string envelopePath;
        public string handoffSnapshotDirectory;
        public string sessionId;
        public int processId;
        public bool alreadyRunning;
        public bool launched;
        public bool terminalStarted;
        public bool promptObserved;
        public bool contextAccepted;
        public bool startupFailed;
        public bool startupTimedOut;
        public string launchPhase;
        public string acceptanceReceiptPath;
        public string startupDiagnosticPath;
        public string startupFailureReason;
    }
}
