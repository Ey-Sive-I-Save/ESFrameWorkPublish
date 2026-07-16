namespace ES
{
    public static class ESCommandServices
    {
        public static ESInputRuntime InputRuntime { get; private set; }
        public static ESRuntimeModeService RuntimeMode { get; private set; }

        public static void SetInputRuntime(ESInputRuntime inputRuntime)
        {
            InputRuntime = inputRuntime;
        }

        public static void SetRuntimeMode(ESRuntimeModeService runtimeMode)
        {
            RuntimeMode = runtimeMode;
        }

        public static void Clear()
        {
            InputRuntime = null;
            RuntimeMode = null;
        }
    }
}
