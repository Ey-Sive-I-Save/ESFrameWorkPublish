namespace ES
{
    public interface IESDeveloperTraceProvider
    {
        bool IsEnabled { get; }

        void Emit(in ESDeveloperEventEnvelope envelope);
    }
}
