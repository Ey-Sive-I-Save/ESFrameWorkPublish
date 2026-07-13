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
        private const string DANGER_PREF_KEY = "ES_ResHelper_ShowAssetQuickInfoDanger";
        private const string GUIDE_EDIT_MODE_PREF_KEY = "ES_AssetGuide_EditMode";
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

            string guid = AssetDatabase.AssetPathToGUID(path);
            FileInfo fileInfo = new FileInfo(path);
            bool appendMode = EditorPrefs.GetBool(APPEND_PREF_KEY, false);
            bool showDanger = EditorPrefs.GetBool(DANGER_PREF_KEY, false);

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawHeader(ob, path, appendMode, showDanger, out appendMode, out showDanger);
                DrawInfoRows(ob, path, guid, appendMode);
                DrawAssetGuide(ob, guid);
                DrawTextCopy(path, fileInfo, appendMode);

                if (showDanger && CanDeleteAsset(path))
                    DrawDangerArea(path);
            }

            return false;
        }

        private static void DrawHeader(
            UnityEngine.Object ob,
            string path,
            bool appendMode,
            bool showDanger,
            out bool newAppendMode,
            out bool newShowDanger)
        {
            newAppendMode = appendMode;
            newShowDanger = showDanger;

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("资源", EditorStyles.boldLabel, GUILayout.Width(34));
            GUILayout.Label(GetShortPath(path), EditorStyles.miniLabel, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Ping", EditorStyles.miniButtonLeft, GUILayout.Width(38)))
                EditorGUIUtility.PingObject(ob);

            if (GUILayout.Button(EditorGUIUtility.IconContent("Folder Icon"), EditorStyles.miniButtonMid, GUILayout.Width(26)))
                EditorUtility.RevealInFinder(path);

            bool toggledAppend = GUILayout.Toggle(appendMode, new GUIContent("追", "追加复制模式"), EditorStyles.miniButtonMid, GUILayout.Width(28));
            if (toggledAppend != appendMode)
            {
                newAppendMode = toggledAppend;
                EditorPrefs.SetBool(APPEND_PREF_KEY, toggledAppend);
            }

            bool canDelete = CanDeleteAsset(path);
            EditorGUI.BeginDisabledGroup(!canDelete);
            bool toggledDanger = GUILayout.Toggle(showDanger, new GUIContent("险", "显示危险操作"), EditorStyles.miniButtonRight, GUILayout.Width(28));
            EditorGUI.EndDisabledGroup();
            if (canDelete && toggledDanger != showDanger)
            {
                newShowDanger = toggledDanger;
                EditorPrefs.SetBool(DANGER_PREF_KEY, toggledDanger);
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawInfoRows(UnityEngine.Object ob, string path, string guid, bool appendMode)
        {
            EditorGUILayout.Space(2);

            DrawCopyRow("GUID", guid, "GUID", appendMode);
            DrawCopyRow("路径", path, "路径", appendMode);
        }

        private static void DrawAssetGuide(UnityEngine.Object ob, string guid)
        {
            EditorGUILayout.Space(2);

            bool hasData = ESGlobalProjectAssetGuideData.TryFindExistingData(out ESGlobalProjectAssetGuideData data);
            ESGlobalProjectAssetGuideData.AssetGuideRecord record = null;
            bool hasRecord = hasData && data.TryGetGuide(guid, out record);

            DrawAssetGuideTitle(hasRecord);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("职责", EditorStyles.boldLabel, GUILayout.Width(34));

            if (hasRecord)
            {
                string title = string.IsNullOrEmpty(record.roleTitle) ? "<未填写>" : record.roleTitle;
                string owner = string.IsNullOrEmpty(record.ownerSystem) ? "未分组" : record.ownerSystem;
                GUILayout.Label($"{owner} / {title}", EditorStyles.boldLabel, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
            }
            else
            {
                GUILayout.Label("未登记职责提示", EditorStyles.miniLabel, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
            }

            GUILayout.FlexibleSpace();

            if (!hasRecord && GUILayout.Button("登记", EditorStyles.miniButton, GUILayout.Width(44)))
            {
                data = ESGlobalProjectAssetGuideData.GetOrCreateData();
                if (data != null)
                {
                    record = data.GetOrCreateGuide(ob);
                    hasRecord = record != null;
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssets();
                }
            }

            if (hasRecord && GUILayout.Button("数据", EditorStyles.miniButton, GUILayout.Width(44)))
            {
                Selection.activeObject = data;
                EditorGUIUtility.PingObject(data);
            }

            EditorGUILayout.EndHorizontal();

            if (!hasRecord)
                return;

            bool editMode = EditorPrefs.GetBool(GUIDE_EDIT_MODE_PREF_KEY, false);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(34);
            GUILayout.FlexibleSpace();
            bool nextEditMode = GUILayout.Toggle(editMode, new GUIContent(editMode ? "编辑" : "展示", "切换职责提示显示/编辑模式"), EditorStyles.miniButton, GUILayout.Width(48));
            if (nextEditMode != editMode)
            {
                editMode = nextEditMode;
                EditorPrefs.SetBool(GUIDE_EDIT_MODE_PREF_KEY, editMode);
            }
            EditorGUILayout.EndHorizontal();

            if (!editMode)
            {
                DrawAssetGuideDisplay(record, data);
                return;
            }

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(34);
            record.ownerSystem = EditorGUILayout.TextField("所属系统", record.ownerSystem);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(34);
            record.roleTitle = EditorGUILayout.TextField("职责标题", record.roleTitle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(34);
            EditorGUILayout.BeginVertical();
            GUILayout.Label("职责提示", EditorStyles.miniBoldLabel);
            record.responsibilityHint = EditorGUILayout.TextArea(record.responsibilityHint, GUILayout.MinHeight(46));
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                record.MarkManuallyEdited();
                EditorUtility.SetDirty(data);
            }
        }

        private static void DrawAssetGuideDisplay(ESGlobalProjectAssetGuideData.AssetGuideRecord record, ESGlobalProjectAssetGuideData data)
        {
            string title = string.IsNullOrWhiteSpace(record.roleTitle) ? "未填写职责标题" : record.roleTitle;
            string owner = string.IsNullOrWhiteSpace(record.ownerSystem) ? "未分配系统" : record.ownerSystem;
            string hint = string.IsNullOrWhiteSpace(record.responsibilityHint) ? "暂无职责提示。" : record.responsibilityHint;

            EditorGUILayout.Space(4);
            Rect rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinHeight(92));

            GUIStyle ownerStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = data != null ? data.displayOwnerColor : new Color(0.45f, 0.85f, 1f, 1f) }
            };
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(data != null ? data.displayTitleFontSize : 22, 14, 36),
                wordWrap = true,
                normal = { textColor = data != null ? data.displayTitleColor : new Color(1f, 0.82f, 0.28f, 1f) }
            };
            GUIStyle hintStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = Mathf.Clamp(data != null ? data.displayHintFontSize : 14, 10, 24),
                wordWrap = true,
                normal = { textColor = data != null ? data.displayHintColor : new Color(0.78f, 1f, 0.78f, 1f) }
            };

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(owner, ownerStyle, GUILayout.Height(18));
            EditorGUILayout.LabelField(title, titleStyle, GUILayout.MinHeight(Mathf.Clamp((data != null ? data.displayTitleFontSize : 22) + 8, 28, 54)));
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(hint, hintStyle);
            EditorGUILayout.Space(4);

            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
            {
                Color oldColor = GUI.color;
                Color titleColor = data != null ? data.displayTitleColor : new Color(1f, 0.82f, 0.28f, 1f);
                GUI.color = new Color(titleColor.r, titleColor.g, titleColor.b, 0.35f);
                GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, 3, rect.height - 2), Texture2D.whiteTexture);
                GUI.color = oldColor;
            }
        }

        private static void DrawAssetGuideTitle(bool hasRecord)
        {
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft,
                normal =
                {
                    textColor = hasRecord ? new Color(0.25f, 0.85f, 0.55f) : new Color(1f, 0.72f, 0.22f)
                }
            };

            EditorGUILayout.LabelField(hasRecord ? "资产职责提示" : "资产职责未登记", style, GUILayout.Height(24));
        }

        private static void DrawCopyRow(string label, string value, string copyLabel, bool appendMode)
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(label, EditorStyles.miniBoldLabel, GUILayout.Width(34));

            if (GUILayout.Button("复制", EditorStyles.miniButton, GUILayout.Width(42), GUILayout.Height(18)))
                CopyToClipboard(value, copyLabel, appendMode);

            Rect valueRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.textField, GUILayout.Height(18), GUILayout.ExpandWidth(true));
            EditorGUI.SelectableLabel(valueRect, value, EditorStyles.textField);

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawTextCopy(string path, FileInfo fileInfo, bool appendMode)
        {
            if (fileInfo.Exists)
            {
                string extension = Path.GetExtension(path).ToLower();

                if (TextExtensions.Contains(extension) && !string.Equals(extension, ".cs"))
                {
                    if (fileInfo.Length > MaxCopyTextBytes)
                    {
                        EditorGUILayout.LabelField("文本内容: 文件过大 (>1MB)，跳过读取", EditorStyles.helpBox);
                    }
                    else
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(34);
                        if (GUILayout.Button($"复制全部文本内容 ({fileInfo.Length / 1024f:F1} KB)", EditorStyles.miniButton, GUILayout.Height(19)))
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
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }

        private static void DrawDangerArea(string path)
        {
            EditorGUILayout.Space(3);
            Color oldBackgroundColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.75f, 0.22f, 0.18f, 0.9f);
            if (GUILayout.Button("删除此资源文件", GUILayout.Height(20)))
            {
                if (!CanDeleteAsset(path))
                {
                    EditorUtility.DisplayDialog("无法删除", $"该路径不允许通过此工具删除:\n{path}", "确定");
                    GUI.backgroundColor = oldBackgroundColor;
                    return;
                }

                bool confirmDelete = EditorUtility.DisplayDialog(
                    "危险操作确认",
                    $"确定要删除以下文件吗？\n\n名称: {Path.GetFileName(path)}\n路径: {path}\n\n此操作无法撤销！",
                    "确认删除",
                    "取消"
                );

                if (confirmDelete)
                {
                    Selection.activeObject = null;
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
            GUI.backgroundColor = oldBackgroundColor;
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

        private static string GetShortPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "<无路径>";

            const int maxLength = 72;
            if (path.Length <= maxLength)
                return path;

            return "..." + path.Substring(path.Length - maxLength);
        }

        private static void CopyToClipboard(string content, string label, bool appendMode)
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
