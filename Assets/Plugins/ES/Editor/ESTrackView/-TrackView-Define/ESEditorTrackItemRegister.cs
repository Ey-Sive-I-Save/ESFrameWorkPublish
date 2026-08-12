using System;
using ES;
using UnityEngine;

public sealed class ESEditorTrackItemRegister : EditorRegister_FOR_ClassAttribute<CreateTrackItemAttribute>
{
    public override void Handle(CreateTrackItemAttribute attribute, Type type)
    {
        if (attribute == null || type == null)
            return;

        if (type.IsAbstract
            || type.IsInterface
            || !typeof(ITrackItem).IsAssignableFrom(type)
            || type.GetConstructor(Type.EmptyTypes) == null)
        {
            Debug.LogWarning(
                $"[轨道编辑器] 忽略无效轨道注册：{type.FullName}。"
                + "轨道必须是可实例化、实现 ITrackItem 且具有无参构造的类型。");
            return;
        }

        string menuName = string.IsNullOrWhiteSpace(attribute.menuName)
            ? type._GetTypeDisplayName()._KeepAfterByLast("/")
            : attribute.menuName.Trim();
        ESTrackViewWindowHelper.RegisterTrackItemType(attribute.itemType, menuName, type);
    }
}
