using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ES.Tests.UI
{
    public sealed class ESUIRuntimeFrameworkTests
    {
        [Test]
        public void CanonicalId_UsesOrdinalIdentityAndStableHash()
        {
            var first = new ESUICanonicalId("ui:inventory");
            var same = new ESUICanonicalId("ui:inventory");
            var differentCase = new ESUICanonicalId("ui:Inventory");

            Assert.That(first, Is.EqualTo(same));
            Assert.That(first.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(differentCase));
            Assert.That(first.ToString(), Is.EqualTo("ui:inventory"));
        }

        [Test]
        public void StateBinding_EmitsCurrentAndPublishesLatestValue()
        {
            int value = 1;
            var owner = new ESUICanonicalId("ui:inventory");
            var binding = new ESUIStateBinding<int>(() => value, owner);
            int observed = 0;
            Assert.That(binding.OwnerId, Is.EqualTo(owner));
            Assert.That(binding.Revision, Is.EqualTo(0));
            using (binding.Subscribe(v => observed = v))
            {
                Assert.That(observed, Is.EqualTo(1));
                value = 2;
                binding.Publish();
                Assert.That(observed, Is.EqualTo(2));
                Assert.That(binding.Revision, Is.EqualTo(1));
            }
            binding.Dispose();
        }

        [Test]
        public void ContentPresenter_IgnoresLateResultFromPreviousGeneration()
        {
            var first = new UniTaskCompletionSource<string>();
            var second = new UniTaskCompletionSource<string>();
            int calls = 0;
            string ready = null;
            var presenter = new ESUIContentPresenter<string>(
                _ => ++calls == 1 ? first.Task : second.Task,
                value => ready = value);

            UniTask<bool> firstLoad = presenter.LoadAsync();
            UniTask<bool> secondLoad = presenter.LoadAsync();
            first.TrySetResult("stale");
            Assert.That(firstLoad.GetAwaiter().GetResult(), Is.False);
            second.TrySetResult("current");
            Assert.That(secondLoad.GetAwaiter().GetResult(), Is.True);
            Assert.That(ready, Is.EqualTo("current"));
            Assert.That(presenter.Generation, Is.EqualTo(2));
            presenter.Dispose();
        }

        [Test]
        public void ContextStore_IsScopedVersionedAndOneShot()
        {
            var store = new ESUIContextStore();
            var id = new ESUICanonicalId("ui:inventory");
            var snapshot = new ESUIContextSnapshot(id, 2, "player:1", "{\"tab\":\"weapons\"}", DateTimeOffset.UtcNow);

            Assert.That(store.Stage(snapshot), Is.True);
            Assert.That(store.TryTake(id, "player:1", 1, out _), Is.False);
            Assert.That(store.Count, Is.EqualTo(1));
            Assert.That(store.TryPeek(id, "player:1", 2, out ESUIContextSnapshot restored), Is.True);
            Assert.That(restored.Payload, Is.EqualTo(snapshot.Payload));
            Assert.That(store.Count, Is.EqualTo(1));
            Assert.That(store.Consume(restored), Is.True);
            Assert.That(store.TryTake(id, "player:1", 2, out _), Is.False);
        }

        [Test]
        public void BootstrapCoordinator_DeduplicatesInFlightAndAllowsRetry()
        {
            var coordinator = new ESUIBootstrapCoordinator();
            var completion = new UniTaskCompletionSource<bool>();
            int starts = 0;
            UniTask<ESUIBootstrapResult> first = coordinator.StartAsync(
                "root:main",
                async token =>
                {
                    starts++;
                    await completion.Task;
                    token.ThrowIfCancellationRequested();
                });
            UniTask<ESUIBootstrapResult> duplicate = coordinator.StartAsync(
                "root:main",
                _ => UniTask.CompletedTask);

            Assert.That(starts, Is.EqualTo(1));
            Assert.That(coordinator.IsInFlight("root:main"), Is.True);
            Assert.That(coordinator.TryGetAttempt("root:main", out long attempt), Is.True);
            Assert.That(attempt, Is.EqualTo(1));
            completion.TrySetResult(true);
            Assert.That(first.GetAwaiter().GetResult().IsSuccess, Is.True);
            Assert.That(duplicate.GetAwaiter().GetResult().Attempt, Is.EqualTo(1));
            Assert.That(coordinator.IsInFlight("root:main"), Is.False);

            UniTask<ESUIBootstrapResult> retry = coordinator.StartAsync("root:main", _ => UniTask.CompletedTask);
            Assert.That(retry.GetAwaiter().GetResult().Attempt, Is.EqualTo(2));
            coordinator.Dispose();
        }

        [Test]
        public void PageNavigator_PushReplaceAndBackMaintainHistory()
        {
            var navigator = new ESUIPageNavigator((identity, data, token) => UniTask.FromResult<ESUIWindowLease>(null));
            navigator.PushAsync(ESUIWindowIdentity.FromBuiltIn(ESUIWindowId.MainMenu)).GetAwaiter().GetResult();
            navigator.PushAsync(ESUIWindowIdentity.FromBuiltIn(ESUIWindowId.Inventory)).GetAwaiter().GetResult();
            Assert.That(navigator.Count, Is.EqualTo(2));
            Assert.That(navigator.Current.Value.BuiltInId, Is.EqualTo(ESUIWindowId.Inventory));
            Assert.That(navigator.CurrentCanonicalId.Value, Is.EqualTo(new ESUICanonicalId("builtin:Inventory")));
            navigator.BackAsync().GetAwaiter().GetResult();
            Assert.That(navigator.Count, Is.EqualTo(1));
        }

        [Test]
        public void PageNavigator_StagesCanonicalNavigationContext()
        {
            var navigator = new ESUIPageNavigator((identity, data, token) => UniTask.FromResult<ESUIWindowLease>(null));
            navigator.PushAsync(ESUIWindowIdentity.FromString("ui:inventory"), "weapons").GetAwaiter().GetResult();
            var store = new ESUIContextStore();
            Assert.That(navigator.StageContext(store, "player:1", 1, entries => entries[0].CanonicalId.Value + ":" + entries[0].Data), Is.True);
            Assert.That(store.TryPeek(new ESUICanonicalId("ui:inventory"), "player:1", 1, out ESUIContextSnapshot snapshot), Is.True);
            Assert.That(snapshot.Payload, Is.EqualTo("ui:inventory:weapons"));
        }

        [Test]
        public void PageNavigator_RestoresValidatedEntriesInOrder()
        {
            var navigator = new ESUIPageNavigator((identity, data, token) => UniTask.FromResult<ESUIWindowLease>(null));
            var entries = new[]
            {
                new ESUIPageNavigationEntry(ESUIWindowIdentity.FromString("ui:main"), new ESUICanonicalId("ui:main"), null),
                new ESUIPageNavigationEntry(ESUIWindowIdentity.FromString("ui:inventory"), new ESUICanonicalId("ui:inventory"), "weapons")
            };
            Assert.That(navigator.RestoreAsync(entries).GetAwaiter().GetResult(), Is.True);
            Assert.That(navigator.Count, Is.EqualTo(2));
            Assert.That(navigator.CurrentCanonicalId.Value, Is.EqualTo(new ESUICanonicalId("ui:inventory")));
        }

        [Test]
        public void FocusCoordinator_ClaimsAndClearsSelectable()
        {
            var eventSystemObject = new GameObject("UI Test EventSystem");
            var eventSystem = eventSystemObject.AddComponent<EventSystem>();
            var buttonObject = new GameObject("Button");
            buttonObject.AddComponent<CanvasRenderer>();
            var button = buttonObject.AddComponent<Button>();
            var focus = new ESUIFocusCoordinator(eventSystem);
            var owner = new ESUICanonicalId("ui:inventory");
            Assert.That(focus.Claim(button, owner), Is.True);
            Assert.That(focus.Current, Is.EqualTo(button));
            Assert.That(focus.CurrentOwnerId, Is.EqualTo(owner));
            focus.Clear();
            Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
            UnityEngine.Object.DestroyImmediate(buttonObject);
            UnityEngine.Object.DestroyImmediate(eventSystemObject);
        }
    }
}
