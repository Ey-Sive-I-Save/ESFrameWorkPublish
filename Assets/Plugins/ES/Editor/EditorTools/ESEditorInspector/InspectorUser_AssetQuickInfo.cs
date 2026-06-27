using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ES
{
    // 项目资源绘制 - 快速定位与信息导出扩展
    public class InspectorUser_AssetQuickInfo : ESEditorInspectorUser
    {
        private const string APPEND_PREF_KEY = "ES_ResHelper_AppendMode";
        private const long MaxCopyTextBytes = 1024 * 1024;

        private static readonly HashSet<string> TextExtensions = new HashSet<string>
        {
            ".txt", ".csv", ".json", ".xml", ".lua", ".cs", ".js", ".shader", ".cginc",
            ".hlsl", ".md", ".yml", ".yaml", ".ini", ".bat", ".sh", ".html", ".css", ".xaml"
        };

        public override bool Apply(UnityEngine.Object ob)
        {
            if (ob == null) return false;
            if (ob.GetType().IsSubclassOf(typeof(VisualGUIDrawerSO))) return false;

            string path = AssetDatabase.GetAssetPath(ob);
            if (string.IsNullOrEmpty(path)) return false;
            if (!IsSafeAssetPath(path)) return false;

            bool appendMode = EditorPrefs.GetBool(APPEND_PREF_KEY, false);

            // 1. 顶部追加模式开关
            EditorGUILayout.BeginHorizontal();
            bool newAppendMode = GUILayout.Toggle(appendMode, " 追加模式", GUILayout.Width(90));
            if (newAppendMode != appendMode)
            {
                appendMode = newAppendMode;
                EditorPrefs.SetBool(APPEND_PREF_KEY, appendMode);
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(appendMode ? "已启用" : "已关闭", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            string guid = AssetDatabase.AssetPathToGUID(path);
            FileInfo fileInfo = new FileInfo(path);

            // 2. GUID 行
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(guid, EditorStyles.textField, GUILayout.Height(18))) { CopyToClipboard(guid, "GUID", appendMode); }
            if (GUILayout.Button("复制", GUILayout.Width(45), GUILayout.Height(18))) { CopyToClipboard(guid, "GUID", appendMode); }
            if (GUILayout.Button("Ping", GUILayout.Width(45), GUILayout.Height(18))) { EditorGUIUtility.PingObject(ob); }
            if (GUILayout.Button(EditorGUIUtility.IconContent("Folder Icon"), GUILayout.Width(30), GUILayout.Height(18), GUILayout.ExpandWidth(false)))
            {
                EditorUtility.RevealInFinder(path);
            }
            EditorGUILayout.EndHorizontal();

            // 3. 路径行
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(path, EditorStyles.textField, GUILayout.Height(18))) { CopyToClipboard(path, "路径", appendMode); }
            if (GUILayout.Button("复制", GUILayout.Width(45), GUILayout.Height(18))) { CopyToClipboard(path, "路径", appendMode); }
            EditorGUILayout.EndHorizontal();

            // 4. 读取文件文本内容的扩展功能
            if (fileInfo.Exists)
            {
                string extension = Path.GetExtension(path).ToLower();

                if (TextExtensions.Contains(extension))
                {
                    if (fileInfo.Length > MaxCopyTextBytes)
                    {
                        EditorGUILayout.LabelField("文本内容: 文件过大 (>1MB)，跳过读取", EditorStyles.helpBox);
                    }
                    else
                    {
                        if (GUILayout.Button($"复制全部文本内容 ({fileInfo.Length / 1024f:F1} KB)", GUILayout.Height(20)))
                        {
                            try
                            {
                                string content = ReadTextWithFallback(path);
                                CopyToClipboard(content, "文本", appendMode);
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"读取文件内容失败: {ex.Message}");
                            }
                        }
                    }
                }
            }

            // =============================================================
            // 5. 【新增】一键删除与二级菜单弹窗保护
            // =============================================================
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider); // 增加一条分隔线
            EditorGUILayout.Space(2);

            // 危险操作区域，颜色加深提示
            Color oldBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f, 0.9f); // 红色背景提示
            if (GUILayout.Button("🗑 删除此资源文件", GUILayout.Height(22)))
            {
                if (!CanDeleteAsset(path))
                {
                    EditorUtility.DisplayDialog("无法删除", $"该路径不允许通过此工具删除:\n{path}", "确定");
                    GUI.backgroundColor = oldBackgroundColor;
                    return false;
                }

                // 二级菜单：确认弹窗
                bool confirmDelete = EditorUtility.DisplayDialog(
                    "⚠️ 危险操作确认",
                    $"确定要删除以下文件吗？\n\n名称: {Path.GetFileName(path)}\n路径: {path}\n\n此操作无法撤销！",
                    "确认删除",
                    "取消"
                );

                if (confirmDelete)
                {
                    // 为了保证安全，先清空当前选中的物体，避免删除后编辑器面板继续渲染死对象
                    Selection.activeObject = null;

                    // 利用之前写的延迟执行框架，在下一帧执行删除（防止 AssetDatabase 操作卡死 GUI）
                    ESEditorHandle.AddSimpleHandleTask(() =>
                    {
                        if (!CanDeleteAsset(path))
                        {
                            Debug.LogWarning($"删除已取消，路径不安全或资源不存在: {path}");
                            return;
                        }

                        if (AssetDatabase.MoveAssetToTrash(path))
                        {
                            AssetDatabase.Refresh(); // 刷新资源数据库
                            Debug.Log($"<color=red>[删除成功]</color> 已删除资源: {path}");
                        }
                        else
                        {
                            Debug.LogError($"删除资源失败: {path}");
                        }
                    }, waitframe: 1);
                }
            }
            GUI.backgroundColor = oldBackgroundColor; // 恢复进入本扩展前的 GUI 颜色

            return false;
        }

        private static bool IsSafeAssetPath(string path)
        {
            return path.StartsWith("Assets/") || path.StartsWith("Packages/");
        }

        private static bool CanDeleteAsset(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!path.StartsWith("Assets/")) return false;
            if (path == "Assets") return false;
            if (!File.Exists(path) && !Directory.Exists(path)) return false;

            return !AssetDatabase.IsValidFolder(path);
        }

        private static string ReadTextWithFallback(string path)
        {
            try
            {
                return File.ReadAllText(path, new UTF8Encoding(false, true));
            }
            catch (DecoderFallbackException)
            {
                return File.ReadAllText(path, Encoding.Default);
            }
        }

        private void CopyToClipboard(string content, string label, bool appendMode)
        {
            if (string.IsNullOrEmpty(content))
            {
                Debug.LogWarning($"复制 {label} 失败：内容为空");
                return;
            }

            string finalContent = content;

            if (appendMode)
            {
                string currentBuffer = EditorGUIUtility.systemCopyBuffer;
                if (string.IsNullOrEmpty(currentBuffer)) finalContent = content;
                else finalContent = currentBuffer + "\n" + content;
            }

            ESDesignUtility.SafeEditor.Wrap_SystemCopyBuffer(finalContent);

            if (appendMode)
            {
                Debug.Log($"<color=#00FF00>[追加模式]</color> 已添加 {label}。当前剪贴板共 {finalContent.Split('\n').Length} 行");
            }
            else
            {
                Debug.Log($"<color=#FFFF00>[覆盖模式]</color> 已复制 {label}");
            }
        }
    }
}
