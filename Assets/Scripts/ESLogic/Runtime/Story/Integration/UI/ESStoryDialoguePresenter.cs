using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>切片 A 的最小可运行 UI；正式 UI 可实现 IESStoryDialoguePresenter 后替换。</summary>
    [DisallowMultipleComponent]
    public sealed class ESStoryDialoguePresenter : MonoBehaviour, IESStoryDialoguePresenter
    {
        [LabelText("显示最小调试 UI")]
        public bool drawDebugUI = true;
        [ShowInInspector, ReadOnly, LabelText("当前视图")]
        private ESDialogueViewData currentView;
        private ESStoryModule module;

        private void OnEnable()
        {
            module = ESGameManager.GetOrCreateModule<ESStoryModule>();
            module?.BindPresenter(this);
        }

        private void OnDisable()
        {
            if (module != null) module.BindPresenter(null);
            currentView = null;
            module = null;
        }

        public void Show(ESDialogueViewData view)
        {
            currentView = view;
        }

        public void Close(string storyInstanceId, string sessionId, int sessionGeneration)
        {
            if (currentView != null
                && currentView.storyInstanceId == storyInstanceId
                && currentView.sessionId == sessionId
                && currentView.sessionGeneration == sessionGeneration)
                currentView = null;
        }

        private void OnGUI()
        {
            if (!drawDebugUI || currentView == null) return;
            GUILayout.BeginArea(new Rect(24f, Screen.height - 280f, Mathf.Min(720f, Screen.width - 48f), 250f), GUI.skin.box);
            GUILayout.Label(string.IsNullOrEmpty(currentView.speakerName) ? "对话" : currentView.speakerName);
            GUILayout.Label(currentView.text ?? string.Empty);
            if (currentView.canContinue)
            {
                if (GUILayout.Button("继续")) module?.SubmitContinue(CreateSubmission());
            }
            else if (currentView.options != null)
            {
                for (int i = 0; i < currentView.options.Count; i++)
                {
                    ESDialogueOptionViewData option = currentView.options[i];
                    if (GUILayout.Button(option.text ?? option.optionId)) module?.SubmitOption(CreateSubmission(option.optionId));
                }
            }
            GUILayout.EndArea();
        }

        private ESStoryViewSubmission CreateSubmission(string optionId = null)
        {
            return new ESStoryViewSubmission(currentView.storyInstanceId, currentView.instanceRevision,
                currentView.sessionId, currentView.sessionGeneration, currentView.viewRevision, optionId);
        }
    }
}
