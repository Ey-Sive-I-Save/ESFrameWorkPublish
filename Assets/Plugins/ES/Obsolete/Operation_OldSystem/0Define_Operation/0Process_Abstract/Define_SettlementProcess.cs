/*
using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ES {
    public enum SettlementChannel
    {
        Normal, Dynamic
    }
    public class Settlement<ValueType, Operation_, This> : IProcess<ValueType, ValueType, Operation_, SettlementChannel> 
        where This: Settlement<ValueType, Operation_, This>
        where Operation_:SettleOperation<ValueType,This, Operation_>,new()
    {
      
        [LabelText("基础�?), SerializeField] private ValueType _baseValue;
        [LabelText("常规结算�?), SerializeField] private ValueType _normalValue;
        [LabelText("最终结算�?), SerializeField] private ValueType _settlementValue;
        [Button("结算")] public void SettleMent() => Debug.Log(SettlementValue);
        public ValueType BaseValue { get => _baseValue; set { _baseValue = value; MakeDirty(); } }
        public ValueType SettlementValue { get { if (_isDirty) ReCalculateNormal(); ReCalculateDynamic(); _isDirty = false; return _settlementValue; } }

        public ValueType Source { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public ValueType Output { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        [ShowInInspector, FoldoutGroup("操作�?), LabelText("全部常规操作")] protected readonly Dictionary<object, ListOperationn_>> AllNormalOperations = new Dictionary<object, ListOperationn_>>();
        [ShowInInspector, FoldoutGroup("操作�?), LabelText("（排序）全部常规操作")] protected readonly SortedList<int, ListOperationn_>> _sortedNormalOperations = new SortedList<int, ListOperationn_>>(Comparer<int>.Create((a, b) => -b.CompareTo(a)));
        [ShowInInspector, FoldoutGroup("操作�?), LabelText("全部动态操�?)] protected readonly SortedSeOperationon_> dyncmicOperations = new SortedSeOperationon_>();
        [FoldoutGroup("操作�?), Button("脏列�?)]
        public void MakeDirty()
        {
            _isDirty = true;
        }
        protected bool _isDirty = true;

        public void ReCalculateNormal()
        {
            _normalValue = _baseValue;
            foreach (var i in _sortedNormalOperations.Values)
            {
                foreach (var ii in i)
                {
                    if (ii == null) continue;
                    _normalValue = ii.HandleOperation(_normalValue);
                }
            }
        }
        public void ReCalculateDynamic()
        {
            _settlementValue = _normalValue;
            foreach (var i in dyncmicOperations)
            {
                if (i == null) continue;
                _settlementValue = i.HandleOperation(_normalValue);
            }
        }
        public void AddNormalOperation(Operation_ use)
        {
            if (use == null) return;
            if (use.Source == null) use.Source = this;
            if (!_sortedNormalOperations.TryGetValue(use.Order, out var group))
            {
                _sortedNormalOperations.Add(use.Order, group = new List<Operation_>());
            }
            group.Add(use);

            if (!AllNormalOperations.TryGetValue(use.Source, out var sourceList))
            {
                sourceList = new List<Operation_>();
                AllNormalOperations.Add(use.Source, sourceList);
            }
            sourceList.Add(use);

            MakeDirty();
        }
        public void RemoveNormalOperation(Operation_ use)
        {
            if (use == null) return;
            if (_sortedNormalOperations.TryGetValue(use.Order, out var group))
            {
                group.Remove(use);
            }
            if (AllNormalOperations.TryGetValue(use.Source, out var sourceList))
            {
                sourceList.Remove(use);
            }
            MakeDirty();
        }
        public void RemoveNormalOperationFromSource(object source)
        {
            if (!AllNormalOperations.TryGetValue(source, out var sourceList)) return;
            foreach (var i in sourceList)
            {
                var group = _sortedNormalOperations[i.Order];
                group.Remove(i);
            }
            AllNormalOperations.Remove(source);

            MakeDirty();
        }
        [FoldoutGroup("操作�?), Button("重整")]
        public void ReSortAll()
        {
            _sortedNormalOperations.Clear();
            var enum_ = AllNormalOperations.ToDictionary((i) => i.Key, (i) => i.Value);
            AllNormalOperations.Clear();
            foreach (var (i, k) in enum_)
            {
                foreach (var ii in k)
                {
                    AddNormalOperation(ii);
                }
            }
        }
        public void AddDynamicOperation(Operation_ use)
        {
            if (use == null) return;
            if (!dyncmicOperations.Contains(use))
            {
                dyncmicOperations.Add(use);
            }
            MakeDirty();
        }
        public void RemoveDynamicOperation(Operation_ use)
        {
            if (use == null) return;
            if (dyncmicOperations.Contains(use))
            {
                dyncmicOperations.Remove(use);
            }
            MakeDirty();
        }

        public void DoProcess(ValueType source)
        {
            BaseValue = source;
            ReCalculateNormal();
            ReCalculateDynamic();
        }

        public void AddOperation(Operation_ op, SettlementChannel channel)
        {
            if (channel == SettlementChannel.Normal) AddNormalOperation(op);
            else AddDynamicOperation(op);
        }

        public void RemoveOperation(Operation_ op, SettlementChannel channel)
        {
            if (channel == SettlementChannel.Normal) RemoveNormalOperation(op);
            else RemoveDynamicOperation(op);
        }

        public void ClearChannel(SettlementChannel channel)
        {
            if (channel == SettlementChannel.Normal)
            {
                _sortedNormalOperations.Clear();
                AllNormalOperations.Clear();
            }
            else
            {
                dyncmicOperations.Clear();
            }
            MakeDirty();
        }

        public IReadOnlyList<Operation_> GetOperations(SettlementChannel channel)
        {
            if (channel == SettlementChannel.Normal)
            {
                return _sortedNormalOperations.Values.SelectMany(i => i).ToList();
            }
            else
            {
                return dyncmicOperations.ToList();
            }
        }
    }

    [TypeRegistryItem("浮点结算结果�?), Serializable]
    public class SettlementFloat : Settlement<float, SettleOperationFloat, SettlementFloat>
    {
        public SettlementFloat()
        {

        }
        public SettlementFloat(float value)
        {
            BaseValue = value;
        }

    }
}

*/