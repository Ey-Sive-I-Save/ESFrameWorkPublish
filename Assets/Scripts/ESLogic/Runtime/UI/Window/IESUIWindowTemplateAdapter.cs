using System.Threading;
using Cysharp.Threading.Tasks;

namespace ES
{
    /// <summary>
    /// Minimal bridge for existing UI prefabs. A template implements this interface on any
    /// component; ESUIWindowView discovers it automatically during Bind/Unbind.
    /// </summary>
    public interface IESUIWindowTemplateAdapter
    {
        void Bind(ESUIWindowContext context);
        void Unbind(ESUIWindowContext context);
    }

    /// <summary>Optional asynchronous extension for templates that load or prepare data.</summary>
    public interface IESUIWindowAsyncTemplateAdapter : IESUIWindowTemplateAdapter
    {
        UniTask PrepareAsync(ESUIWindowContext context, CancellationToken cancellationToken);
        UniTask CommitAsync(ESUIWindowContext context, CancellationToken cancellationToken);
        UniTask RollbackAsync(ESUIWindowContext context, CancellationToken cancellationToken);
    }
}
