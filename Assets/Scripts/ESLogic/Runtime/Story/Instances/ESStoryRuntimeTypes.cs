using System;
using System.Collections.Generic;

namespace ES
{
    public enum ESStoryRunState : byte
    {
        Created, Running, WaitingForForeground, WaitingForUI, Completed, Failed, Aborted
    }

    public enum ESStoryExecutionState : byte { Prepared, Succeeded, Failed, Discarded }

    [Serializable]
    public sealed class ESQuestRecord
    {
        public string definitionId;
        public int contentVersion;
        public string contentSignature;
        public string currentNodeId;
        public ESStoryRunState runState;
        public long recordRevision;
        public long nodeVisitSequence;
    }

    public sealed class ESStoryExecutionTicket
    {
        public string executionId;
        public string storyInstanceId;
        public long expectedInstanceRevision;
        public string nodeId;
        public long nodeVisitSequence;
        public string actionId;
        public ESStoryExecutionState executionState;
        public bool actionResult;
    }

    public sealed class ESStoryInstance
    {
        public string InstanceId { get; internal set; }
        public ESStoryDefinitionSnapshot Definition { get; internal set; }
        public Entity Actor { get; internal set; }
        public ESInteractionBinding InteractionBinding { get; internal set; }
        public string CurrentNodeId { get; internal set; }
        public ESStoryRunState RunState { get; internal set; }
        public long Revision { get; internal set; }
        public long NodeVisitSequence { get; internal set; }
        public string SessionId { get; internal set; }
        public int SessionGeneration { get; internal set; }
        public long ViewRevision { get; internal set; }
        public ESRuntimeModeLease RuntimeModeLease { get; internal set; }
        /// <summary>运行期临时相机 Scope；不参与快照/存档，随前台会话重建。</summary>
        public ESCameraControlScope CameraScope { get; internal set; }
        public ESStoryExecutionTicket CurrentExecution { get; internal set; }
        public ESQuestRecord QuestRecord { get; internal set; }
    }

    [Serializable]
    public sealed class ESDialogueOptionViewData
    {
        public string optionId;
        public string textKey;
        public string text;
        public ESLocalizationResolveStatus resolveStatus;
        public EnumCollect.Envir_LanguageType requestedLanguage;
        public EnumCollect.Envir_LanguageType resolvedLanguage;
    }

    [Serializable]
    public sealed class ESDialogueViewData
    {
        public string definitionId;
        public string storyInstanceId;
        public long instanceRevision;
        public string sessionId;
        public int sessionGeneration;
        public long viewRevision;
        public int localizationGeneration;
        public bool hasSpeaker;
        public string speakerTextKey;
        public string speakerName;
        public ESLocalizationResolveStatus speakerResolveStatus;
        public EnumCollect.Envir_LanguageType speakerRequestedLanguage;
        public EnumCollect.Envir_LanguageType speakerResolvedLanguage;
        public bool hasBodyText;
        public string bodyTextKey;
        public string text;
        public ESLocalizationResolveStatus bodyResolveStatus;
        public EnumCollect.Envir_LanguageType requestedLanguage;
        public EnumCollect.Envir_LanguageType bodyResolvedLanguage;
        public bool canContinue;
        public List<ESDialogueOptionViewData> options = new List<ESDialogueOptionViewData>();
    }

    public readonly struct ESStoryViewSubmission
    {
        public readonly string StoryInstanceId;
        public readonly long ExpectedInstanceRevision;
        public readonly string SessionId;
        public readonly int SessionGeneration;
        public readonly long ViewRevision;
        public readonly string OptionId;

        public ESStoryViewSubmission(string storyInstanceId, long expectedInstanceRevision, string sessionId, int sessionGeneration, long viewRevision, string optionId = null)
        {
            StoryInstanceId = storyInstanceId;
            ExpectedInstanceRevision = expectedInstanceRevision;
            SessionId = sessionId;
            SessionGeneration = sessionGeneration;
            ViewRevision = viewRevision;
            OptionId = optionId;
        }
    }

    public interface IESStoryDialoguePresenter
    {
        void Show(ESDialogueViewData view);
        void Close(string storyInstanceId, string sessionId, int sessionGeneration);
    }

    public static class ESStoryRuntimeGuard
    {
        public static bool HasActiveQuest(IEnumerable<ESStoryInstance> instances, string definitionId)
        {
            if (instances == null || string.IsNullOrEmpty(definitionId)) return false;
            foreach (ESStoryInstance instance in instances)
                if (instance?.Definition?.StoryKind == ESStoryKind.Quest
                    && string.Equals(instance.Definition.DefinitionId, definitionId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        public static bool IsCurrentSubmission(ESStoryInstance instance, ESStoryInstance foreground, ESStoryViewSubmission submission)
        {
            return instance != null && ReferenceEquals(instance, foreground)
                && instance.RunState == ESStoryRunState.WaitingForUI
                && instance.Revision == submission.ExpectedInstanceRevision
                && string.Equals(instance.SessionId, submission.SessionId, StringComparison.Ordinal)
                && instance.SessionGeneration == submission.SessionGeneration
                && instance.ViewRevision == submission.ViewRevision;
        }
    }
}
