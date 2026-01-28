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
        
        [LabelText("包含")]
        public List<Book> Books = new List<Book>();
        [NonSerialized]
        private bool _defaultBooksInitialized = false;
        
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
        /// 初始化默认Books，设置图标和编辑权限
        /// </summary>
        protected virtual void InitializeDefaultBooks()
        {
           
        }
        
        /// <summary>
        /// 获取默认Books，子类重写此方法提供默认Books
        /// </summary>
        protected virtual IEnumerable<Book> GetDefaultBooks() => null;
        
        /// <summary>
        /// 高性能检查资产是否存在于Library中（使用缓存）
        /// </summary>
        public bool ContainsAsset(UnityEngine.Object asset)
        {
            if (asset == null) return false;
            
            // 每次查询前都重建缓存，确保数据准确
            RebuildAssetCache();
            
#if UNITY_EDITOR
            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(asset);
            return !string.IsNullOrEmpty(assetPath) && _assetPathCache != null && _assetPathCache.Contains(assetPath);
#else
            return false;
#endif
        }
        
        /// <summary>
        /// 增量添加资产到缓存（用于批量操作优化）
        /// </summary>
        public void AddAssetToCache(UnityEngine.Object asset)
        {
#if UNITY_EDITOR
            if (asset == null) return;
            
            if (_assetPathCache == null)
            {
                _assetPathCache = new HashSet<string>();
            }
            
            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(assetPath))
            {
                _assetPathCache.Add(assetPath);
            }
#endif
        }
        
        /// <summary>
        /// 重建资产缓存（每次查询前调用）
        /// </summary>
        public void RebuildAssetCache()
        {
            if (_assetPathCache == null)
            {
                _assetPathCache = new HashSet<string>();
            }
            else
            {
                _assetPathCache.Clear();
            }
            
            // 遍历所有Books收集资产
            if (Books != null)
            {
                foreach (var book in Books)
                {
                    if (book != null)
                    {
                        AddBookAssetsToCache(book);
                    }
                }
            }
            
            // 遍历DefaultBooks收集资产
            if (DefaultBooks != null)
            {
                foreach (var book in DefaultBooks)
                {
                    if (book != null)
                    {
                        AddBookAssetsToCache(book);
                    }
                }
            }
        }
        
        /// <summary>
        /// 将Book中的所有资产添加到缓存
        /// </summary>
        private void AddBookAssetsToCache(Book book)
        {
#if UNITY_EDITOR
            // 使用反射获取pages字段
            var pagesField = book.GetType().GetField("pages");
            if (pagesField != null)
            {
                var pages = pagesField.GetValue(book) as System.Collections.IList;
                if (pages != null)
                {
                    foreach (var page in pages)
                    {
                        if (page == null) continue;
                        
                        // 直接使用类型检查，无反射开销
                        if (page is IAssetPage assetPage && assetPage.OB != null)
                        {
                            var assetPath = UnityEditor.AssetDatabase.GetAssetPath(assetPage.OB);
                            if (!string.IsNullOrEmpty(assetPath))
                            {
                                _assetPathCache.Add(assetPath);
                            }
                        }
                    }
                }
            }
#endif
        }
        
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
    public abstract class BookBase<TPage> : IString where TPage : PageBase, IString, new()
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
            foreach (var i in gs)
            {
                if (i != null)
                {
                    // 检查是否已存在相同资源
                    if (!IsDuplicateAsset(i))
                    {
                        pages.Add(CreateNewPage(i));
                    }
                    else
                    {
#if UNITY_EDITOR
                        UnityEngine.Debug.LogWarning($"[BookBase] 资源 [{i.name}] 已存在于Book [{Name}] 中，跳过添加");
#endif
                    }
                }
            }

        }
        
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
