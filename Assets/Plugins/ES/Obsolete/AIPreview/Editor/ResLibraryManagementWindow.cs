using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace ES.Preview.Editor
{
    /// <summary>
    /// ResLibrary 管理面板
    /// 
    /// **核心功能**：
    /// - 可视化浏览 Library/Book/Page 层级
    /// - 资源搜索和过滤
    /// - 批量操作（删除、移动、重命名）
    /// - 资源依赖分析
    /// - 缺失资源检测
    /// </summary>
    public class ResLibraryManagementWindow : EditorWindow
    {
        [MenuItem("ES/Res Library Management")]
        public static void ShowWindow()
        {
            var window = GetWindow<ResLibraryManagementWindow>("Res Library Manager");
            window.minSize = new Vector2(800, 600);
        }
        
        #region Data Members
        
        private Vector2 libraryScrollPos;
        private Vector2 detailScrollPos;
        
        // 所有Library
        private List<ResLibrary> allLibraries = new();
        
        // 当前选中
        private ResLibrary selectedLibrary;
        private ResBook selectedBook;
        private ResPage selectedPage;
        
        // 搜索
        private string searchQuery = "";
        private ResType filterType = ResType.All;
        
        // 显示选项
        private bool showMissingAssets = false;
        private bool showDependencies = false;
        
        #endregion
        
        #region GUI
        
        void OnEnable()
        {
            LoadAllLibraries();
        }
        
        void OnGUI()
        {
            DrawToolbar();
            
            EditorGUILayout.BeginHorizontal();
            {
                DrawLibraryTree();
                DrawDetailPanel();
            }
            EditorGUILayout.EndHorizontal();
            
            DrawStatusBar();
        }
        
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                // 刷新按钮
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
                {
                    LoadAllLibraries();
                }
                
                GUILayout.Space(10);
                
                // 搜索框
                GUILayout.Label("Search:", GUILayout.Width(50));
                searchQuery = GUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField, GUILayout.Width(200));
                
                GUILayout.Space(10);
                
                // 类型过滤
                GUILayout.Label("Type:", GUILayout.Width(40));
                filterType = (ResType)EditorGUILayout.EnumPopup(filterType, EditorStyles.toolbarPopup, GUILayout.Width(100));
                
                GUILayout.FlexibleSpace();
                
                // 选项
                showMissingAssets = GUILayout.Toggle(showMissingAssets, "Show Missing", EditorStyles.toolbarButton);
                showDependencies = GUILayout.Toggle(showDependencies, "Show Dependencies", EditorStyles.toolbarButton);
                
                GUILayout.Space(10);
                
                // 批量操作
                if (GUILayout.Button("Batch Operations", EditorStyles.toolbarButton, GUILayout.Width(120)))
                {
                    ShowBatchOperationsMenu();
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawLibraryTree()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            {
                GUILayout.Label("Resource Library Tree", EditorStyles.boldLabel);
                
                libraryScrollPos = EditorGUILayout.BeginScrollView(libraryScrollPos);
                {
                    foreach (var library in allLibraries)
                    {
                        DrawLibraryNode(library);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }
        
        private void DrawLibraryNode(ResLibrary library)
        {
            EditorGUILayout.BeginHorizontal();
            {
                // 折叠按钮
                library.isExpanded = EditorGUILayout.Foldout(library.isExpanded, "", true);
                
                // Library图标和名称
                GUILayout.Label(EditorGUIUtility.IconContent("Folder Icon"), GUILayout.Width(20));
                
                if (GUILayout.Button(library.name, EditorStyles.label))
                {
                    selectedLibrary = library;
                    selectedBook = null;
                    selectedPage = null;
                }
                
                // 统计信息
                GUILayout.FlexibleSpace();
                GUILayout.Label($"({library.books?.Count ?? 0} books)", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
            
            if (library.isExpanded && library.books != null)
            {
                EditorGUI.indentLevel++;
                foreach (var book in library.books)
                {
                    DrawBookNode(book);
                }
                EditorGUI.indentLevel--;
            }
        }
        
        private void DrawBookNode(ResBook book)
        {
            EditorGUILayout.BeginHorizontal();
            {
                book.isExpanded = EditorGUILayout.Foldout(book.isExpanded, "", true);
                
                GUILayout.Label(EditorGUIUtility.IconContent("ScriptableObject Icon"), GUILayout.Width(20));
                
                if (GUILayout.Button(book.name, EditorStyles.label))
                {
                    selectedBook = book;
                    selectedPage = null;
                }
                
                GUILayout.FlexibleSpace();
                GUILayout.Label($"({book.pages?.Length ?? 0} pages)", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
            
            if (book.isExpanded && book.pages != null)
            {
                EditorGUI.indentLevel++;
                foreach (var page in book.pages)
                {
                    DrawPageNode(page);
                }
                EditorGUI.indentLevel--;
            }
        }
        
        private void DrawPageNode(ResPage page)
        {
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.Space(20);
                
                // 类型图标
                GUILayout.Label(GetIconForResType(page.ResType), GUILayout.Width(20));
                
                if (GUILayout.Button(page.name, EditorStyles.label))
                {
                    selectedPage = page;
                }
                
                // 缺失检测
                if (page.Asset == null)
                {
                    GUILayout.Label("⚠", EditorStyles.boldLabel);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical();
            {
                GUILayout.Label("Details", EditorStyles.boldLabel);
                
                detailScrollPos = EditorGUILayout.BeginScrollView(detailScrollPos);
                {
                    if (selectedPage != null)
                    {
                        DrawPageDetails(selectedPage);
                    }
                    else if (selectedBook != null)
                    {
                        DrawBookDetails(selectedBook);
                    }
                    else if (selectedLibrary != null)
                    {
                        DrawLibraryDetails(selectedLibrary);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Select a Library, Book, or Page to view details.", MessageType.Info);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }
        
        private void DrawLibraryDetails(ResLibrary library)
        {
            EditorGUILayout.LabelField("Library Information", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Name:", library.name);
            EditorGUILayout.LabelField("Books:", library.books?.Count.ToString() ?? "0");
            
            int totalPages = 0;
            if (library.books != null)
            {
                foreach (var book in library.books)
                {
                    totalPages += book.pages?.Length ?? 0;
                }
            }
            EditorGUILayout.LabelField("Total Pages:", totalPages.ToString());
            
            EditorGUILayout.Space();
            
            // 显示所有Book
            if (library.books != null && library.books.Count > 0)
            {
                EditorGUILayout.LabelField("Books in Library:", EditorStyles.boldLabel);
                foreach (var book in library.books)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"  • {book.name}", GUILayout.Width(200));
                    if (GUILayout.Button("Select", GUILayout.Width(60)))
                    {
                        selectedBook = book;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        
        private void DrawBookDetails(ResBook book)
        {
            EditorGUILayout.LabelField("Book Information", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Name:", book.name);
            EditorGUILayout.LabelField("Pages:", book.pages?.Length.ToString() ?? "0");
            
            EditorGUILayout.Space();
            
            // 显示所有Page
            if (book.pages != null && book.pages.Length > 0)
            {
                EditorGUILayout.LabelField("Pages in Book:", EditorStyles.boldLabel);
                foreach (var page in book.pages)
                {
                    DrawPageInList(page);
                }
            }
        }
        
        private void DrawPageDetails(ResPage page)
        {
            EditorGUILayout.LabelField("Page Information", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Name:", page.name);
            EditorGUILayout.LabelField("Type:", page.ResType.ToString());
            EditorGUILayout.LabelField("Key:", page.Key);
            
            EditorGUILayout.Space();
            
            // 资源引用
            EditorGUILayout.LabelField("Asset Reference:", EditorStyles.boldLabel);
            EditorGUILayout.ObjectField("Asset:", page.Asset, typeof(UnityEngine.Object), false);
            
            if (page.Asset == null)
            {
                EditorGUILayout.HelpBox("⚠ Warning: Asset is missing!", MessageType.Warning);
                
                if (GUILayout.Button("Try to Recover"))
                {
                    // TODO: 尝试通过路径恢复
                }
            }
            
            EditorGUILayout.Space();
            
            // 依赖分析
            if (showDependencies && page.Asset != null)
            {
                DrawDependencies(page.Asset);
            }
        }
        
        private void DrawPageInList(ResPage page)
        {
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.Label(GetIconForResType(page.ResType), GUILayout.Width(20));
                EditorGUILayout.LabelField(page.name, GUILayout.Width(150));
                EditorGUILayout.ObjectField(page.Asset, typeof(UnityEngine.Object), false, GUILayout.Width(200));
                
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    selectedPage = page;
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawDependencies(UnityEngine.Object asset)
        {
            EditorGUILayout.LabelField("Dependencies:", EditorStyles.boldLabel);
            
            string assetPath = AssetDatabase.GetAssetPath(asset);
            string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
            
            if (dependencies.Length > 0)
            {
                foreach (var dep in dependencies)
                {
                    var depAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dep);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("  • ", GUILayout.Width(20));
                    EditorGUILayout.ObjectField(depAsset, typeof(UnityEngine.Object), false);
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.LabelField("  No dependencies");
            }
        }
        
        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            {
                GUILayout.Label($"Libraries: {allLibraries.Count}");
                GUILayout.FlexibleSpace();
                
                int totalBooks = allLibraries.Sum(l => l.books?.Count ?? 0);
                GUILayout.Label($"Books: {totalBooks}");
                GUILayout.FlexibleSpace();
                
                int totalPages = allLibraries.Sum(l => l.books?.Sum(b => b.pages?.Length ?? 0) ?? 0);
                GUILayout.Label($"Pages: {totalPages}");
            }
            EditorGUILayout.EndHorizontal();
        }
        
        #endregion
        
        #region Data Loading
        
        private void LoadAllLibraries()
        {
            allLibraries.Clear();
            
            // 查找所有ResLibrary资产
            string[] guids = AssetDatabase.FindAssets("t:ResLibrary");
            
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var library = AssetDatabase.LoadAssetAtPath<ResLibrary>(path);
                if (library != null)
                {
                    allLibraries.Add(library);
                }
            }
            
            Debug.Log($"Loaded {allLibraries.Count} libraries");
        }
        
        #endregion
        
        #region Utilities
        
        private GUIContent GetIconForResType(ResType type)
        {
            return type switch
            {
                ResType.Prefab => EditorGUIUtility.IconContent("Prefab Icon"),
                ResType.Texture => EditorGUIUtility.IconContent("Texture Icon"),
                ResType.Material => EditorGUIUtility.IconContent("Material Icon"),
                ResType.Audio => EditorGUIUtility.IconContent("AudioClip Icon"),
                ResType.Animation => EditorGUIUtility.IconContent("Animation Icon"),
                _ => EditorGUIUtility.IconContent("DefaultAsset Icon")
            };
        }
        
        private void ShowBatchOperationsMenu()
        {
            GenericMenu menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("Delete Missing Assets"), false, BatchDeleteMissing);
            menu.AddItem(new GUIContent("Rename All Pages"), false, BatchRenamepages);
            menu.AddItem(new GUIContent("Export to CSV"), false, ExportToCSV);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Validate All Libraries"), false, ValidateAllLibraries);
            
            menu.ShowAsContext();
        }
        
        private void BatchDeleteMissing()
        {
            Debug.Log("Batch delete missing assets");
            // TODO: 实现批量删除缺失资源
        }
        
        private void BatchRenamepages()
        {
            Debug.Log("Batch rename pages");
            // TODO: 实现批量重命名
        }
        
        private void ExportToCSV()
        {
            Debug.Log("Export to CSV");
            // TODO: 导出为CSV文件
        }
        
        private void ValidateAllLibraries()
        {
            int missingCount = 0;
            
            foreach (var library in allLibraries)
            {
                if (library.books == null) continue;
                
                foreach (var book in library.books)
                {
                    if (book.pages == null) continue;
                    
                    foreach (var page in book.pages)
                    {
                        if (page.Asset == null)
                        {
                            Debug.LogWarning($"Missing asset: {library.name}/{book.name}/{page.name}");
                            missingCount++;
                        }
                    }
                }
            }
            
            if (missingCount > 0)
            {
                EditorUtility.DisplayDialog("Validation Result", 
                    $"Found {missingCount} missing assets.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Validation Result", 
                    "All assets are valid!", "OK");
            }
        }
        
        #endregion
    }
    
    #region Dummy Classes (假设的ES类型，实际应引用真实类型)
    
    public class ResLibrary : ScriptableObject
    {
        public List<ResBook> books;
        public bool isExpanded;  // Editor only
    }
    
    public class ResBook : ScriptableObject
    {
        public ResPage[] pages;
        public bool isExpanded;  // Editor only
    }
    
    public class ResPage : ScriptableObject
    {
        public string Key;
        public ResType ResType;
        public UnityEngine.Object Asset;
    }
    
    public enum ResType
    {
        All,
        Prefab,
        Texture,
        Material,
        Audio,
        Animation
    }
    
    #endregion
}
