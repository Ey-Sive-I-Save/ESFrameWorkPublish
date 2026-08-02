using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESCameraDirectorTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private ESCameraDirector director;
        private RecordingAdapter adapter;

        [SetUp]
        public void SetUp()
        {
            director = new ESCameraDirector();
            adapter = new RecordingAdapter();
            Assert.That(director.RegisterView(ESCameraViewId.Main, 17, adapter), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            director?.Dispose();
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }

        [Test]
        public void ActiveSet_RecomputesAfterOutOfOrderRelease()
        {
            ESCameraLease first = director.Push(CreateRequest("base", 10));
            ESCameraLease second = director.Push(CreateRequest("shot", 10, ESCameraRequestKind.Shot));

            Assert.That(director.FlushNow(ESCameraViewId.Main), Is.True);
            Assert.That(adapter.last.profileKey, Is.EqualTo("shot"));

            Assert.That(director.Release(first), Is.True);
            director.FlushNow(ESCameraViewId.Main);
            Assert.That(adapter.last.profileKey, Is.EqualTo("shot"));

            Assert.That(director.Release(second), Is.True);
            director.FlushNow(ESCameraViewId.Main);
            Assert.That(adapter.clearCount, Is.EqualTo(1));
        }

        [Test]
        public void LeaseGeneration_PreventsOldLeaseFromReleasingReusedSlot()
        {
            ESCameraLease oldLease = director.Push(CreateRequest("first", 1));
            Assert.That(director.Release(oldLease), Is.True);

            ESCameraLease currentLease = director.Push(CreateRequest("second", 1));
            Assert.That(currentLease, Is.Not.EqualTo(oldLease));
            Assert.That(director.Release(oldLease), Is.False);

            director.FlushNow(ESCameraViewId.Main);
            Assert.That(adapter.last.profileKey, Is.EqualTo("second"));
        }

        [Test]
        public void DestroyedOwner_IsPurgedWithoutExplicitRelease()
        {
            ESCameraRequest request = CreateRequest("player", 1);
            ESCameraLease lease = director.Push(request);
            Assert.That(lease.IsValid, Is.True);
            director.FlushNow(ESCameraViewId.Main);

            Object.DestroyImmediate(request.owner);
            director.FlushNow(ESCameraViewId.Main);

            Assert.That(adapter.clearCount, Is.EqualTo(1));
        }

        [Test]
        public void SceneEpoch_PreventsOldSceneLeaseFromTouchingReplacementView()
        {
            ESCameraLease oldLease = director.Push(CreateRequest("old", 1));
            director.UnregisterView(ESCameraViewId.Main, 17, adapter);

            RecordingAdapter replacement = new RecordingAdapter();
            Assert.That(director.RegisterView(ESCameraViewId.Main, 18, replacement), Is.True);
            ESCameraLease currentLease = director.Push(CreateRequest("new", 1));

            Assert.That(director.Release(oldLease), Is.False);
            director.FlushNow(ESCameraViewId.Main);
            Assert.That(replacement.last.profileKey, Is.EqualTo("new"));
            Assert.That(director.Release(currentLease), Is.True);
        }

        [Test]
        public void LookInput_IsOnlyAppliedToTheCurrentWinner()
        {
            ESCameraRequest firstRequest = CreateRequest("first", 1);
            ESCameraRequest secondRequest = CreateRequest("second", 2);
            ESCameraLease first = director.Push(firstRequest);
            ESCameraLease second = director.Push(secondRequest);

            Assert.That(director.TrySetLook(first, new Vector2(3f, 2f)), Is.False);
            director.FlushNow(ESCameraViewId.Main);
            Assert.That(adapter.last.hasLookInput, Is.False);

            Assert.That(director.TrySetLook(second, new Vector2(5f, -1f)), Is.True);
            director.FlushNow(ESCameraViewId.Main);
            Assert.That(adapter.last.hasLookInput, Is.True);
            Assert.That(adapter.last.lookInput, Is.EqualTo(new Vector2(5f, -1f)));
        }

        [Test]
        public void LeaseLookAndTarget_RequireTheWinningLeaseOwner()
        {
            ESCameraRequest firstRequest = CreateRequest("first", 1);
            ESCameraRequest secondRequest = CreateRequest("second", 2);
            ESCameraLease first = director.Push(firstRequest);
            ESCameraLease second = director.Push(secondRequest);

            Assert.That(director.TrySetLook(first, new Vector2(1f, 1f)), Is.False);
            Assert.That(director.TrySetLook(second, new Vector2(3f, -2f)), Is.True);

            GameObject replacementFollow = new GameObject("Camera Follow Replacement");
            created.Add(replacementFollow);
            Assert.That(director.TrySetTarget(second, replacementFollow.transform), Is.True);

            director.FlushNow(ESCameraViewId.Main);
            Assert.That(adapter.last.follow, Is.EqualTo(replacementFollow.transform));
            Assert.That(adapter.last.hasLookInput, Is.True);
            Assert.That(adapter.last.lookInput, Is.EqualTo(new Vector2(3f, -2f)));
        }

        [Test]
        public void Modifiers_ComposeByExplicitOperationAndPriority()
        {
            director.Push(CreateRequest("player", 1));
            director.Push(CreateModifierRequest(0, new ESCameraModifier
            {
                fieldOfView = new ESCameraScalarModifier { operation = ESCameraModifierOperation.Add, value = 5f },
                distanceScale = new ESCameraScalarModifier { operation = ESCameraModifierOperation.Multiply, value = 0.8f },
            }));
            director.Push(CreateModifierRequest(20, new ESCameraModifier
            {
                fieldOfView = new ESCameraScalarModifier { operation = ESCameraModifierOperation.Override, value = 75f },
            }));
            director.Push(CreateModifierRequest(10, new ESCameraModifier
            {
                fieldOfView = new ESCameraScalarModifier { operation = ESCameraModifierOperation.Override, value = 90f },
            }));

            director.FlushNow(ESCameraViewId.Main);

            Assert.That(adapter.last.modifiers.fieldOfView.Apply(60f), Is.EqualTo(80f));
            Assert.That(adapter.last.modifiers.distanceScale.Apply(1f), Is.EqualTo(0.8f));
        }

        [Test]
        public void Modifiers_RespectProfileCompatibility()
        {
            director.Push(CreateRequest("player", 1));
            ESCameraRequest modifier = CreateModifierRequest(100, new ESCameraModifier
            {
                fieldOfView = new ESCameraScalarModifier { operation = ESCameraModifierOperation.Override, value = 20f },
            });
            modifier.compatibleProfileKey = "cutscene";
            director.Push(modifier);

            director.FlushNow(ESCameraViewId.Main);

            Assert.That(adapter.last.modifiers.fieldOfView.Apply(60f), Is.EqualTo(60f));
        }

        [Test]
        public void Dispose_DisposesRegisteredAdapter()
        {
            director.Dispose();
            director = null;

            Assert.That(adapter.disposeCount, Is.EqualTo(1));
        }

        [Test]
        public void OutputTransform_IsAvailableWithoutLeakingTheAdapter()
        {
            GameObject output = new GameObject("Camera Output");
            created.Add(output);
            adapter.outputTransform = output.transform;

            Assert.That(director.TryGetOutputTransform(ESCameraViewId.Main, out Transform actual), Is.True);
            Assert.That(actual, Is.SameAs(output.transform));
        }

        private ESCameraRequest CreateRequest(string profileKey, int priority, ESCameraRequestKind kind = ESCameraRequestKind.Base)
        {
            GameObject owner = new GameObject("Camera Owner " + profileKey);
            GameObject follow = new GameObject("Camera Follow " + profileKey);
            created.Add(owner);
            created.Add(follow);
            return new ESCameraRequest
            {
                viewId = ESCameraViewId.Main,
                kind = kind,
                profileKey = profileKey,
                priority = priority,
                owner = owner,
                follow = follow.transform,
            };
        }

        private ESCameraRequest CreateModifierRequest(int priority, ESCameraModifier modifier)
        {
            GameObject owner = new GameObject("Camera Modifier Owner");
            created.Add(owner);
            return new ESCameraRequest
            {
                viewId = ESCameraViewId.Main,
                kind = ESCameraRequestKind.Modifier,
                priority = priority,
                owner = owner,
                modifier = modifier,
            };
        }

        private sealed class RecordingAdapter : IESCameraViewAdapter, System.IDisposable
        {
            public bool IsReady => true;
            public Transform outputTransform;
            public Transform OutputTransform => outputTransform;
            public ESCameraResolvedView last;
            public int clearCount;
            public int disposeCount;

            public void Apply(in ESCameraResolvedView resolved)
            {
                last = resolved;
            }

            public void Clear()
            {
                clearCount++;
                last = default;
            }

            public void Dispose()
            {
                disposeCount++;
            }
        }
    }
}
