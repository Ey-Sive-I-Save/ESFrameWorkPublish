using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ES
{
    public enum ESUIContentState : byte { Idle, Loading, Ready, Empty, Failed, Released }

    public enum ESUIOverlayKind : byte { Modal, Popup, Toast, System }

    public sealed class ESUIWindowLifecycleEvents
    {
        public event Action<ESUIWindowContext> Opened, Shown, Focused, Rebound;
        public event Action<ESUIWindowContext> Blurred, Paused, Resumed;
        public event Action<ESUIWindowContext, ESUIWindowCloseEffect> Closed;
        internal void RaiseOpened(ESUIWindowContext c) => Opened?.Invoke(c);
        internal void RaiseShown(ESUIWindowContext c) => Shown?.Invoke(c);
        internal void RaiseFocused(ESUIWindowContext c) => Focused?.Invoke(c);
        internal void RaiseBlurred(ESUIWindowContext c) => Blurred?.Invoke(c);
        internal void RaisePaused(ESUIWindowContext c) => Paused?.Invoke(c);
        internal void RaiseResumed(ESUIWindowContext c) => Resumed?.Invoke(c);
        internal void RaiseRebound(ESUIWindowContext c) => Rebound?.Invoke(c);
        internal void RaiseClosed(ESUIWindowContext c, ESUIWindowCloseEffect e) => Closed?.Invoke(c, e);
    }

    /// <summary>Deterministic page navigation independent from window ownership.</summary>
    public readonly struct ESUIPageNavigationEntry
    {
        public ESUIPageNavigationEntry(ESUIWindowIdentity identity, ESUICanonicalId canonicalId, object data)
        {
            Identity = identity;
            CanonicalId = canonicalId;
            Data = data;
        }

        public ESUIWindowIdentity Identity { get; }
        public ESUICanonicalId CanonicalId { get; }
        public object Data { get; }
    }

    public sealed class ESUIPageNavigator
    {
        private sealed class Entry { internal ESUIWindowIdentity identity; internal ESUICanonicalId canonicalId; internal ESUIWindowLease lease; internal object data; }
        private readonly List<Entry> stack = new List<Entry>();
        private readonly Func<ESUIWindowIdentity, object, CancellationToken, UniTask<ESUIWindowLease>> opener;
        private readonly Func<ESUIWindowIdentity, ESUICanonicalId> canonicalizer;
        public int Count => stack.Count;
        public ESUIWindowIdentity? Current => stack.Count == 0 ? (ESUIWindowIdentity?)null : stack[stack.Count - 1].identity;
        public ESUICanonicalId? CurrentCanonicalId => stack.Count == 0 ? (ESUICanonicalId?)null : stack[stack.Count - 1].canonicalId;
        public IReadOnlyList<ESUIWindowIdentity> History => stack.Select(x => x.identity).ToArray();
        public IReadOnlyList<ESUIPageNavigationEntry> Entries =>
            stack.Select(x => new ESUIPageNavigationEntry(x.identity, x.canonicalId, x.data)).ToArray();
        public ESUIPageNavigator(Func<ESUIWindowIdentity, object, CancellationToken, UniTask<ESUIWindowLease>> opener)
            : this(opener, null) { }
        public ESUIPageNavigator(Func<ESUIWindowIdentity, object, CancellationToken, UniTask<ESUIWindowLease>> opener, Func<ESUIWindowIdentity, ESUICanonicalId> canonicalizer)
        { this.opener = opener ?? throw new ArgumentNullException(nameof(opener)); this.canonicalizer = canonicalizer; }
        private ESUICanonicalId Canonicalize(ESUIWindowIdentity identity)
        {
            if (canonicalizer != null) return canonicalizer(identity);
            if (identity.HasStringKey) return new ESUICanonicalId(identity.StringKey);
            return new ESUICanonicalId("builtin:" + identity.BuiltInId);
        }
        public async UniTask<ESUIWindowLease> PushAsync(ESUIWindowIdentity identity, object data = null, CancellationToken token = default)
        { var lease = await opener(identity, data, token); stack.Add(new Entry { identity = identity, canonicalId = Canonicalize(identity), lease = lease, data = data }); return lease; }
        public async UniTask<bool> PopAsync(CancellationToken token = default)
        { if (stack.Count == 0) return false; Entry e = stack[stack.Count - 1]; stack.RemoveAt(stack.Count - 1); if (e.lease != null) await e.lease.CloseAsync(); if (stack.Count > 0) { Entry previous = stack[stack.Count - 1]; previous.lease = await opener(previous.identity, previous.data, token); } return true; }
        public async UniTask<ESUIWindowLease> ReplaceAsync(ESUIWindowIdentity identity, object data = null, CancellationToken token = default)
        { await PopAsync(token); return await PushAsync(identity, data, token); }
        public UniTask<bool> BackAsync(CancellationToken token = default) => PopAsync(token);
        public async UniTask ClearAsync(CancellationToken token = default) { while (await PopAsync(token)) { } }

        /// <summary>Stages a caller-serialized navigation snapshot without imposing a data format.</summary>
        public bool StageContext(
            ESUIContextStore contextStore,
            string scopeKey,
            int schemaVersion,
            Func<IReadOnlyList<ESUIPageNavigationEntry>, string> serializer)
        {
            if (contextStore == null) throw new ArgumentNullException(nameof(contextStore));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            if (stack.Count == 0) return false;
            ESUIPageNavigationEntry current = new ESUIPageNavigationEntry(stack[stack.Count - 1].identity, stack[stack.Count - 1].canonicalId, stack[stack.Count - 1].data);
            string payload = serializer(Entries);
            if (payload == null) return false;
            return contextStore.Stage(new ESUIContextSnapshot(current.CanonicalId, schemaVersion, scopeKey, payload, DateTimeOffset.UtcNow));
        }

        /// <summary>Rebuilds the page stack from caller-validated entries in deterministic order.</summary>
        public async UniTask<bool> RestoreAsync(
            IReadOnlyList<ESUIPageNavigationEntry> entries,
            CancellationToken token = default)
        {
            if (entries == null || entries.Count == 0) return false;
            await ClearAsync(token);
            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    ESUIPageNavigationEntry entry = entries[i];
                    if (entry.CanonicalId != Canonicalize(entry.Identity))
                        throw new InvalidOperationException("导航条目 CanonicalId 与 Identity 不一致：" + entry.Identity);
                    await PushAsync(entry.Identity, entry.Data, token);
                }
                return true;
            }
            catch
            {
                await ClearAsync(CancellationToken.None);
                throw;
            }
        }
    }

    /// <summary>Focus owner and keyboard/gamepad navigation policy for one UI root.</summary>
    public sealed class ESUIFocusCoordinator
    {
        private EventSystem eventSystem;
        private Selectable previous;
        private ESUICanonicalId? previousOwner;
        private ESUICanonicalId? currentOwner;
        public Selectable Current => eventSystem == null ? null : eventSystem.currentSelectedGameObject?.GetComponent<Selectable>();
        public ESUICanonicalId? CurrentOwnerId => currentOwner;
        public ESUIFocusCoordinator(EventSystem eventSystem) => this.eventSystem = eventSystem;
        public void Attach(EventSystem value) => eventSystem = value;
        public bool Claim(Selectable target) => Claim(target, null);
        public bool Claim(Selectable target, ESUICanonicalId? ownerId)
        {
            if (target == null || !target.IsInteractable()) return false;
            previous = Current;
            previousOwner = currentOwner;
            currentOwner = ownerId;
            eventSystem?.SetSelectedGameObject(target.gameObject);
            return true;
        }
        public bool RestorePrevious()
        {
            if (!IsReachable(previous)) return false;
            Selectable target = previous;
            ESUICanonicalId? ownerId = previousOwner;
            previous = null;
            previousOwner = null;
            return Claim(target, ownerId);
        }
        public void Clear() { previous = Current; previousOwner = currentOwner; currentOwner = null; eventSystem?.SetSelectedGameObject(null); }
        public bool Move(MoveDirection direction)
        {
            Selectable current = Current;
            if (current == null) return false;
            Selectable next = direction == MoveDirection.Left ? current.FindSelectableOnLeft()
                : direction == MoveDirection.Right ? current.FindSelectableOnRight()
                : direction == MoveDirection.Up ? current.FindSelectableOnUp()
                : current.FindSelectableOnDown();
            return Claim(next, currentOwner);
        }
        public static bool IsReachable(Selectable target) => target != null && target.IsInteractable() && target.gameObject.activeInHierarchy && target.GetComponentInParent<CanvasGroup>()?.blocksRaycasts != false;
    }

    /// <summary>Observable read-only state bridge. Producers own state; views only subscribe.</summary>
    public sealed class ESUIStateBinding<T> : IDisposable
    {
        private readonly Func<T> getter;
        private readonly List<Action<T>> listeners = new List<Action<T>>();
        private bool disposed;
        private long revision;
        public T Value => getter();
        public ESUICanonicalId? OwnerId { get; }
        public long Revision => revision;
        public ESUIStateBinding(Func<T> getter, ESUICanonicalId? ownerId = null) { this.getter = getter ?? throw new ArgumentNullException(nameof(getter)); OwnerId = ownerId; }
        public IDisposable Subscribe(Action<T> listener, bool emitCurrent = true) { if (disposed) throw new ObjectDisposedException(nameof(ESUIStateBinding<T>)); if (listener == null) throw new ArgumentNullException(nameof(listener)); listeners.Add(listener); if (emitCurrent) listener(getter()); return new Subscription(this, listener); }
        public void Publish() { if (disposed) return; T value = getter(); revision++; for (int i = listeners.Count - 1; i >= 0; i--) listeners[i]?.Invoke(value); }
        public void Dispose() { disposed = true; listeners.Clear(); }
        private sealed class Subscription : IDisposable { private ESUIStateBinding<T> owner; private readonly Action<T> listener; internal Subscription(ESUIStateBinding<T> o, Action<T> l) { owner = o; listener = l; } public void Dispose() { if (owner == null) return; owner.listeners.Remove(listener); owner = null; } }
    }

    public sealed class ESUIOverlayArbiter
    {
        private sealed class Request { internal string key; internal int priority; internal ESUIOverlayKind kind; internal Func<CancellationToken, UniTask> show; internal CancellationTokenSource cts; internal long order; }
        private readonly List<Request> pending = new List<Request>(); private readonly HashSet<string> active = new HashSet<string>(); private long order;
        public int PendingCount => pending.Count;
        public UniTask<bool> EnqueueAsync(ESUICanonicalId canonicalId, ESUIOverlayKind kind, int priority, Func<CancellationToken, UniTask> show, TimeSpan timeout, CancellationToken token = default)
            => EnqueueAsync(canonicalId.Value, kind, priority, show, timeout, token);
        public async UniTask<bool> EnqueueAsync(string key, ESUIOverlayKind kind, int priority, Func<CancellationToken, UniTask> show, TimeSpan timeout, CancellationToken token = default)
        { if (string.IsNullOrWhiteSpace(key) || show == null) throw new ArgumentException("Overlay key/show 不能为空。"); if (!active.Add(key)) return false; var r = new Request { key = key, kind = kind, priority = priority, show = show, order = ++order, cts = CancellationTokenSource.CreateLinkedTokenSource(token) }; pending.Add(r); pending.Sort((a,b) => b.priority != a.priority ? b.priority.CompareTo(a.priority) : a.order.CompareTo(b.order)); try { int winner = await UniTask.WhenAny(DrainAsync(r), UniTask.Delay(timeout, cancellationToken: r.cts.Token)); if (winner != 0) throw new TimeoutException("UI Overlay 显示超时：" + key); return true; } finally { r.cts.Cancel(); r.cts.Dispose(); pending.Remove(r); active.Remove(key); } }
        private async UniTask DrainAsync(Request r) { while (pending.Count > 0 && !ReferenceEquals(pending[0], r)) await UniTask.Yield(); if (pending.Count > 0) await r.show(r.cts.Token); }
    }

    public sealed class ESUITransitionCoordinator
    {
        public async UniTask EnterAsync(ESUIWindowView view, ESUIWindowContext context, TimeSpan timeout, CancellationToken token) { if (view == null) throw new ArgumentNullException(nameof(view)); using (CancellationTokenSource c = CancellationTokenSource.CreateLinkedTokenSource(token)) { UniTask transition = view.EnterAsync(context, c.Token); int winner = await UniTask.WhenAny(transition, UniTask.Delay(timeout, cancellationToken: c.Token)); if (winner != 0) throw new TimeoutException("UI Window Enter 转场超时。"); await transition; c.Cancel(); } }
        public async UniTask ExitAsync(ESUIWindowView view, ESUIWindowCloseEffect effect, TimeSpan timeout, CancellationToken token) { if (view == null) return; using (CancellationTokenSource c = CancellationTokenSource.CreateLinkedTokenSource(token)) { UniTask transition = view.ExitAsync(effect, c.Token); int winner = await UniTask.WhenAny(transition, UniTask.Delay(timeout, cancellationToken: c.Token)); if (winner != 0) throw new TimeoutException("UI Window Exit 转场超时。"); await transition; c.Cancel(); } }
    }

    public sealed class ESUIContentPresenter<T> : IDisposable
    {
        private readonly Func<CancellationToken, UniTask<T>> loader; private readonly Action<T> ready; private readonly Action<Exception> failed; private CancellationTokenSource cts; private bool disposed; private long generation;
        public ESUIContentState State { get; private set; } = ESUIContentState.Idle;
        public long Generation => generation;
        public ESUIContentPresenter(Func<CancellationToken, UniTask<T>> loader, Action<T> ready, Action<Exception> failed = null) { this.loader = loader ?? throw new ArgumentNullException(nameof(loader)); this.ready = ready ?? throw new ArgumentNullException(nameof(ready)); this.failed = failed; }
        public async UniTask<bool> LoadAsync(CancellationToken token = default) { if (disposed) return false; cts?.Cancel(); cts?.Dispose(); cts = CancellationTokenSource.CreateLinkedTokenSource(token); long requestGeneration = ++generation; State = ESUIContentState.Loading; try { T value = await loader(cts.Token); if (disposed || requestGeneration != generation) return false; if (ReferenceEquals(value, null)) { State = ESUIContentState.Empty; return false; } ready(value); State = ESUIContentState.Ready; return true; } catch (OperationCanceledException) { if (requestGeneration == generation) State = ESUIContentState.Released; return false; } catch (Exception e) { if (requestGeneration == generation) { State = ESUIContentState.Failed; failed?.Invoke(e); } return false; } }
        public UniTask<bool> RetryAsync(CancellationToken token = default) => LoadAsync(token);
        public void Dispose() { disposed = true; ++generation; cts?.Cancel(); cts?.Dispose(); cts = null; State = ESUIContentState.Released; }
    }
}
