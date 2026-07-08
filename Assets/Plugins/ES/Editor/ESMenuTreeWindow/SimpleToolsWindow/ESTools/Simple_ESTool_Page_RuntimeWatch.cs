using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    [Serializable]
    public class Page_RuntimeWatch : ESWindowPageBase
    {
        private readonly Dictionary<string, bool> groupFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> ownerFoldouts = new Dictionary<string, bool>();
        private readonly List<WatchEntry> entries = new List<WatchEntry>();

        [Title("运行时观察", "收集场景中带 ESRuntimeWatch 标记的字段", bold: true, titleAlignment: TitleAlignments.Centered)]
        [InfoBox("给 MonoBehaviour 字段添加 [ESRuntimeWatch(\"分组\", \"显示名\")]，运行时可在这里按分组查看当前值。字段元数据由程序集流注册器收集。")]
        [ShowInInspector, LabelText("搜索")]
        private string searchText = "";

        [ShowInInspector, LabelText("自动刷新")]
        private bool autoRefresh = true;

        [ShowInInspector, LabelText("刷新间隔")]
        [MinValue(0.1f)]
        private float refreshInterval = 0.25f;

        private double nextRefreshTime;
        private Vector2 scroll;

        [Button("立即刷新", ButtonHeight = 28), GUIColor(0.35f, 0.65f, 1f)]
        public void RefreshNow()
        {
            CollectEntries();
        }

        [OnInspectorGUI]
        private void DrawRuntimeWatch()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("进入 Play Mode 后显示运行时观察数据。", MessageType.Info);
                if (GUILayout.Button("编辑器下扫描一次"))
                    CollectEntries();
            }

            if (EditorApplication.isPlaying && autoRefresh && EditorApplication.timeSinceStartup >= nextRefreshTime)
            {
                CollectEntries();
                nextRefreshTime = EditorApplication.timeSinceStartup + refreshInterval;
            }

            EditorGUILayout.LabelField($"观察项: {entries.Count}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"已注册字段: {ESRuntimeWatchRegistry.Entries.Count} | 已注册类型: {ESRuntimeWatchRegistry.OwnerTypes.Count}", EditorStyles.miniLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawEntries();
            EditorGUILayout.EndScrollView();

            if (EditorApplication.isPlaying && autoRefresh && SimpleToolsWindow.UsingWindow != null)
                SimpleToolsWindow.UsingWindow.Repaint();
        }

        private void DrawEntries()
        {
            string activeGroup = null;
            string activeOwnerKey = null;
            bool groupVisible = false;
            bool ownerVisible = false;

            foreach (WatchEntry entry in entries)
            {
                if (!MatchesSearch(entry))
                    continue;

                if (activeGroup != entry.Group)
                {
                    activeGroup = entry.Group;
                    if (!groupFoldouts.ContainsKey(activeGroup))
                        groupFoldouts[activeGroup] = true;

                    EditorGUILayout.Space(4);
                    groupFoldouts[activeGroup] = EditorGUILayout.Foldout(groupFoldouts[activeGroup], activeGroup, true);
                    groupVisible = groupFoldouts[activeGroup];
                    activeOwnerKey = null;
                }

                if (!groupVisible)
                    continue;

                string ownerFoldoutKey = entry.Group + "|" + entry.OwnerKey;
                if (activeOwnerKey != ownerFoldoutKey)
                {
                    activeOwnerKey = ownerFoldoutKey;
                    if (!ownerFoldouts.ContainsKey(activeOwnerKey))
                        ownerFoldouts[activeOwnerKey] = true;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(14);
                        ownerFoldouts[activeOwnerKey] = EditorGUILayout.Foldout(ownerFoldouts[activeOwnerKey], entry.OwnerName, true);
                        if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(48)))
                        {
                            Selection.activeObject = entry.Owner;
                            EditorGUIUtility.PingObject(entry.Owner);
                        }
                    }

                    ownerVisible = ownerFoldouts[activeOwnerKey];
                }

                if (!ownerVisible)
                    continue;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(28);
                        EditorGUILayout.LabelField(entry.Label, GUILayout.Width(180));
                        EditorGUILayout.SelectableLabel(entry.ReadValue(), GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    }
                }
            }
        }

        private void CollectEntries()
        {
            entries.Clear();
            IReadOnlyList<ESRuntimeWatchRegistry.Entry> registeredEntries = ESRuntimeWatchRegistry.Entries;
            IReadOnlyList<Type> ownerTypes = ESRuntimeWatchRegistry.OwnerTypes;
            HashSet<string> addedKeys = new HashSet<string>();

            for (int typeIndex = 0; typeIndex < ownerTypes.Count; typeIndex++)
            {
                Type ownerType = ownerTypes[typeIndex];
                UnityEngine.Object[] owners = UnityEngine.Object.FindObjectsByType(ownerType, FindObjectsSortMode.None);
                for (int ownerIndex = 0; ownerIndex < owners.Length; ownerIndex++)
                {
                    MonoBehaviour behaviour = owners[ownerIndex] as MonoBehaviour;
                    if (behaviour == null)
                        continue;

                    Type behaviourType = behaviour.GetType();
                    for (int entryIndex = 0; entryIndex < registeredEntries.Count; entryIndex++)
                    {
                        ESRuntimeWatchRegistry.Entry registeredEntry = registeredEntries[entryIndex];
                        if (!registeredEntry.OwnerType.IsAssignableFrom(behaviourType))
                            continue;

                        string key = behaviour.GetInstanceID() + "|" + registeredEntry.FieldInfo.Module.ModuleVersionId + "|" + registeredEntry.FieldInfo.MetadataToken;
                        if (!addedKeys.Add(key))
                            continue;

                        entries.Add(WatchEntry.FromRegistryEntry(behaviour, registeredEntry));
                    }
                }
            }

            entries.Sort((a, b) =>
            {
                int group = string.Compare(a.Group, b.Group, StringComparison.Ordinal);
                if (group != 0) return group;
                int owner = string.Compare(a.OwnerName, b.OwnerName, StringComparison.Ordinal);
                return owner != 0 ? owner : string.Compare(a.Label, b.Label, StringComparison.Ordinal);
            });
        }

        private bool MatchesSearch(WatchEntry entry)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            return entry.Group.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                   || entry.OwnerName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                   || entry.Label.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                   || entry.OwnerTypeName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private class WatchEntry
        {
            public UnityEngine.Object Owner;
            public string OwnerName;
            public string OwnerKey;
            public string OwnerTypeName;
            public string Group;
            public string Label;
            private Func<string> readValue;

            public static WatchEntry FromRegistryEntry(MonoBehaviour owner, ESRuntimeWatchRegistry.Entry entry)
            {
                return Create(owner, entry.FieldInfo.Name, entry.OwnerType.Name, entry.Attribute, () => entry.FieldInfo.GetValue(owner));
            }

            private static WatchEntry Create(MonoBehaviour owner, string memberName, string ownerTypeName, ESRuntimeWatchAttribute attribute, Func<object> getter)
            {
                return new WatchEntry
                {
                    Owner = owner,
                    OwnerName = BuildOwnerPath(owner),
                    OwnerKey = owner.GetInstanceID() + "|" + ownerTypeName,
                    OwnerTypeName = ownerTypeName,
                    Group = string.IsNullOrEmpty(attribute.Group) ? "Default" : attribute.Group,
                    Label = string.IsNullOrEmpty(attribute.Label) ? memberName : attribute.Label,
                    readValue = () =>
                    {
                        try
                        {
                            object value = getter();
                            return value == null ? "null" : value.ToString();
                        }
                        catch (Exception e)
                        {
                            return "<读取失败: " + e.Message + ">";
                        }
                    }
                };
            }

            public string ReadValue()
            {
                return readValue == null ? "" : readValue();
            }

            private static string BuildOwnerPath(MonoBehaviour owner)
            {
                Transform current = owner.transform;
                string path = current.name;
                while (current.parent != null)
                {
                    current = current.parent;
                    path = current.name + "/" + path;
                }

                return path + " (" + owner.GetType().Name + ")";
            }
        }
    }
}
