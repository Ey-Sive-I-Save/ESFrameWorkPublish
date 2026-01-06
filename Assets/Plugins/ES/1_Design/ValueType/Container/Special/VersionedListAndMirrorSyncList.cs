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
    /// <summary>
    /// 版本化的列表
    /// 主线和同步镜像，使用这个列表可以进行慢加载的版本化管理
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable, TypeRegistryItem("版本化列表")]
    public class VersionedList<T>
    {
        [LabelText("正在更新", SdfIconType.ArrowRepeat), SerializeReference, GUIColor("@ESDesignUtility.ColorSelector.ColorForUpdating")]
        public List<T> ValuesNow = new List<T>(10);
        // 单一有序操作缓冲（环形数组以减少 GC 分配）
        private struct OpRecord
        {
            public T value;
            public bool isAdd;
            public OpRecord(T v, bool a) { value = v; isAdd = a; }
        }
        private OpRecord[] opBuffer = new OpRecord[16];
        private int opHead = 0;
        private int opTail = 0;
        private int opCount = 0;
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
        private void VersionItem(T who, bool isAdd = true)
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
        public void UpdateVersion()
        {
            if (MirrorValidators.Count == 0)
            {
                VersionMin = VersionMax;
                VersionedRecordChanges.Clear();
            }
            else
            {
                int minVersionNew = VersionMax;
                for (int i = 0; i < MirrorValidators.Count; i++)
                {
                    var va = MirrorValidators[i];
                    bool available = va.Available?.Invoke() ?? false;
                    if (available)
                    {
                        if (va.mirror.VersionNow < minVersionNew)
                        {
                            minVersionNew = va.mirror.VersionNow;
                        }
                    }
                    else
                    {
                        i--;
                        MirrorValidators.Remove(va);
                    }
                }
                if (minVersionNew > VersionMin)
                {
                    int countToRemove = minVersionNew - VersionMin;
                    VersionedRecordChanges.RemoveRange(0, countToRemove);
                    VersionMin = minVersionNew;
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Button("添加测试")]
        public void Add(T add, bool versionRecord = true)
        {
            //原生：按调用顺序入队操作（保证顺序语义）
            EnqueueOp(add, true);
            isDirty = true;
            MayHasElement = true;

            //更新
            if (versionRecord) VersionItem(add, true);
        }
        [Button("移除测试")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(T add, bool versionRecord = true)
        {
            EnqueueOp(add, false);
            isDirty = true;

            //更新
            if (versionRecord) VersionItem(add, false);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(IEnumerable<T> add)
        {
            foreach (var i in add)
            {
                EnqueueOp(i, true);
            }
            isDirty = true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRange(IEnumerable<T> remove)
        {
            foreach (var i in remove)
            {
                EnqueueOp(i, false);
            }
            isDirty = true;
        }
        public bool Contains(T who)
        {
            // 先基于当前生效集合判断
            bool inNow = ValuesNow.Contains(who);
            if (opCount == 0) return inNow;
            // 按缓冲区的操作顺序回放该元素的操作来确定最终状态（无额外集合分配）
            int idx = opHead;
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < opCount; i++)
            {
                var op = opBuffer[idx];
                if (comparer.Equals(op.value, who)) inNow = op.isAdd;
                idx++;
                if (idx == opBuffer.Length) idx = 0;
            }
            return inNow;
        }
        public void ApplyBuffers(bool forceUpdate = false)
        {
            if (isDirty || forceUpdate)
            {
                isDirty = false;
                while (opCount > 0)
                {
                    var op = DequeueOp();
                    if (op.isAdd) ValuesNow.Add(op.value);
                    else ValuesNow.Remove(op.value);
                }
            }
        }

        //Dirty模式>相比Update,性能更好
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ApplyBuffersIfDirty()
        {
            if (!isDirty) return;
            isDirty = false;
            while (opCount > 0)
            {
                var op = DequeueOp();
                if (op.isAdd) ValuesNow.Add(op.value);
                else ValuesNow.Remove(op.value);
            }

        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            ValuesNow.Clear();
            ClearOpBuffer();
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

        #region 内部：操作缓冲环形缓冲实现
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnqueueOp(T value, bool isAdd)
        {
            if (opCount == opBuffer.Length)
            {
                ResizeOpBuffer(opBuffer.Length * 2);
            }
            opBuffer[opTail] = new OpRecord(value, isAdd);
            opTail++;
            if (opTail == opBuffer.Length) opTail = 0;
            opCount++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private OpRecord DequeueOp()
        {
            var r = opBuffer[opHead];
            opHead++;
            if (opHead == opBuffer.Length) opHead = 0;
            opCount--;
            return r;
        }

        private void ResizeOpBuffer(int newSize)
        {
            var newArr = new OpRecord[newSize];
            if (opCount > 0)
            {
                if (opHead < opTail)
                {
                    Array.Copy(opBuffer, opHead, newArr, 0, opCount);
                }
                else
                {
                    int right = opBuffer.Length - opHead;
                    Array.Copy(opBuffer, opHead, newArr, 0, right);
                    Array.Copy(opBuffer, 0, newArr, right, opTail);
                }
            }
            opBuffer = newArr;
            opHead = 0;
            opTail = opCount % newSize;
        }

        private void ClearOpBuffer()
        {
            opHead = opTail = opCount = 0;
            if (opBuffer.Length > 64) opBuffer = new OpRecord[16];
        }
        #endregion

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
        public int VersionNow = 0;
        public IEnumerable<VersionedRecordChange<T>> SyncChanges()
        {
            if (Source != null)
            {
                if (Source.VersionMax > VersionNow)
                {
                    int UpdateCount = Source.VersionMax - VersionNow;//更新数量是？
                    int count = Source.VersionedRecordChanges.Count;
                    for (int index = count - UpdateCount; index < count && UpdateCount >= 0; index++, UpdateCount--)
                    {
                        yield return Source.VersionedRecordChanges[index];
                    }
                    VersionNow = Source.VersionMax;
                    Source.UpdateVersion();
                }
            }
        }
        public void UpdateVersionToMaxOnly()
        {
            VersionNow = Source?.VersionMax ?? 0;
        }
        [Button("镜像处添加")]
        public void Add_IgnoreVersion(T t)
        {
            if (Source.MirrorValidators.Count == 1)//只有自己哈
            {
                Source.Add(t, false);
                UpdateVersionToMaxOnly();
            }
            else
            {
                Source.Add(t, true);
                UpdateVersionToMaxOnly();
            }
        }
        [Button("镜像处移除")]
        public void Remove_IgnoreVersion(T t)
        {
            if (Source.MirrorValidators.Count == 1)//只有自己哈
            {
                Source.Remove(t, false);
                UpdateVersionToMaxOnly();
            }
            else
            {
                Source.Remove(t, true);
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
