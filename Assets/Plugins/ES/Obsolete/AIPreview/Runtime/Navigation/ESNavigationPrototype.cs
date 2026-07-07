using System.Collections.Generic;
using UnityEngine;

namespace ES.AIPreview.Navigation
{
    /// <summary>
    /// 节点寻路原型：
    /// - 使用简单的无权重图 + BFS 寻路；
    /// - 仅作为更高级（如四叉树/八叉树）结构的概念起点；
    /// - 不依赖现有项目代码，可安全放在 Preview 目录中。
    /// </summary>
    public class ESNavNode : MonoBehaviour
    {
        public List<ESNavNode> Neighbours = new List<ESNavNode>();
    }

    public static class ESNavPathfinder
    {
        /// <summary>
        /// 在无权重图上使用 BFS 搜索一条路径。
        /// 返回的列表包含起点与终点；如果找不到则返回 null。
        /// </summary>
        public static List<ESNavNode> FindPath(ESNavNode start, ESNavNode goal)
        {
            if (start == null || goal == null) return null;
            if (start == goal) return new List<ESNavNode> { start };

            var queue = new Queue<ESNavNode>();
            var cameFrom = new Dictionary<ESNavNode, ESNavNode>();
            var visited = new HashSet<ESNavNode>();

            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == goal)
                    break;

                foreach (var n in cur.Neighbours)
                {
                    if (n == null || visited.Contains(n)) continue;
                    visited.Add(n);
                    cameFrom[n] = cur;
                    queue.Enqueue(n);
                }
            }

            if (!cameFrom.ContainsKey(goal))
                return null;

            var path = new List<ESNavNode> { goal };
            var curNode = goal;
            while (curNode != start)
            {
                curNode = cameFrom[curNode];
                path.Add(curNode);
            }
            path.Reverse();
            return path;
        }
    }
}
