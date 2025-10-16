namespace FlyRabbit.EventCenter
{
    public class EventUsageInfo
    {
        public string Signature;
        public string AssetPath;
        public int Line;
        public bool IsError;
        public EventUsageInfo(string signature, string assetPath, int line)
        {
            Signature = signature;
            AssetPath = assetPath;
            Line = line;
        }
    }
}
