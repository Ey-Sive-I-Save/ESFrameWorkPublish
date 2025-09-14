using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ES
{
    public static partial class EnumCollect
    {
        #region 修饰符
        public enum Modifier_Access
        {
            [InspectorName("公开的")][ESMessage("public")] Public,
            [InspectorName("受保护的")][ESMessage("protected")] Protected,
            [InspectorName("私有的")][ESMessage("private")] Private,
            [InspectorName("程序集内的")][ESMessage("internal")] Internal,
            [InspectorName("同一程序集内 ​​或​​ 派生类")][ESMessage("protected internal")] Protected_Internal,
            [InspectorName("同一程序集内的派生类（严格限制）")][ESMessage("private protected")] Private_Protected,
            [InspectorName("无")] None,
        }
        //字段专用的
        public enum Modifier_Field_Addition
        {
            [InspectorName("无")] None,//无最优先
            [InspectorName("静态的")][ESMessage("static")] Static,
            [InspectorName("常量")][ESMessage("const")] Const,
            [InspectorName("只读的(仅成员)")][ESMessage("readonly")] Readonly,
        }

        public enum Modifier_Method_Addition
        {
            [InspectorName("无")] None, // 无修饰符（最高优先级）

            // 基础行为修饰符
            [InspectorName("静态的")][ESMessage("static")] Static,
            [InspectorName("虚函数")][ESMessage("virtual")] Virtual,
            [InspectorName("抽象的")][ESMessage("abstract")] Abstract,
            [InspectorName("重写的")][ESMessage("override")] Override,
            [InspectorName("密封重写")][ESMessage("sealed override")] SealedOverride,

            // 隐藏与覆盖修饰符
            [InspectorName("隐藏继承")][ESMessage("new")] New,
            [InspectorName("隐藏虚函数")][ESMessage("new virtual")] NewVirtual,
        }
        public enum Modifier_Class_Addition
        {
            [InspectorName("无")] None, // 无最优先
            [InspectorName("抽象的")][ESMessage("abstract")] Abstract,
            [InspectorName("密封的")][ESMessage("sealed")] Sealed,
            [InspectorName("静态的")][ESMessage("static")] Static,
            [InspectorName("部分的")][ESMessage("partial")] Partial,
        }
        #endregion

        #region 方法
        //方法专用的

        #endregion

        #region 类型定义
        public enum TypeDefine_Orignial
        {
            [InspectorName("整数")][ESMessage("int")] Int,
            [InspectorName("单精度浮点")][ESMessage("float")] Float,
            [InspectorName("布尔值")][ESMessage("bool")] Bool,
            [InspectorName("字符串")][ESMessage("string")] _String,
            [InspectorName("长整数")][ESMessage("long")] Long,
            [InspectorName("字符")][ESMessage("char")] Char,
        }
        public enum TypeDefine_UnityCore
        {
            [InspectorName("变换组件")][ESMessage("Transform")] _Transform,
            [InspectorName("Mono脚本")][ESMessage("MonoBehaviour")] _MonoBehaviour,
            [InspectorName("游戏物体")][ESMessage("GameObject")] _GameObject,
            [InspectorName("刚体")][ESMessage("Rigidbody")] _Rigidbody,
            [InspectorName("刚体2D")][ESMessage("Rigidbody2D")] _Rigidbody2D,
            [InspectorName("动画控制器")][ESMessage("Animator")] _Animator,
            [InspectorName("摄像机")][ESMessage("Camera")] _Camera,
            [InspectorName("碰撞体")][ESMessage("Collider")] _Collider,
            [InspectorName("碰撞体2D")][ESMessage("collider")] _Collider2D,

        }
        public enum TypeDefine_UI_OLD
        {
            [InspectorName("画布")][ESMessage("Canvas")] _Canvas,
            [InspectorName("按钮")][ESMessage("Button")] _Button,
            [InspectorName("文本")][ESMessage("Text")] _Text,
            [InspectorName("图片")][ESMessage("Image")] _Image,
            [InspectorName("输入框")][ESMessage("InputField")] _InputField,
            [InspectorName("滑动条")][ESMessage("Slider")] _Slider,
        }
        public enum TypeDefine_UI_TMP
        {
            [InspectorName("画布")][ESMessage("Canvas")] _Canvas,
            [InspectorName("按钮")][ESMessage("Button")] _Button,
            [InspectorName("文本")][ESMessage("Text")] _Text,
            [InspectorName("图片")][ESMessage("Image")] _Image,
            [InspectorName("输入框")][ESMessage("InputField")] _InputField,
            [InspectorName("滑动条")][ESMessage("Slider")] _Slider,
        }
        public enum TypeDefine_Math
        {
            [InspectorName("整数")][ESMessage("int")] Int,
            [InspectorName("单精度浮点")][ESMessage("float")] Float,
            [InspectorName("向量2维")][ESMessage("Vector2")] _Vector2,
            [InspectorName("向量3维")][ESMessage("Vector3")] _Vector3,
            [InspectorName("四元数")][ESMessage("Quaternion")] _Quaternion,
        }
        #endregion

        #region 变量初始化赋值
        public enum ValueInitType
        {
            [InspectorName("无")] None,//无最优先
            [InspectorName("直接赋值")] DirectGiveValue,
            [InspectorName("New")] NewIt,
            [InspectorName("NULL")] NULL,//无最优先
            [InspectorName("default")] Default,//无最优先
            [InspectorName("New并且附带内容")] NewItAndWith,
        }

        #endregion
    }
}
