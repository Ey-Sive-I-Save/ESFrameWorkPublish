using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;

namespace ES
{
    /// <summary>
    /// 文档页面基类 - 支持丰富的文档元素
    /// </summary>
    [CreateAssetMenu(fileName = "新文档", menuName = MenuItemPathDefine.ASSET_DOCUMENTATION_PATH + "文档页面")]
    public class ESDocumentPageBase : ScriptableObject
    {
        [Title("文档信息")]
        [LabelText("文档标题")]
        public string documentTitle = "新文档";

        [LabelText("分类")]
        public string category = "通用";

        [LabelText("作者")]
        public string author = "";

        [LabelText("创建日期"), ReadOnly]
        public string createDate = DateTime.Now.ToString("yyyy-MM-dd");

        [LabelText("最后修改日期")]
        public string lastModified = DateTime.Now.ToString("yyyy-MM-dd");

        [Title("文档内容")]
        [LabelText("章节"), ListDrawerSettings(DraggableItems = true, ShowIndexLabels = true)]
        public List<ESDocSection> sections = new List<ESDocSection>();

        [Title("文档操作")]
        [Button("导出为Markdown", ButtonHeight = 40), GUIColor(0.3f, 0.8f, 0.3f)]
        public void ExportToMarkdown()
        {
            string markdown = GenerateMarkdown();
            string path = UnityEditor.EditorUtility.SaveFilePanel("保存Markdown文件", "", documentTitle, "md");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, markdown);
                UnityEditor.EditorUtility.DisplayDialog("成功", "Markdown文件已导出！", "确定");
            }
        }

        #if UNITY_EDITOR
            [CustomEditor(typeof(ESDocumentPageBase))]
            public class ESDocumentPageBaseEditor : Sirenix.OdinInspector.Editor.OdinEditor
            {
                private ESAreaSolver area = new ESAreaSolver();
                private static bool showPreview = false;

                public override void OnInspectorGUI()
                {
                    var doc = this.target as ESDocumentPageBase;

                    area.UpdateAtFisrt();

                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    showPreview = GUILayout.Toggle(showPreview, "预览", "Button", GUILayout.Width(80));
                    GUILayout.EndHorizontal();

                    if (showPreview && doc != null)
                    {
                        SirenixEditorGUI.BeginBox();
                        var titleStyle = new GUIStyle(EditorStyles.largeLabel) { alignment = TextAnchor.MiddleCenter };
                        GUILayout.Label(doc.documentTitle, titleStyle);

                        GUILayout.BeginVertical();
                        GUILayout.Label($"分类: {doc.category}    作者: {doc.author}    更新: {doc.lastModified}", EditorStyles.miniLabel);
                        GUILayout.Space(6);

                        foreach (var section in doc.sections)
                        {
                            GUILayout.Label(section.sectionTitle, EditorStyles.boldLabel);
                            GUILayout.BeginVertical(GUI.skin.box);
                            foreach (var item in section.content)
                            {
                                DrawContentPreview(item);
                                GUILayout.Space(4);
                            }
                            GUILayout.EndVertical();
                            GUILayout.Space(8);
                        }

                        GUILayout.EndVertical();
                        SirenixEditorGUI.EndBox();
                    }

                    area.UpdateAtLast();

                    // 然后绘制默认的 Odin Inspector 编辑器
                    base.OnInspectorGUI();
                }

                private void DrawContentPreview(ESDocContentBase item)
                {
                    // 简化版复用之前的绘制逻辑
                    switch (item)
                    {
                        case ESDocText t:
                            EditorGUILayout.LabelField(t.content, EditorStyles.wordWrappedLabel);
                            break;
                        case ESDocCodeBlock c:
                            EditorGUILayout.LabelField($"[代码: {c.language}]");
                            EditorGUILayout.TextArea(c.code, GUILayout.Height(Mathf.Min(200, 10 + c.code.Split('\n').Length * 18)));
                            break;
                        case ESDocImage img:
                            if (img.image != null)
                            {
                                var tex = AssetPreview.GetAssetPreview(img.image) ?? (Texture2D)img.image;
                                if (tex != null)
                                {
                                    GUILayout.Label(tex, GUILayout.Width(200), GUILayout.Height(120));
                                }
                            }
                            else if (!string.IsNullOrEmpty(img.imagePath))
                            {
                                EditorGUILayout.LabelField("图片路径: " + img.imagePath);
                            }
                            if (!string.IsNullOrEmpty(img.caption))
                                EditorGUILayout.LabelField(img.caption);
                            break;
                        case ESDocTable table:
                            if (!string.IsNullOrEmpty(table.tableTitle))
                                EditorGUILayout.LabelField(table.tableTitle);
                            if (table.headers != null && table.headers.Count > 0)
                            {
                                EditorGUILayout.BeginHorizontal();
                                foreach (var h in table.headers)
                                {
                                    GUILayout.Label(h, GUILayout.Width(120));
                                }
                                EditorGUILayout.EndHorizontal();
                                foreach (var row in table.rows)
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    foreach (var cell in row.cells)
                                    {
                                        GUILayout.Label(cell, GUILayout.Width(120));
                                    }
                                    EditorGUILayout.EndHorizontal();
                                }
                            }
                            break;
                        case ESDocLink link:
                            if (GUILayout.Button(link.displayText, EditorStyles.label))
                            {
                                Application.OpenURL(link.url);
                            }
                            if (!string.IsNullOrEmpty(link.description))
                                EditorGUILayout.LabelField(link.description);
                            break;
                        default:
                            EditorGUILayout.LabelField(item.ToPlainText());
                            break;
                    }
                }
            }
        #endif

        [Button("导出为HTML", ButtonHeight = 40), GUIColor(0.3f, 0.6f, 0.9f)]
        public void ExportToHTML()
        {
            string html = GenerateHTML();
            string path = UnityEditor.EditorUtility.SaveFilePanel("保存HTML文件", "", documentTitle, "html");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, html);
                UnityEditor.EditorUtility.DisplayDialog("成功", "HTML文件已导出！", "确定");
            }
        }

        [Button("复制为纯文本", ButtonHeight = 40), GUIColor(0.8f, 0.8f, 0.3f)]
        public void CopyAsPlainText()
        {
            string text = GeneratePlainText();
            UnityEditor.EditorGUIUtility.systemCopyBuffer = text;
            UnityEditor.EditorUtility.DisplayDialog("成功", "内容已复制到剪贴板！", "确定");
        }

        private string GenerateMarkdown()
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine($"# {documentTitle}");
            sb.AppendLine();
            sb.AppendLine($"**分类**: {category}");
            if (!string.IsNullOrEmpty(author))
                sb.AppendLine($"**作者**: {author}");
            sb.AppendLine($"**创建日期**: {createDate}");
            sb.AppendLine($"**最后修改**: {lastModified}");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            foreach (var section in sections)
            {
                sb.AppendLine(section.ToMarkdown());
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GenerateHTML()
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine($"    <title>{documentTitle}</title>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: Arial, sans-serif; max-width: 900px; margin: 0 auto; padding: 20px; }");
            sb.AppendLine("        h1 { color: #333; }");
            sb.AppendLine("        h2 { color: #666; border-bottom: 2px solid #ddd; padding-bottom: 5px; }");
            sb.AppendLine("        code { background: #f4f4f4; padding: 2px 5px; border-radius: 3px; }");
            sb.AppendLine("        pre { background: #f4f4f4; padding: 10px; border-radius: 5px; overflow-x: auto; }");
            sb.AppendLine("        table { border-collapse: collapse; width: 100%; margin: 10px 0; }");
            sb.AppendLine("        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
            sb.AppendLine("        th { background: #f4f4f4; }");
            sb.AppendLine("        blockquote { border-left: 4px solid #ddd; padding-left: 10px; color: #666; }");
            sb.AppendLine("        img { max-width: 100%; height: auto; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            
            sb.AppendLine($"    <h1>{documentTitle}</h1>");
            sb.AppendLine($"    <p><strong>分类</strong>: {category}</p>");
            if (!string.IsNullOrEmpty(author))
                sb.AppendLine($"    <p><strong>作者</strong>: {author}</p>");
            sb.AppendLine($"    <p><strong>创建日期</strong>: {createDate}</p>");
            sb.AppendLine($"    <p><strong>最后修改</strong>: {lastModified}</p>");
            sb.AppendLine("    <hr>");

            foreach (var section in sections)
            {
                sb.AppendLine(section.ToHTML());
            }

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private string GeneratePlainText()
        {
            var sb = new System.Text.StringBuilder();
            
            sb.AppendLine(documentTitle);
            sb.AppendLine(new string('=', documentTitle.Length));
            sb.AppendLine();
            sb.AppendLine($"分类: {category}");
            if (!string.IsNullOrEmpty(author))
                sb.AppendLine($"作者: {author}");
            sb.AppendLine($"创建日期: {createDate}");
            sb.AppendLine($"最后修改: {lastModified}");
            sb.AppendLine();

            foreach (var section in sections)
            {
                sb.AppendLine(section.ToPlainText());
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// 文档章节
    /// </summary>
    [Serializable, TypeRegistryItem("章节")]
    public class ESDocSection
    {
        [LabelText("章节标题")]
        public string sectionTitle = "章节";

        [LabelText("内容"), ListDrawerSettings(DraggableItems = true),SerializeReference]
        public List<ESDocContentBase> content = new List<ESDocContentBase>();

        public string ToMarkdown()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"## {sectionTitle}");
            sb.AppendLine();
            
            foreach (var item in content)
            {
                sb.AppendLine(item.ToMarkdown());
                sb.AppendLine();
            }
            
            return sb.ToString();
        }

        public string ToHTML()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"    <h2>{sectionTitle}</h2>");
            
            foreach (var item in content)
            {
                sb.AppendLine(item.ToHTML());
            }
            
            return sb.ToString();
        }

        public string ToPlainText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(sectionTitle);
            sb.AppendLine(new string('-', sectionTitle.Length));
            
            foreach (var item in content)
            {
                sb.AppendLine(item.ToPlainText());
            }
            
            return sb.ToString();
        }
    }

    /// <summary>
    /// 文档内容基类
    /// </summary>
    [Serializable]
    public abstract class ESDocContentBase
    {
        public abstract string ToMarkdown();
        public abstract string ToHTML();
        public abstract string ToPlainText();
    }

    /// <summary>
    /// 普通文本
    /// </summary>
    [Serializable, TypeRegistryItem("文本")]
    public class ESDocText : ESDocContentBase
    {
        [LabelText("文本内容"), TextArea(3, 10)]
        public string content = "";

        public override string ToMarkdown() => content;
        public override string ToHTML() => $"    <p>{content}</p>";
        public override string ToPlainText() => content;
    }

    /// <summary>
    /// 代码块
    /// </summary>
    [Serializable, TypeRegistryItem("代码块")]
    public class ESDocCodeBlock : ESDocContentBase
    {
        [LabelText("编程语言")]
        [ValueDropdown("GetLanguageOptions")]
        public string language = "csharp";

        [LabelText("代码"), TextArea(5, 20)]
        public string code = "";

        private static IEnumerable<string> GetLanguageOptions()
        {
            return new List<string> { "csharp", "javascript", "python", "java", "cpp", "xml", "json", "sql", "html", "css" };
        }

        public override string ToMarkdown() => $"```{language}\n{code}\n```";
        public override string ToHTML() => $"    <pre><code class=\"language-{language}\">{EscapeHTML(code)}</code></pre>";

        private string EscapeHTML(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("&", "&amp;")
                      .Replace("<", "&lt;")
                      .Replace(">", "&gt;")
                      .Replace("\"", "&quot;")
                      .Replace("'", "&#39;");
        }
        public override string ToPlainText() => $"[代码 - {language}]\n{code}\n[/代码]";
    }

    /// <summary>
    /// 表格
    /// </summary>
    [Serializable, TypeRegistryItem("表格")]
    public class ESDocTable : ESDocContentBase
    {
        [LabelText("表格标题")]
        public string tableTitle = "";

        [LabelText("列标题"), ListDrawerSettings(DraggableItems = true)]
        public List<string> headers = new List<string> { "列1", "列2", "列3" };

        [LabelText("行数据"), TableList(ShowIndexLabels = true)]
        public List<ESDocTableRow> rows = new List<ESDocTableRow>();

        public override string ToMarkdown()
        {
            var sb = new System.Text.StringBuilder();
            
            if (!string.IsNullOrEmpty(tableTitle))
                sb.AppendLine($"**{tableTitle}**\n");
            
            // 表头
            sb.AppendLine("| " + string.Join(" | ", headers) + " |");
            sb.AppendLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");
            
            // 表格内容
            foreach (var row in rows)
            {
                if (row.cells.Count > 0)
                {
                    sb.AppendLine("| " + string.Join(" | ", row.cells) + " |");
                }
            }
            
            return sb.ToString();
        }

        public override string ToHTML()
        {
            var sb = new System.Text.StringBuilder();
            
            if (!string.IsNullOrEmpty(tableTitle))
                sb.AppendLine($"    <h3>{tableTitle}</h3>");
            
            sb.AppendLine("    <table>");
            sb.AppendLine("        <thead>");
            sb.AppendLine("            <tr>");
            foreach (var header in headers)
            {
                sb.AppendLine($"                <th>{header}</th>");
            }
            sb.AppendLine("            </tr>");
            sb.AppendLine("        </thead>");
            sb.AppendLine("        <tbody>");
            
            foreach (var row in rows)
            {
                sb.AppendLine("            <tr>");
                foreach (var cell in row.cells)
                {
                    sb.AppendLine($"                <td>{cell}</td>");
                }
                sb.AppendLine("            </tr>");
            }
            
            sb.AppendLine("        </tbody>");
            sb.AppendLine("    </table>");
            
            return sb.ToString();
        }

        public override string ToPlainText()
        {
            var sb = new System.Text.StringBuilder();
            
            if (!string.IsNullOrEmpty(tableTitle))
                sb.AppendLine(tableTitle);
            
            sb.AppendLine(string.Join(" | ", headers));
            sb.AppendLine(new string('-', headers.Count * 15));
            
            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(" | ", row.cells));
            }
            
            return sb.ToString();
        }
    }

    [Serializable]
    public class ESDocTableRow
    {
        [HideLabel]
        public List<string> cells = new List<string>();
    }

    /// <summary>
    /// 图片
    /// </summary>
    [Serializable, TypeRegistryItem("图片")]
    public class ESDocImage : ESDocContentBase
    {
        [LabelText("图片"), PreviewField(100)]
        public Texture2D image;

        [LabelText("图片说明")]
        public string caption = "";

        [LabelText("图片路径(可选)"), Sirenix.OdinInspector.FilePath]
        public string imagePath = "";

        public override string ToMarkdown()
        {
            string path = string.IsNullOrEmpty(imagePath) && image != null 
                ? UnityEditor.AssetDatabase.GetAssetPath(image) 
                : imagePath;
            
            return !string.IsNullOrEmpty(caption) 
                ? $"![{caption}]({path})" 
                : $"![]({path})";
        }

        public override string ToHTML()
        {
            string path = string.IsNullOrEmpty(imagePath) && image != null 
                ? UnityEditor.AssetDatabase.GetAssetPath(image) 
                : imagePath;
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("    <figure>");
            sb.AppendLine($"        <img src=\"{path}\" alt=\"{caption}\">");
            if (!string.IsNullOrEmpty(caption))
                sb.AppendLine($"        <figcaption>{caption}</figcaption>");
            sb.AppendLine("    </figure>");
            
            return sb.ToString();
        }

        public override string ToPlainText()
        {
            return !string.IsNullOrEmpty(caption) 
                ? $"[图片: {caption}]" 
                : "[图片]";
        }
    }

    /// <summary>
    /// 超链接
    /// </summary>
    [Serializable, TypeRegistryItem("链接")]
    public class ESDocLink : ESDocContentBase
    {
        [LabelText("显示文本")]
        public string displayText = "链接";

        [LabelText("URL地址")]
        public string url = "https://";

        [LabelText("描述(可选)")]
        public string description = "";

        public override string ToMarkdown()
        {
            string link = $"[{displayText}]({url})";
            return !string.IsNullOrEmpty(description) 
                ? $"{link}\n\n{description}" 
                : link;
        }

        public override string ToHTML()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"    <p><a href=\"{url}\" target=\"_blank\">{displayText}</a></p>");
            if (!string.IsNullOrEmpty(description))
                sb.AppendLine($"    <p>{description}</p>");
            return sb.ToString();
        }

        public override string ToPlainText()
        {
            return !string.IsNullOrEmpty(description) 
                ? $"{displayText} ({url})\n{description}" 
                : $"{displayText} ({url})";
        }
    }

    /// <summary>
    /// 无序列表
    /// </summary>
    [Serializable, TypeRegistryItem("无序列表")]
    public class ESDocUnorderedList : ESDocContentBase
    {
        [LabelText("列表项"), ListDrawerSettings(DraggableItems = true)]
        public List<string> items = new List<string>();

        public override string ToMarkdown()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var item in items)
            {
                sb.AppendLine($"- {item}");
            }
            return sb.ToString();
        }

        public override string ToHTML()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("    <ul>");
            foreach (var item in items)
            {
                sb.AppendLine($"        <li>{item}</li>");
            }
            sb.AppendLine("    </ul>");
            return sb.ToString();
        }

        public override string ToPlainText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var item in items)
            {
                sb.AppendLine($"• {item}");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 有序列表
    /// </summary>
    [Serializable, TypeRegistryItem("有序列表")]
    public class ESDocOrderedList : ESDocContentBase
    {
        [LabelText("列表项"), ListDrawerSettings(DraggableItems = true)]
        public List<string> items = new List<string>();

        public override string ToMarkdown()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {items[i]}");
            }
            return sb.ToString();
        }

        public override string ToHTML()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("    <ol>");
            foreach (var item in items)
            {
                sb.AppendLine($"        <li>{item}</li>");
            }
            sb.AppendLine("    </ol>");
            return sb.ToString();
        }

        public override string ToPlainText()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {items[i]}");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 引用块
    /// </summary>
    [Serializable, TypeRegistryItem("引用")]
    public class ESDocQuote : ESDocContentBase
    {
        [LabelText("引用内容"), TextArea(3, 10)]
        public string quoteText = "";

        [LabelText("引用来源(可选)")]
        public string source = "";

        public override string ToMarkdown()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var line in quoteText.Split('\n'))
            {
                sb.AppendLine($"> {line}");
            }
            if (!string.IsNullOrEmpty(source))
                sb.AppendLine($"> \n> — {source}");
            return sb.ToString();
        }

        public override string ToHTML()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("    <blockquote>");
            sb.AppendLine($"        <p>{quoteText}</p>");
            if (!string.IsNullOrEmpty(source))
                sb.AppendLine($"        <footer>— {source}</footer>");
            sb.AppendLine("    </blockquote>");
            return sb.ToString();
        }

        public override string ToPlainText()
        {
            return !string.IsNullOrEmpty(source) 
                ? $"\"{quoteText}\"\n— {source}" 
                : $"\"{quoteText}\"";
        }
    }

    /// <summary>
    /// 警告框
    /// </summary>
    [Serializable, TypeRegistryItem("警告块")]
    public class ESDocAlert : ESDocContentBase
    {
        public enum AlertType
        {
            Info,
            Success,
            Warning,
            Error
        }

        [LabelText("类型")]
        public AlertType alertType = AlertType.Info;

        [LabelText("标题")]
        public string title = "";

        [LabelText("内容"), TextArea(3, 10)]
        public string content = "";

        public override string ToMarkdown()
        {
            string icon = alertType switch
            {
                AlertType.Info => "ℹ️",
                AlertType.Success => "✅",
                AlertType.Warning => "⚠️",
                AlertType.Error => "❌",
                _ => "ℹ️"
            };

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"> {icon} **{title}**");
            sb.AppendLine($"> ");
            foreach (var line in content.Split('\n'))
            {
                sb.AppendLine($"> {line}");
            }
            return sb.ToString();
        }

        public override string ToHTML()
        {
            string cssClass = alertType.ToString().ToLower();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"    <div class=\"alert alert-{cssClass}\">");
            sb.AppendLine($"        <strong>{title}</strong>");
            sb.AppendLine($"        <p>{content}</p>");
            sb.AppendLine("    </div>");
            return sb.ToString();
        }

        public override string ToPlainText()
        {
            return $"[{alertType}] {title}\n{content}";
        }
    }

    /// <summary>
    /// 分隔线
    /// </summary>
    [Serializable, TypeRegistryItem("分隔线")]
    public class ESDocDivider : ESDocContentBase
    {
        public override string ToMarkdown() => "---";
        public override string ToHTML() => "    <hr>";
        public override string ToPlainText() => new string('-', 50);
    }

    // Odin drawer: 在右侧面板顶部显示文档预览f
    public class ESDocPreviewDrawer : Sirenix.OdinInspector.Editor.OdinValueDrawer<ESDocumentPageBase>
    {
        private ESAreaSolver area = new ESAreaSolver();
        private static bool showPreview = false;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            area.UpdateAtFisrt();
            
            var doc = this.ValueEntry.SmartValue;
            
            // 预览开关
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            showPreview = GUILayout.Toggle(showPreview, "预览", "Button", GUILayout.Width(80));
            GUILayout.EndHorizontal();

            if (showPreview && doc != null)
            {
                // 预览盒子
                SirenixEditorGUI.BeginBox();
                var titleStyle = new GUIStyle(EditorStyles.largeLabel) { alignment = TextAnchor.MiddleCenter };
                GUILayout.Label(doc.documentTitle, titleStyle);

                GUILayout.BeginVertical();
                GUILayout.Label($"分类: {doc.category}    作者: {doc.author}    更新: {doc.lastModified}", EditorStyles.miniLabel);
                GUILayout.Space(6);

                // 遍历章节和内容
                foreach (var section in doc.sections)
                {
                    GUILayout.Label(section.sectionTitle, EditorStyles.boldLabel);
                    GUILayout.BeginVertical(GUI.skin.box);
                    foreach (var item in section.content)
                    {
                        DrawContentPreview(item);
                        GUILayout.Space(4);
                    }
                    GUILayout.EndVertical();
                    GUILayout.Space(8);
                }

                GUILayout.EndVertical();
                Sirenix.Utilities.Editor.SirenixEditorGUI.EndBox();
            }

            // 然后继续默认绘制（让用户可以编辑文档完整字段）
            this.CallNextDrawer(label);
            area.UpdateAtLast();
        }

        private void DrawContentPreview(ESDocContentBase item)
        {
            switch (item)
            {
                case ESDocText t:
                    DrawRichLabel(t.content);
                    break;
                case ESDocCodeBlock c:
                    DrawCodeBlock(c.language, c.code);
                    break;
                case ESDocTable table:
                    DrawTablePreview(table);
                    break;
                case ESDocImage img:
                    DrawImagePreview(img);
                    break;
                case ESDocLink link:
                    if (GUILayout.Button(link.displayText, EditorStyles.linkLabel))
                    {
                        Application.OpenURL(link.url);
                    }
                    if (!string.IsNullOrEmpty(link.description))
                        GUILayout.Label(link.description, EditorStyles.wordWrappedLabel);
                    break;
                case ESDocUnorderedList ul:
                    foreach (var it in ul.items)
                    {
                        GUILayout.Label("• " + it, EditorStyles.label);
                    }
                    break;
                case ESDocOrderedList ol:
                    for (int i = 0; i < ol.items.Count; i++)
                    {
                        GUILayout.Label($"{i + 1}. {ol.items[i]}", EditorStyles.label);
                    }
                    break;
                case ESDocQuote q:
                    EditorGUILayout.HelpBox(q.quoteText + (string.IsNullOrEmpty(q.source) ? "" : "\n— " + q.source), MessageType.None);
                    break;
                case ESDocAlert a:
                    var msgType = a.alertType == ESDocAlert.AlertType.Error ? MessageType.Error :
                                  a.alertType == ESDocAlert.AlertType.Warning ? MessageType.Warning :
                                  a.alertType == ESDocAlert.AlertType.Success ? MessageType.Info : MessageType.Info;
                    EditorGUILayout.HelpBox(a.title + "\n" + a.content, msgType);
                    break;
                case ESDocDivider _:
                    GUILayout.Label("——————————————————————");
                    break;
                default:
                    GUILayout.Label(item.ToPlainText());
                    break;
            }
        }

        private void DrawRichLabel(string text)
        {
            var style = new GUIStyle(EditorStyles.label) { wordWrap = true };
            GUILayout.Label(text, style);
        }

        private void DrawCodeBlock(string language, string code)
        {
            var style = new GUIStyle(EditorStyles.textArea) { wordWrap = false };
            GUILayout.Label($"[代码: {language}]", EditorStyles.miniBoldLabel);
            EditorGUILayout.TextArea(code, style, GUILayout.Height(Mathf.Min(200, 10 + code.Split('\n').Length * 18)));
        }

        private void DrawImagePreview(ESDocImage img)
        {
            if (img.image != null)
            {
                var tex = AssetPreview.GetAssetPreview(img.image) ?? (Texture2D)img.image;
                if (tex != null)
                {
                    GUILayout.Label(tex, GUILayout.Width(200), GUILayout.Height(120));
                }
            }
            else if (!string.IsNullOrEmpty(img.imagePath))
            {
                GUILayout.Label("图片路径: " + img.imagePath, EditorStyles.miniLabel);
            }
            if (!string.IsNullOrEmpty(img.caption))
                GUILayout.Label(img.caption, EditorStyles.wordWrappedLabel);
        }

        private void DrawTablePreview(ESDocTable table)
        {
            if (!string.IsNullOrEmpty(table.tableTitle))
                GUILayout.Label(table.tableTitle, EditorStyles.miniLabel);

            // 简单表格渲染
            if (table.headers != null && table.headers.Count > 0)
            {
                GUILayout.BeginHorizontal();
                foreach (var h in table.headers)
                {
                    GUILayout.Label(h, EditorStyles.boldLabel, GUILayout.Width(120));
                }
                GUILayout.EndHorizontal();

                foreach (var row in table.rows)
                {
                    GUILayout.BeginHorizontal();
                    foreach (var cell in row.cells)
                    {
                        GUILayout.Label(cell, GUILayout.Width(120));
                    }
                    GUILayout.EndHorizontal();
                }
            }
            }
        }
    }