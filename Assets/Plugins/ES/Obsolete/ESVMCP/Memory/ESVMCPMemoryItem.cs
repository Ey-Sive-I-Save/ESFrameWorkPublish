using UnityEngine;
using System;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES.VMCP
{
    /// <summary>
    /// 记忆项类型
    /// </summary>
    public enum ESVMCPMemoryItemType
    {
        GameObject,         // GameObject实例
        Asset,             // 资产（Material、Prefab等）
        Component,         // 组件
        Primitive,         // 基础类型（int、string等）
        Custom             // 自定义类型
    }

    /// <summary>
    /// 解析结果枚举
    /// </summary>
    public enum ResolveResultType
    {
        Success,        // 解析成功
        Failed,         // 解析失败
        NotFound,       // 对象不存在
        TypeMismatch    // 类型不匹配
    }

    /// <summary>
    /// 解析结果结构体
    /// </summary>
    public struct ResolveResult
    {
        public ResolveResultType ResultType;
        public object Value;
        public string ErrorMessage;
        public ESVMCPMemoryItemType ItemType;

        public bool Success => ResultType == ResolveResultType.Success;

        public static ResolveResult Succeed(object value, ESVMCPMemoryItemType itemType)
        {
            return new ResolveResult 
            { 
                ResultType = ResolveResultType.Success, 
                Value = value, 
                ItemType = itemType 
            };
        }

        public static ResolveResult Fail(ResolveResultType resultType, string message, ESVMCPMemoryItemType itemType)
        {
            return new ResolveResult 
            { 
                ResultType = resultType, 
                ErrorMessage = message, 
                ItemType = itemType 
            };
        }
    }

    /// <summary>
    /// ESVMCP记忆项 - 支持多种引用方式的聚合对象
    /// 设计理念：同时存储多种引用方式，自动选择最可靠的方式进行解析
    /// </summary>
    [Serializable]
    public class ESVMCPMemoryItem
    {
        
        [LabelText("记忆键")]
        [ReadOnly]
        public string Key;

        [LabelText("类型")]
        [ReadOnly]
        public ESVMCPMemoryItemType ItemType;

        [LabelText("创建时间")]
        [ReadOnly]
        public string CreatedTime;

        [LabelText("最后访问")]
        [ReadOnly]
        public string LastAccessTime;

        [LabelText("访问次数")]
        [ReadOnly]
        public int AccessCount;

        [LabelText("重要性评分")]
        [Range(0, 100)]
        [Tooltip("重要性评分，影响记忆衰减策略")]
        public int ImportanceScore = 50;


        #region 多种引用方式（聚合存储）

        [TitleGroup("引用信息")]
        [LabelText("资产路径")]
        [ShowIf("@ItemType == ESVMCPMemoryItemType.Asset")]
        public string AssetPath;

        [TitleGroup("引用信息")]
        [LabelText("资产GUID")]
        [ShowIf("@ItemType == ESVMCPMemoryItemType.Asset")]
        public string AssetGUID;

        [TitleGroup("引用信息")]
        [LabelText("资产名称")]
        public string AssetName;

        [TitleGroup("引用信息")]
        [LabelText("实例ID")]
        [ShowIf("@ItemType == ESVMCPMemoryItemType.GameObject || ItemType == ESVMCPMemoryItemType.Component")]
        public int InstanceID;

        [TitleGroup("引用信息")]
        [LabelText("GameObject名称")]
        [ShowIf("@ItemType == ESVMCPMemoryItemType.GameObject || ItemType == ESVMCPMemoryItemType.Component")]
        public string GameObjectName;

        [TitleGroup("引用信息")]
        [LabelText("GameObject路径")]
        [ShowIf("@ItemType == ESVMCPMemoryItemType.GameObject || ItemType == ESVMCPMemoryItemType.Component")]
        public string GameObjectPath;

        [TitleGroup("引用信息")]
        [LabelText("组件类型")]
        [ShowIf("@ItemType == ESVMCPMemoryItemType.Component")]
        public string ComponentType;

        [TitleGroup("引用信息")]
        [LabelText("基础值")]
        [ShowIf("@ItemType == ESVMCPMemoryItemType.Primitive")]
        public string PrimitiveValue;

        [TitleGroup("引用信息")]
        [LabelText("自定义数据")]
        [ShowIf("@ItemType == ESVMCPMemoryItemType.Custom")]
        [TextArea(3, 10)]
        public string CustomData;

        #endregion

        #region 运行时引用（不序列化）

        [NonSerialized]
        [ShowInInspector, ReadOnly]
        [LabelText("运行时对象引用")]
        [HideIf("@_runtimeObject == null")]
        private UnityEngine.Object _runtimeObject;

        [NonSerialized]
        [ShowInInspector, ReadOnly]
        [LabelText("运行时GameObject引用")]
        [HideIf("@_runtimeGameObject == null")]
        private GameObject _runtimeGameObject;

        [NonSerialized]
        [ShowInInspector, ReadOnly]
        [LabelText("运行时组件引用")]
        [HideIf("@_runtimeComponent == null")]
        private Component _runtimeComponent;

        #endregion

        #region 构造函数

        /// <summary>
        /// 从GameObject创建记忆项
        /// </summary>
        public static ESVMCPMemoryItem FromGameObject(string key, GameObject go)
        {
            if (go == null) return null;

            var item = new ESVMCPMemoryItem
            {
                Key = key,
                ItemType = ESVMCPMemoryItemType.GameObject,
                CreatedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                LastAccessTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                AccessCount = 0,
                ImportanceScore = 50,

                // 存储多种引用方式
                InstanceID = go.GetInstanceID(),
                GameObjectName = go.name,
                GameObjectPath = GetGameObjectPath(go),
                AssetName = go.name,

                // 运行时引用
                _runtimeGameObject = go,
                _runtimeObject = go
            };

            return item;
        }

        /// <summary>
        /// 从资产创建记忆项（Material、Prefab等）
        /// </summary>
        public static ESVMCPMemoryItem FromAsset(string key, UnityEngine.Object asset)
        {
            if (asset == null) return null;

            var item = new ESVMCPMemoryItem
            {
                Key = key,
                ItemType = ESVMCPMemoryItemType.Asset,
                CreatedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                LastAccessTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                AccessCount = 0,
                ImportanceScore = 70, // 资产默认更重要

                AssetName = asset.name,
                _runtimeObject = asset
            };

#if UNITY_EDITOR
            // 获取资产路径和GUID
            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
            {
                item.AssetPath = path;
                item.AssetGUID = AssetDatabase.AssetPathToGUID(path);
            }
#endif

            return item;
        }

        /// <summary>
        /// 从组件创建记忆项
        /// </summary>
        public static ESVMCPMemoryItem FromComponent(string key, Component component)
        {
            if (component == null) return null;

            var item = new ESVMCPMemoryItem
            {
                Key = key,
                ItemType = ESVMCPMemoryItemType.Component,
                CreatedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                LastAccessTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                AccessCount = 0,
                ImportanceScore = 50,

                InstanceID = component.GetInstanceID(),
                GameObjectName = component.gameObject.name,
                GameObjectPath = GetGameObjectPath(component.gameObject),
                ComponentType = component.GetType().Name,
                AssetName = component.name,

                _runtimeComponent = component,
                _runtimeGameObject = component.gameObject,
                _runtimeObject = component
            };

            return item;
        }

        /// <summary>
        /// 从基础类型创建记忆项
        /// </summary>
        public static ESVMCPMemoryItem FromPrimitive(string key, object value)
        {
            return new ESVMCPMemoryItem
            {
                Key = key,
                ItemType = ESVMCPMemoryItemType.Primitive,
                CreatedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                LastAccessTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                AccessCount = 0,
                ImportanceScore = 30, // 基础类型重要性较低

                PrimitiveValue = value?.ToString() ?? "",
                AssetName = key
            };
        }

        #endregion

        #region 解析方法（多策略尝试）

        /// <summary>
        /// 解析为GameObject - 多策略尝试
        /// </summary>
        public GameObject ResolveAsGameObject()
        {
            UpdateAccessInfo();

            // 策略1：运行时引用（最快）
            if (_runtimeGameObject != null)
            {
                return _runtimeGameObject;
            }

            // 策略2：通过InstanceID查找
            if (InstanceID != 0)
            {
                var obj = EditorUtilityHelper.InstanceIDToObject(InstanceID);
                if (obj is GameObject go)
                {
                    _runtimeGameObject = go;
                    _runtimeObject = go;
                    return go;
                }
            }

            // 策略3：通过路径查找
            if (!string.IsNullOrEmpty(GameObjectPath))
            {
                var go = GameObject.Find(GameObjectPath);
                if (go != null)
                {
                    _runtimeGameObject = go;
                    _runtimeObject = go;
                    InstanceID = go.GetInstanceID(); // 更新InstanceID
                    return go;
                }
            }

            // 策略4：通过名称查找
            if (!string.IsNullOrEmpty(GameObjectName))
            {
                var go = GameObject.Find(GameObjectName);
                if (go != null)
                {
                    _runtimeGameObject = go;
                    _runtimeObject = go;
                    InstanceID = go.GetInstanceID();
                    GameObjectPath = GetGameObjectPath(go);
                    return go;
                }
            }

            Debug.LogWarning($"[ESVMCP Memory] 无法解析GameObject记忆: {Key}");
            return null;
        }

        /// <summary>
        /// 解析为资产 - 多策略尝试
        /// </summary>
        public UnityEngine.Object ResolveAsAsset()
        {
            UpdateAccessInfo();

            // 策略1：运行时引用
            if (_runtimeObject != null)
            {
                return _runtimeObject;
            }

#if UNITY_EDITOR
            // 策略2：通过GUID加载（最可靠）
            if (!string.IsNullOrEmpty(AssetGUID))
            {
                string path = AssetDatabase.GUIDToAssetPath(AssetGUID);
                if (!string.IsNullOrEmpty(path))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (asset != null)
                    {
                        _runtimeObject = asset;
                        AssetPath = path; // 更新路径
                        return asset;
                    }
                }
            }

            // 策略3：通过路径加载
            if (!string.IsNullOrEmpty(AssetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetPath);
                if (asset != null)
                {
                    _runtimeObject = asset;
                    AssetGUID = AssetDatabase.AssetPathToGUID(AssetPath); // 更新GUID
                    return asset;
                }
            }

            // 策略4：通过名称查找
            if (!string.IsNullOrEmpty(AssetName))
            {
                string[] guids = AssetDatabase.FindAssets(AssetName);
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (asset != null)
                    {
                        _runtimeObject = asset;
                        AssetPath = path;
                        AssetGUID = guids[0];
                        return asset;
                    }
                }
            }
#endif

            Debug.LogWarning($"[ESVMCP Memory] 无法解析Asset记忆: {Key}");
            return null;
        }

        /// <summary>
        /// 解析为组件 - 多策略尝试
        /// </summary>
        public Component ResolveAsComponent()
        {
            UpdateAccessInfo();

            // 策略1：运行时引用
            if (_runtimeComponent != null)
            {
                return _runtimeComponent;
            }

            // 策略2：先解析GameObject，再获取组件
            var go = ResolveAsGameObject();
            if (go != null && !string.IsNullOrEmpty(ComponentType))
            {
                Type type = Type.GetType(ComponentType);
                if (type != null)
                {
                    var component = go.GetComponent(type);
                    if (component != null)
                    {
                        _runtimeComponent = component;
                        _runtimeObject = component;
                        return component;
                    }
                }
            }

            Debug.LogWarning($"[ESVMCP Memory] 无法解析Component记忆: {Key}");
            return null;
        }

        /// <summary>
        /// 解析为基础类型
        /// </summary>
        public T ResolveAsPrimitive<T>()
        {
            UpdateAccessInfo();

            try
            {
                return (T)Convert.ChangeType(PrimitiveValue, typeof(T));
            }
            catch
            {
                Debug.LogWarning($"[ESVMCP Memory] 无法解析Primitive记忆: {Key}");
                return default;
            }
        }

        /// <summary>
        /// 通用解析方法 - 返回解析结果枚举和值
        /// </summary>
        public ResolveResult Resolve()
        {
            switch (ItemType)
            {
                case ESVMCPMemoryItemType.GameObject:
                    var go = ResolveAsGameObject();
                    return go != null 
                        ? ResolveResult.Succeed(go, ItemType) 
                        : ResolveResult.Fail(ResolveResultType.NotFound, $"GameObject not found: {Key}", ItemType);

                case ESVMCPMemoryItemType.Asset:
                    var asset = ResolveAsAsset();
                    return asset != null 
                        ? ResolveResult.Succeed(asset, ItemType) 
                        : ResolveResult.Fail(ResolveResultType.NotFound, $"Asset not found: {Key}", ItemType);

                case ESVMCPMemoryItemType.Component:
                    var component = ResolveAsComponent();
                    return component != null 
                        ? ResolveResult.Succeed(component, ItemType) 
                        : ResolveResult.Fail(ResolveResultType.NotFound, $"Component not found: {Key}", ItemType);

                case ESVMCPMemoryItemType.Primitive:
                    return !string.IsNullOrEmpty(PrimitiveValue) 
                        ? ResolveResult.Succeed(PrimitiveValue, ItemType) 
                        : ResolveResult.Fail(ResolveResultType.Failed, $"Primitive value is empty: {Key}", ItemType);

                case ESVMCPMemoryItemType.Custom:
                    return !string.IsNullOrEmpty(CustomData) 
                        ? ResolveResult.Succeed(CustomData, ItemType) 
                        : ResolveResult.Fail(ResolveResultType.Failed, $"Custom data is empty: {Key}", ItemType);

                default:
                    return ResolveResult.Fail(ResolveResultType.TypeMismatch, $"Unsupported item type: {ItemType}", ItemType);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 更新访问信息
        /// </summary>
        private void UpdateAccessInfo()
        {
            LastAccessTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            AccessCount++;

            // 根据访问频率调整重要性
            if (AccessCount > 10)
            {
                ImportanceScore = Mathf.Min(100, ImportanceScore + 1);
            }
        }

        /// <summary>
        /// 获取GameObject的完整路径
        /// </summary>
        private static string GetGameObjectPath(GameObject go)
        {
            if (go == null) return "";

            string path = go.name;
            Transform current = go.transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        /// <summary>
        /// 检查记忆项是否有效
        /// </summary>
        public bool IsValid()
        {
            switch (ItemType)
            {
                case ESVMCPMemoryItemType.GameObject:
                    return ResolveAsGameObject() != null;
                case ESVMCPMemoryItemType.Asset:
                    return ResolveAsAsset() != null;
                case ESVMCPMemoryItemType.Component:
                    return ResolveAsComponent() != null;
                case ESVMCPMemoryItemType.Primitive:
                    return !string.IsNullOrEmpty(PrimitiveValue);
                case ESVMCPMemoryItemType.Custom:
                    return !string.IsNullOrEmpty(CustomData);
                default:
                    return false;
            }
        }

        /// <summary>
        /// 计算记忆衰减分数
        /// </summary>
        public float GetDecayScore()
        {
            // 基础分数 = 重要性评分
            float score = ImportanceScore;

            // 根据访问次数调整
            score += Mathf.Min(20, AccessCount * 2);

            // 根据最后访问时间调整
            if (DateTime.TryParse(LastAccessTime, out DateTime lastAccess))
            {
                double daysSinceAccess = (DateTime.Now - lastAccess).TotalDays;
                score -= (float)(daysSinceAccess * 2); // 每天减2分
            }

            return Mathf.Max(0, score);
        }

        #endregion
    }

    /// <summary>
    /// Editor辅助类
    /// </summary>
    public static class EditorUtilityHelper
    {
        public static UnityEngine.Object InstanceIDToObject(int instanceID)
        {
#if UNITY_EDITOR
            return EditorUtility.InstanceIDToObject(instanceID);
#else
            // 运行时查找所有对象（性能较差）
            var allObjects = UnityEngine.Object.FindObjectsOfType<UnityEngine.Object>();
            foreach (var obj in allObjects)
            {
                if (obj.GetInstanceID() == instanceID)
                    return obj;
            }
            return null;
#endif
        }
    }
}
