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
}
