using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
/* 安全循环列表：允许在遍历期间延迟提交增加和删除操作。 */
namespace ES
{
  

    //Dirty 刷新
    [Serializable, TypeRegistryItem("队列循环安全脏列表_持久")]
    public class SafeNormalList<T> : BaseSafeList<T>,ISafeList<T>
    {
        [LabelText("正在更新", SdfIconType.ArrowRepeat), SerializeReference/*, GUIColor("@ESDesignUtility.ColorSelector.ColorForUpdating")*/]
        public List<T> ValuesNow = new List<T>(10);
        [FoldoutGroup("缓冲中"),HideInEditorMode]
        [ShowInInspector, NonSerialized, LabelText("缓冲添加队列", SdfIconType.BoxArrowInLeft)]
        private Queue<T> ValuesBufferToAdd = new Queue<T>(4);
        [FoldoutGroup("缓冲中"), HideInEditorMode]
        [ShowInInspector,NonSerialized,LabelText("缓冲移除队列", SdfIconType.BoxArrowRight)]
        private Queue<T> ValuesBufferToRemove = new Queue<T>(4);
        private bool isDirty;
        [HideInInspector]
        public bool MayHasAddingElement = true;
        // AutoApplyBuffers provided by BaseSafeList
        protected override IEnumerable<T> _Internal_ValuesIEnumable
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
        public override void Add(T add)
        {
            ValuesBufferToAdd.Enqueue(add);
            isDirty = true;
            MayHasAddingElement = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void Remove(T add)
        {
            ValuesBufferToRemove.Enqueue(add);
            isDirty = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(IEnumerable<T> add)
        {
            foreach (var i in add)
            {
                ValuesBufferToAdd.Enqueue(i);
            }
            isDirty = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRange(IEnumerable<T> remove)
        {
            foreach (var i in remove)
            {
                ValuesBufferToRemove.Enqueue(i);
            }
            isDirty = true;
        }
        public override bool Contains(T who)
        {
            var result = ValuesNow.Contains(who);
            if (ValuesBufferToAdd.Contains(who)) result = true;
            if (ValuesBufferToRemove.Contains(who)) result = false;
            return result;
        }
        public override void ApplyBuffers(bool forceUpdate = false)
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
        public override void ApplyBuffers()
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
        public override void Clear()
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

}

