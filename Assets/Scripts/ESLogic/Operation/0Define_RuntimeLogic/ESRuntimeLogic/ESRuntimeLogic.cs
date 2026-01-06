using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    
       //标准ES运行时逻辑持有者
    public interface IESRuntimeLogic : 
    IOpStoreKeyValueForOutputOpeation//满足委托任务
    <IOperation, DeleAndCount, OutputOpeationDelegateFlag>,
   IOpStoreSafeKeyGroupForOutputOpeation//满足缓冲任务
   <ES.OutputOperationBufferFloat_TargetAndDirectInput
   <ES.ESRuntimeTarget, ES.IESRuntimeLogic, ES.ESRuntimeOpSupport_ValueEntryFloatOperation>,
    ES.BufferOperationFloat, ES.OutputOpeationBufferFlag>
    {
        public ContextPool Context{get { return Provider.contextPool; }}//上下文
        public CacherPool Cacher{get{ return Provider.cacherPool; }}//运行中缓存值
        
         Dictionary<IOperation, DeleAndCount>  IOpStoreKeyValueForOutputOpeation//满足委托任务
    <IOperation, DeleAndCount, OutputOpeationDelegateFlag>.GetFromOpStore(OutputOpeationDelegateFlag flag)
        {
            return Provider.storeFordele;
        }

         SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<ESRuntimeTarget, IESRuntimeLogic, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat> 
         IOpStoreSafeKeyGroupForOutputOpeation//满足缓冲任务
   <ES.OutputOperationBufferFloat_TargetAndDirectInput
   <ES.ESRuntimeTarget, ES.IESRuntimeLogic, ES.ESRuntimeOpSupport_ValueEntryFloatOperation>,
    ES.BufferOperationFloat, ES.OutputOpeationBufferFlag>.GetFromOpStore(OutputOpeationBufferFlag flag)
        {
           return Provider.storeForbufer;
        }
        
        
        public ESRuntimeLogicProvider Provider{get;}
        
           
    }

    public class VisualLogicer /*比如 Skill*/ : IESRuntimeLogic
    {
        public ESRuntimeLogicProvider Provider => provider;

        public ESRuntimeLogicProvider provider=new ESRuntimeLogicProvider();
    }



    //通用装载者
    public class ESRuntimeLogicProvider : IESRuntimeLogic
    {
        public ContextPool contextPool;
        public CacherPool cacherPool;
        public ContextPool Context => contextPool;
        public CacherPool Cacher =>cacherPool;

        public ESRuntimeLogicProvider Provider => this;

        public Dictionary<IOperation, DeleAndCount> storeFordele=new Dictionary<IOperation, DeleAndCount>();
        public SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput
        <ESRuntimeTarget, IESRuntimeLogic, ESRuntimeOpSupport_ValueEntryFloatOperation>
        , BufferOperationFloat> storeForbufer=new ();

        public Dictionary<IOperation, DeleAndCount> GetFromOpStore(OutputOpeationDelegateFlag flag = null)
        {
            return storeFordele;
        }

        public SafeKeyGroup<OutputOperationBufferFloat_TargetAndDirectInput<ESRuntimeTarget, IESRuntimeLogic, ESRuntimeOpSupport_ValueEntryFloatOperation>, BufferOperationFloat> GetFromOpStore(OutputOpeationBufferFlag flag = null)
        {
           return storeForbufer;
        }
    }
}
