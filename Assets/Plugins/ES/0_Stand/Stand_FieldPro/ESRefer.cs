using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace ES
{
    [Serializable]
    public class ESReferAbstract<T>
    {
        [ShowInInspector]
        public T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_current != null) return _current;
                return _current = HandleIfNull();
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            { _current = value; }
        }

        private T _current;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCurrent(T t, bool internalSet = false)
        {
            if (internalSet) _current = t;
            else Current = t;
        }
        public virtual T HandleIfNull()
        {
            return _current;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(ESReferAbstract<T> from)
        {
            return from.Current;
        }

    }

    [Serializable]
    public sealed class ESReferLazy<T> where T : class
    {
        [ShowInInspector,LabelText("引用值")]
        public T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                /*if (HasValue) { return _value; }*/
/*                if (HasValue&& safe) return _value;
                UpdateValueBySource();*/
                return _value;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            { _SetDirtyInternal(_value = value); }
        }
        [SerializeField,HideInInspector]
        private T _value;
        private float lastTime = -1;
        [ReadOnly,LabelText("已赋值")]
        public bool HasValue = false;
        private bool safe = true;
        public ESReferLazy() { }
        public ESReferLazy(Func<T> func) { SetValueSourceGetter(func); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(T t, bool internalSet = false)
        {
            if (internalSet) _value = t;
            else Value = t;
            if (t != null)
            {
                Debug.Log(t+"NULL???");
                HasValue = true;
            }
        }
        public Func<T> ValueSourceGetter;
        public Action<T> ValueToDo;
        private void UpdateValueBySource()
        {
            if (Time.time - lastTime > 0.5f)
            {
                _SetDirtyInternal(_value = ValueSourceGetter?.Invoke());
                lastTime = Time.time;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValueSourceGetter(Func<T> func)
        {
            ValueSourceGetter = func;
            
        }
        public void SetValueToDO(Action<T> todo)
        {
            
            if (HasValue)
            {
                ValueToDo?.Invoke(_value);
            }
            else
            {
                ValueToDo = todo;
            }
        }
        public void AddValueToDO(Action<T> todo)
        {
            
            if (HasValue)
            {
                ValueToDo?.Invoke(_value);
            }
            else
            {
                ValueToDo += todo;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(ESReferLazy<T> from)
        {
            return from.Value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator bool(ESReferLazy<T> from)
        {
            return from != null;
        }
        private void _SetDirtyInternal(T who)
        {
            if (!HasValue&& who!=null)
            {
                HasValue = true;
                ValueToDo?.Invoke(who);
            }
            else HasValue = false;
        }
        public void SetDirty()
        {
            if (!HasValue && _value!=null)

            {
                HasValue = true;
                ValueToDo?.Invoke(_value);
            }
            else HasValue = false;
        }
        public void SetSafeMode(bool safe=false)
        {
            this.safe = safe;
        }
        public static bool operator !=(ESReferLazy<T> a, ESReferLazy<T> b)
        {
            if (a.safe && a.HasValue) return true;
            a.UpdateValueBySource();
            return a.HasValue;
            // 自定义NULL判断逻辑（例如检查内部GameObject是否激活）
        }
        public static bool operator !=(ESReferLazy<T> a,object b)
        {
            if (a.safe && a.HasValue) return true;
            a.UpdateValueBySource();
            return a.HasValue;
            // 自定义NULL判断逻辑（例如检查内部GameObject是否激活）
        }
        public static bool operator ==(ESReferLazy<T> a, ESReferLazy<T> b)
        {
            if (a.safe && a.HasValue) return false;
            a.UpdateValueBySource();
            return !a.HasValue;
        }
        public static bool operator ==(ESReferLazy<T> a, object b)
        {
            if (a.safe && a.HasValue) return false;
            a.UpdateValueBySource();
            return !a.HasValue;
        }
        public override bool Equals(object obj)
        {
            if (obj is T other)
                return this.Value == other;
            if (obj is ESReferLazy<T> refer)
                return this.Value == refer.Value;
            return false;
        }
        public override int GetHashCode()
        {
            return Value != null ? Value.GetHashCode() : 0;
        }
    }
}
