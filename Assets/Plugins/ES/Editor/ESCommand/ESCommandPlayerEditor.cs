using UnityEditor;
using UnityEngine;

namespace ES.Editor
{
    [CustomEditor(typeof(ESCommandPlayer))]
    public sealed class ESCommandPlayerEditor : UnityEditor.Editor
    {
        private SerializedProperty playOnStart;
        private SerializedProperty eventToPlay;

        private void OnEnable()
        {
            playOnStart = serializedObject.FindProperty("playOnStart");
            eventToPlay = serializedObject.FindProperty("eventToPlay");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            ESCommandPlayer player = (ESCommandPlayer)target;
            DrawHeader(player);

            EditorGUILayout.Space(6f);
            EditorGUILayout.PropertyField(playOnStart, new GUIContent("启动时播放"));

            EditorGUILayout.Space(8f);
            DrawButtons(player);

            EditorGUILayout.Space(8f);
            EditorGUILayout.PropertyField(eventToPlay, new GUIContent("播放事件"), true);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawHeader(ESCommandPlayer player)
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 42f, GUILayout.ExpandWidth(true));
            Color color = GetStateColor(player.State);
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.14f, 0.16f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 5f, rect.height), color);

            GUIStyle title = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
            GUIStyle sub = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.75f, 0.8f, 0.84f, 1f) }
            };

            EditorGUI.LabelField(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, 18f), "ES Command Player", title);
            EditorGUI.LabelField(new Rect(rect.x + 12f, rect.y + 24f, rect.width - 24f, 14f), "状态：" + GetStateText(player.State), sub);
        }

        private static void DrawButtons(ESCommandPlayer player)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !Application.isPlaying || !player.IsPlaying;
                if (GUILayout.Button("播放", GUILayout.Height(24f)))
                {
                    if (Application.isPlaying)
                        player.Play();
                }

                GUI.enabled = Application.isPlaying && player.IsPlaying;
                if (GUILayout.Button("取消", GUILayout.Height(24f)))
                    player.Cancel();

                if (GUILayout.Button("停止", GUILayout.Height(24f)))
                    player.Stop();

                GUI.enabled = true;
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("播放按钮只在运行时执行。编辑模式下用于配置命令列表。", MessageType.Info);
            }
        }

        private static Color GetStateColor(ESRunState state)
        {
            switch (state)
            {
                case ESRunState.Running:
                    return new Color(0.25f, 0.62f, 0.82f, 1f);
                case ESRunState.Succeeded:
                    return new Color(0.25f, 0.68f, 0.38f, 1f);
                case ESRunState.Failed:
                    return new Color(0.85f, 0.28f, 0.24f, 1f);
                case ESRunState.Canceled:
                    return new Color(0.92f, 0.62f, 0.22f, 1f);
                default:
                    return new Color(0.45f, 0.48f, 0.52f, 1f);
            }
        }

        private static string GetStateText(ESRunState state)
        {
            switch (state)
            {
                case ESRunState.Running: return "运行中";
                case ESRunState.Succeeded: return "成功";
                case ESRunState.Failed: return "失败";
                case ESRunState.Canceled: return "已取消";
                case ESRunState.Skipped: return "已跳过";
                default: return "无";
            }
        }
    }
}
