using System;

namespace ES
{
    public sealed class ESInputRuntime : IDisposable
    {
        private readonly ESInputService service = new ESInputService();
        private readonly ESInputSystemSource inputSystemSource = new ESInputSystemSource();
        private readonly ESInputVirtualSource virtualSource = new ESInputVirtualSource();
        private readonly ESInputSchemeResolver schemeResolver = new ESInputSchemeResolver();

        public ESInputService Service
        {
            get { return service; }
        }

        public ESInputSystemSource InputSystemSource
        {
            get { return inputSystemSource; }
        }

        public ESInputVirtualSource VirtualSource
        {
            get { return virtualSource; }
        }

        public ESInputSchemeResolver SchemeResolver
        {
            get { return schemeResolver; }
        }

        public void Initialize(ESInputRuntimeBuildResult build, ESRuntimeModeService modeService)
        {
            service.SetModeService(modeService);
            service.SetCache(build != null ? build.cache : null);
            inputSystemSource.Initialize(build, service);
            virtualSource.Initialize(build, service);
            schemeResolver.Initialize(build != null ? build.activeSchemeId : ESInputSchemeIds.KeyboardMouse);
        }

        public void Enable()
        {
            inputSystemSource.Enable();
            schemeResolver.Enable();
        }

        public void Disable()
        {
            schemeResolver.Disable();
            inputSystemSource.Disable();
            virtualSource.ClearAll();
            service.ResetAll();
        }

        public void Update(float time)
        {
            service.BeginFrame();
            inputSystemSource.Update(time, false);
            virtualSource.Update(time);
            service.EndFrame(time);
        }

        public void Dispose()
        {
            schemeResolver.Dispose();
            inputSystemSource.Dispose();
            service.SetCache(null);
            service.SetModeService(null);
        }
    }
}
