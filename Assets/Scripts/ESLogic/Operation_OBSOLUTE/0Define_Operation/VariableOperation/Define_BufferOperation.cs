using ES;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


namespace ES
{ 
    /*BufferOperation是一个可执行的对值的缓冲处理，他是动态生成和销毁的，类似于*/

    public interface IBufferOperation:IPoolableAuto
    {
       
    }
    public abstract class BufferOperationAbstract: IBufferOperation
    {
        [NonSerialized]
        public float timeHasGo = 0;

        public abstract bool IsRecycled { get; set; }
        public abstract void OnResetAsPoolable();
        public abstract void TryAutoPushedToPool();
    }
    #region 缓冲操作
    public class BufferOperation<ValueType,Source,This> : BufferOperationAbstract
        where Source: BufferDataSource<ValueType>
        where This: BufferOperation<ValueType, Source, This>,new()
    {
        #region 默认
        [NonSerialized]
        public Source source;
      
        #endregion

        #region 池化
        public sealed override bool IsRecycled { get; set; }
        public sealed override void OnResetAsPoolable()
        {
            source = null;
            timeHasGo = 0;
        }
        public static This GetOne()
        {
            return POOL.GetInPool();
        }
        public static ESSimplePool<This> POOL = new ESSimplePool<This>(()=>new This(),null,10);
        public sealed override void TryAutoPushedToPool()
        {
            POOL.PushToPool(this as This);
        }
        #endregion
    }
    
    //浮点缓冲操作
    public class BufferOperationFloat : BufferOperation<float, BufferDataFloatSource, BufferOperationFloat>
    {

    }
    //浮点缓冲操作
    public class BufferOperationVector2 : BufferOperation<Vector2, BufferDataVector2Source, BufferOperationVector2>
    {

    }
    //浮点缓冲操作
    public class BufferOperationVector3 : BufferOperation<Vector3, BufferDataVector3Source, BufferOperationVector3>
    {

    }
    #endregion
    #region 缓冲数据源
    [Serializable/*BufferDataSource*/]
    public abstract class BufferDataSource<ValueType>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueType EvaluateThisFrame(ref float timeHasGoFrom)
        {
            return Offset(EvaluateAt(timeHasGoFrom), EvaluateAt(timeHasGoFrom += Time.deltaTime));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueType EvaluateToEndFrame(ref float timeHasGoFrom)
        {
            Debug.Log(" TTB "+EvaluateAt(timeHasGoFrom) + "  "+ EvaluateAt(allTime));
            return Offset(EvaluateAt(timeHasGoFrom), EvaluateAt(allTime));
        }
        public abstract ValueType EvaluateAt(float timeHasGo);
        protected abstract ValueType Offset(ValueType pre, ValueType next);
        [LabelText("乘数")] public float mutipler = 1;
        [LabelText("总时间")] public float allTime = 1;
    }
    [Serializable, TypeRegistryItem("缓冲浮点数")]
    public class BufferDataFloatSource : BufferDataSource<float>
    {
        [LabelText("缓冲曲线")] public AnimationCurve curve = AnimationCurve.Constant(0, 1, 1);

        public sealed override float EvaluateAt(float timeHasGo)
        {
            return curve.Evaluate(timeHasGo/allTime);
        }

        protected sealed override float Offset(float pre, float now)
        {
            return (now - pre) * mutipler;
        }
    }
    [Serializable, TypeRegistryItem("缓冲2维向量")]
    public class BufferDataVector2Source : BufferDataSource<Vector2>
    {
        [LabelText("缓冲曲线X")] public AnimationCurve curveX = AnimationCurve.Constant(0, 1, 1);
        [LabelText("缓冲曲线Y")] public AnimationCurve curveY = AnimationCurve.Constant(0, 1, 1);

        public sealed override Vector2 EvaluateAt(float timeHasGo)
        {
            return new Vector2 (curveX.Evaluate(timeHasGo / allTime), curveY.Evaluate(timeHasGo / allTime));
        }

        protected sealed override Vector2 Offset(Vector2 pre, Vector2 now)
        {
            return (now - pre) * mutipler;
        }
    }
    [Serializable, TypeRegistryItem("缓冲3维向量")]
    public class BufferDataVector3Source : BufferDataSource<Vector3>
    {
        [LabelText("缓冲曲线X")] public AnimationCurve curveX = AnimationCurve.Constant(0, 1, 1);
        [LabelText("缓冲曲线Y")] public AnimationCurve curveY = AnimationCurve.Constant(0, 1, 1);
        [LabelText("缓冲曲线Z")] public AnimationCurve curveZ = AnimationCurve.Constant(0, 1, 1);
        public sealed override Vector3 EvaluateAt(float timeHasGo)
        {
            return new Vector3(curveX.Evaluate(timeHasGo / allTime), curveY.Evaluate(timeHasGo / allTime), curveZ.Evaluate(timeHasGo / allTime));
        }

        protected sealed override Vector3 Offset(Vector3 pre, Vector3 now)
        {
            return (now - pre) * mutipler;
        }
    }
    #endregion
}