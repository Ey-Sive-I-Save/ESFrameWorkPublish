using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Base component for a window prefab. Override EnterAsync/ExitAsync for presentation only;
    /// authoritative open/close state remains in ESUIRootCoordinator.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("【ES】/UI/UI Window View")]
    public class ESUIWindowView : MonoBehaviour, IESGameObjectPoolLifecycle
    {
        [SerializeField] private CanvasGroup presentationCanvasGroup;

        private ESUIWindowContext context;
        private IESUIWindowTemplateAdapter templateAdapter;
        private bool baselineCaptured;
        private float baselineAlpha;
        private bool baselineInteractable;
        private bool baselineBlocksRaycasts;

        public ESUIWindowContext Context => context;
        public bool IsBound => context != null;
        public CanvasGroup PresentationCanvasGroup => presentationCanvasGroup;

        internal UniTask PrepareTemplateAsync(CancellationToken cancellationToken)
        {
            if (templateAdapter == null)
                templateAdapter = GetComponent<IESUIWindowTemplateAdapter>();
            if (templateAdapter is IESUIWindowAsyncTemplateAdapter asyncAdapter && context != null)
                return asyncAdapter.PrepareAsync(context, cancellationToken);
            return UniTask.CompletedTask;
        }

        internal UniTask CommitTemplateAsync(CancellationToken cancellationToken)
        {
            if (templateAdapter is IESUIWindowAsyncTemplateAdapter asyncAdapter && context != null)
                return asyncAdapter.CommitAsync(context, cancellationToken);
            return UniTask.CompletedTask;
        }

        internal UniTask RollbackTemplateAsync(CancellationToken cancellationToken)
        {
            if (templateAdapter is IESUIWindowAsyncTemplateAdapter asyncAdapter && context != null)
                return asyncAdapter.RollbackAsync(context, cancellationToken);
            return UniTask.CompletedTask;
        }

        internal void Bind(ESUIWindowContext value)
        {
            if (value == null)
                throw new System.ArgumentNullException(nameof(value));
            if (context != null)
                throw new System.InvalidOperationException("UI Window View 已绑定到其他窗口实例。");

            CapturePresentationBaseline();
            RestorePresentationBaseline();
            context = value;
            templateAdapter = GetComponent<IESUIWindowTemplateAdapter>();
            templateAdapter?.Bind(value);
            OnBound(value);
        }

        internal void Unbind()
        {
            ESUIWindowContext previous = context;
            if (previous == null)
                return;

            context = null;
            try
            {
                OnUnbound(previous);
            }
            finally
            {
                try
                {
                    templateAdapter?.Unbind(previous);
                }
                finally
                {
                    templateAdapter = null;
                    previous.Clear();
                    RestorePresentationBaseline();
                }
            }
        }

        public virtual UniTask EnterAsync(ESUIWindowContext value, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask ExitAsync(ESUIWindowCloseEffect effect, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        public void OnPoolSpawned()
        {
            CapturePresentationBaseline();
            RestorePresentationBaseline();
            OnSpawnedFromPool();
        }

        public void OnPoolDespawned()
        {
            RestorePresentationBaseline();
            OnReturnedToPool();
        }

        protected virtual void OnBound(ESUIWindowContext value)
        {
        }

        protected virtual void OnUnbound(ESUIWindowContext previous)
        {
        }

        protected virtual void OnSpawnedFromPool()
        {
        }

        protected virtual void OnReturnedToPool()
        {
        }

        private void Awake()
        {
            CapturePresentationBaseline();
        }

        private void OnDestroy()
        {
            ESUIWindowContext previous = context;
            context = null;
            if (previous == null)
                return;

            previous.Root?.NotifyViewDestroyed(this, previous);
            previous.Clear();
        }

        private void CapturePresentationBaseline()
        {
            if (baselineCaptured || presentationCanvasGroup == null)
                return;

            baselineCaptured = true;
            baselineAlpha = presentationCanvasGroup.alpha;
            baselineInteractable = presentationCanvasGroup.interactable;
            baselineBlocksRaycasts = presentationCanvasGroup.blocksRaycasts;
        }

        private void RestorePresentationBaseline()
        {
            if (!baselineCaptured || presentationCanvasGroup == null)
                return;

            presentationCanvasGroup.alpha = baselineAlpha;
            presentationCanvasGroup.interactable = baselineInteractable;
            presentationCanvasGroup.blocksRaycasts = baselineBlocksRaycasts;
        }
    }
}
