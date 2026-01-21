using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>全局 ScriptableObject 基类（编辑器友好）。在编辑器中可自动管理实例；运行时可使用，但需显式获取引用。</summary>
    /// <remarks>使用 <see cref="EnsureInstance(bool)"/> 创建或查找实例；编辑器交互请用 <see cref="EnsureInstanceInteractive"/>。</remarks>

    public interface IESGlobalData
    {
        /// <summary>
        /// 标记为全局配置数据的接口（用于区分普通 SO 与全局单例 SO）。
        /// </summary>
    }
    public class ESEditorGlobalSo<This> : ESSO,IESGlobalData where This : ESEditorGlobalSo<This>
    {
        /// <summary>
        /// 编辑器全局 SO 的泛型基类。
        /// - 管理同类型的所有实例（`AllCaches`），并支持通过 `HasConfirm` 选中其中一个作为主数据（单例行为）。
        /// - 仅建议在编辑器环境中使用包含交互（如创建对话框）的逻辑；运行时调用应避开 Editor API。
        /// </summary>
        #region 单例声明
        /// <summary>返回当前类型的全局实例；若不存在，仅尝试非交互式查找。</summary>
        public static This Instance
        {
            get
            {
                //已经确定
                if (_instance != null) return _instance;

                // 优先返回已经确认的主数据
                if (AllCaches.Count > 0)
                {
                    foreach (var i in AllCaches)
                    {
                        if (i != null && i.HasConfirm) return _instance = i;
                    }
                    foreach (var i in AllCaches)
                    {
                        if (i != null) return _instance = i.ConfirmThisOnly();
                    }
                }

                // 若仍然为空，尝试确保实例（非交互方式）——不会弹窗
                EnsureInstance(interactive: false);
                return _instance;
            }
            set
            {
                _instance = value;
            }

        }
        /// <summary>
        /// 标记此 SO 是否被选中为主数据（单例）。
        /// 选中后会成为 `Instance` 返回的对象。
        /// </summary>
        [LabelText("被选中为主数据"), BoxGroup("showGlobal", LabelText = "关于全局数据", VisibleIf = "SHOW"), ReadOnly, PropertyOrder(-3)]
        public bool HasConfirm = false;//选定一个
        /// <summary>
        /// 当前被选中的实例引用（编辑器可见）。
        /// 注意：该字段为静态，跨域存在，需要谨慎管理生命周期。
        /// </summary>
        [ShowInInspector, BoxGroup("showGlobal"), LabelText("选中单例"), ReadOnly]
        private static This _instance;
        /// <summary>
        /// 缓存同类型的所有 SO 实例，用于在 `Instance` 获取时搜索可用实例。
        /// 注意：HashSet 在并发场景中不是线程安全的；若有多线程访问请增加同步。
        /// </summary>
        [ShowInInspector, BoxGroup("showGlobal"), LabelText("该类型全部数据"), ListDrawerSettings(HideAddButton = true, HideRemoveButton = true), InlineButton("TryConfirmSwitchThis", "选中为主数据")]
        private static HashSet<This> AllCaches = new HashSet<This>();
        /// <summary>
        /// 当某个 SO 被设置为主数据时广播的回调，用于让其他实例更新状态。
        /// 订阅/取消订阅需要严格匹配以避免重复订阅或内存泄漏。
        /// </summary>
        private static Action<This> OnConfirmOneSO = (who) => { };



        private static Dictionary<Type, bool> HasReactiveTable = new Dictionary<Type, bool>();//敏感互动表(自动创建)
        [NonSerialized]
        public Func<bool> SHOW_Global;
        private bool SHOW()
        {
            return SHOW_Global?.Invoke() ?? true;
        }
        private void OnDestroy()
        {

            // 清理订阅，防止静态回调持有已销毁的实例
            if (HasConfirm)
            {
                HasReactiveTable[typeof(This)] = false;
            }
            // 移除缓存与取消订阅
            AllCaches.Remove(this as This);
            OnConfirmOneSO -= Delegate_OnConfirmOneSO;
        }
        public override void OnEditorInitialized()
        {
            base.OnEditorInitialized();
            if (this is This use)
            {

                if (HasConfirm) _instance = use;
                if (!AllCaches.Contains(use))
                {
                    AllCaches.Add(use);
                    // 订阅变更广播（在 OnDestroy 中会取消订阅）
                    OnConfirmOneSO += Delegate_OnConfirmOneSO;
                    HasReactiveTable[typeof(This)] = false;
                }
            }
        }
        /// <summary>
        /// 运行时唤醒（非编辑器），确保单例实例被设置。
        /// 在运行时应避免依赖 Editor-only API。
        /// </summary>
        public void RunTimeAwake()
        {
            if (_instance != null && _instance != this)
            {
                return;
            }
            if (_instance == null)
            {
                ConfirmThisOnly();
                _instance = this as This;
                // 可在此处添加其他初始化逻辑
            }
        }
        internal This ConfirmThisOnly()
        {
            HasConfirm = true;
            return this as This;
        }
        internal void TryConfirmSwitchThis()
        {
            if (this is This use)
            {

                HasConfirm = true;
                _instance = use;
                OnConfirmOneSO?.Invoke(use);
            }
        }
        private void Delegate_OnConfirmOneSO(This who)
        {
            if (who != this as This)
            {
                HasConfirm = false;
                ESStandUtility.SafeEditor.Wrap_SetDirty(this);
            }
        }
        /// <summary>
        /// 确保存在一个可用的实例。
        /// - <paramref name="interactive"/> 为 true 时在编辑器环境会弹出创建对话框（仅 Editor 可用）。
        /// - 返回找到或新建的实例（找不到时返回 null）。
        /// </summary>
        /// <summary>
        /// 确保存在一个实例；交互模式（仅编辑器）会提示创建。
        /// </summary>
        /// <param name="interactive">是否以交互方式创建（仅编辑器有效）。</param>
        /// <returns>返回实例或 <c>null</c>。</returns>
        public static This EnsureInstance(bool interactive = false)
        {
            if (_instance != null) return _instance;

            // 先从缓存中查找已确认或任意可用实例
            if (AllCaches.Count > 0)
            {
                foreach (var i in AllCaches)
                    if (i != null && i.HasConfirm) return _instance = i;
                foreach (var i in AllCaches)
                    if (i != null) return _instance = i.ConfirmThisOnly();
            }

            bool hasReactive = HasReactiveTable.TryGetValue(typeof(This), out var bo);
            if (!hasReactive || !bo)
            {
                HasReactiveTable[typeof(This)] = true;
            }

#if UNITY_EDITOR
            if (interactive)
            {
                if (EditorUtility.DisplayDialog("全局配置缺失，准备创建", "检测不到可用的【" + typeof(This) + "】SO数据,准备新建一个，是否继续", "创建", "取消"))
                {
                    string path = ESStandUtility.SafeEditor.Wrap_OpenSelectorFolderPanel(title: "创建数据到：");
                    path = path._KeepAfterByLast("Asset", true);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        var first = ESStandUtility.SafeEditor.CreateSOAsset<This>(path, "全局数据" + typeof(This).Name);
                        first.TryConfirmSwitchThis();
                        return _instance = first;
                    }
                }
            }
#endif
            return _instance;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only 便捷调用：以交互方式确保实例存在（会弹窗并可创建新 SO）。
        /// 调用示例：`YourGlobalSoType.EnsureInstanceInteractive();`
        /// </summary>
        /// <summary>Editor-only：交互式确保实例存在（弹窗创建）。</summary>
        /// <returns>返回实例或 <c>null</c>。</returns>
        public static This EnsureInstanceInteractive()
        {
            return EnsureInstance(interactive: true);
        }
#endif
        #endregion
    }
}
