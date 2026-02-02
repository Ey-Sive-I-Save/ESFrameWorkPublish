using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    // =================================================================================================
    //  SamplesCore 说明（面向AI学习/快速上手）
    // -------------------------------------------------------------------------------------------------
    // 1) Core / Domain / Module 的职责：
    //    - Core：功能入口与统一注册点，决定“哪些域需要参与运行”。
    //    - Domain：同类模块集合与生命周期边界；只负责组织模块，不直接实现功能。
    //    - Module：功能实现单元，具体逻辑在这里。
    //
    // 2) 文件夹与命名规则（本脚本不拆文件，但规则必须被理解）：
    //    - 每个 Core 独立一个文件夹：
    //        Assets/Plugins/ES/1_Design/Core_Domain_Module/<CoreName>/
    //    - Core 脚本放在 Core 文件夹根目录：
    //        <CoreName>Core.cs
    //    - 每个域一个子文件夹：
    //        <CoreName>/<DomainName>/
    //    - 域脚本使用“_开头”确保优先排序：
    //        <CoreName>/<DomainName>/_<DomainName>Domain.cs
    //    - 模块脚本放在域文件夹中：
    //        <CoreName>/<DomainName>/<ModuleName>.cs
    //    - 允许把多个模块合并在同一脚本，甚至合并到域脚本内（示例在本文件中）。
    //
    // 3) “模块实例”来源：
    //    - 不需要在初始化时注入新模块。
    //    - 模块是否存在由编辑器（序列化字段/Inspector）决定。
    //    - Core 只负责注册“域”，域内部自动管理模块实例。
    //
    // 4) 注册规则：
    //    - Core 对域的声明是统一的，且按案例注册（RegisterDomain）。
    //    - 只注册需要参与当前 Core 的域。
    //
    // 5) Domain 内拒绝随意 public 模块字段：
    //    - 默认不允许声明 public 模块字段（避免耦合/误用）。
    //    - 如必须公开，也只能用于“极端高频”且必须禁止序列化，并且需要审查。
    //    - 更推荐：超高频逻辑直接写在 Domain 内部，不拆成 Module。
    //    - Module 的最大价值是“可选存在”：没有它也能正常运行，用于灵活扩展。
    //
    // 6) 域与模块需要注册中文名：
    //    - 使用 [TypeRegistryItem("中文名")] 统一显示与检索。
    //    - 这是编辑器展示与配置检索的关键约定。
    //
    // 7) 更新时序（默认）：
    //    - Core.Update -> Domain.Update -> Module.Update
    //    - Core.OnEnable/OnDisable/OnDestroy 同理递进到 Domain 与 Module
    //
    // 8) TypeRegistryItem + TableKeyType：
    //    - 用于类型注册与检索（例如表驱动/配置驱动）。
    //    - TableKeyType 通常返回自身类型，示例中保持一致。
    // =================================================================================================
    public class SamplesCore : Core
    {
        // ===============================
        // 域声明（只声明，不注入模块）
        // ===============================
        [TabGroup("域", "类型1域"), InlineProperty, HideLabel, SerializeReference]
        public SampleDomainType1 sampleDomainType1;

        [TabGroup("域", "类型2域"), InlineProperty, HideLabel, SerializeReference]
        public SampleDomainType2 sampleDomainType2;

        [TabGroup("域", "类型3域"), InlineProperty, HideLabel, SerializeReference]
        public SampleDomainType3 sampleDomainType3;

        [TabGroup("域", "类型4域"), InlineProperty, HideLabel, SerializeReference]
        public SampleDomainType4 sampleDomainType4;

        // ===============================
        // 常用生命周期重写（示例）
        // 注意时序：Awake -> OnBeforeAwakeRegister -> OnAwakeRegisterOnly -> OnAfterAwakeRegister
        // ===============================
        protected override void OnBeforeAwakeRegister()
        {
            // 这里做：Core 级别的“数据准备/缓存/校验”（不注册域）
            base.OnBeforeAwakeRegister();
        }

        // Core 只负责注册需要参与的域。
        protected override void OnAwakeRegisterOnly()
        {
            base.OnAwakeRegisterOnly();
            RegisterDomain(sampleDomainType1);
            RegisterDomain(sampleDomainType2);
            RegisterDomain(sampleDomainType3);
            RegisterDomain(sampleDomainType4);
        }

        protected override void OnAfterAwakeRegister()
        {
            // 这里做：所有域注册完成后的“统一收尾/依赖检查”
            base.OnAfterAwakeRegister();
        }

        protected override void Update()
        {
            // 如需在域更新前/后插入逻辑，注意调用顺序
            // 前置逻辑...
            base.Update(); // 会驱动所有已注册域的 Update
            // 后置逻辑...
        }

        protected override void OnEnable()
        {
            // Core 启用时会驱动域的 OnEnable
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            // Core 禁用时会驱动域的 OnDisable
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            // Core 销毁时会驱动域的 OnDestroy
            base.OnDestroy();
        }
    }

    // =================================================================================================
    // 域1：示例域（类型1）
    // -------------------------------------------------------------------------------------------------
    // - Domain 继承：Domain<Core, ModuleBase>
    // - ModuleBase 继承：Module<Core, Domain>
    // - 模块是否存在由编辑器配置决定
    // =================================================================================================
    [Serializable]
    public class SampleDomainType1 : Domain<SamplesCore, SampleModuleType1Base>
    {
        // 规则重申：默认不在 Domain 中 public 暴露模块。
    }

    [Serializable]
    public abstract class SampleModuleType1Base : Module<SamplesCore, SampleDomainType1>
    {
    }

    [Serializable, TypeRegistryItem("示例模块类型1——1")]
    public class SampleModuleType1_1 : SampleModuleType1Base
    {
        public override Type TableKeyType => typeof(SampleModuleType1_1);
    }

    [Serializable, TypeRegistryItem("示例模块类型1——2")]
    public class SampleModuleType1_2 : SampleModuleType1Base
    {
        public override Type TableKeyType => typeof(SampleModuleType1_2);
    }

    [Serializable, TypeRegistryItem("示例模块类型1——3")]
    public class SampleModuleType1_3 : SampleModuleType1Base
    {
        public override Type TableKeyType => typeof(SampleModuleType1_3);
    }

    // =================================================================================================
    // 域2：示例域（类型2）
    // -------------------------------------------------------------------------------------------------
    // 说明：模块可以是“配置驱动/表驱动/状态驱动”等风格，这里仅示范结构。
    // =================================================================================================
    [Serializable]
    public class SampleDomainType2 : Domain<SamplesCore, SampleModuleType2Base>
    {
    }

    [Serializable]
    public abstract class SampleModuleType2Base : Module<SamplesCore, SampleDomainType2>
    {
    }

    [Serializable, TypeRegistryItem("示例模块类型2——1")]
    public class SampleModuleType2_1 : SampleModuleType2Base
    {
        public override Type TableKeyType => typeof(SampleModuleType2_1);
    }

    [Serializable, TypeRegistryItem("示例模块类型2——2")]
    public class SampleModuleType2_2 : SampleModuleType2Base
    {
        public override Type TableKeyType => typeof(SampleModuleType2_2);
    }

    [Serializable, TypeRegistryItem("示例模块类型2——3")]
    public class SampleModuleType2_3 : SampleModuleType2Base
    {
        public override Type TableKeyType => typeof(SampleModuleType2_3);
    }

    // =================================================================================================
    // 域3：示例域（类型3）
    // -------------------------------------------------------------------------------------------------
    // 说明：域只负责组织模块，不进行具体逻辑；模块实现功能。
    // =================================================================================================
    [Serializable]
    public class SampleDomainType3 : Domain<SamplesCore, SampleModuleType3Base>
    {
    }

    [Serializable]
    public abstract class SampleModuleType3Base : Module<SamplesCore, SampleDomainType3>
    {
    }

    [Serializable, TypeRegistryItem("示例模块类型3——1")]
    public class SampleModuleType3_1 : SampleModuleType3Base
    {
        public override Type TableKeyType => typeof(SampleModuleType3_1);
    }

    [Serializable, TypeRegistryItem("示例模块类型3——2")]
    public class SampleModuleType3_2 : SampleModuleType3Base
    {
        public override Type TableKeyType => typeof(SampleModuleType3_2);
    }

    [Serializable, TypeRegistryItem("示例模块类型3——3")]
    public class SampleModuleType3_3 : SampleModuleType3Base
    {
        public override Type TableKeyType => typeof(SampleModuleType3_3);
    }

    // =================================================================================================
    // 域4：示例域（类型4）
    // -------------------------------------------------------------------------------------------------
    // 说明：模块可合并在域脚本中（如本文件），也可拆分成多个文件。
    // =================================================================================================
    [Serializable]
    public class SampleDomainType4 : Domain<SamplesCore, SampleModuleType4Base>
    {
    }

    [Serializable]
    public abstract class SampleModuleType4Base : Module<SamplesCore, SampleDomainType4>
    {
    }

    [Serializable, TypeRegistryItem("示例模块类型4——1")]
    public class SampleModuleType4_1 : SampleModuleType4Base
    {
        public override Type TableKeyType => typeof(SampleModuleType4_1);
    }

    [Serializable, TypeRegistryItem("示例模块类型4——2")]
    public class SampleModuleType4_2 : SampleModuleType4Base
    {
        public override Type TableKeyType => typeof(SampleModuleType4_2);
    }

    [Serializable, TypeRegistryItem("示例模块类型4——3")]
    public class SampleModuleType4_3 : SampleModuleType4Base
    {
        public override Type TableKeyType => typeof(SampleModuleType4_3);
    }
}
