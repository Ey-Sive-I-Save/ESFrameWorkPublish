#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>Runtime-only inspector for the Entity float-stat surface.</summary>
    public sealed class EntityStatDebugWindow : EditorWindow
    {
        private readonly List<ESFloatStatDebugEntry> entries = new List<ESFloatStatDebugEntry>(24);
        private readonly List<ESFloatStatModifierSnapshot> modifiers = new List<ESFloatStatModifierSnapshot>(8);

        private Entity boundEntity;
        private Vector2 scrollPosition;
        private ushort expandedEnumKey;
        private string expandedStringKey;
        private bool hasExpandedEntry;
        private bool autoRefresh = true;
        private double nextRefreshTime;

        [MenuItem(MenuItemPathDefine.STAT_RUNTIME_PANEL_PATH, false, 0)]
        public static void Open()
        {
            EntityStatDebugWindow window = GetWindow<EntityStatDebugWindow>("Entity Stat Monitor");
            window.minSize = new Vector2(620f, 380f);
            window.TryBindSelection(force: false);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnSelectionChange()
        {
            if (boundEntity == null)
                TryBindSelection(force: false);
        }

        private void OnEditorUpdate()
        {
            if (!autoRefresh || boundEntity == null || !Application.isPlaying)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now < nextRefreshTime)
                return;

            nextRefreshTime = now + 0.1d;
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            if (boundEntity == null)
            {
                EditorGUILayout.HelpBox("请选择运行中的 Entity，然后点击“绑定当前选中”。", MessageType.Info);
                return;
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("该面板显示运行时数值。进入 Play Mode 后可观察实时 Modifier 与最终值。", MessageType.Warning);

            DrawCatalogState();
            boundEntity.CopyFloatStatDebugEntriesTo(entries);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < entries.Count; i++)
                DrawEntry(entries[i]);
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("绑定当前选中", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                TryBindSelection(force: true);

            if (GUILayout.Button("清除绑定", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                boundEntity = null;
                entries.Clear();
                modifiers.Clear();
                expandedEnumKey = 0;
                expandedStringKey = null;
                hasExpandedEntry = false;
            }

            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                Repaint();

            GUILayout.FlexibleSpace();
            autoRefresh = GUILayout.Toggle(autoRefresh, "实时刷新", EditorStyles.toolbarButton, GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCatalogState()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Entity", boundEntity, typeof(Entity), true);

            ESSuperAttributeCatalog catalog = boundEntity.SuperAttributeCatalog;
            EditorGUILayout.LabelField("属性 Schema", catalog != null ? catalog.SchemaHash : "未绑定");
            if (!string.IsNullOrEmpty(boundEntity.SuperAttributeCatalogError))
                EditorGUILayout.HelpBox(boundEntity.SuperAttributeCatalogError, MessageType.Error);
        }

        private void DrawEntry(ESFloatStatDebugEntry entry)
        {
            ESFloatStatSnapshot stat = entry.stat;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            string title = !string.IsNullOrEmpty(entry.displayName)
                ? entry.displayName
                : !string.IsNullOrEmpty(entry.stringKey)
                    ? entry.stringKey
                    : "Enum " + entry.enumKey;
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(entry.storagePolicy.ToString(), GUILayout.Width(72f));
            EditorGUILayout.LabelField(entry.isMaterialized ? "运行中" : "仅定义", GUILayout.Width(52f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Stable Key", string.IsNullOrEmpty(entry.stringKey) ? "Enum-only" : entry.stringKey);
            EditorGUILayout.LabelField("EnumKey / RuntimeKey", entry.enumKey + " / " + entry.runtimeKey);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.FloatField("基础", stat.baseValue);
            EditorGUILayout.FloatField("最终", stat.value);
            EditorGUILayout.IntField("修正数", stat.changeCount);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.FloatField("Add", stat.additiveValue);
            EditorGUILayout.FloatField("Percent", stat.addedPercent);
            EditorGUILayout.FloatField("Multiply", stat.multiplyValue);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.FloatField("定义最小", stat.definitionMinimum);
            EditorGUILayout.FloatField("定义最大", stat.definitionMaximum);
            EditorGUILayout.EndHorizontal();

            if (stat.hasOverride)
                EditorGUILayout.LabelField("Override", stat.overrideValue + "  Priority=" + stat.overridePriority + "  Order=" + stat.overrideOrder);
            if (stat.hasModifierMinimum || stat.hasModifierMaximum)
                EditorGUILayout.LabelField("运行时上下界", (stat.hasModifierMinimum ? stat.modifierMinimum.ToString() : "-")
                                                              + " / "
                                                              + (stat.hasModifierMaximum ? stat.modifierMaximum.ToString() : "-"));

            bool expanded = hasExpandedEntry
                            && expandedEnumKey == entry.enumKey
                            && string.Equals(expandedStringKey, entry.stringKey);
            bool nextExpanded = EditorGUILayout.Foldout(expanded, "修正明细", true);
            if (nextExpanded != expanded)
            {
                hasExpandedEntry = nextExpanded;
                expandedEnumKey = nextExpanded ? entry.enumKey : (ushort)0;
                expandedStringKey = nextExpanded ? entry.stringKey : null;
            }

            if (nextExpanded)
                DrawModifiers(entry);

            EditorGUILayout.EndVertical();
        }

        private void DrawModifiers(ESFloatStatDebugEntry entry)
        {
            ESFloatValueChangeSet set = null;
            if (entry.storagePolicy == ESKeyStoragePolicy.HotSlot
                && ESCharacterAttributeCatalog.TryGetFloatId(entry.enumKey, out ESCharacterFloatAttributeId characterId))
            {
                boundEntity.TryGetCharacterFloatStat(characterId, out set);
            }
            else
            {
                boundEntity.TryGetFloatStat(entry.runtimeKey, out set);
            }

            if (set == null)
            {
                EditorGUILayout.LabelField("当前没有活动修正。该属性尚未实例化 ValueChange 容器。");
                return;
            }

            set.CopyDebugModifiersTo(modifiers);
            if (modifiers.Count == 0)
            {
                EditorGUILayout.LabelField("当前没有活动修正。");
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                ESFloatStatModifierSnapshot modifier = modifiers[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(modifier.operation.ToString(), GUILayout.Width(78f));
                EditorGUILayout.FloatField(modifier.value, GUILayout.Width(82f));
                EditorGUILayout.LabelField("Owner " + modifier.ownerId, GUILayout.Width(78f));
                EditorGUILayout.LabelField("Source " + modifier.sourceId, GUILayout.Width(82f));
                EditorGUILayout.LabelField("P " + modifier.priority, GUILayout.Width(48f));
                if (modifier.isWinningOverride)
                    EditorGUILayout.LabelField("生效 Override", GUILayout.Width(84f));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void TryBindSelection(bool force)
        {
            if (!force && boundEntity != null)
                return;

            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                boundEntity = null;
                return;
            }

            boundEntity = selected.GetComponent<Entity>();
            if (boundEntity == null)
                boundEntity = selected.GetComponentInParent<Entity>();
            if (boundEntity == null)
                boundEntity = selected.GetComponentInChildren<Entity>(true);
        }
    }
}
#endif
