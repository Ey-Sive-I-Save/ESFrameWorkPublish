using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    public class GameObjectRefer : SerializedMonoBehaviour
    {
        [LabelText("游戏对象划分(key可忽略)")]
        [SerializeReference]
        [Searchable]
        [ListDrawerSettings(
            DraggableItems = true,
            DefaultExpandedState = true,
            ShowIndexLabels = false

        )]
        public List<GameObjectTreeItem> children = new List<GameObjectTreeItem>();

        private MultiKeyDictionary<string, GameObject> runtimeDict;
        private bool isBuilt = false;


        #region 运行时 API（需先 Build）

        /// <summary> 构建运行时字典（在 Awake 或手动调用）</summary>
        public void Build()
        {
            runtimeDict = new MultiKeyDictionary<string, GameObject>();
            if (children != null)
            {
                foreach (var item in children)
                    item?.BuildDictionary(runtimeDict, "");
            }
            isBuilt = true;
        }

        /// <summary> 检查是否已构建 </summary>
        public bool IsBuilt => isBuilt;

        /// <summary> 通过路径获取 GameObject（运行时）</summary>
        public GameObject GetGameObject(string path)
        {
            if (!isBuilt) Build();
            return runtimeDict.TryGetValue(path, out var go) ? go : null;
        }

        /// <summary> 尝试获取（运行时）</summary>
        public bool TryGetGameObject(string path, out GameObject go)
        {
            if (!isBuilt) Build();
            return runtimeDict.TryGetValue(path, out go);
        }

        /// <summary> 判断路径是否存在（运行时）</summary>
        public bool ContainsKey(string path)
        {
            if (!isBuilt) Build();
            return runtimeDict.ContainsKey(path);
        }

        /// <summary> 通过 GameObject 获取所有路径（运行时）</summary>
        public string[] GetPathsByGameObject(GameObject go)
        {
            if (!isBuilt) Build();
            return runtimeDict.GetKeysByValue(go);
        }

        /// <summary> 获取第一个路径（运行时）</summary>
        public string GetFirstPathByGameObject(GameObject go)
        {
            if (!isBuilt) Build();
            return runtimeDict.GetFirstKeyByValue(go);
        }

        /// <summary> 获取所有路径（运行时）</summary>
        public IEnumerable<string> GetAllPaths()
        {
            if (!isBuilt) Build();
            return runtimeDict.Keys;
        }
        /// <summary> 重新构建（当树结构发生动态变化后调用）</summary>
        public void Rebuild()
        {
            runtimeDict = null;
            isBuilt = false;
            Build();

        }

        #endregion

        #region 编辑器 API（实时查询，无需 Build，适合编辑器工具）

        /// <summary> 实时通过路径查找（遍历树结构，低性能）</summary>
        public GameObject GetGameObjectEditor(string path)
        {
            return FindGameObjectByFullPath(children, "", path);
        }
        [Button("测试Editor查询")]
        public void DebugGameObjectEditor(string path)
        {
            GameObject gameObject = GetGameObjectEditor(path);
            if (gameObject != null)
            {
                Debug.Log($"[GameObjectRefer] Editor query success | Path={path} | GameObject={gameObject.name}", gameObject);
                return;
            }

            List<string> paths = GetAllPathsEditor();
            Debug.LogWarning($"[GameObjectRefer] Editor query failed | Path={path} | AvailableCount={paths.Count}\n{string.Join("\n", paths)}", this);
        }

        /// <summary> 实时获取所有路径（遍历）</summary>
        public List<string> GetAllPathsEditor()
        {
            var paths = new List<string>();
            CollectPaths(children, "", paths);
            return paths;
        }

        /// <summary> 实时通过 GameObject 获取所有路径（遍历）</summary>
        public List<string> GetPathsByGameObjectEditor(GameObject go)
        {
            var result = new List<string>();
            FindPathsByGameObject(children, "", go, result);
            return result;
        }

        #endregion

        #region 内部辅助方法（编辑器实时查找）

        private GameObject FindGameObjectByFullPath(List<GameObjectTreeItem> items, string currentPath, string targetPath)
        {
            if (items == null || string.IsNullOrEmpty(targetPath))
                return null;

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                if (string.IsNullOrEmpty(item.itemName))
                    continue;

                string itemPath = string.IsNullOrEmpty(currentPath) ? item.itemName : $"{currentPath}/{item.itemName}";
                if (item is GameObjectLeaf leaf)
                {
                    if (itemPath == targetPath && leaf.gameObject != null)
                        return leaf.gameObject;

                    continue;
                }

                if (item is GameObjectGroup group)
                {
                    GameObject result = FindGameObjectByFullPath(group.children, itemPath, targetPath);
                    if (result != null)
                        return result;
                }
            }

            return null;
        }

        private GameObject FindGameObjectInTree(List<GameObjectTreeItem> items, string targetPath)
        {
            if (items == null || string.IsNullOrEmpty(targetPath))
                return null;

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                if (item is GameObjectGroup group)
                {
                    string groupPath = group.itemName;
                    if (IsDirectPathMatch(targetPath, groupPath))
                    {
                        string remaining = targetPath.Substring(groupPath.Length);
                        if (remaining.StartsWith("/")) remaining = remaining.Substring(1);
                        if (string.IsNullOrEmpty(remaining))
                            return null; // 路径刚好是分组名，没有对象
                        return FindGameObjectInTree(group.children, remaining);
                    }
                }
                else if (item is GameObjectLeaf leaf)
                {
                    string leafPath = leaf.itemName;
                    if (leafPath == targetPath)
                        return leaf.gameObject;
                }
            }
            return null;
        }

        private void CollectPaths(List<GameObjectTreeItem> items, string currentPath, List<string> outPaths)
        {
            if (items == null || outPaths == null)
                return;

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                if (string.IsNullOrEmpty(item.itemName))
                    continue;

                string myPath = string.IsNullOrEmpty(currentPath) ? item.itemName : $"{currentPath}/{item.itemName}";
                if (item is GameObjectLeaf leaf)
                {
                    if (leaf.gameObject == null)
                        continue;

                    outPaths.Add(myPath);
                }
                else if (item is GameObjectGroup group)
                {
                    CollectPaths(group.children, myPath, outPaths);
                }
            }
        }

        private void FindPathsByGameObject(List<GameObjectTreeItem> items, string currentPath, GameObject target, List<string> outPaths)
        {
            if (items == null || target == null || outPaths == null)
                return;

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                if (string.IsNullOrEmpty(item.itemName))
                    continue;

                string myPath = string.IsNullOrEmpty(currentPath) ? item.itemName : $"{currentPath}/{item.itemName}";
                if (item is GameObjectLeaf leaf && leaf.gameObject == target)
                {
                    outPaths.Add(myPath);
                }
                else if (item is GameObjectGroup group)
                {
                    FindPathsByGameObject(group.children, myPath, target, outPaths);
                }
            }
        }

        private static bool IsDirectPathMatch(string targetPath, string groupPath)
        {
            if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(groupPath))
                return false;

            return targetPath == groupPath || targetPath.StartsWith(groupPath + "/", StringComparison.Ordinal);
        }

        #endregion

        #region Unity 生命周期

        private void Awake()
        {
            Build();
        }

        #endregion



        #region 分组
        [Serializable]
        public abstract class GameObjectTreeItem
        {
            [LabelText("key名称")]
            public string itemName = "新节点";

            // 修改为 MultiKeyDictionary
            public abstract void BuildDictionary(MultiKeyDictionary<string, GameObject> dict, string currentPath);
            protected GameObjectTreeItem()
            {
                // 生成随机短后缀（8位）
                string suffix = Guid.NewGuid().ToString("N").Substring(0, 4);
                itemName = $"新节点_{suffix}";
            }
        }

        [Serializable, TypeRegistryItem("分组")]
        public class GameObjectGroup : GameObjectTreeItem
        {
            [LabelText("子节点")]
            [SerializeReference]
            public List<GameObjectTreeItem> children = new List<GameObjectTreeItem>();
            public GameObjectGroup()
            {
                // 生成随机短后缀（8位）
                string suffix = Guid.NewGuid().ToString("N").Substring(0, 4);
                itemName = $"新组_{suffix}";
            }
            public override void BuildDictionary(MultiKeyDictionary<string, GameObject> dict, string currentPath)
            {
                if (dict == null)
                    return;

                string myPath = string.IsNullOrEmpty(currentPath) ? itemName : $"{currentPath}/{itemName}";
                if (children == null)
                    return;

                foreach (var child in children)
                {
                    if (child == null)
                        continue;

                    child.BuildDictionary(dict, myPath);
                }
            }
            /// <summary>
            /// 尝试添加 GameObject 引用作为新的叶子节点
            /// </summary>
            /// <param name="go">目标 GameObject</param>
            /// <param name="key">指定键名（为空则使用 go.name）</param>
            /// <param name="preventDuplicateName">是否防止重名：true 时若已存在同名叶子则跳过；false 时自动添加 _N 后缀</param>
            /// <param name="skipDuplicateObject">是否跳过重复对象：true 时若同一 GameObject 已存在任何叶子则跳过</param>
            /// <returns>添加成功返回 true，否则 false</returns>
            public bool TryAddGameObject(GameObject go, string key = null,
      bool preventDuplicateName = false, bool skipDuplicateObject = false)
            {
                if (go == null) return false;

                string baseName = string.IsNullOrEmpty(key) ? go.name : key;

                // 检查重复对象（如果需要）
                if (skipDuplicateObject)
                {
                    for (int i = 0; i < children.Count; i++)
                    {
                        if (children[i] is GameObjectLeaf leaf && leaf.gameObject == go)
                        {
                            Debug.LogWarning($"尝试添加重复的 GameObject: {go.name}，已跳过。");
                            return false;
                        }
                    }
                }

                // 防止重名模式
                if (preventDuplicateName)
                {
                    for (int i = 0; i < children.Count; i++)
                    {
                        if (children[i] is GameObjectLeaf leaf && leaf.itemName == baseName)
                        {
                            Debug.LogWarning($"尝试添加重复的叶子节点名称: {baseName}，已跳过。");
                            return false;
                        }
                    }
                    children.Add(new GameObjectLeaf { itemName = baseName, gameObject = go });
                    return true;
                }
                else
                {
                    // 允许重名，自动添加 _N 后缀
                    string finalName = baseName;
                    int counter = 1;
                    while (true)
                    {
                        bool exists = false;
                        for (int i = 0; i < children.Count; i++)
                        {
                            if (children[i] is GameObjectLeaf leaf && leaf.itemName == finalName)
                            {
                                exists = true;
                                break;
                            }
                        }
                        if (!exists) break;
                        finalName = $"{baseName}_{counter++}";
                    }
                    children.Add(new GameObjectLeaf { itemName = finalName, gameObject = go });
                    return true;
                }
            }
        }

        [Serializable, TypeRegistryItem("GameObject引用")]
        public class GameObjectLeaf : GameObjectTreeItem
        {
            [LabelText("目标对象")]
            public GameObject gameObject;
            public GameObjectLeaf()
            {
                // 生成随机短后缀（8位）
                string suffix = Guid.NewGuid().ToString("N").Substring(0, 4);
                itemName = $"新对象_{suffix}";
            }
            public override void BuildDictionary(MultiKeyDictionary<string, GameObject> dict, string currentPath)
            {
                if (dict == null)
                    return;

                string fullPath = string.IsNullOrEmpty(currentPath) ? itemName : $"{currentPath}/{itemName}";

                if (string.IsNullOrEmpty(fullPath))
                {
                    Debug.LogWarning("GameObjectRefer 构建时发现空路径，已跳过。");
                    return;
                }

                if (gameObject == null)
                {
                    Debug.LogWarning($"GameObjectRefer 构建时发现空 GameObject 引用，路径: {fullPath}，已跳过。");
                    return;
                }

                if (dict.ContainsKey(fullPath))
                {
                    Debug.LogWarning($"GameObjectRefer 构建时发现重复路径: {fullPath}，后写入的 GameObject 将覆盖旧值。");
                }

                dict.Add(fullPath, gameObject);
            }
        }
        #endregion


    }
}
