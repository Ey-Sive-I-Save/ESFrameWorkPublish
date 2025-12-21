using System;

namespace Gaskellgames
{
    /// <summary>
    /// Code updated by Gaskellgames
    /// Original code from 'EditorCools': https://github.com/datsfain/EditorCools
    /// </summary>

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ButtonAttribute : Attribute
    {
        public readonly string Row;

        public ButtonAttribute(string row = "")
        {
            Row = row;
        }
        
    } // class end
}