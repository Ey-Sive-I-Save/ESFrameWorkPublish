using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace ES
{
    #region 定义VersionedList
    //版本化的列表
    [Serializable, TypeRegistryItem("版本化列表")]
    public class VersionedList<T>
    {
        [LabelText("正在更新", SdfIconType.ArrowRepeat), SerializeReference, GUIColor("@ESDesignUtility.ColorSelector.ColorForUpdating")]
        public List<T> ValuesNow = new List<T>(10);
        [FoldoutGroup("缓冲")]
        [ShowInInspector, NonSerialized, LabelText("缓冲添加队列", SdfIconType.BoxArrowInLeft)]
        private Queue<T> ValuesBufferToAdd = new Queue<T>();
        [FoldoutGroup("缓冲")]
        [ShowInInspector, NonSerialized, LabelText("缓冲移除队列", SdfIconType.BoxArrowRight)]
        private Queue<T> ValuesBufferToRemove = new Queue<T>();
        private bool isDirty;
        public bool MayHasElement = true;
        [LabelText("最低版本")]
        public int VersionMin = 0;
        [LabelText("最高版本")]
        public int VersionMax = 0;
        [LabelText("记录操作列")]
        public List<VersionedRecordChange<T>> VersionedRecordChanges = new List<VersionedRecordChange<T>>(50);
        [LabelText("镜像验证器")]
        public List<MirrorSyncListValidator<T>> MirrorValidators = new List<MirrorSyncListValidator<T>>();
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
        public void VersionItem(T who, bool isAdd = true)
        {
            if (MirrorValidators.Count == 0)
            {
                //清空
                if (VersionMax > 0)
                {
                    VersionedRecordChanges.Clear();
                    VersionMax = VersionMin = 0;
                }
            }
            else
            {
                VersionMax++;
                VersionedRecordChanges.Add(new VersionedRecordChange<T>() { value = who, IsAdd = isAdd });
            }
        }
        public void TryUpdateVersion()
        {
            if (MirrorValidators.Count == 0)
            {
                VersionMin = VersionMax;
                VersionedRecordChanges.Clear();
            }
            else
            {
                int minVersonNew = VersionMax;
                for (int i = 0; i < MirrorValidators.Count; i++)
                {
                    var va = MirrorValidators[i];
                    bool available = va.Available?.Invoke() ?? false;
                    if (available)
                    {
                        if (va.mirror.VersonNow < minVersonNew)
                        {
                            minVersonNew = va.mirror.VersonNow;
                        }
                    }
                    else
                    {
                        i--;
                        MirrorValidators.Remove(va);
                    }
                }
                if (minVersonNew > VersionMin)
                {
                    int countToRemove = minVersonNew - VersionMin;
                    VersionedRecordChanges.RemoveRange(0, countToRemove);
                    VersionMin = minVersonNew;
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Button("添加测试")]
        public void TryAdd(T add, bool versonRecord = true)
        {
            //原生
            ValuesBufferToAdd.Enqueue(add);
            isDirty = true;
            MayHasElement = true;

            //更新
            if (versonRecord) VersionItem(add, true);
        }
        [Button("移除测试")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryRemove(T add, bool versonRecord = true)
        {
            ValuesBufferToRemove.Enqueue(add);
            isDirty = true;

            //更新
            if (versonRecord) VersionItem(add, false);
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
            MayHasElement = false;
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
        public void BindMirrorValidators(MirrorSyncListValidator<T> validator)
        {
            if (MirrorValidators.Contains(validator)) return;
            if (validator.Available?.Invoke() ?? false)
            {
                validator.mirror.Source = this;
                MirrorValidators.Add(validator);
            }
        }

    }
    #endregion

    #region 定义MirrorSync
    [Serializable, TypeRegistryItem("镜像同步")]
    public class MirrorSync<T>
    {
        [NonSerialized]
        public VersionedList<T> Source = null;
        [LabelText("已经初始化")]
        public bool HasInit = true;
        [LabelText("当前版本")]
        public int VersonNow = 0;
        public IEnumerable<VersionedRecordChange<T>> SyncChanges()
        {
            if (Source != null)
            {
                if (Source.VersionMax > VersonNow)
                {
                    int UpdateCount = Source.VersionMax - VersonNow;//更新数量是？
                    int count = Source.VersionedRecordChanges.Count;
                    for (int index = count - UpdateCount; index < count && UpdateCount >= 0; index++, UpdateCount--)
                    {
                        yield return Source.VersionedRecordChanges[index];
                    }
                    VersonNow = Source.VersionMax;
                    Source.TryUpdateVersion();
                }
            }
        }
        public void UpdateVersionToMaxOnly()
        {
            VersonNow = Source?.VersionMax ?? 0;
        }
        [Button("镜像处添加")]
        public void TryAdd_IgnoreVersion(T t)
        {
            if (Source.MirrorValidators.Count == 1)//只有自己哈
            {
                Source.TryAdd(t, false);
                UpdateVersionToMaxOnly();
            }
            else
            {
                Source.TryAdd(t, true);
                UpdateVersionToMaxOnly();
            }
        }
        [Button("镜像处移除")]
        public void TryRemove_IgnoreVersion(T t)
        {
            if (Source.MirrorValidators.Count == 1)//只有自己哈
            {
                Source.TryRemove(t, false);
                UpdateVersionToMaxOnly();
            }
            else
            {
                Source.TryRemove(t, true);
                UpdateVersionToMaxOnly();
            }
        }
    }
    #endregion

    #region 辅助类-VersionRecordChange
    [Serializable, TypeRegistryItem("更改记录")]
    public class VersionedRecordChange<T>
    {
        public bool IsAdd = true;
        public T value;
    }
    #endregion

    #region 辅助类-MirrorListValidator
    [Serializable, TypeRegistryItem("镜像同步验证器")]
    public class MirrorSyncListValidator<T>
    {
        public static Func<bool> DefaultAvailable => () => true;
        public Func<bool> Available = DefaultAvailable;
        [NonSerialized]
        public MirrorSync<T> mirror = null;
        public MirrorSyncListValidator(MirrorSync<T> mirror, Func<bool> Available)
        {
            this.mirror = mirror;
            this.Available = Available;
        }
        public MirrorSyncListValidator(MirrorSync<T> mirror, UnityEngine.Object baseOn)
        {
            this.mirror = mirror;
            this.Available = () => baseOn != null;
        }
    }

    #endregion
}
