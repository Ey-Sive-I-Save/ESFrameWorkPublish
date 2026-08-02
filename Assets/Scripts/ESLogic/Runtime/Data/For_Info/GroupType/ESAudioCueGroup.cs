namespace ES
{
    /// <summary>
    /// Editor-facing audio library. It owns Cue assets for authoring and GameCore collection;
    /// runtime playback continues to resolve individual ESAudioCueKey values.
    /// </summary>
    [ESCreatePath("数据组/GameCore", "音频 Cue 库")]
    public sealed class ESAudioCueGroup : SoDataGroup<ESAudioCueInfo>
    {
    }
}
