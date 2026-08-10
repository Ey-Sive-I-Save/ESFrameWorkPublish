namespace ESFramework.ESAITest
{
    public interface ESAITestCapabilityProvider
    {
        string CapabilityId { get; }
        string ProviderId { get; }
        int ProviderVersion { get; }
        string[] Commands { get; }
        ESAITestCapabilityResponseDto Execute(ESAITestCapabilityRequestDto request);
    }
}
