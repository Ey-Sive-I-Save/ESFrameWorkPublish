namespace ES
{
    /// <summary>
    /// Expression 在 Odin TypeRegistry 中的标准分组路径。
    /// 新增表达式时统一使用这里的常量，避免 SerializeReference 菜单失控。
    /// </summary>
    public static class ExpressionTypeRegistryNames
    {
        public const string Root = "Expression";

        public const string Common = Root + "/通用基础";

        public const string Value = Root + "/数值";
        public const string Int = Value + "/Int";
        public const string Float = Value + "/Float";
        public const string FloatMath = Float + "/数学";
        public const string Vector3 = Value + "/Vector3";
        public const string String = Root + "/字符串";

        public const string Bool = Root + "/布尔";
        public const string BoolLogic = Bool + "/逻辑";
        public const string BoolCompare = Bool + "/比较";

        public const string GameObject = Root + "/对象";
        public const string GameObjectDirect = GameObject + "/直接引用";
        public const string GameObjectRuntimeTarget = GameObject + "/运行目标";
        public const string GameObjectHierarchy = GameObject + "/层级";

        public const string RuntimeTarget = Root + "/运行目标包";
        public const string RuntimeTargetEntity = RuntimeTarget + "/实体";
        public const string RuntimeTargetValue = RuntimeTarget + "/运行数值";

        public const string Entity = Root + "/实体";
        public const string EntityDirect = Entity + "/直接引用";
        public const string Asset = Root + "/资源";
        public const string AnimationClip = Asset + "/AnimationClip";
        public const string AudioClip = Asset + "/AudioClip";
        public const string Combat = Root + "/战斗";
        public const string Time = Root + "/时间";
        public const string Random = Root + "/随机";
        public const string Debug = Root + "/调试";

        public const string ConstantFloat = Float + "/常量";
        public const string ConstantInt = Int + "/常量";
        public const string ConstantVector3 = Vector3 + "/常量";
        public const string ConstantString = String + "/常量";
        public const string AddFloat = FloatMath + "/相加";
        public const string MultiplyFloat = FloatMath + "/相乘";
        public const string ConstantBool = Bool + "/常量";
        public const string AndBool = BoolLogic + "/AND";
        public const string OrBool = BoolLogic + "/OR";
        public const string CompareFloat = BoolCompare + "/Float比较";

        public const string DirectGameObject = GameObjectDirect + "/GameObject引用";
        public const string RuntimeTargetEntityGameObject = GameObjectRuntimeTarget + "/使用者实体对象";
        public const string RuntimeTargetEntityAnimator = GameObjectRuntimeTarget + "/使用者Animator";
        public const string RuntimeTargetGameObjectReferPath = GameObjectRuntimeTarget + "/GameObjectRefer路径";
        public const string ChildPath = GameObjectHierarchy + "/子节点路径";
        public const string Parent = GameObjectHierarchy + "/父对象";
        public const string DirectEntity = EntityDirect + "/Entity引用";
        public const string ConstantAnimationClip = AnimationClip + "/直接引用";
        public const string ConstantAudioClip = AudioClip + "/直接引用";
    }
}
