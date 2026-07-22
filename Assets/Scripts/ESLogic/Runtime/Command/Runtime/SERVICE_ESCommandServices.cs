namespace ES
{
    public static class ESCommandServices
    {
        public static ESInputModule InputModule { get; private set; }
        public static ESRuntimeModeService RuntimeMode { get; private set; }

        public static void SetInputModule(ESInputModule inputModule)
        {
            InputModule = inputModule;
        }

        public static void SetRuntimeMode(ESRuntimeModeService runtimeMode)
        {
            RuntimeMode = runtimeMode;
        }

        public static void Clear()
        {
            InputModule = null;
            RuntimeMode = null;
        }
    }
}
