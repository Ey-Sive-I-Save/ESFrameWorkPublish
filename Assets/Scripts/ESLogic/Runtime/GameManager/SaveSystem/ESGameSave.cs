namespace ES
{
    public enum ESGameSaveApplyPhase : byte
    {
        Config, World, Player, Inventory, Quest, Runtime, Presentation
    }

    public static class ESGameSave
    {
        public static event System.Action BeforeSave;
        public static event ESGameSaveValidateCandidateHandler ValidateCandidate;
        public static event ESGameSavePrepareCandidateHandler PrepareCandidate;
        public static event ESGameSaveCommitCandidateHandler CommitCandidate;
        public static event ESGameSaveRollbackCandidateHandler RollbackCandidate;
        public static event ESGameSaveFinalizeCandidateHandler FinalizeCandidate;

        internal static bool NotifyBeforeSave()
        {
            System.Action callback = BeforeSave;
            if (callback == null) return true;
            bool success = true;
            foreach (System.Action handler in callback.GetInvocationList())
            {
                try { handler(); }
                catch (System.Exception exception) { success = false; UnityEngine.Debug.LogException(exception); }
            }
            return success;
        }

        internal static ESGameSaveApplyResult NotifyValidateCandidate(ESGameSaveCandidate candidate)
        {
            ESGameSaveValidateCandidateHandler callback = ValidateCandidate;
            if (callback == null) return ESGameSaveApplyResult.Ok();
            foreach (ESGameSaveValidateCandidateHandler handler in callback.GetInvocationList())
            {
                try
                {
                    ESGameSaveApplyResult result = handler(candidate);
                    if (result == null)
                        return ESGameSaveApplyResult.Fail("Save.Validate.NullResult", "存档参与者返回了空验证结果。");
                    if (!result.Success)
                        return result;
                }
                catch (System.Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                    return ESGameSaveApplyResult.Fail("Save.Validate.Exception", exception.Message);
                }
            }
            return ESGameSaveApplyResult.Ok();
        }

        internal static ESGameSaveApplyResult NotifyPrepareCandidate(ESGameSaveCandidate candidate)
        {
            ESGameSavePrepareCandidateHandler callback = PrepareCandidate;
            if (callback == null) return ESGameSaveApplyResult.Ok();
            foreach (ESGameSavePrepareCandidateHandler handler in callback.GetInvocationList())
            {
                try
                {
                    ESGameSaveApplyResult result = handler(candidate);
                    if (result == null)
                        return ESGameSaveApplyResult.Fail("Save.Prepare.NullResult", "存档参与者返回了空 Prepare 结果。");
                    if (!result.Success)
                        return result;
                }
                catch (System.Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                    return ESGameSaveApplyResult.Fail("Save.Prepare.Exception", exception.Message);
                }
            }
            return ESGameSaveApplyResult.Ok();
        }

        internal static ESGameSaveApplyResult NotifyCommitCandidate(ESGameSaveCandidate candidate)
        {
            ESGameSaveCommitCandidateHandler callback = CommitCandidate;
            if (callback == null) return ESGameSaveApplyResult.Ok();
            System.Delegate[] handlers = callback.GetInvocationList();
            candidate.CommittedCalls.Clear();
            ESGameSaveApplyPhase[] phases = (ESGameSaveApplyPhase[])System.Enum.GetValues(typeof(ESGameSaveApplyPhase));
            for (int i = 0; i < phases.Length; i++)
            {
                for (int h = 0; h < handlers.Length; h++)
                {
                    try
                    {
                        ESGameSaveApplyResult result = ((ESGameSaveCommitCandidateHandler)handlers[h])(candidate, phases[i]);
                        if (result == null)
                            return RollbackAfterFailure(candidate, "Save.Commit.NullResult", "存档参与者返回了空提交结果。");
                        if (!result.Success)
                            return RollbackAfterFailure(candidate, result.ErrorCode, result.Message);
                        candidate.CommittedCalls.Add(new ESGameSaveCommittedCall { participantIndex = h, phase = phases[i] });
                    }
                    catch (System.Exception exception)
                    {
                        UnityEngine.Debug.LogException(exception);
                        return RollbackAfterFailure(candidate, "Save.Commit.Exception", exception.Message);
                    }
                }
            }
            ESGameSaveApplyResult finalize = NotifyFinalizeCandidate(candidate);
            if (!finalize.Success)
                return RollbackAfterFailure(candidate, finalize.ErrorCode, finalize.Message);
            candidate.CommittedCalls.Clear();
            return ESGameSaveApplyResult.Ok();
        }

        private static ESGameSaveApplyResult NotifyFinalizeCandidate(ESGameSaveCandidate candidate)
        {
            ESGameSaveFinalizeCandidateHandler callback = FinalizeCandidate;
            if (callback == null) return ESGameSaveApplyResult.Ok();
            System.Delegate[] handlers = callback.GetInvocationList();
            for (int i = 0; i < candidate.CommittedCalls.Count; i++)
            {
                ESGameSaveCommittedCall call = candidate.CommittedCalls[i];
                if (call.participantIndex >= handlers.Length)
                    return ESGameSaveApplyResult.Fail("Save.Finalize.MissingParticipant", "Finalize Participant 数量与 Commit 不一致。");
                try
                {
                    ESGameSaveApplyResult result = ((ESGameSaveFinalizeCandidateHandler)handlers[call.participantIndex])(candidate, call.phase);
                    if (result == null)
                        return ESGameSaveApplyResult.Fail("Save.Finalize.NullResult", "存档参与者返回了空 Finalize 结果。");
                    if (!result.Success)
                        return result;
                }
                catch (System.Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                    return ESGameSaveApplyResult.Fail("Save.Finalize.Exception", exception.Message);
                }
            }
            return ESGameSaveApplyResult.Ok();
        }

        private static ESGameSaveApplyResult RollbackAfterFailure(ESGameSaveCandidate candidate, string errorCode, string message)
        {
            ESGameSaveRollbackCandidateHandler callback = RollbackCandidate;
            System.Delegate[] handlers = callback != null ? callback.GetInvocationList() : null;
            bool rollbackSuccess = true;
            string rollbackMessage = string.Empty;
            for (int i = candidate.CommittedCalls.Count - 1; i >= 0; i--)
            {
                ESGameSaveCommittedCall call = candidate.CommittedCalls[i];
                if (handlers == null || call.participantIndex >= handlers.Length)
                {
                    rollbackSuccess = false;
                    rollbackMessage = "缺少对应的 Rollback Participant。";
                    continue;
                }
                try
                {
                    ESGameSaveApplyResult result = ((ESGameSaveRollbackCandidateHandler)handlers[call.participantIndex])(candidate, call.phase);
                    if (result == null || !result.Success)
                    {
                        rollbackSuccess = false;
                        rollbackMessage = result == null ? "Rollback 返回空结果。" : result.Message;
                    }
                }
                catch (System.Exception exception)
                {
                    rollbackSuccess = false;
                    rollbackMessage = exception.Message;
                    UnityEngine.Debug.LogException(exception);
                }
            }
            candidate.CommittedCalls.Clear();
            if (!rollbackSuccess)
                return ESGameSaveApplyResult.Fail("Save.Rollback.Failed", (message ?? "Commit 失败") + "；Rollback 失败：" + rollbackMessage);
            return ESGameSaveApplyResult.Fail(errorCode ?? "Save.Commit.Failed", message ?? "Commit 失败，已完成回滚。");
        }

        public static bool TryGetCurrentCandidate(out ESGameSaveCandidate candidate)
        {
            candidate = null;
            ESGameSaveModule module = Module;
            return module != null && module.TryGetCurrentCandidate(out candidate);
        }
        public static ESGameSaveModule Module
        {
            get
            {
                ESGameManager.TryGetModule(out ESGameSaveModule module);
                return module;
            }
        }

        /// <summary>Explicitly initializes the save module for a write/load workflow.</summary>
        public static ESGameSaveModule EnsureModule()
        {
            return ESGameManager.GetOrCreateModule<ESGameSaveModule>();
        }

        public static void Set<T>(string sectionKey, T data)
        {
            ESGameSaveModule module = EnsureModule();
            if (module != null)
                module.Set(sectionKey, data);
        }

        public static void SetCurrent<T>(string sectionKey, T data)
        {
            ESGameSaveModule module = EnsureModule();
            if (module != null)
                module.SetCurrent(sectionKey, data);
        }

        public static void Set<T>(string slotId, string sectionKey, T data)
        {
            ESGameSaveModule module = EnsureModule();
            if (module != null)
                module.Set(slotId, sectionKey, data);
        }

        public static void Set<T>(string slotId, string displayName, string sectionKey, T data)
        {
            ESGameSaveModule module = EnsureModule();
            if (module != null)
                module.Set(slotId, displayName, sectionKey, data);
        }

        public static bool Get<T>(string sectionKey, out T value)
        {
            ESGameSaveModule module = Module;
            if (module != null)
                return module.Get(sectionKey, out value);

            value = default;
            return false;
        }

        public static bool Get<T>(string slotId, string sectionKey, out T value)
        {
            ESGameSaveModule module = Module;
            if (module != null)
                return module.Get(slotId, sectionKey, out value);

            value = default;
            return false;
        }

        public static bool Save()
        {
            ESGameSaveModule module = Module;
            return module != null && module.Save();
        }

        public static bool Save(string slotId)
        {
            ESGameSaveModule module = Module;
            return module != null && module.Save(slotId);
        }

        public static bool Load()
        {
            ESGameSaveModule module = EnsureModule();
            return module != null && module.Load();
        }

        public static bool Load(string slotId)
        {
            ESGameSaveModule module = EnsureModule();
            return module != null && module.Load(slotId);
        }

        public static bool Has()
        {
            ESGameSaveModule module = Module;
            return module != null && module.Has();
        }

        public static bool Has(string slotId)
        {
            ESGameSaveModule module = Module;
            return module != null && module.Has(slotId);
        }

        public static bool Delete()
        {
            ESGameSaveModule module = Module;
            return module != null && module.Delete();
        }

        public static bool Delete(string slotId)
        {
            ESGameSaveModule module = Module;
            return module != null && module.Delete(slotId);
        }

        public static ESGameSaveSlotInfo Info()
        {
            ESGameSaveModule module = Module;
            return module != null ? module.Info() : null;
        }

        public static ESGameSaveSlotInfo Info(string slotId)
        {
            ESGameSaveModule module = Module;
            return module != null ? module.Info(slotId) : null;
        }
    }
}
