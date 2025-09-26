using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
/* 安全循环列表 就是允许在遍历时 进行增加和删除操作
 * 在这个基础上的线程安全列表，会加一个Thread  依赖Lock实现
   必须在每帧或者必要时，调用他的Update

    SafeLoopUpdateList_EasyQueue_SeriNot_Dirty ->SafeList
 */
namespace ES
{
    //帧刷新
    [Serializable, TypeRegistryItem("队列循环安全列表_持久")]
    public class SafeBasicList<T> : ISafeList<T>
    {
        [LabelText("正在更新", SdfIconType.ArrowRepeat), SerializeReference/*, GUIColor("@ESDesignUtility.ColorSelector.ColorForUpdating")*/]
        public List<T> ValuesNow = new List<T>(10);
        [FoldoutGroup("缓冲")][LabelText("缓冲添加", SdfIconType.BoxArrowInLeft), SerializeReference] public List<T> ValuesBufferToAdd = new List<T>();
        [FoldoutGroup("缓冲")][LabelText("缓冲移除", SdfIconType.BoxArrowRight), SerializeReference] public List<T> ValuesBufferToRemove = new List<T>();
        public Action<bool, T> OnChange = (Add, What) => { };

        public IEnumerable<T> ValuesIEnumable
        {
            get
            {
                // 内联迭代器：直接返回 ValuesNow 的枚举器
                foreach (var item in ValuesNow)
                {
                    yield return item;
                }
            }
        }


        public bool AutoApplyBuffers
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set;
        } = true;


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAutoApplyBuffers(bool b) => AutoApplyBuffers = b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAdd(T add)
        {
            ValuesBufferToAdd.Add(add);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemove(T remove)
        {
            ValuesBufferToRemove.Add(remove);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAddRange(IEnumerable<T> add)
        {
            ValuesBufferToAdd.AddRange(add);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemoveRange(IEnumerable<T> remove)
        {
            ValuesBufferToRemove.AddRange(remove);
        }
        public void TryApplyBuffers()
        {
            foreach (var i in ValuesBufferToAdd)
            {
                if (ValuesNow.Contains(i)) continue;
                OnChange.Invoke(true, i);
                ValuesNow.Add(i);
            }
            foreach (var i in ValuesBufferToRemove)
            {
                if (!ValuesNow.Contains(i)) continue;
                OnChange.Invoke(false, i);
                ValuesNow.Remove(i);
            }
            ValuesBufferToAdd.Clear();
            ValuesBufferToRemove.Clear();
        }

        public bool TryContains(T who)
        {
            if (ValuesBufferToRemove.Contains(who)) return false;
            if (ValuesBufferToAdd.Contains(who)) return true;
            return ValuesNow.Contains(who);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyBuffers(bool force = false)
        {
            TryApplyBuffers();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            ValuesNow.Clear();
            ValuesBufferToAdd.Clear();
            ValuesBufferToRemove.Clear();
        }
    }

    //Dirty 刷新
    [Serializable, TypeRegistryItem("队列循环安全脏列表_持久")]
    public class SafeNormalList<T> : ISafeList<T>
    {
        [LabelText("正在更新", SdfIconType.ArrowRepeat), SerializeReference/*, GUIColor("@ESDesignUtility.ColorSelector.ColorForUpdating")*/]
        public List<T> ValuesNow = new List<T>(10);
        [FoldoutGroup("缓冲中"),HideInEditorMode]
        [ShowInInspector, NonSerialized, LabelText("缓冲添加队列", SdfIconType.BoxArrowInLeft)]
        private Queue<T> ValuesBufferToAdd = new Queue<T>();
        [FoldoutGroup("缓冲中"), HideInEditorMode]
        [ShowInInspector,NonSerialized,LabelText("缓冲移除队列", SdfIconType.BoxArrowRight)]
        private Queue<T> ValuesBufferToRemove = new Queue<T>();
        private bool isDirty;
        [HideInInspector]
        public bool MayHasAddingElement = true;
        public bool AutoApplyBuffers { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] set; } = true;
        public void SetAutoApplyBuffers(bool b) => AutoApplyBuffers = b;
        public IEnumerable<T> ValuesIEnumable
        {
            get
            {
                // 内联迭代器：直接返回 ValuesNow 的枚举器
                foreach (var item in ValuesNow)
                {
                    yield return item;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAdd(T add)
        {
            ValuesBufferToAdd.Enqueue(add);
            isDirty = true;
            MayHasAddingElement = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemove(T add)
        {
            ValuesBufferToRemove.Enqueue(add);
            isDirty = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAddRange(IEnumerable<T> add)
        {
            foreach (var i in add)
            {
                ValuesBufferToAdd.Enqueue(i);
            }
            isDirty = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemoveRange(IEnumerable<T> remove)
        {
            foreach (var i in remove)
            {
                ValuesBufferToRemove.Enqueue(i);
            }
            isDirty = true;
        }
        public bool TryContains(T who)
        {
            if (ValuesBufferToRemove.Contains(who)) return false;
            if (ValuesBufferToAdd.Contains(who)) return true;
            return ValuesNow.Contains(who);
        }
        public void ApplyBuffers(bool forceUpdate = false)
        {
            if (isDirty || forceUpdate)
            {
                isDirty = false;
                while (ValuesBufferToAdd.Count > 0)
                {
                    ValuesNow.Add(ValuesBufferToAdd.Dequeue());
                }
                while (ValuesBufferToRemove.Count > 0)
                {
                    ValuesNow.Remove(ValuesBufferToRemove.Dequeue());
                }
            }
        }

        //Dirty模式>相比Update,性能更好
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryApplyBuffers()
        {
            if (!isDirty) return;
            isDirty = false;
            while (ValuesBufferToAdd.Count > 0)
            {
                ValuesNow.Add(ValuesBufferToAdd.Dequeue());
            }
            while (ValuesBufferToRemove.Count > 0)
            {
                ValuesNow.Remove(ValuesBufferToRemove.Dequeue());
            }

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            ValuesNow.Clear();
            ValuesBufferToAdd.Clear();
            ValuesBufferToRemove.Clear();
            MayHasAddingElement = false;
        }
        #region 杂项


        public void _ES_ClearWarnning()
        {
            //只是用来清除 Warn 项 没有任何意义
            ForceUpdate();
        }

        [Button("强制更新")]
        [FoldoutGroup("缓冲中"), HideInEditorMode]
        private void ForceUpdate()
        {
            ApplyBuffers(true);
        }

        #endregion


    }



    [Serializable, TypeRegistryItem("队列线程安全列表_持久")]
    public class SafeThreadBasicList<T> : ISafeList<T>
    {
        [LabelText("正在更新", SdfIconType.ArrowRepeat), SerializeReference, GUIColor("@ESDesignUtility.ColorSelector.ColorForUpdating")]
        public List<T> ValuesNow = new List<T>(10);
        [FoldoutGroup("缓冲")][LabelText("缓冲添加", SdfIconType.BoxArrowInLeft), SerializeReference] public List<T> ValuesBufferToAdd = new List<T>();
        [FoldoutGroup("缓冲")][LabelText("缓冲移除", SdfIconType.BoxArrowRight), SerializeReference] public List<T> ValuesBufferToRemove = new List<T>();
        public Action<bool, T> OnChange = (Add, What) => { };
        /*private readonly object _lockObj = new object(); // 单一锁对象*/
        public bool AutoApplyBuffers { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] set; } = true;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAutoApplyBuffers(bool b) => AutoApplyBuffers = b;
        public IEnumerable<T> ValuesIEnumable => ValuesNow;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAdd(T add)
        {
            lock (ValuesBufferToAdd)
            {
                ValuesBufferToAdd.Add(add);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemove(T remove)
        {
            lock (ValuesBufferToRemove)
            {
                ValuesBufferToRemove.Add(remove);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAddRange(IEnumerable<T> add)
        {
            lock (ValuesBufferToAdd)
            {
                ValuesBufferToAdd.AddRange(add);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemoveRange(IEnumerable<T> remove)
        {
            lock (ValuesBufferToRemove)
            {
                ValuesBufferToRemove.AddRange(remove);
            }
        }
        public void TryApplyBuffers()
        {
            lock (ValuesBufferToAdd)
            {
                foreach (var i in ValuesBufferToAdd)
                {
                    if (ValuesNow.Contains(i)) continue;
                    OnChange.Invoke(true, i);
                    ValuesNow.Add(i);
                }
                ValuesBufferToAdd.Clear();
            }
            lock (ValuesBufferToRemove)
            {
                foreach (var i in ValuesBufferToRemove)
                {
                    if (!ValuesNow.Contains(i)) continue;
                    OnChange.Invoke(false, i);
                    ValuesNow.Remove(i);
                }
                ValuesBufferToRemove.Clear();
            }
        }
        public bool TryContains(T who)
        {
            lock (ValuesBufferToAdd)
            {
                if (ValuesBufferToAdd.Contains(who)) return true;
            }
            lock (ValuesBufferToRemove)
            {
                if (ValuesBufferToRemove.Contains(who)) return false;
            }
            return ValuesNow.Contains(who);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyBuffers(bool force = false)
        {
            TryApplyBuffers();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            ValuesNow.Clear();
            ValuesBufferToAdd.Clear();
            ValuesBufferToRemove.Clear();
        }
    }

    //Dirty 刷新
    [Serializable, TypeRegistryItem("队列线程安全脏列表_持久")]
    public class SafeThreadNormalList<T> : ISafeList<T>
    {
        [LabelText("正在更新", SdfIconType.ArrowRepeat), SerializeReference, ShowInInspector, GUIColor("@ESDesignUtility.ColorSelector.ColorForUpdating")]
        public List<T> ValuesNow = new List<T>(10);
        [FoldoutGroup("缓冲")]
        [ShowInInspector, LabelText("缓冲添加队列", SdfIconType.BoxArrowInLeft)]
        private Queue<T> ValuesBufferToAdd = new Queue<T>();
        [FoldoutGroup("缓冲")]
        [ShowInInspector, LabelText("缓冲移除队列", SdfIconType.BoxArrowRight)]
        private Queue<T> ValuesBufferToRemove = new Queue<T>();
        private bool isDirty;
        public bool AutoApplyBuffers { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] set; } = true;
        public void SetAutoApplyBuffers(bool b) => AutoApplyBuffers = b;
        public IEnumerable<T> ValuesIEnumable => ValuesNow;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAdd(T add)
        {
            lock (ValuesBufferToAdd)
            {
                ValuesBufferToAdd.Enqueue(add);
                isDirty = true;
            }

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemove(T add)
        {
            lock (ValuesBufferToRemove)
            {
                ValuesBufferToRemove.Enqueue(add);
                isDirty = true;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryAddRange(IEnumerable<T> add)
        {
            lock (ValuesBufferToAdd)
            {
                foreach (var i in add)
                {
                    ValuesBufferToAdd.Enqueue(i);
                }
                isDirty = true;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemoveRange(IEnumerable<T> remove)
        {
            lock (ValuesBufferToRemove)
            {
                foreach (var i in remove)
                {
                    ValuesBufferToRemove.Enqueue(i);
                }
                isDirty = true;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryContains(T who)
        {
            lock (ValuesBufferToAdd) if (ValuesBufferToAdd.Contains(who)) return true;
            lock (ValuesBufferToRemove) if (ValuesBufferToRemove.Contains(who)) return false;
            return ValuesNow.Contains(who);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyBuffers(bool forceUpdate = false)
        {
            if (isDirty || forceUpdate)
            {
                isDirty = false;

                lock (ValuesBufferToAdd)
                    while (ValuesBufferToAdd.Count > 0)
                    {
                        ValuesNow.Add(ValuesBufferToAdd.Dequeue());
                    }

                lock (ValuesBufferToRemove)
                    while (ValuesBufferToRemove.Count > 0)
                    {
                        ValuesNow.Remove(ValuesBufferToRemove.Dequeue());
                    }
            }
        }

        //Dirty模式>相比Update,性能更好
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryApplyBuffers()
        {
            if (!isDirty) return;
            isDirty = false;
            lock (ValuesBufferToAdd)
                while (ValuesBufferToAdd.Count > 0)
                {
                    ValuesNow.Add(ValuesBufferToAdd.Dequeue());
                }
            lock (ValuesBufferToRemove)
                while (ValuesBufferToRemove.Count > 0)
                {
                    ValuesNow.Remove(ValuesBufferToRemove.Dequeue());
                }

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            ValuesNow.Clear();
            ValuesBufferToAdd.Clear();
            ValuesBufferToRemove.Clear();
        }
        #region 杂项


        public void _ES_ClearWarnning()
        {
            //只是用来清除 Warn 项 没有任何意义
            ForceUpdate();
        }

        [Button("强制更新")]
        [FoldoutGroup("缓冲")]
        private void ForceUpdate()
        {
            ApplyBuffers(true);
        }

        #endregion


    }

    #region 废案

    /// <summary>
    /// 队列安全(脏)集合_不持久 HashSet
    ///  废弃原因： 不具有可视化修改 只是具有HashSet查重 一般用不到
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable, TypeRegistryItem("队列安全集合_不持久")]
    public class OB_SafeUpdateSet_EasyQueue_SeriNot<T>
    {

        [LabelText("正在更新", SdfIconType.ArrowRepeat), GUIColor("@ESDesignUtility.ColorSelector.ColorForUpdating")]
        public HashSet<T> valuesNow_ = new HashSet<T>(10);
        [FoldoutGroup("缓冲")]
        [ShowInInspector, LabelText("缓冲添加队列", SdfIconType.BoxArrowInLeft)]
        public Queue<T> valuesToAdd = new Queue<T>();
        [FoldoutGroup("缓冲")]
        [ShowInInspector, LabelText("缓冲移除队列", SdfIconType.BoxArrowRight)]
        public Queue<T> valuesToRemove = new Queue<T>();


        public void Update()
        {
            while (valuesToAdd.Count > 0)
            {
                valuesNow_.Add(valuesToAdd.Dequeue());
            }
            while (valuesToRemove.Count > 0)
            {
                valuesNow_.Remove(valuesToRemove.Dequeue());
            }
        }
    }
    [Serializable, TypeRegistryItem("队列安全脏集合_不持久")]
    public class OB_SafeUpdateSet_EasyQueue_SeriNot_Dirty<T>
    {
        [LabelText("正在更新", SdfIconType.ArrowRepeat), ShowInInspector, GUIColor("@ESDesignUtility.ColorSelector.ColorForUpdating")]
        public HashSet<T> valuesNow_ = new HashSet<T>(10);
        [FoldoutGroup("缓冲")]
        [ShowInInspector, LabelText("缓冲添加队列", SdfIconType.BoxArrowInLeft)]
        private Queue<T> valuesToAdd = new Queue<T>();
        [FoldoutGroup("缓冲")]
        [ShowInInspector, LabelText("缓冲移除队列", SdfIconType.BoxArrowRight)]
        private Queue<T> valuesToRemove = new Queue<T>();
        private bool isDirty { get; set; }
        public void TryAdd(T add)
        {
            valuesToAdd.Enqueue(add);
            isDirty = true;
        }
        public void TryAdd(ref T add)
        {
            valuesToAdd.Enqueue(add);
            isDirty = true;
        }
        public void TryRemove(T add)
        {
            valuesToRemove.Enqueue(add);
            isDirty = true;
        }
        [Button("强制更新")]
        [FoldoutGroup("缓冲")]
        private void ForceUpdate()
        {
            Update(true);
        }
        public void Update(bool forceUpdate = false)
        {
            if (isDirty || forceUpdate)
            {
                isDirty = false;
                while (valuesToAdd.Count > 0)
                {
                    valuesNow_.Add(valuesToAdd.Dequeue());
                }
                while (valuesToRemove.Count > 0)
                {
                    valuesNow_.Remove(valuesToRemove.Dequeue());
                }
            }
        }
    }


    #endregion
}

