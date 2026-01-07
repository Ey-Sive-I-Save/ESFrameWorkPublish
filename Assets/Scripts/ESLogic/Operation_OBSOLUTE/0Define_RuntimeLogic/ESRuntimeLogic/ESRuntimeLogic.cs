using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{

    //标准ES运行时逻辑持有者
    public interface IOpSupporter_OB :
    IOpStoreDictionary//满足委托任务
    <IOperation, DeleAndCount, OutputOpeationDelegateFlag>,
   IOpStoreKeyGroup//满足缓冲任务
   <ES.OutputOperationBufferFloat_TargetAndDirectInput
   <ES.ESRuntimeTarget, ES.IOpSupporter_OB, ES.ESRuntimeOpSupport_ValueEntryFloatOperation>,
    ES.BufferOperationFloat, ES.OutputOpeationBufferFlag>
    {
        public ContextPool Context { get { return Provider.contextPool; } }//上下文
        public CacherPool Cacher { get { return Provider.cacherPool; } }//运行中缓存值

        SafeDictionary<IOperation, DeleAndCount> IOpStoreDictionary//满足委托任务
   <IOperation, DeleAndCount, OutputOpeationDelegateFlag>.GetFromOpStore(OutputOpeationDelegateFlag flag)
        {
            return Provider.storeFordele;
        }

        SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<ESRuntimeTarget, IOpSupporter_OB, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat>
        IOpStoreKeyGroup//满足缓冲任务
  <ES.OutputOperationBufferFloat_TargetAndDirectInput
  <ES.ESRuntimeTarget, ES.IOpSupporter_OB, ES.ESRuntimeOpSupport_ValueEntryFloatOperation>,
   ES.BufferOperationFloat, ES.OutputOpeationBufferFlag>.GetFromOpStore(OutputOpeationBufferFlag flag)
        {
            return Provider.storeForbufer;
        }


        public ESRuntimeLogicProvider Provider { get; }


    }

    public class VisualLogicer /*比如 Skill*/ : IOpSupporter_OB
    {
        public ESRuntimeLogicProvider Provider => provider;

        public ESRuntimeLogicProvider provider = new ESRuntimeLogicProvider();
    }



    //通用装载者
    public class ESRuntimeLogicProvider : IOpSupporter_OB
    {
        //上下文支持
        public ContextPool contextPool;
        //缓存数据支持
        public CacherPool cacherPool;
        //委托任务存储支持
        public SafeDictionary<IOperation, DeleAndCount> storeFordele = new SafeDictionary<IOperation, DeleAndCount>();
        //缓冲任务存储支持
        public SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput
        <ESRuntimeTarget, IOpSupporter_OB, ESRuntimeOpSupport_ValueEntryFloatOperation>
        , BufferOperationFloat> storeForbufer = new();




        public SafeDictionary<IOperation, DeleAndCount> GetFromOpStore(OutputOpeationDelegateFlag flag = null)
        {
            return storeFordele;
        }

        public SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<ESRuntimeTarget, IOpSupporter_OB, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat> GetFromOpStore(OutputOpeationBufferFlag flag = null)
        {
            return storeForbufer;
        }

        #region  属性器
        public ContextPool Context => contextPool;
        public CacherPool Cacher => cacherPool;

        public ESRuntimeLogicProvider Provider => this;

        #endregion
    }
}
