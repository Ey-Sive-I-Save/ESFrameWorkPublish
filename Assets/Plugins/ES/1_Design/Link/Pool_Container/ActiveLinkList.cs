using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ES
{
    /// <summary>
    /// 维护一组具备激活状态的 Link 接收者。
    ///
    /// ActiveLinkList 是“本列表是否持有某项”的唯一状态权威。它不关心接收者的具体业务；
    /// 资源计划、游戏模式或 UI 状态只需实现 IReceiveActiveLink 即可复用本容器。
    ///
    /// 为适配常见的小集合场景，默认预留四项并使用引用线性查找。通常四项左右时，
    /// 线性扫描比 Dictionary 的哈希与额外内存成本更低。若需要更大集合，请在初始化阶段
    /// 调用 Reserve，保证正常运行期不因扩容产生 GC。
    /// </summary>
    /// <typeparam name="T">引用类型的 ActiveLinkList 接收者。</typeparam>
    public sealed class ActiveLinkList<T> where T : class, IReceiveActiveLink
    {
        private const int DefaultCapacity = 4;

        private readonly List<T> _activeItems;
        private readonly List<PendingChange> _pendingChanges;
        private T _highlighted;
        private bool _isDispatching;
        private int _dispatchIndex;

        private struct PendingChange
        {
            public readonly T Item;
            public readonly bool Enable;

            public PendingChange(T item, bool enable)
            {
                Item = item;
                Enable = enable;
            }
        }

        /// <summary>
        /// 当前被本 ActiveLinkList 持有的数量。
        /// </summary>
        public int Count => _activeItems.Count;

        /// <summary>
        /// 当前活动表已预留的容量。运行前 Reserve 至最大预期数量后，正常增删不产生 GC。
        /// </summary>
        public int Capacity => _activeItems.Capacity;

        /// <summary>
        /// 当前高亮项。活动列表为空时为 null。
        /// 高亮是本 ActiveLinkList 内的调度优先级，不会改变任何项的激活或持有状态。
        /// </summary>
        public T Highlighted => _highlighted;

        /// <summary>
        /// 是否存在高亮项。只要 Count 大于零，该值必为 true。
        /// </summary>
        public bool HasHighlight => _highlighted != null;

        public ActiveLinkList(int initialCapacity = DefaultCapacity)
        {
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            _activeItems = new List<T>(initialCapacity);
            _pendingChanges = new List<PendingChange>(GetPendingCapacity(initialCapacity));
        }

        /// <summary>
        /// 在初始化阶段预留容量。不要在状态回调中调用。
        /// </summary>
        public void Reserve(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (_isDispatching)
            {
                throw new InvalidOperationException("ActiveLinkList cannot reserve capacity while dispatching state changes.");
            }

            if (capacity > _activeItems.Capacity)
            {
                _activeItems.Capacity = capacity;
            }

            int pendingCapacity = GetPendingCapacity(capacity);
            if (pendingCapacity > _pendingChanges.Capacity)
            {
                _pendingChanges.Capacity = pendingCapacity;
            }
        }

        /// <summary>
        /// 查询指定项是否正在被本 ActiveLinkList 持有。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsActive(T item)
        {
            ValidateItem(item);
            return IndexOf(item) >= 0;
        }

        /// <summary>
        /// 激活指定项。已激活时不重复通知，并返回 false。
        /// </summary>
        public bool Activate(T item)
        {
            ValidateItem(item);
            if (IndexOf(item) >= 0)
            {
                return false;
            }

            _activeItems.Add(item);
            if (_highlighted == null)
            {
                _highlighted = item;
            }

            Enqueue(item, true);
            DispatchPendingChanges();
            return true;
        }

        /// <summary>
        /// 禁用指定项。原本未激活时不通知，并返回 false。
        /// </summary>
        public bool Deactivate(T item)
        {
            ValidateItem(item);
            int index = IndexOf(item);
            if (index < 0)
            {
                return false;
            }

            _activeItems.RemoveAt(index);
            if (ReferenceEquals(_highlighted, item))
            {
                _highlighted = _activeItems.Count > 0 ? _activeItems[0] : null;
            }

            Enqueue(item, false);
            DispatchPendingChanges();
            return true;
        }

        /// <summary>
        /// 将一个已激活项设为当前高亮。高亮切换只修改优先级，不发送启用或禁用通知。
        /// </summary>
        public bool SetHighlight(T item)
        {
            ValidateItem(item);
            if (IndexOf(item) < 0)
            {
                return false;
            }

            if (ReferenceEquals(_highlighted, item))
            {
                return false;
            }

            _highlighted = item;
            return true;
        }

        /// <summary>
        /// 查询指定项是否为本 ActiveLinkList 当前的高亮项。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsHighlighted(T item)
        {
            ValidateItem(item);
            return ReferenceEquals(_highlighted, item);
        }

        /// <summary>
        /// 原子替换本列表的活动集合，使其最终仅保留指定项。
        /// 状态会在任何回调前完成提交：禁用回调与启用回调均可读取到最终集合。
        /// </summary>
        public bool ReplaceExclusive(T item)
        {
            ValidateItem(item);

            int count = _activeItems.Count;
            bool itemWasActive = false;
            for (int i = 0; i < count; i++)
            {
                T current = _activeItems[i];
                if (ReferenceEquals(current, item))
                {
                    itemWasActive = true;
                }
                else
                {
                    Enqueue(current, false);
                }
            }

            if (itemWasActive && count == 1)
            {
                return false;
            }

            _activeItems.Clear();
            _activeItems.Add(item);
            _highlighted = item;
            if (!itemWasActive)
            {
                Enqueue(item, true);
            }

            DispatchPendingChanges();
            return true;
        }

        /// <summary>
        /// 禁用本列表当前持有的全部项，返回实际禁用数量。
        /// </summary>
        public int DeactivateAll()
        {
            int count = _activeItems.Count;
            if (count == 0)
            {
                return 0;
            }

            for (int i = 0; i < count; i++)
            {
                Enqueue(_activeItems[i], false);
            }

            _activeItems.Clear();
            _highlighted = null;
            DispatchPendingChanges();
            return count;
        }

        /// <summary>
        /// 显式禁用除当前高亮项外的全部项，返回实际禁用数量。
        /// 此方法不会由高亮切换自动调用，避免把“调度优先级变化”误变为“资源释放”。
        /// </summary>
        public int DeactivateNonHighlighted()
        {
            int count = _activeItems.Count;
            if (count <= 1)
            {
                return 0;
            }

            T highlighted = _highlighted;
            // Count 大于零时 Highlighted 是 ActiveLinkList 的不变量；此处保留保护，避免
            // 外部异常状态导致错误地保留空引用。
            if (highlighted == null)
            {
                highlighted = _activeItems[0];
                _highlighted = highlighted;
            }

            for (int i = 0; i < count; i++)
            {
                T current = _activeItems[i];
                if (!ReferenceEquals(current, highlighted))
                {
                    Enqueue(current, false);
                }
            }

            _activeItems.Clear();
            _activeItems.Add(highlighted);
            DispatchPendingChanges();
            return count - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int IndexOf(T item)
        {
            int count = _activeItems.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_activeItems[i], item))
                {
                    return i;
                }
            }

            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Enqueue(T item, bool enable)
        {
            _pendingChanges.Add(new PendingChange(item, enable));
        }

        /// <summary>
        /// 回调允许重入调用 Activate / Deactivate。重入变更会追加至队尾并在当前派发结束前处理，
        /// 因而不会修改正在遍历的状态表，也不会创建临时委托或枚举器。
        /// </summary>
        private void DispatchPendingChanges()
        {
            if (_isDispatching)
            {
                return;
            }

            _isDispatching = true;
            try
            {
                while (_dispatchIndex < _pendingChanges.Count)
                {
                    PendingChange change = _pendingChanges[_dispatchIndex++];
                    if (change.Enable)
                    {
                        change.Item.OnLinkEnable();
                    }
                    else
                    {
                        change.Item.OnLinkDisable();
                    }
                }

                _pendingChanges.Clear();
                _dispatchIndex = 0;
            }
            finally
            {
                // 回调异常不能让 ActiveLinkList 永远停在“派发中”。已处理的通知被移除；
                // 尚未处理的通知保留，下一次状态变更会从其继续派发。
                if (_dispatchIndex > 0)
                {
                    _pendingChanges.RemoveRange(0, _dispatchIndex);
                    _dispatchIndex = 0;
                }

                _isDispatching = false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetPendingCapacity(int itemCapacity)
        {
            return itemCapacity > DefaultCapacity ? itemCapacity * 2 : DefaultCapacity * 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidateItem(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
        }
    }
}
