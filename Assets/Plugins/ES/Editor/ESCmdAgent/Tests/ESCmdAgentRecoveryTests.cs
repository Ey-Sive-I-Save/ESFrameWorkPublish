using NUnit.Framework;

namespace ES.Tests
{
    public sealed class ESCmdAgentRecoveryTests
    {
        [Test]
        public void MessageRecovery_UsesExactMessageIdWhenItExists()
        {
            const string json = "{\"messages\":[{\"messageId\":\"message-a\",\"idempotencyKey\":\"key-a\",\"targetRecordId\":\"record-a\"}]}";

            bool selected = ESCmdAgentWindow.TrySelectManagedMessageRecordForTests(json, "message-a",
                out string matched, out string error);

            Assert.That(selected, Is.True, error);
            Assert.That(matched, Is.EqualTo("message-a"));
        }

        [Test]
        public void MessageRecovery_UsesIdempotencyKeyOnlyWithOneExactRecordMatch()
        {
            const string json = "{\"messages\":[{\"messageId\":\"message-a\",\"idempotencyKey\":\"key-a\",\"targetRecordId\":\"record-a\"},{\"messageId\":\"message-b\",\"idempotencyKey\":\"key-a\",\"targetRecordId\":\"record-b\"}]}";

            bool selected = ESCmdAgentWindow.TrySelectManagedMessageByIdempotencyForTests(json,
                "key-a", "record-a", out string matched, out string error);

            Assert.That(selected, Is.True, error);
            Assert.That(matched, Is.EqualTo("message-a"));
        }

        [Test]
        public void MessageRecovery_RejectsAmbiguousIdempotencyKey()
        {
            const string json = "{\"messages\":[{\"messageId\":\"message-a\",\"idempotencyKey\":\"key-a\",\"targetRecordId\":\"record-a\"},{\"messageId\":\"message-b\",\"idempotencyKey\":\"key-a\",\"targetRecordId\":\"record-a\"}]}";

            bool selected = ESCmdAgentWindow.TrySelectManagedMessageByIdempotencyForTests(json,
                "key-a", "record-a", out _, out string error);

            Assert.That(selected, Is.False);
            StringAssert.Contains("多个", error);
        }

        [TestCase("New", "LaunchNew")]
        [TestCase("Resume", "Resume")]
        [TestCase("SendMessage", "SendMessage")]
        [TestCase("Status", "RefreshStatus")]
        [TestCase("MessageStatus", "RefreshMessageStatus")]
        [TestCase("PrepareExternalClaim", "PrepareExternalClaim")]
        [TestCase("SubmitExternalClaimInput", "SubmitExternalClaimInput")]
        [TestCase("FinalizeExternalClaim", "FinalizeExternalClaim")]
        [TestCase("CancelExternalClaim", "CancelExternalClaim")]
        public void PersistedOperationMode_UsesExplicitRecoveryKind(string mode, string expectedKind)
        {
            Assert.That(ESCmdAgentWindow.GetManagedRecoveryModeForTests(mode), Is.EqualTo(expectedKind));
        }

        [Test]
        public void MessageRecovery_RejectsIdempotencyMatchFromAnotherRecord()
        {
            const string json = "{\"messages\":[{\"messageId\":\"message-a\",\"idempotencyKey\":\"key-a\",\"targetRecordId\":\"record-b\"}]}";

            bool selected = ESCmdAgentWindow.TrySelectManagedMessageByIdempotencyForTests(json,
                "key-a", "record-a", out _, out string error);

            Assert.That(selected, Is.False);
            StringAssert.Contains("未返回", error);
        }

        [Test]
        public void StatusRecovery_PreservesOnlyACompleteAcceptedIdentityWhenRegistryObservationIsEmpty()
        {
            Assert.That(ESCmdAgentWindow.ShouldPreserveAcceptedIdentityForTests(true,
                "session-a", "record-a", "C:/receipts/session-a.json"), Is.True);
            Assert.That(ESCmdAgentWindow.ShouldPreserveAcceptedIdentityForTests(true,
                "session-a", string.Empty, "C:/receipts/session-a.json"), Is.False);
            Assert.That(ESCmdAgentWindow.ShouldPreserveAcceptedIdentityForTests(false,
                "session-a", "record-a", "C:/receipts/session-a.json"), Is.False);
        }

        [Test]
        public void BackgroundRecovery_DoesNotPollLegacyTaskKeysWithoutARecoverableOperation()
        {
            Assert.That(ESCmdAgentWindow.ShouldBackgroundRefreshForTests(string.Empty, false,
                false, false, false), Is.False);
            Assert.That(ESCmdAgentWindow.ShouldBackgroundRefreshForTests("session-a", false,
                false, false, false), Is.True);
            Assert.That(ESCmdAgentWindow.ShouldBackgroundRefreshForTests(string.Empty, true,
                false, false, false), Is.True);
        }

        [Test]
        public void RegistryObservation_StopsAutomaticPollingAfterBoundedRetries()
        {
            Assert.That(ESCmdAgentWindow.ShouldPauseRegistryObservationForTests(2), Is.False);
            Assert.That(ESCmdAgentWindow.ShouldPauseRegistryObservationForTests(3), Is.True);
            Assert.That(ESCmdAgentWindow.ShouldBackgroundRefreshForTests("session-a", false,
                false, false, false, true), Is.False);
            Assert.That(ESCmdAgentWindow.ShouldBackgroundRefreshForTests(string.Empty, true,
                false, false, false, true), Is.True);
        }

        [Test]
        public void OperationDirectoryRecovery_RequiresTheOriginalLocalTabIdentity()
        {
            Assert.That(ESCmdAgentWindow.DoesPersistedOperationMatchLocalSessionForTests(
                "local-a", "local-a", "session-a", "session-a", "record-a", "record-a"), Is.True);
            Assert.That(ESCmdAgentWindow.DoesPersistedOperationMatchLocalSessionForTests(
                "local-a", "local-b", "session-a", "session-a", "record-a", "record-a"), Is.False);
            Assert.That(ESCmdAgentWindow.DoesPersistedOperationMatchLocalSessionForTests(
                "local-a", "local-a", "session-b", "session-a", "record-a", "record-a"), Is.False);
        }
    }
}
