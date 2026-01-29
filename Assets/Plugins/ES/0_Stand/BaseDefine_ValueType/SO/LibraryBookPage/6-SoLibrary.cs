using ES;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    public interface IESLibrary : IString
    {
        public const string DefaultLibFolderName = "LibraryFolder";
        public const string DefaultTargetPackageName = "Main";
    }

    public interface IESPage : IString
    {
        public bool Draw();
    }
    
    /// <summary>
    /// 具有资产引用的Page接口
    /// </summary>
    public interface IAssetPage
    {
        UnityEngine.Object OB { get; }
    }
    
    /// <summary>
    /// Book接口 - 提供页面数量查询（避免反射）
    /// </summary>
    public interface IBook
    {
        /// <summary>获取Book中的页面数量</summary>
        int PageCount { get; }
    }
    public abstract class LibrarySoBase<Book> : ESSO, IESLibrary
    {
        [Title("资产收集配置")]
        [LabelText("收集优先级配置")]
        [HideLabel]
        public LibraryCollectionConfig collectionConfig = new LibraryCollectionConfig();

        [LabelText("Library名字")]
        public string Name = "Library PreNameToABKeys";
        [LabelText("构建辅助专用的文件夹名(英文)")]
        public string LibFolderName = "LibFolderName";
        [InlineButton("HandleAdd", "手动变更")]
        public int ChangeCount = 0;
        [LabelText("主包包含库")]
        public bool IsMainInClude= true;

        // 资产缓存（用于高性能去重检查）
        [NonSerialized]
        public HashSet<string> _assetPathCache;

        void HandleAdd()
        {
            ChangeCount++;
            ESStandUtility.SafeEditor.Wrap_SetDirty(this);
        }
        public virtual void Refresh()
        {

        }
        
        /// <summary>
        /// 拖入资产到Books时自动分配到合适的DefaultBook
        /// </summary>
        public virtual void EditorOnly_DragAssetsToBooks(UnityEngine.Object[] assets)
        {
#if UNITY_EDITOR
            if (assets == null || assets.Length == 0)
                return;

            foreach (var asset in assets)
            {
                if (asset == null) continue;

                // 判断资产类型
                var category = ESGlobalResToolsSupportConfig.DetermineAssetCategory(asset);
                
                // 获取对应的DefaultBook
                var targetBook = GetDefaultBookByCategory(category);
                
                // 如果没有找到匹配的，尝试使用Other类型
                if (targetBook == null)
                {
                    targetBook = GetDefaultBookByCategory(ESAssetCategory.Other);
                }
                
                if (targetBook == null)
                {
                    UnityEngine.Debug.LogWarning($"[Library] 无法找到合适的DefaultBook收集资产 [{asset.name}]");
                    continue;
                }

                // 调用Book的拖拽方法添加资产
                var dragMethod = targetBook.GetType().GetMethod("EditorOnly_DragAtArea");
                if (dragMethod != null)
                {
                    dragMethod.Invoke(targetBook, new object[] { new UnityEngine.Object[] { asset } });
                    UnityEngine.Debug.Log($"[Library] 资产 [{asset.name}] 已自动添加到 DefaultBook [{GetBookName(targetBook)}]");
                }
            }
            
            // 标记为脏数据
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        
        /// <summary>
        /// 获取Book的名字（使用反射兼容性处理）
        /// </summary>
        private string GetBookName(Book book)
        {
            if (book == null) return "Unknown";
            
            var nameField = book.GetType().GetField("Name");
            if (nameField != null)
            {
                return nameField.GetValue(book) as string ?? "Unknown";
            }
            
            return book.ToString();
        }
        
        [LabelText("自定义Books")]
        public List<Book> Books = new List<Book>();
        
        [NonSerialized]
        private bool _defaultBooksInitialized = false;
        
        /// <summary>
        /// 获取默认Books - 首次访问时自动初始化
        /// </summary>
        public IEnumerable<Book> DefaultBooks
        {
            get
            {
                if (!_defaultBooksInitialized)
                {
                    InitializeDefaultBooks();
                    _defaultBooksInitialized = true;
                }
                return GetDefaultBooks();
            }
        }
        
        /// <summary>
        /// 初始化默认Books，设置图标和编辑权限（子类重写）
        /// </summary>
        protected virtual void InitializeDefaultBooks()
        {
            // 子类重写此方法设置默认Books的属性
        }
        
        /// <summary>
        /// 获取默认Books的内部实现，子类重写此方法
        /// </summary>
        protected virtual IEnumerable<Book> GetDefaultBooks() => null;
        
        /// <summary>
        /// 实时检查资产是否存在于Library中（无缓存，适合高频删改场景）
        /// </summary>
        public bool ContainsAsset(UnityEngine.Object asset)
        {
            if (asset == null) return false;
            
#if UNITY_EDITOR
            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath)) return false;
            
            // 实时遍历所有Books（自定义+默认）
            foreach (var book in GetAllUseableBooks())
            {
                if (book == null) continue;
                
                // 使用反射获取pages字段（兼容泛型约束）
                var pagesField = book.GetType().GetField("pages");
                if (pagesField == null) continue;
                
                var pages = pagesField.GetValue(book) as System.Collections.IEnumerable;
                if (pages == null) continue;
                
                foreach (var page in pages)
                {
                    if (page is IAssetPage assetPage && assetPage.OB != null)
                    {
                        var pagePath = UnityEditor.AssetDatabase.GetAssetPath(assetPage.OB);
                        if (pagePath == assetPath)
                        {
                            return true;
                        }
                    }
                }
            }
            
            return false;
#else
            return false;
#endif
        }
        
        // ==================== 已废弃的缓存方法 ====================
        // 注意：以下缓存方法已废弃，因为在高频删改场景下缓存可能导致数据不一致
        // 现在所有检测都使用实时遍历，确保数据准确性
        // _assetPathCache 字段保留是为了向后兼容，但不再使用
        // ==========================================================
        
        /// <summary>
        /// 根据资产类别获取推荐的DefaultBook
        /// </summary>
        public Book GetDefaultBookByCategory(ESAssetCategory category)
        {
            if (DefaultBooks == null)
                return default;
            
            foreach (var book in DefaultBooks)
            {
                // 使用反射获取PreferredAssetCategory字段
                var field = book.GetType().GetField("PreferredAssetCategory");
                if (field != null)
                {
                    var bookCategory = (ESAssetCategory)field.GetValue(book);
                    if (bookCategory == category)
                    {
                        return book;
                    }
                }
            }
            
            return default;
        }
        
        /// <summary>
        /// 获取所有可用的Books（包含普通Books和DefaultBooks，自动过滤空Book）
        /// 用于构建和编辑器工具链中的统一遍历
        /// </summary>
        public IEnumerable<Book> GetAllUseableBooks()
        {
            // 遍历普通Books
            if (Books != null && Books.Count > 0)
            {
                foreach (var book in Books)
                {
                    if (book != null && book is IBook iBook && iBook.PageCount > 0)
                    {
                        yield return book;
                    }
                }
            }
            
            // 遍历DefaultBooks
            if (DefaultBooks != null)
            {
                foreach (var book in DefaultBooks)
                {
                    if (book != null && book is IBook iBook && iBook.PageCount > 0)
                    {
                        yield return book;
                    }
                }
            }
        }
        
        [LabelText("描述")]
        public string Desc = "";
        public string GetSTR()
        {
            return Name;
        }

        public void SetSTR(string str)
        {
            Name = str;
        }
    }
    
    #region 颜色标记系统
    
    /// <summary>
    /// 颜色标记类型，用于编辑器中标记Books和Pages
    /// 提供视觉分类和快速识别功能
    /// </summary>
    public enum ColorLabel
    {
        /// <summary>无颜色标记（默认白色）</summary>
        [InspectorName("无颜色")]
        None = 0,
        
        /// <summary>红色标记 - 适用于紧急、重要或错误相关资源</summary>
        [InspectorName("红色")]
        Red = 1,
        
        /// <summary>橙色标记 - 适用于警告或待处理资源</summary>
        [InspectorName("橙色")]
        Orange = 2,
        
        /// <summary>黄色标记 - 适用于临时或测试资源</summary>
        [InspectorName("黄色")]
        Yellow = 3,
        
        /// <summary>绿色标记 - 适用于已完成或验证通过的资源</summary>
        [InspectorName("绿色")]
        Green = 4,
        
        /// <summary>蓝色标记 - 适用于UI或功能性资源</summary>
        [InspectorName("蓝色")]
        Blue = 5,
        
        /// <summary>紫色标记 - 适用于特效或特殊资源</summary>
        [InspectorName("紫色")]
        Purple = 6,
        
        /// <summary>粉色标记 - 适用于角色或动画资源</summary>
        [InspectorName("粉色")]
        Pink = 7,
        
        /// <summary>灰色标记 - 适用于已弃用或归档资源</summary>
        [InspectorName("灰色")]
        Gray = 8
    }
    
    #endregion

    /// <summary>
    /// Book基类 - 资源页面的容器
    /// 用于组织和管理相关的资源Pages
    /// </summary>
    /// <typeparam name="TPage">Page类型，必须继承自PageBase</typeparam>
    [Serializable]
    public abstract class BookBase<TPage> : IString, IBook where TPage : PageBase, IString, new()
    {
        [LabelText("Book名字")]
        public string Name = "book PreNameToABKeys";
        
        /// <summary>
        /// 编辑器中是否允许编辑默认消息
        /// 对于系统预设的Book，此值为false
        /// </summary>
        public bool WritableDefaultMessageOnEditor=true;
        
        /// <summary>
        /// 推荐资产类别 - DefaultBook专用
        /// 用于资产自动收集功能，标识此Book适合存放哪类资产
        /// </summary>
        [LabelText("推荐资产类别")]
        public ESAssetCategory PreferredAssetCategory = ESAssetCategory.Other;        
        
        /// <summary>
        /// Book的图标类型，用于编辑器显示
        /// 可被CustomIcon覆盖
        /// </summary>
        public virtual EditorIconType Icon => EditorIconType.Folder;
        
        /// <summary>
        /// 自定义图标纹理（优先于EditorIconType）
        /// 为null时使用Icon属性返回的默认图标
        /// </summary>
        [LabelText("自定义图标")]
        public Texture2D CustomIcon;
        
        /// <summary>
        /// 颜色标记 - 用于视觉分类和快速识别
        /// 参见ColorLabel枚举定义
        /// </summary>
        [LabelText("颜色标签")]
        public ColorLabel ColorTag = ColorLabel.None;
        
        /// <summary>
        /// 对当前 Book 的补充说明，例如“战斗相关资源”“登录场景资源”等，
        /// 仅用于编辑器标记与文档，可结合构建管线生成报表。
        /// </summary>
        [LabelText("描述")]
        public string Desc = "";
        [LabelText("收容页面")]
        [SerializeField]
        public List<TPage> pages = new List<TPage>();
        
        /// <summary>
        /// 实现IBook接口 - 获取页面数量（高性能，无反射）
        /// </summary>
        public int PageCount => pages?.Count ?? 0;

        public string GetSTR()
        {
            return Name;
        }

        public void SetSTR(string str)
        {
            Name = str;
        }

        public virtual void EditorOnly_DragAtArea(UnityEngine.Object[] gs)
        {
#if UNITY_EDITOR
            if (gs == null || gs.Length == 0) return;
            
            var assetsToProcess = new List<UnityEngine.Object>();
            
            // 第一步：展开文件夹，收集所有实际资源
            foreach (var obj in gs)
            {
                if (obj == null) continue;
                
                var assetPath = UnityEditor.AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(assetPath)) continue;
                
                // 检查是否为文件夹
                if (UnityEditor.AssetDatabase.IsValidFolder(assetPath))
                {
                    // 递归遍历文件夹中的所有资源
                    var folderAssets = ExpandFolder(assetPath);
                    assetsToProcess.AddRange(folderAssets);
                    UnityEngine.Debug.Log($"[BookBase] 文件夹 [{obj.name}] 展开为 {folderAssets.Count} 个资源");
                }
                else
                {
                    assetsToProcess.Add(obj);
                }
            }
            
            if (assetsToProcess.Count == 0)
            {
                UnityEngine.Debug.LogWarning($"[BookBase] 没有有效的资源可添加到Book [{Name}]");
                return;
            }
            
            // 第二步：处理每个资源
            int addedCount = 0;
            int skippedCount = 0;
            int rejectedCount = 0;
            
            foreach (var obj in assetsToProcess)
            {
                if (obj == null) continue;
                
                var assetPath = UnityEditor.AssetDatabase.GetAssetPath(obj);
                
                // 拒绝.cs文件
                if (assetPath.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
                {
                    UnityEngine.Debug.LogWarning($"[BookBase] 拒绝添加C#脚本文件: {obj.name}", obj);
                    rejectedCount++;
                    continue;
                }
                
                UnityEngine.Object assetToAdd = obj;
                
                // 检测是否为子资产
                if (ESGlobalResToolsSupportConfig.IsSubAsset(obj, out var mainAsset))
                {
                    UnityEngine.Debug.LogWarning(
                        $"[BookBase] 检测到子资产 [{obj.name}]，自动退化为主资产 [{mainAsset.name}]",
                        obj
                    );
                    assetToAdd = mainAsset;
                    
                    // 再次检查主资产是否为.cs文件
                    var mainAssetPath = UnityEditor.AssetDatabase.GetAssetPath(mainAsset);
                    if (mainAssetPath.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
                    {
                        UnityEngine.Debug.LogWarning($"[BookBase] 拒绝添加C#脚本文件: {mainAsset.name}", mainAsset);
                        rejectedCount++;
                        continue;
                    }
                }
                
                // 检查当前Book是否已存在
                if (IsDuplicateAsset(assetToAdd))
                {
                    UnityEngine.Debug.LogWarning($"[BookBase] 资源 [{assetToAdd.name}] 已存在于当前Book [{Name}] 中，跳过添加");
                    skippedCount++;
                    continue;
                }
                
                // 全项目查重检查（DragAtPages的特权）
                var duplicateLocation = FindAssetInProject(assetToAdd);
                if (duplicateLocation.library != null)
                {
                    // 弹窗询问用户
                    string bookName = duplicateLocation.bookName ?? "未知Book";
                    string message = $"资源 '{assetToAdd.name}' 已存在于其他位置：\n\n"
                        + $"Library: {duplicateLocation.library.Name}\n"
                        + $"Book: {bookName}\n\n"
                        + "是否强制重复收集到当前Book？\n\n"
                        + "• 强制收集：资源将同时存在于多个位置\n"
                        + "• 取消：跳过此资源";
                    
                    bool forceAdd = UnityEditor.EditorUtility.DisplayDialog(
                        "⚠️ 检测到重复资源",
                        message,
                        "强制收集",
                        "取消"
                    );
                    
                    if (!forceAdd)
                    {
                        UnityEngine.Debug.Log($"[BookBase] 用户取消添加重复资源: {assetToAdd.name}");
                        skippedCount++;
                        continue;
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"[BookBase] 用户强制添加重复资源: {assetToAdd.name}", assetToAdd);
                    }
                }
                
                // 添加资源
                pages.Add(CreateNewPage(assetToAdd));
                addedCount++;
            }
            
            // 汇总报告
            string summary = $"[BookBase] Book [{Name}] 资源添加完成：\n"
                + $"  • 成功添加: {addedCount}\n"
                + $"  • 跳过重复: {skippedCount}\n"
                + $"  • 拒绝文件: {rejectedCount}";
            UnityEngine.Debug.Log(summary);
            
            if (addedCount > 0)
            {
                UnityEditor.EditorUtility.SetDirty(this as UnityEngine.Object);
            }
#endif
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// 递归展开文件夹，获取所有子资源（排除文件夹本身）
        /// </summary>
        private List<UnityEngine.Object> ExpandFolder(string folderPath)
        {
            var result = new List<UnityEngine.Object>();
            
            // 获取文件夹中所有GUID
            var guids = UnityEditor.AssetDatabase.FindAssets("", new[] { folderPath });
            
            foreach (var guid in guids)
            {
                var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                
                // 跳过文件夹本身
                if (UnityEditor.AssetDatabase.IsValidFolder(assetPath))
                    continue;
                
                // 跳过.cs文件
                if (assetPath.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset != null)
                {
                    result.Add(asset);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 在整个项目中查找资产是否已存在（包括文件夹包含检测）
        /// </summary>
        private (LibrarySoBase<BookBase<TPage>> library, string bookName) FindAssetInProject(UnityEngine.Object asset)
        {
            if (asset == null)
                return (null, null);
            
            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
                return (null, null);
            
            // 获取所有同类型Library
            var allLibraries = ESEditorSO.SOS.GetNewGroupOfType<LibrarySoBase<BookBase<TPage>>>();
            if (allLibraries == null)
                return (null, null);
            
            foreach (var lib in allLibraries)
            {
                if (lib == null) continue;
                
                // 遍历所有Books（包括自定义和默认）
                foreach (var book in lib.GetAllUseableBooks())
                {
                    if (book?.pages == null) continue;
                    
                    foreach (var page in book.pages)
                    {
                        if (!(page is IAssetPage assetPage) || assetPage.OB == null)
                            continue;
                        
                        var pageAssetPath = UnityEditor.AssetDatabase.GetAssetPath(assetPage.OB);
                        
                        // 情况1：完全相同的资产
                        if (assetPage.OB == asset)
                        {
                            UnityEngine.Debug.Log($"[FindAsset] 发现完全相同的资产引用: {asset.name}");
                            return (lib, book.Name);
                        }
                        
                        // 情况2：Page的OB是文件夹，检查资产是否在该文件夹内
                        if (UnityEditor.AssetDatabase.IsValidFolder(pageAssetPath))
                        {
                            // 检查资产路径是否以文件夹路径开头（即资产在文件夹内）
                            if (assetPath.StartsWith(pageAssetPath + "/", System.StringComparison.OrdinalIgnoreCase))
                            {
                                UnityEngine.Debug.LogWarning(
                                    $"[FindAsset] 发现文件夹包含关系: 资产 [{asset.name}] 已被文件夹Page [{assetPage.OB.name}] 包含\n"
                                    + $"  资产路径: {assetPath}\n"
                                    + $"  文件夹路径: {pageAssetPath}"
                                );
                                return (lib, book.Name);
                            }
                        }
                    }
                }
            }
            
            return (null, null);
        }
#endif
        
        /// <summary>
        /// 检查资源是否已存在于Book中
        /// </summary>
        protected virtual bool IsDuplicateAsset(UnityEngine.Object asset)
        {
            if (asset == null || pages == null)
                return false;
            
            // 遍历所有Page检查是否有相同资源
            foreach (var page in pages)
            {
                // 直接使用类型检查，无反射开销
                if (page is IAssetPage assetPage && assetPage.OB == asset)
                {
                    return true;
                }
            }
            
            return false;
        }

        public virtual TPage CreateNewPage(UnityEngine.Object uo)
        {

            return new TPage() { Name = uo.name };
        }

    }


    [Serializable]
    public abstract class PageBase : IESPage, IString
    {
        [LabelText("资源页名")]
        public string Name = "资源页名";
        
        /// <summary>
        /// 颜色标记
        /// </summary>
        [LabelText("颜色标签")]
        public ColorLabel ColorTag = ColorLabel.None;
        
        public string GetSTR()
        {
            return Name;
        }

        public void SetSTR(string str)
        {
            Name = str;
        }

        public virtual bool Draw()
        {
            return false;
        }

    }

}
