using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace ES.Editor
{
    [CustomPropertyDrawer(typeof(ESCommandEvent))]
    public sealed class ESCommandEventDrawer : PropertyDrawer
    {
        private const float Line = 20f;
        private const float Gap = 4f;
        private const float Pad = 8f;
        private static readonly Color HeaderColor = new Color(0.18f, 0.24f, 0.29f, 1f);
        private static readonly Color BodyColor = new Color(0.11f, 0.12f, 0.13f, 1f);
        private static readonly Color AccentColor = new Color(0.25f, 0.62f, 0.82f, 1f);
        private static readonly Color DisabledColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        private static List<Type> commandTypes;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty commands = property.FindPropertyRelative("commands");
            if (commands == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            Rect outer = new Rect(position.x, position.y, position.width, position.height - Gap);
            DrawPanelBackground(outer);

            Rect header = new Rect(outer.x, outer.y, outer.width, 28f);
            DrawHeader(header, label, commands);

            Rect content = new Rect(outer.x + Pad, header.yMax + Gap, outer.width - Pad * 2f, outer.height - header.height - Gap - Pad);
            DrawCommandList(content, commands);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty commands = property.FindPropertyRelative("commands");
            if (commands == null)
                return EditorGUI.GetPropertyHeight(property, label, true);

            float height = 28f + Pad + Gap;
            if (commands.arraySize == 0)
                return height + 28f + Pad;

            for (int i = 0; i < commands.arraySize; i++)
            {
                SerializedProperty item = commands.GetArrayElementAtIndex(i);
                height += GetCommandHeight(item) + Gap;
            }

            return height + Pad;
        }

        private static void DrawHeader(Rect rect, GUIContent label, SerializedProperty commands)
        {
            EditorGUI.DrawRect(rect, HeaderColor);

            Rect accent = new Rect(rect.x, rect.y, 4f, rect.height);
            EditorGUI.DrawRect(accent, AccentColor);

            Rect titleRect = new Rect(rect.x + 10f, rect.y + 4f, rect.width - 180f, Line);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.white }
            };
            EditorGUI.LabelField(titleRect, label.text, "命令事件  " + commands.arraySize, titleStyle);

            Rect addRect = new Rect(rect.xMax - 116f, rect.y + 4f, 52f, Line);
            if (GUI.Button(addRect, "添加"))
                ShowAddMenu(commands);

            Rect clearRect = new Rect(rect.xMax - 58f, rect.y + 4f, 52f, Line);
            using (new EditorGUI.DisabledScope(commands.arraySize == 0))
            {
                if (GUI.Button(clearRect, "清空"))
                {
                    commands.ClearArray();
                    commands.serializedObject.ApplyModifiedProperties();
                }
            }
        }

        private static void DrawCommandList(Rect rect, SerializedProperty commands)
        {
            if (commands.arraySize == 0)
            {
                Rect empty = new Rect(rect.x, rect.y, rect.width, 24f);
                GUIStyle emptyStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                EditorGUI.LabelField(empty, "没有命令。点击右上角“添加”。", emptyStyle);
                return;
            }

            float y = rect.y;
            for (int i = 0; i < commands.arraySize; i++)
            {
                SerializedProperty item = commands.GetArrayElementAtIndex(i);
                float height = GetCommandHeight(item);
                Rect itemRect = new Rect(rect.x, y, rect.width, height);
                DrawCommandItem(itemRect, commands, item, i);
                y += height + Gap;
            }
        }

        private static void DrawCommandItem(Rect rect, SerializedProperty commands, SerializedProperty item, int index)
        {
            object command = item.managedReferenceValue;
            bool enabled = true;
            if (command is ESCommand esCommand)
                enabled = esCommand.enabled;

            EditorGUI.DrawRect(rect, enabled ? new Color(0.16f, 0.17f, 0.18f, 1f) : DisabledColor);
            Rect inner = new Rect(rect.x + Pad, rect.y + Pad, rect.width - Pad * 2f, rect.height - Pad * 2f);

            Rect titleRect = new Rect(inner.x, inner.y, inner.width - 62f, Line);
            string typeName = GetCommandTitle(command);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = enabled ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f) }
            };
            EditorGUI.LabelField(titleRect, index + 1 + ". " + typeName, titleStyle);

            Rect removeRect = new Rect(inner.xMax - 52f, inner.y, 52f, Line);
            if (GUI.Button(removeRect, "删除"))
            {
                commands.DeleteArrayElementAtIndex(index);
                commands.serializedObject.ApplyModifiedProperties();
                return;
            }

            Rect fieldRect = new Rect(inner.x, titleRect.yMax + Gap, inner.width, EditorGUI.GetPropertyHeight(item, true));
            EditorGUI.PropertyField(fieldRect, item, GUIContent.none, true);
        }

        private static float GetCommandHeight(SerializedProperty item)
        {
            return Pad * 2f + Line + Gap + EditorGUI.GetPropertyHeight(item, true);
        }

        private static void DrawPanelBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, BodyColor);
            Handles.color = new Color(0f, 0f, 0f, 0.35f);
            Handles.DrawAAPolyLine(
                1f,
                new Vector3(rect.x, rect.y),
                new Vector3(rect.xMax, rect.y),
                new Vector3(rect.xMax, rect.yMax),
                new Vector3(rect.x, rect.yMax),
                new Vector3(rect.x, rect.y));
        }

        private static void ShowAddMenu(SerializedProperty commands)
        {
            GenericMenu menu = new GenericMenu();
            EnsureCommandTypes();

            for (int i = 0; i < commandTypes.Count; i++)
            {
                Type type = commandTypes[i];
                string menuName = GetMenuName(type);
                menu.AddItem(new GUIContent(menuName), false, AddCommand, new AddCommandData(commands, type));
            }

            if (commandTypes.Count == 0)
                menu.AddDisabledItem(new GUIContent("没有可用命令"));

            menu.ShowAsContext();
        }

        private static void AddCommand(object userData)
        {
            AddCommandData data = (AddCommandData)userData;
            SerializedProperty commands = data.commands;
            int index = commands.arraySize;
            commands.InsertArrayElementAtIndex(index);
            SerializedProperty item = commands.GetArrayElementAtIndex(index);
            item.managedReferenceValue = Activator.CreateInstance(data.type);
            commands.serializedObject.ApplyModifiedProperties();
        }

        private static void EnsureCommandTypes()
        {
            if (commandTypes != null)
                return;

            commandTypes = new List<Type>(32);
            TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<ESCommand>();
            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                if (type.IsAbstract || type.IsGenericTypeDefinition)
                    continue;

                if (type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                commandTypes.Add(type);
            }

            commandTypes.Sort((a, b) => string.Compare(GetMenuName(a), GetMenuName(b), StringComparison.Ordinal));
        }

        private static string GetMenuName(Type type)
        {
            TypeRegistryItemAttribute attribute = type.GetCustomAttribute<TypeRegistryItemAttribute>();
            if (attribute != null && !string.IsNullOrEmpty(attribute.Name))
                return "命令/" + attribute.Name;

            return "命令/" + type.Name.Replace("ESCommand_", string.Empty).Replace("_", "/");
        }

        private static string GetCommandTitle(object command)
        {
            if (command is ESCommand esCommand)
                return esCommand.CommandName;

            return command == null ? "空命令" : command.GetType().Name;
        }

        private readonly struct AddCommandData
        {
            public readonly SerializedProperty commands;
            public readonly Type type;

            public AddCommandData(SerializedProperty commands, Type type)
            {
                this.commands = commands.Copy();
                this.type = type;
            }
        }
    }
}
