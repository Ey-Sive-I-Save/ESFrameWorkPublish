using System;

namespace ES
{
    /// <summary>
    /// Marks a ScriptableObject type as editor/tooling data that must not be collected into runtime resources or asset packages.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class ESOnlyEditorSOAttribute : Attribute
    {
        public string Reason { get; }

        public ESOnlyEditorSOAttribute(string reason = "")
        {
            Reason = reason ?? string.Empty;
        }
    }
}
