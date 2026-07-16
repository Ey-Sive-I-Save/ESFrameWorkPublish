using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.DebugLog)]
    public sealed class ESCommand_Debug_Log : ESCommand
    {
        [LabelText("日志内容")]
        public string message = "ESCommand Log";

        [LabelText("输出等级")]
        public ESCommandDebugLogLevel level = ESCommandDebugLogLevel.Log;

        public override string CommandName
        {
            get { return "输出日志"; }
        }

        public override void Invoke()
        {
            switch (level)
            {
                case ESCommandDebugLogLevel.Warning:
                    Debug.LogWarning(message);
                    break;
                case ESCommandDebugLogLevel.Error:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }
    }

    public enum ESCommandDebugLogLevel
    {
        [InspectorName("普通")]
        Log,

        [InspectorName("警告")]
        Warning,

        [InspectorName("错误")]
        Error
    }
}
