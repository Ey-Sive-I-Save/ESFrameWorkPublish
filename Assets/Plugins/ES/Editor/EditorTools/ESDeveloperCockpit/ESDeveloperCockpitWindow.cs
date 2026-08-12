using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public sealed class ESDeveloperCockpitWindow : ESSinglePageIMGUIWindow<ESDeveloperCockpitWindow>
    {
        private static ESDeveloperTraceProvider traceProvider;
        private Vector2 scroll;
        private string feedback = string.Empty;
        private double nextRepaintAt;
        private bool lastObservingState;

        [MenuItem(MenuItemPathDefine.VALIDATION_DIAGNOSTICS_PATH + "开发者驾驶舱/打开开发者驾驶舱")]
        public static void Open()
        {
            ESDeveloperCockpitWindow window = GetWindow<ESDeveloperCockpitWindow>(
                false,
                "ES 开发者驾驶舱");
            window.minSize = new Vector2(560f, 360f);
            window.Show();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("ES 开发者驾驶舱", "观察同帧角色控制快照与开发者事件流");
        }

        protected override string ESWindow_Subtitle => "ContextSnapshot 与 Frame-Aligned Observation";
        protected override Vector2 ESWindow_MinSize => new Vector2(560f, 360f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(820f, 620f);
        protected override string ESWindow_PageStableId => "developer.cockpit";
        protected override string ESWindow_PageTitle => "开发者驾驶舱";
        protected override string ESWindow_PageKeywords => "开发者 Observation Trace Snapshot Entity PlayMode";

        protected override void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction(
                    "cockpit.start",
                    "开始观测",
                    "开始新的 Observation Run，并清空旧事件。",
                    context =>
                    {
                        traceProvider?.Clear();
                        if (ESDeveloperObservationController.TryBeginObservationRun(out string error))
                        {
                            feedback = "Observation Run 已开始。";
                            context.Notify(feedback);
                        }
                        else
                        {
                            feedback = error;
                            context.Notify(error, ESMenuTreePageStatus.Warning);
                        }
                        lastObservingState = ESDeveloperObservationController.IsObserving;
                        context.RefreshPageActions();
                        Repaint();
                    })
                .WhenVisible(() => !ESDeveloperObservationController.IsObserving)
                .When(() => EditorApplication.isPlayingOrWillChangePlaymode)
                .WithUnityIcon("PlayButton")
                .WithPriority(100));
            actions.Add(new ESMenuTreePageAction(
                    "cockpit.stop",
                    "停止观测",
                    "停止当前 Observation Run。",
                    context =>
                    {
                        ESDeveloperObservationController.StopObservationRun();
                        feedback = "Observation Run 已停止。";
                        lastObservingState = false;
                        context.RefreshPageActions();
                        context.SetStatus(feedback);
                        Repaint();
                    })
                .WhenVisible(() => ESDeveloperObservationController.IsObserving)
                .WithUnityIcon("PauseButton")
                .WithPriority(100));
            actions.Add(new ESMenuTreePageAction(
                    "cockpit.clear",
                    "清空事件",
                    "清空当前开发者事件快照。",
                    context =>
                    {
                        traceProvider?.Clear();
                        feedback = "事件快照已清空。";
                        context.SetStatus(feedback);
                        Repaint();
                    })
                .When(() => traceProvider != null && traceProvider.Count > 0)
                .WithUnityIcon("TreeEditor.Trash")
                .WithPriority(20));
        }

        protected override void ESWindow_OnHostEnable()
        {
            traceProvider ??= new ESDeveloperTraceProvider();
            ESDeveloperTraceHost.SetProvider(traceProvider);
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            nextRepaintAt = 0d;
            lastObservingState = ESDeveloperObservationController.IsObserving;
        }

        protected override void ESWindow_OnHostDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            nextRepaintAt = 0d;
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextRepaintAt)
                return;

            bool active = ESDeveloperObservationController.IsObserving
                || EditorApplication.isPlayingOrWillChangePlaymode;
            bool observing = ESDeveloperObservationController.IsObserving;
            if (observing != lastObservingState)
            {
                lastObservingState = observing;
                ESWindow_CurrentPageContext?.RefreshPageActions();
            }
            double interval = active && hasFocus ? 0.10d : active ? 0.25d : 0.50d;
            nextRepaintAt = now + interval;
            Repaint();
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("ES 开发者驾驶舱", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("只读 ContextSnapshot / Frame-Aligned Observation");

            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox(
                    "进入 PlayMode 并建立本地控制 Entity 后开始 Observation Run。",
                    MessageType.Info);
            }

            if (!string.IsNullOrWhiteSpace(feedback))
            {
                EditorGUILayout.HelpBox(feedback, MessageType.None);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "RunId",
                ESDeveloperTraceHost.CurrentRunId.IsValid
                    ? ESDeveloperTraceHost.CurrentRunId.ToString()
                    : "None");
            EditorGUILayout.LabelField("事件数", traceProvider == null ? "0" : traceProvider.Count.ToString());

            if (traceProvider != null && traceProvider.Count > 0)
            {
                ESDeveloperEventEnvelope last = traceProvider.LastEvent;
                EditorGUILayout.LabelField(
                    "最近事件",
                    last.EventKind + " / " + last.SourceId.Value);
                EditorGUILayout.LabelField(
                    "Sequence",
                    last.Sequence.ToString());
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("本地控制", EditorStyles.boldLabel);
            Entity entity = ESGameManager.LocalControl?.ControlledEntity;
            if (entity == null)
            {
                EditorGUILayout.LabelField("None");
            }
            else
            {
                EditorGUILayout.LabelField(entity.name);
                EditorGUILayout.LabelField("RuntimeHandle", entity.GetInstanceID().ToString());
                EditorGUILayout.LabelField(
                    "IsGrounded",
                    entity.kcc != null && entity.kcc.motor != null
                        ? entity.kcc.motor.GroundingStatus.IsStableOnGround.ToString()
                        : "Unknown");
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("最近同帧快照", EditorStyles.boldLabel);
            ESCharacterControlFrameSnapshot snapshot =
                ESDeveloperObservationController.LastSnapshot;
            if (snapshot.SampleSequence < 1)
            {
                EditorGUILayout.LabelField("None");
            }
            else
            {
                EditorGUILayout.LabelField("Frame", snapshot.FrameCount.ToString());
                EditorGUILayout.LabelField(
                    "SampleSequence",
                    snapshot.SampleSequence.ToString());
                EditorGUILayout.LabelField(
                    "MoveInput",
                    snapshot.MoveInput.ToString("F2"));
                EditorGUILayout.LabelField(
                    "GroundTangentVelocity",
                    snapshot.GroundTangentVelocity.ToString("F2"));
                EditorGUILayout.LabelField(
                    "IsGrounded",
                    snapshot.IsGrounded.ToString());
                EditorGUILayout.LabelField(
                    "StateName",
                    string.IsNullOrWhiteSpace(snapshot.StateName)
                        ? "None"
                        : snapshot.StateName);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
