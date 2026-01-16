// using UnityEngine;
// using UnityEngine.UI;
// using System;
// using System.Collections.Generic;
// using System.Linq;

// namespace ES.Preview.UIFramework
// {
//     /// <summary>
//     /// ES UI框架完整实现
//     /// 
//     /// **核心功能**：
//     /// - UI栈管理（导航历史）
//     /// - 路由系统（URL式导航）
//     /// - 数据绑定（单向/双向）
//     /// - 生命周期管理（与Module集成）
//     /// - 资源自动加载/卸载
//     /// </summary>
    
//     #region UI View Base
    
//     /// <summary>
//     /// UI视图基类
//     /// </summary>
//     public abstract class ESUIView : MonoBehaviour, IESModule
//     {
//         [Header("View Configuration")]
//         public string viewId;
//         public UIViewLayer layer = UIViewLayer.Normal;
//         public bool closeOthersOnShow = false;
//         public bool addToNavigationStack = true;
        
//         // Module状态
//         public bool EnabledSelf { get; set; }
//         public bool Signal_IsActiveAndEnable { get; private set; }
//         public bool Signal_HasSubmit { get; private set; }
//         public bool HasStart { get; private set; }
//         public bool HasDestroy { get; private set; }
//         public bool Singal_Dirty { get; set; }
        
//         // 数据上下文
//         protected object dataContext;
        
//         // 生命周期回调
//         public event Action<ESUIView> OnViewShown;
//         public event Action<ESUIView> OnViewHidden;
        
//         public virtual void TryEnableSelf()
//         {
//             if (Signal_IsActiveAndEnable) return;
//             Signal_IsActiveAndEnable = true;
//             gameObject.SetActive(true);
//             OnShow();
//             OnViewShown?.Invoke(this);
//         }
        
//         public virtual void TryDisableSelf()
//         {
//             if (!Signal_IsActiveAndEnable) return;
//             Signal_IsActiveAndEnable = false;
//             OnHide();
//             gameObject.SetActive(false);
//             OnViewHidden?.Invoke(this);
//         }
        
//         public virtual void TryUpdateSelf()
//         {
//             if (!Signal_IsActiveAndEnable) return;
//             OnUpdate();
//         }
        
//         public virtual void TryDestroySelf()
//         {
//             if (HasDestroy) return;
//             HasDestroy = true;
//             OnDestroy();
//             Destroy(gameObject);
//         }
        
//         // 子类重写
//         protected virtual void OnShow() { }
//         protected virtual void OnHide() { }
//         protected virtual void OnUpdate() { }
        
//         /// <summary>
//         /// 设置数据上下文并刷新UI
//         /// </summary>
//         public virtual void SetDataContext(object data)
//         {
//             dataContext = data;
//             OnDataContextChanged();
//         }
        
//         protected virtual void OnDataContextChanged()
//         {
//             // 子类实现数据绑定逻辑
//         }
//     }
    
//     public enum UIViewLayer
//     {
//         Background = 0,    // 背景层（如主菜单背景）
//         Normal = 1,        // 普通UI层
//         Popup = 2,         // 弹窗层
//         System = 3,        // 系统层（如Loading）
//         Tooltip = 4        // 提示层
//     }
    
//     #endregion
    
//     #region UI Manager
    
//     /// <summary>
//     /// UI管理器（单例）
//     /// </summary>
//     public class ESUIManager : MonoBehaviour, IESHosting
//     {
//         private static ESUIManager instance;
//         public static ESUIManager Instance
//         {
//             get
//             {
//                 if (instance == null)
//                 {
//                     var go = new GameObject("ESUIManager");
//                     instance = go.AddComponent<ESUIManager>();
//                     DontDestroyOnLoad(go);
//                 }
//                 return instance;
//             }
//         }
        
//         [Header("UI Canvas")]
//         public Canvas uiCanvas;
//         public Camera uiCamera;
        
//         [Header("Layer Transforms")]
//         public Transform backgroundLayer;
//         public Transform normalLayer;
//         public Transform popupLayer;
//         public Transform systemLayer;
//         public Transform tooltipLayer;
        
//         // 视图缓存
//         private Dictionary<string, ESUIView> viewCache = new();
        
//         // 导航栈
//         private Stack<ESUIView> navigationStack = new();
        
//         // 当前激活的视图
//         private Dictionary<UIViewLayer, List<ESUIView>> activeViews = new();
        
//         // 路由表
//         private Dictionary<string, Type> routeTable = new();
        
//         void Awake()
//         {
//             if (instance != null && instance != this)
//             {
//                 Destroy(gameObject);
//                 return;
//             }
//             instance = this;
            
//             InitializeLayers();
//             RegisterDefaultRoutes();
//         }
        
//         private void InitializeLayers()
//         {
//             if (uiCanvas == null)
//             {
//                 uiCanvas = GetComponentInChildren<Canvas>();
//                 if (uiCanvas == null)
//                 {
//                     var canvasGo = new GameObject("UICanvas");
//                     canvasGo.transform.SetParent(transform);
//                     uiCanvas = canvasGo.AddComponent<Canvas>();
//                     uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
//                     canvasGo.AddComponent<CanvasScaler>();
//                     canvasGo.AddComponent<GraphicRaycaster>();
//                 }
//             }
            
//             // 创建层级
//             backgroundLayer = CreateLayer("BackgroundLayer", 0);
//             normalLayer = CreateLayer("NormalLayer", 1);
//             popupLayer = CreateLayer("PopupLayer", 2);
//             systemLayer = CreateLayer("SystemLayer", 3);
//             tooltipLayer = CreateLayer("TooltipLayer", 4);
            
//             // 初始化字典
//             foreach (UIViewLayer layer in Enum.GetValues(typeof(UIViewLayer)))
//             {
//                 activeViews[layer] = new List<ESUIView>();
//             }
//         }
        
//         private Transform CreateLayer(string layerName, int sortOrder)
//         {
//             var layerGo = new GameObject(layerName);
//             layerGo.transform.SetParent(uiCanvas.transform, false);
            
//             var rectTransform = layerGo.AddComponent<RectTransform>();
//             rectTransform.anchorMin = Vector2.zero;
//             rectTransform.anchorMax = Vector2.one;
//             rectTransform.sizeDelta = Vector2.zero;
            
//             var canvas = layerGo.AddComponent<Canvas>();
//             canvas.overrideSorting = true;
//             canvas.sortingOrder = sortOrder;
            
//             return layerGo.transform;
//         }
        
//         private void RegisterDefaultRoutes()
//         {
//             // 注册路由：URL → View Type
//             // routeTable["main-menu"] = typeof(MainMenuView);
//             // routeTable["settings"] = typeof(SettingsView);
//         }
        
//         /// <summary>
//         /// 显示视图
//         /// </summary>
//         public T ShowView<T>(string viewId = null, object dataContext = null) where T : ESUIView
//         {
//             viewId ??= typeof(T).Name;
            
//             // 检查缓存
//             if (viewCache.TryGetValue(viewId, out var cachedView))
//             {
//                 return ShowExistingView(cachedView, dataContext) as T;
//             }
            
//             // 创建新视图
//             var view = CreateView<T>(viewId);
//             return ShowExistingView(view, dataContext) as T;
//         }
        
//         private T CreateView<T>(string viewId) where T : ESUIView
//         {
//             // 尝试从Resources加载预制体
//             var prefab = Resources.Load<GameObject>($"UI/{viewId}");
            
//             GameObject viewGo;
//             if (prefab != null)
//             {
//                 viewGo = Instantiate(prefab);
//             }
//             else
//             {
//                 // 动态创建
//                 viewGo = new GameObject(viewId);
//                 viewGo.AddComponent<RectTransform>();
//                 viewGo.AddComponent<T>();
//             }
            
//             var view = viewGo.GetComponent<T>();
//             view.viewId = viewId;
            
//             // 设置到对应层级
//             SetViewLayer(view);
            
//             // 缓存
//             viewCache[viewId] = view;
            
//             return view;
//         }
        
//         private ESUIView ShowExistingView(ESUIView view, object dataContext)
//         {
//             // 处理closeOthersOnShow
//             if (view.closeOthersOnShow)
//             {
//                 CloseAllViewsInLayer(view.layer);
//             }
            
//             // 设置数据上下文
//             if (dataContext != null)
//             {
//                 view.SetDataContext(dataContext);
//             }
            
//             // 添加到导航栈
//             if (view.addToNavigationStack && (navigationStack.Count == 0 || navigationStack.Peek() != view))
//             {
//                 navigationStack.Push(view);
//             }
            
//             // 激活视图
//             if (!activeViews[view.layer].Contains(view))
//             {
//                 activeViews[view.layer].Add(view);
//             }
            
//             view.TryEnableSelf();
            
//             return view;
//         }
        
//         private void SetViewLayer(ESUIView view)
//         {
//             Transform parentLayer = view.layer switch
//             {
//                 UIViewLayer.Background => backgroundLayer,
//                 UIViewLayer.Normal => normalLayer,
//                 UIViewLayer.Popup => popupLayer,
//                 UIViewLayer.System => systemLayer,
//                 UIViewLayer.Tooltip => tooltipLayer,
//                 _ => normalLayer
//             };
            
//             view.transform.SetParent(parentLayer, false);
            
//             // 设置RectTransform为全屏
//             var rect = view.GetComponent<RectTransform>();
//             if (rect != null)
//             {
//                 rect.anchorMin = Vector2.zero;
//                 rect.anchorMax = Vector2.one;
//                 rect.sizeDelta = Vector2.zero;
//             }
//         }
        
//         /// <summary>
//         /// 关闭视图
//         /// </summary>
//         public void CloseView(string viewId)
//         {
//             if (viewCache.TryGetValue(viewId, out var view))
//             {
//                 CloseView(view);
//             }
//         }
        
//         public void CloseView(ESUIView view)
//         {
//             view.TryDisableSelf();
            
//             // 从激活列表移除
//             if (activeViews.TryGetValue(view.layer, out var list))
//             {
//                 list.Remove(view);
//             }
            
//             // 从导航栈移除
//             if (navigationStack.Contains(view))
//             {
//                 // 重建栈（移除指定view）
//                 var temp = navigationStack.ToList();
//                 temp.Remove(view);
//                 navigationStack = new Stack<ESUIView>(temp.AsEnumerable().Reverse());
//             }
//         }
        
//         private void CloseAllViewsInLayer(UIViewLayer layer)
//         {
//             if (activeViews.TryGetValue(layer, out var views))
//             {
//                 // 复制列表避免迭代中修改
//                 foreach (var view in views.ToList())
//                 {
//                     CloseView(view);
//                 }
//             }
//         }
        
//         /// <summary>
//         /// 返回上一个视图
//         /// </summary>
//         public void NavigateBack()
//         {
//             if (navigationStack.Count <= 1)
//             {
//                 Debug.LogWarning("No view to navigate back to");
//                 return;
//             }
            
//             var currentView = navigationStack.Pop();
//             CloseView(currentView);
            
//             if (navigationStack.Count > 0)
//             {
//                 var previousView = navigationStack.Peek();
//                 ShowExistingView(previousView, null);
//             }
//         }
        
//         /// <summary>
//         /// 路由导航（URL式）
//         /// </summary>
//         public void Navigate(string route, object dataContext = null)
//         {
//             if (routeTable.TryGetValue(route, out var viewType))
//             {
//                 var method = GetType().GetMethod(nameof(ShowView)).MakeGenericMethod(viewType);
//                 method.Invoke(this, new object[] { route, dataContext });
//             }
//             else
//             {
//                 Debug.LogError($"Route not found: {route}");
//             }
//         }
        
//         /// <summary>
//         /// 注册路由
//         /// </summary>
//         public void RegisterRoute<T>(string route) where T : ESUIView
//         {
//             routeTable[route] = typeof(T);
//         }
        
//         // IESHosting实现
//         public void _TryAddToListOnly(IESModule module) { }
//         public void _TryRemoveFromListOnly(IESModule module) { }
//         public void UpdateAsHosting() { }
//         public void EnableAsHosting() { }
//         public void DisableAsHosting() { }
        
//         void Update()
//         {
//             // 更新所有激活的视图
//             foreach (var layerViews in activeViews.Values)
//             {
//                 foreach (var view in layerViews)
//                 {
//                     view.TryUpdateSelf();
//                 }
//             }
//         }
//     }
    
//     #endregion
    
//     #region Data Binding
    
//     /// <summary>
//     /// 简易数据绑定系统
//     /// </summary>
//     public class ESDataBinding : MonoBehaviour
//     {
//         [Header("Binding Configuration")]
//         public string propertyPath;              // 如 "playerData.name"
//         public UnityEngine.Object targetComponent; // 如 Text组件
//         public string targetProperty;            // 如 "text"
//         public BindingMode mode = BindingMode.OneWay;
        
//         private object dataContext;
//         private System.Reflection.PropertyInfo sourceProperty;
//         private System.Reflection.PropertyInfo targetPropertyInfo;
        
//         public void SetDataContext(object data)
//         {
//             dataContext = data;
//             ResolveProperties();
//             UpdateTarget();
//         }
        
//         private void ResolveProperties()
//         {
//             if (dataContext == null) return;
            
//             // 解析属性路径（支持嵌套）
//             var parts = propertyPath.Split('.');
//             object current = dataContext;
            
//             foreach (var part in parts)
//             {
//                 var type = current.GetType();
//                 sourceProperty = type.GetProperty(part);
//                 if (sourceProperty == null) break;
//                 current = sourceProperty.GetValue(current);
//             }
            
//             // 目标属性
//             if (targetComponent != null)
//             {
//                 targetPropertyInfo = targetComponent.GetType().GetProperty(targetProperty);
//             }
//         }
        
//         private void UpdateTarget()
//         {
//             if (sourceProperty == null || targetPropertyInfo == null) return;
            
//             var value = sourceProperty.GetValue(dataContext);
//             targetPropertyInfo.SetValue(targetComponent, value);
//         }
        
//         void Update()
//         {
//             if (mode == BindingMode.OneWay || mode == BindingMode.TwoWay)
//             {
//                 UpdateTarget();
//             }
//         }
//     }
    
//     public enum BindingMode
//     {
//         OneWay,      // 数据 → UI
//         TwoWay,      // 数据 ↔ UI
//         OneTime      // 仅初始化时绑定
//     }
    
//     #endregion
    
//     #region UI Components
    
//     /// <summary>
//     /// 示例：登录界面
//     /// </summary>
//     public class LoginView : ESUIView
//     {
//         [Header("UI References")]
//         public InputField usernameInput;
//         public InputField passwordInput;
//         public Button loginButton;
//         public Text errorText;
        
//         protected override void OnShow()
//         {
//             loginButton.onClick.AddListener(OnLoginClicked);
//             errorText.gameObject.SetActive(false);
//         }
        
//         protected override void OnHide()
//         {
//             loginButton.onClick.RemoveListener(OnLoginClicked);
//         }
        
//         private void OnLoginClicked()
//         {
//             string username = usernameInput.text;
//             string password = passwordInput.text;
            
//             if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
//             {
//                 ShowError("请输入用户名和密码");
//                 return;
//             }
            
//             // 发送登录事件
//             UISystemEvents.OnLoginAttempt?.Invoke(new LoginData
//             {
//                 username = username,
//                 password = password
//             });
            
//             // 导航到主界面
//             ESUIManager.Instance.Navigate("main-menu");
//         }
        
//         private void ShowError(string message)
//         {
//             errorText.text = message;
//             errorText.gameObject.SetActive(true);
//         }
//     }
    
//     /// <summary>
//     /// 示例：主菜单
//     /// </summary>
//     public class MainMenuView : ESUIView
//     {
//         public Button startButton;
//         public Button settingsButton;
//         public Button quitButton;
        
//         protected override void OnShow()
//         {
//             startButton.onClick.AddListener(() => ESUIManager.Instance.Navigate("game-scene"));
//             settingsButton.onClick.AddListener(() => ESUIManager.Instance.ShowView<SettingsView>());
//             quitButton.onClick.AddListener(Application.Quit);
//         }
//     }
    
//     /// <summary>
//     /// 示例：设置界面
//     /// </summary>
//     public class SettingsView : ESUIView
//     {
//         public Slider volumeSlider;
//         public Toggle fullscreenToggle;
//         public Button backButton;
        
//         protected override void OnShow()
//         {
//             // 加载当前设置
//             volumeSlider.value = AudioListener.volume;
//             fullscreenToggle.isOn = Screen.fullScreen;
            
//             volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
//             fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
//             backButton.onClick.AddListener(() => ESUIManager.Instance.NavigateBack());
//         }
        
//         private void OnVolumeChanged(float value)
//         {
//             AudioListener.volume = value;
//         }
        
//         private void OnFullscreenChanged(bool isOn)
//         {
//             Screen.fullScreen = isOn;
//         }
//     }
    
//     #endregion
    
//     #region UI Events (Link Integration)
    
//     public static class UISystemEvents
//     {
//         public static Action<LoginData> OnLoginAttempt;
//         public static Action<string> OnSceneChangeRequested;
//     }
    
//     public struct LoginData
//     {
//         public string username;
//         public string password;
//     }
    
//     #endregion
// }
