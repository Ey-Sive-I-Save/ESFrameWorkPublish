using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace ES
{
    public partial class ESResMaster
    {
        [NonSerialized]
        public ESRuntimeReleaseDownloadResult RuntimeReleaseResult;

        public async UniTask<ESRuntimeReleaseDownloadResult> InitializeReleaseAsync(CancellationToken cancellationToken = default)
        {
            if (Settings == null) throw new InvalidOperationException("ESResMaster 缺少 ESGlobalResSetting。");
            RuntimeReleaseResult = await ESRuntimeReleaseBootstrap.InitializeAsync(Settings, cancellationToken);
            return RuntimeReleaseResult;
        }
    }
}
