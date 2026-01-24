using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES {
    /// <summary>
    /// ES框架菜单项路径定义
    /// 统一管理所有MenuItem和CreateAssetMenu的路径常量
    /// </summary>
    public static class MenuItemPathDefine 
    {
        // ============ 顶级菜单 ============
        public const string ROOT_MENU = "【ES】";
        
        // ============ 二级菜单 ============
        public const string INSTALL_DEPENDENCY = "【安装与依赖】";
        public const string EDITOR_TOOLS = "【窗口】";
        public const string VMCP_SYSTEM = "【VMCP系统】";
        public const string ASSET_CREATION = "【资产创建】";
        public const string TEST_TOOLS = "测试工具";
        
        // ============ 三级菜单 ============
        // 编辑器工具
        public const string EDITOR_DOCS = "【文档】";
        
        // VMCP系统
        public const string VMCP_ASSET_MANAGEMENT = "【资产管理】";
        public const string VMCP_ASSET_CREATION = "【资产创建】";
        public const string VMCP_SYSTEM_MANAGEMENT = "【系统管理】";
        public const string VMCP_HELP = "【帮助】";
        
        // 资产创建
        public const string ASSET_GLOBAL_SO = "全局SO";
        public const string ASSET_DEV_MANAGEMENT = "开发管理";
        public const string ASSET_DOCUMENTATION = "文档";
        
        // ============ 完整路径组合 ============
        // 顶级路径
        public const string ROOT_PATH = ROOT_MENU + "/";
        
        // 安装与依赖
        public const string INSTALL_DEPENDENCY_PATH = ROOT_PATH + INSTALL_DEPENDENCY + "/";
        
        // 编辑器工具
        public const string EDITOR_TOOLS_PATH = ROOT_PATH + EDITOR_TOOLS + "/";
        public const string EDITOR_DOCS_PATH = EDITOR_TOOLS_PATH + EDITOR_DOCS + "/";
        
        // VMCP系统
        public const string VMCP_SYSTEM_PATH = ROOT_PATH + VMCP_SYSTEM + "/";
        public const string VMCP_ASSET_MANAGEMENT_PATH = VMCP_SYSTEM_PATH + VMCP_ASSET_MANAGEMENT + "/";
        public const string VMCP_ASSET_CREATION_PATH = VMCP_SYSTEM_PATH + VMCP_ASSET_CREATION + "/";
        public const string VMCP_SYSTEM_MANAGEMENT_PATH = VMCP_SYSTEM_PATH + VMCP_SYSTEM_MANAGEMENT + "/";
        public const string VMCP_HELP_PATH = VMCP_SYSTEM_PATH + VMCP_HELP + "/";
        
        // 资产创建
        public const string ASSET_CREATION_PATH = ROOT_PATH + ASSET_CREATION + "/";
        public const string ASSET_GLOBAL_SO_PATH = ASSET_CREATION_PATH + ASSET_GLOBAL_SO + "/";
        public const string ASSET_DEV_MANAGEMENT_PATH = ASSET_CREATION_PATH + ASSET_DEV_MANAGEMENT + "/";
        public const string ASSET_DOCUMENTATION_PATH = ASSET_CREATION_PATH + ASSET_DOCUMENTATION + "/";
        
        // 测试工具
        public const string TEST_TOOLS_PATH = ROOT_PATH + TEST_TOOLS + "/";
    }
}
