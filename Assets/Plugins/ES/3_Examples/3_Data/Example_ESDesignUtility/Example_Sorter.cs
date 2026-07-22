using UnityEngine;
using ES;
using System.Collections.Generic;

namespace ES.Samples{
    /// <summary>
    /// Sorter API 婕旂ず - 鎺掑簭宸ュ叿
    /// 鎻愪緵Vector3璺緞鎺掑簭銆佽嚜瀹氫箟瀵硅薄鎺掑簭绛夊姛鑳?
    /// </summary>
    public class Example_Sorter : MonoBehaviour
    {
        public class Waypoint
        {
            public string name;
            public Vector3 position;

            public Waypoint(string name, Vector3 position)
            {
                this.name = name;
                this.position = position;
            }
        }

        private void Start()
        {
            Debug.Log("=== Sorter API 婕旂ず ===");

            // 鍑嗗娴嬭瘯鏁版嵁
            List<Vector3> points = new List<Vector3>
            {
                new Vector3(10, 0, 5),
                new Vector3(2, 10, 3),
                new Vector3(5, 5, 8),
                new Vector3(0, 2, 1),
                new Vector3(8, 3, 2)
            };

            Vector3 startPos = Vector3.zero;

            // 1. 鎸夎窛绂讳粠杩戝埌杩滄帓搴?
            List<Vector3> nearToFar = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.StartFromNearToFar,
                pos: startPos
            );
            Debug.Log($"浠庤繎鍒拌繙: {string.Join(", ", nearToFar)}");

            // 2. 鎸夎窛绂讳粠杩滃埌杩戞帓搴?
            List<Vector3> farToNear = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.StartFromFarToNear,
                pos: startPos
            );
            Debug.Log($"浠庤繙鍒拌繎: 绗竴涓偣={farToNear[0]}");

            // 3. 鎸塝杞村崌搴忔帓搴?
            List<Vector3> yUp = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.Yup
            );
            Debug.Log($"Y杞村崌搴? 鏈€浣庣偣={yUp[0].y}, 鏈€楂樼偣={yUp[yUp.Count - 1].y}");

            // 4. 鎸塜杞撮檷搴忔帓搴?
            List<Vector3> xDown = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.Xdown
            );
            Debug.Log($"X杞撮檷搴? {string.Join(", ", xDown)}");

            // 5. 闅忔満鎺掑簭
            List<Vector3> random = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.Random
            );
            Debug.Log($"闅忔満鎺掑簭: {string.Join(", ", random)}");

            // 6. 鎬绘槸閫夋嫨鏈€杩戠殑鐐癸紙璐績鏈€鐭矾寰勶級
            List<Vector3> alwaysNear = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.AlwaysFirstNear,
                pos: startPos
            );
            Debug.Log($"璐績鏈€杩? 绗竴涓偣={alwaysNear[0]}, 绗簩涓偣={alwaysNear[1]}");

            // 7. 浣跨敤Transform鐨刦orward鏂瑰悜鎺掑簭
            GameObject obj = new GameObject("SorterObject");
            obj.transform.position = Vector3.zero;
            obj.transform.rotation = Quaternion.Euler(0, 45, 0);

            List<Vector3> forwardSort = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.StartForwardZup,
                pos: startPos,
                transform: obj.transform
            );
            Debug.Log($"鎸塮orward鏂瑰悜鎺掑簭瀹屾垚");

            // 8. 鑷畾涔夊璞℃帓搴忥紙浣跨敤SortVectorPathFromUser锛?
            List<Waypoint> waypoints = new List<Waypoint>
            {
                new Waypoint("A", new Vector3(5, 0, 0)),
                new Waypoint("B", new Vector3(1, 0, 0)),
                new Waypoint("C", new Vector3(3, 0, 0)),
                new Waypoint("D", new Vector3(10, 0, 0))
            };

            List<Waypoint> sortedWaypoints = ESDesignUtility.Sorter.SortVectorPathFromUser(
                vectorUsers: waypoints,
                GetPos: (wp) => wp.position,
                sortType: EnumCollect.PathSort.StartFromNearToFar,
                pos: Vector3.zero
            );

            Debug.Log("鑷畾涔夊璞℃帓搴忕粨鏋?");
            foreach (var wp in sortedWaypoints)
            {
                Debug.Log($"  {wp.name}: {wp.position}");
            }

            // 9. 涓嶆帓搴忥紙淇濇寔鍘熷簭锛?
            List<Vector3> noSort = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.NoneSort
            );
            Debug.Log($"涓嶆帓搴? 鏁伴噺={noSort.Count}");

            Destroy(obj);
        }
    }
}

