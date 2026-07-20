using System;

namespace ES
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public class ESRuntimeWatchAttribute : Attribute
    {
        public const string CategoryNone = "无分类";
        public const string CategoryTemporary = "临时";
        public const string CategoryDebug = "调试";
        public const string CategoryBattle = "战斗";
        public const string CategoryAI = "AI";
        public const string CategoryCharacter = "角色";
        public const string CategoryScene = "场景";
        public const string CategoryAsset = "资源";
        public const string CategoryPerformance = "性能";
        public const string CategoryNetwork = "网络";
        public const string CategorySave = "存档";
        public const string CategoryInput = "输入";
        public const string CategoryUI = "UI";

        public string Group;
        public string Label;
        public string Category;
        public string RequiredTag;
        public string ShowIf;

        public ESRuntimeWatchAttribute(string group = "Default", string label = null, string requiredTag = null, string showIf = null, string category = null)
        {
            Group = string.IsNullOrEmpty(group) ? "Default" : group;
            Label = label;
            Category = string.IsNullOrEmpty(category) ? CategoryNone : category;
            RequiredTag = requiredTag;
            ShowIf = showIf;
        }
    }
}
