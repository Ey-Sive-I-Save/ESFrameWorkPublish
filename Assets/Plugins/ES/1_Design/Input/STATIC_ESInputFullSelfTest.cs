namespace ES
{
    public static class ESInputFullSelfTest
    {
        public static string RunAll()
        {
            string runtimeModeResult = ESRuntimeModeSelfTest.RunAll();
            string inputResult = ESInputRuntimeModeSelfTest.RunAll();
            string bindingResult = ESInputActionBindingSelfTest.RunAll();
            return runtimeModeResult + "\n" + inputResult + "\n" + bindingResult;
        }
    }
}
