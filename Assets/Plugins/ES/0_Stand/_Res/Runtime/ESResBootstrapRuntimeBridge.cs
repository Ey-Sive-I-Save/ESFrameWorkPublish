using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace ES
{
    /// <summary>Stand 启动层与项目运行时数据层的单向桥接；Stand 不引用任何 ESLogic 类型。</summary>
    public static class ESResBootstrapRuntimeBridge
    {
        private static Func<ESGlobalResSetting, ESRuntimeReleaseDownloadResult, CancellationToken, UniTask> initializer;

        public static void Register(Func<ESGlobalResSetting, ESRuntimeReleaseDownloadResult, CancellationToken, UniTask> value)
        {
            initializer = value ?? throw new ArgumentNullException(nameof(value));
        }

        public static UniTask InitializeAsync(ESGlobalResSetting settings, ESRuntimeReleaseDownloadResult result, CancellationToken cancellationToken)
        {
            if (initializer == null)
                return UniTask.FromException(new InvalidOperationException("未注册 ES 运行时资源初始化器。请确认 ESLogic RuntimeData 模块已包含在当前构建中。"));
            return initializer(settings, result, cancellationToken);
        }
    }
}
