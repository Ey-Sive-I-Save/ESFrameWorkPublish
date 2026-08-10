namespace ESFramework.Diagnostics
{
    public interface IESRuntimeDiagnosticsProvider
    {
        string ProviderId { get; }
        int ProviderVersion { get; }
    }

    public interface IESRuntimeObservationSink
    {
        void Record(string source, string name, string value);
    }

    public interface IESRuntimeTestCapability
    {
        string CapabilityId { get; }
        string[] Commands { get; }
    }
}
