using UnityEngine;
using ES;
using System.Collections.Generic;

namespace ES.Samples{
    /// <summary>
    /// TransformSetter API 婕旂ず - Transform鎿嶄綔宸ュ叿
    /// 鎻愪緵鐖剁骇璁剧疆銆佹壒閲忔搷浣溿€佷綅缃棆杞缉鏀惧垵濮嬪寲绛夊姛鑳?
    /// </summary>
    public class Example_TransformSetter : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("=== TransformSetter API 婕旂ず ===");

            // 1. 鍒涘缓娴嬭瘯瀵硅薄
            GameObject parent = new GameObject("ParentObject");
            GameObject child1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child1.name = "Child1";
            GameObject child2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            child2.name = "Child2";

            // 2. 浠呰缃埗绾э紙淇濈暀褰撳墠浣嶇疆锛?
            child1.transform.position = new Vector3(5, 5, 5);
            ESDesignUtility.TransformSetter.HandleTransformAtParent(
                me: child1.transform,
                parent: parent.transform,
                localRot0: true,    // 閲嶇疆鏃嬭浆
                localScale0: true   // 閲嶇疆缂╂斁
            );
            Debug.Log($"Child1 涓栫晫浣嶇疆淇濈暀: {child1.transform.position}");

            // 3. 璁剧疆鐖剁骇骞舵寚瀹氫綅缃紙涓栫晫鍧愭爣锛?
            ESDesignUtility.TransformSetter.HandleTransformAtParent(
                me: child2.transform,
                parent: parent.transform,
                pos: new Vector3(0, 2, 0),  // 鎸囧畾涓栫晫浣嶇疆
                atWorldPos: true,
                localRot0: true,
                localScale0: true
            );
            Debug.Log($"Child2 璁剧疆涓栫晫浣嶇疆: {child2.transform.position}");

            // 4. 璁剧疆鐖剁骇骞舵寚瀹氭湰鍦颁綅缃?
            GameObject child3 = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            child3.name = "Child3";
            ESDesignUtility.TransformSetter.HandleTransformAtParent(
                me: child3.transform,
                parent: parent.transform,
                pos: new Vector3(1, 0, 0),  // 鏈湴浣嶇疆
                atWorldPos: false,
                localRot0: true,
                localScale0: false  // 涓嶉噸缃缉鏀?
            );
            Debug.Log($"Child3 鏈湴浣嶇疆: {child3.transform.localPosition}");

            // 5. pos涓簄ull鏃朵笉淇敼浣嶇疆
            GameObject child4 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            child4.name = "Child4";
            child4.transform.position = new Vector3(10, 10, 10);
            ESDesignUtility.TransformSetter.HandleTransformAtParent(
                me: child4.transform,
                parent: parent.transform,
                pos: null,  // 涓嶄慨鏀逛綅缃?
                atWorldPos: true,
                localRot0: false,  // 涓嶉噸缃棆杞?
                localScale0: false // 涓嶉噸缃缉鏀?
            );
            Debug.Log($"Child4 浣嶇疆鏈敼鍙? {child4.transform.position}");

            // 6. 鎵归噺鎿嶄綔澶氫釜Transform
            List<Transform> children = new List<Transform>
            {
                child1.transform,
                child2.transform,
                child3.transform
            };

            GameObject batchParent = new GameObject("BatchParent");
            ESDesignUtility.TransformSetter.HandleTransformsAtParent(
                transforms: children,
                parent: batchParent.transform,
                pos: Vector3.zero,
                atWorldPos: false,
                localRot0: true,
                localScale0: true
            );
            Debug.Log("鎵归噺澶勭悊瀹屾垚锛?涓瓙瀵硅薄宸茬Щ鍔ㄥ埌BatchParent");

            // 7. 鍏稿瀷搴旂敤鍦烘櫙锛歎I鍏冪礌鍒濆鍖?
            GameObject canvas = new GameObject("Canvas");
            GameObject uiPanel = new GameObject("UIPanel");
            
            ESDesignUtility.TransformSetter.HandleTransformAtParent(
                me: uiPanel.transform,
                parent: canvas.transform,
                pos: Vector3.zero,
                atWorldPos: false,  // UI浣跨敤鏈湴鍧愭爣
                localRot0: true,
                localScale0: true
            );
            Debug.Log("UI Panel 宸插垵濮嬪寲鍒癈anvas涓?);

            // 8. 瀵硅薄姹犲洖鏀剁ず渚?
            GameObject pooledObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pooledObj.transform.position = new Vector3(100, 200, 300);
            pooledObj.transform.rotation = Quaternion.Euler(45, 90, 0);
            pooledObj.transform.localScale = new Vector3(2, 2, 2);

            GameObject poolRoot = new GameObject("PoolRoot");
            ESDesignUtility.TransformSetter.HandleTransformAtParent(
                me: pooledObj.transform,
                parent: poolRoot.transform,
                pos: Vector3.zero,
                atWorldPos: false,
                localRot0: true,   // 閲嶇疆鏃嬭浆
                localScale0: true  // 閲嶇疆缂╂斁
            );
            Debug.Log("瀵硅薄姹犲璞″凡閲嶇疆骞跺洖鏀?);
        }
    }
}

