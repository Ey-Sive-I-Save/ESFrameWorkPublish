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
            Assert.That(adapter.last.definition.stringKey, Is.EqualTo("shot"));

            Assert.That(director.Release(first), Is.True);
            director.FlushNow(ESCameraViewId.Main);
            Assert.That(adapter.last.definition.stringKey, Is.EqualTo("shot"));

            Assert.That(director.Release(second), Is.True);
            director.FlushNow(ESCameraViewId.Main);
            Assert.That(adapter.clearCount, Is.EqualTo(1));
        }

        [Test]
        public void DiagnosticSnapshot_ReportsWinnerAndActiveCount()
        {
            ESCameraRequest request = CreateRequest("diagnostic", 5);
            ESCameraLease lease = director.Push(request);

            Assert.That(director.TryGetDiagnosticSnapshot(ESCameraViewId.Main, out ESCameraDiagnosticSnapshot snapshot), Is.True);
            Assert.That(snapshot.hasWinner, Is.True);
            Assert.That(snapshot.activeRequestCount, Is.EqualTo(1));
            Assert.That(snapshot.winnerKind, Is.EqualTo(ESCameraRequestKind.Base));
            Assert.That(snapshot.winnerDefinition.stringKey, Is.EqualTo("diagnostic"));
            Assert.That(snapshot.sceneEpoch, Is.EqualTo(17));
            ESCameraDiagnosticReceipt receipt = ESCameraDiagnosticReceipt.FromSnapshot(snapshot, 42);
            Assert.That(receipt.frame, Is.EqualTo(42));
            Assert.That(receipt.viewKey, Is.EqualTo("MainView"));
            Assert.That(receipt.winnerDefinitionKey, Is.EqualTo("diagnostic"));

            Assert.That(director.Release(lease), Is.True);
            Assert.That(director.TryGetDiagnosticSnapshot(ESCameraViewId.Main, out snapshot), Is.True);
            Assert.That(snapshot.hasWinner, Is.False);
            Assert.That(snapshot.activeRequestCount, Is.EqualTo(0));
        }

        [Test]
        public void WinnerOrdering_IsDeterministicForKindThenSubmissionSequence()
        {
            ESCameraLease baseLease = director.Push(CreateRequest("base", 10));
            ESCameraLease shotLease = director.Push(CreateRequest("shot", 10, ESCameraRequestKind.Shot));

            director.FlushNow(ESCameraViewId.Main);
            Assert.That(adapter.last.definition.stringKey, Is.EqualTo("shot"));

            Assert.That(director.Release(shotLease), Is.True);
            ESCameraLease newerBaseLease = director.Push(CreateRequest("newer-base", 10));
            director.FlushNow(ESCameraViewId.Main);

            Assert.That(adapter.last.definition.stringKey, Is.EqualTo("newer-base"));
            Assert.That(director.Release(baseLease), Is.True);
            Assert.That(director.Release(newerBaseLease), Is.True);
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
            Assert.That(adapter.last.definition.stringKey, Is.EqualTo("second"));
        }

        [Test]
        public void ReleaseOwnedBy_ClearsAllRequestsForTheOwner()
        {
            ESCameraRequest firstRequest = CreateRequest("owned-base", 1);
            ESCameraRequest secondRequest = CreateRequest("owned-shot", 10, ESCameraRequestKind.Shot);
            secondRequest.owner = firstRequest.owner;

            ESCameraLease first = director.Push(firstRequest);
            ESCameraLease second = director.Push(secondRequest);
            Assert.That(first.IsValid, Is.True);
            Assert.That(second.IsValid, Is.True);
            director.FlushNow(ESCameraViewId.Main);

            Assert.That(director.ReleaseOwnedBy(firstRequest.owner), Is.EqualTo(2));
            director.FlushNow(ESCameraViewId.Main);

            Assert.That(adapter.clearCount, Is.EqualTo(1));
            Assert.That(director.Release(first), Is.False);
            Assert.That(director.Release(second), Is.False);
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
        public void InactiveFollowTarget_IsPurgedWithoutExplicitRelease()
        {
            ESCameraRequest request = CreateRequest("inactive-follow", 1);
            ESCameraLease lease = director.Push(request);
            Assert.That(lease.IsValid, Is.True);
            director.FlushNow(ESCameraViewId.Main);

            request.follow.gameObject.SetActive(false);
            director.FlushNow(ESCameraViewId.Main);

            Assert.That(adapter.clearCount, Is.EqualTo(1));
            Assert.That(director.Release(lease), Is.False);
        }

        [Test]
        public void InactiveLookAtTarget_IsClearedWhileFollowRemainsActive()
        {
            ESCameraRequest request = CreateRequest("inactive-look-at", 1);
            GameObject lookAt = new GameObject("LookAt Target");
            created.Add(lookAt);
            request.lookAt = lookAt.transform;
            ESCameraLease lease = director.Push(request);
            Assert.That(lease.IsValid, Is.True);
            director.FlushNow(ESCameraViewId.Main);

            lookAt.SetActive(false);
            director.FlushNow(ESCameraViewId.Main);

            Assert.That(adapter.last.hasWinner, Is.True);
            Assert.That(adapter.last.follow, Is.EqualTo(request.follow));
            Assert.That(adapter.last.lookAt, Is.Null);
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
            Assert.That(replacement.last.definition.stringKey, Is.EqualTo("new"));
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
        public void LosingLeaseCannotSubmitLookWhenOwnerIsSharedWithWinner()
        {
            ESCameraRequest baseRequest = CreateRequest("shared-base", 1);
            ESCameraRequest shotRequest = CreateRequest("shared-shot", 20, ESCameraRequestKind.Shot);
            shotRequest.owner = baseRequest.owner;

            ESCameraLease baseLease = director.Push(baseRequest);
            ESCameraLease shotLease = director.Push(shotRequest);
            Assert.That(baseLease.IsValid, Is.True);
            Assert.That(shotLease.IsValid, Is.True);
            director.FlushNow(ESCameraViewId.Main);

            Assert.That(director.TrySetLook(baseLease, new Vector2(99f, 99f)), Is.False);
            Assert.That(director.TrySetLook(shotLease, new Vector2(4f, -2f)), Is.True);
            director.FlushNow(ESCameraViewId.Main);

            Assert.That(adapter.last.definition.stringKey, Is.EqualTo("shared-shot"));
            Assert.That(adapter.last.lookInput, Is.EqualTo(new Vector2(4f, -2f)));
        }

        [Test]
        public void ModifierLease_CannotChangeFollowTarget()
        {
            ESCameraLease modifierLease = director.Push(CreateModifierRequest(10, new ESCameraModifier
            {
                fieldOfView = new ESCameraScalarModifier
                {
                    operation = ESCameraModifierOperation.Add,
                    value = 2f,
                },
            }));
            GameObject follow = new GameObject("Modifier Follow");
            created.Add(follow);

            Assert.That(director.TrySetTarget(modifierLease, follow.transform), Is.False);
        }

        [Test]
        public void ModifierWithInvalidSecondaryField_IsRejectedInsteadOfMaskedByValidField()
        {
            ESCameraRequest request = CreateModifierRequest(10, new ESCameraModifier
            {
                fieldOfView = new ESCameraScalarModifier
                {
                    operation = ESCameraModifierOperation.Add,
                    value = 2f,
                },
                shoulderOffset = new ESCameraVectorModifier
                {
                    operation = ESCameraModifierOperation.Add,
                    value = new Vector3(float.NaN, 0f, 0f),
                },
            });

            Assert.That(request.IsStructurallyValid, Is.False);
            Assert.That(director.Push(request).IsValid, Is.False);
        }

        [Test]
        public void UpdateCannotTransferLeaseToAnotherOwner()
        {
            ESCameraRequest original = CreateRequest("owner-a", 1);
            ESCameraRequest replacement = CreateRequest("owner-b", 1);
            ESCameraLease lease = director.Push(original);

            Assert.That(lease.IsValid, Is.True);
            replacement.viewId = original.viewId;
            Assert.That(director.Update(lease, replacement), Is.False);
        }

        [Test]
        public void NonFiniteLookInput_IsRejected()
        {
            ESCameraLease lease = director.Push(CreateRequest("finite-look", 1));
            Assert.That(lease.IsValid, Is.True);
            Assert.That(director.TrySetLook(lease, new Vector2(float.NaN, 0f)), Is.False);
            Assert.That(director.TrySetLook(lease, new Vector2(0f, float.PositiveInfinity)), Is.False);
        }

        [Test]
        public void TimelineShot_ConvertsToRegularShotWithoutPrivilege()
        {
            GameObject owner = new GameObject("Timeline Owner");
            GameObject follow = new GameObject("Timeline Follow");
            created.Add(owner);
            created.Add(follow);

            ESCameraTimelineShot shot = ESCameraTimelineShot.Create(
                ESCameraViewId.Main,
                new ESCameraDefinitionReference(ESCameraDefinitionEnumKey.None, "timeline"),
                50,
                owner,
                follow.transform);

            Assert.That(shot.IsValid, Is.True);
            ESCameraRequest request = shot.ToRequest();
            Assert.That(request.kind, Is.EqualTo(ESCameraRequestKind.Shot));
            Assert.That(request.priority, Is.EqualTo(50));
            Assert.That(request.owner, Is.SameAs(owner));
            Assert.That(request.follow, Is.SameAs(follow.transform));
        }

        [Test]
        public void TimelineShot_RejectsMissingFollowTarget()
        {
            GameObject owner = new GameObject("Timeline Owner Without Target");
            created.Add(owner);

            ESCameraTimelineShot shot = ESCameraTimelineShot.Create(
                ESCameraViewId.Main,
                new ESCameraDefinitionReference(ESCameraDefinitionEnumKey.None, "timeline"),
                1,
                owner,
                null);

            Assert.That(shot.IsValid, Is.False);
            Assert.That(shot.ToRequest().IsStructurallyValid, Is.False);
        }

        [Test]
        public void LookOnNewWinner_DoesNotClearPendingConfigurationChange()
        {
            ESCameraLease first = director.Push(CreateRequest("first", 1));
            Assert.That(first.IsValid, Is.True);
            director.FlushNow(ESCameraViewId.Main);
            Assert.That(adapter.last.definition.stringKey, Is.EqualTo("first"));

            ESCameraLease second = director.Push(CreateRequest("second", 10));
            Assert.That(director.TrySetLook(second, new Vector2(2f, -1f)), Is.True);
            director.FlushNow(ESCameraViewId.Main);

            Assert.That(adapter.last.definition.stringKey, Is.EqualTo("second"));
            Assert.That(adapter.last.configurationChanged, Is.True);
            Assert.That(adapter.last.hasLookInput, Is.True);
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
            modifier.compatibleDefinition = new ESCameraDefinitionReference(ESCameraDefinitionEnumKey.None, "cutscene");
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

        [Test]
        public void FailedAdapterApply_DoesNotBecomeApplied()
        {
            adapter.applyResult = false;
            ESCameraLease lease = director.Push(CreateRequest("failed", 1));
            Assert.That(lease.IsValid, Is.True);

            director.FlushNow(ESCameraViewId.Main);

            Assert.That(adapter.clearCount, Is.EqualTo(1));
            adapter.applyResult = true;
            director.FlushNow(ESCameraViewId.Main);
            Assert.That(adapter.last.hasWinner, Is.True);
        }

        [Test]
        public void LateTick_IsIdempotentWithinTheSameFrame()
        {
            ESCameraLease lease = director.Push(CreateRequest("late-tick", 1));
            Assert.That(lease.IsValid, Is.True);

            director.LateTick();
            director.LateTick();

            Assert.That(adapter.applyCount, Is.EqualTo(1));
        }

        private ESCameraRequest CreateRequest(string definitionKey, int priority, ESCameraRequestKind kind = ESCameraRequestKind.Base)
        {
            GameObject owner = new GameObject("Camera Owner " + definitionKey);
            GameObject follow = new GameObject("Camera Follow " + definitionKey);
            created.Add(owner);
            created.Add(follow);
            return new ESCameraRequest
            {
                viewId = ESCameraViewId.Main,
                kind = kind,
                definition = new ESCameraDefinitionReference(ESCameraDefinitionEnumKey.None, definitionKey),
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
            private readonly Dictionary<ESCameraDefinitionReference, ESCameraDefinitionRuntimeHandle> handles = new Dictionary<ESCameraDefinitionReference, ESCameraDefinitionRuntimeHandle>();
            public bool IsReady => true;
            public Transform outputTransform;
            public Transform OutputTransform => outputTransform;
            public ESCameraResolvedView last;
            public int clearCount;
            public int disposeCount;
            public int applyCount;
            public bool applyResult = true;

            public bool TryResolveDefinition(ESCameraDefinitionReference reference, out ESCameraDefinitionRuntimeHandle handle)
            {
                if (!reference.IsConfigured)
                {
                    handle = default;
                    return false;
                }

                if (!handles.TryGetValue(reference, out handle))
                {
                    handle = new ESCameraDefinitionRuntimeHandle(1, 1, handles.Count + 1, "TEST");
                    handles.Add(reference, handle);
                }

                return true;
            }

            public bool Apply(in ESCameraResolvedView resolved)
            {
                applyCount++;
                last = resolved;
                return applyResult;
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
