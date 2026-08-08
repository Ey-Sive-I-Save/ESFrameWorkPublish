using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 可回收的 Guillotine 分配器。
    ///
    /// 每个空闲区域是分割树中的叶子。释放时只需要向上折叠已完全空闲的父区域，
    /// 因此不会在高碎片场景下反复全表扫描，也可以在最后一个分配释放后恢复整页。
    /// </summary>
    internal sealed class ESDynamicAtlasAllocator
    {
        private sealed class Node
        {
            public readonly RectInt rect;
            public Node parent;
            public Node first;
            public Node second;
            public bool allocated;
            public int allocationCount;

            public Node(RectInt rect, Node parent)
            {
                this.rect = rect;
                this.parent = parent;
            }

            public bool IsLeaf => first == null && second == null;
        }

        private readonly int width;
        private readonly int height;
        private readonly HashSet<Node> freeLeaves = new HashSet<Node>();
        private readonly Dictionary<RectInt, Node> allocatedNodes = new Dictionary<RectInt, Node>();
        private readonly Node root;
        private int usedPixels;

        public ESDynamicAtlasAllocator(int width, int height)
        {
            this.width = Mathf.Max(1, width);
            this.height = Mathf.Max(1, height);
            root = new Node(new RectInt(0, 0, this.width, this.height), null);
            freeLeaves.Add(root);
        }

        public int UsedPixels => usedPixels;
        public int FreeRectCount => freeLeaves.Count;
        public int FreePixels => width * height - usedPixels;
        public int LargestFreeRectPixels
        {
            get
            {
                int largest = 0;
                foreach (Node leaf in freeLeaves)
                    largest = Mathf.Max(largest, leaf.rect.width * leaf.rect.height);
                return largest;
            }
        }

        public bool TryAllocate(int requestedWidth, int requestedHeight, out RectInt result)
        {
            result = default;
            if (requestedWidth <= 0 || requestedHeight <= 0
                || requestedWidth > width || requestedHeight > height)
            {
                return false;
            }

            Node selected = null;
            int bestAreaWaste = int.MaxValue;
            int bestShortSide = int.MaxValue;

            foreach (Node freeLeaf in freeLeaves)
            {
                RectInt free = freeLeaf.rect;
                if (requestedWidth > free.width || requestedHeight > free.height)
                    continue;

                int areaWaste = free.width * free.height - requestedWidth * requestedHeight;
                int shortSide = Mathf.Min(free.width - requestedWidth, free.height - requestedHeight);
                if (areaWaste < bestAreaWaste || (areaWaste == bestAreaWaste && shortSide < bestShortSide))
                {
                    selected = freeLeaf;
                    bestAreaWaste = areaWaste;
                    bestShortSide = shortSide;
                }
            }

            if (selected == null)
                return false;

            result = new RectInt(selected.rect.x, selected.rect.y, requestedWidth, requestedHeight);
            Node allocated = SplitLeaf(selected, result);
            allocatedNodes.Add(result, allocated);
            usedPixels += requestedWidth * requestedHeight;
            IncrementAllocationCounts(allocated);
            return true;
        }

        public void Free(RectInt rect)
        {
            if (rect.width <= 0 || rect.height <= 0)
                return;

            RectInt clamped = ClampToPage(rect);
            if (clamped.width <= 0 || clamped.height <= 0)
                return;

            // Provider transition, Domain close and lease release can independently
            // request cleanup. Only the owner of the exact leaf may return it.
            if (!allocatedNodes.TryGetValue(clamped, out Node allocated))
                return;

            allocatedNodes.Remove(clamped);
            allocated.allocated = false;
            freeLeaves.Add(allocated);
            usedPixels = Mathf.Max(0, usedPixels - clamped.width * clamped.height);
            DecrementAllocationCountsAndCollapse(allocated);
        }

        private Node SplitLeaf(Node leaf, RectInt used)
        {
            freeLeaves.Remove(leaf);

            int remainingWidth = leaf.rect.width - used.width;
            int remainingHeight = leaf.rect.height - used.height;
            if (remainingWidth <= remainingHeight)
            {
                // First split the full leaf into a top strip and lower free strip;
                // then split the top strip into the allocation and its right remainder.
                Node top = new Node(new RectInt(leaf.rect.x, leaf.rect.y, leaf.rect.width, used.height), leaf);
                Node bottom = new Node(new RectInt(leaf.rect.x, leaf.rect.y + used.height,
                    leaf.rect.width, remainingHeight), leaf);
                leaf.first = top;
                leaf.second = bottom;

                Node allocated = new Node(used, top) { allocated = true };
                Node right = new Node(new RectInt(leaf.rect.x + used.width, leaf.rect.y,
                    remainingWidth, used.height), top);
                top.first = allocated;
                top.second = right;

                AddFreeLeaf(bottom);
                AddFreeLeaf(right);
                return allocated;
            }

            // First split the full leaf into a left strip and right free strip;
            // then split the left strip into the allocation and its lower remainder.
            Node left = new Node(new RectInt(leaf.rect.x, leaf.rect.y, used.width, leaf.rect.height), leaf);
            Node rightStrip = new Node(new RectInt(leaf.rect.x + used.width, leaf.rect.y,
                remainingWidth, leaf.rect.height), leaf);
            leaf.first = left;
            leaf.second = rightStrip;

            Node usedLeaf = new Node(used, left) { allocated = true };
            Node bottomStrip = new Node(new RectInt(leaf.rect.x, leaf.rect.y + used.height,
                used.width, remainingHeight), left);
            left.first = usedLeaf;
            left.second = bottomStrip;

            AddFreeLeaf(rightStrip);
            AddFreeLeaf(bottomStrip);
            return usedLeaf;
        }

        private void AddFreeLeaf(Node leaf)
        {
            if (leaf.rect.width > 0 && leaf.rect.height > 0)
                freeLeaves.Add(leaf);
        }

        private static void IncrementAllocationCounts(Node allocated)
        {
            for (Node current = allocated; current != null; current = current.parent)
                current.allocationCount++;
        }

        private void DecrementAllocationCountsAndCollapse(Node released)
        {
            Node current = released;
            while (current != null)
            {
                current.allocationCount--;
                current = current.parent;
            }

            current = released;
            while (current.parent != null)
            {
                Node parent = current.parent;
                if (parent.allocationCount != 0)
                    break;

                // A zero-count parent is reached only after all its descendants have
                // collapsed, so its direct children are free leaves at this point.
                freeLeaves.Remove(parent.first);
                freeLeaves.Remove(parent.second);
                parent.first = null;
                parent.second = null;
                parent.allocated = false;
                freeLeaves.Add(parent);
                current = parent;
            }
        }

        private RectInt ClampToPage(RectInt rect)
        {
            int xMin = Mathf.Clamp(rect.xMin, 0, width);
            int yMin = Mathf.Clamp(rect.yMin, 0, height);
            int xMax = Mathf.Clamp(rect.xMax, 0, width);
            int yMax = Mathf.Clamp(rect.yMax, 0, height);
            return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
        }
    }
}
