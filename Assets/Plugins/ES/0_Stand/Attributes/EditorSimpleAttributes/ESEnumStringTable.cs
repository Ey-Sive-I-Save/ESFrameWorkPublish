using System;
using UnityEngine;

namespace ES
{
    public enum ESEnumStringTableNewEntryMode
    {
        EnumAndString,
        EnumOnly,
        StringOnly
    }

    /// <summary>
    /// Presents an ESEnumStringMirrorMap field as an ES authoring table without changing its
    /// serialized data. The map's entries list remains the only authority.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class ESEnumStringTableAttribute : PropertyAttribute
    {
        public string EnumColumn { get; set; } = "Enum Key";
        public string StringColumn { get; set; } = "String Key";
        public string ValueColumn { get; set; } = "Value";
        public ESEnumStringTableNewEntryMode NewEntryMode { get; set; } =
            ESEnumStringTableNewEntryMode.EnumAndString;
        public bool Searchable { get; set; } = true;
        public bool AllowReorder { get; set; } = true;
        public bool ShowAdvancedSettings { get; set; }
    }
}
