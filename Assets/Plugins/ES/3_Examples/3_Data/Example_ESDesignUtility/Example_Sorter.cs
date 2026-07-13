using UnityEngine;
using ES;
using System.Collections.Generic;

namespace ES.Examples
{
    /// <summary>
    /// Sorter API 演示 - 排序工具
    /// 提供Vector3路径排序、自定义对象排序等功能
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
            Debug.Log("=== Sorter API 演示 ===");

            // 准备测试数据
            List<Vector3> points = new List<Vector3>
            {
                new Vector3(10, 0, 5),
                new Vector3(2, 10, 3),
                new Vector3(5, 5, 8),
                new Vector3(0, 2, 1),
                new Vector3(8, 3, 2)
            };

            Vector3 startPos = Vector3.zero;

            // 1. 按距离从近到远排序
            List<Vector3> nearToFar = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.StartFromNearToFar,
                pos: startPos
            );
            Debug.Log($"从近到远: {string.Join(", ", nearToFar)}");

            // 2. 按距离从远到近排序
            List<Vector3> farToNear = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.StartFromFarToNear,
                pos: startPos
            );
            Debug.Log($"从远到近: 第一个点={farToNear[0]}");

            // 3. 按Y轴升序排序
            List<Vector3> yUp = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.Yup
            );
            Debug.Log($"Y轴升序: 最低点={yUp[0].y}, 最高点={yUp[yUp.Count - 1].y}");

            // 4. 按X轴降序排序
            List<Vector3> xDown = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.Xdown
            );
            Debug.Log($"X轴降序: {string.Join(", ", xDown)}");

            // 5. 随机排序
            List<Vector3> random = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.Random
            );
            Debug.Log($"随机排序: {string.Join(", ", random)}");

            // 6. 总是选择最近的点（贪心最短路径）
            List<Vector3> alwaysNear = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.AlwaysFirstNear,
                pos: startPos
            );
            Debug.Log($"贪心最近: 第一个点={alwaysNear[0]}, 第二个点={alwaysNear[1]}");

            // 7. 使用Transform的forward方向排序
            GameObject obj = new GameObject("SorterObject");
            obj.transform.position = Vector3.zero;
            obj.transform.rotation = Quaternion.Euler(0, 45, 0);

            List<Vector3> forwardSort = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.StartForwardZup,
                pos: startPos,
                transform: obj.transform
            );
            Debug.Log($"按forward方向排序完成");

            // 8. 自定义对象排序（使用SortVectorPathFromUser）
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

            Debug.Log("自定义对象排序结果:");
            foreach (var wp in sortedWaypoints)
            {
                Debug.Log($"  {wp.name}: {wp.position}");
            }

            // 9. 不排序（保持原序）
            List<Vector3> noSort = ESDesignUtility.Sorter.SortVectorPath(
                vectors: points,
                sortType: EnumCollect.PathSort.NoneSort
            );
            Debug.Log($"不排序: 数量={noSort.Count}");

            Destroy(obj);
        }
    }
}
