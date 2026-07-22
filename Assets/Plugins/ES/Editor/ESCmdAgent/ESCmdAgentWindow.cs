using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ES
{
    public sealed class ESCmdAgentWindow : EditorWindow
    {
        private const string DefaultAgentAssetPath = "Assets/ESNormalAssets/Data/GlobalData/CmdAgent/ESCmdAgent.asset";
        private const string AIWarningsRelativePath = "Assets/Plugins/ES/AIWarnings";
        private const string AICommandsRelativePath = "Assets/Plugins/ES/AICommands";
        private const int FallbackMaxLocalTabs = 2;
        private const int FallbackOutputCharLimit = 12000;

        [SerializeField] private List<AgentSessionTab> tabs = new List<AgentSessionTab>();
        [SerializeField] private int selectedTabIndex;
        [SerializeField] private int mainPageIndex;
        [SerializeField] private bool showAdvancedSettings;
        [SerializeField] private bool showCommandDetails;

        private ESCmdAgent agent;

        [SerializeField] private bool architectIncludeCodexSessions = true;
        [SerializeField] private bool architectIncludeAITalkSessions = true;
        [SerializeField] private bool architectIncludeAIWarnings = true;
        [SerializeField] private int architectMaxCodexSessions = 16;
        [SerializeField] private int architectMaxFilesPerFolder = 80;
        [SerializeField] private string architectSearchText = "";
        [SerializeField] private Vector2 architectScroll;
        [SerializeField] private List<ArchitectNode> architectNodes = new List<ArchitectNode>();
        [SerializeField] private List<ArchitectEdge> architectEdges = new List<ArchitectEdge>();
        [SerializeField] private int architectSelectedNodeIndex = -1;

        private readonly Dictionary<string, GUIStyle> architectStyleCache = new Dictionary<string, GUIStyle>();
        private bool architectAutoBuiltThisOpen;
        private bool architectDraggingNode;
        private int architectDraggingNodeIndex = -1;
        private Vector2 architectDragOffset;

        [Serializable]
        private sealed class ArchitectNode
        {
            public string id;
            public string title;
            public string type;
            public string sourcePath;
            public string summary;
            public Rect rect;
            public Color color;
        }

        [Serializable]
        private sealed class ArchitectEdge
        {
            public int from;
            public int to;
            public string label;
        }

        [Serializable]
        private sealed class AgentSessionTab
        {
            public string title = "会话";
            public string sessionId = "";
            public string createdAt = "";
            public string lastStartTime = "";
            public string lastStopTime = "";
            public string lastCommand = "";
            public string summary = "等待恢复";
            public bool capturedSessionKey;
            public string createdSessionFile = "";

            [NonSerialized] public string outputText = "";
            [NonSerialized] public string inputText = "";
            [NonSerialized] public Vector2 scroll;
            [NonSerialized] public Process process;
            [NonSerialized] public ConcurrentQueue<string> pendingOutput;
            [NonSerialized] public DateTime startedAtUtc;

            public bool IsRunning
            {
                get { return process != null && !process.HasExited; }
            }

            public void EnsureRuntime()
            {
                if (pendingOutput == null)
                    pendingOutput = new ConcurrentQueue<string>();
            }
        }

        public static void OpenAndResume()
        {
            var window = GetWindow<ESCmdAgentWindow>();
            window.titleContent = new GUIContent("ES Cmd Agent");
            window.minSize = new Vector2(720, 520);
            window.Show();
            window.Focus();
            window.EnsureAgent();

            if (window.agent != null && window.agent.enableAgent && window.agent.autoResumeOnOpen)
                window.CreateAndResumeTab(window.GetPreferredResumeSessionId());
            else
                window.EnsureTabExists();
        }

        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "Cmd Agent / AI 会话与架构师", false, -960)]
        public static void OpenFromMenu()
        {
            OpenAndResume();
        }

        private void OnEnable()
        {
            EnsureAgent();
            EnsureTabExists();
            EnsureTabRuntime();
            EditorApplication.update += FlushOutput;
        }

        private void OnDisable()
        {
            EditorApplication.update -= FlushOutput;
            ReduceLocalResidue();
            StopAllProcesses();
        }

        private void OnGUI()
        {
            EnsureAgent();
            EnsureTabExists();
            EnsureTabRuntime();

            DrawPolishedHeader();
            DrawPolishedMainPageTabs();

            if (agent == null)
            {
                EditorGUILayout.HelpBox("未找到 ESCmdAgent 全局配置。", MessageType.Warning);
                if (GUILayout.Button("创建或定位 ESCmdAgent", GUILayout.Height(30)))
                    EnsureAgentInteractive();
                return;
            }

            if (mainPageIndex == 0)
            {
                DrawPolishedSessionPage();
            }
            else
            {
                DrawPolishedArchitectPage();
            }

            if (GUI.changed)
                EditorUtility.SetDirty(agent);
        }

        private void DrawPolishedHeader()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 72);
            EditorGUI.DrawRect(rect, new Color(0.13f, 0.15f, 0.18f));

            Rect accent = new Rect(rect.x, rect.y, 4, rect.height);
            EditorGUI.DrawRect(accent, new Color(0.28f, 0.55f, 0.95f));

            Rect titleRect = new Rect(rect.x + 16, rect.y + 10, rect.width - 32, 24);
            GUIStyle title = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal = { textColor = Color.white }
            };
            GUI.Label(titleRect, "【ES】Cmd Agent", title);

            Rect descRect = new Rect(rect.x + 16, rect.y + 36, rect.width - 32, 22);
            GUIStyle desc = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.78f, 0.82f, 0.88f) }
            };
            GUI.Label(descRect, "像聊天软件一样继续上次 Codex；本地只保留必要页签和恢复 Key，项目架构图可直接查看。", desc);
        }

        private void DrawPolishedMainPageTabs()
        {
            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);
                mainPageIndex = GUILayout.Toolbar(mainPageIndex, new[] { "AI 对话", "项目图" }, EditorStyles.toolbarButton, GUILayout.Width(180));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(mainPageIndex == 0 ? "直接输入需求，自动恢复记忆" : "全项目架构节点图", EditorStyles.miniBoldLabel, GUILayout.Width(180));
                GUILayout.Space(4);
            }
        }

        private void DrawPolishedSessionPage()
        {
            DrawPolishedStatusStrip();
            DrawBeginnerHint();
            DrawPolishedPrimaryActions();
            DrawAICollaborationActions();
            DrawAdvancedSettings();
            DrawSessionTabs();
            DrawCurrentSession();
        }

        private void DrawBeginnerHint()
        {
            AgentSessionTab current = GetCurrentTab();
            if (current != null && (current.IsRunning || !string.IsNullOrWhiteSpace(current.outputText)))
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("快速上手", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("直接在底部输入需求并点击“发送”，工具会自动恢复最近会话；需要固定某次对话时，在高级设置里填写会话 ID。", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawPolishedStatusStrip()
        {
            AgentSessionTab current = GetCurrentTab();
            int runningCount = tabs.Count(tab => tab != null && tab.IsRunning);
            string keyState = current != null && !string.IsNullOrWhiteSpace(current.sessionId) ? ShortId(current.sessionId) : "等待记录";

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPolishedMetric("Agent", agent != null && agent.enableAgent ? "已启用" : "未启用", agent != null && agent.enableAgent);
                DrawPolishedMetric("运行页签", runningCount.ToString(), runningCount > 0);
                DrawPolishedMetric("恢复 Key", keyState, current != null && !string.IsNullOrWhiteSpace(current.sessionId));
                DrawPolishedMetric("本地留存", $"最多 {Mathf.Clamp(agent.maxLocalTabsToKeep, 1, 12)} 页签", true);
            }
        }

        private static void DrawPolishedMetric(string label, string value, bool ok)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 46, GUILayout.MinWidth(140));
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.19f, 0.21f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), ok ? new Color(0.25f, 0.70f, 0.42f) : new Color(0.86f, 0.48f, 0.25f));

            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.72f, 0.75f, 0.80f) }
            };
            GUIStyle valueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(rect.x + 12, rect.y + 6, rect.width - 18, 16), label, labelStyle);
            GUI.Label(new Rect(rect.x + 12, rect.y + 23, rect.width - 18, 18), value, valueStyle);
        }

        private void DrawPolishedPrimaryActions()
        {
            AgentSessionTab current = GetCurrentTab();
            GUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);
                using (new EditorGUI.DisabledScope(!agent.enableAgent))
                {
                    if (GUILayout.Button("继续上次", EditorStyles.toolbarButton, GUILayout.Width(86)))
                        CreateAndResumeTab("");

                    if (GUILayout.Button("打开指定", EditorStyles.toolbarButton, GUILayout.Width(86)))
                        CreateAndResumeTab(agent.resumeSessionId);

                    using (new EditorGUI.DisabledScope(current == null || current.IsRunning))
                    {
                        if (GUILayout.Button("重连本页", EditorStyles.toolbarButton, GUILayout.Width(86)))
                            StartResume(current, current.sessionId);
                    }
                }

                GUILayout.Space(10);
                using (new EditorGUI.DisabledScope(current == null || string.IsNullOrWhiteSpace(current.sessionId)))
                {
                    if (GUILayout.Button("复制 Key", EditorStyles.toolbarButton, GUILayout.Width(76)))
                        CopyResumeKey(current);
                }

                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(current == null || !current.IsRunning))
                {
                    if (GUILayout.Button("停止", EditorStyles.toolbarButton, GUILayout.Width(58)))
                        StopProcess(current);
                }

                if (GUILayout.Button("关闭页签", EditorStyles.toolbarButton, GUILayout.Width(78)))
                    CloseCurrentTab();

                if (GUILayout.Button("清理停止页", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    RemoveStoppedTabs();
                GUILayout.Space(4);
            }
        }

        private void DrawAICollaborationActions()
        {
            AgentSessionTab current = GetCurrentTab();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);
                EditorGUILayout.LabelField("常用指令", EditorStyles.miniBoldLabel, GUILayout.Width(60));

                if (GUILayout.Button("读取项目规则", EditorStyles.toolbarButton, GUILayout.Width(104)))
                    SendPromptToCurrentTab(BuildReadWarningsPrompt());

                if (GUILayout.Button("更新项目记忆", EditorStyles.toolbarButton, GUILayout.Width(104)))
                    SendPromptToCurrentTab(BuildUpdateWarningsPrompt());

                if (GUILayout.Button("执行预设指令", EditorStyles.toolbarButton, GUILayout.Width(104)))
                    ShowAICommandMenu();

                GUILayout.Space(8);

                if (GUILayout.Button("打开记忆库", EditorStyles.toolbarButton, GUILayout.Width(88)))
                    RevealProjectRelativePath(AIWarningsRelativePath);

                if (GUILayout.Button("打开指令库", EditorStyles.toolbarButton, GUILayout.Width(88)))
                    RevealProjectRelativePath(AICommandsRelativePath);

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(current != null && current.IsRunning ? "当前页可直接发送" : "未启动也可直接发送", EditorStyles.miniLabel, GUILayout.Width(150));
            }
        }

        private void DrawPolishedArchitectPage()
        {
            EnsureArchitectGraphVisible();
            DrawPolishedArchitectToolbar();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                DrawArchitectSummaryBadge("节点", architectNodes.Count.ToString());
                DrawArchitectSummaryBadge("关系", architectEdges.Count.ToString());
                GUILayout.Space(8);
                EditorGUILayout.LabelField("搜索", GUILayout.Width(34));
                architectSearchText = EditorGUILayout.TextField(architectSearchText, GUILayout.MinWidth(180));
                if (GUILayout.Button("清除", GUILayout.Width(58)))
                    architectSearchText = "";
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("拖拽节点可调整布局；双击来源按钮可定位文件，导出会写入 AIWarnings/ArchitectReports。", EditorStyles.miniLabel, GUILayout.Width(460));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawArchitectCanvas();
                DrawPolishedArchitectInspector();
            }
        }

        private void DrawPolishedArchitectToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);
                if (GUILayout.Button("刷新项目图", EditorStyles.toolbarButton, GUILayout.Width(92)))
                    RebuildArchitectGraph();

                if (GUILayout.Button("自动布局", EditorStyles.toolbarButton, GUILayout.Width(78)))
                    LayoutArchitectNodes();

                if (GUILayout.Button("导出说明", EditorStyles.toolbarButton, GUILayout.Width(86)))
                    ExportArchitectMarkdown();

                if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(54)))
                {
                    architectNodes.Clear();
                    architectEdges.Clear();
                    architectSelectedNodeIndex = -1;
                }

                GUILayout.FlexibleSpace();
                architectIncludeAIWarnings = GUILayout.Toggle(architectIncludeAIWarnings, "AIWarnings", EditorStyles.toolbarButton, GUILayout.Width(88));
                architectIncludeAITalkSessions = GUILayout.Toggle(architectIncludeAITalkSessions, "AITalk", EditorStyles.toolbarButton, GUILayout.Width(66));
                architectIncludeCodexSessions = GUILayout.Toggle(architectIncludeCodexSessions, "Codex", EditorStyles.toolbarButton, GUILayout.Width(62));
                GUILayout.Space(4);
            }
        }

        private static void DrawArchitectSummaryBadge(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(92)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(32));
                EditorGUILayout.LabelField(value, EditorStyles.miniBoldLabel, GUILayout.Width(44));
            }
        }

        private void DrawPolishedArchitectInspector()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(320), GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField("节点详情", EditorStyles.boldLabel);
                ArchitectNode node = GetSelectedArchitectNode();
                if (node == null)
                {
                    EditorGUILayout.HelpBox("选择一个节点后，这里会显示摘要、来源定位和复制操作。", MessageType.Info);
                    DrawArchitectScanSettings();
                    return;
                }

                EditorGUILayout.LabelField(node.title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(node.type, EditorStyles.miniBoldLabel);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("摘要", EditorStyles.miniBoldLabel);
                EditorGUILayout.TextArea(node.summary, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(96));

                EditorGUILayout.Space(4);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("来源", node.sourcePath);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(node.sourcePath)))
                    {
                        if (GUILayout.Button("定位来源"))
                            RevealArchitectSource(node.sourcePath);
                    }

                    if (GUILayout.Button("复制摘要"))
                        EditorGUIUtility.systemCopyBuffer = $"{node.title}\n{node.summary}\n{node.sourcePath}";
                }

                EditorGUILayout.Space(8);
                DrawArchitectScanSettings();
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("ES Cmd Agent", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField("Codex resume 面板", EditorStyles.miniLabel, GUILayout.Width(120));
                }

                EditorGUILayout.LabelField(
                    "默认用 resume 恢复最近会话；面板只保留页签壳、恢复 Key 和有限输出，避免本地长期堆积历史正文。",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawMainPageTabs()
        {
            mainPageIndex = GUILayout.Toolbar(mainPageIndex, new[] { "会话", "架构" });
        }

        private void DrawStatusOverview()
        {
            AgentSessionTab current = GetCurrentTab();
            int runningCount = tabs.Count(tab => tab != null && tab.IsRunning);
            string keyState = current != null && !string.IsNullOrWhiteSpace(current.sessionId)
                ? ShortId(current.sessionId)
                : "等待记录";

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSummaryCell("Agent", agent.enableAgent ? "已启用" : "未启用", agent.enableAgent);
                DrawSummaryCell("运行页签", runningCount.ToString(), runningCount > 0);
                DrawSummaryCell("恢复 Key", keyState, current != null && !string.IsNullOrWhiteSpace(current.sessionId));
                DrawSummaryCell("本地留存", $"最多 {Mathf.Clamp(agent.maxLocalTabsToKeep, 1, 12)} 页签", true);
            }
        }

        private static void DrawSummaryCell(string label, string value, bool ok)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(120)))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
                GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = ok ? new Color(0.35f, 0.75f, 0.45f) : new Color(0.9f, 0.55f, 0.35f);
                EditorGUILayout.LabelField(value, style);
            }
        }

        private void DrawPrimaryActions()
        {
            AgentSessionTab current = GetCurrentTab();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(!agent.enableAgent))
                {
                    if (GUILayout.Button("新页签恢复最近", EditorStyles.toolbarButton, GUILayout.Width(120)))
                        CreateAndResumeTab("");

                    if (GUILayout.Button("新页签恢复指定", EditorStyles.toolbarButton, GUILayout.Width(120)))
                        CreateAndResumeTab(agent.resumeSessionId);

                    using (new EditorGUI.DisabledScope(current == null || current.IsRunning))
                    {
                        if (GUILayout.Button("当前页签重新恢复", EditorStyles.toolbarButton, GUILayout.Width(130)))
                            StartResume(current, current.sessionId);
                    }
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(current == null || string.IsNullOrWhiteSpace(current.sessionId)))
                {
                    if (GUILayout.Button("复制恢复 Key", EditorStyles.toolbarButton, GUILayout.Width(95)))
                        CopyResumeKey(current);
                }

                using (new EditorGUI.DisabledScope(current == null || !current.IsRunning))
                {
                    if (GUILayout.Button("停止当前", EditorStyles.toolbarButton, GUILayout.Width(80)))
                        StopProcess(current);
                }

                if (GUILayout.Button("关闭页签", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    CloseCurrentTab();

                if (GUILayout.Button("清理已停止", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    RemoveStoppedTabs();
            }
        }

        private void DrawAdvancedSettings()
        {
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "高级设置", true);
            if (!showAdvancedSettings)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("配置资产", agent, typeof(ESCmdAgent), false);

                agent.enableAgent = EditorGUILayout.ToggleLeft("启用 Agent", agent.enableAgent);
                agent.autoResumeOnOpen = EditorGUILayout.ToggleLeft("打开入口时自动恢复最近会话", agent.autoResumeOnOpen);
                agent.autoCaptureResumeKey = EditorGUILayout.ToggleLeft("自动记录恢复 Key", agent.autoCaptureResumeKey);
                agent.codexCommand = EditorGUILayout.TextField("Codex 命令", string.IsNullOrWhiteSpace(agent.codexCommand) ? "codex.cmd" : agent.codexCommand);
                agent.workspacePath = EditorGUILayout.TextField("工作目录", agent.GetWorkspacePath());
                agent.resumeSessionId = EditorGUILayout.TextField("指定会话 ID（留空=最近会话）", agent.resumeSessionId ?? "");

                using (new EditorGUILayout.HorizontalScope())
                {
                    agent.maxLocalTabsToKeep = Mathf.Clamp(EditorGUILayout.IntField("本地页签上限", agent.maxLocalTabsToKeep), 1, 12);
                    agent.maxOutputCharsPerTab = Mathf.Clamp(EditorGUILayout.IntField("单页签输出上限", agent.maxOutputCharsPerTab), 2000, 200000);
                }
            }
        }

        private void DrawSessionTabs()
        {
            if (tabs.Count <= 0)
                return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(4);

                if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(48)))
                    CreateAndResumeTab("");

                if (GUILayout.Button("排序", EditorStyles.toolbarButton, GUILayout.Width(48)))
                    ShowTabSortMenu();

                using (new EditorGUI.DisabledScope(tabs.Count <= 1 || selectedTabIndex <= 0))
                {
                    if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(26)))
                        MoveTabLeft(selectedTabIndex);
                }

                using (new EditorGUI.DisabledScope(tabs.Count <= 1 || selectedTabIndex < 0 || selectedTabIndex >= tabs.Count - 1))
                {
                    if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(26)))
                        MoveTabRight(selectedTabIndex);
                }

                GUILayout.Space(6);
                string[] tabNames = new string[tabs.Count];
                for (int i = 0; i < tabs.Count; i++)
                {
                    AgentSessionTab tab = tabs[i];
                    string state = tab != null && tab.IsRunning ? "运行 " : "空闲 ";
                    tabNames[i] = state + (tab != null ? tab.title : "会话");
                }

                selectedTabIndex = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
                selectedTabIndex = GUILayout.Toolbar(selectedTabIndex, tabNames, EditorStyles.toolbarButton);
            }
        }

        private void DrawCurrentSession()
        {
            AgentSessionTab tab = GetCurrentTab();
            if (tab == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("当前页签", EditorStyles.boldLabel, GUILayout.Width(70));
                    tab.title = EditorGUILayout.TextField(tab.title);
                    GUILayout.Space(8);
                    EditorGUILayout.LabelField(tab.IsRunning ? "运行中" : "未运行", EditorStyles.miniBoldLabel, GUILayout.Width(60));
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("恢复目标", string.IsNullOrWhiteSpace(tab.sessionId) ? "最近会话（等待自动记录 Key）" : tab.sessionId);
                    EditorGUILayout.TextField("摘要", tab.summary);
                    EditorGUILayout.TextField("最近启动", tab.lastStartTime);
                    EditorGUILayout.TextField("最近停止", tab.lastStopTime);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(tab.createdSessionFile)))
                    {
                        if (GUILayout.Button("定位会话文件", GUILayout.Width(110)))
                            EditorUtility.RevealInFinder(tab.createdSessionFile);
                    }

                    showCommandDetails = EditorGUILayout.Foldout(showCommandDetails, "显示命令与本地文件", true);
                }

                if (showCommandDetails)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField("会话文件", tab.createdSessionFile);
                        EditorGUILayout.TextField("启动命令", tab.lastCommand);
                    }
                }
            }

            EditorGUILayout.LabelField("对话输出（仅保留最近内容）", EditorStyles.miniBoldLabel);
            tab.scroll = EditorGUILayout.BeginScrollView(tab.scroll, GUILayout.ExpandHeight(true));
            string output = string.IsNullOrEmpty(tab.outputText) ? "暂无输出。可以直接在下方输入需求并发送，工具会自动恢复最近会话；关闭窗口时不会长期保存正文。" : tab.outputText;
            EditorGUILayout.TextArea(output, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                tab.inputText = EditorGUILayout.TextField(tab.inputText);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(tab.inputText)))
                {
                    string sendLabel = tab.IsRunning ? "发送" : "启动并发送";
                    if (GUILayout.Button(sendLabel, GUILayout.Width(90)))
                        SendUserInputSmart(tab);
                }

                if (GUILayout.Button("清空本页输出", GUILayout.Width(100)))
                    tab.outputText = "";
            }
        }

        private void CopyResumeKey(AgentSessionTab tab)
        {
            if (tab == null || string.IsNullOrWhiteSpace(tab.sessionId))
                return;

            EditorGUIUtility.systemCopyBuffer = tab.sessionId;
            tab.summary = "已复制恢复 Key";
            Repaint();
        }

        private void EnsureAgent()
        {
            if (agent != null)
                return;

            agent = ESCmdAgent.Instance;
            if (agent == null)
                agent = CreateDefaultAgentAsset();
        }

        private void EnsureAgentInteractive()
        {
            agent = CreateDefaultAgentAsset();
            if (agent != null)
            {
                Selection.activeObject = agent;
                EditorGUIUtility.PingObject(agent);
            }
        }

        private string GetPreferredResumeSessionId()
        {
            if (agent == null)
                return "";

            if (!string.IsNullOrWhiteSpace(agent.resumeSessionId))
                return agent.resumeSessionId.Trim();

            if (!string.IsNullOrWhiteSpace(agent.lastResumeSessionId))
                return agent.lastResumeSessionId.Trim();

            return "";
        }

        private static ESCmdAgent CreateDefaultAgentAsset()
        {
            ESCmdAgent existing = AssetDatabase.LoadAssetAtPath<ESCmdAgent>(DefaultAgentAssetPath);
            if (existing != null)
                return existing;

            EnsureAssetFolder("Assets/ESNormalAssets/Data/GlobalData/CmdAgent");

            var created = CreateInstance<ESCmdAgent>();
            created.name = "ESCmdAgent";
            created.enableAgent = true;
            created.codexCommand = "codex.cmd";
            created.workspacePath = Application.dataPath.EndsWith("/Assets")
                ? Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length)
                : Application.dataPath;
            created.autoResumeOnOpen = true;
            created.autoCaptureResumeKey = true;
            created.maxLocalTabsToKeep = FallbackMaxLocalTabs;
            created.maxOutputCharsPerTab = FallbackOutputCharLimit;
            created.HasConfirm = true;

            AssetDatabase.CreateAsset(created, DefaultAgentAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(created);
            return created;
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private void EnsureTabExists()
        {
            if (tabs == null)
                tabs = new List<AgentSessionTab>();

            if (tabs.Count == 0)
                tabs.Add(CreateTab(""));

            selectedTabIndex = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
        }

        private void EnsureTabRuntime()
        {
            if (tabs == null)
                return;

            foreach (AgentSessionTab tab in tabs)
                tab?.EnsureRuntime();
        }

        private AgentSessionTab CreateTab(string sessionId)
        {
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string cleanSessionId = string.IsNullOrWhiteSpace(sessionId) ? "" : sessionId.Trim();
            int index = tabs == null ? 1 : tabs.Count + 1;

            return new AgentSessionTab
            {
                title = string.IsNullOrEmpty(cleanSessionId) ? $"最近会话 {index}" : $"指定会话 {ShortId(cleanSessionId)}",
                sessionId = cleanSessionId,
                createdAt = now,
                summary = "等待恢复"
            };
        }

        private void CreateAndResumeTab(string sessionId)
        {
            EnsureAgent();
            EnsureTabExists();

            AgentSessionTab tab = GetCurrentTab();
            if (!IsReusableEmptyTab(tab))
            {
                tab = CreateTab(sessionId);
                tabs.Add(tab);
            }
            else
            {
                string cleanSessionId = string.IsNullOrWhiteSpace(sessionId) ? "" : sessionId.Trim();
                tab.sessionId = cleanSessionId;
                tab.title = string.IsNullOrEmpty(cleanSessionId) ? $"最近会话 {tabs.Count}" : $"指定会话 {ShortId(cleanSessionId)}";
                tab.createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            selectedTabIndex = tabs.IndexOf(tab);
            TrimStoppedTabsToLimit();
            StartResume(tab, sessionId);
        }

        private void MoveTabLeft(int index)
        {
            MoveTab(index, index - 1);
        }

        private void EnsureArchitectGraphVisible()
        {
            if (architectAutoBuiltThisOpen || architectNodes.Count > 0)
                return;

            architectAutoBuiltThisOpen = true;
            RebuildArchitectGraph();
        }

        private void MoveTabRight(int index)
        {
            MoveTab(index, index + 1);
        }

        private void MoveTab(int from, int to)
        {
            if (tabs == null || from < 0 || from >= tabs.Count || to < 0 || to >= tabs.Count || from == to)
                return;

            AgentSessionTab tab = tabs[from];
            tabs.RemoveAt(from);
            tabs.Insert(to, tab);
            selectedTabIndex = to;
            Repaint();
        }

        private void ShowTabSortMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("按名称排序"), false, SortTabsByTitle);
            menu.AddItem(new GUIContent("按创建时间排序"), false, SortTabsByCreatedAt);
            menu.AddItem(new GUIContent("运行中优先"), false, SortTabsByRunningState);
            menu.ShowAsContext();
        }

        private void SortTabsByTitle()
        {
            SortTabs((left, right) => string.Compare(left?.title, right?.title, StringComparison.OrdinalIgnoreCase));
        }

        private void SortTabsByCreatedAt()
        {
            SortTabs((left, right) => string.Compare(left?.createdAt, right?.createdAt, StringComparison.OrdinalIgnoreCase));
        }

        private void SortTabsByRunningState()
        {
            SortTabs((left, right) => (right?.IsRunning == true ? 1 : 0).CompareTo(left?.IsRunning == true ? 1 : 0));
        }

        private void SortTabs(Comparison<AgentSessionTab> comparison)
        {
            if (tabs == null || tabs.Count <= 1)
                return;

            AgentSessionTab selected = GetCurrentTab();
            tabs.Sort(comparison);
            selectedTabIndex = Mathf.Max(0, tabs.IndexOf(selected));
            Repaint();
        }

        private static bool IsReusableEmptyTab(AgentSessionTab tab)
        {
            return tab != null
                && !tab.IsRunning
                && string.IsNullOrEmpty(tab.outputText)
                && string.IsNullOrEmpty(tab.lastStartTime)
                && tab.summary == "等待恢复";
        }

        private AgentSessionTab GetCurrentTab()
        {
            EnsureTabExists();
            if (tabs == null || tabs.Count == 0)
                return null;

            selectedTabIndex = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
            return tabs[selectedTabIndex];
        }

        private void StartResume(AgentSessionTab tab, string sessionId = "")
        {
            EnsureAgent();
            if (agent == null || tab == null)
                return;

            tab.EnsureRuntime();

            if (!agent.enableAgent)
            {
                AppendOutput(tab, "[ES Cmd Agent] Agent 未启用。\n");
                tab.summary = "未启用";
                return;
            }

            if (tab.IsRunning)
            {
                AppendOutput(tab, "[ES Cmd Agent] 当前页签已有进程在运行。\n");
                return;
            }

            string cleanSessionId = string.IsNullOrWhiteSpace(sessionId) ? "" : sessionId.Trim();
            tab.sessionId = cleanSessionId;
            tab.title = string.IsNullOrEmpty(cleanSessionId) ? tab.title : $"指定会话 {ShortId(cleanSessionId)}";

            string workspace = agent.GetWorkspacePath();
            string codex = string.IsNullOrWhiteSpace(agent.codexCommand) ? "codex.cmd" : agent.codexCommand.Trim();
            string resumeArg = string.IsNullOrEmpty(cleanSessionId) ? "resume --last" : "resume " + Quote(cleanSessionId);
            string command = $"chcp 65001 >nul && {codex} {resumeArg} -C {Quote(workspace)} --no-alt-screen";

            try
            {
                Process processToStart = new Process
                {
                    StartInfo =
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c " + command,
                        WorkingDirectory = workspace,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    },
                    EnableRaisingEvents = true
                };

                processToStart.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                        tab.pendingOutput.Enqueue(e.Data + "\n");
                };
                processToStart.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                        tab.pendingOutput.Enqueue("[错误] " + e.Data + "\n");
                };
                processToStart.Exited += (_, _) =>
                {
                    tab.lastStopTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    tab.summary = "进程已退出";
                    tab.pendingOutput.Enqueue("[ES Cmd Agent] 进程已退出。\n");
                };

                tab.process = processToStart;
                processToStart.Start();
                processToStart.BeginOutputReadLine();
                processToStart.BeginErrorReadLine();

                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                tab.lastStartTime = now;
                tab.startedAtUtc = DateTime.UtcNow;
                tab.lastCommand = command;
                tab.summary = string.IsNullOrEmpty(cleanSessionId) ? "正在恢复最近会话" : "正在恢复指定会话";
                tab.capturedSessionKey = false;
                tab.createdSessionFile = "";

                if (!string.IsNullOrWhiteSpace(cleanSessionId))
                    agent.lastResumeSessionId = cleanSessionId;
                agent.lastStartTime = now;
                EditorUtility.SetDirty(agent);

                AppendOutput(tab, "[ES Cmd Agent] 已启动页签。\n");
                AppendOutput(tab, "[ES Cmd Agent] 命令: " + command + "\n");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                tab.summary = "启动失败";
                AppendOutput(tab, "[ES Cmd Agent] 启动失败: " + ex.Message + "\n");
            }
        }

        private void SendInput(AgentSessionTab tab)
        {
            if (tab == null || !tab.IsRunning)
                return;

            try
            {
                tab.process.StandardInput.WriteLine(tab.inputText);
                tab.process.StandardInput.Flush();
                AppendOutput(tab, "> " + tab.inputText + "\n");
                tab.inputText = "";
            }
            catch (Exception ex)
            {
                tab.summary = "发送失败";
                AppendOutput(tab, "[ES Cmd Agent] 发送失败: " + ex.Message + "\n");
            }
        }

        private void SendUserInputSmart(AgentSessionTab tab)
        {
            if (tab == null || string.IsNullOrWhiteSpace(tab.inputText))
                return;

            if (!tab.IsRunning)
            {
                string pending = tab.inputText;
                StartResume(tab, tab.sessionId);
                tab.inputText = pending;

                if (!tab.IsRunning)
                {
                    AppendOutput(tab, "[ES Cmd Agent] 当前页签启动失败，输入内容已保留。\n");
                    return;
                }
            }

            SendInput(tab);
        }

        private void SendPromptToCurrentTab(string prompt)
        {
            AgentSessionTab tab = GetCurrentTab();
            if (tab == null || string.IsNullOrWhiteSpace(prompt))
                return;

            tab.inputText = prompt.Trim();
            if (!tab.IsRunning)
            {
                StartResume(tab, tab.sessionId);
                if (!tab.IsRunning)
                {
                    AppendOutput(tab, "[ES Cmd Agent] 当前页签启动失败，指令已保留在输入框。\n");
                    return;
                }
            }

            SendInput(tab);
        }

        private void ShowAICommandMenu()
        {
            string root = GetProjectRelativeFullPath(AICommandsRelativePath);
            GenericMenu menu = new GenericMenu();

            if (!Directory.Exists(root))
            {
                menu.AddDisabledItem(new GUIContent("未找到 AICommands 目录"));
                menu.ShowAsContext();
                return;
            }

            List<string> files = Directory.GetFiles(root, "*.md", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(path => Path.GetFileName(path).StartsWith("方案_", StringComparison.OrdinalIgnoreCase))
                .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有可调用的 AICommand"));
                menu.ShowAsContext();
                return;
            }

            foreach (string file in files)
            {
                string menuName = BuildAICommandMenuName(root, file);
                string assetPath = ToProjectRelativeAssetPath(file);
                menu.AddItem(new GUIContent(menuName), false, () => SendPromptToCurrentTab(BuildUseAICommandPrompt(assetPath)));
            }

            menu.ShowAsContext();
        }

        private static string BuildReadWarningsPrompt()
        {
            return "请先快速读取项目 AIWarnings，优先读取 Assets/Plugins/ES/AIWarnings/README.md、项目最高警告、CodexNotes，以及和当前任务相关的警告文件。读取后先用短列表说明你看到的关键约束，再继续处理我的请求。";
        }

        private static string BuildUpdateWarningsPrompt()
        {
            return "请根据本轮已经完成的工作，更新或新增合适的 AIWarnings。要求：写入 Assets/Plugins/ES/AIWarnings 下的准确位置；内容要给后续 AI 可执行的约束、风险、路径和禁止事项；不要写空泛总结；不要产生乱码；更新后说明改了哪些文件。";
        }

        private static string BuildUseAICommandPrompt(string assetPath)
        {
            return $"请读取并执行这个 AICommand：{assetPath}。先复述该命令的目标、约束和涉及路径，再按命令推进；如果命令只适合分析，不要擅自写文件。";
        }

        private static string BuildAICommandMenuName(string root, string file)
        {
            string relative = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            relative = relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
            string name = Path.ChangeExtension(relative, null);

            if (name.StartsWith("方案_", StringComparison.OrdinalIgnoreCase))
                return "方案/" + name.Substring("方案_".Length);
            if (name.StartsWith("执行_", StringComparison.OrdinalIgnoreCase))
                return "执行/" + name.Substring("执行_".Length);
            if (name.StartsWith("检查_", StringComparison.OrdinalIgnoreCase))
                return "检查/" + name.Substring("检查_".Length);
            if (name.StartsWith("信息_", StringComparison.OrdinalIgnoreCase))
                return "信息/" + name.Substring("信息_".Length);

            return "其他/" + name;
        }

        private static void RevealProjectRelativePath(string relativePath)
        {
            string fullPath = GetProjectRelativeFullPath(relativePath);
            if (Directory.Exists(fullPath) || File.Exists(fullPath))
                EditorUtility.RevealInFinder(fullPath);
        }

        private static string GetProjectRelativeFullPath(string relativePath)
        {
            return Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ToProjectRelativeAssetPath(string fullPath)
        {
            string normalizedProjectRoot = NormalizePath(ProjectRoot);
            string normalizedPath = NormalizePath(fullPath);
            if (normalizedPath.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
                return normalizedPath.Substring(normalizedProjectRoot.Length).TrimStart('/').Replace('\\', '/');

            return fullPath.Replace('\\', '/');
        }

        private void FlushOutput()
        {
            if (tabs == null)
                return;

            bool changed = false;
            int outputLimit = GetOutputLimit();

            foreach (AgentSessionTab tab in tabs)
            {
                if (tab == null)
                    continue;

                tab.EnsureRuntime();
                TryCaptureSessionKey(tab);

                while (tab.pendingOutput.TryDequeue(out string line))
                {
                    tab.outputText += line;
                    TrimOutput(tab, outputLimit);
                    changed = true;
                }
            }

            if (changed)
                Repaint();
        }

        private void AppendOutput(AgentSessionTab tab, string text)
        {
            if (tab == null)
                return;

            tab.outputText += text;
            TrimOutput(tab, GetOutputLimit());
            Repaint();
        }

        private void TryCaptureSessionKey(AgentSessionTab tab)
        {
            if (agent == null || tab == null || tab.capturedSessionKey || !agent.autoCaptureResumeKey)
                return;

            if (!tab.IsRunning)
                return;

            if ((DateTime.UtcNow - tab.startedAtUtc).TotalSeconds < 2.0)
                return;

            string sessionId = TryReadLatestSessionId(tab);
            if (string.IsNullOrWhiteSpace(sessionId))
                return;

            tab.sessionId = sessionId;
            tab.title = $"会话 {ShortId(sessionId)}";
            tab.capturedSessionKey = true;
            tab.summary = "已记录恢复 Key";
            agent.lastResumeSessionId = sessionId;
            EditorUtility.SetDirty(agent);
            Repaint();
        }

        private string TryReadLatestSessionId(AgentSessionTab tab)
        {
            try
            {
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");
                if (!Directory.Exists(root))
                    return "";

                string workspace = agent != null ? agent.GetWorkspacePath() : string.Empty;
                DateTime lowerBoundUtc = tab.startedAtUtc == default ? DateTime.UtcNow.AddMinutes(-5) : tab.startedAtUtc.AddSeconds(-3);

                List<FileInfo> candidates = new DirectoryInfo(root)
                    .GetFiles("*.jsonl", SearchOption.AllDirectories)
                    .Where(file => file.LastWriteTimeUtc >= lowerBoundUtc)
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .ToList();

                foreach (FileInfo file in candidates)
                {
                    if (TryReadSessionIdFromFile(file.FullName, workspace, out string sessionId))
                    {
                        tab.createdSessionFile = file.FullName;
                        return sessionId;
                    }
                }
            }
            catch
            {
                return "";
            }

            return "";
        }

        private static bool TryReadSessionIdFromFile(string filePath, string workspace, out string sessionId)
        {
            sessionId = string.Empty;

            try
            {
                using (StreamReader reader = new StreamReader(filePath, Encoding.UTF8, true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.IndexOf("\"type\":\"session_meta\"", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        string id = ExtractJsonString(line, "session_id");
                        if (string.IsNullOrWhiteSpace(id))
                            id = ExtractJsonString(line, "id");

                        if (string.IsNullOrWhiteSpace(id))
                            continue;

                        if (!string.IsNullOrWhiteSpace(workspace))
                        {
                            string cwd = ExtractJsonString(line, "cwd");
                            if (!string.IsNullOrWhiteSpace(cwd) && !WorkspaceMatches(cwd, workspace))
                                continue;
                        }

                        sessionId = id;
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

        private static string ExtractJsonString(string text, string key)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            string needle = "\"" + key + "\":\"";
            int index = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return string.Empty;

            index += needle.Length;
            int endIndex = text.IndexOf('"', index);
            if (endIndex < 0 || endIndex <= index)
                return string.Empty;

            return UnescapeSimpleJsonString(text.Substring(index, endIndex - index));
        }

        private static string UnescapeSimpleJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\\\", "\\")
                .Replace("\\/", "/")
                .Replace("\\\"", "\"");
        }

        private static bool WorkspaceMatches(string left, string right)
        {
            return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Replace('/', '\\').TrimEnd('\\');
        }

        private void TrimOutput(AgentSessionTab tab, int maxChars)
        {
            if (tab == null || string.IsNullOrEmpty(tab.outputText) || tab.outputText.Length <= maxChars)
                return;

            int removeCount = tab.outputText.Length - maxChars;
            tab.outputText = "[本地输出已自动截断，仅保留最近内容]\n" + tab.outputText.Substring(removeCount);
        }

        private int GetOutputLimit()
        {
            return Mathf.Clamp(agent != null ? agent.maxOutputCharsPerTab : FallbackOutputCharLimit, 2000, 200000);
        }

        private int GetTabLimit()
        {
            return Mathf.Clamp(agent != null ? agent.maxLocalTabsToKeep : FallbackMaxLocalTabs, 1, 12);
        }

        private void CloseCurrentTab()
        {
            if (tabs == null || tabs.Count == 0)
                return;

            AgentSessionTab tab = GetCurrentTab();
            if (tab != null && tab.IsRunning)
            {
                bool stop = EditorUtility.DisplayDialog(
                    "关闭页签",
                    "当前页签仍在运行。关闭会停止本地命令行进程，但之后仍可通过 resume 恢复会话。",
                    "停止并关闭",
                    "取消");
                if (!stop)
                    return;
            }

            StopProcess(tab);
            tabs.RemoveAt(selectedTabIndex);
            EnsureTabExists();
            selectedTabIndex = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
        }

        private void RemoveStoppedTabs()
        {
            if (tabs == null || tabs.Count == 0)
                return;

            for (int i = tabs.Count - 1; i >= 0; i--)
            {
                if (tabs[i] != null && !tabs[i].IsRunning)
                    tabs.RemoveAt(i);
            }

            EnsureTabExists();
            selectedTabIndex = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
        }

        private void TrimStoppedTabsToLimit()
        {
            if (tabs == null)
                return;

            int limit = GetTabLimit();
            for (int i = 0; i < tabs.Count && tabs.Count > limit;)
            {
                if (tabs[i] != null && !tabs[i].IsRunning && i != selectedTabIndex)
                {
                    tabs.RemoveAt(i);
                    if (selectedTabIndex > i)
                        selectedTabIndex--;
                    continue;
                }

                i++;
            }

            selectedTabIndex = Mathf.Clamp(selectedTabIndex, 0, tabs.Count - 1);
        }

        private void ReduceLocalResidue()
        {
            if (tabs == null || tabs.Count == 0)
                return;

            int limit = Mathf.Clamp(GetTabLimit(), 1, 2);
            for (int i = tabs.Count - 1; i >= 0; i--)
            {
                AgentSessionTab tab = tabs[i];
                if (tab != null)
                {
                    tab.inputText = "";
                    tab.outputText = "";
                    tab.scroll = Vector2.zero;
                }

                if (tab != null && tabs.Count > limit && !tab.IsRunning && i != selectedTabIndex)
                    tabs.RemoveAt(i);
            }
        }

        private void StopProcess(AgentSessionTab tab)
        {
            if (tab == null || tab.process == null)
                return;

            try
            {
                if (!tab.process.HasExited)
                    tab.process.Kill();
            }
            catch
            {
                // Ignore process shutdown races.
            }
            finally
            {
                tab.process.Dispose();
                tab.process = null;
                tab.lastStopTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                tab.summary = "已停止";
            }
        }

        private void StopAllProcesses()
        {
            if (tabs == null)
                return;

            foreach (AgentSessionTab tab in tabs)
                StopProcess(tab);
        }

        private void DrawArchitectPage()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (GUILayout.Button("扫描并生成思路图", EditorStyles.toolbarButton, GUILayout.Width(130)))
                        RebuildArchitectGraph();

                    if (GUILayout.Button("自动布局", EditorStyles.toolbarButton, GUILayout.Width(75)))
                        LayoutArchitectNodes();

                    if (GUILayout.Button("导出 Markdown", EditorStyles.toolbarButton, GUILayout.Width(95)))
                        ExportArchitectMarkdown();

                    if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(50)))
                    {
                        architectNodes.Clear();
                        architectEdges.Clear();
                        architectSelectedNodeIndex = -1;
                    }

                    GUILayout.FlexibleSpace();
                    architectIncludeCodexSessions = GUILayout.Toggle(architectIncludeCodexSessions, "Codex", EditorStyles.toolbarButton, GUILayout.Width(58));
                    architectIncludeAITalkSessions = GUILayout.Toggle(architectIncludeAITalkSessions, "AITalk", EditorStyles.toolbarButton, GUILayout.Width(60));
                    architectIncludeAIWarnings = GUILayout.Toggle(architectIncludeAIWarnings, "AIWarnings", EditorStyles.toolbarButton, GUILayout.Width(88));
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"节点 {architectNodes.Count}", EditorStyles.miniBoldLabel, GUILayout.Width(80));
                    EditorGUILayout.LabelField($"关系 {architectEdges.Count}", EditorStyles.miniBoldLabel, GUILayout.Width(80));
                    architectSearchText = EditorGUILayout.TextField(architectSearchText);
                    if (GUILayout.Button("重置搜索", GUILayout.Width(80)))
                        architectSearchText = "";
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawArchitectCanvas();
                    DrawArchitectInspector();
                }
            }
        }

        private void DrawArchitectCanvas()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                Rect viewRect = GUILayoutUtility.GetRect(10, 10000, 10, 10000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                architectScroll = GUI.BeginScrollView(viewRect, architectScroll, new Rect(0, 0, 2800, 1800));

                DrawArchitectGrid(new Vector2(2800, 1800), 24, new Color(1f, 1f, 1f, 0.05f));
                DrawArchitectGrid(new Vector2(2800, 1800), 120, new Color(1f, 1f, 1f, 0.08f));
                DrawArchitectEdges();
                DrawArchitectNodes();
                HandleArchitectCanvasEvents();

                GUI.EndScrollView();
            }
        }

        private void DrawArchitectInspector()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(300), GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField("节点详情", EditorStyles.boldLabel);
                ArchitectNode node = GetSelectedArchitectNode();
                if (node == null)
                {
                    EditorGUILayout.HelpBox("先点一个节点。这里会显示来源、摘要和定位操作。", MessageType.Info);
                    DrawArchitectScanSettings();
                    return;
                }

                EditorGUILayout.LabelField(node.title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(node.type, EditorStyles.miniBoldLabel);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("摘要", EditorStyles.miniBoldLabel);
                EditorGUILayout.TextArea(node.summary, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(88));

                EditorGUILayout.Space(4);
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField("来源", node.sourcePath);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(node.sourcePath)))
                    {
                        if (GUILayout.Button("定位文件"))
                            RevealArchitectSource(node.sourcePath);
                    }

                    if (GUILayout.Button("复制摘要"))
                        EditorGUIUtility.systemCopyBuffer = $"{node.title}\n{node.summary}\n{node.sourcePath}";
                }

                EditorGUILayout.Space(8);
                DrawArchitectScanSettings();
            }
        }

        private void DrawArchitectScanSettings()
        {
            EditorGUILayout.LabelField("扫描范围", EditorStyles.boldLabel);
            architectIncludeCodexSessions = EditorGUILayout.ToggleLeft("读取 Codex 本机会话", architectIncludeCodexSessions);
            architectIncludeAITalkSessions = EditorGUILayout.ToggleLeft("读取 AITalk 协作会话", architectIncludeAITalkSessions);
            architectIncludeAIWarnings = EditorGUILayout.ToggleLeft("读取 AIWarnings 长期结论", architectIncludeAIWarnings);
            architectMaxCodexSessions = Mathf.Clamp(EditorGUILayout.IntField("Codex 会话上限", architectMaxCodexSessions), 1, 100);
            architectMaxFilesPerFolder = Mathf.Clamp(EditorGUILayout.IntField("每类文件上限", architectMaxFilesPerFolder), 10, 500);
            EditorGUILayout.HelpBox("这里只做本地聚合和可视化，不调用模型。", MessageType.None);
        }

        private void RebuildArchitectGraph()
        {
            architectNodes.Clear();
            architectEdges.Clear();
            architectSelectedNodeIndex = -1;

            AddArchitectNode("root", "ES 项目架构总图", "Project", Application.dataPath, "由本地 AI 会话、AITalk 协作记录、AIWarnings 长期结论聚合生成。", new Color(0.28f, 0.50f, 0.88f), new Vector2(80, 70));

            if (architectIncludeAIWarnings)
                ScanArchitectAIWarnings();

            if (architectIncludeAITalkSessions)
                ScanArchitectAITalkSessions();

            if (architectIncludeCodexSessions)
                ScanArchitectCodexSessions();

            BuildArchitectHeuristicEdges();
            LayoutArchitectNodes();
            Repaint();
        }

        private void ScanArchitectAIWarnings()
        {
            string root = Path.Combine(ProjectRoot, "Assets/Plugins/ES/AIWarnings".Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
                return;

            AddArchitectCategory("AIWarnings", "长期架构结论", "长期沉淀的项目规则、禁止事项和跨系统纠偏。", root, new Color(0.74f, 0.48f, 0.22f));

            foreach (string file in Directory.GetFiles(root, "*.md", SearchOption.AllDirectories).Take(architectMaxFilesPerFolder))
            {
                string title = ReadArchitectTitle(file);
                string summary = ReadArchitectSummary(file);
                int index = AddArchitectNode("warning:" + file, title, "AIWarning", file, summary, new Color(0.78f, 0.58f, 0.30f), Vector2.zero);
                architectEdges.Add(new ArchitectEdge { from = FindArchitectNodeIndex("AIWarnings"), to = index, label = "沉淀" });
            }
        }

        private void ScanArchitectAITalkSessions()
        {
            string root = Path.Combine(ProjectRoot, "Assets/Plugins/ES/AITalk/Sessions".Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(root))
                return;

            AddArchitectCategory("AITalk", "AI 协作会话", "跨 AI 讨论、多人规则推演、阶段性共识和最终结论。", root, new Color(0.38f, 0.62f, 0.45f));

            foreach (string sessionDir in Directory.GetDirectories(root).OrderByDescending(Directory.GetLastWriteTime).Take(architectMaxFilesPerFolder))
            {
                string name = Path.GetFileName(sessionDir);
                string summary = ReadArchitectSessionSummary(sessionDir);
                int sessionIndex = AddArchitectNode("aitalk:" + sessionDir, name, "AITalk Session", sessionDir, summary, new Color(0.42f, 0.70f, 0.50f), Vector2.zero);
                architectEdges.Add(new ArchitectEdge { from = FindArchitectNodeIndex("AITalk"), to = sessionIndex, label = "会话" });

                string consensus = Path.Combine(sessionDir, "Consensus");
                if (Directory.Exists(consensus))
                {
                    foreach (string file in Directory.GetFiles(consensus, "*.md").Take(8))
                    {
                        int child = AddArchitectNode("consensus:" + file, ReadArchitectTitle(file), "Consensus", file, ReadArchitectSummary(file), new Color(0.50f, 0.74f, 0.56f), Vector2.zero);
                        architectEdges.Add(new ArchitectEdge { from = sessionIndex, to = child, label = "结论" });
                    }
                }
            }
        }

        private void ScanArchitectCodexSessions()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions");
            if (!Directory.Exists(root))
                return;

            AddArchitectCategory("CodexSessions", "Codex 本机会话", "本机 Codex CLI 记录。只读取匹配当前工程 cwd 的 session_meta 和有限摘要。", root, new Color(0.42f, 0.48f, 0.82f));

            List<FileInfo> files = new DirectoryInfo(root)
                .GetFiles("*.jsonl", SearchOption.AllDirectories)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Take(architectMaxCodexSessions * 3)
                .ToList();

            int added = 0;
            foreach (FileInfo file in files)
            {
                if (added >= architectMaxCodexSessions)
                    break;

                if (!TryReadCodexSessionMeta(file.FullName, out string sessionId, out string cwd, out string summary))
                    continue;

                if (!WorkspaceContainsProject(cwd))
                    continue;

                int index = AddArchitectNode("codex:" + sessionId, "Codex " + ShortId(sessionId), "Codex Session", file.FullName, summary, new Color(0.46f, 0.52f, 0.86f), Vector2.zero);
                architectEdges.Add(new ArchitectEdge { from = FindArchitectNodeIndex("CodexSessions"), to = index, label = "记录" });
                added++;
            }
        }

        private void AddArchitectCategory(string id, string title, string summary, string sourcePath, Color color)
        {
            int rootIndex = FindArchitectNodeIndex("root");
            int index = AddArchitectNode(id, title, "Category", sourcePath, summary, color, Vector2.zero);
            if (rootIndex >= 0)
                architectEdges.Add(new ArchitectEdge { from = rootIndex, to = index, label = "分类" });
        }

        private int AddArchitectNode(string id, string title, string type, string sourcePath, string summary, Color color, Vector2 position)
        {
            int existing = FindArchitectNodeIndex(id);
            if (existing >= 0)
                return existing;

            architectNodes.Add(new ArchitectNode
            {
                id = id,
                title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(sourcePath) : title.Trim(),
                type = type,
                sourcePath = sourcePath,
                summary = LimitText(summary),
                color = color,
                rect = new Rect(position.x, position.y, 230, 112)
            });

            return architectNodes.Count - 1;
        }

        private void BuildArchitectHeuristicEdges()
        {
            string[] keyTerms =
            {
                "表格", "资源", "输入", "玩家", "状态", "技能", "对象池", "GameManager", "运动", "Item", "AI", "架构", "编译", "ReloadDomain"
            };

            for (int i = 0; i < architectNodes.Count; i++)
            {
                ArchitectNode node = architectNodes[i];
                if (node.type == "Category" || node.type == "Project")
                    continue;

                foreach (string term in keyTerms)
                {
                    if (!ContainsArchitectTerm(node, term))
                        continue;

                    int anchor = FindOrCreateArchitectTermNode(term);
                    if (!HasArchitectEdge(anchor, i))
                        architectEdges.Add(new ArchitectEdge { from = anchor, to = i, label = "关联" });
                }
            }
        }

        private int FindOrCreateArchitectTermNode(string term)
        {
            string id = "term:" + term;
            int existing = FindArchitectNodeIndex(id);
            if (existing >= 0)
                return existing;

            int rootIndex = FindArchitectNodeIndex("root");
            int index = AddArchitectNode(id, term, "Topic", "", "自动从 AI 记录标题和摘要中识别出的项目主题。", new Color(0.45f, 0.45f, 0.45f), Vector2.zero);
            if (rootIndex >= 0)
                architectEdges.Add(new ArchitectEdge { from = rootIndex, to = index, label = "主题" });

            return index;
        }

        private static bool ContainsArchitectTerm(ArchitectNode node, string term)
        {
            return (!string.IsNullOrEmpty(node.title) && node.title.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrEmpty(node.summary) && node.summary.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrEmpty(node.sourcePath) && node.sourcePath.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private bool HasArchitectEdge(int from, int to)
        {
            return architectEdges.Any(edge => edge.from == from && edge.to == to);
        }

        private void LayoutArchitectNodes()
        {
            if (architectNodes.Count == 0)
                return;

            Dictionary<string, int> typeRows = new Dictionary<string, int>
            {
                ["Project"] = 0,
                ["Category"] = 1,
                ["Topic"] = 2,
                ["AIWarning"] = 3,
                ["AITalk Session"] = 4,
                ["Consensus"] = 5,
                ["Codex Session"] = 6
            };

            Dictionary<string, int> typeColumns = new Dictionary<string, int>();
            for (int i = 0; i < architectNodes.Count; i++)
            {
                ArchitectNode node = architectNodes[i];
                int row = typeRows.TryGetValue(node.type, out int knownRow) ? knownRow : 7;
                int column = typeColumns.TryGetValue(node.type, out int knownColumn) ? knownColumn : 0;
                typeColumns[node.type] = column + 1;
                node.rect.position = new Vector2(70 + column * 270, 60 + row * 170);
            }
        }

        private void DrawArchitectGrid(Vector2 size, float spacing, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            for (float x = 0; x < size.x; x += spacing)
                Handles.DrawLine(new Vector3(x, 0), new Vector3(x, size.y));
            for (float y = 0; y < size.y; y += spacing)
                Handles.DrawLine(new Vector3(0, y), new Vector3(size.x, y));
            Handles.color = Color.white;
            Handles.EndGUI();
        }

        private void DrawArchitectEdges()
        {
            Handles.BeginGUI();
            foreach (ArchitectEdge edge in architectEdges)
            {
                if (edge.from < 0 || edge.from >= architectNodes.Count || edge.to < 0 || edge.to >= architectNodes.Count)
                    continue;

                Rect from = architectNodes[edge.from].rect;
                Rect to = architectNodes[edge.to].rect;
                Vector3 start = new Vector3(from.xMax, from.center.y);
                Vector3 end = new Vector3(to.xMin, to.center.y);
                Vector3 startTan = start + Vector3.right * 70f;
                Vector3 endTan = end + Vector3.left * 70f;
                Handles.DrawBezier(start, end, startTan, endTan, new Color(0.85f, 0.85f, 0.85f, 0.45f), null, 2f);
            }
            Handles.EndGUI();
        }

        private void DrawArchitectNodes()
        {
            for (int i = 0; i < architectNodes.Count; i++)
            {
                ArchitectNode node = architectNodes[i];
                if (!PassArchitectSearch(node))
                    continue;

                Color old = GUI.color;
                GUI.color = architectSelectedNodeIndex == i ? Color.white : new Color(1f, 1f, 1f, 0.95f);
                node.rect = GUI.Window(i + 1000, node.rect, id => DrawArchitectNodeWindow(id - 1000), GUIContent.none, GetArchitectNodeStyle(node));
                GUI.color = old;
            }
        }

        private void DrawArchitectNodeWindow(int index)
        {
            if (index < 0 || index >= architectNodes.Count)
                return;

            ArchitectNode node = architectNodes[index];
            Rect header = new Rect(0, 0, node.rect.width, 24);
            EditorGUI.DrawRect(header, node.color);
            GUI.Label(new Rect(8, 4, node.rect.width - 16, 18), node.title, EditorStyles.whiteMiniLabel);
            GUI.Label(new Rect(8, 30, node.rect.width - 16, 18), node.type, EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(8, 50, node.rect.width - 16, node.rect.height - 56), node.summary, EditorStyles.wordWrappedMiniLabel);
            GUI.DragWindow(new Rect(0, 0, node.rect.width, node.rect.height));

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                architectSelectedNodeIndex = index;
                Repaint();
            }
        }

        private GUIStyle GetArchitectNodeStyle(ArchitectNode node)
        {
            string key = node.type;
            if (architectStyleCache.TryGetValue(key, out GUIStyle style))
                return style;

            style = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(6, 6, 6, 6)
            };
            architectStyleCache[key] = style;
            return style;
        }

        private void HandleArchitectCanvasEvents()
        {
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0)
            {
                for (int i = architectNodes.Count - 1; i >= 0; i--)
                {
                    if (!PassArchitectSearch(architectNodes[i]) || !architectNodes[i].rect.Contains(current.mousePosition))
                        continue;

                    architectSelectedNodeIndex = i;
                    architectDraggingNode = true;
                    architectDraggingNodeIndex = i;
                    architectDragOffset = current.mousePosition - architectNodes[i].rect.position;
                    current.Use();
                    break;
                }
            }
            else if (current.type == EventType.MouseDrag && architectDraggingNode && architectDraggingNodeIndex >= 0 && architectDraggingNodeIndex < architectNodes.Count)
            {
                architectNodes[architectDraggingNodeIndex].rect.position = current.mousePosition - architectDragOffset;
                current.Use();
                Repaint();
            }
            else if (current.type == EventType.MouseUp)
            {
                architectDraggingNode = false;
                architectDraggingNodeIndex = -1;
            }
        }

        private bool PassArchitectSearch(ArchitectNode node)
        {
            if (node == null)
                return false;

            if (string.IsNullOrWhiteSpace(architectSearchText))
                return true;

            return ContainsArchitectTerm(node, architectSearchText);
        }

        private ArchitectNode GetSelectedArchitectNode()
        {
            if (architectSelectedNodeIndex < 0 || architectSelectedNodeIndex >= architectNodes.Count)
                return null;

            return architectNodes[architectSelectedNodeIndex];
        }

        private int FindArchitectNodeIndex(string id)
        {
            return architectNodes.FindIndex(node => node.id == id);
        }

        private static string ReadArchitectTitle(string filePath)
        {
            try
            {
                foreach (string line in File.ReadLines(filePath, Encoding.UTF8).Take(20))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("#"))
                        return trimmed.TrimStart('#').Trim();
                }
            }
            catch
            {
                return Path.GetFileNameWithoutExtension(filePath);
            }

            return Path.GetFileNameWithoutExtension(filePath);
        }

        private static string ReadArchitectSummary(string filePath)
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                foreach (string line in File.ReadLines(filePath, Encoding.UTF8).Take(80))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                        continue;

                    builder.Append(trimmed).Append(' ');
                    if (builder.Length >= 220)
                        break;
                }

                return LimitText(builder.ToString());
            }
            catch
            {
                return "";
            }
        }

        private static string ReadArchitectSessionSummary(string sessionDir)
        {
            string final = Path.Combine(sessionDir, "Consensus", "最终结论_返回用户.md");
            if (File.Exists(final))
                return ReadArchitectSummary(final);

            string current = Path.Combine(sessionDir, "Consensus", "当前共同意见.md");
            if (File.Exists(current))
                return ReadArchitectSummary(current);

            string desc = Path.Combine(sessionDir, "00_会话说明.md");
            return File.Exists(desc) ? ReadArchitectSummary(desc) : Path.GetFileName(sessionDir);
        }

        private static bool TryReadCodexSessionMeta(string filePath, out string sessionId, out string cwd, out string summary)
        {
            sessionId = "";
            cwd = "";
            summary = "";

            try
            {
                foreach (string line in File.ReadLines(filePath, Encoding.UTF8).Take(40))
                {
                    if (line.IndexOf("\"type\":\"session_meta\"", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    sessionId = ExtractJsonString(line, "session_id");
                    if (string.IsNullOrWhiteSpace(sessionId))
                        sessionId = ExtractJsonString(line, "id");

                    cwd = ExtractJsonString(line, "cwd");
                    summary = $"会话 {ShortId(sessionId)}，工作目录：{cwd}";
                    return !string.IsNullOrWhiteSpace(sessionId);
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool WorkspaceContainsProject(string cwd)
        {
            if (string.IsNullOrWhiteSpace(cwd))
                return false;

            string projectRoot = NormalizePath(ProjectRoot);
            string normalizedCwd = NormalizePath(cwd);
            return string.Equals(projectRoot, normalizedCwd, StringComparison.OrdinalIgnoreCase)
                || projectRoot.StartsWith(normalizedCwd, StringComparison.OrdinalIgnoreCase)
                || normalizedCwd.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string ProjectRoot
        {
            get
            {
                string dataPath = Application.dataPath;
                return dataPath.EndsWith("/Assets", StringComparison.OrdinalIgnoreCase)
                    ? dataPath.Substring(0, dataPath.Length - "/Assets".Length)
                    : Directory.GetParent(dataPath)?.FullName ?? dataPath;
            }
        }

        private void ExportArchitectMarkdown()
        {
            string folder = Path.Combine(ProjectRoot, "Assets/Plugins/ES/AIWarnings/ArchitectReports".Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, "项目全局架构师_思路图.md");
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# 项目全局架构师：思路图");
            builder.AppendLine();
            builder.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine();

            foreach (ArchitectNode node in architectNodes)
            {
                builder.AppendLine($"## {node.title}");
                builder.AppendLine();
                builder.AppendLine($"- 类型：{node.type}");
                builder.AppendLine($"- 来源：{node.sourcePath}");
                builder.AppendLine($"- 摘要：{node.summary}");
                builder.AppendLine();
            }

            builder.AppendLine("## 关系");
            builder.AppendLine();
            foreach (ArchitectEdge edge in architectEdges)
            {
                if (edge.from < 0 || edge.from >= architectNodes.Count || edge.to < 0 || edge.to >= architectNodes.Count)
                    continue;

                builder.AppendLine($"- {architectNodes[edge.from].title} -> {architectNodes[edge.to].title}：{edge.label}");
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(path);
        }

        private static void RevealArchitectSource(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return;

            EditorUtility.RevealInFinder(sourcePath);
        }

        private static string LimitText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            value = value.Trim();
            return value.Length <= 220 ? value : value.Substring(0, 220) + "...";
        }

        private static string ShortId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            string clean = value.Trim();
            return clean.Length <= 10 ? clean : clean.Substring(0, 10);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        }
    }
}
