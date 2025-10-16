using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace FlyRabbit.EventCenter
{
    public class EventViewerWindow : EditorWindow
    {
        [MenuItem("FlyRabbit/Event Center/事件查看器")]
        private static void Open()
        {
            EditorWindow.GetWindow<EventViewerWindow>("事件查看器");
        }

        private static string m_LastScanTime = "none";

        private Vector2 m_ScrollPosition;
        /// <summary>
        /// 存储了扫描到的所有脚本的路径
        /// </summary>
        private static List<string> m_ScriptPaths = new List<string>();
        /// <summary>
        /// 提示文本-中文
        /// </summary>
        private static string m_NotesText = "事件查看器使用正则表达式搜索项目中的以下方法的调用来工作：\nEventCenter.AddListener\nEventCenter.RemoveListener\nEventCenter.TriggerEvent\n如果你的项目中有其他的\"EventCenter\"类，并且也拥有这些方法，那么事件查看器可能无法正常工作。\n此外，事件查看器会忽略Editor文件夹。";


        private static readonly Regex m_AddRegex = new Regex(@"(?<!""[^\s]*)EventCenter\s*\.\s*AddListener\s*(?:<\s*(?<Types>[^>]+)\s*>)?\s*\(\s*EventName\s*\.\s*(?<Name>\w+)", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex m_RemoveRegex = new Regex(@"(?<!""[^\s]*)EventCenter\s*\.\s*RemoveListener\s*(?:<\s*(?<Types>[^>]+)\s*>)?\s*\(\s*EventName\s*\.\s*(?<Name>\w+)", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex m_TriggerRegex = new Regex(@"(?<!""[^\s]*)EventCenter\s*\.\s*TriggerEvent\s*(?:<\s*(?<Types>[^>]+)\s*>)?\s*\(\s*EventName\s*\.\s*(?<Name>\w+)", RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// key为事件名，value为对应的group
        /// </summary>
        private static readonly Dictionary<string, EventGroup> m_Events = new Dictionary<string, EventGroup>();
        /// <summary>
        /// key为事件名，value为此foldout是否折叠
        /// </summary>
        private static readonly Dictionary<string, bool> m_foldouts = new Dictionary<string, bool>();

        private void OnEnable()
        {
        }
        private void OnGUI()
        {

            {   //"扫描项目中的事件"按钮
                if (GUILayout.Button("扫描项目中的事件", GUILayout.Height(24)))
                {
                    OnScanButtonClick();
                }
            }

            {   //提示信息
                EditorGUILayout.HelpBox(m_NotesText, MessageType.Warning, true);
            }

            {   //事件具体显示内容
                m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition);

                foreach (var item in m_Events)
                {
                    DrawSeparator();
                    string eventName = item.Key;
                    EventGroup eventGroup = item.Value;

                    GUIStyle style;
                    if (eventGroup.HasError)
                    {
                        style = GUIStyles.ErrorFoldoutStyle;
                    }
                    else
                    {
                        style = GUIStyles.NormalFoldoutStyle;
                    }
                    m_foldouts[eventName] = EditorGUILayout.Foldout(m_foldouts[eventName], $"{eventName}  参数类型：{eventGroup.Signature}", style);

                    if (m_foldouts[eventName])
                    {
                        DrawBlock("触发事件", eventGroup.Triggers);
                        DrawBlock("添加监听", eventGroup.Adds);
                        DrawBlock("移除监听", eventGroup.Removes);
                    }

                }
                DrawSeparator();

                GUILayout.EndScrollView();
            }

            GUILayout.FlexibleSpace();

            {   //底部时间提示
                GUILayout.BeginHorizontal();
                GUILayout.Label("上次扫描时间：");
                GUILayout.Label(m_LastScanTime);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }



        }

        /// <summary>
        /// 清除代码中的注释并返回,
        /// 只是简单的判断，用于清除被注释的事件中心方法的调用,会误判字符串中的内容，但不影响使用
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        private static string RemoveComments(string code)
        {
            //清除单行注释
            code = Regex.Replace(code, @"//.*", "");
            //清除多行注释
            code = Regex.Replace(code, @"/\*[\s\S]*?\*/", "");
            return code;
        }
        /// <summary>
        /// 返回index在text的第几行
        /// </summary>
        /// <param name="code"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private static int GetLine(string text, int index)
        {
            int result = 1;
            for (int i = 0; i < text.Length && i < index; i++)
            {
                if (text[i] == '\n')
                {
                    result++;
                }
            }
            return result;
        }
        /// <summary>
        /// 删除字符串中的所有换行和空格
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private static string RemoveAllWhitespace(string text)
        {
            return Regex.Replace(text, @"\s+", "");
        }
        /// <summary>
        /// 获取或创建与事件对应的EventGroup
        /// </summary>
        /// <param name="eventName"></param>
        /// <returns></returns>
        private static EventGroup GetOrCreatEventGroup(string eventName)
        {
            EventGroup result;
            if (m_Events.TryGetValue(eventName, out result) == false)
            {
                result = new EventGroup();
                m_Events[eventName] = result;
            }
            return result;
        }
        /// <summary>
        /// 确保eventGroup的signature已经被赋值，如果没有被赋值，使用传进来的值
        /// </summary>
        /// <param name="eventGroup"></param>
        /// <param name="signature"></param>
        private static void EnsureSignature(EventGroup eventGroup, string signature)
        {
            if (eventGroup.Signature != null)
            {
                return;
            }
            eventGroup.Signature = signature;
        }
        /// <summary>
        /// 处理匹配信息
        /// </summary>
        private static void ProcessMatch(string code, string scriptPath, MatchType matchType)
        {
            MatchCollection matchResult;
            switch (matchType)
            {
                case MatchType.Add:
                    matchResult = m_AddRegex.Matches(code);
                    break;
                case MatchType.Remove:
                    matchResult = m_RemoveRegex.Matches(code);
                    break;
                case MatchType.Trigger:
                    matchResult = m_TriggerRegex.Matches(code);
                    break;
                default:
                    matchResult = null;
                    break;
            }
            foreach (Match matched in matchResult)
            {
                string name = matched.Groups["Name"].Value;
                string signature = RemoveAllWhitespace(matched.Groups["Types"].Value);
                int line = GetLine(code, matched.Index);

                EventUsageInfo eventUsageInfo = new EventUsageInfo(signature, scriptPath, line);

                EventGroup eventGroup = GetOrCreatEventGroup(name);

                EnsureSignature(eventGroup, signature);

                switch (matchType)
                {
                    case MatchType.Add:
                        eventGroup.Adds.Add(eventUsageInfo);
                        break;
                    case MatchType.Remove:
                        eventGroup.Removes.Add(eventUsageInfo);
                        break;
                    case MatchType.Trigger:
                        eventGroup.Triggers.Add(eventUsageInfo);
                        break;
                    default:
                        break;
                }
            }
        }
        /// <summary>
        /// 绘制块
        /// </summary>
        /// <param name="title"></param>
        /// <param name="usageInfos"></param>
        private static void DrawBlock(string title, List<EventUsageInfo> usageInfos)
        {
            if (usageInfos.Count == 0)
            {
                return;
            }
            GUILayout.Label($"[{title}]", EditorStyles.boldLabel);
            foreach (var item in usageInfos)
            {
                EditorGUILayout.BeginHorizontal();

                GUILayout.Label(Path.GetFileName(item.AssetPath) + "  " + $"（第{item.Line}行）" + "  ");

                GUIStyle style;
                if (item.IsError)
                {
                    style =GUIStyles.ErrorLabelStyle;
                }
                else
                {
                    style = GUIStyles.NormalLabelStyle;
                }
                GUILayout.Label(item.Signature, style);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("定位", GUILayout.Width(60)))
                {
                    OnLocationButtonClick(item.AssetPath);
                }

                if (GUILayout.Button("打开", GUILayout.Width(60)))
                {
                    OnOpenButtonClick(item);
                }

                EditorGUILayout.EndHorizontal();
            }
        }
        /// <summary>
        /// 绘制一条分割线
        /// </summary>
        private static void DrawSeparator()
        {
            GUILayout.Space(5);
            GUILayout.Box("", GUILayout.Height(3), GUILayout.ExpandWidth(true));
            GUILayout.Space(5);
        }
        /// <summary>
        /// 检查错误的签名
        /// </summary>
        private static void CheckError()
        {
            foreach (EventGroup eventGroup in m_Events.Values)
            {
                string trueSignature = eventGroup.Signature;
                foreach (EventUsageInfo add in eventGroup.Adds)
                {
                    if (add.Signature == trueSignature)
                    {
                        continue;
                    }
                    eventGroup.HasError = true;
                    add.IsError = true;
                }
                foreach (EventUsageInfo remove in eventGroup.Removes)
                {
                    if (remove.Signature == trueSignature)
                    {
                        continue;
                    }
                    eventGroup.HasError = true;
                    remove.IsError = true;
                }
                foreach (EventUsageInfo trigger in eventGroup.Triggers)
                {
                    if (trigger.Signature == trueSignature)
                    {
                        continue;
                    }
                    eventGroup.HasError = true;
                    trigger.IsError = true;
                }
            }
        }

        private static void OnScanButtonClick()
        {
            m_ScriptPaths.Clear();
            m_foldouts.Clear();
            m_Events.Clear();

            //获取所有Assets目录下的脚本的GUID
            string[] ScriptGuids = AssetDatabase.FindAssets("t:Script", new string[] { "Assets" });
            
            //GUID转为路径，并排除Editor文件夹下的脚本，存储至m_ScriptPaths
            foreach (var item in ScriptGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(item);
                if (path.Contains("/Editor/"))
                {
                    continue;
                }
                m_ScriptPaths.Add(path);
            }
            //遍历每一个文件
            foreach (string scriptPath in m_ScriptPaths)
            {
                //获得不带注释的源码
                string code;
                string fullPath = Path.GetFullPath(scriptPath);
                code = File.ReadAllText(fullPath);
                code = RemoveComments(code);
                //进行匹配
                ProcessMatch(code, scriptPath, MatchType.Trigger);
                ProcessMatch(code, scriptPath, MatchType.Add);
                ProcessMatch(code, scriptPath, MatchType.Remove);
            }
            m_LastScanTime = DateTime.Now.ToString();
            //生成事件对应的foldout需要的参数
            foreach (var item in m_Events)
            {
                m_foldouts[item.Key] = false;
            }
            CheckError();
        }

        private static void OnLocationButtonClick(string assetPath)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (script == null)
            {
                EditorUtility.DisplayDialog("出错了！", $"未能找到脚本。\n路径：{assetPath}。", "确认");
                return;
            }

            Selection.activeObject = script;
            EditorGUIUtility.PingObject(script);
        }

        private static void OnOpenButtonClick(EventUsageInfo usageInfo)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(usageInfo.AssetPath);
            if (script == null)
            {
                EditorUtility.DisplayDialog("出错了！", $"未能找到脚本。\n路径：{usageInfo.AssetPath}。", "确认");
            }
            else
            {
                AssetDatabase.OpenAsset(script, usageInfo.Line);
            }
        }
    }
}
