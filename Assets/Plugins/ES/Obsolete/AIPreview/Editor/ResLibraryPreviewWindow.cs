#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using ES;

/// <summary>
/// ResLibrary 预览与简单管理面板原型：
/// - 浏览工程中的所有 ResLibrary 资产；
/// - 按 Book / Page 层级展示结构；
/// - 仅做只读预览与简单定位，避免修改现有运行时代码。
/// </summary>
public class ResLibraryPreviewWindow : EditorWindow
{
    private Vector2 _scroll;
    private ResLibrary[] _libraries;

    [MenuItem("ES/Preview/ResLibrary 预览窗口")]
    public static void Open()
    {
        var win = GetWindow<ResLibraryPreviewWindow>(false, "ResLibrary Preview", true);
        win.minSize = new Vector2(600, 300);
        win.RefreshLibraries();
        win.Show();
    }

    private void OnFocus()
    {
        // 焦点回到窗口时刷新一次，保证看到最新的资产结构
        RefreshLibraries();
    }

    private void RefreshLibraries()
    {
        var guids = AssetDatabase.FindAssets("t:ResLibrary");
        _libraries = new ResLibrary[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            _libraries[i] = AssetDatabase.LoadAssetAtPath<ResLibrary>(path);
        }
    }

    private void OnGUI()
    {
        if (_libraries == null)
        {
            RefreshLibraries();
        }

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshLibraries();
            }

            GUILayout.FlexibleSpace();
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        if (_libraries == null || _libraries.Length == 0)
        {
            EditorGUILayout.HelpBox("当前工程中未找到 ResLibrary 资产。", MessageType.Info);
        }
        else
        {
            foreach (var lib in _libraries)
            {
                if (lib == null) continue;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(lib.name, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Ping", GUILayout.Width(60)))
                {
                    EditorGUIUtility.PingObject(lib);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("描述", lib.Desc);
                EditorGUILayout.LabelField("参与构建", lib.ContainsBuild ? "是" : "否");
                EditorGUILayout.LabelField("是否远程库", lib.IsNet ? "远程" : "本体");

                // 展示 Book / Page 层级（包含自定义Books和DefaultBooks）
                var useableBooks = lib.GetAllUseableBooks();
                if (useableBooks != null)
                {
                    EditorGUI.indentLevel++;
                    int bookIndex = 0;
                    foreach (var book in useableBooks)
                    {
                        if (book == null) continue;

                        EditorGUILayout.LabelField($"Book[{bookIndex}] - {book.Name}");
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("描述", book.Desc);

                        if (book.pages != null)
                        {
                            for (int p = 0; p < book.pages.Count; p++)
                            {
                                var page = book.pages[p];
                                if (page == null) continue;

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField($"Page[{p}] - {page.Name}");
                                if (page.OB != null)
                                {
                                    if (GUILayout.Button("Ping 资源", GUILayout.Width(80)))
                                    {
                                        EditorGUIUtility.PingObject(page.OB);
                                    }
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                        }

                        EditorGUI.indentLevel--;
                        bookIndex++;
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
                GUILayout.Space(4);
            }
        }

        EditorGUILayout.EndScrollView();
    }
}
#endif
