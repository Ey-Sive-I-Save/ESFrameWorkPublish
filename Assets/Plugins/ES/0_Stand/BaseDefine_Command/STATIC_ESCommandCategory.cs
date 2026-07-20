namespace ES
{
    public static class ESCommandCategory
    {
        public const string Root = "\u547d\u4ee4";

        public const string Common = Root + "/\u901a\u7528";
        public const string Input = Root + "/\u8f93\u5165";
        public const string UI = Root + "/UI";
        public const string RuntimeMode = Root + "/\u8fd0\u884c\u6a21\u5f0f";
        public const string Scene = Root + "/\u573a\u666f";
        public const string Object = Root + "/\u5bf9\u8c61";
        public const string Animation = Root + "/\u52a8\u753b";
        public const string Audio = Root + "/\u97f3\u9891";
        public const string Link = Root + "/Link";
        public const string Debug = Root + "/\u8c03\u8bd5";
        public const string Play = Root + "/\u64ad\u653e";

        public const string InputVirtual = Input + "/\u865a\u62df\u8f93\u5165";
        public const string InputProfile = Input + "/\u952e\u4f4d\u6863\u6848";
        public const string UIPage = UI + "/\u9875\u9762";
        public const string UIPanel = UI + "/\u9762\u677f";
        public const string UIElement = UI + "/\u5143\u7d20";
        public const string ObjectActive = Object + "/\u663e\u9690";
        public const string ObjectTransform = Object + "/\u53d8\u6362";
        public const string ObjectComponent = Object + "/\u7ec4\u4ef6";
    }

    public static class ESCommandTypeName
    {
        public const string Delay = ESCommandCategory.Play + "/\u5ef6\u65f6";
        public const string InputSetVirtualButton = ESCommandCategory.InputVirtual + "/\u8bbe\u7f6e\u6309\u94ae";
        public const string InputPulseVirtualButton = ESCommandCategory.InputVirtual + "/\u89e6\u53d1\u6309\u94ae";
        public const string InputPulseVirtualControlButton = ESCommandCategory.InputVirtual + "/\u89e6\u53d1\u865a\u62df\u63a7\u4ef6\u6309\u94ae";
        public const string InputClearVirtualButton = ESCommandCategory.InputVirtual + "/\u6e05\u9664\u6309\u94ae";
        public const string InputSetVirtualAxis = ESCommandCategory.InputVirtual + "/\u8bbe\u7f6e\u5355\u8f74";
        public const string InputClearVirtualAxis = ESCommandCategory.InputVirtual + "/\u6e05\u9664\u5355\u8f74";
        public const string InputSetVirtualVector2 = ESCommandCategory.InputVirtual + "/\u8bbe\u7f6e\u4e8c\u7ef4\u5411\u91cf";
        public const string InputClearVirtualVector2 = ESCommandCategory.InputVirtual + "/\u6e05\u9664\u4e8c\u7ef4\u5411\u91cf";
        public const string InputClearAllVirtual = ESCommandCategory.InputVirtual + "/\u6e05\u9664\u5168\u90e8\u865a\u62df\u8f93\u5165";
        public const string RuntimeModePush = ESCommandCategory.RuntimeMode + "/\u538b\u5165\u6a21\u5f0f";
        public const string RuntimeModePopTop = ESCommandCategory.RuntimeMode + "/\u5f39\u51fa\u9876\u5c42\u6a21\u5f0f";
        public const string RuntimeModeRemove = ESCommandCategory.RuntimeMode + "/\u79fb\u9664\u6a21\u5f0f";
        public const string RuntimeModeAddTag = ESCommandCategory.RuntimeMode + "/\u6dfb\u52a0\u6807\u8bb0";
        public const string RuntimeModeRemoveTag = ESCommandCategory.RuntimeMode + "/\u79fb\u9664\u6807\u8bb0";
        public const string RuntimeModeClear = ESCommandCategory.RuntimeMode + "/\u6e05\u7a7a\u6a21\u5f0f\u548c\u6807\u8bb0";
        public const string ObjectSetActive = ESCommandCategory.ObjectActive + "/\u8bbe\u7f6e\u6fc0\u6d3b";
        public const string ObjectSetActiveList = ESCommandCategory.ObjectActive + "/\u6279\u91cf\u8bbe\u7f6e\u6fc0\u6d3b";
        public const string ObjectSetBehaviourEnabled = ESCommandCategory.ObjectComponent + "/\u8bbe\u7f6e\u884c\u4e3a\u7ec4\u4ef6\u542f\u7528";
        public const string ObjectSetBehaviourEnabledList = ESCommandCategory.ObjectComponent + "/\u6279\u91cf\u8bbe\u7f6e\u884c\u4e3a\u7ec4\u4ef6\u542f\u7528";
        public const string ObjectSetLocalPosition = ESCommandCategory.ObjectTransform + "/\u8bbe\u7f6e\u672c\u5730\u5750\u6807";
        public const string ObjectSetLocalPositionList = ESCommandCategory.ObjectTransform + "/\u6279\u91cf\u8bbe\u7f6e\u672c\u5730\u5750\u6807";
        public const string ObjectSetLocalEulerAngles = ESCommandCategory.ObjectTransform + "/\u8bbe\u7f6e\u672c\u5730\u65cb\u8f6c";
        public const string ObjectSetLocalEulerAnglesList = ESCommandCategory.ObjectTransform + "/\u6279\u91cf\u8bbe\u7f6e\u672c\u5730\u65cb\u8f6c";
        public const string ObjectSetLocalScale = ESCommandCategory.ObjectTransform + "/\u8bbe\u7f6e\u672c\u5730\u7f29\u653e";
        public const string ObjectSetLocalScaleList = ESCommandCategory.ObjectTransform + "/\u6279\u91cf\u8bbe\u7f6e\u672c\u5730\u7f29\u653e";
        public const string AudioSourcePlay = ESCommandCategory.Audio + "/\u64ad\u653e\u97f3\u9891\u6e90";
        public const string AudioSourceStop = ESCommandCategory.Audio + "/\u505c\u6b62\u97f3\u9891\u6e90";
        public const string UICanvasGroupState = ESCommandCategory.UIElement + "/\u8bbe\u7f6e\u753b\u5e03\u7ec4";
        public const string DebugLog = ESCommandCategory.Debug + "/\u8f93\u51fa\u65e5\u5fd7";
    }
}
