using System.Threading;
using System.Threading.Tasks;

namespace ES
{
    public interface IESDialogPresenter
    {
        ESDialogHost Host { get; }
        ESDialogCapabilities Capabilities { get; }
        Task<ESDialogResult> ShowAsync(ESDialogRequest request, CancellationToken cancellationToken);
        void Stop(ESDialogPresenterStopReason reason);
    }

    /// <summary>
    /// 可选的同步模态能力。Presenter 只有在宿主确实提供原生模态消息循环时才实现；
    /// ESDialog 不会通过阻塞 Task 或轮询伪造模态行为。
    /// </summary>
    public interface IESDialogModalPresenter
    {
        ESDialogResult ShowModal(ESDialogRequest request);
    }
}
