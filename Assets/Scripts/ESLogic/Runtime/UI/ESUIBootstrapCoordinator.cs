using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace ES
{
    public enum ESUIBootstrapState : byte
    {
        Idle,
        Starting,
        Ready,
        Failed,
        Cancelled,
        Stopped
    }

    public readonly struct ESUIBootstrapResult
    {
        private ESUIBootstrapResult(string key, ESUIBootstrapState state, Exception error, long attempt)
        {
            BootstrapKey = key;
            State = state;
            Error = error;
            Attempt = attempt;
        }

        public string BootstrapKey { get; }
        public ESUIBootstrapState State { get; }
        public Exception Error { get; }
        public long Attempt { get; }
        public bool IsSuccess => State == ESUIBootstrapState.Ready;

        internal static ESUIBootstrapResult Create(string key, ESUIBootstrapState state, Exception error, long attempt) =>
            new ESUIBootstrapResult(key, state, error, attempt);
    }

    /// <summary>
    /// Deduplicates asynchronous UI bootstrap attempts by a stable key. Caller cancellation
    /// cancels only that attempt when it owns the attempt; repeated callers observe the same
    /// completion and never start a second pipeline.
    /// </summary>
    public sealed class ESUIBootstrapCoordinator : IDisposable
    {
        private sealed class Attempt
        {
            internal readonly CancellationTokenSource Cancellation;
            internal readonly UniTaskCompletionSource<ESUIBootstrapResult> Completion =
                new UniTaskCompletionSource<ESUIBootstrapResult>();
            internal readonly long Number;

            internal Attempt(long number, CancellationToken token)
            {
                Number = number;
                Cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            }
        }

        private readonly Dictionary<string, Attempt> attempts = new Dictionary<string, Attempt>(StringComparer.Ordinal);
        private long nextAttempt;
        private bool disposed;

        public int InFlightCount => attempts.Count;

        public bool IsInFlight(string bootstrapKey) =>
            !string.IsNullOrWhiteSpace(bootstrapKey) && attempts.ContainsKey(bootstrapKey);

        public bool TryGetAttempt(string bootstrapKey, out long attempt)
        {
            if (!string.IsNullOrWhiteSpace(bootstrapKey) && attempts.TryGetValue(bootstrapKey, out Attempt current))
            {
                attempt = current.Number;
                return true;
            }

            attempt = 0;
            return false;
        }

        public UniTask<ESUIBootstrapResult> StartAsync(
            string bootstrapKey,
            Func<CancellationToken, UniTask> start,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(bootstrapKey))
                throw new ArgumentException("BootstrapKey 不能为空。", nameof(bootstrapKey));
            if (start == null)
                throw new ArgumentNullException(nameof(start));
            if (disposed)
                return UniTask.FromResult(ESUIBootstrapResult.Create(bootstrapKey, ESUIBootstrapState.Stopped, null, 0));

            if (attempts.TryGetValue(bootstrapKey, out Attempt existing))
                return existing.Completion.Task;

            Attempt attempt = new Attempt(++nextAttempt, cancellationToken);
            attempts.Add(bootstrapKey, attempt);
            RunAsync(bootstrapKey, start, attempt).Forget();
            return attempt.Completion.Task;
        }

        public bool TryCancel(string bootstrapKey)
        {
            if (string.IsNullOrWhiteSpace(bootstrapKey) || !attempts.TryGetValue(bootstrapKey, out Attempt attempt))
                return false;
            attempt.Cancellation.Cancel();
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Attempt attempt in attempts.Values) attempt.Cancellation.Cancel();
            attempts.Clear();
        }

        private async UniTaskVoid RunAsync(string key, Func<CancellationToken, UniTask> start, Attempt attempt)
        {
            ESUIBootstrapResult result;
            try
            {
                await start(attempt.Cancellation.Token);
                result = ESUIBootstrapResult.Create(key, ESUIBootstrapState.Ready, null, attempt.Number);
            }
            catch (OperationCanceledException exception)
            {
                result = ESUIBootstrapResult.Create(key, ESUIBootstrapState.Cancelled, exception, attempt.Number);
            }
            catch (Exception exception)
            {
                result = ESUIBootstrapResult.Create(key, ESUIBootstrapState.Failed, exception, attempt.Number);
            }
            finally
            {
                attempts.Remove(key);
                attempt.Cancellation.Dispose();
            }

            attempt.Completion.TrySetResult(result);
        }
    }
}
