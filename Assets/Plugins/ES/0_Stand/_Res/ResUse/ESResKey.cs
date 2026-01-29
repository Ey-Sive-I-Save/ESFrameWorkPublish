using System;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 资源查询键（Resource Query Key）
    /// 
    /// 【核心职责】
    /// ESResKey是ES资源系统的统一资源标识符，用于唯一定位和查询各类资源。
    /// 它整合了资源的来源类型、库信息、AB包信息、资源名称等多维度数据，
    /// 是ESResMaster、ESResLoader、ESResTable等核心组件的通信桥梁。
    /// 
    /// 【支持的资源类型】
    /// 1. ABAsset       - AssetBundle中的资源（Prefab、Texture、AudioClip等）
    /// 2. AB            - AssetBundle包本身
    /// 3. Scene         - 场景资源
    /// 4. Shader        - Shader资源
    /// 5. RawFile       - 原始文件（二进制数据、文本文件等）
    /// 6. InternalResource - Unity Resources文件夹资源
    /// 7. NetImageRes   - 网络图片资源（HTTP/HTTPS下载）
    /// 
    /// 【关键字段说明】
    /// - SourceLoadType   : 资源加载类型（决定使用哪个ESResSource子类）
    /// - LibName          : 资源库名称（用于多库管理）
    /// - LibFolderName    : 资源库文件夹名称
    /// - ABPreName        : AB包前缀名（不含Hash后缀）
    /// - ResName          : 资源名称（Asset名、文件名、URL等）
    /// - GUID             : 资源唯一标识符（Unity资产的GUID）
    /// - Path             : 资源路径（AssetDatabase路径或文件路径）
    /// - TargetType       : 目标类型（如Texture2D、AudioClip等）
    /// - LocalABLoadPath  : AB包本地加载路径（运行时动态设置）
    /// 
    /// 【对象池支持】
    /// ESResKey实现了IPoolableAuto接口，通过ESResMaster.PoolForESResKey管理。
    /// - 获取：ESResMaster.Instance.PoolForESResKey.GetInPool()
    /// - 归还：key.TryAutoPushedToPool()
    /// - 优势：减少GC压力，提高资源查询性能
    /// 
    /// 【典型使用场景】
    /// 
    /// 场景1：加载AssetBundle中的Prefab
    /// <code>
    /// var key = ESResMaster.Instance.PoolForESResKey.GetInPool();
    /// key.SourceLoadType = ESResSourceLoadType.ABAsset;
    /// key.LibName = "DefaultLib";
    /// key.ABPreName = "ui_mainmenu";
    /// key.ResName = "MainMenuPanel";
    /// key.TargetType = typeof(GameObject);
    /// ESResLoader.Instance.AddAsset2Load(key, ...);
    /// </code>
    /// 
    /// 场景2：加载Resources文件夹资源
    /// <code>
    /// var key = ESResMaster.Instance.PoolForESResKey.GetInPool();
    /// key.SourceLoadType = ESResSourceLoadType.InternalResource;
    /// key.ResName = "UI/Icons/default_icon"; // Resources路径，不含扩展名
    /// ESResLoader.Instance.AddInternalResource2Load("UI/Icons/default_icon", ...);
    /// </code>
    /// 
    /// 场景3：加载网络图片
    /// <code>
    /// var key = ESResMaster.Instance.PoolForESResKey.GetInPool();
    /// key.SourceLoadType = ESResSourceLoadType.NetImageRes;
    /// key.ResName = "https://example.com/avatar.jpg"; // URL作为ResName
    /// ESResLoader.Instance.AddNetImage2Load("https://example.com/avatar.jpg", ...);
    /// </code>
    /// 
    /// 场景4：加载原始文件
    /// <code>
    /// var key = ESResMaster.Instance.PoolForESResKey.GetInPool();
    /// key.SourceLoadType = ESResSourceLoadType.RawFile;
    /// key.ResName = "Configs/game_settings.json"; // 相对路径
    /// key.LocalABLoadPath = Application.persistentDataPath + "/Configs/game_settings.json";
    /// ESResLoader.Instance.AddRawFile2Load(key.LocalABLoadPath, ...);
    /// </code>
    /// 
    /// 【字段组合规则】
    /// 不同资源类型需要设置不同的字段组合：
    /// 
    /// ABAsset资源：
    ///   ✅ 必需：SourceLoadType, LibName, ABPreName, ResName, TargetType
    ///   ⚙️ 可选：GUID, Path, LibFolderName
    /// 
    /// InternalResource资源：
    ///   ✅ 必需：SourceLoadType, ResName（Resources路径，无扩展名）
    ///   ⚠️ 不需要：GUID, ABPreName, LibName
    /// 
    /// NetImageRes资源：
    ///   ✅ 必需：SourceLoadType, ResName（完整URL）
    ///   ⚠️ 不需要：GUID, ABPreName, LibName
    /// 
    /// RawFile资源：
    ///   ✅ 必需：SourceLoadType, ResName, LocalABLoadPath（完整文件路径）
    ///   ⚠️ 不需要：GUID, ABPreName（除非是AB中的RawFile）
    /// 
    /// 【哈希与相等性】
    /// ⚠️ 注意：ESResKey作为Dictionary的Key使用时，依赖引用相等性（ReferenceEquals）。
    /// 如果需要基于内容比较，建议在ESResTable中使用自定义的KeyComparer。
    /// 当前实现中，ESResTable使用ESResKey实例作为键，不依赖内容哈希。
    /// 
    /// 【内存管理建议】
    /// 1. 总是从对象池获取：避免频繁new ESResKey()
    /// 2. 及时归还对象池：使用完毕后调用TryAutoPushedToPool()
    /// 3. 避免长期持有：ESResKey应作为临时查询键，不应作为成员变量长期持有
    /// 4. 注意IsRecycled状态：归还对象池后IsRecycled=true，不应再使用
    /// 
    /// 【与其他组件的协作】
    /// - ESResLoader：使用ESResKey指定要加载的资源
    /// - ESResMaster：通过ESResKey查询已注册的资源
    /// - ESResTable：使用ESResKey作为字典键索引资源
    /// - ESResSourceFactory：根据SourceLoadType创建对应的ESResSource实例
    /// 
    /// 【设计原则】
    /// ✅ 轻量级数据结构：仅包含资源查询所需的最少信息
    /// ✅ 类型安全：TargetType确保资源类型匹配
    /// ✅ 对象池友好：实现IPoolableAuto接口，支持高频复用
    /// ✅ 灵活扩展：支持多种资源加载类型，易于扩展新类型
    /// </summary>
    [Serializable]
    public class ESResKey : IPoolableAuto
    {
        #region 资源类型与来源信息

        /// <summary>
        /// 资源加载类型（决定使用哪个ESResSource子类）
        /// 
        /// 【类型说明】
        /// - ABAsset: AB包中的Asset资源（Prefab、Texture、AudioClip等）
        /// - AB: AB包本身
        /// - Scene: 场景资源
        /// - Shader: Shader资源
        /// - RawFile: 原始文件（二进制数据、JSON、XML等）
        /// - InternalResource: Unity Resources文件夹资源
        /// - NetImageRes: 网络图片资源（HTTP/HTTPS）
        /// 
        /// 【默认值】
        /// ABAsset（最常用的资源类型）
        /// </summary>
        public ESResSourceLoadType SourceLoadType = ESResSourceLoadType.ABAsset;

        #endregion

        #region 资源库信息（用于多库管理）

        /// <summary>
        /// 资源库名称（Library Name）
        /// 
        /// 【用途】
        /// 用于支持多资源库管理，不同库可以有独立的加载策略。
        /// 例如：
        /// - "DefaultLib": 默认资源库
        /// - "DLCLib": DLC资源库
        /// - "ModLib": Mod资源库
        /// 
        /// 【注意】
        /// - InternalResource和NetImageRes类型不需要此字段
        /// - RawFile类型根据情况决定是否需要
        /// </summary>
        public string LibName;

        /// <summary>
        /// 资源库文件夹名称
        /// 
        /// 【用途】
        /// 资源库在文件系统中的文件夹名称，用于构建完整路径。
        /// 例如：StreamingAssets/DefaultLib/
        /// 
        /// 【注意】
        /// 通常由ESResMaster自动设置，开发者较少手动设置
        /// </summary>
        public string LibFolderName;

        #endregion

        #region AssetBundle信息

        /// <summary>
        /// AB包的前缀名（PreName，不含Hash后缀）
        /// 
        /// 【重要说明】
        /// 这不是完整的AB文件名，完整名需要加上Hash后缀！
        /// 
        /// 【示例】
        /// - PreName: "ui_mainmenu"
        /// - 完整AB文件名: "ui_mainmenu_a1b2c3d4"（Hash后缀由构建系统生成）
        /// 
        /// 【用途】
        /// 1. 在ESResTable中索引AB资源
        /// 2. 与Hash映射表配合，获取完整AB文件名
        /// 3. 支持AB包的增量更新（同名AB通过Hash区分版本）
        /// 
        /// 【注意】
        /// - 仅ABAsset和AB类型需要此字段
        /// - InternalResource、NetImageRes、RawFile类型不需要
        /// </summary>
        public string ABPreName;

        #endregion

        #region 资源名称与标识

        /// <summary>
        /// 资源名称（Resource Name）
        /// 
        /// 【不同资源类型的ResName含义】
        /// 
        /// ABAsset类型：
        ///   - Asset在AB包中的名称
        ///   - 例如："MainMenuPanel"（Prefab名）、"icon_sword"（Texture名）
        /// 
        /// InternalResource类型：
        ///   - Resources文件夹中的相对路径（不含扩展名）
        ///   - 例如："UI/Icons/default_icon"（对应Resources/UI/Icons/default_icon.png）
        /// 
        /// NetImageRes类型：
        ///   - 完整的URL地址
        ///   - 例如："https://example.com/avatars/user123.jpg"
        /// 
        /// RawFile类型：
        ///   - 文件的相对路径或标识名
        ///   - 例如："Configs/game_settings.json"
        /// 
        /// Scene类型：
        ///   - 场景名称
        ///   - 例如："MainScene"
        /// 
        /// 【关键特性】
        /// - 这是最重要的资源标识字段之一
        /// - 与SourceLoadType配合使用，确定资源的完整定位
        /// </summary>
        public string ResName;

        /// <summary>
        /// Unity资产的全局唯一标识符（GUID）
        /// 
        /// 【用途】
        /// 1. Unity AssetDatabase中的唯一标识
        /// 2. 用于资源依赖关系追踪
        /// 3. 支持资源重命名时的稳定引用
        /// 
        /// 【注意】
        /// - 仅Unity项目内的资源有GUID
        /// - InternalResource、NetImageRes、外部RawFile没有GUID
        /// - AB包中的资源在构建时会记录GUID
        /// 
        /// 【格式示例】
        /// "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6"（32位16进制字符串）
        /// </summary>
        public string GUID;

        /// <summary>
        /// 资源路径（AssetDatabase路径或文件系统路径）
        /// 
        /// 【用途】
        /// 1. Editor模式下通过AssetDatabase.LoadAssetAtPath加载
        /// 2. 记录资源在项目中的原始位置
        /// 3. 资源定位和调试
        /// 
        /// 【路径格式】
        /// - Unity资产路径：Assets/Prefabs/Player.prefab
        /// - 文件系统路径：C:/GameData/Configs/settings.json
        /// 
        /// 【注意】
        /// - 主要用于Editor模式
        /// - 运行时通常不使用此字段（使用LocalABLoadPath代替）
        /// </summary>
        public string Path;

        #endregion

        #region 类型信息

        /// <summary>
        /// 目标资源类型（Target Type）
        /// 
        /// 【用途】
        /// 1. 类型安全：确保加载的资源类型正确
        /// 2. 类型转换：AssetBundle.LoadAsset<T>需要指定类型
        /// 3. 资源过滤：根据类型筛选资源
        /// 
        /// 【常见类型】
        /// - GameObject       : Prefab、场景对象
        /// - Texture2D        : 2D纹理、UI图标
        /// - AudioClip        : 音频文件
        /// - Material         : 材质
        /// - TextAsset        : 文本文件（JSON、XML、TXT）
        /// - ScriptableObject : 配置数据
        /// - Sprite           : UI精灵图
        /// 
        /// 【注意】
        /// - InternalResource和ABAsset必须指定正确的类型
        /// - RawFile通常不需要（加载为byte[]）
        /// - NetImageRes自动转换为Texture2D
        /// </summary>
        public Type TargetType;

        #endregion

        #region 运行时路径信息

        /// <summary>
        /// AB包本地加载路径（运行时动态设置）
        /// 
        /// 【用途】
        /// 1. 指定AB包或RawFile的完整文件系统路径
        /// 2. 运行时根据不同平台动态设置
        /// 3. 支持热更新资源的路径切换
        /// 
        /// 【典型路径】
        /// - StreamingAssets: Application.streamingAssetsPath + "/DefaultLib/ui_mainmenu"
        /// - PersistentData:  Application.persistentDataPath + "/DownloadCache/ui_mainmenu"
        /// - 临时路径:        Application.temporaryCachePath + "/temp_file.dat"
        /// 
        /// 【使用场景】
        /// - AB包加载：AssetBundle.LoadFromFile(LocalABLoadPath)
        /// - RawFile读取：File.ReadAllBytes(LocalABLoadPath)
        /// 
        /// 【注意】
        /// - [NonSerialized]：不会被序列化保存
        /// - 运行时由ESResMaster根据加载策略动态设置
        /// - 每次使用前应重新设置，避免使用过期路径
        /// </summary>
        [NonSerialized]
        public string LocalABLoadPath;

        #endregion

        #region 对象池接口实现

        /// <summary>
        /// 对象是否已回收到对象池（IPoolableAuto接口要求）
        /// 
        /// 【状态说明】
        /// - false: 对象正在使用中
        /// - true:  对象已归还对象池，不应再使用
        /// 
        /// 【安全检查】
        /// 在使用ESResKey前，应检查IsRecycled状态：
        /// <code>
        /// if (key.IsRecycled) {
        ///     Debug.LogError("尝试使用已回收的ESResKey！");
        ///     return;
        /// }
        /// </code>
        /// 
        /// 【自动管理】
        /// - GetInPool()时自动设置为false
        /// - PushToPool()时自动设置为true
        /// </summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 重置为对象池可用状态（IPoolableAuto接口要求）
        /// 
        /// 【调用时机】
        /// 对象归还到对象池时，自动调用此方法清理状态。
        /// 
        /// 【清理内容】
        /// 根据需要重置字段为默认值，避免对象池中的实例携带旧数据。
        /// 当前实现为空，因为ESResKey字段会在GetInPool后重新设置。
        /// 
        /// 【扩展建议】
        /// 如果未来需要更严格的状态清理，可以在此方法中：
        /// <code>
        /// public void OnResetAsPoolable()
        /// {
        ///     SourceLoadType = ESResSourceLoadType.ABAsset;
        ///     LibName = null;
        ///     ABPreName = null;
        ///     ResName = null;
        ///     GUID = null;
        ///     Path = null;
        ///     TargetType = null;
        ///     LocalABLoadPath = null;
        /// }
        /// </code>
        /// </summary>
        public void OnResetAsPoolable()
        {
            // 当前为空实现
            // ESResKey的字段会在使用时重新设置，无需在此清理
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 转换为易读的字符串表示（用于调试和日志输出）
        /// 
        /// 【输出格式】
        /// "资源查询键, 库名:{LibName} AB包名:{ABPreName} 类型:{TargetType},资源名{ResName}"
        /// 
        /// 【使用场景】
        /// 1. Debug.Log输出
        /// 2. 资源加载日志
        /// 3. 错误报告
        /// 
        /// 【示例输出】
        /// "资源查询键, 库名:DefaultLib AB包名:ui_mainmenu 类型:UnityEngine.GameObject,资源名MainMenuPanel"
        /// </summary>
        public override string ToString()
        {
            return string.Format("资源查询键, 库名:{0} AB包名:{1} 类型:{2},资源名{3}", 
                LibName, ABPreName, TargetType, ResName);
        }

        /// <summary>
        /// 尝试自动归还对象池（对象池管理方法）
        /// 
        /// 【归还流程】
        /// 1. 调用ESResMaster.Instance.PoolForESResKey.PushToPool(this)
        /// 2. 对象池自动设置IsRecycled=true
        /// 3. 对象池自动调用OnResetAsPoolable()清理状态
        /// 
        /// 【调用时机】
        /// - ESResLoader加载完成后
        /// - ESResMaster资源查询完成后
        /// - 任何不再需要此ESResKey的地方
        /// 
        /// 【使用示例】
        /// <code>
        /// var key = ESResMaster.Instance.PoolForESResKey.GetInPool();
        /// key.SourceLoadType = ESResSourceLoadType.ABAsset;
        /// key.ResName = "MainPanel";
        /// // ... 使用key进行资源加载 ...
        /// key.TryAutoPushedToPool(); // 使用完毕后归还
        /// </code>
        /// 
        /// 【注意事项】
        /// ⚠️ 归还后不应再访问此对象，IsRecycled=true表示已失效
        /// ⚠️ 如果仍有其他地方持有引用，可能导致使用已回收对象的错误
        /// </summary>
        public void TryAutoPushedToPool()
        {
            ESResMaster.Instance.PoolForESResKey.PushToPool(this);
        }

        #endregion
    }
}
