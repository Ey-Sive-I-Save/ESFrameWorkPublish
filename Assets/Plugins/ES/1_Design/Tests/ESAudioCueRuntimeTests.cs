using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Type = System.Type;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESAudioCueRuntimeTests
    {
        [Test]
        public void TryValidate_DoesNotAdvanceUnityRandomState()
        {
            ESAudioCueInfo cue = ScriptableObject.CreateInstance<ESAudioCueInfo>();
            try
            {
                cue.key.stringKey = "tests.audio.validate";
                cue.variants.Add(new ESAudioCueVariant
                {
                    clipKey = new ESAssetReferAudioClipConfigKey { stringKey = "tests.audio.clip" }
                });

                Random.InitState(712367);
                Random.State before = Random.state;

                Assert.That(cue.TryValidate(out string error), Is.True, error);
                Assert.That(Random.state.Equals(before), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void TryValidate_RejectsNonFiniteAudioParameters()
        {
            ESAudioCueInfo cue = ScriptableObject.CreateInstance<ESAudioCueInfo>();
            try
            {
                cue.key.stringKey = "tests.audio.finite";
                cue.variants.Add(new ESAudioCueVariant
                {
                    clipKey = new ESAssetReferAudioClipConfigKey { stringKey = "tests.audio.clip" }
                });

                cue.cooldownSeconds = float.NaN;
                Assert.That(cue.TryValidate(out _), Is.False);

                cue.cooldownSeconds = 0f;
                cue.randomPitch = new Vector2(1f, float.PositiveInfinity);
                Assert.That(cue.TryValidate(out _), Is.False);

                cue.randomPitch = Vector2.one;
                cue.minDistance = float.NegativeInfinity;
                Assert.That(cue.TryValidate(out _), Is.False);

                cue.minDistance = 1f;
                cue.variants[0].weight = float.PositiveInfinity;
                Assert.That(cue.TryValidate(out _), Is.False);

                cue.variants[0].weight = 1f;
                cue.spatialSettings.enableDoppler = true;
                cue.spatialSettings.dopplerLevel = float.NaN;
                Assert.That(cue.TryValidate(out _), Is.False);

                cue.spatialSettings.dopplerLevel = 1f;
                cue.playbackStartSeconds = float.NaN;
                Assert.That(cue.TryValidate(out _), Is.False);

                cue.playbackStartSeconds = 0f;
                cue.usePlaybackWindow = true;
                cue.playbackStartSeconds = 2f;
                cue.playbackEndSeconds = 1f;
                Assert.That(cue.TryValidate(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void PlaybackWindow_IsDisabledByDefault_AndValidatedWhenEnabled()
        {
            ESAudioCueInfo cue = ScriptableObject.CreateInstance<ESAudioCueInfo>();
            try
            {
                cue.key.stringKey = "tests.audio.playback-window";
                cue.variants.Add(new ESAudioCueVariant
                {
                    clipKey = new ESAssetReferAudioClipConfigKey { stringKey = "tests.audio.clip" }
                });
                cue.playbackStartSeconds = 2f;
                cue.playbackEndSeconds = 1f;

                Assert.That(cue.usePlaybackWindow, Is.False);
                Assert.That(cue.TryValidate(out string disabledError), Is.True, disabledError);

                cue.usePlaybackWindow = true;
                Assert.That(cue.TryValidate(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void DirectClipPlayConfig_UsesEntryDefaultsUntilExplicitlyOverridden_AndRejectsInvalidValues()
        {
            var config = new ESAudioClipPlayConfig();

            Assert.That(config.category, Is.EqualTo(ESAudioCategory.Sfx));
            Assert.That(config.spatialMode, Is.EqualTo(ESAudioSpatialMode.TwoD));
            Assert.That(config.overrideCategory, Is.False);
            Assert.That(config.overrideSpatialMode, Is.False);
            Assert.That(config.TryValidate(out string validError), Is.True, validError);
            Assert.That(config.GetCategory(ESAudioCategory.UI), Is.EqualTo(ESAudioCategory.UI));
            Assert.That(config.GetSpatialMode(ESAudioSpatialMode.ThreeD), Is.EqualTo(ESAudioSpatialMode.ThreeD));

            config.overrideCategory = true;
            config.category = ESAudioCategory.UI;
            config.overrideSpatialMode = true;
            config.spatialMode = ESAudioSpatialMode.ThreeD;
            Assert.That(config.TryValidate(out validError), Is.True, validError);
            Assert.That(config.GetCategory(ESAudioCategory.Sfx), Is.EqualTo(ESAudioCategory.UI));
            Assert.That(config.GetSpatialMode(ESAudioSpatialMode.TwoD), Is.EqualTo(ESAudioSpatialMode.ThreeD));

            config.pitch = float.NaN;
            Assert.That(config.TryValidate(out _), Is.False);

            config.pitch = 1f;
            config.maxDistance = float.PositiveInfinity;
            Assert.That(config.TryValidate(out _), Is.False);

            config.maxDistance = 30f;
            config.priority = 257;
            Assert.That(config.TryValidate(out _), Is.False);
        }

        [Test]
        public void ProviderTransitionCleanup_RetainsDirectClipVoices()
        {
            var module = new ESAudioModule();
            ESAudioCueInfo cue = ScriptableObject.CreateInstance<ESAudioCueInfo>();
            try
            {
                object directVoice = CreateActiveVoice(module, 1, 0, ESAudioCategory.Sfx);
                object cueVoice = CreateActiveVoice(module, 2, 101, ESAudioCategory.Sfx);
                ESAudioVoiceHandle cueHandle = (ESAudioVoiceHandle)CreateVoiceHandle(2, 1);
                Type voiceType = cueVoice.GetType();
                SetVoiceField(voiceType, cueVoice, "sourceConfig", cue);

                Assert.That(InvokeHasCueVoices(module), Is.True);
                InvokeStopCueVoices(module, ESAudioVoiceEndReason.ProviderTransition);

                IList voices = GetVoices(module);
                Assert.That(voices.Count, Is.EqualTo(1));
                Assert.That(voices[0], Is.SameAs(directVoice));
                Assert.That(module.TryGetVoiceStatus(cueHandle, out ESAudioVoiceStatus status), Is.True);
                Assert.That(status.EndReason, Is.EqualTo(ESAudioVoiceEndReason.ProviderTransition));
            }
            finally
            {
                Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void ProviderTransitionCleanup_CancelsPendingCueAdmissions()
        {
            var module = new ESAudioModule();
            ESAudioCueInfo cue = ScriptableObject.CreateInstance<ESAudioCueInfo>();
            try
            {
                object directVoice = CreateActiveVoice(module, 1, 0, ESAudioCategory.Sfx);
                object pendingVoice = CreatePooledVoice(module, 2, 201, ESAudioCategory.Sfx, 128);
                ESAudioVoiceHandle pendingHandle = (ESAudioVoiceHandle)CreateVoiceHandle(2, 1);
                SetVoiceField(pendingVoice.GetType(), pendingVoice, "sourceConfig", cue);
                object pendingAdmission = RentAdmission(module, pendingVoice, true, true, null);
                GetPendingAdmissions(module).Add(pendingAdmission);

                Assert.That(InvokeHasCueVoices(module), Is.True);
                InvokeStopCueVoices(module, ESAudioVoiceEndReason.ProviderTransition);

                Assert.That(GetPendingAdmissions(module).Count, Is.Zero);
                Assert.That(GetVoices(module).Count, Is.EqualTo(1));
                Assert.That(GetVoices(module)[0], Is.SameAs(directVoice));
                Assert.That(module.TryGetVoiceStatus(pendingHandle, out ESAudioVoiceStatus status), Is.True);
                Assert.That(status.EndReason, Is.EqualTo(ESAudioVoiceEndReason.ProviderTransition));
            }
            finally
            {
                Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void CueVoice_BorrowsPlanIdentity_WithoutOwningAnAudioLocalScope()
        {
            Type voiceType = typeof(ESAudioModule).GetNestedType("Voice", BindingFlags.NonPublic);
            Assert.That(voiceType, Is.Not.Null);
            Assert.That(voiceType.GetField("clipIdentity", BindingFlags.Instance | BindingFlags.Public), Is.Not.Null);
            Assert.That(voiceType.GetField("scope", BindingFlags.Instance | BindingFlags.Public), Is.Null);
        }

        [Test]
        public void ResourceOwnerEnding_StopsOnlyCueVoicesBorrowingThatIdentity()
        {
            var module = new ESAudioModule();
            ESAudioCueInfo cue = ScriptableObject.CreateInstance<ESAudioCueInfo>();
            try
            {
                object borrowedCueVoice = CreateActiveVoice(module, 1, 101, ESAudioCategory.Ambient);
                object otherCueVoice = CreateActiveVoice(module, 2, 102, ESAudioCategory.Ambient);
                object directVoice = CreateActiveVoice(module, 3, 0, ESAudioCategory.Sfx);
                SetVoiceField(borrowedCueVoice.GetType(), borrowedCueVoice, "sourceConfig", cue);
                SetVoiceField(otherCueVoice.GetType(), otherCueVoice, "sourceConfig", cue);
                SetVoiceField(borrowedCueVoice.GetType(), borrowedCueVoice, "clipIdentity", new ESAssetIdentity("borrowed-clip"));
                SetVoiceField(otherCueVoice.GetType(), otherCueVoice, "clipIdentity", new ESAssetIdentity("other-clip"));

                InvokeActivePlanAssetOwnershipEnding(module, new ESAssetIdentity("borrowed-clip"));

                IList voices = GetVoices(module);
                Assert.That(voices.Count, Is.EqualTo(2));
                Assert.That(voices, Does.Contain(otherCueVoice));
                Assert.That(voices, Does.Contain(directVoice));
                Assert.That(module.TryGetVoiceStatus((ESAudioVoiceHandle)CreateVoiceHandle(1, 1), out ESAudioVoiceStatus status), Is.True);
                Assert.That(status.State, Is.EqualTo(ESAudioVoiceState.Ended));
                Assert.That(status.EndReason, Is.EqualTo(ESAudioVoiceEndReason.ResourceOwnerReleased));
            }
            finally
            {
                Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void DestroyedLifecycleOwner_EndsVoiceEvenWhenItDoesNotFollowOwner()
        {
            var module = new ESAudioModule();
            GameObject owner = new GameObject("AudioOwnerTest");
            try
            {
                object voice = CreateActiveVoice(module, 1, 0, ESAudioCategory.Sfx);
                Type voiceType = voice.GetType();
                SetVoiceField(voiceType, voice, "owner", owner.transform);
                SetVoiceField(voiceType, voice, "hasLifecycleOwner", true);
                SetVoiceField(voiceType, voice, "followOwner", false);

                Object.DestroyImmediate(owner);
                InvokeAudioUpdate(module);

                Assert.That(GetVoices(module).Count, Is.Zero);
                Assert.That(module.TryGetVoiceStatus(CreateVoiceHandle(1, 1), out ESAudioVoiceStatus status), Is.True);
                Assert.That(status.State, Is.EqualTo(ESAudioVoiceState.Ended));
                Assert.That(status.EndReason, Is.EqualTo(ESAudioVoiceEndReason.OwnerDestroyed));
            }
            finally
            {
                if (owner != null)
                    Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void DestroyedLifecycleOwner_CancelsPendingAdmission()
        {
            var module = new ESAudioModule();
            GameObject owner = new GameObject("PendingAudioOwnerTest");
            try
            {
                object pendingVoice = CreatePooledVoice(module, 7, 0, ESAudioCategory.Sfx, 128);
                Type voiceType = pendingVoice.GetType();
                SetVoiceField(voiceType, pendingVoice, "owner", owner.transform);
                SetVoiceField(voiceType, pendingVoice, "hasLifecycleOwner", true);
                object admission = RentAdmission(module, pendingVoice, true, true, null);
                GetPendingAdmissions(module).Add(admission);
                ESAudioVoiceHandle handle = (ESAudioVoiceHandle)CreateVoiceHandle(7, 1);

                Assert.That(module.TryGetVoiceStatus(handle, out ESAudioVoiceStatus pending), Is.True);
                Assert.That(pending.State, Is.EqualTo(ESAudioVoiceState.PendingLoad));

                Object.DestroyImmediate(owner);
                InvokeAudioUpdate(module);

                Assert.That(GetPendingAdmissions(module).Count, Is.Zero);
                Assert.That(module.TryGetVoiceStatus(handle, out ESAudioVoiceStatus ended), Is.True);
                Assert.That(ended.EndReason, Is.EqualTo(ESAudioVoiceEndReason.OwnerDestroyed));
            }
            finally
            {
                if (owner != null)
                    Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AdmissionStartupFailure_DoesNotCommitPlannedVictims()
        {
            var module = new ESAudioModule { maxVoices = 1 };
            object victim = CreateActiveVoice(module, 1, 0, ESAudioCategory.Sfx);
            object incoming = CreatePooledVoice(module, 2, 0, ESAudioCategory.Sfx, 256);
            object admission = RentAdmission(module, incoming, false, true, null);

            bool started = InvokeTryAdmitPreparedVoice(module, admission, out string error);

            Assert.That(started, Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(GetVoices(module).Count, Is.EqualTo(1));
            Assert.That(GetVoices(module)[0], Is.SameAs(victim));
            InvokeDiscardUnstartedVoice(module, incoming);
            InvokeCompleteAdmission(module, admission);
        }

        [Test]
        public void AdmissionPlanning_UsesIgnoredVoiceAsTransientMusicSlot()
        {
            var module = new ESAudioModule
            {
                maxVoices = 1,
                categoryBudgets = new System.Collections.Generic.List<ESAudioCategoryVoiceBudget>
                {
                    new ESAudioCategoryVoiceBudget { category = ESAudioCategory.Music, maxVoices = 1 }
                }
            };
            ESAudioCueInfo cue = ScriptableObject.CreateInstance<ESAudioCueInfo>();
            try
            {
                cue.category = ESAudioCategory.Music;
                cue.maxConcurrent = 1;
                object outgoingVoice = CreateActiveVoice(module, 1, 101, ESAudioCategory.Music);
                SetVoiceField(outgoingVoice.GetType(), outgoingVoice, "sourceConfig", cue);
                object incoming = CreatePooledVoice(module, 2, 101, ESAudioCategory.Music, 128);
                SetVoiceField(incoming.GetType(), incoming, "sourceConfig", cue);
                object admission = RentAdmission(module, incoming, true, false, outgoingVoice);

                Assert.That(InvokeTryPlanAdmission(module, admission, out string error), Is.True, error);
                Assert.That(GetVoices(module).Count, Is.EqualTo(1));
                Assert.That(GetReservedVictimCount(admission), Is.Zero);

                InvokeDiscardUnstartedVoice(module, incoming);
                InvokeCompleteAdmission(module, admission);
            }
            finally
            {
                Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void MusicAdmission_CanPreemptNormalVoiceWhenNoOutgoingTrackExists()
        {
            var module = new ESAudioModule { maxVoices = 1 };
            ESAudioCueInfo cue = ScriptableObject.CreateInstance<ESAudioCueInfo>();
            try
            {
                cue.category = ESAudioCategory.Music;
                cue.priority = 256;
                object normalVoice = CreateActiveVoice(module, 1, 0, ESAudioCategory.Sfx);
                SetVoiceField(normalVoice.GetType(), normalVoice, "priority", 128);
                object incoming = CreatePooledVoice(module, 2, 301, ESAudioCategory.Music, 256);
                SetVoiceField(incoming.GetType(), incoming, "sourceConfig", cue);
                object admission = RentAdmission(module, incoming, true, true, null);

                Assert.That(InvokeTryPlanAdmission(module, admission, out string error), Is.True, error);
                Assert.That(GetReservedVictimCount(admission), Is.EqualTo(1));
                Assert.That(GetVoices(module).Count, Is.EqualTo(1));

                InvokeDiscardUnstartedVoice(module, incoming);
                InvokeCompleteAdmission(module, admission);
            }
            finally
            {
                Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void AdmissionPlanning_RejectsRatherThanExpandsASeverelyOverBudgetPool()
        {
            var module = new ESAudioModule { maxVoices = 1 };
            object first = CreateActiveVoice(module, 1, 0, ESAudioCategory.Sfx);
            object second = CreateActiveVoice(module, 2, 0, ESAudioCategory.Sfx);
            object third = CreateActiveVoice(module, 3, 0, ESAudioCategory.Sfx);
            SetVoiceField(first.GetType(), first, "priority", 1);
            SetVoiceField(second.GetType(), second, "priority", 1);
            SetVoiceField(third.GetType(), third, "priority", 1);
            object incoming = CreatePooledVoice(module, 4, 0, ESAudioCategory.Sfx, 256);
            object admission = RentAdmission(module, incoming, false, true, null);

            Assert.That(InvokeTryPlanAdmission(module, admission, out string error), Is.False);
            Assert.That(error, Does.Contain("remains over limit"));
            Assert.That(GetVoices(module).Count, Is.EqualTo(3));

            InvokeDiscardUnstartedVoice(module, incoming);
            InvokeCompleteAdmission(module, admission);
        }

        [Test]
        public void MusicTransitionReservation_ProtectsBothSidesOfActiveCrossfade()
        {
            var module = new ESAudioModule { maxVoices = 2 };
            ESAudioCueInfo cue = ScriptableObject.CreateInstance<ESAudioCueInfo>();
            try
            {
                cue.category = ESAudioCategory.Sfx;
                object currentMusic = CreateActiveVoice(module, 1, 101, ESAudioCategory.Music);
                object fadingMusic = CreateActiveVoice(module, 2, 102, ESAudioCategory.Music);
                SetModuleField(module, "currentMusicHandle", CreateVoiceHandle(1, 1));
                SetModuleField(module, "fadingOutMusicHandle", CreateVoiceHandle(2, 1));
                object incoming = CreatePooledVoice(module, 3, 201, ESAudioCategory.Sfx, 256);
                object admission = RentAdmission(module, incoming, false, true, null);

                Assert.That(IsTransitionReservedMusicVoice(module, currentMusic), Is.True);
                Assert.That(IsTransitionReservedMusicVoice(module, fadingMusic), Is.True);
                Assert.That(InvokeTryPlanAdmission(module, admission, out string error), Is.False);
                Assert.That(error, Is.Not.Empty);
                Assert.That(GetVoices(module).Count, Is.EqualTo(2));

                InvokeDiscardUnstartedVoice(module, incoming);
                InvokeCompleteAdmission(module, admission);
            }
            finally
            {
                Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void UserSettings_PersistDbGainsAndSeparateMuteState()
        {
            var module = new ESAudioModule();
            module.SetMasterVolumeDb(-12f);
            module.SetMasterMuted(true);
            module.SetCategoryVolumeDb(ESAudioCategory.Sfx, -24f);
            module.SetCategoryMuted(ESAudioCategory.Sfx, true);

            var snapshot = new ESAudioUserSettings();
            module.CopyUserSettings(snapshot);
            ESAudioCategoryUserSetting sfx = snapshot.categorySettings.Find(entry => entry.category == ESAudioCategory.Sfx);

            Assert.That(snapshot.masterVolumeDb, Is.EqualTo(-12f).Within(0.001f));
            Assert.That(snapshot.masterMuted, Is.True);
            Assert.That(sfx, Is.Not.Null);
            Assert.That(sfx.volumeDb, Is.EqualTo(-24f).Within(0.001f));
            Assert.That(sfx.muted, Is.True);

            var restoredModule = new ESAudioModule();
            restoredModule.ApplyUserSettings(snapshot);

            Assert.That(restoredModule.GetMasterVolumeDb(), Is.EqualTo(-12f).Within(0.001f));
            Assert.That(restoredModule.IsMuted, Is.True);
            Assert.That(restoredModule.GetCategoryVolumeDb(ESAudioCategory.Sfx), Is.EqualTo(-24f).Within(0.001f));
            Assert.That(restoredModule.IsCategoryMuted(ESAudioCategory.Sfx), Is.True);
            Assert.That(ESAudioModule.DbToLinear(-80f), Is.Zero);
            Assert.That(ESAudioModule.LinearToDb(0f), Is.EqualTo(-80f));
        }

        [Test]
        public void GameCoreTable_Clear_ReleasesCueSourceAndRetainsShell()
        {
            var table = new ESAudioCueConfigKeyTable(2);
            var key = new ESAudioCueKey { stringKey = "tests.audio.retained" };
            ESAudioCueRuntimeData data = table.AcquireRetained(key);
            ESAudioCueInfo cue = ScriptableObject.CreateInstance<ESAudioCueInfo>();
            try
            {
                data.source = cue;
                data.keyName = key.StringKey;
                Assert.That(table.CommitRetained(key, data, "Audio Cue Test"), Is.GreaterThan(0));
                Assert.That(data.Ready, Is.True);

                table.BeginBuild(clear: true);
                table.EndBuild();

                Assert.That(data.Ready, Is.False);
                Assert.That(data.source, Is.Null);
                Assert.That(table.AcquireRetained(key), Is.SameAs(data));
            }
            finally
            {
                Object.DestroyImmediate(cue);
            }
        }

        [Test]
        public void AdmissionReservation_IsFixedDeduplicatedAndCapped()
        {
            var module = new ESAudioModule();
            object incoming = CreatePooledVoice(module, 1, 0, ESAudioCategory.Sfx, 128);
            object first = CreateActiveVoice(module, 2, 0, ESAudioCategory.Sfx);
            object second = CreateActiveVoice(module, 3, 0, ESAudioCategory.Sfx);
            object third = CreateActiveVoice(module, 4, 0, ESAudioCategory.Sfx);
            object fourth = CreateActiveVoice(module, 5, 0, ESAudioCategory.Sfx);
            object admission = RentAdmission(module, incoming, false, true, null);

            Assert.That(InvokeTryReserveVictim(admission, first), Is.True);
            Assert.That(InvokeTryReserveVictim(admission, first), Is.True);
            Assert.That(InvokeTryReserveVictim(admission, second), Is.True);
            Assert.That(InvokeTryReserveVictim(admission, third), Is.True);
            Assert.That(InvokeTryReserveVictim(admission, fourth), Is.False);
            Assert.That(GetReservedVictimCount(admission), Is.EqualTo(3));

            InvokeDiscardUnstartedVoice(module, incoming);
            InvokeCompleteAdmission(module, admission);
        }

        [Test]
        public void CancelledAdmission_IsNoLongerDiscoverableByHandle()
        {
            var module = new ESAudioModule();
            object voice = CreatePooledVoice(module, 17, 0, ESAudioCategory.Sfx, 128);
            object admission = RentAdmission(module, voice, true, true, null);
            GetPendingAdmissions(module).Add(admission);
            object handle = CreateVoiceHandle(17, 1);

            Assert.That(InvokeTryGetAdmission(module, handle), Is.True);
            InvokeCancelAdmission(module, admission, ESAudioVoiceEndReason.ExplicitStop);
            Assert.That(InvokeTryGetAdmission(module, handle), Is.False);
            Assert.That(GetPendingAdmissions(module).Count, Is.Zero);
            Assert.That(module.TryGetVoiceStatus((ESAudioVoiceHandle)handle, out ESAudioVoiceStatus status), Is.True);
            Assert.That(status.State, Is.EqualTo(ESAudioVoiceState.Ended));
            Assert.That(status.EndReason, Is.EqualTo(ESAudioVoiceEndReason.ExplicitStop));
        }

        [Test]
        public void VoiceStatus_ReportsStableLiveStatesAndTerminalReason()
        {
            var module = new ESAudioModule();
            object voice = CreateActiveVoice(module, 41, 0, ESAudioCategory.Sfx);
            ESAudioVoiceHandle handle = (ESAudioVoiceHandle)CreateVoiceHandle(41, 1);

            Assert.That(module.TryGetVoiceStatus(handle, out ESAudioVoiceStatus playing), Is.True);
            Assert.That(playing.State, Is.EqualTo(ESAudioVoiceState.Playing));
            Assert.That(playing.EndReason, Is.EqualTo(ESAudioVoiceEndReason.None));

            SetVoiceField(voice.GetType(), voice, "fadeOutEndTime", 1f);
            Assert.That(module.TryGetVoiceStatus(handle, out ESAudioVoiceStatus stopping), Is.True);
            Assert.That(stopping.State, Is.EqualTo(ESAudioVoiceState.Stopping));

            InvokeEndVoice(module, voice, ESAudioVoiceEndReason.Preempted);
            Assert.That(module.TryGetVoiceStatus(handle, out ESAudioVoiceStatus ended), Is.True);
            Assert.That(ended.State, Is.EqualTo(ESAudioVoiceState.Ended));
            Assert.That(ended.EndReason, Is.EqualTo(ESAudioVoiceEndReason.Preempted));
            Assert.That(ended.FailureCode, Is.EqualTo(ESAudioFailureCode.VoicePreempted));
        }

        [Test]
        public void VoiceStatus_StopWithFadeReportsStoppingBeforeTermination()
        {
            var module = new ESAudioModule();
            object voice = CreateActiveVoice(module, 46, 0, ESAudioCategory.Sfx);
            ESAudioVoiceHandle handle = (ESAudioVoiceHandle)CreateVoiceHandle(46, 1);
            GameObject emitter = new GameObject("AudioStatusFadeTest");
            try
            {
                SetVoiceField(voice.GetType(), voice, "source", emitter.AddComponent<AudioSource>());

                Assert.That(module.Stop(handle, 0.5f), Is.True);
                Assert.That(module.TryGetVoiceStatus(handle, out ESAudioVoiceStatus status), Is.True);
                Assert.That(status.State, Is.EqualTo(ESAudioVoiceState.Stopping));

                SetVoiceField(voice.GetType(), voice, "source", null);
                InvokeEndVoice(module, voice, ESAudioVoiceEndReason.ExplicitStop);
            }
            finally
            {
                Object.DestroyImmediate(emitter);
            }
        }

        [Test]
        public void VoiceStatus_TerminalHistorySeparatesHandleGenerations()
        {
            var module = new ESAudioModule();
            object firstVoice = CreateActiveVoice(module, 61, 0, ESAudioCategory.Sfx);
            ESAudioVoiceHandle firstHandle = (ESAudioVoiceHandle)CreateVoiceHandle(61, 1);
            InvokeEndVoice(module, firstVoice, ESAudioVoiceEndReason.NaturalEnd);

            object reusedVoice = CreateActiveVoice(module, 61, 0, ESAudioCategory.Sfx);
            SetVoiceField(reusedVoice.GetType(), reusedVoice, "generation", 2);
            ESAudioVoiceHandle reusedHandle = (ESAudioVoiceHandle)CreateVoiceHandle(61, 2);

            Assert.That(module.TryGetVoiceStatus(firstHandle, out ESAudioVoiceStatus firstStatus), Is.True);
            Assert.That(firstStatus.EndReason, Is.EqualTo(ESAudioVoiceEndReason.NaturalEnd));
            Assert.That(module.TryGetVoiceStatus(reusedHandle, out ESAudioVoiceStatus reusedStatus), Is.True);
            Assert.That(reusedStatus.State, Is.EqualTo(ESAudioVoiceState.Playing));

            InvokeEndVoice(module, reusedVoice, ESAudioVoiceEndReason.ExplicitStop);
            Assert.That(module.TryGetVoiceStatus(reusedHandle, out ESAudioVoiceStatus endedReused), Is.True);
            Assert.That(endedReused.EndReason, Is.EqualTo(ESAudioVoiceEndReason.ExplicitStop));
        }

        [Test]
        public void VoiceStatus_TerminalHistoryUsesGenerationAndExpiresWhenOverwritten()
        {
            var module = new ESAudioModule();
            ESAudioVoiceHandle firstHandle = default;
            ESAudioVoiceHandle latestHandle = default;

            for (int id = 1; id <= 129; id++)
            {
                object voice = CreateActiveVoice(module, id, 0, ESAudioCategory.Sfx);
                ESAudioVoiceHandle handle = (ESAudioVoiceHandle)CreateVoiceHandle(id, 1);
                if (id == 1)
                    firstHandle = handle;
                latestHandle = handle;
                InvokeEndVoice(module, voice, ESAudioVoiceEndReason.NaturalEnd);
            }

            Assert.That(module.TryGetVoiceStatus(firstHandle, out _), Is.False);
            Assert.That(module.TryGetVoiceStatus(latestHandle, out ESAudioVoiceStatus latest), Is.True);
            Assert.That(latest.State, Is.EqualTo(ESAudioVoiceState.Ended));
            Assert.That(latest.EndReason, Is.EqualTo(ESAudioVoiceEndReason.NaturalEnd));
        }

        [Test]
        public void VoiceStatus_AsyncFailureRetainsTheFailureReasonBeforeVoiceIsReturned()
        {
            var module = new ESAudioModule();
            object voice = CreatePooledVoice(module, 52, 0, ESAudioCategory.Sfx, 128);
            object admission = RentAdmission(module, voice, true, true, null);
            GetPendingAdmissions(module).Add(admission);
            ESAudioVoiceHandle handle = (ESAudioVoiceHandle)CreateVoiceHandle(52, 1);

            InvokeFailAdmission(
                module,
                admission,
                ESAudioVoiceEndReason.BackendFailure,
                ESAudioFailureCode.SourceConfigurationFailed,
                "test failure");

            Assert.That(module.TryGetVoiceStatus(handle, out ESAudioVoiceStatus status), Is.True);
            Assert.That(status.State, Is.EqualTo(ESAudioVoiceState.Ended));
            Assert.That(status.EndReason, Is.EqualTo(ESAudioVoiceEndReason.BackendFailure));
            Assert.That(status.FailureCode, Is.EqualTo(ESAudioFailureCode.SourceConfigurationFailed));
        }

        [Test]
        public void AudioDiagnosticText_ProvidesChinesePresentationWithoutChangingMachineCodes()
        {
            Assert.That(ESAudioDiagnosticText.GetChineseState(ESAudioVoiceState.PendingLoad), Is.EqualTo("等待资源加载"));
            Assert.That(ESAudioDiagnosticText.GetChineseState(ESAudioVoiceState.Ended), Is.EqualTo("已结束"));
            Assert.That(ESAudioDiagnosticText.GetChineseEndReason(ESAudioVoiceEndReason.Preempted), Is.EqualTo("被高优先级 Voice 抢占"));
            Assert.That(ESAudioDiagnosticText.GetChineseFailure(ESAudioFailureCode.CooldownActive), Is.EqualTo("Cue 仍在冷却中"));
            Assert.That(ESAudioDiagnosticText.GetChineseFailure(ESAudioFailureCode.AutoPlayQueueCapacityExceeded), Is.EqualTo("OnEnable 自动播放队列超出容量"));
            Assert.That(ESAudioDiagnosticText.GetChineseFailure(ESAudioFailureCode.CueClipNotPrewarmed), Is.EqualTo("Cue 的 Clip 未由当前 ResourcePlan 预热并持有"));
        }

        [Test]
        public void BoundEmitterSnapshot_ExplicitlyClearsAPlaybackCustomRolloffCurve_WhenAuthoringHadNone()
        {
            GameObject emitter = new GameObject("BoundEmitterRolloffSnapshotTest");
            try
            {
                AudioSource source = emitter.AddComponent<AudioSource>();
                source.rolloffMode = AudioRolloffMode.Custom;
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, null);

                object snapshot = InvokeCaptureAudioSourceSnapshot(source);
                source.SetCustomCurve(
                    AudioSourceCurveType.CustomRolloff,
                    AnimationCurve.Linear(0f, 1f, 1f, 0f));

                Assert.That(source.GetCustomCurve(AudioSourceCurveType.CustomRolloff), Is.Not.Null);
                InvokeRestoreBoundEmitter(source, snapshot);
                Assert.That(source.GetCustomCurve(AudioSourceCurveType.CustomRolloff), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(emitter);
            }
        }

        [Test]
        public void LegacyAudioSourceCommands_AreNotFormalTypeRegistryEntries()
        {
            Type legacyPlay = typeof(ESCommand).Assembly.GetType("ES.ESCommand_AudioSource_Play", true);
            Type legacyStop = typeof(ESCommand).Assembly.GetType("ES.ESCommand_AudioSource_Stop", true);

            Assert.That(HasTypeRegistryItem(legacyPlay), Is.False);
            Assert.That(HasTypeRegistryItem(legacyStop), Is.False);
            Assert.That(HasTypeRegistryItem(typeof(ESCommand_AudioEmitter_Play)), Is.True);
            Assert.That(HasTypeRegistryItem(typeof(ESCommand_AudioEmitter_Stop)), Is.True);
        }

        [Test]
        public void FailureDiagnostics_RetainMachineCodeWithoutRequiringLocalizedText()
        {
            var module = new ESAudioModule();
            InvokeRecordFailure(
                module,
                "tests.audio.cooldown",
                ESAudioVoiceEndReason.None,
                ESAudioFailureCode.CooldownActive,
                "technical detail");

            var failures = new List<ESAudioFailureDiagnostic>();
            module.CopyRecentFailures(failures);

            Assert.That(failures.Count, Is.EqualTo(1));
            Assert.That(failures[0].Code, Is.EqualTo(ESAudioFailureCode.CooldownActive));
            Assert.That(failures[0].Reason, Is.EqualTo(ESAudioVoiceEndReason.None));
            Assert.That(ESAudioDiagnosticText.GetChineseFailure(failures[0].Code), Is.EqualTo("Cue 仍在冷却中"));
        }

        [Test]
        public void AuthoredAutoPlayQueue_BoundsA500EmitterReadinessBurstToSixteenStartsPerFrame()
        {
            var module = new ESAudioModule();
            var emitters = new List<GameObject>(500);
            try
            {
                for (int i = 0; i < 500; i++)
                {
                    GameObject instance = new GameObject("AudioAutoPlayQueueTest_" + i);
                    emitters.Add(instance);
                    ESVfxAudioEmitter emitter = instance.AddComponent<ESVfxAudioEmitter>();
                    Assert.That(InvokeTryEnqueueAutoPlay(module, emitter), Is.True, i.ToString());
                }

                Assert.That(GetAutoPlayQueueCount(module), Is.EqualTo(500));

                // Module Update is the sole executor. No readiness-event callback starts a Voice
                // directly, so a 500-emitter catalog edge cannot submit 500 admissions in one frame.
                InvokeAudioUpdate(module);
                Assert.That(GetAutoPlayQueueCount(module), Is.EqualTo(484));
            }
            finally
            {
                for (int i = 0; i < emitters.Count; i++)
                    Object.DestroyImmediate(emitters[i]);
            }
        }

        [Test]
        public void AuthoredAutoPlayQueue_UsesInternalFifoWaitingSlots_For513OverflowRequests()
        {
            var module = new ESAudioModule();
            var emitters = new List<GameObject>(1025);
            try
            {
                for (int i = 0; i < 1025; i++)
                {
                    GameObject instance = new GameObject("AudioAutoPlayFifoTest_" + i);
                    emitters.Add(instance);
                    Assert.That(InvokeTryEnqueueAutoPlay(module, instance.AddComponent<ESVfxAudioEmitter>()), Is.True, i.ToString());
                }

                Assert.That(GetAutoPlayQueueCount(module), Is.EqualTo(1025));
                Assert.That(GetAutoPlayWaitingQueueCount(module), Is.EqualTo(513));

                InvokeAudioUpdate(module);

                Assert.That(GetAutoPlayQueueCount(module), Is.EqualTo(1009));
                Assert.That(GetAutoPlayWaitingQueueCount(module), Is.EqualTo(497));
                Assert.That(GetAutoPlayExecutionEmitter(module, 0), Is.SameAs(emitters[16].GetComponent<ESVfxAudioEmitter>()));
                Assert.That(GetAutoPlayExecutionEmitter(module, 496), Is.SameAs(emitters[512].GetComponent<ESVfxAudioEmitter>()));
            }
            finally
            {
                for (int i = 0; i < emitters.Count; i++)
                    Object.DestroyImmediate(emitters[i]);
            }
        }

        [Test]
        public void AuthoredAutoPlayQueue_Retains1024WaitingRequests_AndCancellationReleasesWaitingCapacity()
        {
            var module = new ESAudioModule();
            var emitters = new List<GameObject>(1536);
            try
            {
                for (int i = 0; i < 1536; i++)
                {
                    GameObject instance = new GameObject("AudioAutoPlayCapacityTest_" + i);
                    emitters.Add(instance);
                    Assert.That(InvokeTryEnqueueAutoPlay(module, instance.AddComponent<ESVfxAudioEmitter>()), Is.True, i.ToString());
                }

                Assert.That(GetAutoPlayQueueCount(module), Is.EqualTo(1536));
                Assert.That(GetAutoPlayWaitingQueueCount(module), Is.EqualTo(1024));

                // OnDisable / pool despawn call this same module cancellation path before the
                // GameObject becomes inactive, so a waiting Emitter cannot retain a start slot.
                InvokeCancelAutoPlay(module, emitters[700].GetComponent<ESVfxAudioEmitter>());
                Assert.That(GetAutoPlayQueueCount(module), Is.EqualTo(1535));
                Assert.That(GetAutoPlayWaitingQueueCount(module), Is.EqualTo(1023));

                InvokeAudioUpdate(module);
                Assert.That(GetAutoPlayQueueCount(module), Is.EqualTo(1519));
                Assert.That(GetAutoPlayWaitingQueueCount(module), Is.EqualTo(1007));
            }
            finally
            {
                for (int i = 0; i < emitters.Count; i++)
                    Object.DestroyImmediate(emitters[i]);
            }
        }

        [Test]
        public void QueuedAutoPlay_DoesNotStartWhenTheQueuedModuleIsNoLongerCurrent()
        {
            ESAudioModule previousAudio = GetStaticAudioModule();
            GameObject instance = new GameObject("StaleQueuedAutoPlayTest");
            try
            {
                ESVfxAudioEmitter emitter = instance.AddComponent<ESVfxAudioEmitter>();
                SetPrivateField(emitter, "autoPlayArmed", true);
                SetPrivateField(emitter, "autoPlayQueued", true);

                // Simulates a Provider/module generation change between queue admission and the
                // next audio Update. The stale executor must only re-arm readiness, never start.
                SetStaticAudioModule(new ESAudioModule());
                emitter.ExecuteQueuedAutoPlay(new ESAudioModule());

                Assert.That(GetPrivateField<bool>(emitter, "autoPlayArmed"), Is.True);
                Assert.That(GetPrivateField<bool>(emitter, "autoPlayQueued"), Is.False);
                Assert.That(emitter.ActiveHandle.IsValid, Is.False);
            }
            finally
            {
                SetStaticAudioModule(previousAudio);
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void QueuedAutoPlay_RejectsASecondPendingIntentFromTheSameEmitter()
        {
            var module = new ESAudioModule();
            GameObject instance = new GameObject("DuplicateQueuedAutoPlayTest");
            try
            {
                ESVfxAudioEmitter emitter = instance.AddComponent<ESVfxAudioEmitter>();
                SetPrivateField(emitter, "autoPlayArmed", true);
                SetPrivateField(emitter, "autoPlayQueued", true);

                InvokeTryPlayArmedOnEnable(emitter);

                Assert.That(GetAutoPlayQueueCount(module), Is.Zero);
                Assert.That(GetPrivateField<bool>(emitter, "autoPlayQueued"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void AutoPlayOverflowDiagnostics_BatchAtMostFourOriginSamples()
        {
            var module = new ESAudioModule();
            var emitters = new List<GameObject>(6);
            try
            {
                for (int i = 0; i < 6; i++)
                {
                    GameObject instance = new GameObject("AudioAutoPlayOverflowOrigin_" + i);
                    emitters.Add(instance);
                    ESVfxAudioEmitter emitter = instance.AddComponent<ESVfxAudioEmitter>();
                    if (i == 0)
                    {
                        SetPrivateField(emitter, "diagnosticPrefabPath", "Assets/Vfx/AudioAutoPlayOverflowOrigin.prefab");
                        string origin = emitter.DescribeAutoPlayOriginForDiagnostics();
                        Assert.That(origin, Does.Contain("ScenePath="));
                        Assert.That(origin, Does.Contain("PrefabPath=Assets/Vfx/AudioAutoPlayOverflowOrigin.prefab"));
                        Assert.That(origin, Does.Contain("LegacyClip="));
                    }
                    InvokeReportAutoPlayQueueOverflow(module, emitter);
                }

                Assert.That(GetModulePrivateField<int>(module, "autoPlayOverflowCount"), Is.EqualTo(6));
                Assert.That(GetModulePrivateField<int>(module, "autoPlayOverflowSourceSampleCount"), Is.EqualTo(4));
                Assert.That(
                    GetModulePrivateField<string[]>(module, "autoPlayOverflowSourceSamples")[0],
                    Does.Contain("PrefabPath=Assets/Vfx/AudioAutoPlayOverflowOrigin.prefab"));
            }
            finally
            {
                for (int i = 0; i < emitters.Count; i++)
                    Object.DestroyImmediate(emitters[i]);
            }
        }

        private static object InvokeCaptureAudioSourceSnapshot(AudioSource source)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod(
                "CaptureAudioSourceSnapshot",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, new object[] { source });
        }

        private static void InvokeRestoreBoundEmitter(AudioSource source, object snapshot)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod(
                "RestoreBoundEmitter",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new[] { (object)source, snapshot });
        }

        private static bool HasTypeRegistryItem(Type type)
        {
            object[] attributes = type.GetCustomAttributes(false);
            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].GetType().Name == "TypeRegistryItemAttribute")
                    return true;
            }

            return false;
        }

        private static bool IsTransitionReservedMusicVoice(ESAudioModule module, object voice)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod("IsTransitionReservedMusicVoice", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(module, new[] { voice });
        }

        private static bool InvokeHasCueVoices(ESAudioModule module)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod("HasCueVoices", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(module, null);
        }

        private static void InvokeStopCueVoices(ESAudioModule module, ESAudioVoiceEndReason reason)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod("StopCueVoices", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(module, new object[] { reason });
        }

        private static void InvokeActivePlanAssetOwnershipEnding(ESAudioModule module, ESAssetIdentity identity)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod(
                "OnActivePlanAssetOwnershipEnding",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(module, new object[] { identity });
        }

        private static void InvokeAudioUpdate(ESAudioModule module)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(module, null);
        }

        private static bool InvokeTryEnqueueAutoPlay(ESAudioModule module, ESVfxAudioEmitter emitter)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod(
                "TryEnqueueAutoPlay",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(module, new object[] { emitter });
        }

        private static int GetAutoPlayQueueCount(ESAudioModule module)
        {
            PropertyInfo property = typeof(ESAudioModule).GetProperty(
                "PendingAutoPlayCount",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return (int)property.GetValue(module);
        }

        private static int GetAutoPlayWaitingQueueCount(ESAudioModule module)
        {
            PropertyInfo property = typeof(ESAudioModule).GetProperty(
                "PendingAutoPlayWaitingCount",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return (int)property.GetValue(module);
        }

        private static ESVfxAudioEmitter GetAutoPlayExecutionEmitter(ESAudioModule module, int logicalIndex)
        {
            FieldInfo queueField = typeof(ESAudioModule).GetField("autoPlayQueue", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo headField = typeof(ESAudioModule).GetField("autoPlayQueueHead", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(queueField, Is.Not.Null);
            Assert.That(headField, Is.Not.Null);
            var queue = (ESVfxAudioEmitter[])queueField.GetValue(module);
            int head = (int)headField.GetValue(module);
            return queue[(head + logicalIndex) % queue.Length];
        }

        private static void InvokeCancelAutoPlay(ESAudioModule module, ESVfxAudioEmitter emitter)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod(
                "CancelAutoPlay",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(module, new object[] { emitter });
        }

        private static void InvokeTryPlayArmedOnEnable(ESVfxAudioEmitter emitter)
        {
            MethodInfo method = typeof(ESVfxAudioEmitter).GetMethod(
                "TryPlayArmedOnEnable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(emitter, null);
        }

        private static ESAudioModule GetStaticAudioModule()
        {
            PropertyInfo property = typeof(ESGameManager).GetProperty("Audio", BindingFlags.Static | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return (ESAudioModule)property.GetValue(null);
        }

        private static void SetStaticAudioModule(ESAudioModule module)
        {
            PropertyInfo property = typeof(ESGameManager).GetProperty("Audio", BindingFlags.Static | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            MethodInfo setter = property.GetSetMethod(true);
            Assert.That(setter, Is.Not.Null);
            setter.Invoke(null, new object[] { module });
        }

        private static T GetPrivateField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(target);
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static void InvokeReportAutoPlayQueueOverflow(ESAudioModule module, ESVfxAudioEmitter emitter)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod(
                "ReportAutoPlayQueueOverflow",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(module, new object[] { emitter });
        }

        private static T GetModulePrivateField<T>(ESAudioModule module, string name)
        {
            FieldInfo field = typeof(ESAudioModule).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(module);
        }

        private static object RentAdmission(
            ESAudioModule module,
            object voice,
            bool isCue,
            bool allowPreemption,
            object ignoredVoice)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod("RentAdmission", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(module, new[] { voice, (object)isCue, allowPreemption, ignoredVoice });
        }

        private static bool InvokeTryAdmitPreparedVoice(ESAudioModule module, object admission, out string error)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod("TryAdmitPreparedVoice", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { admission, null };
            bool admitted = (bool)method.Invoke(module, arguments);
            error = (string)arguments[1];
            return admitted;
        }

        private static bool InvokeTryPlanAdmission(ESAudioModule module, object admission, out string error)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod("TryPlanAdmission", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { admission, null };
            bool planned = (bool)method.Invoke(module, arguments);
            error = (string)arguments[1];
            return planned;
        }

        private static bool InvokeTryReserveVictim(object admission, object voice)
        {
            MethodInfo method = admission.GetType().GetMethod("TryReserveVictim", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(admission, new[] { voice });
        }

        private static int GetReservedVictimCount(object admission)
        {
            PropertyInfo property = admission.GetType().GetProperty("ReservedVictimCount", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return (int)property.GetValue(admission);
        }

        private static void InvokeDiscardUnstartedVoice(ESAudioModule module, object voice)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod("DiscardUnstartedVoice", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(module, new[] { voice });
        }

        private static void InvokeCompleteAdmission(ESAudioModule module, object admission)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod("CompleteAdmission", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(module, new[] { admission });
        }

        private static void InvokeCancelAdmission(ESAudioModule module, object admission, ESAudioVoiceEndReason reason)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod(
                "CancelAdmission",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { admission.GetType(), typeof(ESAudioVoiceEndReason), typeof(string), typeof(bool) },
                null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(module, new object[] { admission, reason, null, false });
        }

        private static void InvokeFailAdmission(
            ESAudioModule module,
            object admission,
            ESAudioVoiceEndReason reason,
            ESAudioFailureCode code,
            string error)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod(
                "FailAdmission",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    admission.GetType(),
                    typeof(ESAudioVoiceEndReason),
                    typeof(ESAudioFailureCode),
                    typeof(string)
                },
                null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(module, new object[] { admission, reason, code, error });
        }

        private static void InvokeEndVoice(ESAudioModule module, object voice, ESAudioVoiceEndReason reason)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod(
                "EndVoice",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { voice.GetType(), typeof(ESAudioVoiceEndReason), typeof(string) },
                null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(module, new object[] { voice, reason, null });
        }

        private static void InvokeRecordFailure(
            ESAudioModule module,
            string cueKey,
            ESAudioVoiceEndReason reason,
            ESAudioFailureCode code,
            string message)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod(
                "RecordFailure",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(string),
                    typeof(ESAudioVoiceEndReason),
                    typeof(ESAudioFailureCode),
                    typeof(string)
                },
                null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(module, new object[] { cueKey, reason, code, message });
        }

        private static bool InvokeTryGetAdmission(ESAudioModule module, object handle)
        {
            MethodInfo method = typeof(ESAudioModule).GetMethod("TryGetAdmission", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { handle, null };
            return (bool)method.Invoke(module, arguments);
        }

        private static object CreateActiveVoice(ESAudioModule module, int id, int runtimeCueKey, ESAudioCategory category)
        {
            Type voiceType = typeof(ESAudioModule).GetNestedType("Voice", BindingFlags.NonPublic);
            Assert.That(voiceType, Is.Not.Null);
            object voice = System.Activator.CreateInstance(voiceType, true);
            SetVoiceField(voiceType, voice, "id", id);
            SetVoiceField(voiceType, voice, "generation", 1);
            SetVoiceField(voiceType, voice, "runtimeCueKey", runtimeCueKey);
            SetVoiceField(voiceType, voice, "category", category);
            SetVoiceField(voiceType, voice, "priority", 128);
            SetVoiceField(voiceType, voice, "active", true);
            GetVoices(module).Add(voice);
            return voice;
        }

        private static object CreatePooledVoice(
            ESAudioModule module,
            int id,
            int runtimeCueKey,
            ESAudioCategory category,
            int priority)
        {
            FieldInfo poolField = typeof(ESAudioModule).GetField("voicePool", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(poolField, Is.Not.Null);
            object pool = poolField.GetValue(module);
            MethodInfo getMethod = pool.GetType().GetMethod("GetInPool", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(getMethod, Is.Not.Null);
            object voice = getMethod.Invoke(pool, null);
            Type voiceType = voice.GetType();
            SetVoiceField(voiceType, voice, "id", id);
            SetVoiceField(voiceType, voice, "generation", 1);
            SetVoiceField(voiceType, voice, "runtimeCueKey", runtimeCueKey);
            SetVoiceField(voiceType, voice, "category", category);
            SetVoiceField(voiceType, voice, "priority", priority);
            SetVoiceField(voiceType, voice, "active", false);
            return voice;
        }

        private static ESAudioVoiceHandle CreateVoiceHandle(int id, int generation)
        {
            ConstructorInfo constructor = typeof(ESAudioVoiceHandle).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(int) },
                null);
            Assert.That(constructor, Is.Not.Null);
            return (ESAudioVoiceHandle)constructor.Invoke(new object[] { id, generation });
        }

        private static IList GetVoices(ESAudioModule module)
        {
            FieldInfo field = typeof(ESAudioModule).GetField("voices", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (IList)field.GetValue(module);
        }

        private static IList GetPendingAdmissions(ESAudioModule module)
        {
            FieldInfo field = typeof(ESAudioModule).GetField("pendingAdmissions", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (IList)field.GetValue(module);
        }

        private static void SetVoiceField(Type voiceType, object voice, string name, object value)
        {
            FieldInfo field = voiceType.GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(voice, value);
        }

        private static void SetModuleField(ESAudioModule module, string name, object value)
        {
            FieldInfo field = typeof(ESAudioModule).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(module, value);
        }
    }
}
