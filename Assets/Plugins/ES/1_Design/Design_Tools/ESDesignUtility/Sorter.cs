using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ES.EnumCollect;

namespace ES
{

    public static partial class ESDesignUtility
    {
        //排序器
        public static class Sorter
        {
            /// <summary>
            /// 排序路径（List<V3>）
            /// </summary>
            /// <param name="vectors">全部路径点</param>
            /// <param name="sortType">排序类型</param>
            /// <param name="pos">开始点</param>
            /// <param name="transform">出发人</param>
            /// <returns></returns>
            public static List<Vector3> SortVectorPath(List<Vector3> vectors, EnumCollect.PathSort sortType, Vector3 pos = default, Transform transform = null)
            {
                if (vectors == null) return new List<Vector3>();
                if (vectors.Count <= 1) return vectors;

                switch (sortType)
                {
                    case PathSort.NoneSort: return vectors;
                    case PathSort.StartFromNearToFar:
                        return vectors.OrderBy((n) => (pos - n).sqrMagnitude).ToList();
                    case PathSort.StartFromFarToNear:
                        return vectors.OrderByDescending((n) => (pos - n).sqrMagnitude).ToList();
                    case PathSort.Yup:
                        return vectors.OrderBy((n) => n.y).ToList();
                    case PathSort.Ydown:
                        return vectors.OrderByDescending((n) => n.y).ToList();
                    case PathSort.Xup:
                        return vectors.OrderBy((n) => n.x).ToList();
                    case PathSort.Xdown:
                        return vectors.OrderByDescending((n) => n.x).ToList();
                    case PathSort.Zup:
                        return vectors.OrderBy((n) => n.z).ToList();
                    case PathSort.Zdown:
                        return vectors.OrderByDescending((n) => n.z).ToList();
                    case PathSort.StartForwardZup:
                        if (transform != null)
                            return vectors.OrderBy((n) => transform.InverseTransformPoint(n).z).ToList();
                        else return vectors.OrderBy((n) => n.z).ToList();
                    case PathSort.StartForwardZdown:
                        if (transform != null)
                            return vectors.OrderByDescending((n) => transform.InverseTransformPoint(n).z).ToList();
                        else return vectors.OrderByDescending((n) => n.z).ToList();
                    case PathSort.Random:
                        {
                            var shuffled = new List<Vector3>(vectors);
                            ShuffleInPlace(shuffled);
                            return shuffled;
                        }
                    case PathSort.AlwaysFirstNear:
                        return SortVectorPathForLast_Nearest(vectors, pos);
                    case PathSort.AlwaysFirstFar:
                        return SortForLast(vectors, pos, (a, b) => -(a - b).sqrMagnitude);
                    case PathSort.AlwaysForwardZup:
                        return SortForLast_Three(vectors, pos, (a, b, c) =>
                        {
                            if (b != c)
                            {
                                return Vector3.Angle(a - b, b - c);
                            }
                            return (a - b).sqrMagnitude;

                        });
                    case PathSort.AlwaysForwardZdown:
                        return SortForLast_Three(vectors, pos, (a, b, c) =>
                        {
                            if (b != c)
                            {
                                return -Vector3.Angle(a - b, b - c);
                            }
                            return (a - b).sqrMagnitude;

                        });
                }
                return vectors;
            }
            /// <summary>
            /// 排序路径（List<use-> V3>）
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="vectorUsers">全部路径点持有者</param>
            /// <param name="GetPos">持有者->点</param>
            /// <param name="sortType">排序类型</param>
            /// <param name="pos">开始点</param>
            /// <param name="transform">出发人</param>
            /// <returns></returns>
            public static List<T> SortVectorPathFromUser<T>(List<T> vectorUsers, Func<T, Vector3> GetPos, EnumCollect.PathSort sortType, Vector3 pos = default, Transform transform = null)
            {
                if (vectorUsers == null) return new List<T>();
                if (vectorUsers.Count <= 1) return vectorUsers;
                if (GetPos == null) return vectorUsers;

                switch (sortType)
                {
                    case PathSort.NoneSort: return vectorUsers;
                    case PathSort.StartFromNearToFar:
                        return vectorUsers.OrderBy((n) => (pos - GetPos(n)).sqrMagnitude).ToList();
                    case PathSort.StartFromFarToNear:
                        return vectorUsers.OrderByDescending((n) => (pos - GetPos(n)).sqrMagnitude).ToList();
                    case PathSort.Yup:
                        return vectorUsers.OrderBy((n) => GetPos(n).y).ToList();
                    case PathSort.Ydown:
                        return vectorUsers.OrderByDescending((n) => GetPos(n).y).ToList();
                    case PathSort.Xup:
                        return vectorUsers.OrderBy((n) => GetPos(n).x).ToList();
                    case PathSort.Xdown:
                        return vectorUsers.OrderByDescending((n) => GetPos(n).x).ToList();
                    case PathSort.Zup:
                        return vectorUsers.OrderBy((n) => GetPos(n).z).ToList();
                    case PathSort.Zdown:
                        return vectorUsers.OrderByDescending((n) => GetPos(n).z).ToList();
                    case PathSort.StartForwardZup:
                        if (transform != null)
                            return vectorUsers.OrderBy((n) => transform.InverseTransformPoint(GetPos(n)).z).ToList();
                        else return vectorUsers.OrderBy((n) => GetPos(n).z).ToList();
                    case PathSort.StartForwardZdown:
                        if (transform != null)
                            return vectorUsers.OrderByDescending((n) => transform.InverseTransformPoint(GetPos(n)).z).ToList();
                        else return vectorUsers.OrderByDescending((n) => GetPos(n).z).ToList();
                    case PathSort.Random:
                        {
                            var shuffled = new List<T>(vectorUsers);
                            ShuffleInPlace(shuffled);
                            return shuffled;
                        }

                }
                return vectorUsers;
            }

            private static void ShuffleInPlace<T>(IList<T> list)
            {
                if (list == null || list.Count <= 1) return;

                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);

                    T tmp = list[i];
                    list[i] = list[j];
                    list[j] = tmp;
                }
            }
            /// <summary>
            /// 排序路径点-每次找到离当前最近的点
            /// </summary>
            /// <param name="vectors"></param>
            /// <param name="pos">开始点</param>
            /// <returns></returns>
            public static List<Vector3> SortVectorPathForLast_Nearest(List<Vector3> vectors, Vector3 pos)
            {
                List<Vector3> reSort = new List<Vector3>(vectors);

                for (int i = 0; i < vectors.Count; i++)
                {
                    Vector3 last = i == 0 ? pos : reSort[i - 1];

                    float dis = float.PositiveInfinity;
                    int minIndex = i;
                    for (int j = i; j < vectors.Count; j++)
                    {
                        float disN;
                        if ((disN = (reSort[j] - last).sqrMagnitude) < dis)
                        {
                            minIndex = j;
                            dis = disN;
                        }
                    }
                    Vector3 cache = reSort[i];
                    reSort[i] = reSort[minIndex];
                    reSort[minIndex] = cache;
                }
                return reSort;
            }
            /// <summary>
            /// 排序T-每次按照特定机制返回最小 T1 当前判据 T2 上一个确定点
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="ts"></param>
            /// <param name="start">开始状态</param>
            /// <param name="func">判据获取</param>
            /// <returns>排序后(New)</returns>
            public static List<T> SortForLast<T>(List<T> ts, T start, Func<T, T, float> func)
            {
                List<T> reSort = new List<T>(ts);

                for (int i = 0; i < ts.Count; i++)
                {
                    T last = i == 0 ? start : reSort[i - 1];

                    float dis = float.PositiveInfinity;
                    int minIndex = i;
                    for (int j = i; j < ts.Count; j++)
                    {
                        float disN;
                        if ((disN = func.Invoke(reSort[j], last)) < dis)
                        {
                            minIndex = j;
                            dis = disN;
                        }
                    }
                    T cache = reSort[i];
                    reSort[i] = reSort[minIndex];
                    reSort[minIndex] = cache;
                }
                return reSort;
            }
            /// <summary>
            /// 排序T-每次按照特定机制返回最小 T1 当前判据 T2 上一个确定点 T3 上上1个确定点
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="ts"></param>
            /// <param name="start">开始状态</param>
            /// <param name="func">判据获取</param>
            /// <returns>排序后(New)</returns>
            public static List<T> SortForLast_Three<T>(List<T> ts, T start, Func<T, T, T, float> func)
            {
                List<T> reSort = new List<T>(ts);

                for (int i = 0; i < ts.Count; i++)
                {
                    T last = i == 0 ? start : reSort[i - 1];
                    T lastLast = i < 2 ? start : reSort[i - 2];
                    float dis = float.PositiveInfinity;
                    int minIndex = i;
                    for (int j = i; j < ts.Count; j++)
                    {
                        float disN;
                        if ((disN = func.Invoke(reSort[j], last, lastLast)) < dis)
                        {
                            minIndex = j;
                            dis = disN;
                        }
                    }
                    T cache = reSort[i];
                    reSort[i] = reSort[minIndex];
                    reSort[minIndex] = cache;
                }
                return reSort;
            }
        }
    }
}

