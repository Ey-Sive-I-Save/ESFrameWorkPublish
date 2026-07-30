using System;
using NUnit.Framework;

namespace ES.Tests
{
    public sealed class ESValueChangeSetTests
    {
        private sealed class EffectLeaseOwner : IESEffectLeaseOwner
        {
            public int releaseCount;
            public int activeGeneration;

            public bool ReleaseEffect(int effectSlot, int generation)
            {
                if (effectSlot != 4 || generation != activeGeneration)
                    return false;

                activeGeneration = 0;
                releaseCount++;
                return true;
            }
        }

        private sealed class ExpressionDependencySink : IESExpressionDependencySink
        {
            public ContextPool floatContext;
            public string floatKey;

            public void ObserveContextFloat(ContextPool context, string key)
            {
                floatContext = context;
                floatKey = key;
            }

            public void ObserveContextBool(ContextPool context, string key)
            {
            }
        }

        private sealed class ContextFloatReceiver : IReceiveChannelLink_Context_Float
        {
            public int count;
            public Action onReceived;

            public void OnLink(string key, Link_ContextEvent_FloatChange link)
            {
                count++;
                onReceived?.Invoke();
            }
        }

        [Test]
        public void FloatSet_ComposesModifierStagesAndClamps()
        {
            ESFloatValueChangeSet set = new ESFloatValueChangeSet(10f);
            set.Add(ESFloatValueChangeOp.Add, 2f);
            set.Add(ESFloatValueChangeOp.AddPercent, 0.5f);
            set.Add(ESFloatValueChangeOp.Multiply, 2f);
            set.Add(ESFloatValueChangeOp.Min, 20f);
            set.Add(ESFloatValueChangeOp.Max, 40f);

            Assert.That(set.Value, Is.EqualTo(36f));
        }

        [Test]
        public void FloatSet_RejectsNonFiniteInputsWithoutMutatingState()
        {
            ESFloatValueChangeSet set = new ESFloatValueChangeSet(5f);
            ESValueChangeToken token = set.Add(ESFloatValueChangeOp.Add, 2f);
            int revision = set.Revision;

            Assert.That(set.Add(ESFloatValueChangeOp.Add, float.NaN).IsValid, Is.False);
            Assert.That(set.Add(ESFloatValueChangeOp.Add, float.PositiveInfinity).IsValid, Is.False);
            Assert.That(set.Add(ESFloatValueChangeOp.Add, float.NegativeInfinity).IsValid, Is.False);
            Assert.That(set.Update(token, float.NaN), Is.False);
            Assert.That(set.Update(token, ESFloatValueChangeOp.Add, float.PositiveInfinity, 0), Is.False);
            Assert.That(set.Revision, Is.EqualTo(revision));
            Assert.That(set.Value, Is.EqualTo(7f));

            Assert.Throws<ArgumentOutOfRangeException>(() => new ESFloatValueChangeSet(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => set.BaseValue = float.NegativeInfinity);
        }

        [Test]
        public void FloatSet_SaturatesFiniteModifierAggregation()
        {
            ESFloatValueChangeSet set = new ESFloatValueChangeSet(float.MaxValue);
            set.Add(ESFloatValueChangeOp.Add, float.MaxValue);

            Assert.That(float.IsNaN(set.Value), Is.False);
            Assert.That(float.IsInfinity(set.Value), Is.False);
            Assert.That(set.Value, Is.EqualTo(float.MaxValue));
        }

        [Test]
        public void ValueChangeExpressionBindings_RejectNonDeterministicExpressions()
        {
            ESFloatValueChangeSet floatSet = new ESFloatValueChangeSet();
            ESFloatValueChangeTracker floatTracker = new ESFloatValueChangeTracker(floatSet);
            ESFloatValueChangeExpressionBinding floatBinding = new ESFloatValueChangeExpressionBinding
            {
                value = new FloatExpressionSource
                {
                    useDirectFloat = false,
                    expression = new ESRandomRangeFloatExpression()
                }
            };

            Assert.That(floatBinding.IsDeterministic, Is.False);
            Assert.That(floatBinding.ApplyOrRefresh(floatTracker, null, null).IsValid, Is.False);
            Assert.That(floatTracker.Count, Is.Zero);

            ESPermitSet permitSet = new ESPermitSet();
            ESPermitValueChangeTracker permitTracker = new ESPermitValueChangeTracker(permitSet);
            ESPermitValueChangeExpressionBinding permitBinding = new ESPermitValueChangeExpressionBinding
            {
                condition = new BoolExpressionSource
                {
                    useDirectBool = false,
                    expression = new ESCompareFloatExpression
                    {
                        left = new ESRandomRangeFloatExpression(),
                        right = new ESConstantFloatExpression()
                    }
                }
            };

            Assert.That(permitBinding.IsDeterministic, Is.False);
            Assert.That(permitBinding.ApplyOrRefresh(permitTracker, null, null).IsValid, Is.False);
            Assert.That(permitTracker.Count, Is.Zero);
        }

        [Test]
        public void ContextExpressionCaptureAndLinkLease_AreKeyScopedAndReversible()
        {
            ESOpSupport support = ESOpSupport.CreateStandalone();
            ContextPool context = support.Context;
            context.SetFloat("Buff.Power", 1f);
            ExpressionDependencySink sink = new ExpressionDependencySink();

            using (ESExpressionDependencyCapture.Begin(sink))
                new ESContextFloatExpression { key = "Buff.Power" }.Evaluate(null, support);

            Assert.That(sink.floatContext, Is.SameAs(context));
            Assert.That(sink.floatKey, Is.EqualTo("Buff.Power"));

            ContextFloatReceiver receiver = new ContextFloatReceiver();
            Assert.That(context.TryAcquireValueChangeFloatLink("Buff.Power"), Is.True);
            Assert.That(context.LinkRCL_Float.AddReceiver("Buff.Power", receiver), Is.True);
            context.LinkRCL_Float.ApplyChannelBuffers("Buff.Power");
            Assert.That(context.LinkRCL_Float.GetSubscriberCount("Buff.Power"), Is.EqualTo(1));
            context.SetFloat("Buff.Power", 2f);
            Assert.That(receiver.count, Is.EqualTo(1));

            Assert.That(context.LinkRCL_Float.RemoveReceiver("Buff.Power", receiver), Is.True);
            context.LinkRCL_Float.ApplyChannelBuffers("Buff.Power");
            Assert.That(context.LinkRCL_Float.GetSubscriberCount("Buff.Power"), Is.Zero);
            context.ReleaseValueChangeFloatLink("Buff.Power");
            context.SetFloat("Buff.Power", 3f);
            Assert.That(receiver.count, Is.EqualTo(1));

            ContextFloatReceiver reentrantReceiver = new ContextFloatReceiver();
            reentrantReceiver.onReceived = () => context.LinkRCL_Float.RemoveReceiver("Buff.Power", reentrantReceiver);
            Assert.That(context.TryAcquireValueChangeFloatLink("Buff.Power"), Is.True);
            Assert.That(context.LinkRCL_Float.AddReceiver("Buff.Power", reentrantReceiver), Is.True);
            context.LinkRCL_Float.ApplyChannelBuffers("Buff.Power");
            context.SetFloat("Buff.Power", 4f);
            Assert.That(reentrantReceiver.count, Is.EqualTo(1));
            Assert.That(context.LinkRCL_Float.GetSubscriberCount("Buff.Power"), Is.Zero);
            context.ReleaseValueChangeFloatLink("Buff.Power");

            support.Dispose();
        }

        [Test]
        public void FloatSet_DefinitionBoundsApplyAfterRuntimeModifierBounds()
        {
            ESFloatValueChangeSet set = new ESFloatValueChangeSet(5f);
            set.SetBounds(0f, 10f);
            ESValueChangeToken token = set.Add(ESFloatValueChangeOp.Override, 20f);
            set.Add(ESFloatValueChangeOp.Min, 50f);
            Assert.That(set.Value, Is.EqualTo(10f));

            Assert.That(set.Update(token, ESFloatValueChangeOp.Override, -20f, 0), Is.True);
            set.Add(ESFloatValueChangeOp.Max, -50f);
            Assert.That(set.Value, Is.EqualTo(0f));
        }

        [Test]
        public void FloatSet_OverrideUsesPriorityThenLatestOrder()
        {
            ESFloatValueChangeSet set = new ESFloatValueChangeSet(10f);
            set.Add(ESFloatValueChangeOp.Override, 4f, priority: 1);
            ESValueChangeToken latestSamePriority = set.Add(ESFloatValueChangeOp.Override, 6f, priority: 1);
            set.Add(ESFloatValueChangeOp.Override, 8f, priority: 2);
            set.Add(ESFloatValueChangeOp.Add, 2f);

            Assert.That(set.Value, Is.EqualTo(10f));
            Assert.That(set.Update(latestSamePriority, ESFloatValueChangeOp.Override, 20f, 3), Is.True);
            Assert.That(set.Value, Is.EqualTo(22f));
        }

        [Test]
        public void FloatSet_ClearInvalidatesExistingTokens()
        {
            ESFloatValueChangeSet set = new ESFloatValueChangeSet();
            ESValueChangeToken stale = set.Add(ESFloatValueChangeOp.Add, 2f);

            set.Clear();
            ESValueChangeToken current = set.Add(ESFloatValueChangeOp.Add, 5f);

            Assert.That(current.tokenId, Is.EqualTo(stale.tokenId));
            Assert.That(current.tokenVersion, Is.Not.EqualTo(stale.tokenVersion));
            Assert.That(set.Contains(stale), Is.False);
            Assert.That(set.Release(stale), Is.False);
            Assert.That(set.Value, Is.EqualTo(5f));
        }

        [Test]
        public void FloatSet_RejectsTokenIssuedByAnotherSet()
        {
            ESFloatValueChangeSet first = new ESFloatValueChangeSet();
            ESFloatValueChangeSet second = new ESFloatValueChangeSet();
            ESValueChangeToken firstToken = first.Add(ESFloatValueChangeOp.Add, 2f);
            ESValueChangeToken secondToken = second.Add(ESFloatValueChangeOp.Add, 5f);

            Assert.That(firstToken.tokenId, Is.EqualTo(secondToken.tokenId));
            Assert.That(firstToken.setId, Is.Not.EqualTo(secondToken.setId));
            Assert.That(second.Release(firstToken), Is.False);
            Assert.That(second.Contains(secondToken), Is.True);
            Assert.That(second.Value, Is.EqualTo(5f));
        }

        [Test]
        public void FloatTracker_RejectsForeignTokenWithoutForgettingItsOwnToken()
        {
            ESFloatValueChangeSet ownedSet = new ESFloatValueChangeSet();
            ESFloatValueChangeSet foreignSet = new ESFloatValueChangeSet();
            ESFloatValueChangeTracker tracker = new ESFloatValueChangeTracker(ownedSet);
            ESValueChangeToken owned = tracker.Add(ESFloatValueChangeOp.Add, 2f);
            ESValueChangeToken foreign = foreignSet.Add(ESFloatValueChangeOp.Add, 7f);

            Assert.That(tracker.Release(foreign), Is.False);
            Assert.That(tracker.HasToken, Is.True);
            Assert.That(ownedSet.Contains(owned), Is.True);
            Assert.That(tracker.Release(owned), Is.True);
        }

        [Test]
        public void FloatSet_BatchCoalescesNotificationsWithoutDelayingRevision()
        {
            ESFloatValueChangeSet set = new ESFloatValueChangeSet();
            int notificationCount = 0;
            set.Changed += _ => notificationCount++;

            using (set.BeginBatch())
            {
                set.Add(ESFloatValueChangeOp.Add, 2f);
                set.Add(ESFloatValueChangeOp.Add, 3f);
            }

            Assert.That(set.Revision, Is.EqualTo(2));
            Assert.That(notificationCount, Is.EqualTo(1));
            Assert.That(set.Value, Is.EqualTo(5f));
        }

        [Test]
        public void FloatSet_ZeroOwnerAndSourceAreNotBulkReleaseTargets()
        {
            ESFloatValueChangeSet set = new ESFloatValueChangeSet();
            ESValueChangeToken token = set.Add(ESFloatValueChangeOp.Add, 2f);

            Assert.That(set.ReleaseAllByOwner(0), Is.EqualTo(0));
            Assert.That(set.ReleaseAllBySource(0), Is.EqualTo(0));
            Assert.That(set.Contains(token), Is.True);
        }

        [Test]
        public void EffectLease_RejectsRepeatedOrStaleRelease()
        {
            EffectLeaseOwner owner = new EffectLeaseOwner { activeGeneration = 3 };
            ESEffectLease first = new ESEffectLease(owner, 4, 3);
            ESEffectLease copied = first;

            Assert.That(first.TryRelease(), Is.True);
            Assert.That(copied.TryRelease(), Is.False);
            Assert.That(owner.releaseCount, Is.EqualTo(1));
        }

        [Test]
        public void FloatSet_OwnerAndSourceBulkReleaseRemainCorrectAfterSwapBack()
        {
            ESFloatValueChangeSet set = new ESFloatValueChangeSet();
            ESValueChangeToken ownerAndSource = set.Add(ESFloatValueChangeOp.Add, 1f, ownerId: 1, sourceId: 1);
            ESValueChangeToken ownerOnly = set.Add(ESFloatValueChangeOp.Add, 2f, ownerId: 1, sourceId: 2);
            ESValueChangeToken sourceOnly = set.Add(ESFloatValueChangeOp.Add, 4f, ownerId: 2, sourceId: 1);

            Assert.That(set.Release(ownerOnly), Is.True);
            Assert.That(set.ReleaseAllByOwner(1), Is.EqualTo(1));
            Assert.That(set.Contains(ownerAndSource), Is.False);
            Assert.That(set.Contains(sourceOnly), Is.True);
            Assert.That(set.ReleaseAllBySource(1), Is.EqualTo(1));
            Assert.That(set.Count, Is.EqualTo(0));
        }

        [Test]
        public void FloatSet_BulkReleaseKeepsReentrantOwnerRegistration()
        {
            ESFloatValueChangeSet set = new ESFloatValueChangeSet();
            const int ownerId = 7;
            set.Add(ESFloatValueChangeOp.Add, 1f, ownerId: ownerId);

            bool addedDuringNotification = false;
            set.Changed += _ =>
            {
                if (addedDuringNotification)
                    return;

                addedDuringNotification = true;
                set.Add(ESFloatValueChangeOp.Add, 2f, ownerId: ownerId);
            };

            Assert.That(set.ReleaseAllByOwner(ownerId), Is.EqualTo(1));
            Assert.That(set.Value, Is.EqualTo(2f));
            Assert.That(set.ReleaseAllByOwner(ownerId), Is.EqualTo(1));
            Assert.That(set.Value, Is.Zero);
        }

        [Test]
        public void FloatTracker_CannotRebindWhileOwningModifiers()
        {
            ESFloatValueChangeSet first = new ESFloatValueChangeSet();
            ESFloatValueChangeSet second = new ESFloatValueChangeSet();
            ESFloatValueChangeTracker tracker = new ESFloatValueChangeTracker(first);
            tracker.Add(ESFloatValueChangeOp.Add, 1f);

            Assert.That(tracker.TryBind(second), Is.False);
            Assert.Throws<InvalidOperationException>(() => tracker.Bind(second));

            Assert.That(tracker.ReleaseAll(), Is.EqualTo(1));
            Assert.That(tracker.TryBind(second), Is.True);
            tracker.Add(ESFloatValueChangeOp.Add, 3f);
            Assert.That(second.Value, Is.EqualTo(3f));
        }

        [Test]
        public void FloatSet_RevisionAndNotificationOnlyAdvanceForActualInputChanges()
        {
            ESFloatValueChangeSet set = new ESFloatValueChangeSet();
            int notificationCount = 0;
            set.Changed += _ => notificationCount++;

            ESValueChangeToken token = set.Add(ESFloatValueChangeOp.Add, 1f);
            int afterAdd = set.Revision;
            set.Update(token, 1f);
            set.SetEnabled(token, true);
            set.Update(token, 2f);

            Assert.That(set.Revision, Is.EqualTo(afterAdd + 1));
            Assert.That(notificationCount, Is.EqualTo(2));
        }

        [Test]
        public void PermitSet_AndStandaloneResolverAgreeOnHardAuthority()
        {
            ESPermitSet set = new ESPermitSet(fallbackValue: true);
            set.Add(ESPermitLaw.AllowEnable, priority: 100);
            set.Add(ESPermitLaw.HardDisable, priority: -100);

            ESPermitLawEntry[] entries =
            {
                new ESPermitLawEntry(ESPermitLaw.AllowEnable, 100, 1),
                new ESPermitLawEntry(ESPermitLaw.HardDisable, -100, 2)
            };

            Assert.That(set.Value, Is.False);
            Assert.That(ESPermitLawResolver.Resolve(entries, entries.Length, true), Is.False);
            Assert.That(set.Result.decision, Is.EqualTo(ESPermitLaw.HardDisable));
        }

        [Test]
        public void PermitSet_ClearInvalidatesExistingTokens()
        {
            ESPermitSet set = new ESPermitSet();
            ESValueChangeToken stale = set.Add(ESPermitLaw.HardDisable);

            set.Clear();
            ESValueChangeToken current = set.Add(ESPermitLaw.HardEnable);

            Assert.That(current.tokenId, Is.EqualTo(stale.tokenId));
            Assert.That(current.tokenVersion, Is.Not.EqualTo(stale.tokenVersion));
            Assert.That(set.Update(stale, ESPermitLaw.HardDisable), Is.False);
            Assert.That(set.Value, Is.True);
        }
    }
}
