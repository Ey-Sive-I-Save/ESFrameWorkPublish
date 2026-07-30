
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace ES
{
    /// <summary>
    /// SO 配置信息的编辑期组织契约。
    /// <para>
    /// 这里的 Key 仅用于数据组字典、策划命名、SO 表格导入导出和编辑器定位；
    /// 它不是运行时业务身份，也不得作为 ConfigKey、RuntimeKey 或运行时查表的回退值。
    /// </para>
    /// </summary>
    public interface ISoDataInfo
    {
#if UNITY_EDITOR
        void DestroyDirecly();
#endif
        /// <summary>
        /// 设置编辑器/策划数据键。只允许数据组织工具、SO 表格和编辑器工作流调用；
        /// 运行时系统不得用它生成或补全业务 Key。
        /// </summary>
        void SetKey(string key);

        /// <summary>
        /// 返回编辑器/策划数据键，用于 Group 字典、表格和资源定位。
        /// 返回值不具备运行时身份语义。
        /// </summary>
        string GetKey();
    }

    /// <summary>
    /// 所有 SO 数据条目的抽象基类。
    /// <para>
    /// <see cref="KeyName"/> 是编辑器与策划层的组织字段：用于数据组字典键、策划可读命名、
    /// SO 表格合并以及编辑器定位。它可以随策划组织方式调整，不承诺跨版本运行时稳定。
    /// </para>
    /// <para>
    /// 运行时 GameCore 身份必须由领域自己的显式强类型 ConfigKey 提供，例如
    /// ESGameCoreConfigKey 的 enumKey/stringKey。禁止用 KeyName 生成 RuntimeKey、补全 StringKey、
    /// 参与运行时查表、存档身份、网络协议或跨进程资源身份。
    /// </para>
    /// </summary>
    public abstract class SoDataInfo : ESSO, ISoDataInfo
    {
        /// <summary>
        /// 编辑器/策划数据键。
        /// <para>允许：Group 字典、策划命名、SO 表格、编辑器定位。</para>
        /// <para>禁止：ConfigKey 回退、RuntimeKey、运行时查表、存档键、网络键、资源身份。</para>
        /// </summary>
        [ReadOnly, LabelText("策划数据键（仅编辑器组织）")]
        public string KeyName;

        /// <inheritdoc />
        public void SetKey(string key)
        {
            KeyName = key;
        }

        /// <inheritdoc />
        public string GetKey()
        {
            return KeyName;
        }

#if UNITY_EDITOR

        [ContextMenu("删除自己")]
        public void DestroyDirecly()
        {
            Undo.DestroyObjectImmediate(this);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }
#endif
    }
}
