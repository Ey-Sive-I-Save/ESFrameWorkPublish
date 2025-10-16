using System;
using System.Collections.Generic;
namespace FlyRabbit.EventCenter
{
    public delegate void Listener();
    public delegate void Listener<T>(T Parameter);
    public delegate void Listener<T1, T2>(T1 Parameter1, T2 Parameter2);
    public delegate void Listener<T1, T2, T3>(T1 Parameter1, T2 Parameter2, T3 Parameter3);
    public delegate void Listener<T1, T2, T3, T4>(T1 Parameter1, T2 Parameter2, T3 Parameter3, T4 Parameter4);
    public delegate void Listener<T1, T2, T3, T4, T5>(T1 Parameter1, T2 Parameter2, T3 Parameter3, T4 Parameter4, T5 Parameter5);
    public static class EventCenter
    {
        /// <summary>
        /// 存储了所有事件的监听
        /// </summary>
        private static readonly Dictionary<EventName, Delegate> m_EventTable = new Dictionary<EventName, Delegate>();

        #region 添加监听
        /// <summary>
        /// 对没有参数的事件添加监听
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="listener"></param>
        public static void AddListener(EventName eventName, Listener listener)
        {
            AddListenerInternal(eventName, listener);
        }
        /// <summary>
        /// 对有一个参数的事件添加监听
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="listener"></param>
        public static void AddListener<T>(EventName eventName, Listener<T> listener)
        {
            AddListenerInternal(eventName, listener);
        }
        /// <summary>
        /// 对有两个参数的事件添加监听
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="listener"></param>
        public static void AddListener<T1, T2>(EventName eventName, Listener<T1, T2> listener)
        {
            AddListenerInternal(eventName, listener);
        }
        /// <summary>
        /// 对有三个参数的事件添加监听
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="listener"></param>
        public static void AddListener<T1, T2, T3>(EventName eventName, Listener<T1, T2, T3> listener)
        {
            AddListenerInternal(eventName, listener);
        }
        /// <summary>
        /// 对有四个参数的事件添加监听
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="listener"></param>
        public static void AddListener<T1, T2, T3, T4>(EventName eventName, Listener<T1, T2, T3, T4> listener)
        {
            AddListenerInternal(eventName, listener);
        }
        /// <summary>
        /// 对有五个参数的事件添加监听
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        /// <typeparam name="T5"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="listener"></param>
        public static void AddListener<T1, T2, T3, T4, T5>(EventName eventName, Listener<T1, T2, T3, T4, T5> listener)
        {
            AddListenerInternal(eventName, listener);
        }
        #endregion
        #region 移除监听
        /// <summary>
        /// 对没有参数的事件移除监听
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="listener"></param>
        public static void RemoveListener(EventName eventName, Listener listener)
        {
            RemoveListenerInternal(eventName, listener);
        }
        /// <summary>
        /// 对有一个参数的事件移除监听
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="listener"></param>
        public static void RemoveListener<T>(EventName eventName, Listener<T> listener)
        {
            RemoveListenerInternal(eventName, listener);
        }
        /// <summary>
        /// 对有两个参数的事件移除监听
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="listener"></param>
        public static void RemoveListener<T1, T2>(EventName eventName, Listener<T1, T2> listener)
        {
            RemoveListenerInternal(eventName, listener);
        }
        /// <summary>
        /// 对有三个参数的事件移除监听
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="listener"></param>
        public static void RemoveListener<T1, T2, T3>(EventName eventName, Listener<T1, T2, T3> listener)
        {
            RemoveListenerInternal(eventName, listener);
        }
        /// <summary>
        /// 对有四个参数的事件移除监听
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="listener"></param>
        public static void RemoveListener<T1, T2, T3, T4>(EventName eventName, Listener<T1, T2, T3, T4> listener)
        {
            RemoveListenerInternal(eventName, listener);
        }
        /// <summary>
        /// 对有五个参数的事件移除监听
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        /// <typeparam name="T5"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="listener"></param>
        public static void RemoveListener<T1, T2, T3, T4, T5>(EventName eventName, Listener<T1, T2, T3, T4, T5> listener)
        {
            RemoveListenerInternal(eventName, listener);
        }
        #endregion
        #region 触发事件
        /// <summary>
        /// 触发没有参数的事件
        /// </summary>
        /// <param name="eventName"></param>
        public static void TriggerEvent(EventName eventName)
        {
            if (IsNotRegisteredOrEmpty(eventName))
            {
                return;
            }
            if (m_EventTable[eventName] is not Listener listener)
            {
                ReportError($"[触发事件错误]，类型不匹配,\n事件名{eventName},事件类型:{m_EventTable[eventName].GetType()},监听类型：{typeof(Listener)}");
                return;
            }
            listener.Invoke();
        }
        /// <summary>
        /// 触发有一个参数的事件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="Parameter"></param>
        public static void TriggerEvent<T>(EventName eventName, T Parameter)
        {
            if (IsNotRegisteredOrEmpty(eventName))
            {
                return;
            }
            if (m_EventTable[eventName] is not Listener<T> listener)
            {
                ReportError($"[触发事件错误]，类型不匹配,\n事件名{eventName},事件类型:{m_EventTable[eventName].GetType()},监听类型：{typeof(Listener)}");
                return;
            }
            listener.Invoke(Parameter);
        }
        /// <summary>
        /// 触发有两个参数的事件
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="Parameter1"></param>
        /// <param name="Parameter2"></param>
        public static void TriggerEvent<T1, T2>(EventName eventName, T1 Parameter1, T2 Parameter2)
        {
            if (IsNotRegisteredOrEmpty(eventName))
            {
                return;
            }
            if (m_EventTable[eventName] is not Listener<T1, T2> listener)
            {
                ReportError($"[触发事件错误]，类型不匹配,\n事件名{eventName},事件类型:{m_EventTable[eventName].GetType()},监听类型：{typeof(Listener)}");
                return;
            }
            listener.Invoke(Parameter1, Parameter2);
        }
        /// <summary>
        /// 触发有三个参数的事件
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="Parameter1"></param>
        /// <param name="Parameter2"></param>
        /// <param name="Parameter3"></param>
        public static void TriggerEvent<T1, T2, T3>(EventName eventName, T1 Parameter1, T2 Parameter2, T3 Parameter3)
        {
            if (IsNotRegisteredOrEmpty(eventName))
            {
                return;
            }
            if (m_EventTable[eventName] is not Listener<T1, T2, T3> listener)
            {
                ReportError($"[触发事件错误]，类型不匹配,\n事件名{eventName},事件类型:{m_EventTable[eventName].GetType()},监听类型：{typeof(Listener)}");
                return;
            }
            listener.Invoke(Parameter1, Parameter2, Parameter3);
        }
        /// <summary>
        /// 触发有四个参数的事件
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="Parameter1"></param>
        /// <param name="Parameter2"></param>
        /// <param name="Parameter3"></param>
        /// <param name="Parameter4"></param>
        public static void TriggerEvent<T1, T2, T3, T4>(EventName eventName, T1 Parameter1, T2 Parameter2, T3 Parameter3, T4 Parameter4)
        {
            if (IsNotRegisteredOrEmpty(eventName))
            {
                return;
            }
            if (m_EventTable[eventName] is not Listener<T1, T2, T3, T4> listener)
            {
                ReportError($"[触发事件错误]，类型不匹配,\n事件名{eventName},事件类型:{m_EventTable[eventName].GetType()},监听类型：{typeof(Listener)}");
                return;
            }
            listener.Invoke(Parameter1, Parameter2, Parameter3, Parameter4);
        }
        /// <summary>
        /// 触发有五个参数的事件
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        /// <typeparam name="T5"></typeparam>
        /// <param name="eventName"></param>
        /// <param name="Parameter1"></param>
        /// <param name="Parameter2"></param>
        /// <param name="Parameter3"></param>
        /// <param name="Parameter4"></param>
        /// <param name="Parameter5"></param>
        public static void TriggerEvent<T1, T2, T3, T4, T5>(EventName eventName, T1 Parameter1, T2 Parameter2, T3 Parameter3, T4 Parameter4, T5 Parameter5)
        {
            if (IsNotRegisteredOrEmpty(eventName))
            {
                return;
            }
            if (m_EventTable[eventName] is not Listener<T1, T2, T3, T4, T5> listener)
            {
                ReportError($"[触发事件错误]，类型不匹配,\n事件名{eventName},事件类型:{m_EventTable[eventName].GetType()},监听类型：{typeof(Listener)}");
                return;
            }
            listener.Invoke(Parameter1, Parameter2, Parameter3, Parameter4, Parameter5);
        }
        #endregion

        #region Other API
        /// <summary>
        /// 事件是否未注册或者监听为空
        /// </summary>
        /// <param name="eventName"></param>
        /// <returns></returns>
        public static bool IsNotRegisteredOrEmpty(EventName eventName)
        {
            if (m_EventTable.ContainsKey(eventName) == false)
            {
                return true;
            }
            if (m_EventTable[eventName] == null)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 清除所有监听为空的事件。
        /// </summary>
        /// <remarks>
        /// 绝大多数时候你不必调用这个方法，即便有很多空事件，也占不了多少内存。
        /// </remarks>
        public static void ClearEmptyEvent()
        {
            List<EventName> remove = new List<EventName>(m_EventTable.Count);
            foreach (var item in m_EventTable)
            {
                if (item.Value == null)
                {
                    remove.Add(item.Key);
                }
            }
            foreach (var item in remove)
            {
                m_EventTable.Remove(item);
            }
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 添加监听的内部方法
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="listener"></param>
        private static void AddListenerInternal(EventName eventName, Delegate listener)
        {
            if (listener == null)
            {
                ReportError("[添加监听错误]：监听不能为空。");
                return;
            }
            if (IsNotRegisteredOrEmpty(eventName))
            {
                m_EventTable[eventName] = listener;
                return;
            }
            if (m_EventTable[eventName].GetType() != listener.GetType())
            {
                ReportError($"[添加监听错误]：类型不匹配，\n事件名：{eventName},事件类型：{m_EventTable[eventName].GetType()},监听类型：{listener.GetType()}");
                return;
            }
            m_EventTable[eventName] = Delegate.Combine(m_EventTable[eventName], listener);
        }
        /// <summary>
        /// 移除监听的内部方法
        /// </summary>
        /// <param name="eventName"></param>
        /// <param name="listener"></param>
        private static void RemoveListenerInternal(EventName eventName, Delegate listener)
        {
            if (listener == null)
            {
                ReportError("[移除监听错误]：监听不能为空。");
                return;
            }
            //这种情况要不要报错呢?
            if (IsNotRegisteredOrEmpty(eventName))
            {
                return;
            }
            if (m_EventTable[eventName].GetType() != listener.GetType())
            {
                ReportError($"[移除监听错误]：类型不匹配，\n事件名：{eventName},事件类型：{m_EventTable[eventName].GetType()},监听类型：{listener.GetType()}");
                return;
            }
            m_EventTable[eventName] = Delegate.Remove(m_EventTable[eventName], listener);
        }
        /// <summary>
        /// 报错
        /// </summary>
        private static void ReportError(string message)
        {
            throw new EventCenterException(message);
            //如果你认为不该直接抛出异常，可改为打印日志等其他报错方式
            //Debug.LogError(message);
        }
        #endregion
    }
    public class EventCenterException : Exception
    {
        public EventCenterException(string message) : base(message) { }
    }
}