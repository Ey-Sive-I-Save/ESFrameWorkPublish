using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES 
{
    [Serializable]
    public sealed class ESReferLazy<T> where T : class
    {
        #region 基本构造
        [ShowInInspector, LabelText("引用值")]
        public T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (!init)
                {
                    Init();
                }
                return _value;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if(_value!=value)//只有不同时进行测试
                _SetDirtyInternal(_value = value);
            }
        }
        [SerializeField, HideInInspector]
        private T _value;
        /// <summary>
        /// 上一次更新时机
        /// </summary>
        private float lastUpdateTime = -1;
        /// <summary>
        /// 已经完成有效赋值
        /// </summary>
        public bool HasValidValue = false;
        /// <summary>
        /// 安全模式--如果打开会直接停止间隔时间-进行刷新
        /// </summary>
        private bool safeMode = true;
        private bool init = false;
        #endregion

        #region 构造和初始化方法
        public ESReferLazy() { }
        public ESReferLazy(Func<T> func) { SetValueSourceGetter(func); }
        /// <summary>
        /// 直接设置值
        /// </summary>
        /// <param name="t"></param>
        /// <param name="internalSet"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(T t, bool internalSet = false)
        {
            if (internalSet) _value = t;
            else Value = t;
        }
        /// <summary>
        /// 设置赋值源函数
        /// </summary>
        /// <param name="func"></param>
        public void SetValueSourceGetter(Func<T> func)
        {
            ValueSourceGetter = func;
            if (HasValidValue) _UpdateValueBySource();//需要刷新
        }
        /// <summary>
        /// 设置等待赋值要做的事情
        /// </summary>
        /// <param name="todo"></param>
        public void SetToDOForValue(Action<T> todo)
        {
            if (todo != null)
            {
                ToDoForValue = todo;
                if (HasValidValue)
                {
                    todo?.Invoke(_value);
                }
            }
        }
        /// <summary>
        /// 添加等待赋值要做的事情
        /// </summary>
        /// <param name="todo"></param>
        public void AddToDOForValue(Action<T> todo)
        {
            if (todo != null)
            {
                ToDoForValue += todo;
                if (HasValidValue)
                {
                    todo.Invoke(_value);
                }
            }
        }
        /// <summary>
        /// 设置安全模式(如果不安全,即使被赋值，也会间断检测,打开安全模式则会放弃不间断检测)
        /// </summary>
        public void SetSafeMode(bool safe = false)
        {
            this.safeMode = safe;
        }
        
        #endregion

        #region 内置方法
        private Func<T> ValueSourceGetter;
        private Action<T> ToDoForValue;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void _UpdateValueBySource()
        {
            if (Time.time - lastUpdateTime > 1f)
            {
                var v = ValueSourceGetter?.Invoke();
                if (v != _value)
                {
                    _SetDirtyInternal(_value =v );
                }
                lastUpdateTime = Time.time;
            }
        }
        private void _SetDirtyInternal(T who)
        {
            if (!HasValidValue && who != null)
            {
                HasValidValue = true;
                ToDoForValue?.Invoke(who);
            }
            else HasValidValue = false;
        }

        private void Init()
        {
            init = true;
            lastUpdateTime = -1;
            _UpdateValueBySource();
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        }

        private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            lastUpdateTime = -1;
            _UpdateValueBySource();
        }
        #endregion


        #region 转化判空与比较

        /// <summary>
        /// 直接返回值--不安全
        /// </summary>
        /// <param name="from"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(ESReferLazy<T> from)
        {
            return from.Value;
        }
        /// <summary>
        /// 判定值为空--安全
        /// </summary>
        /// <param name="from"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator bool(ESReferLazy<T> from)
        {
            return from != null;
        }
        /// <summary>
        /// 判定不为空
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator !=(ESReferLazy<T> a, ESReferLazy<T> b)
        {
            if (a.safeMode && a.HasValidValue) return true;
            a._UpdateValueBySource();
            return a.HasValidValue;
            // 自定义NULL判断逻辑（例如检查内部GameObject是否激活）
        }
        public static bool operator !=(ESReferLazy<T> a, object b)
        {
            if (a.safeMode && a.HasValidValue) return true;
            a._UpdateValueBySource();
            return a.HasValidValue;
            // 自定义NULL判断逻辑（例如检查内部GameObject是否激活）
        }
        public static bool operator ==(ESReferLazy<T> a, ESReferLazy<T> b)
        {
            if (a.safeMode && a.HasValidValue) return false;
            a._UpdateValueBySource();
            return !a.HasValidValue;
        }
        public static bool operator ==(ESReferLazy<T> a, object b)
        {
            if (a.safeMode && a.HasValidValue) return false;
            a._UpdateValueBySource();
            return !a.HasValidValue;
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
        #endregion
    }
}
