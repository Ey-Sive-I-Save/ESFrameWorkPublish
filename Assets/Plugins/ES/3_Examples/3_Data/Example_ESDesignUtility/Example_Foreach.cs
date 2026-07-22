using UnityEngine;
using ES;
using System.Collections.Generic;
using System.Linq;

namespace ES.Samples{
    /// <summary>
    /// Foreach API 婕旂ず - Transform鏌ユ壘涓庨亶鍘嗗伐鍏?
    /// 鎻愪緵鎸夊悕绉?鏍囩/灞傜骇鏌ユ壘銆佹壒閲忔搷浣滅瓑鍔熻兘
    /// </summary>
    public class Example_Foreach : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("=== Foreach API 婕旂ず ===");

            // 鍒涘缓娴嬭瘯灞傜骇缁撴瀯
            GameObject root = new GameObject("Root");
            GameObject child1 = new GameObject("Child1");
            GameObject child2 = new GameObject("Child2");
            GameObject grandChild = new GameObject("GrandChild");

            child1.transform.SetParent(root.transform);
            child2.transform.SetParent(root.transform);
            grandChild.transform.SetParent(child1.transform);

            // 璁剧疆鏍囩鍜屽眰绾?
            child1.tag = "Player";
            child2.gameObject.layer = LayerMask.NameToLayer("UI");

            // ========== 鎸夊悕绉版煡鎵?==========
            Debug.Log("--- 鎸夊悕绉版煡鎵?---");

            // 1. 鏌ユ壘瀛愯妭鐐癸紙涓嶅寘鎷嚜宸憋級
            Transform found1 = ESDesignUtility.Foreach.FindChildByName(root.transform, "Child1", includeSelf: false);
            if (found1 != null)
            {
                Debug.Log($"鎵惧埌瀛愯妭鐐? {found1.name}");
            }

            // 2. 鏌ユ壘瀛愯妭鐐癸紙鍖呮嫭鑷繁锛?
            Transform found2 = ESDesignUtility.Foreach.FindChildByName(root.transform, "Root", includeSelf: true);
            if (found2 != null)
            {
                Debug.Log($"鎵惧埌鑷繁: {found2.name}");
            }

            // 3. 鏌ユ壘娣卞眰鑺傜偣
            Transform foundGrand = ESDesignUtility.Foreach.FindChildByName(root.transform, "GrandChild", includeSelf: false);
            if (foundGrand != null)
            {
                Debug.Log($"鎵惧埌瀛欒妭鐐? {foundGrand.name}");
            }

            // 4. 鏌ユ壘鎵€鏈夊尮閰嶅悕绉扮殑鑺傜偣
            List<Transform> results = ESDesignUtility.Foreach.FindAllChildrenByName(
                root.transform, 
                "Child", 
                includeSelf: false
            );
            Debug.Log($"鎵惧埌 {results.Count} 涓寘鍚?Child'鐨勮妭鐐?);

            // ========== 鎸夋爣绛炬煡鎵?==========
            Debug.Log("--- 鎸夋爣绛炬煡鎵?---");

            // 5. 鏌ユ壘绗竴涓尮閰嶆爣绛剧殑鑺傜偣
            Transform foundByTag = ESDesignUtility.Foreach.FindChildByTag(root.transform, "Player", includeSelf: false);
            if (foundByTag != null)
            {
                Debug.Log($"鎵惧埌Player鏍囩: {foundByTag.name}");
            }

            // 6. 鏌ユ壘鎵€鏈夊尮閰嶆爣绛剧殑鑺傜偣
            List<Transform> tagResults = ESDesignUtility.Foreach.FindAllChildrenByTag(
                root.transform, 
                "Player", 
                includeSelf: false
            );
            Debug.Log($"鎵惧埌 {tagResults.Count} 涓狿layer鏍囩鑺傜偣");

            // ========== 鎸夊眰绾ф煡鎵?==========
            Debug.Log("--- 鎸夊眰绾ф煡鎵?---");

            // 7. 鏌ユ壘绗竴涓尮閰嶅眰绾х殑鑺傜偣
            int uiLayer = LayerMask.NameToLayer("UI");
            Transform foundByLayer = ESDesignUtility.Foreach.FindChildByLayer(root.transform, uiLayer, includeSelf: false);
            if (foundByLayer != null)
            {
                Debug.Log($"鎵惧埌UI灞傜骇: {foundByLayer.name}");
            }

            // 8. 浣跨敤灞傜骇鎺╃爜鏌ユ壘锛堜娇鐢?FindChildInLayerMask锛?
            LayerMask layerMask = 1 << uiLayer;
            Transform foundByMask = ESDesignUtility.Foreach.FindChildInLayerMask(
                root.transform, 
                layerMask, 
                includeSelf: false
            );
            if (foundByMask != null)
            {
                Debug.Log($"閫氳繃鎺╃爜鎵惧埌: {foundByMask.name}");
            }

            // ========== 鎸夌粍浠剁被鍨嬫煡鎵?==========
            Debug.Log("--- 鎸夌粍浠剁被鍨嬫煡鎵?---");

            // 娣诲姞缁勪欢
            child1.AddComponent<BoxCollider>();
            grandChild.AddComponent<SphereCollider>();

            // 9. 鏌ユ壘绗竴涓甫鐗瑰畾缁勪欢鐨勮妭鐐?
            Transform foundWithComponent = ESDesignUtility.Foreach.FindChildWithComponent<Collider>(root.transform, includeSelf: false);
            if (foundWithComponent != null)
            {
                Debug.Log($"鎵惧埌甯ollider鐨勮妭鐐? {foundWithComponent.name}");
            }

            // 10. 鏌ユ壘鎵€鏈夊甫鐗瑰畾缁勪欢鐨勮妭鐐?
            List<Transform> componentResults = ESDesignUtility.Foreach.FindAllChildrenWithComponent<Collider>(
                root.transform, 
                includeSelf: false
            );
            Debug.Log($"鎵惧埌 {componentResults.Count} 涓甫Collider鐨勮妭鐐?);

            // ========== 鑷畾涔夋潯浠舵煡鎵?==========
            Debug.Log("--- 鑷畾涔夋潯浠舵煡鎵?---");

            // 11. 浣跨敤璋撹瘝鏌ユ壘锛堝悕绉伴暱搴?5鐨勮妭鐐癸級
            Transform foundByPredicate = ESDesignUtility.Foreach.FindChildWhere(
                root.transform,
                condition: (t) => t.name.Length > 5,
                includeSelf: false
            );
            if (foundByPredicate != null)
            {
                Debug.Log($"鎵惧埌鍚嶇О闀垮害>5鐨勮妭鐐? {foundByPredicate.name}");
            }

            // 12. 鏌ユ壘鎵€鏈夋縺娲荤殑鑺傜偣
            List<Transform> activeResults = ESDesignUtility.Foreach.FindAllChildrenWhere(
                root.transform,
                condition: (t) => t.gameObject.activeSelf,
                includeSelf: false
            );
            Debug.Log($"鎵惧埌 {activeResults.Count} 涓縺娲荤殑鑺傜偣");

            // ========== 鑾峰彇鎵€鏈夊瓙鑺傜偣 ==========
            Debug.Log("--- 鑾峰彇鎵€鏈夊瓙鑺傜偣 ---");

            // 13. 鑾峰彇鎵€鏈夊瓙鑺傜偣锛堜笉閫掑綊锛?
            List<Transform> directChildren = ESDesignUtility.Foreach.GetAllChildren(
                root.transform, 
                includeSelf: false
            );
            Debug.Log($"鐩存帴瀛愯妭鐐规暟: {directChildren.Count}");

            // 14. 鑾峰彇鎵€鏈夊瓙鑺傜偣锛堥€掑綊锛? 娉細姝PI榛樿宸叉槸閫掑綊鐨?
            List<Transform> allChildren = ESDesignUtility.Foreach.GetAllChildren(
                root.transform, 
                includeSelf: false
            );
            Debug.Log($"鎵€鏈夊瓙瀛欒妭鐐规暟: {allChildren.Count}");

            // ========== GameObject鏌ユ壘 ==========
            Debug.Log("--- GameObject鏌ユ壘 ---");

            // 15. 鏌ユ壘GameObject锛堥€氳繃 Transform 鏌ユ壘鍐嶈闂?gameObject锛?
            Transform foundTransform = ESDesignUtility.Foreach.FindChildByName(root.transform, "Child2");
            if (foundTransform != null)
            {
                GameObject foundGO = foundTransform.gameObject;
                Debug.Log($"鎵惧埌GameObject: {foundGO.name}");
            }

            // 16. 鏌ユ壘鎵€鏈塆ameObject锛堥€氳繃 Transform 鍒楄〃杞崲锛?
            List<Transform> childTransforms = ESDesignUtility.Foreach.FindAllChildrenByName(
                root.transform, 
                "Child"
            );
            List<GameObject> allGOs = childTransforms.Select(t => t.gameObject).ToList();
            Debug.Log($"鎵惧埌 {allGOs.Count} 涓狦ameObject");

            // 娓呯悊
            Destroy(root);
        }
    }
}

