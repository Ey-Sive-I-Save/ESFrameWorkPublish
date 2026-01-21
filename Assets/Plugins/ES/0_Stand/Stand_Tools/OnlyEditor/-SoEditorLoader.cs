using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 编辑器用的 SO 索引容器。
    /// - <c>SOS</c>：按类型分组的已注册 <see cref="ESSO"/> 实例集合。
    /// - <c>AllSoNames</c>：映射显示名到 ScriptableObject 类型的双向字典，便于菜单/反射使用。
    /// </summary>
    public class ESEditorSO
    {
        /// <summary>按类型分组的已注册 ESSO 实例。</summary>
        public static TypeMatchKeyGroup<ESSO> SOS = new TypeMatchKeyGroup<ESSO>();



        /// <summary>显示名 ↔ 类型 的双向映射（编辑器使用）。</summary>
        public static BidirectionalDictionary<string,Type> AllSoNames=new BidirectionalDictionary<string, Type>();

        /// <summary>仅包含实现 <see cref="IESGlobalData"/> 的全局数据类型映射（显示名 ↔ 类型）。</summary>
        public static BidirectionalDictionary<string, Type> AllGlobalSoNames = new BidirectionalDictionary<string, Type>();
        
        
    }
#if UNITY_EDITOR
    /// <summary>
    /// 编辑器启动时扫描并初始化所有 <see cref="ESSO"/> 资产。
    /// 调用时机：Editor 初始化阶段（继承自 <c>EditorInvoker_Level0</c>）。
    /// </summary>
    public class SoEditorIniter : EditorInvoker_Level0
    {
        /// <summary>查找项目中所有 ESSO 类型资产并调用 <c>OnEditorInitialized</c>。</summary>
        public override void InitInvoke()
        {
            // 清空旧索引后重新注册
            ESEditorSO.SOS.Clear();
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(ESSO)}");

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                ESSO soAsset = AssetDatabase.LoadAssetAtPath<ESSO>(assetPath);
                if (soAsset != null)
                {
                    soAsset.OnEditorInitialized();
                }
            }
        }
    }
    
    /// <summary>
    /// 在编辑器应用阶段调用所有已注册 ESSO 的 <c>OnEditorApply</c> 钩子（例如在保存或刷新时）。
    /// </summary>
    public class SoEditorApplier : EditorInvoker_SoApply
    {
        /// <summary>对已注册的每个 ESSO 实例执行一次 Apply 操作。</summary>
        public override void InitInvoke()
        {
            var keys = ESEditorSO.SOS.Groups.Keys;
            foreach (var i in keys)
            {
                var group = ESEditorSO.SOS.GetGroupDirectly(i);
                foreach (var g in group)
                {
                    g.OnEditorApply();
                }
            }
        }
    }


    /// <summary>
    /// 编辑器注册器：当检测到 ScriptableObject 子类时，将其显示名登记到 <see cref="ESEditorSO.AllSoNames"/>。
    /// 用于在编辑器菜单或选择器中显示友好的类型名。
    /// </summary>
    public class ER_So_SubClass : EditorRegister_FOR_AsSubclass<ScriptableObject>
    {
        public override int Order => 10;

        /// <summary>处理检测到的子类类型并登记显示名与类型的映射。</summary>
        public override void Handle(Type SubClassType)
        {
            var Disname = SubClassType._GetTypeDisplayName();
            ESEditorSO.AllSoNames.Add(Disname, SubClassType);
            // 如果该类型实现了 IESGlobalData，则同时登记到 AllGlobalSoNames（便于区分全局配置类）
            if (typeof(IESGlobalData).IsAssignableFrom(SubClassType))
            {
                ESEditorSO.AllGlobalSoNames.Add(Disname, SubClassType);
            }
        }
    }
    
#endif

}