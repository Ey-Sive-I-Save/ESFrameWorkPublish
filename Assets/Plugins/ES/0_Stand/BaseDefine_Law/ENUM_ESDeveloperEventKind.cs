namespace ES
{
    public enum ESDeveloperEventKind : byte
    {
        InputSampled = 1,
        LocalControlChanged = 2,
        AIDomainIntent = 3,
        KCCBeforeUpdate = 4,
        AnimatorState = 5,
        CameraArbitration = 6,
        FrameSnapshot = 10,
        ObservationRunStarted = 20,
        ObservationRunStopped = 21,
        ObservationRunInvalid = 22,
    }
}
