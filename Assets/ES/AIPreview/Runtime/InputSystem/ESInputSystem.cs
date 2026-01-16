using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ES.Preview.InputSystem
{
    /// <summary>
    /// 通用输入系统
    /// 
    /// **核心功能**：
    /// - 跨平台支持（键鼠、手柄、触屏）
    /// - 输入重绑定
    /// - 输入上下文（gameplay, UI, menu）
    /// - 动作缓冲
    /// - 组合键支持
    /// </summary>
    /// 
    #region Input Action
    
    /// <summary>
    /// 输入动作定义
    /// </summary>
    [CreateAssetMenu(menuName = "ES/Input/Input Action")]
    public class ESInputAction : ScriptableObject
    {
        public string actionName;
        public InputActionType actionType;
        public List<InputBinding> bindings = new();
        
        [Header("Settings")]
        public float holdDuration = 0.3f;      // 长按判定时间
        public float doublePressWindow = 0.3f;  // 双击判定窗口
    }
    
    public enum InputActionType
    {
        Button,         // 按钮（按下/释放）
        Axis,           // 轴（-1到1）
        Vector2         // 2D向量（如摇杆）
    }
    
    [System.Serializable]
    public class InputBinding
    {
        public InputDeviceType deviceType;
        public string inputPath;                // 如 "Keyboard/Space", "Mouse/LeftButton", "Gamepad/ButtonSouth"
        public List<string> modifiers = new();  // 修饰键，如 "Keyboard/LeftShift"
        
        [Header("Gamepad Axis")]
        public bool isAxis;
        public bool invertAxis;
    }
    
    public enum InputDeviceType
    {
        Keyboard,
        Mouse,
        Gamepad,
        Touch
    }
    
    #endregion
    
    #region Input Context
    
    /// <summary>
    /// 输入上下文（决定哪些输入处于激活状态）
    /// </summary>
    [CreateAssetMenu(menuName = "ES/Input/Input Context")]
    public class ESInputContext : ScriptableObject
    {
        public string contextName;
        public List<ESInputAction> actions = new();
        public int priority = 0;  // 优先级（高优先级上下文阻止低优先级）
    }
    
    #endregion
    
    #region Input Manager
    
    /// <summary>
    /// 输入管理器
    /// </summary>
    public class ESInputManager : MonoBehaviour
    {
        private static ESInputManager instance;
        public static ESInputManager Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("ESInputManager");
                    instance = go.AddComponent<ESInputManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }
        
        [Header("Input Contexts")]
        public List<ESInputContext> registeredContexts = new();
        private Stack<ESInputContext> activeContexts = new();
        
        [Header("Buffer Settings")]
        public bool enableActionBuffer = true;
        public float bufferWindow = 0.15f;
        
        // 输入状态
        private Dictionary<ESInputAction, InputState> inputStates = new();
        
        // 事件回调
        private Dictionary<string, Action> buttonPressCallbacks = new();
        private Dictionary<string, Action> buttonReleaseCallbacks = new();
        private Dictionary<string, Action<float>> axisCallbacks = new();
        private Dictionary<string, Action<Vector2>> vector2Callbacks = new();
        
        // 动作缓冲
        private Queue<BufferedAction> actionBuffer = new();
        
        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }
        
        void Update()
        {
            ProcessInput();
            ProcessActionBuffer();
        }
        
        private void ProcessInput()
        {
            if (activeContexts.Count == 0)
                return;
            
            var currentContext = activeContexts.Peek();
            
            foreach (var action in currentContext.actions)
            {
                if (!inputStates.ContainsKey(action))
                {
                    inputStates[action] = new InputState();
                }
                
                var state = inputStates[action];
                
                switch (action.actionType)
                {
                    case InputActionType.Button:
                        ProcessButtonAction(action, state);
                        break;
                    case InputActionType.Axis:
                        ProcessAxisAction(action, state);
                        break;
                    case InputActionType.Vector2:
                        ProcessVector2Action(action, state);
                        break;
                }
            }
        }
        
        private void ProcessButtonAction(ESInputAction action, InputState state)
        {
            bool isPressed = false;
            
            foreach (var binding in action.bindings)
            {
                // 检查修饰键
                bool modifiersPressed = true;
                foreach (var modifier in binding.modifiers)
                {
                    if (!IsKeyPressed(modifier))
                    {
                        modifiersPressed = false;
                        break;
                    }
                }
                
                if (!modifiersPressed)
                    continue;
                
                // 检查主键
                if (IsKeyPressed(binding.inputPath))
                {
                    isPressed = true;
                    break;
                }
            }
            
            // 状态转换
            if (isPressed && !state.isPressed)
            {
                // 按下
                state.isPressed = true;
                state.pressTime = Time.time;
                OnButtonPress(action.actionName);
            }
            else if (!isPressed && state.isPressed)
            {
                // 释放
                state.isPressed = false;
                OnButtonRelease(action.actionName);
            }
            
            // 长按检测
            if (state.isPressed && (Time.time - state.pressTime) > action.holdDuration && !state.holdTriggered)
            {
                state.holdTriggered = true;
                OnButtonHold(action.actionName);
            }
        }
        
        private void ProcessAxisAction(ESInputAction action, InputState state)
        {
            float axisValue = 0f;
            
            foreach (var binding in action.bindings)
            {
                float value = GetAxisValue(binding.inputPath);
                if (binding.invertAxis)
                    value = -value;
                
                if (Mathf.Abs(value) > Mathf.Abs(axisValue))
                {
                    axisValue = value;
                }
            }
            
            state.axisValue = axisValue;
            OnAxisUpdate(action.actionName, axisValue);
        }
        
        private void ProcessVector2Action(ESInputAction action, InputState state)
        {
            Vector2 vector = Vector2.zero;
            
            // 查找X和Y轴绑定
            foreach (var binding in action.bindings)
            {
                if (binding.inputPath.Contains("Horizontal"))
                {
                    vector.x = GetAxisValue(binding.inputPath);
                }
                else if (binding.inputPath.Contains("Vertical"))
                {
                    vector.y = GetAxisValue(binding.inputPath);
                }
            }
            
            state.vector2Value = vector;
            OnVector2Update(action.actionName, vector);
        }
        
        #region Platform-Specific Input
        
        private bool IsKeyPressed(string inputPath)
        {
            var parts = inputPath.Split('/');
            if (parts.Length != 2)
                return false;
            
            string device = parts[0];
            string key = parts[1];
            
            switch (device)
            {
                case "Keyboard":
                    if (Enum.TryParse<KeyCode>(key, out var keyCode))
                        return Input.GetKey(keyCode);
                    break;
                    
                case "Mouse":
                    switch (key)
                    {
                        case "LeftButton": return Input.GetMouseButton(0);
                        case "RightButton": return Input.GetMouseButton(1);
                        case "MiddleButton": return Input.GetMouseButton(2);
                    }
                    break;
                    
                case "Gamepad":
                    // 使用新Input System或手柄按钮映射
                    return Input.GetButton(key);
            }
            
            return false;
        }
        
        private float GetAxisValue(string inputPath)
        {
            var parts = inputPath.Split('/');
            if (parts.Length != 2)
                return 0f;
            
            string device = parts[0];
            string axis = parts[1];
            
            switch (device)
            {
                case "Keyboard":
                    // 键盘模拟轴（WASD）
                    if (axis == "Horizontal")
                    {
                        float h = 0f;
                        if (Input.GetKey(KeyCode.A)) h -= 1f;
                        if (Input.GetKey(KeyCode.D)) h += 1f;
                        return h;
                    }
                    else if (axis == "Vertical")
                    {
                        float v = 0f;
                        if (Input.GetKey(KeyCode.S)) v -= 1f;
                        if (Input.GetKey(KeyCode.W)) v += 1f;
                        return v;
                    }
                    break;
                    
                case "Gamepad":
                    return Input.GetAxis(axis);
                    
                case "Mouse":
                    if (axis == "X") return Input.GetAxis("Mouse X");
                    if (axis == "Y") return Input.GetAxis("Mouse Y");
                    break;
            }
            
            return 0f;
        }
        
        #endregion
        
        #region Action Buffer
        
        private void ProcessActionBuffer()
        {
            if (!enableActionBuffer)
                return;
            
            // 移除过期的缓冲动作
            while (actionBuffer.Count > 0 && (Time.time - actionBuffer.Peek().timestamp) > bufferWindow)
            {
                actionBuffer.Dequeue();
            }
        }
        
        public void BufferAction(string actionName)
        {
            actionBuffer.Enqueue(new BufferedAction
            {
                actionName = actionName,
                timestamp = Time.time
            });
        }
        
        public bool ConsumeBufferedAction(string actionName)
        {
            foreach (var action in actionBuffer)
            {
                if (action.actionName == actionName && !action.consumed)
                {
                    action.consumed = true;
                    return true;
                }
            }
            return false;
        }
        
        #endregion
        
        #region Context Management
        
        public void PushContext(ESInputContext context)
        {
            activeContexts.Push(context);
            
            // 初始化新上下文的输入状态
            foreach (var action in context.actions)
            {
                if (!inputStates.ContainsKey(action))
                {
                    inputStates[action] = new InputState();
                }
            }
        }
        
        public void PopContext()
        {
            if (activeContexts.Count > 0)
            {
                activeContexts.Pop();
            }
        }
        
        public void SetContext(ESInputContext context)
        {
            activeContexts.Clear();
            PushContext(context);
        }
        
        #endregion
        
        #region Event Registration
        
        public void RegisterButtonPress(string actionName, Action callback)
        {
            if (!buttonPressCallbacks.ContainsKey(actionName))
                buttonPressCallbacks[actionName] = null;
            buttonPressCallbacks[actionName] += callback;
        }
        
        public void UnregisterButtonPress(string actionName, Action callback)
        {
            if (buttonPressCallbacks.ContainsKey(actionName))
                buttonPressCallbacks[actionName] -= callback;
        }
        
        public void RegisterAxis(string actionName, Action<float> callback)
        {
            if (!axisCallbacks.ContainsKey(actionName))
                axisCallbacks[actionName] = null;
            axisCallbacks[actionName] += callback;
        }
        
        private void OnButtonPress(string actionName)
        {
            Debug.Log($"Button pressed: {actionName}");
            buttonPressCallbacks.TryGetValue(actionName, out var callback);
            callback?.Invoke();
            
            // 添加到缓冲区
            if (enableActionBuffer)
            {
                BufferAction(actionName);
            }
        }
        
        private void OnButtonRelease(string actionName)
        {
            buttonReleaseCallbacks.TryGetValue(actionName, out var callback);
            callback?.Invoke();
        }
        
        private void OnButtonHold(string actionName)
        {
            Debug.Log($"Button held: {actionName}");
        }
        
        private void OnAxisUpdate(string actionName, float value)
        {
            axisCallbacks.TryGetValue(actionName, out var callback);
            callback?.Invoke(value);
        }
        
        private void OnVector2Update(string actionName, Vector2 value)
        {
            vector2Callbacks.TryGetValue(actionName, out var callback);
            callback?.Invoke(value);
        }
        
        #endregion
        
        #region Rebinding
        
        public void RebindAction(ESInputAction action, InputBinding newBinding)
        {
            if (action.bindings.Count > 0)
            {
                action.bindings[0] = newBinding;
            }
            else
            {
                action.bindings.Add(newBinding);
            }
        }
        
        #endregion
    }
    
    #endregion
    
    #region Helper Classes
    
    public class InputState
    {
        public bool isPressed;
        public float pressTime;
        public bool holdTriggered;
        public float axisValue;
        public Vector2 vector2Value;
    }
    
    public class BufferedAction
    {
        public string actionName;
        public float timestamp;
        public bool consumed;
    }
    
    #endregion
    
    #region Usage Example
    
    public class PlayerController : MonoBehaviour
    {
        public ESInputContext gameplayContext;
        
        void Start()
        {
            // 设置输入上下文
            ESInputManager.Instance.SetContext(gameplayContext);
            
            // 注册输入回调
            ESInputManager.Instance.RegisterButtonPress("Jump", OnJump);
            ESInputManager.Instance.RegisterButtonPress("Attack", OnAttack);
            ESInputManager.Instance.RegisterAxis("MoveHorizontal", OnMoveHorizontal);
        }
        
        private void OnJump()
        {
            Debug.Log("Jump!");
            // 跳跃逻辑
        }
        
        private void OnAttack()
        {
            Debug.Log("Attack!");
            // 攻击逻辑
        }
        
        private void OnMoveHorizontal(float value)
        {
            transform.Translate(Vector3.right * value * Time.deltaTime * 5f);
        }
    }
    
    #endregion
}
