using System.Collections.Generic;
using UnityEngine;

namespace ES.AIPreview.Navigation
{
    /// <summary>
    /// 简单的八叉树空间划分原型：
    /// - 可用于加速附近节点/物体查询，为寻路或物理做准备；
    /// - 这里只实现插入与范围查询示例。
    /// </summary>
    public class ESOctree<T>
    {
        private readonly int _maxDepth;
        private readonly int _maxObjectsPerNode;
        private readonly Node _root;

        private class Node
        {
            public Bounds Bounds;
            public List<(Vector3 pos, T value)> Objects = new List<(Vector3 pos, T value)>();
            public Node[] Children;
            public bool IsLeaf => Children == null;
        }

        public ESOctree(Bounds bounds, int maxDepth = 5, int maxObjectsPerNode = 8)
        {
            _maxDepth = maxDepth;
            _maxObjectsPerNode = maxObjectsPerNode;
            _root = new Node { Bounds = bounds };
        }

        public void Insert(Vector3 pos, T value)
        {
            Insert(_root, pos, value, 0);
        }

        private void Insert(Node node, Vector3 pos, T value, int depth)
        {
            if (!node.Bounds.Contains(pos)) return;

            if (node.IsLeaf && (node.Objects.Count < _maxObjectsPerNode || depth >= _maxDepth))
            {
                node.Objects.Add((pos, value));
                return;
            }

            if (node.IsLeaf)
            {
                Subdivide(node);
                // 重新分发已有对象
                foreach (var obj in node.Objects)
                {
                    foreach (var child in node.Children)
                    {
                        if (child.Bounds.Contains(obj.pos))
                        {
                            Insert(child, obj.pos, obj.value, depth + 1);
                            break;
                        }
                    }
                }
                node.Objects.Clear();
            }

            foreach (var child in node.Children)
            {
                if (child.Bounds.Contains(pos))
                {
                    Insert(child, pos, value, depth + 1);
                    return;
                }
            }
        }

        private void Subdivide(Node node)
        {
            node.Children = new Node[8];
            Vector3 size = node.Bounds.size / 2f;
            Vector3 center = node.Bounds.center;

            int index = 0;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        // 修正子节点中心点计算：应为 size * 0.5f * (x, y, z)
                        var childCenter = center + Vector3.Scale(new Vector3(x, y, z), size * 0.5f);
                        node.Children[index++] = new Node
                        {
                            Bounds = new Bounds(childCenter, size)
                        };
                    }
                }
            }
        }

        public List<T> Query(Bounds area)
        {
            var result = new List<T>();
            Query(_root, area, result);
            return result;
        }

        private void Query(Node node, Bounds area, List<T> result)
        {
            if (!node.Bounds.Intersects(area)) return;

            if (node.IsLeaf)
            {
                foreach (var obj in node.Objects)
                {
                    if (area.Contains(obj.pos))
                        result.Add(obj.value);
                }
                return;
            }

            foreach (var child in node.Children)
            {
                Query(child, area, result);
            }
        }
    }
}
