using UnityEditor;
using UnityEngine;

namespace ES
{
    public sealed class ESDeveloperCockpitWindow : EditorWindow
    {
        private static ESDeveloperTraceProvider traceProvider;
        private Vector2 scroll;
        private string feedback = string.Empty;
        private bool subscribed;

        [MenuItem(MenuItemPathDefine.PROJECT_SETTINGS_PATH + "开发者驾驶舱")]
        public static void Open()
        {
            ESDeveloperCockpitWindow window = GetWindow<ESDeveloperCockpitWindow>(
                false,
                "ES 开发者驾驶舱");
            window.minSize = new Vector2(560f, 360f);
            window.Show();
        }

        private void OnEnable()
        {
            traceProvider ??= new ESDeveloperTraceProvider();
            ESDeveloperTraceHost.SetProvider(traceProvider);
            if (!subscribed)
            {
                EditorApplication.update += Repaint;
                subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (subscribed)
            {
                EditorApplication.update -= Repaint;
                subscribed = false;
            }
        }

        private void OnGUI()
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

            using (new EditorGUILayout.HorizontalScope())
            {
                if (!ESDeveloperObservationController.IsObserving
                    && GUILayout.Button("开始 Observation Run"))
                {
                    traceProvider?.Clear();
                    if (ESDeveloperObservationController.TryBeginObservationRun(out string error))
                    {
                        feedback = "Observation Run 已开始。";
                    }
                    else
                    {
                        feedback = error;
                    }
                }

                if (ESDeveloperObservationController.IsObserving
                    && GUILayout.Button("停止 Observation Run"))
                {
                    ESDeveloperObservationController.StopObservationRun();
                    feedback = "Observation Run 已停止。";
                }
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
