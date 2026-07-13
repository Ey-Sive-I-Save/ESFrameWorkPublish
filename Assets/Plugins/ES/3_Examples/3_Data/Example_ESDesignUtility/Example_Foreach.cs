using UnityEngine;
using ES;
using System.Collections.Generic;
using System.Linq;

namespace ES.Examples
{
    /// <summary>
    /// Foreach API 演示 - Transform查找与遍历工具
    /// 提供按名称/标签/层级查找、批量操作等功能
    /// </summary>
    public class Example_Foreach : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("=== Foreach API 演示 ===");

            // 创建测试层级结构
            GameObject root = new GameObject("Root");
            GameObject child1 = new GameObject("Child1");
            GameObject child2 = new GameObject("Child2");
            GameObject grandChild = new GameObject("GrandChild");

            child1.transform.SetParent(root.transform);
            child2.transform.SetParent(root.transform);
            grandChild.transform.SetParent(child1.transform);

            // 设置标签和层级
            child1.tag = "Player";
            child2.gameObject.layer = LayerMask.NameToLayer("UI");

            // ========== 按名称查找 ==========
            Debug.Log("--- 按名称查找 ---");

            // 1. 查找子节点（不包括自己）
            Transform found1 = ESDesignUtility.Foreach.FindChildByName(root.transform, "Child1", includeSelf: false);
            if (found1 != null)
            {
                Debug.Log($"找到子节点: {found1.name}");
            }

            // 2. 查找子节点（包括自己）
            Transform found2 = ESDesignUtility.Foreach.FindChildByName(root.transform, "Root", includeSelf: true);
            if (found2 != null)
            {
                Debug.Log($"找到自己: {found2.name}");
            }

            // 3. 查找深层节点
            Transform foundGrand = ESDesignUtility.Foreach.FindChildByName(root.transform, "GrandChild", includeSelf: false);
            if (foundGrand != null)
            {
                Debug.Log($"找到孙节点: {foundGrand.name}");
            }

            // 4. 查找所有匹配名称的节点
            List<Transform> results = ESDesignUtility.Foreach.FindAllChildrenByName(
                root.transform, 
                "Child", 
                includeSelf: false
            );
            Debug.Log($"找到 {results.Count} 个包含'Child'的节点");

            // ========== 按标签查找 ==========
            Debug.Log("--- 按标签查找 ---");

            // 5. 查找第一个匹配标签的节点
            Transform foundByTag = ESDesignUtility.Foreach.FindChildByTag(root.transform, "Player", includeSelf: false);
            if (foundByTag != null)
            {
                Debug.Log($"找到Player标签: {foundByTag.name}");
            }

            // 6. 查找所有匹配标签的节点
            List<Transform> tagResults = ESDesignUtility.Foreach.FindAllChildrenByTag(
                root.transform, 
                "Player", 
                includeSelf: false
            );
            Debug.Log($"找到 {tagResults.Count} 个Player标签节点");

            // ========== 按层级查找 ==========
            Debug.Log("--- 按层级查找 ---");

            // 7. 查找第一个匹配层级的节点
            int uiLayer = LayerMask.NameToLayer("UI");
            Transform foundByLayer = ESDesignUtility.Foreach.FindChildByLayer(root.transform, uiLayer, includeSelf: false);
            if (foundByLayer != null)
            {
                Debug.Log($"找到UI层级: {foundByLayer.name}");
            }

            // 8. 使用层级掩码查找（使用 FindChildInLayerMask）
            LayerMask layerMask = 1 << uiLayer;
            Transform foundByMask = ESDesignUtility.Foreach.FindChildInLayerMask(
                root.transform, 
                layerMask, 
                includeSelf: false
            );
            if (foundByMask != null)
            {
                Debug.Log($"通过掩码找到: {foundByMask.name}");
            }

            // ========== 按组件类型查找 ==========
            Debug.Log("--- 按组件类型查找 ---");

            // 添加组件
            child1.AddComponent<BoxCollider>();
            grandChild.AddComponent<SphereCollider>();

            // 9. 查找第一个带特定组件的节点
            Transform foundWithComponent = ESDesignUtility.Foreach.FindChildWithComponent<Collider>(root.transform, includeSelf: false);
            if (foundWithComponent != null)
            {
                Debug.Log($"找到带Collider的节点: {foundWithComponent.name}");
            }

            // 10. 查找所有带特定组件的节点
            List<Transform> componentResults = ESDesignUtility.Foreach.FindAllChildrenWithComponent<Collider>(
                root.transform, 
                includeSelf: false
            );
            Debug.Log($"找到 {componentResults.Count} 个带Collider的节点");

            // ========== 自定义条件查找 ==========
            Debug.Log("--- 自定义条件查找 ---");

            // 11. 使用谓词查找（名称长度>5的节点）
            Transform foundByPredicate = ESDesignUtility.Foreach.FindChildWhere(
                root.transform,
                condition: (t) => t.name.Length > 5,
                includeSelf: false
            );
            if (foundByPredicate != null)
            {
                Debug.Log($"找到名称长度>5的节点: {foundByPredicate.name}");
            }

            // 12. 查找所有激活的节点
            List<Transform> activeResults = ESDesignUtility.Foreach.FindAllChildrenWhere(
                root.transform,
                condition: (t) => t.gameObject.activeSelf,
                includeSelf: false
            );
            Debug.Log($"找到 {activeResults.Count} 个激活的节点");

            // ========== 获取所有子节点 ==========
            Debug.Log("--- 获取所有子节点 ---");

            // 13. 获取所有子节点（不递归）
            List<Transform> directChildren = ESDesignUtility.Foreach.GetAllChildren(
                root.transform, 
                includeSelf: false
            );
            Debug.Log($"直接子节点数: {directChildren.Count}");

            // 14. 获取所有子节点（递归）- 注：此API默认已是递归的
            List<Transform> allChildren = ESDesignUtility.Foreach.GetAllChildren(
                root.transform, 
                includeSelf: false
            );
            Debug.Log($"所有子孙节点数: {allChildren.Count}");

            // ========== GameObject查找 ==========
            Debug.Log("--- GameObject查找 ---");

            // 15. 查找GameObject（通过 Transform 查找再访问 gameObject）
            Transform foundTransform = ESDesignUtility.Foreach.FindChildByName(root.transform, "Child2");
            if (foundTransform != null)
            {
                GameObject foundGO = foundTransform.gameObject;
                Debug.Log($"找到GameObject: {foundGO.name}");
            }

            // 16. 查找所有GameObject（通过 Transform 列表转换）
            List<Transform> childTransforms = ESDesignUtility.Foreach.FindAllChildrenByName(
                root.transform, 
                "Child"
            );
            List<GameObject> allGOs = childTransforms.Select(t => t.gameObject).ToList();
            Debug.Log($"找到 {allGOs.Count} 个GameObject");

            // 清理
            Destroy(root);
        }
    }
}
