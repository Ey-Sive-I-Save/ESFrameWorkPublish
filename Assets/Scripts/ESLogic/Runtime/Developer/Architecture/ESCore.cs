namespace ES
{
    /// <summary>
    /// Daily-use facade for the ES runtime core.
    /// Keep this class thin: it only forwards to the real GameManager static cache.
    /// </summary>
    public static class ESCore
    {
        public static ESGameManager Manager
        {
            get { return ESGameManager.Instance; }
        }

        public static bool IsReady
        {
            get { return ESGameManager.IsReady; }
        }

        public static ESRuntimeModeService RuntimeMode
        {
            get { return ESGameManager.RuntimeMode; }
        }

        public static ESInputModule Input
        {
            get { return ESGameManager.InputModule; }
        }

        public static ESCommandModule Command
        {
            get { return ESGameManager.CommandModule; }
        }

        public static ESSystemDomain System
        {
            get { return ESGameManager.SystemDomain; }
        }

        public static ESFlowDomain Flow
        {
            get { return ESGameManager.FlowDomain; }
        }

        public static ESWorldDomain World
        {
            get { return ESGameManager.WorldDomain; }
        }

        public static LinkReceivePool Link
        {
            get { return ESGameManager.GlobalLinkPool; }
        }

        public static T Module<T>() where T : class, IModule, new()
        {
            return ESGameManager.GetModuleFast<T>();
        }

        public static void Refresh()
        {
            ESGameManager.RefreshStaticCache();
        }
    }
}
