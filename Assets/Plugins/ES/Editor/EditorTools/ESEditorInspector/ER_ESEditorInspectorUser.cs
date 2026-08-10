using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace ES
{
    public class ER_ESEditorInspectorUser : EditorRegister_FOR_Singleton<ESEditorInspectorUser>
    {
        public override int Order => EditorRegisterOrder.Level1.GetHashCode();
        private static bool init = true;
        private static ER_ESEditorInspectorUser activeRegister;
        Comparer<ESEditorInspectorUser> comp;
        public List<ESEditorInspectorUser> users = new List<ESEditorInspectorUser>();
        public override void Handle(ESEditorInspectorUser singleton)
        {
            activeRegister = this;
            if (init)
            {
                init = false;
                comp = Comparer<ESEditorInspectorUser>.Create((a, b) => a.Order - b.Order);
                UnityEditor.Editor.finishedDefaultHeaderGUI -= OnFinishedDefaultHeaderGUI;
                UnityEditor.Editor.finishedDefaultHeaderGUI += OnFinishedDefaultHeaderGUI;
            }
            if (singleton != null && !users.Exists(user => user != null && user.GetType() == singleton.GetType()))
                users.Add(singleton);
            users.Sort(comp);
        }

        private static void OnFinishedDefaultHeaderGUI(UnityEditor.Editor ed)
        {
            if (activeRegister == null || ed == null || ed.targets == null || ed.targets.Length == 0)
                return;

            var target = ed.targets[0];

            // Import Settings 与 Imported Object 会分别创建 Editor，并触发两次 Header 回调。
            // 有实际主资产时只在 Imported Object 上绘制，确保信息区只出现一次，且类型判断拿到真实资产。
            if (target is AssetImporter importer &&
                !string.IsNullOrEmpty(importer.assetPath) &&
                AssetDatabase.LoadMainAssetAtPath(importer.assetPath) != null)
            {
                return;
            }

            ESEditorInspectorContext context = BuildContext(ed);
            foreach (var user in activeRegister.users)
            {
                if (user.Apply(context))
                    break;
            }
        }

        private static ESEditorInspectorContext BuildContext(UnityEditor.Editor ed)
        {
            EditorWindow host = GetCurrentEditorWindow();
            if (host == null || !IsInspectorHost(host))
                host = GetFallbackInspectorWindow();
            EventType eventType = Event.current != null ? Event.current.type : EventType.Repaint;
            return new ESEditorInspectorContext(
                ed,
                ed.targets,
                eventType,
                host != null ? host.GetInstanceID() : ed.GetInstanceID(),
                ClassifyContext(ed, ed.targets, host));
        }

        private static ESEditorInspectorContextKind ClassifyContext(
            UnityEditor.Editor ed,
            UnityEngine.Object[] targets,
            EditorWindow host)
        {
            if (targets == null || targets.Length == 0)
                return ESEditorInspectorContextKind.Other;

            UnityEngine.Object target = targets[0];
            if (target is GameObject)
            {
                return ESEditorInspectorContextKind.GameObjectMainHeader;
            }

            if (target is Component component)
            {
                return IsInspectorHost(host) && HasGameObjectRootEditor(host)
                    ? ESEditorInspectorContextKind.ComponentInGameObjectInspector
                    : ESEditorInspectorContextKind.StandaloneComponentInspector;
            }

            return ESEditorInspectorContextKind.Other;
        }

        private static EditorWindow GetFallbackInspectorWindow()
        {
            EditorWindow window = EditorWindow.mouseOverWindow ?? EditorWindow.focusedWindow;
            return IsInspectorHost(window) ? window : null;
        }

        private static EditorWindow GetCurrentEditorWindow()
        {
            try
            {
                Assembly editorAssembly = typeof(UnityEditor.Editor).Assembly;
                Type guiViewType = editorAssembly.GetType("UnityEditor.GUIView");
                PropertyInfo currentProperty = guiViewType?.GetProperty(
                    "current",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                object currentView = currentProperty?.GetValue(null, null);
                if (currentView == null)
                    return null;

                Type hostViewType = editorAssembly.GetType("UnityEditor.HostView");
                PropertyInfo actualViewProperty = hostViewType?.GetProperty(
                    "actualView",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                return actualViewProperty?.GetValue(currentView, null) as EditorWindow;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsInspectorHost(EditorWindow host)
        {
            if (host == null)
                return false;

            Assembly editorAssembly = typeof(UnityEditor.Editor).Assembly;
            Type inspectorType = editorAssembly.GetType("UnityEditor.InspectorWindow");
            Type propertyEditorType = editorAssembly.GetType("UnityEditor.PropertyEditor");
            return (inspectorType != null && inspectorType.IsInstanceOfType(host))
                || (propertyEditorType != null && propertyEditorType.IsInstanceOfType(host));
        }

        private static bool HasGameObjectRootEditor(EditorWindow host)
        {
            ActiveEditorTracker tracker = GetInspectorTracker(host);
            if (tracker == null)
                return false;

            try
            {
                UnityEditor.Editor[] activeEditors = tracker.activeEditors;
                for (int i = 0; i < activeEditors.Length; i++)
                {
                    UnityEditor.Editor editor = activeEditors[i];
                    if (editor != null && editor.targets != null
                        && editor.targets.Length > 0
                        && editor.targets[0] is GameObject)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static ActiveEditorTracker GetInspectorTracker(EditorWindow host)
        {
            try
            {
                Assembly editorAssembly = typeof(UnityEditor.Editor).Assembly;
                Type inspectorType = editorAssembly.GetType("UnityEditor.InspectorWindow");
                if (inspectorType == null || !inspectorType.IsInstanceOfType(host))
                    return null;

                FieldInfo trackerField = inspectorType.GetField(
                    "m_Tracker",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (trackerField?.GetValue(host) is ActiveEditorTracker tracker)
                    return tracker;

                MethodInfo getTracker = inspectorType.GetMethod(
                    "GetTracker",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (getTracker != null)
                {
                    object value = getTracker.IsStatic
                        ? getTracker.Invoke(null, null)
                        : getTracker.Invoke(host, null);
                    return value as ActiveEditorTracker;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }

}
