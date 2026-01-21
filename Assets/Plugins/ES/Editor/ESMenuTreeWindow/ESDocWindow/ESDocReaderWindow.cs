using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;

// 抑制私有字段未使用警告
#pragma warning disable CS0414
// 抑制无法访问的代码警告（提前return）
#pragma warning disable CS0162

namespace ES
{
    /// <summary>
    /// ESDocReaderWindow - ES文档专属阅读器
    /// 高性能渲染、完整层级结构、专业视觉效果
    /// 菜单: Tools/ES文档/ES文档阅读器
    /// </summary>
    public class ESDocReaderWindow : ESMenuTreeWindowAB<ESDocReaderWindow>
    {
        [MenuItem(MenuItemPathDefine.EDITOR_DOCS_PATH + "ES文档阅读器")]
        private static new void OpenWindow()
        {
            OpenDocReaderWindow("📖 ES文档阅读器");
        }

        private static void OpenDocReaderWindow(string title)
        {
            UsingWindow = GetWindow<ESDocReaderWindow>();
            UsingWindow.titleContent = new GUIContent(title, EditorGUIUtility.IconContent("TextAsset Icon").image);
            UsingWindow.minSize = new Vector2(1000, 650);
            UsingWindow.MenuWidth = 280;
            UsingWindow.Show();
        }

        public enum SortMode { 默认, 标题升序, 标题降序, 日期最新, 日期最旧 }

        private ESDocumentPageBase currentDoc;
        private Vector2 contentScroll;
        private Dictionary<string, bool> sectionFoldouts = new Dictionary<string, bool>();
        private SortMode currentSortMode = SortMode.默认;
        private int? focusedSectionIndex = null; // 当前聚焦的章节索引，null表示显示全部
        // 性能缓存
        private Dictionary<ESDocContentBase, string> previewCache = new Dictionary<ESDocContentBase, string>();
        private GUIStyle linkStyleCached;
        private GUIStyle smallItalicStyleCached;
        private GUIStyle titleStyle;
        private GUIStyle sectionHeaderStyle;
        private GUIStyle codeBlockStyle;
        private GUIStyle tableHeaderStyle;
        private GUIStyle tableCellStyle;
        private GUIStyle quoteStyle;
        private GUIStyle headingStyle;
        private GUIStyle subheadingStyle;
        private GUIStyle noteBoxStyle;
        private bool stylesInitialized = false;

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            // 文档标题样式 - 修复显示问题
            titleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 10, 10),
                wordWrap = true,
                fixedHeight = 0,
                stretchHeight = true
            };

            // 一级标题样式
            headingStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(5, 5, 5, 5),
                wordWrap = true,
                normal = { textColor = new Color(0.2f, 0.4f, 0.8f) }
            };

            // 二级标题样式
            subheadingStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(5, 5, 3, 3),
                wordWrap = true
            };

            // 提示框样式
            noteBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 12,
                padding = new RectOffset(12, 12, 10, 10),
                normal = { background = MakeTex(2, 2, new Color(0.9f, 0.95f, 1f, 0.3f)) }
            };

            // 章节标题样式
            sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                padding = new RectOffset(5, 5, 8, 8),
                normal = { background = MakeTex(2, 2, new Color(0.3f, 0.5f, 0.7f, 0.2f)) }
            };

            // 代码块样式
            codeBlockStyle = new GUIStyle(EditorStyles.textArea)
            {
                fontSize = 12,
                font = EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font ?? Font.CreateDynamicFontFromOSFont("Consolas", 12),
                padding = new RectOffset(10, 10, 10, 10),
                normal = { background = MakeTex(2, 2, new Color(0.15f, 0.15f, 0.15f, 1f)), textColor = new Color(0.85f, 0.85f, 0.85f) },
                wordWrap = false
            };

            // 表格表头样式
            tableHeaderStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = MakeTex(2, 2, new Color(0.4f, 0.6f, 0.8f, 0.3f)) }
            };

            // 表格单元格样式
            tableCellStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(5, 5, 3, 3),
                normal = { background = MakeTex(2, 2, new Color(0.2f, 0.2f, 0.2f, 0.1f)) }
            };

            // 引用块样式
            quoteStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                padding = new RectOffset(15, 15, 10, 10),
                normal = { background = MakeTex(2, 2, new Color(0.3f, 0.3f, 0.4f, 0.15f)) }
            };

            // 链接样式缓存
            linkStyleCached = new GUIStyle(EditorStyles.linkLabel)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };

            // 小斜体样式
            smallItalicStyleCached = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                fontStyle = FontStyle.Italic,
                wordWrap = true
            };

            stylesInitialized = true;
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        protected override void ES_OnBuildMenuTree(OdinMenuTree tree)
        {
            base.ES_OnBuildMenuTree(tree);

            tree.Config.DrawSearchToolbar = true;
            tree.DefaultMenuStyle.IconSize = 20;
            tree.DefaultMenuStyle.Height = 28;

            // 动态加载文档
            LoadDocumentsToTree(tree);
        }

        private void LoadDocumentsToTree(OdinMenuTree tree)
        {
            tree.Add("📘 阅读首页", new Page_ReaderHome());

            string docPath = "Assets/ES/Documentation";
            if (!AssetDatabase.IsValidFolder(docPath))
            {
                tree.Add("📚 文档库/⚠️ 路径不存在", null);
                tree.Add("📚 文档库/💡 提示", new Page_NoDocuments());
                return;
            }

            var guids = AssetDatabase.FindAssets("t:ESDocumentPageBase", new[] { docPath });
            if (guids.Length == 0)
            {
                tree.Add("📚 文档库/📭 暂无文档", null);
                tree.Add("📚 文档库/💡 提示", new Page_NoDocuments());
                return;
            }

            var docs = new List<ESDocumentPageBase>();
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var d = AssetDatabase.LoadAssetAtPath<ESDocumentPageBase>(p);
                if (d != null) docs.Add(d);
            }

            // 排序文档
            docs = SortDocuments(docs);

            // 按分类分组
            var groups = docs.GroupBy(d => string.IsNullOrEmpty(d.category) ? "📂 未分类" : "📂 " + d.category)
                             .OrderBy(g => g.Key);

            int totalCount = 0;
            foreach (var g in groups)
            {
                foreach (var d in g)
                {
                    // 为每个文档创建主项
                    var str = $"文档库/{g.Key}/{d.documentTitle}";
                    var docItem = tree.Add(str, d);

                    // 为每个章节创建子菜单
                    if (d.sections != null && d.sections.Count > 0)
                    {
                        for (int i = 0; i < d.sections.Count; i++)
                        {
                            var section = d.sections[i];
                            var sectionProxy = new SectionProxy { document = d, sectionIndex = i };
                            tree.Add($"{str}/{i}.{section.sectionTitle}", sectionProxy);
                        }
                    }

                    totalCount++;
                }
            }

            // 默认展开前两层（文档库 -> 分类）
            tree.EnumerateTree().ForEach(item =>
            {
                if (item.Value == null || item.Value is Page_ReaderHome || item.Value is Page_Statistics)
                {
                    item.Toggled = item.GetParentMenuItemsRecursive(false).Count() < 2;
                }
            });

            // 统计信息 - 填充完整表格
            var stats = new Page_Statistics
            {
                documentCount = totalCount,
                categoryCount = groups.Count(),
                currentSortMode = currentSortMode.ToString(),
                allDocuments = docs.Select(d => new Page_Statistics.DocStatItem
                {
                    title = d.documentTitle,
                    category = string.IsNullOrEmpty(d.category) ? "未分类" : d.category,
                    author = string.IsNullOrEmpty(d.author) ? "未知" : d.author,
                    createDate = d.createDate,
                    lastModified = d.lastModified,
                    sectionCount = d.sections?.Count ?? 0
                }).ToList()
            };
            tree.Add($"📊 统计信息 ({totalCount} 篇文档)", stats);

            // 设定刷新入口（可从外部调用）
            // External callers can call ESDocReaderWindow.DoRefresh() to reload docs while editing
        }

        // 外部可调用：刷新文档库并重建菜单树
        public static void DoRefresh()
        {
            if (UsingWindow == null) UsingWindow = GetWindow<ESDocReaderWindow>();
            UsingWindow.InternalRefresh();
        }

        private void InternalRefresh()
        {
            AssetDatabase.Refresh();
            ForceMenuTreeRebuild();
            previewCache.Clear();
            Repaint();
        }

        private List<ESDocumentPageBase> SortDocuments(List<ESDocumentPageBase> docs)
        {
            switch (currentSortMode)
            {
                case SortMode.标题升序:
                    return docs.OrderBy(d => d.documentTitle).ToList();
                case SortMode.标题降序:
                    return docs.OrderByDescending(d => d.documentTitle).ToList();
                case SortMode.日期最新:
                    return docs.OrderByDescending(d => d.lastModified).ToList();
                case SortMode.日期最旧:
                    return docs.OrderBy(d => d.lastModified).ToList();
                default:
                    return docs;
            }
        }

        // 章节代理类，用于在菜单中表示章节
        [Serializable]
        public class SectionProxy
        {
            [HideInInspector]
            public ESDocumentPageBase document;
            public int sectionIndex;
        }

        protected override void OnImGUI()
        {
            if (UsingWindow == null) UsingWindow = this;
            InitializeStyles();
            base.OnImGUI();
        }

        protected override void DrawEditors()
        {
            // 根据菜单选择更新当前文档
            if (MenuTree != null && MenuTree.Selection != null)
            {
                if (MenuTree.Selection.SelectedValue is ESDocumentPageBase selectedDoc)
                {
                    if (currentDoc != selectedDoc || focusedSectionIndex != null)
                    {
                        currentDoc = selectedDoc;
                        contentScroll = Vector2.zero;
                        sectionFoldouts.Clear();
                        focusedSectionIndex = null; // 选中文档时显示全部章节
                    }
                }
                else if (MenuTree.Selection.SelectedValue is SectionProxy sectionProxy)
                {
                    // 选中章节时，只显示该特定章节
                    if (currentDoc != sectionProxy.document || focusedSectionIndex != sectionProxy.sectionIndex)
                    {
                        currentDoc = sectionProxy.document;
                        contentScroll = Vector2.zero;
                        sectionFoldouts.Clear();
                        focusedSectionIndex = sectionProxy.sectionIndex; // 设置聚焦章节
                    }
                    // 确保该章节展开
                    string foldoutKey = $"section_{sectionProxy.sectionIndex}_{sectionProxy.document.sections[sectionProxy.sectionIndex].sectionTitle}";
                    sectionFoldouts[foldoutKey] = true;
                }
                else if (MenuTree.Selection.SelectedValue is ESWindowPageBase)
                {
                    currentDoc = null;
                    focusedSectionIndex = null;
                }
            }

            EditorGUILayout.BeginVertical(GUILayout.ExpandHeight(true));

            if (currentDoc == null)
            {
                DrawEmptyState();
            }
            else
            {
                DrawDocumentReader(currentDoc);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawEmptyState()
        {
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical(GUILayout.Width(500));
            GUILayout.FlexibleSpace();

            // 大标题
            var titleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 32,
                fontStyle = FontStyle.Bold
            };
            GUILayout.Label("📖", titleStyle);
            GUILayout.Space(10);

            titleStyle.fontSize = 24;
            EditorGUILayout.LabelField("ES 文档阅读器", titleStyle);
            GUILayout.Space(5);

            // 副标题
            var subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Italic
            };
            EditorGUILayout.LabelField("高性能 · 专业渲染 · 完整层级", subtitleStyle);

            GUILayout.Space(30);

            // 装饰线
            var lineRect = GUILayoutUtility.GetRect(400, 2);
            EditorGUI.DrawRect(new Rect(lineRect.x + 50, lineRect.y, lineRect.width - 100, 2), new Color(0.5f, 0.7f, 0.9f, 0.5f));

            GUILayout.Space(20);

            // 提示信息
            var hintStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                wordWrap = true
            };
            EditorGUILayout.LabelField("👈 从左侧菜单选择文档开始阅读", hintStyle);

            GUILayout.Space(30);

            // 功能图标网格
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DrawFeatureIcon("📊", "表格支持");
            GUILayout.Space(20);
            DrawFeatureIcon("💻", "代码高亮");
            GUILayout.Space(20);
            DrawFeatureIcon("🖼️", "图片预览");
            GUILayout.Space(20);
            DrawFeatureIcon("🔗", "链接跳转");
            GUILayout.Space(20);
            DrawFeatureIcon("📝", "Markdown导出");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(20);

            // 第二行图标
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DrawFeatureIcon("⚠️", "警告提示");
            GUILayout.Space(20);
            DrawFeatureIcon("💬", "引用块");
            GUILayout.Space(20);
            DrawFeatureIcon("📑", "章节导航");
            GUILayout.Space(20);
            DrawFeatureIcon("🔍", "快速搜索");
            GUILayout.Space(20);
            DrawFeatureIcon("✨", "精美渲染");
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
        }

        private void DrawFeatureIcon(string icon, string label)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(90), GUILayout.Height(80));
            GUILayout.FlexibleSpace();
            var iconStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fixedHeight = 35
            };
            GUILayout.Label(icon, iconStyle);
            GUILayout.Space(3);
            var labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                wordWrap = true
            };
            GUILayout.Label(label, labelStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
        }

        private void DrawDocumentReader(ESDocumentPageBase doc)
        {
            // 顶部工具栏
            DrawToolbar(doc);

            // 内容滚动区域
            contentScroll = EditorGUILayout.BeginScrollView(contentScroll, GUILayout.ExpandHeight(true));

            GUILayout.Space(10);



            // 渲染章节
            if (doc.sections != null && doc.sections.Count > 0)
            {
                if (focusedSectionIndex.HasValue)
                {
                    // 只绘制特定章节
                    if (focusedSectionIndex.Value >= 0 && focusedSectionIndex.Value < doc.sections.Count)
                    {
                        // 显示提示信息
                        var hintRect = EditorGUILayout.GetControlRect(false, 24);
                        EditorGUI.DrawRect(hintRect, new Color(0.4f, 0.6f, 0.9f, 0.15f));
                        var hintStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            fontSize = 11,
                            fontStyle = FontStyle.Italic,
                            alignment = TextAnchor.MiddleCenter
                        };
                        GUI.Label(hintRect, $"📍 当前显示: 第 {focusedSectionIndex.Value + 1} 章节 （点击文档名称查看全部章节）", hintStyle);
                        GUILayout.Space(10);

                        DrawSection(doc.sections[focusedSectionIndex.Value], focusedSectionIndex.Value);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("❌ 章节索引无效", MessageType.Error);
                    }
                }
                else
                {
                    // 文档标题区
                    DrawDocumentHeader(doc);

                    GUILayout.Space(15);
                    DrawHorizontalLine(new Color(0.5f, 0.5f, 0.5f, 0.3f));
                    GUILayout.Space(15);
                    // 绘制所有章节
                    for (int i = 0; i < doc.sections.Count; i++)
                    {
                        DrawSection(doc.sections[i], i);
                        if (i < doc.sections.Count - 1)
                        {
                            GUILayout.Space(20);
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("📭 此文档暂无内容", MessageType.Info);
            }

            GUILayout.Space(30);
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar(ESDocumentPageBase doc)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(25));

            if (GUILayout.Button(new GUIContent("📄 导出Markdown", "导出为Markdown格式"), EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                ExportMarkdown(doc);
            }

            if (GUILayout.Button(new GUIContent("🌐 导出HTML", "导出为HTML格式"), EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                ExportHTML(doc);
            }

            if (GUILayout.Button(new GUIContent("📋 复制文本", "复制为纯文本"), EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                CopyPlainText(doc);
            }

            GUILayout.FlexibleSpace();

            // 排序下拉菜单
            var newSortMode = (SortMode)EditorGUILayout.EnumPopup(currentSortMode, EditorStyles.toolbarDropDown, GUILayout.Width(100));
            if (newSortMode != currentSortMode)
            {
                currentSortMode = newSortMode;
                ForceMenuTreeRebuild();
            }

            if (GUILayout.Button(new GUIContent("🔄 刷新", "重新加载文档"), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                AssetDatabase.Refresh();
                ForceMenuTreeRebuild();
                Repaint();
            }

            if (GUILayout.Button(new GUIContent("✏️ 编辑", "在Inspector中打开"), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                Selection.activeObject = doc;
                EditorGUIUtility.PingObject(doc);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ExportMarkdown(ESDocumentPageBase doc)
        {
            try
            {
                // 自己生成Markdown
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"# {doc.documentTitle}");
                sb.AppendLine();
                sb.AppendLine($"**分类**: {doc.category}");
                if (!string.IsNullOrEmpty(doc.author))
                    sb.AppendLine($"**作者**: {doc.author}");
                sb.AppendLine($"**创建日期**: {doc.createDate}");
                sb.AppendLine($"**最后修改**: {doc.lastModified}");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();

                foreach (var section in doc.sections)
                {
                    sb.AppendLine(section.ToMarkdown());
                    sb.AppendLine();
                }

                string md = sb.ToString();
                string path = EditorUtility.SaveFilePanel("导出Markdown", "", doc.documentTitle, "md");
                if (!string.IsNullOrEmpty(path))
                {
                    System.IO.File.WriteAllText(path, md, System.Text.Encoding.UTF8);
                    EditorUtility.DisplayDialog("导出成功", $"已导出到:\n{path}", "确定");
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("导出失败", e.Message, "确定");
            }
        }

        private void ExportHTML(ESDocumentPageBase doc)
        {
            try
            {
                // 自己生成HTML
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<!DOCTYPE html>");
                sb.AppendLine("<html><head><meta charset='utf-8'>");
                sb.AppendLine($"<title>{doc.documentTitle}</title>");
                sb.AppendLine("<style>body{font-family:Arial,sans-serif;max-width:900px;margin:40px auto;padding:20px;}h1{border-bottom:2px solid #333;}table{border-collapse:collapse;width:100%;}th,td{border:1px solid #ddd;padding:8px;text-align:left;}th{background-color:#f2f2f2;}code{background:#f4f4f4;padding:2px 6px;border-radius:3px;}pre{background:#f4f4f4;padding:10px;border-radius:5px;overflow-x:auto;}.quote{border-left:4px solid #ccc;margin:10px 0;padding:10px 20px;background:#f9f9f9;}.alert{padding:12px;margin:10px 0;border-radius:4px;}.alert-info{background:#d1ecf1;border-left:4px solid #0c5460;}.alert-warning{background:#fff3cd;border-left:4px solid #856404;}.alert-error{background:#f8d7da;border-left:4px solid #721c24;}</style>");
                sb.AppendLine("</head><body>");
                sb.AppendLine($"<h1>{doc.documentTitle}</h1>");
                sb.AppendLine($"<p><strong>分类:</strong> {doc.category} | <strong>作者:</strong> {doc.author} | <strong>更新:</strong> {doc.lastModified}</p>");
                sb.AppendLine("<hr>");

                foreach (var section in doc.sections)
                {
                    sb.AppendLine(section.ToHTML());
                }

                sb.AppendLine("</body></html>");
                string html = sb.ToString();
                string path = EditorUtility.SaveFilePanel("导出HTML", "", doc.documentTitle, "html");
                if (!string.IsNullOrEmpty(path))
                {
                    System.IO.File.WriteAllText(path, html, System.Text.Encoding.UTF8);
                    if (EditorUtility.DisplayDialog("导出成功", $"已导出到:\n{path}\n\n是否在浏览器中打开?", "打开", "取消"))
                    {
                        Application.OpenURL("file:///" + path.Replace("\\", "/"));
                    }
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("导出失败", e.Message, "确定");
            }
        }

        private void CopyPlainText(ESDocumentPageBase doc)
        {
            try
            {
                // 自己生成纯文本
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(doc.documentTitle);
                sb.AppendLine(new string('=', doc.documentTitle.Length));
                sb.AppendLine();
                sb.AppendLine($"分类: {doc.category}");
                sb.AppendLine($"作者: {doc.author}");
                sb.AppendLine($"更新: {doc.lastModified}");
                sb.AppendLine();

                foreach (var section in doc.sections)
                {
                    sb.AppendLine(section.ToPlainText());
                    sb.AppendLine();
                }

                string text = sb.ToString();
                EditorGUIUtility.systemCopyBuffer = text;
                Debug.Log("✅ 文本已复制到剪贴板");
            }
            catch (Exception e)
            {
                Debug.LogError($"复制失败: {e.Message}");
            }
        }

        private void DrawDocumentHeader(ESDocumentPageBase doc)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            // 标题
            GUILayout.Label(doc.documentTitle, titleStyle);

            GUILayout.Space(5);

            // 元信息
            EditorGUILayout.BeginHorizontal();

            DrawMetaInfo("📂", "分类", doc.category);
            GUILayout.FlexibleSpace();
            DrawMetaInfo("✍️", "作者", doc.author);
            GUILayout.FlexibleSpace();
            DrawMetaInfo("📅", "创建", doc.createDate);
            GUILayout.FlexibleSpace();
            DrawMetaInfo("🔄", "更新", doc.lastModified);

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawMetaInfo(string icon, string label, string value)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(150));
            var labelStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            EditorGUILayout.LabelField($"{icon} {label}", labelStyle);
            EditorGUILayout.LabelField(string.IsNullOrEmpty(value) ? "未设置" : value, EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawSection(ESDocSection section, int index)
        {
            if (section == null || section.content == null) return;

            string foldoutKey = $"section_{index}_{section.sectionTitle}";
            if (!sectionFoldouts.ContainsKey(foldoutKey))
            {
                sectionFoldouts[foldoutKey] = true;
            }

            // 章节标题（可折叠）- 使用自定义布局确保左对齐
            var headerRect = EditorGUILayout.GetControlRect(false, 36);
            EditorGUI.DrawRect(headerRect, new Color(0.25f, 0.45f, 0.75f, 0.15f));

            // 左侧装饰条
            var decorRect = new Rect(headerRect.x, headerRect.y, 3, headerRect.height);
            EditorGUI.DrawRect(decorRect, new Color(0.3f, 0.6f, 0.9f, 0.8f));

            var foldoutRect = new Rect(headerRect.x + 8, headerRect.y + 10, 15, 15);
            sectionFoldouts[foldoutKey] = EditorGUI.Foldout(foldoutRect, sectionFoldouts[foldoutKey], "", true);

            var labelRect = new Rect(headerRect.x + 30, headerRect.y + 8, headerRect.width - 120, 20);
            var labelStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, fixedHeight = 20 };
            EditorGUI.LabelField(labelRect, $"📑 {section.sectionTitle}", labelStyle);

            var countRect = new Rect(headerRect.xMax - 80, headerRect.y + 11, 70, 15);
            EditorGUI.LabelField(countRect, $"({section.content.Count} 项)", EditorStyles.miniLabel);

            if (sectionFoldouts[foldoutKey])
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Space(8);

                for (int i = 0; i < section.content.Count; i++)
                {
                    DrawContentElement(section.content[i]);

                    if (i < section.content.Count - 1)
                    {
                        GUILayout.Space(10);
                        // 分隔线
                        var lineRect = GUILayoutUtility.GetRect(1, 1);
                        EditorGUI.DrawRect(new Rect(lineRect.x + 10, lineRect.y, lineRect.width - 20, 1), new Color(0.5f, 0.5f, 0.5f, 0.2f));
                        GUILayout.Space(10);
                    }
                }

                GUILayout.Space(8);
                EditorGUILayout.EndVertical();
            }
            else
            {
                // 预览模式：显示前3个元素的摘要
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Space(5);
                var previewStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic };
                int previewCount = Mathf.Min(3, section.content.Count);
                for (int i = 0; i < previewCount; i++)
                {
                    string preview = GetContentPreview(section.content[i]);
                    if (!string.IsNullOrEmpty(preview))
                    {
                        EditorGUILayout.LabelField($"  • {preview}", previewStyle);
                    }
                }
                if (section.content.Count > 3)
                {
                    EditorGUILayout.LabelField($"  ... 还有 {section.content.Count - 3} 项", previewStyle);
                }
                GUILayout.Space(5);
                EditorGUILayout.EndVertical();
            }
        }

        private string GetContentPreview(ESDocContentBase item)
        {
            if (item == null) return "";

            if (previewCache.TryGetValue(item, out var cached))
            {
                return cached;
            }

            string result;
            switch (item)
            {
                case ESDocText text:
                    result = text.content?.Length > 50 ? text.content.Substring(0, 50) + "..." : text.content;
                    break;
                case ESDocCodeBlock code:
                    result = $"💻 代码块 ({code.language})";
                    break;
                case ESDocTable table:
                    result = $"📊 表格: {table.tableTitle}";
                    break;
                case ESDocImage img:
                    result = $"🖼️ 图片: {img.caption}";
                    break;
                case ESDocLink link:
                    result = $"🔗 {link.displayText}";
                    break;
                case ESDocUnorderedList ul:
                    result = $"📋 无序列表 ({ul.items?.Count ?? 0} 项)";
                    break;
                case ESDocOrderedList ol:
                    result = $"🔢 有序列表 ({ol.items?.Count ?? 0} 项)";
                    break;
                case ESDocQuote quote:
                    var q = quote.quoteText ?? "";
                    result = $"💬 引用: {q.Substring(0, Math.Min(30, q.Length))}...";
                    break;
                case ESDocAlert alert:
                    result = $"⚠️ {alert.alertType}: {alert.title}";
                    break;
                default:
                    result = item.GetType().Name;
                    break;
            }

            previewCache[item] = result;
            return result;
        }

        private void DrawContentElement(ESDocContentBase item)
        {
            if (item == null) return;

            switch (item)
            {
                case ESDocText text:
                    DrawTextElement(text);
                    break;
                case ESDocCodeBlock code:
                    DrawCodeBlock(code);
                    break;
                case ESDocTable table:
                    DrawTable(table);
                    break;
                case ESDocImage image:
                    DrawImage(image);
                    break;
                case ESDocLink link:
                    DrawLink(link);
                    break;
                case ESDocUnorderedList ul:
                    DrawUnorderedList(ul);
                    break;
                case ESDocOrderedList ol:
                    DrawOrderedList(ol);
                    break;
                case ESDocQuote quote:
                    DrawQuote(quote);
                    break;
                case ESDocAlert alert:
                    DrawAlert(alert);
                    break;
                case ESDocDivider divider:
                    DrawDivider();
                    break;
                default:
                    EditorGUILayout.LabelField(item.ToPlainText(), EditorStyles.wordWrappedLabel);
                    break;
            }
        }

        private void DrawTextElement(ESDocText text)
        {
            if (string.IsNullOrEmpty(text.content)) return;

            var textStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 13,
                padding = new RectOffset(8, 8, 6, 6),
                wordWrap = true,
                richText = true
            };

            // 自动计算高度
            var content = new GUIContent(text.content);
            float height = textStyle.CalcHeight(content, EditorGUIUtility.currentViewWidth - 400);
            EditorGUILayout.LabelField(text.content, textStyle, GUILayout.Height(Mathf.Max(height, 20)));
        }

        private void DrawCodeBlock(ESDocCodeBlock code)
        {
            if (string.IsNullOrEmpty(code.code)) return;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            // 代码块标题栏 - Unity官方风格
            var titleBarRect = EditorGUILayout.GetControlRect(false, 28);
            EditorGUI.DrawRect(titleBarRect, new Color(0.2f, 0.2f, 0.25f, 0.95f));

            var iconRect = new Rect(titleBarRect.x + 8, titleBarRect.y + 5, 18, 18);
            GUI.Label(iconRect, "💻", new GUIStyle(EditorStyles.label) { fontSize = 16 });

            var langRect = new Rect(titleBarRect.x + 30, titleBarRect.y + 6, 100, 16);
            var langStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.7f, 0.85f, 1f) }
            };
            GUI.Label(langRect, code.language.ToUpper(), langStyle);

            var btnRect = new Rect(titleBarRect.xMax - 90, titleBarRect.y + 4, 85, 20);
            if (GUI.Button(btnRect, new GUIContent(" 📋 复制", "复制代码到剪贴板"), EditorStyles.miniButton))
            {
                EditorGUIUtility.systemCopyBuffer = code.code;
                Debug.Log("✅ 代码已复制到剪贴板");
            }

            GUILayout.Space(2);

            // 代码内容区
            int lines = code.code.Split('\n').Length;
            float height = Mathf.Clamp(20 + lines * 16, 80, 1200);

            var codeStyle = new GUIStyle(EditorStyles.textArea)
            {
                fontSize = 12,
                font = Font.CreateDynamicFontFromOSFont("Consolas", 12),
                padding = new RectOffset(12, 12, 10, 10),
                normal = { background = MakeTex(2, 2, new Color(0.12f, 0.12f, 0.15f, 1f)), textColor = new Color(0.88f, 0.88f, 0.88f) },
                wordWrap = false
            };

            // 使用显式 Rect 绘制，避免 GUILayout 有时在滚动视图底部截断内容
            var codeRect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 60, height, GUILayout.ExpandWidth(true));
            // Draw background manually (ensure consistent look)
            EditorGUI.DrawRect(codeRect, new Color(0.12f, 0.12f, 0.15f, 1f));
            // 绘制可编辑/可选择文本区域
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.TextArea(new Rect(codeRect.x + 8, codeRect.y + 8, codeRect.width - 16, codeRect.height - 16), code.code, codeStyle);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();
        }

        private void DrawTable(ESDocTable table)
        {
            if (table == null || table.headers == null || table.headers.Count == 0) return;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            // 表格标题
            if (!string.IsNullOrEmpty(table.tableTitle))
            {
                var titleRect = EditorGUILayout.GetControlRect(false, 30);
                EditorGUI.DrawRect(titleRect, new Color(0.3f, 0.5f, 0.8f, 0.15f));
                var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                GUI.Label(titleRect, $"📊 {table.tableTitle}", titleStyle);
                GUILayout.Space(2);
            }

            // 计算列宽
            int cols = table.headers.Count;
            float availableWidth = EditorGUIUtility.currentViewWidth - 380;
            float colWidth = Mathf.Max(100, availableWidth / cols);

            // 表头 - Unity官方风格
            var headerRect = EditorGUILayout.GetControlRect(false, 28);
            EditorGUI.DrawRect(headerRect, new Color(0.35f, 0.55f, 0.85f, 0.25f));

            float xPos = headerRect.x + 5;
            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            foreach (var header in table.headers)
            {
                var cellRect = new Rect(xPos, headerRect.y + 6, colWidth - 10, 16);
                GUI.Label(cellRect, header, headerStyle);
                xPos += colWidth;
            }

            // 数据行
            if (table.rows != null)
            {
                for (int rowIndex = 0; rowIndex < table.rows.Count; rowIndex++)
                {
                    var row = table.rows[rowIndex];
                    if (row == null || row.cells == null) continue;

                    var rowRect = EditorGUILayout.GetControlRect(false, 24);

                    // 交替行背景
                    if (rowIndex % 2 == 0)
                    {
                        EditorGUI.DrawRect(rowRect, new Color(0.25f, 0.25f, 0.28f, 0.08f));
                    }

                    xPos = rowRect.x + 5;
                    var cellStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        fontSize = 11
                    };

                    for (int i = 0; i < cols; i++)
                    {
                        string cellContent = i < row.cells.Count ? row.cells[i] : "";
                        var cellRect = new Rect(xPos, rowRect.y + 4, colWidth - 10, 16);
                        GUI.Label(cellRect, cellContent, cellStyle);
                        xPos += colWidth;
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawImage(ESDocImage image)
        {
            if (image == null) return;

            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Space(5);

            if (image.image != null)
            {
                Texture2D tex = AssetPreview.GetAssetPreview(image.image);
                if (tex == null && image.image is Texture2D)
                {
                    tex = image.image as Texture2D;
                }

                if (tex != null)
                {
                    float maxWidth = Mathf.Min(650, EditorGUIUtility.currentViewWidth - 400);
                    float ratio = (float)tex.height / tex.width;
                    float displayWidth = Mathf.Min(maxWidth, tex.width);
                    float displayHeight = displayWidth * ratio;

                    // 图片边框
                    var bgRect = GUILayoutUtility.GetRect(displayWidth + 4, displayHeight + 4);
                    EditorGUI.DrawRect(bgRect, new Color(0.3f, 0.3f, 0.35f, 0.3f));

                    var imgRect = new Rect(bgRect.x + 2, bgRect.y + 2, displayWidth, displayHeight);
                    EditorGUI.DrawPreviewTexture(imgRect, tex, null, ScaleMode.ScaleToFit);
                }
                else
                {
                    var loadingRect = EditorGUILayout.GetControlRect(false, 60);
                    EditorGUI.DrawRect(loadingRect, new Color(0.2f, 0.2f, 0.25f, 0.5f));
                    var loadStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 12 };
                    GUI.Label(loadingRect, "⏳ 图片预览加载中...", loadStyle);
                }
            }
            else if (!string.IsNullOrEmpty(image.imagePath))
            {
                var pathRect = EditorGUILayout.GetControlRect(false, 24);
                EditorGUI.DrawRect(pathRect, new Color(0.25f, 0.25f, 0.3f, 0.3f));
                var pathStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
                GUI.Label(new Rect(pathRect.x + 8, pathRect.y + 5, pathRect.width - 16, 14), $"🖼️ 图片路径: {image.imagePath}", pathStyle);
            }
            else
            {
                EditorGUILayout.HelpBox("❌ 未设置图片", MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(image.caption))
            {
                GUILayout.Space(6);
                var captionStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontStyle = FontStyle.Italic,
                    wordWrap = true,
                    fontSize = 11
                };
                EditorGUILayout.LabelField(image.caption, captionStyle);
            }

            GUILayout.Space(5);
            EditorGUILayout.EndVertical();
        }

        private void DrawLink(ESDocLink link)
        {
            if (link == null) return;

            var linkRect = EditorGUILayout.GetControlRect(false, 32);
            EditorGUI.DrawRect(linkRect, new Color(0.25f, 0.45f, 0.75f, 0.12f));

            // 左侧图标
            var iconRect = new Rect(linkRect.x + 8, linkRect.y + 7, 18, 18);
            GUI.Label(iconRect, "🔗", new GUIStyle(EditorStyles.label) { fontSize = 16 });

            // 链接文本
            var linkTextRect = new Rect(linkRect.x + 32, linkRect.y + 8, linkRect.width - 120, 16);
            var linkStyle = new GUIStyle(EditorStyles.linkLabel)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };

            if (GUI.Button(linkTextRect, link.displayText, linkStyle))
            {
                if (!string.IsNullOrEmpty(link.url))
                {
                    Application.OpenURL(link.url);
                    Debug.Log($"🔗 打开链接: {link.url}");
                }
            }

            // 右侧按钮
            var btnRect = new Rect(linkRect.xMax - 80, linkRect.y + 6, 75, 20);
            if (GUI.Button(btnRect, "外部打开", EditorStyles.miniButton))
            {
                if (!string.IsNullOrEmpty(link.url))
                {
                    Application.OpenURL(link.url);
                }
            }

            if (!string.IsNullOrEmpty(link.description))
            {
                GUILayout.Space(3);
                var descRect = EditorGUILayout.GetControlRect(false, 18);
                var descStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Italic,
                    padding = new RectOffset(40, 8, 0, 0)
                };
                GUI.Label(descRect, link.description, descStyle);
            }

            if (!string.IsNullOrEmpty(link.url))
            {
                GUILayout.Space(2);
                var urlRect = EditorGUILayout.GetControlRect(false, 14);
                var urlStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    padding = new RectOffset(40, 8, 0, 0),
                    normal = { textColor = Color.gray }
                };
                GUI.Label(urlRect, link.url, urlStyle);
            }
        }

        private void DrawUnorderedList(ESDocUnorderedList list)
        {
            if (list == null || list.items == null || list.items.Count == 0) return;

            var listStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                padding = new RectOffset(20, 5, 2, 2)
            };

            foreach (var item in list.items)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("•", GUILayout.Width(15));
                EditorGUILayout.LabelField(item, listStyle);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawOrderedList(ESDocOrderedList list)
        {
            if (list == null || list.items == null || list.items.Count == 0) return;

            var listStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                padding = new RectOffset(5, 5, 2, 2)
            };

            for (int i = 0; i < list.items.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"{i + 1}.", GUILayout.Width(25));
                EditorGUILayout.LabelField(list.items[i], listStyle);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawQuote(ESDocQuote quote)
        {
            if (quote == null || string.IsNullOrEmpty(quote.quoteText)) return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical();

            var quoteRect = EditorGUILayout.GetControlRect(false, 6);
            EditorGUI.DrawRect(new Rect(quoteRect.x, quoteRect.y, 4, 40), new Color(0.4f, 0.6f, 0.9f, 0.6f));

            EditorGUILayout.BeginVertical(quoteStyle);

            // 引号图标
            var iconStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 28,
                normal = { textColor = new Color(0.5f, 0.7f, 0.95f, 0.5f) },
                fixedHeight = 30
            };
            GUILayout.Label("❝", iconStyle);

            GUILayout.Space(-20);

            var quoteTextStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                padding = new RectOffset(10, 10, 5, 5),
                wordWrap = true
            };

            var content = new GUIContent(quote.quoteText);
            float height = quoteTextStyle.CalcHeight(content, EditorGUIUtility.currentViewWidth - 450);
            EditorGUILayout.LabelField(quote.quoteText, quoteTextStyle, GUILayout.Height(Mathf.Max(height, 20)));

            if (!string.IsNullOrEmpty(quote.source))
            {
                GUILayout.Space(6);
                var sourceStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontStyle = FontStyle.Bold,
                    fontSize = 11
                };
                EditorGUILayout.LabelField($"— {quote.source}", sourceStyle);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawAlert(ESDocAlert alert)
        {
            if (alert == null) return;

#pragma warning disable CS0168
#pragma warning disable CS0219
            MessageType msgType = MessageType.Info;
#pragma warning restore CS0219
#pragma warning restore CS0168
            string icon = "ℹ️";
            Color bgColor = new Color(0.5f, 0.7f, 0.95f, 0.2f);
            Color borderColor = new Color(0.3f, 0.5f, 0.9f, 0.6f);

            switch (alert.alertType)
            {
                case ESDocAlert.AlertType.Info:
                    msgType = MessageType.Info;
                    icon = "💬";
                    bgColor = new Color(0.5f, 0.7f, 0.95f, 0.15f);
                    borderColor = new Color(0.3f, 0.6f, 0.95f, 0.5f);
                    break;
                case ESDocAlert.AlertType.Success:
                    msgType = MessageType.Info;
                    icon = "✅";
                    bgColor = new Color(0.5f, 0.9f, 0.6f, 0.15f);
                    borderColor = new Color(0.3f, 0.8f, 0.4f, 0.5f);
                    break;
                case ESDocAlert.AlertType.Warning:
                    msgType = MessageType.Warning;
                    icon = "⚠️";
                    bgColor = new Color(0.95f, 0.8f, 0.4f, 0.15f);
                    borderColor = new Color(0.9f, 0.7f, 0.2f, 0.5f);
                    break;
                case ESDocAlert.AlertType.Error:
                    msgType = MessageType.Error;
                    icon = "❌";
                    bgColor = new Color(0.95f, 0.5f, 0.5f, 0.15f);
                    borderColor = new Color(0.9f, 0.3f, 0.3f, 0.5f);
                    break;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical();

            // 左侧边框
            var borderRect = EditorGUILayout.GetControlRect(false, 6);
            EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.y, 4, 60), borderColor);

            EditorGUILayout.BeginVertical(GUI.skin.box);

            // var bgRect = GUILayoutUtility.GetLastRect();
            // EditorGUI.DrawRect(bgRect, bgColor);

            GUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);

            // 图标
            var iconStyle = new GUIStyle(EditorStyles.largeLabel) { fontSize = 20, fixedHeight = 24 };
            GUILayout.Label(icon, iconStyle, GUILayout.Width(30));

            // 标题和内容
            EditorGUILayout.BeginVertical();

            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            GUILayout.Label(alert.title, titleStyle);

            GUILayout.Space(4);

            var contentStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 12,
                wordWrap = true
            };
            var content = new GUIContent(alert.content);
            float height = contentStyle.CalcHeight(content, EditorGUIUtility.currentViewWidth - 480);
            EditorGUILayout.LabelField(alert.content, contentStyle, GUILayout.Height(Mathf.Max(height, 18)));

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDivider()
        {
            GUILayout.Space(5);
            DrawHorizontalLine(new Color(0.5f, 0.5f, 0.5f, 0.5f));
            GUILayout.Space(5);
        }

        private void DrawHorizontalLine(Color color)
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, color);
        }

        [Serializable]
        public class Page_ReaderHome : ESWindowPageBase
        {
            [Title("📖 ES 文档阅读器", "专业的文档阅读体验", bold: true, titleAlignment: TitleAlignments.Centered)]

            [InfoBox("欢迎使用ES文档阅读器！这是一个专为ES框架设计的高性能文档阅读工具。", InfoMessageType.Info)]

            [FoldoutGroup("功能特性")]
            [DisplayAsString(fontSize: 13), HideLabel]
            public string features = "✨ 功能特性:\n\n• 🎨 精美的视觉呈现\n• 📊 完整的表格支持\n• 💻 代码高亮显示\n• 🖼️ 图片预览\n• 🔗 链接跳转\n• 📋 一键复制\n• 📄 多格式导出（Markdown/HTML）\n• 🔍 快速搜索\n• 📑 章节折叠";

            [FoldoutGroup("使用指南")]
            [DisplayAsString(fontSize: 13), HideLabel]
            public string guide = "📚 使用指南:\n\n1️⃣ 从左侧菜单选择要阅读的文档\n2️⃣ 在右侧阅读器中浏览内容\n3️⃣ 使用工具栏导出或编辑文档\n4️⃣ 点击章节标题可折叠/展开内容";

            [FoldoutGroup("快捷操作")]
            [Button("📝 创建新文档", ButtonHeight = 35), GUIColor(0.4f, 0.8f, 0.4f)]
            public void CreateDocument()
            {
                EditorApplication.ExecuteMenuItem("Tools/ES工具/ES文档创建窗口");
            }

            [FoldoutGroup("快捷操作")]
            [Button("🔄 刷新文档库", ButtonHeight = 35), GUIColor(0.4f, 0.6f, 0.9f)]
            public void RefreshLibrary()
            {
                AssetDatabase.Refresh();
                ESDocReaderWindow.UsingWindow?.ForceMenuTreeRebuild();
            }
        }

        [Serializable]
        public class Page_NoDocuments : ESWindowPageBase
        {
            [Title("📭 暂无文档", titleAlignment: TitleAlignments.Centered)]

            [InfoBox("Assets/ES/Documentation 目录下尚未创建文档。", InfoMessageType.Warning)]

            [DisplayAsString(fontSize: 13), HideLabel]
            public string hint = "💡 建议:\n\n1. 使用「ES文档创建窗口」创建新文档\n2. 或将现有文档资产移动到此目录";

            [Button("打开文档创建窗口", ButtonHeight = 40), GUIColor(0.3f, 0.9f, 0.3f)]
            public void OpenCreator()
            {
                EditorApplication.ExecuteMenuItem("Tools/ES工具/ES文档创建窗口");
            }
        }

        [Serializable]
        public class Page_Statistics : ESWindowPageBase
        {
            [Title("📊 文档库统计信息", "完整的文档统计与列表", bold: true, titleAlignment: TitleAlignments.Centered)]

            [FoldoutGroup("📈 概览")]
            [HorizontalGroup("📈 概览/Stats")]
            [BoxGroup("📈 概览/Stats/Left"), LabelText("📚 文档总数"), DisplayAsString(false)]
            public int documentCount;

            [BoxGroup("📈 概览/Stats/Right"), LabelText("📂 分类数量"), DisplayAsString(false)]
            public int categoryCount;

            [BoxGroup("📈 概览/Info"), LabelText("🔄 当前排序"), DisplayAsString(false)]
            public string currentSortMode;

            [Title("📋 完整文档列表")]
            [TableList(ShowIndexLabels = true, IsReadOnly = true, AlwaysExpanded = true)]
            public List<DocStatItem> allDocuments = new List<DocStatItem>();

            [Button("🔄 刷新统计", ButtonHeight = 35), GUIColor(0.4f, 0.7f, 0.9f)]
            public void Refresh()
            {
                ESDocReaderWindow.UsingWindow?.ForceMenuTreeRebuild();
            }

            [Serializable]
            public class DocStatItem
            {
                [TableColumnWidth(200, Resizable = true)]
                [LabelText("📄 文档标题")]
                public string title;

                [TableColumnWidth(100)]
                [LabelText("📂 分类")]
                public string category;

                [TableColumnWidth(100)]
                [LabelText("✍️ 作者")]
                public string author;

                [TableColumnWidth(90)]
                [LabelText("📅 创建日期")]
                public string createDate;

                [TableColumnWidth(90)]
                [LabelText("🔄 最后修改")]
                public string lastModified;

                [TableColumnWidth(60)]
                [LabelText("📑 章节数")]
                public int sectionCount;
            }
        }
    }
}

// 恢复警告
#pragma warning restore CS0414
#pragma warning restore CS0162
