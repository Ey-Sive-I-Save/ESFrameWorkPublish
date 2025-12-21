using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Gaskellgames
{
    #region Base GgEvent class

    public abstract class GgEventBase 
    {
        [SerializeField]
        [Tooltip("If debug is true: info message logs will be displayed in the console. [GgLogs.Info must also be enabled!]")]
        internal bool verboseLogs = false;
        
        [SerializeField]
        [Tooltip("The colour of the verbose logs.")]
        public Color32 logColor = InspectorExtensions.textNormalColor;
        
        [SerializeField]
        [Tooltip("InstanceName used to set the label in the inspector.")]
        internal string instanceName = "";

        [SerializeField, Min(0)]
        [Tooltip("Delay in seconds from this event being called, to this event being invoked.")]
        internal float delay;
        
        /// <summary>
        /// Get/Set the delay in seconds from this event being called, to this event being invoked
        /// </summary>
        public float Delay
        {
            get => delay;
            set => delay = value < 0 ? 0 : value;
        }

        protected CancellationTokenSource TokenSource = new CancellationTokenSource();
        
        /// <summary>
        /// Returns the type information for all args.
        /// </summary>
        /// <returns></returns>
        public abstract Type[] GetArgTypes();
        
        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent) using default args.
        /// </summary>
        internal abstract void InvokeEventWithDefaultArgs();
        
        /// <summary>
        /// Cancel any outstanding delayed invoked callbacks (runtime and persistent).
        /// </summary>
        public void CancelInvoke()
        {
            TokenSource?.Cancel();
            TokenSource = new CancellationTokenSource();
        }

        internal abstract int ListenerCount();
    }

    #endregion

    //----------------------------------------------------------------------------------------------------

    #region zero argument GgEvent class

    /// <summary>
    /// A zero argument persistent callback that can be saved with the Scene.
    /// </summary>
    [Serializable]
    public class GgEvent : GgEventBase
    {
        [SerializeField]
        [Tooltip("UnityEvent used as a base to allow subscribing to and invoking GGEvents")]
        private UnityEvent unityEvent;

        /// <summary>
        /// Returns the type information for all args.
        /// </summary>
        /// <returns></returns>
        public override Type[] GetArgTypes()
        {
            return null;
        }

        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent) using default args.
        /// </summary>
        internal override void InvokeEventWithDefaultArgs()
        {
            Invoke();
        }

        /// <summary>
        /// Get the number of listeners for the event.
        /// </summary>
        /// <returns></returns>
        internal override int ListenerCount()
        {
            return unityEvent.GetPersistentEventCount();
        }

        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent).
        /// </summary>
        public async void Invoke()
        {
            UnityEvent instanceEvent = unityEvent;
            if (verboseLogs && 0 < delay) { GgLogs.Log(logColor, null, GgLogType.Info, "{0} waiting for {1} seconds.", instanceName, delay); }
            
            TaskResultType waitResult = 0 < delay ? await GgTask.WaitForSeconds(delay, TokenSource) : TaskResultType.Complete;
            if (instanceEvent == null) { return; }
            switch (waitResult)
            {
                case TaskResultType.Timeout:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Timeout.", instanceName); }
                    return;
                    
                case TaskResultType.Cancelled:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Cancelled.", instanceName); }
                    return;
                    
                default:
                case TaskResultType.Complete:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Invoked.", instanceName); }
                    instanceEvent?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// Add a non persistent listener to the UnityEvent.
        /// </summary>
        /// <param name="call"></param>
        public void AddListener(UnityAction call)
        {
            unityEvent.AddListener(call);
        }

        /// <summary>
        /// Remove a non persistent listener from the UnityEvent. If you have added the same listener multiple times, this method will remove all occurrences of it.
        /// </summary>
        /// <param name="call"></param>
        public void RemoveListener(UnityAction call)
        {
            unityEvent.RemoveListener(call);
        }

        /// <summary>
        /// Remove all non-persistent (i.e. created from script) listeners from the event.
        /// </summary>
        public void RemoveAllListeners()
        {
            unityEvent.RemoveAllListeners();
        }

        /// <summary>
        /// Get the number of registered persistent listeners.
        /// </summary>
        /// <returns></returns>
        public int GetPersistentEventCount()
        {
            return unityEvent.GetPersistentEventCount();
        }

        /// <summary>
        /// Get the target component of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public Object GetPersistentTarget(int index)
        {
            return unityEvent.GetPersistentTarget(index);
        }

        /// <summary>
        /// Get the target method name of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public string GetPersistentMethodName(int index)
        {
            return unityEvent.GetPersistentMethodName(index);
        }

        /// <summary>
        /// Returns the execution state of a persistent listener
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns>Execution state of the persistent listener.</returns>
        public UnityEventCallState GetPersistentListenerState(int index)
        {
            return unityEvent.GetPersistentListenerState(index);
        }
    }

    #endregion

    //----------------------------------------------------------------------------------------------------

    #region single argument GgEvent class

    /// <summary>
    /// A single argument version of GGEvent.
    /// </summary>
    [Serializable]
    public class GgEvent<T0> : GgEventBase
    {
        [SerializeField]
        [Tooltip("UnityEvent used as a base to allow subscribing to and invoking GGEvents")]
        private UnityEvent<T0> unityEvent;

        /// <summary>
        /// Returns the type information for all args.
        /// </summary>
        /// <returns></returns>
        public override Type[] GetArgTypes()
        {
            return typeof(UnityEvent<T0>).GetGenericArguments();
        }

        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent) using default args.
        /// </summary>
        internal override void InvokeEventWithDefaultArgs() { Invoke(default); }

        /// <summary>
        /// Get the number of listeners for the event.
        /// </summary>
        /// <returns></returns>
        internal override int ListenerCount()
        {
            return unityEvent.GetPersistentEventCount();
        }
        
        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent).
        /// </summary>
        public async void Invoke(T0 arg0)
        {
            UnityEvent<T0> instanceEvent = unityEvent;
            if (verboseLogs && 0 < delay) { GgLogs.Log(logColor, null, GgLogType.Info, "{0} waiting for {1} seconds.", instanceName, delay); }
            
            TaskResultType waitResult = 0 < delay ? await GgTask.WaitForSeconds(delay, TokenSource) : TaskResultType.Complete;
            if (instanceEvent == null) { return; }
            switch (waitResult)
            {
                case TaskResultType.Timeout:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Timeout.", instanceName); }
                    return;
                    
                case TaskResultType.Cancelled:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Cancelled.", instanceName); }
                    return;
                    
                default:
                case TaskResultType.Complete:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Invoked.", instanceName); }
                    instanceEvent?.Invoke(arg0);
                    break;
            }
        }

        /// <summary>
        /// Add a non persistent listener to the UnityEvent.
        /// </summary>
        /// <param name="call"></param>
        public void AddListener(UnityAction<T0> call)
        {
            unityEvent.AddListener(call);
        }

        /// <summary>
        /// Remove a non persistent listener from the UnityEvent. If you have added the same listener multiple times, this method will remove all occurrences of it.
        /// </summary>
        /// <param name="call"></param>
        public void RemoveListener(UnityAction<T0> call)
        {
            unityEvent.RemoveListener(call);
        }

        /// <summary>
        /// Remove all non-persistent (i.e. created from script) listeners from the event.
        /// </summary>
        public void RemoveAllListeners()
        {
            unityEvent.RemoveAllListeners();
        }

        /// <summary>
        /// Get the number of registered persistent listeners.
        /// </summary>
        /// <returns></returns>
        public int GetPersistentEventCount()
        {
            return unityEvent.GetPersistentEventCount();
        }

        /// <summary>
        /// Get the target component of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public Object GetPersistentTarget(int index)
        {
            return unityEvent.GetPersistentTarget(index);
        }

        /// <summary>
        /// Get the target method name of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public string GetPersistentMethodName(int index)
        {
            return unityEvent.GetPersistentMethodName(index);
        }

        /// <summary>
        /// Returns the execution state of a persistent listener
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns>Execution state of the persistent listener.</returns>
        public UnityEventCallState GetPersistentListenerState(int index)
        {
            return unityEvent.GetPersistentListenerState(index);
        }

    } //  class end
    
    #endregion

    //----------------------------------------------------------------------------------------------------

    #region double argument GgEvent class

    /// <summary>
    /// A double argument version of GGEvent.
    /// </summary>
    [Serializable]
    public class GgEvent<T0, T1> : GgEventBase
    {
        [SerializeField]
        [Tooltip("UnityEvent used as a base to allow subscribing to and invoking GGEvents")]
        private UnityEvent<T0, T1> unityEvent;

        /// <summary>
        /// Returns the type information for all args.
        /// </summary>
        /// <returns></returns>
        public override Type[] GetArgTypes()
        {
            return typeof(UnityEvent<T0, T1>).GetGenericArguments();
        }

        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent) using default args.
        /// </summary>
        internal override void InvokeEventWithDefaultArgs() { Invoke(default, default); }

        /// <summary>
        /// Get the number of listeners for the event.
        /// </summary>
        /// <returns></returns>
        internal override int ListenerCount()
        {
            return unityEvent.GetPersistentEventCount();
        }
    
        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent).
        /// </summary>
        public async void Invoke(T0 arg0, T1 args1)
        {
            UnityEvent<T0, T1> instanceEvent = unityEvent;
            if (verboseLogs && 0 < delay) { GgLogs.Log(logColor, null, GgLogType.Info, "{0} waiting for {1} seconds.", instanceName, delay); }
            
            TaskResultType waitResult = 0 < delay ? await GgTask.WaitForSeconds(delay, TokenSource) : TaskResultType.Complete;
            if (instanceEvent == null) { return; }
            switch (waitResult)
            {
                case TaskResultType.Timeout:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Timeout.", instanceName); }
                    return;
                    
                case TaskResultType.Cancelled:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Cancelled.", instanceName); }
                    return;
                    
                default:
                case TaskResultType.Complete:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Invoked.", instanceName); }
                    instanceEvent?.Invoke(arg0, args1);
                    break;
            }
        }

        /// <summary>
        /// Add a non persistent listener to the UnityEvent.
        /// </summary>
        /// <param name="call"></param>
        public void AddListener(UnityAction<T0, T1> call)
        {
            unityEvent.AddListener(call);
        }

        /// <summary>
        /// Remove a non persistent listener from the UnityEvent. If you have added the same listener multiple times, this method will remove all occurrences of it.
        /// </summary>
        /// <param name="call"></param>
        public void RemoveListener(UnityAction<T0, T1> call)
        {
            unityEvent.RemoveListener(call);
        }

        /// <summary>
        /// Remove all non-persistent (i.e. created from script) listeners from the event.
        /// </summary>
        public void RemoveAllListeners()
        {
            unityEvent.RemoveAllListeners();
        }

        /// <summary>
        /// Get the number of registered persistent listeners.
        /// </summary>
        /// <returns></returns>
        public int GetPersistentEventCount()
        {
            return unityEvent.GetPersistentEventCount();
        }

        /// <summary>
        /// Get the target component of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public Object GetPersistentTarget(int index)
        {
            return unityEvent.GetPersistentTarget(index);
        }

        /// <summary>
        /// Get the target method name of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public string GetPersistentMethodName(int index)
        {
            return unityEvent.GetPersistentMethodName(index);
        }

        /// <summary>
        /// Returns the execution state of a persistent listener
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns>Execution state of the persistent listener.</returns>
        public UnityEventCallState GetPersistentListenerState(int index)
        {
            return unityEvent.GetPersistentListenerState(index);
        }

    } //  class end
    
    #endregion

    //----------------------------------------------------------------------------------------------------

    #region triple argument GgEvent class

    /// <summary>
    /// A double argument version of GGEvent.
    /// </summary>
    [Serializable]
    public class GgEvent<T0, T1, T2> : GgEventBase
    {
        [SerializeField]
        [Tooltip("UnityEvent used as a base to allow subscribing to and invoking GGEvents")]
        private UnityEvent<T0, T1, T2> unityEvent;

        /// <summary>
        /// Returns the type information for all args.
        /// </summary>
        /// <returns></returns>
        public override Type[] GetArgTypes()
        {
            return typeof(UnityEvent<T0, T1, T2>).GetGenericArguments();
        }

        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent) using default args.
        /// </summary>
        internal override void InvokeEventWithDefaultArgs() { Invoke(default, default, default); }

        /// <summary>
        /// Get the number of listeners for the event.
        /// </summary>
        /// <returns></returns>
        internal override int ListenerCount()
        {
            return unityEvent.GetPersistentEventCount();
        }
    
        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent).
        /// </summary>
        public async void Invoke(T0 arg0, T1 args1, T2 args2)
        {
            UnityEvent<T0, T1, T2> instanceEvent = unityEvent;
            if (verboseLogs && 0 < delay) { GgLogs.Log(logColor, null, GgLogType.Info, "{0} waiting for {1} seconds.", instanceName, delay); }
            
            TaskResultType waitResult = 0 < delay ? await GgTask.WaitForSeconds(delay, TokenSource) : TaskResultType.Complete;
            if (instanceEvent == null) { return; }
            switch (waitResult)
            {
                case TaskResultType.Timeout:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Timeout.", instanceName); }
                    return;
                    
                case TaskResultType.Cancelled:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Cancelled.", instanceName); }
                    return;
                    
                default:
                case TaskResultType.Complete:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Invoked.", instanceName); }
                    instanceEvent?.Invoke(arg0, args1, args2);
                    break;
            }
        }

        /// <summary>
        /// Add a non persistent listener to the UnityEvent.
        /// </summary>
        /// <param name="call"></param>
        public void AddListener(UnityAction<T0, T1, T2> call)
        {
            unityEvent.AddListener(call);
        }

        /// <summary>
        /// Remove a non persistent listener from the UnityEvent. If you have added the same listener multiple times, this method will remove all occurrences of it.
        /// </summary>
        /// <param name="call"></param>
        public void RemoveListener(UnityAction<T0, T1, T2> call)
        {
            unityEvent.RemoveListener(call);
        }

        /// <summary>
        /// Remove all non-persistent (i.e. created from script) listeners from the event.
        /// </summary>
        public void RemoveAllListeners()
        {
            unityEvent.RemoveAllListeners();
        }

        /// <summary>
        /// Get the number of registered persistent listeners.
        /// </summary>
        /// <returns></returns>
        public int GetPersistentEventCount()
        {
            return unityEvent.GetPersistentEventCount();
        }

        /// <summary>
        /// Get the target component of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public Object GetPersistentTarget(int index)
        {
            return unityEvent.GetPersistentTarget(index);
        }

        /// <summary>
        /// Get the target method name of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public string GetPersistentMethodName(int index)
        {
            return unityEvent.GetPersistentMethodName(index);
        }

        /// <summary>
        /// Returns the execution state of a persistent listener
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns>Execution state of the persistent listener.</returns>
        public UnityEventCallState GetPersistentListenerState(int index)
        {
            return unityEvent.GetPersistentListenerState(index);
        }

    } //  class end
    
    #endregion
    
}
