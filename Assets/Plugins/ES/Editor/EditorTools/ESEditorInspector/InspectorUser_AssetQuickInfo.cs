using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using ES.EditorInternal;
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

        private static GUIStyle assetGuideOwnerStyle;
        private static GUIStyle assetGuideTitleStyle;
        private static GUIStyle assetGuideHintStyle;
        private static GUIStyle assetGuideHeadingStyle;

        private static readonly HashSet<string> TextExtensions = new HashSet<string>
        {
            ".txt", ".csv", ".json", ".xml", ".lua", ".cs", ".js", ".shader", ".cginc",
            ".hlsl", ".md", ".yml", ".yaml", ".ini", ".bat", ".sh", ".html", ".css", ".xaml"
        };

        public override bool Apply(ESEditorInspectorContext context)
        {
            if (context.Targets == null || context.Targets.Count != 1)
                return false;

            UnityEngine.Object ob = context.Target;
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
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle))
            {
                DrawAssetInspectorTitle();
                DrawHeader(ob, path, appendMode, showDanger, out appendMode, out showDanger);
                DrawInfoRows(ob, path, guid, appendMode);
                DrawAssetGuide(ob, guid);
                DrawAssetRegistryKeys(ob, path, guid);
                DrawTextCopy(path, fileInfo, appendMode);

                if (showDanger && CanDeleteAsset(path))
                    DrawDangerArea(path);
            }

            return false;
        }

        private static void DrawAssetInspectorTitle()
        {
            GUILayout.Label("资产快速信息", ESEditorPresentation.HeaderStyle);
            Rect dividerRect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            ESEditorPresentation.DrawDivider(dividerRect);
            EditorGUILayout.Space(3f);
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

            GUILayout.Label("资源", ESEditorPresentation.HeaderStyle, GUILayout.Width(34));
            GUILayout.Label(GetShortPath(path), ESEditorPresentation.MetaStyle, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Ping", ESEditorInspectorControls.ButtonLeft, GUILayout.Width(38)))
                EditorGUIUtility.PingObject(ob);

            if (GUILayout.Button(EditorGUIUtility.IconContent("Folder Icon"), ESEditorInspectorControls.ButtonMid, GUILayout.Width(26)))
                EditorUtility.RevealInFinder(path);

            bool toggledAppend = GUILayout.Toggle(appendMode, new GUIContent("追", "追加复制模式"), ESEditorInspectorControls.ButtonMid, GUILayout.Width(28));
            if (toggledAppend != appendMode)
            {
                newAppendMode = toggledAppend;
                EditorPrefs.SetBool(APPEND_PREF_KEY, toggledAppend);
            }

            bool canDelete = CanDeleteAsset(path);
            EditorGUI.BeginDisabledGroup(!canDelete);
            bool toggledDanger = GUILayout.Toggle(showDanger, new GUIContent("险", "显示危险操作"), ESEditorInspectorControls.ButtonRight, GUILayout.Width(28));
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
            GUILayout.Label("职责", ESEditorPresentation.HeaderStyle, GUILayout.Width(34));

            if (hasRecord)
            {
                string title = string.IsNullOrEmpty(record.roleTitle) ? "<未填写>" : record.roleTitle;
                string owner = string.IsNullOrEmpty(record.ownerSystem) ? "未分组" : record.ownerSystem;
                GUILayout.Label($"{owner} / {title}", ESEditorPresentation.HeaderStyle, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
            }
            else
            {
                GUILayout.Label("未登记职责提示", ESEditorPresentation.MetaStyle, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
            }

            GUILayout.FlexibleSpace();

            if (!hasRecord && GUILayout.Button("登记", ESEditorInspectorControls.Button, GUILayout.Width(44)))
            {
                data = ESGlobalProjectAssetGuideData.GetOrCreateData();
                if (data != null)
                {
                    record = data.GetOrCreateGuide(ob);
                    hasRecord = record != null;
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssetIfDirty(data);
                }
            }

            if (hasRecord && GUILayout.Button("数据", ESEditorInspectorControls.Button, GUILayout.Width(44)))
            {
                Selection.activeObject = data;
                EditorGUIUtility.PingObject(data);
            }

            if (hasRecord && GUILayout.Button("复制职责", ESEditorInspectorControls.Button, GUILayout.Width(66)))
            {
                bool appendMode = EditorPrefs.GetBool(APPEND_PREF_KEY, false);
                CopyToClipboard(BuildAssetGuideClipboardText(record), "资产职责", appendMode);
            }

            EditorGUILayout.EndHorizontal();

            if (!hasRecord)
                return;

            bool editMode = EditorPrefs.GetBool(GUIDE_EDIT_MODE_PREF_KEY, false);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(34);
            GUILayout.FlexibleSpace();
            bool nextEditMode = GUILayout.Toggle(editMode, new GUIContent(editMode ? "编辑" : "展示", "切换职责提示显示/编辑模式"), ESEditorInspectorControls.Button, GUILayout.Width(48));
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
            string nextOwnerSystem = record.ownerSystem;
            string nextRoleTitle = record.roleTitle;
            string nextResponsibilityHint = record.responsibilityHint;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(34);
            GUILayout.Label("所属系统", ESEditorPresentation.MetaStyle, GUILayout.Width(64));
            nextOwnerSystem = EditorGUILayout.TextField(
                nextOwnerSystem,
                ESEditorInspectorControls.TextField,
                GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(34);
            GUILayout.Label("职责标题", ESEditorPresentation.MetaStyle, GUILayout.Width(64));
            nextRoleTitle = EditorGUILayout.TextField(
                nextRoleTitle,
                ESEditorInspectorControls.TextField,
                GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(34);
            EditorGUILayout.BeginVertical();
            GUILayout.Label("职责提示", ESEditorPresentation.MetaStyle);
            nextResponsibilityHint = EditorGUILayout.TextArea(
                nextResponsibilityHint,
                ESEditorInspectorControls.TextArea,
                GUILayout.MinHeight(46));
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "编辑资产职责提示");
                record.ownerSystem = nextOwnerSystem;
                record.roleTitle = nextRoleTitle;
                record.responsibilityHint = nextResponsibilityHint;
                record.MarkManuallyEdited();
                EditorUtility.SetDirty(data);
            }
        }

        private static void DrawAssetRegistryKeys(UnityEngine.Object asset, string path, string guid)
        {
            if (asset is MonoScript)
                return;

            ESAssetReferKind kind = ESAssetPage.DetermineKind(asset);
            if (!ESAssetReferConfigKeySwitch.IsSupportedKind(kind))
                return;

            EditorGUILayout.Space(5);
            Color previousBackgroundColor = GUI.backgroundColor;
            Color registryPanelColor = ESEditorPresentation.LogicSteelBlue;
            registryPanelColor.a = 0.30f;
            GUI.backgroundColor = registryPanelColor;

            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle))
            {
                GUI.backgroundColor = previousBackgroundColor;
                ESAssetPage page = null;
                bool registered = !string.IsNullOrEmpty(guid) && ESAssetRegistry.TryGetByGuid(guid, out page);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("资源注册键", ESEditorPresentation.HeaderStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label(registered ? $"已注册 · {kind}" : $"未注册 · {kind}", ESEditorPresentation.MetaStyle);
                EditorGUILayout.EndHorizontal();

                if (!registered)
                {
                    EditorGUILayout.HelpBox("当前资产尚未进入资源注册表。点击后进入统一预检，确认稳定 Key 后才能提交。", MessageType.Info);
                    if (GUILayout.Button("注册当前资产", ESEditorInspectorControls.Button, GUILayout.Height(22)))
                    {
                        RegisterCurrentAsset(asset);
                        GUIUtility.ExitGUI();
                    }
                    return;
                }

                DrawRegisteredAssetKeys(asset, page, kind);
            }

            GUI.backgroundColor = previousBackgroundColor;
        }

        private static void DrawRegisteredAssetKeys(UnityEngine.Object asset, ESAssetPage page, ESAssetReferKind kind)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("String Key", ESEditorPresentation.MetaStyle, GUILayout.Width(70));
            string nextStringKey = EditorGUILayout.DelayedTextField(
                page.StringKey ?? string.Empty,
                ESEditorInspectorControls.TextField,
                GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
            if (!string.Equals(nextStringKey, page.StringKey, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(nextStringKey))
                {
                    Debug.LogWarning("[资源注册键] String Key 不能为空。", asset);
                }
                else
                {
                    ESResourceCollectionWorkflowWindow.OpenForAssetKeyUpdate(page, page.EnumKey, nextStringKey);
                }
            }

            Type enumType = GetAssetEnumType(kind);
            if (enumType == null)
            {
                EditorGUILayout.HelpBox($"资产类型 {kind} 没有绑定枚举类型，无法编辑 Enum Key。", MessageType.Warning);
            }
            else
            {
                Enum current = (Enum)Enum.ToObject(enumType, page.EnumKey);
                Enum selected = EditorGUILayout.EnumPopup(
                    new GUIContent("Enum Key"),
                    current,
                    ESEditorInspectorControls.Popup,
                    GUILayout.ExpandWidth(true));
                int nextEnumKey = Convert.ToInt32(selected);
                if (nextEnumKey != page.EnumKey)
                {
                    ESResourceCollectionWorkflowWindow.OpenForAssetKeyUpdate(page, nextEnumKey, page.EffectiveStringKey);
                }
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();

            if (enumType != null && GUILayout.Button("定位枚举成员", ESEditorInspectorControls.ButtonLeft, GUILayout.ExpandWidth(true)))
            {
                string memberName = Enum.GetName(enumType, page.EnumKey);
                if (string.IsNullOrEmpty(memberName))
                    ESEnumScriptJump.OpenEnumAppendPosition(enumType);
                else
                    ESEnumScriptJump.OpenEnumMember(enumType, memberName);
            }
            if (enumType != null && GUILayout.Button("枚举扩容", ESEditorInspectorControls.ButtonRight, GUILayout.ExpandWidth(true)))
                ESEnumScriptJump.OpenEnumAppendPosition(enumType);
            EditorGUILayout.EndHorizontal();
        }

        private static void RegisterCurrentAsset(UnityEngine.Object asset)
        {
            ESResourceCollectionWorkflowWindow.OpenForAssetRegistration(asset);
        }

        private static Type GetAssetEnumType(ESAssetReferKind kind)
        {
            switch (kind)
            {
                case ESAssetReferKind.Prefab: return typeof(ESAssetReferPrefabEnumKey);
                case ESAssetReferKind.Scene: return typeof(ESAssetReferSceneEnumKey);
                case ESAssetReferKind.Sprite: return typeof(ESAssetReferSpriteEnumKey);
                case ESAssetReferKind.SpriteAtlas: return typeof(ESAssetReferSpriteAtlasEnumKey);
                case ESAssetReferKind.Texture2D: return typeof(ESAssetReferTexture2DEnumKey);
                case ESAssetReferKind.Texture: return typeof(ESAssetReferTextureEnumKey);
                case ESAssetReferKind.Material: return typeof(ESAssetReferMaterialEnumKey);
                case ESAssetReferKind.Mesh: return typeof(ESAssetReferMeshEnumKey);
                case ESAssetReferKind.AnimationClip: return typeof(ESAssetReferAnimationClipEnumKey);
                case ESAssetReferKind.AnimatorController: return typeof(ESAssetReferAnimatorControllerEnumKey);
                case ESAssetReferKind.Avatar: return typeof(ESAssetReferAvatarEnumKey);
                case ESAssetReferKind.AudioClip: return typeof(ESAssetReferAudioClipEnumKey);
                case ESAssetReferKind.VideoClip: return typeof(ESAssetReferVideoClipEnumKey);
                case ESAssetReferKind.TimelineAsset: return typeof(ESAssetReferTimelineAssetEnumKey);
                case ESAssetReferKind.PlayableAsset: return typeof(ESAssetReferPlayableAssetEnumKey);
                case ESAssetReferKind.TerrainData: return typeof(ESAssetReferTerrainDataEnumKey);
                case ESAssetReferKind.Raw: return typeof(ESAssetReferRawEnumKey);
                default: return null;
            }
        }

        private static string BuildAssetGuideClipboardText(ESGlobalProjectAssetGuideData.AssetGuideRecord record)
        {
            if (record == null)
                return string.Empty;

            StringBuilder builder = new StringBuilder(512);
            builder.AppendLine("【资产职责】");
            AppendClipboardLine(builder, "所属系统", record.ownerSystem);
            AppendClipboardLine(builder, "职责标题", record.roleTitle);
            AppendClipboardLine(builder, "职责提示", record.responsibilityHint);

            if (!string.IsNullOrWhiteSpace(record.readMe))
                AppendClipboardLine(builder, "ReadMe", record.readMe);

            if (record.tags != null && record.tags.Count > 0)
                AppendClipboardLine(builder, "标签", string.Join(", ", record.tags));

            AppendClipboardLine(builder, "资源路径", record.assetPath);
            AppendClipboardLine(builder, "资源名称", record.assetName);
            AppendClipboardLine(builder, "资源类型", record.assetTypeName);
            AppendClipboardLine(builder, "GUID", record.guid);

            return builder.ToString().TrimEnd();
        }

        private static void AppendClipboardLine(StringBuilder builder, string label, string value)
        {
            builder.Append(label);
            builder.Append("：");
            builder.AppendLine(string.IsNullOrWhiteSpace(value) ? "<未填写>" : value.Trim());
        }

        private static void DrawAssetGuideDisplay(ESGlobalProjectAssetGuideData.AssetGuideRecord record, ESGlobalProjectAssetGuideData data)
        {
            string title = string.IsNullOrWhiteSpace(record.roleTitle) ? "未填写职责标题" : record.roleTitle;
            string owner = string.IsNullOrWhiteSpace(record.ownerSystem) ? "未分配系统" : record.ownerSystem;
            string hint = string.IsNullOrWhiteSpace(record.responsibilityHint) ? "暂无职责提示。" : record.responsibilityHint;

            EditorGUILayout.Space(4);
            Rect rect = EditorGUILayout.BeginVertical(ESEditorPresentation.SurfaceStyle, GUILayout.MinHeight(92));

            GUIStyle ownerStyle = GetAssetGuideOwnerStyle(data);
            GUIStyle titleStyle = GetAssetGuideTitleStyle(data);
            GUIStyle hintStyle = GetAssetGuideHintStyle(data);

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
                Color titleColor = data != null ? data.displayTitleColor : ESEditorPresentation.SectionSelectedTextColor;
                Color accent = titleColor;
                accent.a = 0.35f;
                GUI.color = accent;
                GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, 3, rect.height - 2), Texture2D.whiteTexture);
                GUI.color = oldColor;
            }
        }

        private static void DrawAssetGuideTitle(bool hasRecord)
        {
            GUIStyle style = GetAssetGuideHeadingStyle(hasRecord);

            EditorGUILayout.LabelField(hasRecord ? "资产职责提示" : "资产职责未登记", style, GUILayout.Height(24));
        }

        private static GUIStyle GetAssetGuideOwnerStyle(ESGlobalProjectAssetGuideData data)
        {
            if (assetGuideOwnerStyle == null)
            {
                assetGuideOwnerStyle = new GUIStyle(ESEditorPresentation.MetaStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12
                };
            }

            assetGuideOwnerStyle.normal.textColor = data != null
                ? data.displayOwnerColor
                : ESEditorPresentation.SectionMutedTextColor;
            return assetGuideOwnerStyle;
        }

        private static GUIStyle GetAssetGuideTitleStyle(ESGlobalProjectAssetGuideData data)
        {
            if (assetGuideTitleStyle == null)
            {
                assetGuideTitleStyle = new GUIStyle(ESEditorPresentation.HeaderStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
            }

            assetGuideTitleStyle.fontSize = Mathf.Clamp(data != null ? data.displayTitleFontSize : 22, 14, 36);
            assetGuideTitleStyle.normal.textColor = data != null
                ? data.displayTitleColor
                : ESEditorPresentation.SectionSelectedTextColor;
            return assetGuideTitleStyle;
        }

        private static GUIStyle GetAssetGuideHintStyle(ESGlobalProjectAssetGuideData data)
        {
            if (assetGuideHintStyle == null)
            {
                assetGuideHintStyle = new GUIStyle(ESEditorPresentation.SubtitleStyle)
                {
                    alignment = TextAnchor.UpperCenter,
                    wordWrap = true
                };
            }

            assetGuideHintStyle.fontSize = Mathf.Clamp(data != null ? data.displayHintFontSize : 14, 10, 24);
            assetGuideHintStyle.normal.textColor = data != null
                ? data.displayHintColor
                : ESEditorPresentation.SectionTextColor;
            return assetGuideHintStyle;
        }

        private static GUIStyle GetAssetGuideHeadingStyle(bool hasRecord)
        {
            if (assetGuideHeadingStyle == null)
            {
                assetGuideHeadingStyle = new GUIStyle(ESEditorPresentation.HeaderStyle)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleLeft
                };
            }

            assetGuideHeadingStyle.normal.textColor = hasRecord
                ? ESEditorPresentation.SectionSelectedTextColor
                : ESEditorPresentation.LogicGold;
            return assetGuideHeadingStyle;
        }

        private static void DrawCopyRow(string label, string value, string copyLabel, bool appendMode)
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(label, ESEditorPresentation.MetaStyle, GUILayout.Width(34));

            if (GUILayout.Button("复制", ESEditorInspectorControls.Button, GUILayout.Width(42), GUILayout.Height(18)))
                CopyToClipboard(value, copyLabel, appendMode);

            Rect valueRect = GUILayoutUtility.GetRect(GUIContent.none, ESEditorInspectorControls.TextField, GUILayout.Height(18), GUILayout.ExpandWidth(true));
            EditorGUI.SelectableLabel(valueRect, value, ESEditorInspectorControls.TextField);

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
                        EditorGUILayout.LabelField("文本内容: 文件过大 (>1MB)，跳过读取", ESEditorPresentation.MetaStyle);
                    }
                    else
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(34);
                        if (GUILayout.Button($"复制全部文本内容 ({fileInfo.Length / 1024f:F1} KB)", ESEditorInspectorControls.Button, GUILayout.Height(19)))
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
            GUI.backgroundColor = ESEditorPresentation.WarningBackground;
            if (GUILayout.Button("删除此资源文件", ESEditorInspectorControls.Button, GUILayout.Height(20)))
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
            string normalized = path.Replace('\\', '/').Trim();
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(normalized))) return false;

            return !AssetDatabase.IsValidFolder(normalized);
        }

        private static string ReadTextWithFallback(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("文件路径为空。");

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length > MaxCopyTextBytes)
                    throw new InvalidOperationException("文件超过 1MB 读取上限。");

                int length = checked((int)stream.Length);
                byte[] bytes = new byte[length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0) break;
                    offset += read;
                }
                if (offset != bytes.Length)
                    Array.Resize(ref bytes, offset);

                try
                {
                    return new UTF8Encoding(false, true).GetString(bytes);
                }
                catch (DecoderFallbackException)
                {
                    return Encoding.Default.GetString(bytes);
                }
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

            try
            {
                if (appendMode)
                {
                    string currentBuffer = EditorGUIUtility.systemCopyBuffer;
                    if (!string.IsNullOrEmpty(currentBuffer)
                        && currentBuffer.Length > MaxCopyTextBytes)
                    {
                        Debug.LogWarning($"追加复制 {label} 失败：现有剪贴板内容超过 1MB 上限。");
                        return;
                    }
                    if (!string.IsNullOrEmpty(currentBuffer))
                        finalContent = currentBuffer + "\n" + content;
                }

                if (finalContent.Length > MaxCopyTextBytes)
                {
                    Debug.LogWarning($"复制 {label} 失败：合并后的内容超过 1MB 上限。");
                    return;
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
            catch (Exception exception)
            {
                Debug.LogWarning($"复制 {label} 失败：{exception.Message}");
            }
        }
    }

    /// <summary>
    /// Cached IMGUI control styles for ES inspector panels. Styles are created once and reused,
    /// so hot drawing paths do not allocate new GUIStyle objects.
    /// </summary>
    internal static class ESEditorInspectorControls
    {
        private static GUIStyle buttonStyle;
        private static GUIStyle buttonLeftStyle;
        private static GUIStyle buttonMidStyle;
        private static GUIStyle buttonRightStyle;
        private static GUIStyle textFieldStyle;
        private static GUIStyle toolbarTextFieldStyle;
        private static GUIStyle textAreaStyle;
        private static GUIStyle popupStyle;
        private static int cachedStyleSkinGeneration = -1;

        public static GUIStyle Button
        {
            get { return GetButtonStyle(ref buttonStyle, EditorStyles.miniButton); }
        }

        public static GUIStyle ButtonLeft
        {
            get { return GetButtonStyle(ref buttonLeftStyle, EditorStyles.miniButtonLeft); }
        }

        public static GUIStyle ButtonMid
        {
            get { return GetButtonStyle(ref buttonMidStyle, EditorStyles.miniButtonMid); }
        }

        public static GUIStyle ButtonRight
        {
            get { return GetButtonStyle(ref buttonRightStyle, EditorStyles.miniButtonRight); }
        }

        public static GUIStyle TextField
        {
            get { return GetTextFieldStyle(ref textFieldStyle, EditorStyles.textField); }
        }

        public static GUIStyle ToolbarTextField
        {
            get { return GetTextFieldStyle(ref toolbarTextFieldStyle, EditorStyles.toolbarTextField); }
        }

        public static GUIStyle TextArea
        {
            get { return GetTextFieldStyle(ref textAreaStyle, EditorStyles.textArea); }
        }

        public static GUIStyle Popup
        {
            get { return GetButtonStyle(ref popupStyle, EditorStyles.popup); }
        }

        private static GUIStyle GetButtonStyle(ref GUIStyle cachedStyle, GUIStyle source)
        {
            EnsureStyleGeneration();
            if (cachedStyle == null)
                cachedStyle = CreateButtonStyle(source);

            return cachedStyle;
        }

        private static GUIStyle GetTextFieldStyle(ref GUIStyle cachedStyle, GUIStyle source)
        {
            EnsureStyleGeneration();
            if (cachedStyle == null)
                cachedStyle = CreateTextFieldStyle(source);

            return cachedStyle;
        }

        private static void EnsureStyleGeneration()
        {
            int currentGeneration = ESEditorPresentation.SkinGeneration;
            if (cachedStyleSkinGeneration == currentGeneration)
                return;

            cachedStyleSkinGeneration = currentGeneration;
            buttonStyle = null;
            buttonLeftStyle = null;
            buttonMidStyle = null;
            buttonRightStyle = null;
            textFieldStyle = null;
            toolbarTextFieldStyle = null;
            textAreaStyle = null;
            popupStyle = null;
        }

        private static GUIStyle CreateButtonStyle(GUIStyle source)
        {
            var style = new GUIStyle(source)
            {
                normal = { textColor = ESEditorPresentation.SectionTextColor },
                hover = { textColor = ESEditorPresentation.SectionSelectedTextColor },
                active = { textColor = ESEditorPresentation.SelectedTextColor },
                focused = { textColor = ESEditorPresentation.SectionSelectedTextColor }
            };

            Texture2D background = ESEditorPresentation.SurfaceStyle.normal.background;
            style.normal.background = background;
            style.hover.background = background;
            return style;
        }

        private static GUIStyle CreateTextFieldStyle(GUIStyle source)
        {
            var style = new GUIStyle(source)
            {
                normal = { textColor = ESEditorPresentation.SectionTextColor },
                focused = { textColor = ESEditorPresentation.SectionSelectedTextColor }
            };

            style.normal.background = ESEditorPresentation.SurfaceStyle.normal.background;
            return style;
        }
    }
}
