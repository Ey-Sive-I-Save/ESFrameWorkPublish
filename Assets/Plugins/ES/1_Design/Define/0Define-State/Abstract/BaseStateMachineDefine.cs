using ES;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Sirenix.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using static UnityEditor.VersionControl.Asset;
#endif

namespace ES
{
    public abstract class BaseOriginalStateMachine
    {
    }
}
//     #region 最原始定义
//     //原始定义
//     public abstract class BaseOriginalStateMachine : IESHosting<BaseOriginalStateMachine>, IStateMachine, IESModule,IESOriginalModule<BaseOriginalStateMachine>
//     {
//         #region 杂货
//         public static IState NullState = new ESNanoStateMachine_StringKey() { key_ = "空状态" };
//         public static IState NullState_Micro = new BaseESMicroStateOverrideRunTimeLogic_StringKey() { key = "空状态" };
//         [DisplayAsString(FontSize = 25), HideLabel, PropertyOrder(-1)]
//         public string Des_ => "我是一个状态机噢耶";//随便描述一下而已
//         public bool CheckThisStateCanUpdating
//         {
//             get
//             {
//                 if (AsThis != null && AsThis != this)
//                 {
//                     if (!AsThis.CheckThisStateCanUpdating) return false;
//                 }
//                 if (GetHost != null)
//                 {
//                     if (GetHost is IStateMachine machine)
//                     {
//                         if (!machine.CheckThisStateCanUpdating)
//                         {
//                             return false;
//                         }
//                         else if (machine is ESMicroStateMachine_StringKey mS && (mS.SelfRunningStates?.Contains(this) ?? false)) return true;
//                         return false;
//                     }
//                 }
//                 return true;
//             }
//         }

//         //默认进入状态
//         [NonSerialized] public IState StartWithState;
//         [LabelText("设置默认状态的键(这里设置没用)"),ShowInInspector] public object defaultStateKey = null;
//         //Host获取哈
//         protected bool OnSubmitHostingAsNormal(BaseOriginalStateMachine hosting)
//         {
//             host = hosting;
//             return true;
//         }
//         //获取初始状态
//         protected IState GetStartWith()
//         {
//             if (StartWithState != null && StartWithState != NullState)
//             {
//                 return StartWithState;
//             }
//             else
//             {
//                 return GetDefaultState();
//             }
//         }
//         public IState GetDefaultState()
//         {
//             if (defaultStateKey == null) return null;
//             return GetStateByKey(defaultStateKey);
//         }
//         public bool IsIdle()
//         {
//             return !IsStateNotNull(_SelfRunningState);
//         }
//         public IState AsThis
//         {
//             get => thisState;
//             set { /*Debug.LogWarning("修改状态机自状态是危险的，但是允许");*/ thisState = value; }
//         }

//         [LabelText("状态机自基准状态"), SerializeReference, FoldoutGroup("子状态")] public IState thisState = NullState_Micro;//基准状态不准是纳米的，起码是微型的罢

//         #endregion

//         #region 父子关系与保持的运行
//         //基准状态
//         //获得根状态机--非常好的东西
//         public BaseOriginalStateMachine Root { get { if (GetHost is BaseOriginalStateMachine machine) return machine.Root; else return this; } }

//         public IState SelfRunningMainState { get => _SelfRunningState; set => _SelfRunningState = value; }
//         [SerializeReference, LabelText("当前运行状态"), FoldoutGroup("子状态")] public IState _SelfRunningState = null;
//         //总状态机(冲突合并需要的)--只有标准状态机需要
//         /*[SerializeReference, LabelText("总状态机运行中状态")]
//         public HashSet<IESNanoState> MainRunningStates;*/

//         //自己掌控运行--至少是微型状态机才有
//         /**/
//         #endregion

//         #region 生命周期_状态专属的
//         [ShowInInspector, ReadOnly, LabelText("状态进入 是/否"), FoldoutGroup("是否状况收集")]
//         public bool IsRunning { get; set; }
//         public void OnStateEnter()
//         {
//             if (IsRunning) return;
//             IsRunning = true;//状态更新
//             /*MainRunningStates = Root.MainRunningStates;//使用统一的根处引用*/
//             AsThis?.OnStateEnter();//自基准状态
//             _Expand_PreparedHappenDesign();//扩展Prepare时间
//             /*_SelfRunningStates?.Update();//自己层级下的运行中状态--子状态机也是状态哈--这个更新就是删除多余的的加入新的
//              */
//             if (host is ESMicroStateMachine_StringKey parent)//您是微型状态机
//             {
//                 parent._SelfRunningState = this;
//                 parent._SelfRunningStates.TryAdd(this);//如果自己是子，那么也要加入到父级的运行状态中
//             }
//             OnStateEnter();//状态机没啥好额外准备的罢
//         }
//         //进入时--都不是必要功能
//         protected virtual void OnStateEnter()
//         {
//             IState state = GetStartWith();
//             if (state != null)
//             {
//                 bool b = TryActiveState(state);
//                 if (b == false) OnStateExit();
//                 StartWithState = null;
//             }
//         }

//         //退出时,状态退出
//         public virtual void OnStateExit()
//         {
//             if (!IsRunning) return;
//             IsRunning = false;//状态更新

//             AsThis?.OnStateExit();//自基准状态


//             _Expand_ExitHappenDesign();//扩展Exit时机

//             if (host is ESMicroStateMachine_StringKey parent)
//             {
//                 if (parent._SelfRunningState == this)
//                 {
//                     parent._SelfRunningState = null;
//                 }
//                 parent._SelfRunningStates.TryRemove(this);//如果自己是子，那么也要从父级的运行状态中移除
//             }
//             _SelfRunningState = NullState;
//         }
//         //更新时--状态更新
//         public void OnStateUpdate()
//         {
//             AsThis?.OnStateUpdate();
//             _Expand_UpdateHappenDesign();//扩展更新Update的时机
//             /* _SelfRunningStates?.Update();
//              if (_SelfRunningStates != null)
//              {
//                  foreach (var i in _SelfRunningStates.valuesNow_)
//                  {
//                      if (i != null)
//                      {
//                          if(i is IESMicroState ESMicro)
//                          {
//                              if (ESMicro.RunningStatus != EnumStateRunningStatus.StateUpdate) continue;
//                          }
//                          else if(!i.HasPrepared)
//                          {
//                              continue;
//                          }
//                          i.OnStateUpdate();
//                      }
//                  }
//              }*/
//         }
//         #endregion

//         #region 生命周期_作为模块的
//         protected override void OnEnable()
//         {
//             base.OnEnable();
//             if (GetHost is BaseOriginalStateMachine machine) { }//如果有父状态机--》受父级更新控制
//             else OnStateEnter();//如果没有父级或者父级不是状态机--》自己就完成控制了
//         }

//         protected override void OnDisable()
//         {
//             base.OnDisable();
//             if (GetHost is BaseOriginalStateMachine machine) { }//如果有父状态机--》受父级更新控制
//             else OnStateExit();//如果没有父级或者父级不是状态机--》自己就完成控制了
//         }
//         protected override void Update()
//         {
//             base.Update();
//             if (GetHost is BaseOriginalStateMachine machine) { Debug.Log(2); }//如果有父状态机--》受父级更新控制
//             else OnStateUpdate();//如果没有父级或者父级不是状态机--》自己就完成控制了
//         }
//         #endregion

//         #region 对基准状态的包装
//         public object key_;
//         public void SetKey(object key)
//         {
//             AsThis?.SetKey(key);
//             key_ = key;
//         }
//         public object GetKey()
//         {
//             if (!IsStateNotNull(AsThis)) return key_;
//             return AsThis.GetKey();
//         }
//         public bool IsStateNotNull(IState state)
//         {
//             if (state == null || state == NullState || state == NullState_Micro) return false;
//             return true;
//         }

//         public EnumStateRunningStatus RunningStatus => AsThis?.RunningStatus??(IsRunning?EnumStateRunningStatus.StateUpdate:EnumStateRunningStatus.StateExit);
//         IStateSharedData IState.SharedData
//         {
//             get => AsThis?.SharedData; set { if (AsThis != null) AsThis.SharedData = value; }
//         }
//         IStateVariableData IState.VariableData
//         {
//             get => AsThis?.VariableData; set { if (AsThis != null) AsThis.VariableData = value; }
//         }
//         #endregion

//         #region 状态切换支持
//         public abstract bool TryActiveState(IState use);//通用
//         public abstract bool TryInActiveState(IState use);//通用
//         public abstract bool TryActiveStateByKey(object key_);//通用
//         public abstract string[] KeysWithLayer(string atFirst="");
//         public abstract void RegisterNewState_Original(object key, IState aState);
//         public abstract bool TryInActiveState(object key_);
//         public abstract bool TryInActiveStateByKey(string key_);
//         public abstract IState GetStateByKey(object o);
//         public abstract IState GetStateByKey(string o);
//         #endregion

//         #region 设计层扩展重写状态机逻辑
//         protected virtual void _Expand_PreparedHappenDesign()
//         {
//             host._SelfRunningState = this;
//         }
//         protected virtual void _Expand_UpdateHappenDesign()
//         {
//             if (_SelfRunningState != null)
//             {
//                 _SelfRunningState.OnStateUpdate();
//             }
//             else
//             {
//                 if (IsIdle())
//                 {
//                     var default_ = GetDefaultState();
//                     if (default_ != null)
//                     {
//                        bool b= TryActiveState(default_);
//                         if (b == false) OnStateExit();
//                     }
//                 }
//             }
//         }
//         protected virtual void _Expand_ExitHappenDesign()
//         {

//             if (_SelfRunningState != null)
//             {
//                 _RightlyExitTheState(_SelfRunningState);
//             }
//         }
//         #endregion

//         #region 测试方法
//         [Button("初始化"), FoldoutGroup("状态机测试按钮"), Tooltip("定义初始化的状态")]
//         public void WithEnterState(IState state)
//         {
//             if (state != null)
//                 StartWithState = state;
//         }


//         #endregion

//         #region 注册状态


//         #endregion

//         #region 状态切换辅助等
//         protected void _RightlyPreparedTheState(IState use)
//         {
//             if (use != null)
//             {
//                 use.OnStateEnter();
//                 _SelfRunningState = use;
//             }
//         }
//         protected void _RightlyExitTheState(IState use)
//         {
//             if (use != null)
//             {
//                 if (_SelfRunningState == use) { _SelfRunningState = null; };
//                 use.OnStateExit();
//             }
//         }

//         bool IESOriginalModule<BaseOriginalStateMachine>.OnSubmitHosting(BaseOriginalStateMachine host)
//         {
//             this.host = host;
//             return true;
//         }
//         #endregion
//     }
//     #endregion

//     #region 纳米级别状态机--空的-泛型键-状态机-只能是纳米了--给键哈
//     public abstract class BaseESNanoStateMachine<Key_> : BaseOriginalStateMachine
//     {
//         #region 杂货与基准定义
//         #endregion

//         #region 字典表
//         public override string[] KeysWithLayer(string atFirst)
//         {
//             List<string> all = new List<string>();

//             //加键
//             foreach (var i in allStates.Keys)
//             {
//                 all.Add(atFirst + i);
//             };
//             //遍历
//             foreach (var i in allStates.Values)
//             {
//                 if (i is BaseOriginalStateMachine machine)
//                 {
//                     all.AddRange(machine.KeysWithLayer(atFirst + i.GetKey()));
//                 }
//             }
//             return all.ToArray();
//         }
//         [SerializeReference, LabelText("全部状态字典")]
//         public Dictionary<Key_, IState> allStates = new Dictionary<Key_, IState>();
//         public Dictionary<Key_, IState> AllStates => allStates;
//         public override IState GetStateByKey(object o)
//         {
//             if(o is Key_ thekey&&allStates.ContainsKey(thekey))
//             {
//                 return allStates[thekey];
//             }
//             return null;
//         }
//         public override IState GetStateByKey(string s)
//         {
//             if (s is Key_ thekey && allStates.ContainsKey(thekey))
//             {
//                 return allStates[thekey];
//             }
//             var use = ESDesignUtility.Matcher.SystemObjectToT<Key_>(s);
//             if (use != null && allStates.ContainsKey(use))
//             {
//                 return allStates[use];
//             }
//             return null;
//         }
//         #endregion

//         #region 定义

//         #endregion
//         /* [GUIColor("@OLDESDesignUtilityOLD.ColorSelector.Color_03"),LabelText("当前状态")]*/
//         /* public IESMicroState CurrentState =>currentFirstState?? NullState;*/


//         [Button("测试切换状态"), Tooltip("该方法建议用于运行时，不然得话会立刻调用状态的Enter，请切换使用WithEnterState")]
//         public void TryActiveStateByKey(Key_ key, IState ifNULL = null)
//         {
//             if (allStates == null)
//             {
//                 allStates = new Dictionary<Key_, IState>();
//                 if (ifNULL != null)
//                 {
//                     RegisterNewState_Original(key, ifNULL);
//                     OnlyESNanoPrivate_SwitchStateToFromByKey(key);
//                     //新建 没啥
//                 }
//                 return;
//             }
//             if (allStates.ContainsKey(key))
//             {
//                 TryActiveState(allStates[key]);
//             }
//             else
//             {
//                 if (ifNULL != null)
//                 {
//                     RegisterNewState_Original(key, ifNULL);
//                     TryActiveState(ifNULL);
//                 }
//             }
//         }
//         public override bool TryActiveStateByKey(object key_)
//         {
//             if(key_ is Key_ key_1)
//             {
//                 return TryActiveStateByKey(key_1);
//             }
//             return false;
//         }
//         protected void OnlyESNanoPrivate_SwitchStateToFromByKey(Key_ to, Key_ from = default)
//         {
//             if (AllStates.ContainsKey(to))
//             {
//                 OnlyESNanoPrivate_SwitchStateToFrom(AllStates[to]);
//             }
//         }
//         protected void OnlyESNanoPrivate_SwitchStateToFrom(IState to, IState from = default)
//         {
//             if (AllStates.Values.Contains(to))
//             {
//                 IState willUse = to;
//                 if (SelfRunningMainState == willUse) return;//同一个？无意义

//                 _RightlyExitTheState(SelfRunningMainState);//过去的真退了

//                 _RightlyPreparedTheState(to);//我真来了

//                 if (SelfRunningMainState == null)
//                 {
//                     Debug.LogError("状态为空！键是" + to.GetKey());
//                 }
//             }
//         }
//         public override bool TryActiveState(IState use)
//         {
//             if (!this.IsRunning&&host is BaseOriginalStateMachine originalStateMachine)
//             {
//                 this.WithEnterState(use);
//                 originalStateMachine.TryActiveState(this);
//             }
//             /* if (RootMainRunningStates == null)
//              {
//                  RootMainRunningStates = new Dictionary<Key_, IESMicroState>();
//                  if (ESMicro != null)
//                  {
//                      RegisterNewState_Original(ESMicro, ESMicro);
//                      OnlyESNanoPrivate_SwitchStateToFrom(key);
//                  }
//                  return;
//              }*/
//             if (allStates.Values.Contains(use))
//             {
//                 OnlyESNanoPrivate_SwitchStateToFrom(use);
//                 return true;
//             }
//             else
//             {
//                 Debug.LogError("暂时不支持活动为注册的状态");
//                 return false;
//             }

//         }
//         public bool TryActiveStateByKey(Key_ key_)
//         {
//             if (allStates.ContainsKey(key_))
//             {
//                 return TryActiveState(allStates[key_]);
//             }
//             return false;
//         }
//         public override void RegisterNewState_Original(object key, IState logic)
//         {
//             RegesterNewState(ESDesignUtility.Matcher.SystemObjectToT<Key_>(key), logic);
//         }

//         [Button("初始化"), Tooltip("定义初始化的状态")]
//         public void WithEnterStateByKey(Key_ key)
//         {
//             if (key != null && allStates.ContainsKey(key))
//                 StartWithState = allStates[key];
//             else Debug.LogError("状态机没注册这个状态");
//         }
//         protected override void OnStateEnter()
//         {
//             IState state = GetStartWith();
//             if (state != null)
//             {
//                 bool b = TryActiveState(state);
//                 if (b == false) OnStateExit();
//                 StartWithState = null;
//             }
//             base.OnStateEnter();
//         }
//         public void RegesterNewState(Key_ key, IState logic)
//         {
//             if (allStates.ContainsKey(key))
//             {
//                 Debug.LogError("重复注册状态?键是" + key);
//             }
//             else
//             {
//                 allStates.Add(key, logic);
//                 logic.SetKey(key);

//                 if (logic is IESOriginalModule<BaseOriginalStateMachine> logic1)
//                 {
//                     logic1.OnSubmitHosting(this);
//                     Debug.Log("注册成功？" + logic.GetKey());
//                 }
//                 else if(logic is BaseOriginalStateMachine machine)
//                 {
//                     machine.TrySubmitHosting(this,false);
//                 }
//                 /*else if(logic is BaseOriginalStateMachine standMachine)
//                 {
//                     standMachine.TrySubmitHosting(this);
//                 }*/
//                 else
//                 {
//                     Debug.Log("啥也不是？");
//                 }

//             }
//         }
//         public override bool TryInActiveState(object key_)
//         {
//             return TryInActiveStateByKey(key_.ToString());
//         }

//         public override bool TryInActiveStateByKey(string s)
//         {
//             Debug.Log("尝试关闭状态"+s);
//             if (s is Key_ thekey && allStates.ContainsKey(thekey))
//             {
//                 TryInActiveState(allStates[thekey]);
//             }
//             var use = ESDesignUtility.Matcher.SystemObjectToT<Key_>(s);
//             if (use != null && allStates.ContainsKey(use))
//             {
//                 TryInActiveState(allStates[use]);
//             }
//             return false;
//         }

//         public override bool TryInActiveState(IState use)
//         {
//             if (allStates.ContainsValue(use))
//             {
//                 _RightlyExitTheState(use);
//             }
//             return false;
//         }



//         //默认的单播放

//         /*  private void OnlyESNanoPrivate_SwitchStateToFrom(IESNanoState to, IESNanoState from = default)
//           {
//               if (allStates.Values.Contains(to))
//               {
//                   IESNanoState willUse = to;
//                   if (_SelfRunningState == willUse) return;//同一个？无意义

//                   _RightlyExitTheState(_SelfRunningState);//过去的真退了

//                   _RightlyPreparedTheState(to);//我真来了

//                   if (_SelfRunningState == null)
//                   {
//                       Debug.LogError("状态为空！键是" + to.GetKey());
//                   }
//               }
//           }*/
//         protected override void OnSubmitHosting(BaseOriginalStateMachine host)
//         {
//             this.host = host;
//             base.OnSubmitHosting(host);
//         }
//     }
//     #endregion

//  /*   #region 微型级别状态机 : 支持局部并行和级别处理了
//     [Serializable, TypeRegistryItem("微型状态机(字符串)")]
//     public class BaseESMicroStateMachine_StringKey2 : BaseESNanoStateMachine<string>
//     {
//         public override void TryRemoveModuleAsNormal(IESNanoState key_)
//         {

//         }
//     }
//     #endregion*/

//     #region 标准状态机支持并行和数据配置

//     [Serializable, TypeRegistryItem("字符串键标准并行状态机")]
//     public class BaseESStandardStateMachine2 : ESMicroStateMachine_StringKey, IESModule
//     {
//         #region 
//         [NonSerialized] public HashSet<IState> RootMainRunningStates;//根部的运行
//         #endregion
//         public override bool TryActiveState(IState use)
//         {
//             if (use is IState stand)
//             {
//                 //空状态：直接使用
//                 if (RootMainRunningStates.Count == 0) return base.TryActiveState(stand);
//                 //已经包含-就取消
//                 if (RootMainRunningStates.Contains(stand))
//                 {
//                     Debug.LogWarning("尝试使用已经存在的状态，有何用意");
//                     return false;
//                 }

//                 Debug.Log("-----------《《《《合并测试开始------来自" + stand.GetKey().ToString());
//                 //单状态，简易判断
//                 if (RootMainRunningStates.Count == 1)
//                 {
//                     IState state = RootMainRunningStates.First();
//                     {
//                         //state的共享数据有的不是标准的哈/
//                         //标准情形
//                         Debug.Log(stand.SharedData);
//                         if (state.SharedData is StateSharedData left && stand.SharedData is StateSharedData right)
//                         {
//                             string leftKey = state.GetKey().ToString();
//                             string rightKey = stand.GetKey().ToString();
//                             var back = StateSharedData.HandleMerge(left.MergePart_, right.MergePart_, leftKey, rightKey);
//                             if (back == HandleMergeBack.HitAndReplace)
//                             {
//                                 state.OnStateExit();
//                                 stand.OnStateEnter();
//                                 _SelfRunningState = stand;
//                                 Debug.Log("单-合并--打断  原有的  " + leftKey + " 被 新的  " + rightKey + "  打断!");
//                             }
//                             else if (back == HandleMergeBack.MergeComplete)
//                             {
//                                 stand.OnStateEnter();
//                                 Debug.Log("单-合并--成功  原有的  " + leftKey + " 和  新的  " + rightKey + "  合并!");
//                             }
//                             else //合并失败
//                             {
//                                 Debug.Log("单-合并--失败  原有的  " + leftKey + " 阻止了  新的  " + rightKey + "  !");
//                             }
//                         }
//                         else //有的不是标准状态
//                         {
//                             base.TryActiveState(stand);
//                         }
//                     }
//                 }
//                 else  //多项目
//                 {
//                     if (stand.SharedData is StateSharedData right)
//                     {
//                         string rightKey = stand.GetKey().ToString();
//                         List<IState> hit = new List<IState>();
//                         List<string> merge = new List<string>();
//                         foreach (var i in RootMainRunningStates)
//                         {
//                             if (i.SharedData is StateSharedData left)
//                             {
//                                 string leftKey = i.GetKey().ToString();
//                                 var back = StateSharedData.HandleMerge(left.MergePart_, right.MergePart_, leftKey, rightKey);
//                                 if (back == HandleMergeBack.HitAndReplace)
//                                 {
//                                     hit.Add(i);

//                                     //打断一个捏
//                                 }
//                                 else if (back == HandleMergeBack.MergeComplete)
//                                 {
//                                     //正常的
//                                     merge.Add(leftKey);

//                                 }
//                                 else //合并失败
//                                 {
//                                     Debug.LogWarning("多-合并--失败" + leftKey + " 阻止了 " + rightKey + "的本次合并测，无事发生试!");
//                                     return false;
//                                 }
//                             }
//                         }
//                         //成功合并了
//                         Debug.Log("---√多-合并--完全成功！来自" + rightKey + "以下是细则：");
//                         stand.OnStateEnter();
//                         foreach (var i in merge)
//                         {
//                             Debug.Log("     --合并细则  本次合并-合并了了" + i);
//                         }
//                         foreach (var i in hit)
//                         {
//                             Debug.Log("     --合并细则  本次合并-打断了" + i.GetKey());
//                             i.OnStateExit();
//                         }
//                     }
//                     else //不是标准状态滚
//                     {
//                         base.TryActiveState(stand);
//                     }
//                 }
//                 return false;
//             }
//             return false;
//         }
//     }
//     #endregion


//     #region 可用状态机--String锁定了
//     [Serializable,TypeRegistryItem("纳米状态机(String)")]
//     public class ESNanoStateMachine_StringKey : BaseOriginalStateMachine
//     {
//         #region 字典表

//         [SerializeReference, LabelText("全部状态字典"), FoldoutGroup("子状态")]
//         public Dictionary<string, IState> allStates = new Dictionary<string, IState>();
//         public override string[] KeysWithLayer(string atFirst)
//         {
//             List<string> all = new List<string>();

//             //加键
//             foreach (var i in allStates.Keys) {
//                 all.Add(atFirst+i);
//             };
//             //遍历
//             foreach (var i in allStates.Values)
//             {
//                 if (i is BaseOriginalStateMachine machine)
//                 {
//                     all.AddRange(machine.KeysWithLayer(atFirst + machine.GetKey()+'/'));
//                 }
//             }
//             return all.ToArray();
//         }
//         public override IState GetStateByKey(object o)
//         {
//             string s = o.ToString();
//             if (allStates.ContainsKey(s))
//             {
//                 return allStates[s];
//             }
//             return null;
//         }
//         public override IState GetStateByKey(string s)
//         {
//             if (s != null && allStates.ContainsKey(s))
//             {
//                 return allStates[s];
//             }
//             return null;
//         }
//         #endregion

//         #region 切换状态
//         protected void OnlyESNanoPrivate_SwitchStateToFromByKey(string to, string from = default)
//         {
//             if (allStates.ContainsKey(to))
//             {
//                 OnlyESNanoPrivate_SwitchStateToFrom(allStates[to]);
//             }
//         }
//         protected void OnlyESNanoPrivate_SwitchStateToFrom(IState to, IState from = default)
//         {
//             if (allStates.Values.Contains(to))
//             {
//                 IState willUse = to;
//                 if (SelfRunningMainState == willUse) return;//同一个？无意义

//                 _RightlyExitTheState(SelfRunningMainState);//过去的真退了

//                 _RightlyPreparedTheState(to);//我真来了

//                 if (SelfRunningMainState == null)
//                 {
//                     Debug.LogError("状态为空！键是" + to.GetKey());
//                 }
//             }
//         }

//         public bool TryActiveStateByKeyWithLayer(string layerKey)
//         {
//             if (layerKey.Contains('/')||layerKey.Contains('?')||layerKey.Contains(".."))
//             {
//                 string[] layers = layerKey.Split('/');
//                 BaseOriginalStateMachine TheMachine = this;
//                 IState TheState=null;
//                 Queue<IState> TheStates=null;
//                 bool endBack = true;

//                 for(int index = 0; index < layers.Length; index++)
//                 {
//                     string i = layers[index];
//                     Debug.Log("by" + i+index);
//                     if (i == "..")//回退一级
//                     {
//                         Debug.Log("回退");
//                         var parent = TheMachine.GetHost as BaseOriginalStateMachine;
//                         if (parent != null)
//                         {
//                             TheMachine = parent;
//                             continue;
//                         }
//                     }
//                     var aState = i;
//                     TheStates = null;
//                     if (i.Contains('?'))
//                     {
//                         TheStates = new Queue<IState>();
//                         string[] Addstates = i.Split('?');
//                         foreach (var a in Addstates)
//                         {
//                             var state = TheMachine.GetStateByKey(a);
//                             if (state != null)
//                                 TheStates.Enqueue(state);
//                         }
//                         aState = Addstates[0];
//                     }

//                     TheState = TheMachine.GetStateByKey(aState);

//                     if (TheState != null)
//                     {
//                         if (index == layers.Length - 1) { Debug.Log("last" + index+ TheState.GetKey()); break; }
//                         if (TheState is BaseOriginalStateMachine nextMachine)
//                         {
//                             TheMachine = nextMachine;
//                             Debug.Log("next"+index);
//                             continue;
//                         }
//                         else
//                         {
//                             Debug.Log("state" + index);
//                             break;
//                         }
//                     }
//                     else
//                     {
//                         Debug.Log("null" + index);
//                         endBack = false;
//                         break;
//                     }
//                 }
//                /* foreach(var i in layers)
//                 {

//                 }*/
//                 if (TheStates != null&&TheStates.Count>0)
//                 {
//                     while (TheStates.Count > 0)
//                     {
//                         var use = TheStates.Dequeue();
//                         if (TheMachine.TryActiveState(use)) return true;
//                     }
//                 }
//                 else if (TheState != null)
//                 {
//                    return TheMachine.TryActiveState(TheState);
//                 }
//                 return endBack;
//             }
//             else
//             {
//                return TryActiveStateByKey(layerKey);
//             }
//         }
//         public bool TryInActiveStateByKeyWithLayer(string layerKey)
//         {
//             if (layerKey.Contains('/') || layerKey.Contains('?') || layerKey.Contains(".."))
//             {
//                 string[] layers = layerKey.Split('/');
//                 BaseOriginalStateMachine TheMachine = this;
//                 IState TheState = null;
//                 Queue<IState> TheStates = null;
//                 bool endBack = true;

//                 for (int index = 0; index < layers.Length; index++)
//                 {
//                     string i = layers[index];
//                     Debug.Log("by" + i + index);
//                     if (i == "..")//回退一级
//                     {
//                         Debug.Log("回退");
//                         var parent = TheMachine.GetHost as BaseOriginalStateMachine;
//                         if (parent != null)
//                         {
//                             TheMachine = parent;
//                             continue;
//                         }
//                     }
//                     var aState = i;
//                     TheStates = null;
//                     if (i.Contains('?'))
//                     {
//                         TheStates = new Queue<IState>();
//                         string[] Addstates = i.Split('?');
//                         foreach (var a in Addstates)
//                         {
//                             var state = TheMachine.GetStateByKey(a);
//                             if (state != null)
//                                 TheStates.Enqueue(state);
//                         }
//                         aState = Addstates[0];
//                     }

//                     TheState = TheMachine.GetStateByKey(aState);

//                     if (TheState != null)
//                     {
//                         if (index == layers.Length - 1) break;
//                         if (TheState is BaseOriginalStateMachine nextMachine)
//                         {
//                             TheMachine = nextMachine;
//                             Debug.Log("next" + index);
//                             continue;
//                         }
//                         else
//                         {
//                             Debug.Log("state" + index);
//                             break;
//                         }
//                     }
//                     else
//                     {
//                         Debug.Log("null" + index);
//                         endBack = false;
//                         break;
//                     }
//                 }
//                 /* foreach(var i in layers)
//                  {

//                  }*/
//                 if (TheStates != null && TheStates.Count > 0)
//                 {
//                     while (TheStates.Count > 0)
//                     {
//                         var use = TheStates.Dequeue();
//                         if (TheMachine.TryInActiveState(use)) return true;
//                     }
//                 }
//                 else if (TheState != null)
//                 {
//                     return TheMachine.TryInActiveState(TheState);
//                 }
//                 return endBack;
//             }
//             else
//             {
//                 return TryInActiveStateByKey(layerKey);
//             }
//         }
//         public override bool TryInActiveState(IState use)
//         {
//             if (allStates.ContainsValue(use))
//             {
//                 _RightlyExitTheState(use);
//             }
//             return false;
//         }
//         public bool TryActiveStateByKey(string key_)
//         {
//             if (allStates.ContainsKey(key_))
//             {

//                 return TryActiveState(allStates[key_]);
//             }
//             return false;
//         }

//         public override bool TryActiveStateByKey(object key_)
//         {
//             return TryActiveStateByKey(key_.ToString());
//         }

//         public override bool TryActiveState(IState use)
//         {
//             if (use.IsRunning) return true;
//             if (!this.IsRunning && host is BaseOriginalStateMachine originalStateMachine)
//             { 
//                 this.WithEnterState(use);
//                 return originalStateMachine.TryActiveState(this);
//             }
//             if (allStates.Values.Contains(use))
//             {
//                 OnlyESNanoPrivate_SwitchStateToFrom(use);
//                 return true;
//             }
//             else
//             {
//                 if (use.GetKey().ToString() == "玩家格挡")
//                 {
//                     Debug.Log("RESIST-SI");
//                 }
//                 Debug.LogError("暂时不支持活动为注册的状态");
//                 return false;
//             }
//         }
//         #endregion

//         #region 注册注销
//         public override void RegisterNewState_Original(object key, IState aState)
//         {
//             RegisterNewState(key.ToString(), aState);
//         }
//         public void RegisterNewState(string key, IState logic)
//         {
//             if (allStates.ContainsKey(key))
//             {
//                // Debug.LogError("重复注册状态?键是" + key);
//             }
//             else
//             {
//                 allStates.Add(key, logic);
//                 logic.SetKey(key);
//                 if (StartWithState == null || StartWithState == NullState)
//                 {
//                     //新的
//                     StartWithState = logic;
//                 }
//                 if (logic is IESOriginalModule<BaseOriginalStateMachine> logic1)
//                 {
//                     logic1.OnSubmitHosting(this);
//                     //Debug.Log("注册状态成功？" + logic.GetKey());
//                 }
//                 else if (logic is BaseOriginalStateMachine machine)
//                 {
//                     machine.TrySubmitHosting(this, false);
//                     //Debug.Log("注册状态机成功？" + logic.GetKey());
//                 }
//                 else
//                 {
//                     Debug.Log("啥也不是？");
//                 }

//             }
//         }


//         #endregion

//         #region 设计层
//         protected override void _Expand_PreparedHappenDesign()
//         {
//             base._Expand_PreparedHappenDesign();
//         }

//         public override bool TryInActiveState(object key_)
//         {
//            return TryInActiveStateByKey(key_.ToString());
//         }

//         public override bool TryInActiveStateByKey(string key_)
//         {
//             Debug.Log("尝试关闭" + key_);
//             var it = GetStateByKey(key_);
//             if (it == null) return false;
//             TryInActiveState(it);
//             return false;
//         }

//         #endregion
//     }
//     //微型状态机定义
//     [Serializable, TypeRegistryItem("微型状态机(String)")]
//     public class ESMicroStateMachine_StringKey : ESNanoStateMachine_StringKey, IStateMachine
//     {
//         #region 当前并行
//         [LabelText("自己的运行中状态"),FoldoutGroup("子状态")]
//         public SafeUpdateList_EasyQueue_SeriNot_Dirty<IState> _SelfRunningStates = new SafeUpdateList_EasyQueue_SeriNot_Dirty<IState>();
//         public IEnumerable<IState> SelfRunningStates => _SelfRunningStates.valuesNow_;
//         #endregion

//         #region 设计层
//         protected override void _Expand_PreparedHappenDesign()
//         {
//             _SelfRunningStates?.Update();
//             // base._Expand_PreparedHappenDesign();
//         }
//         protected override void _Expand_UpdateHappenDesign()
//         {
//             if (_SelfRunningStates != null)
//             {
//                 _SelfRunningStates.Update();
//                 int count = _SelfRunningStates.valuesNow_.Count;
//                 if (count == 0)
//                 {
//                     if (IsIdle())
//                     {
//                         var default_ = GetDefaultState();
//                         if (default_ != null)
//                         {
//                             bool b = TryActiveState(default_);
//                             if (b == false)
//                             {
//                                 if (GetHost is not BaseOriginalStateMachine machine) return;//最高级您
//                                 OnStateExit();
//                             }
//                         }
//                         else
//                         {
//                             if (GetHost is not BaseOriginalStateMachine machine) return;//最高级您
//                             OnStateExit();
//                         }
//                     }
//                 }
//                 else
//                 {

//                     for (int iq = 0; iq < count; iq++)
//                     {
//                         var i = _SelfRunningStates.valuesNow_[iq];
//                         /*                    if (i is IESMicroState ESMicro)
//                                             {
//                                                 if (ESMicro.RunningStatus != EnumStateRunningStatus.StateUpdate) continue;
//                                             }
//                                             else if (!i.HasPrepared)
//                                             {
//                                                 continue;
//                                             }*/
//                         if (i.RunningStatus == EnumStateRunningStatus.StateUpdate)
//                             i.OnStateUpdate();
//                     }
//                 }

//             }





//             //base._Expand_UpdateHappenDesign();不需要您
//         }
//         protected override void _Expand_ExitHappenDesign()
//         {
//             _SelfRunningStates?.Update();
//             if (_SelfRunningStates != null)
//             {
//                 foreach (var i in _SelfRunningStates.valuesNow_)
//                 {
//                     if (i != null)
//                     {
//                         _RightlyExitTheState(i);
//                     }
//                 }
//             }
//             //base._Expand_ExitHappenDesign();
//         }
//         #endregion

//         #region Active重写
//         public override bool TryActiveState(IState use)
//         {
//             if (use is IState ESMicro)
//             {
//                 //空状态：直接使用
//                 if (SelfRunningStates.Count() == 0) return base.TryActiveState(ESMicro);
//                 //已经包含-就取消
//                 if (SelfRunningStates.Contains(ESMicro))
//                 {
//                     Debug.LogWarning("尝试使用已经存在的状态，有何用意");
//                     return false;
//                 }

//                 Debug.Log("-----------《《《《合并测试开始------来自" + ESMicro.GetKey().ToString());
//                 //单状态，简易判断
//                 if (SelfRunningStates.Count() == 1)
//                 {
//                     IState state = SelfRunningStates.First();
//                     {
//                         //state的共享数据有的不是标准的哈/
//                         //标准情形
//                         if (state.SharedData is IStateSharedData left && ESMicro.SharedData is IStateSharedData right)
//                         {
//                             string leftKey = state.GetKey().ToString();
//                             string rightKey = ESMicro.GetKey().ToString();
//                             var back = StateSharedData.HandleMerge(left, right, leftKey, rightKey);
//                             if (back == HandleMergeBack.HitAndReplace)
//                             {
//                                 state.OnStateExit();
//                                 ESMicro.OnStateEnter();
//                                 _SelfRunningState = ESMicro;
//                                 Debug.Log("单-合并--打断  原有的  " + leftKey + " 被 新的  " + rightKey + "  打断!");
//                             }
//                             else if (back ==HandleMergeBack.MergeComplete)
//                             {
//                                 ESMicro.OnStateEnter();
//                                 Debug.Log("单-合并--成功  原有的  " + leftKey + " 和  新的  " + rightKey + "  合并!");
//                             }
//                             else //合并失败
//                             {
//                                 Debug.Log("单-合并--失败  原有的  " + leftKey + " 阻止了  新的  " + rightKey + "  !");
//                             }
//                         }
//                         else //有的不是标准状态
//                         {
//                             base.TryActiveState(ESMicro);
//                             Debug.Log("不具有");
//                         }
//                     }
//                 }
//                 else  //多项目
//                 {
//                     if (ESMicro.SharedData is IStateSharedData right)
//                     {
//                         string rightKey = ESMicro.GetKey().ToString();
//                         List<IState> hit = new List<IState>();
//                         List<string> merge = new List<string>();
//                         foreach (var i in SelfRunningStates)
//                         {
//                             if (i.SharedData is IStateSharedData left)
//                             {
//                                 string leftKey = i.GetKey().ToString();
//                                 var back = StateSharedData.HandleMerge(left, right, leftKey, rightKey);
//                                 if (back == HandleMergeBack.HitAndReplace)
//                                 {
//                                     hit.Add(i);

//                                     //打断一个捏
//                                 }
//                                 else if (back == HandleMergeBack.MergeComplete)
//                                 {
//                                     //正常的
//                                     merge.Add(leftKey);

//                                 }
//                                 else //合并失败
//                                 {
//                                     Debug.LogWarning("多-合并--失败" + leftKey + " 阻止了 " + rightKey + "的本次合并测，无事发生试!");
//                                     return false;
//                                 }
//                             }
//                         }
//                         //成功合并了
//                         Debug.Log("---√多-合并--完全成功！来自" + rightKey + "以下是细则：");
//                         ESMicro.OnStateEnter();
//                         foreach (var i in merge)
//                         {
//                             Debug.Log("     --合并细则  本次合并-合并了了" + i);
//                         }
//                         foreach (var i in hit)
//                         {
//                             Debug.Log("     --合并细则  本次合并-打断了" + i.GetKey());
//                             i.OnStateExit();
//                         }
//                     }
//                     else //不是标准状态滚
//                     {
//                         base.TryActiveState(ESMicro);
//                     }
//                 }
//                 return false;
//             }
//             return base.TryActiveState(use);
//         }

//         #endregion
//     }

//     //标准状态机
//     [Serializable, TypeRegistryItem("标准状态机(String)")]
//     public class ESStandardStateMachine_StringKey : ESMicroStateMachine_StringKey, IStateMachine
//     {
//         #region 总状态机
//         [OdinSerialize, LabelText("总状态机运行中状态"),ShowInInspector, FoldoutGroup("子状态")]
//         public HashSet<IState> MainRunningStates=new HashSet<IState>();
//         public HashSet<IState> RootAllRunningStates => MainRunningStates;
//         #endregion

//         #region 设计层
//         protected override void _Expand_PreparedHappenDesign()
//         {
//             if (MainRunningStates ==null&&Root == this)
//             {
//                 MainRunningStates = new HashSet<IState>();
//             }
//             if (Root is ESStandardStateMachine_StringKey standMachine)
//             {
//                 MainRunningStates = standMachine.MainRunningStates;
//             }
//             base._Expand_PreparedHappenDesign();
//         }
//         protected override void OnEnable()
//         {
//             base.OnEnable();
//             if (Root is ESStandardStateMachine_StringKey standMachine)
//             {
//                 MainRunningStates = standMachine.MainRunningStates;
//             }
//         }
//       /*  protected override void _Expand_UpdateHappenDesign()
//         {
//             base._Expand_UpdateHappenDesign();//照常更新
//         }*/
//         protected override void _Expand_ExitHappenDesign()
//         {
//             _SelfRunningStates?.Update();
//             base._Expand_ExitHappenDesign();//照常更新
//         }
//         #endregion

//         #region Active重写
//         public override bool TryActiveState(IState use)
//         {

//             if (use is IState stand)
//             {
//                 //空状态：直接使用
//                 if (RootAllRunningStates.Count == 0) { return base.TryActiveState(stand); }
//                 //已经包含-就取消
//                 if (RootAllRunningStates.Contains(stand)||SelfRunningStates.Contains(stand))
//                 {
//                     return stand.IsRunning;
//                 }

//                 //Debug.Log("-----------《《《《合并测试开始------来自" + stand.GetKey().ToString());
//                 //单状态，简易判断
//                 if (RootAllRunningStates.Count == 1)
//                 {
//                     IState state = RootAllRunningStates.First();
//                     {
//                         //state的共享数据有的不是标准的哈/
//                         //标准情形
//                       //  Debug.Log("单-合并--测试");
//                         if (state.SharedData is StateSharedData left && stand.SharedData is StateSharedData right)
//                         {
//                             string leftKey = state.GetKey().ToString();
//                             string rightKey = stand.GetKey().ToString();
//                             var back = StateSharedData.HandleMerge(left.MergePart_, right.MergePart_, leftKey, rightKey);
//                             if (back == HandleMergeBack.HitAndReplace)
//                             {
//                                 state.OnStateExit();
//                                 stand.OnStateEnter();
//                                 _SelfRunningState = stand;
//                                 Debug.Log("单-合并--打断  原有的  " + leftKey + " 被 新的  " + rightKey + "  打断!");
//                                 return true;
//                             }
//                             else if (back == HandleMergeBack.MergeComplete)
//                             {
//                                 stand.OnStateEnter();
//                                 Debug.Log("单-合并--成功  原有的  " + leftKey + " 和  新的  " + rightKey + "  合并!");
//                                 return true;
//                             }
//                             else //合并失败
//                             {
//                                 Debug.Log("单-合并--失败  原有的  " + leftKey + " 阻止了  新的  " + rightKey + "  !");
//                                 return false;
//                             }
//                         }
//                         else //有的不是标准状态
//                         {
//                             base.TryActiveState(stand);
//                         }
//                     }
//                 }
//                 else  //多项目
//                 {
//                     if (stand.SharedData is StateSharedData right)
//                     {
//                         string rightKey = stand.GetKey().ToString();
//                         List<IState> hit = new List<IState>();
//                         List<string> merge = new List<string>();
//                         foreach (var i in RootAllRunningStates)
//                         {
//                             if (i.SharedData is StateSharedData left)
//                             {
//                                 string leftKey = i.GetKey().ToString();
//                                 var back = StateSharedData.HandleMerge(left.MergePart_, right.MergePart_, leftKey, rightKey);
//                                 if (back == HandleMergeBack.HitAndReplace)
//                                 {
//                                     hit.Add(i);

//                                     //打断一个捏
//                                 }
//                                 else if (back == HandleMergeBack.MergeComplete)
//                                 {
//                                     //正常的
//                                     merge.Add(leftKey);

//                                 }
//                                 else //合并失败
//                                 {
//                                    // Debug.LogWarning("多-合并--失败" + leftKey + " 阻止了 " + rightKey + "的本次合并测，无事发生试!");
//                                     return false;
//                                 }
//                             }
//                         }
//                         //成功合并了
//                       //  Debug.Log("---√多-合并--完全成功！来自" + rightKey + "以下是细则：");
//                         stand.OnStateEnter();
//                         foreach (var i in merge)
//                         {
//                           //  Debug.Log("     --合并细则  本次合并-合并了了" + i);
//                         }
//                         foreach (var i in hit)
//                         {
//                         //    Debug.Log("     --合并细则  本次合并-打断了" + i.GetKey());
//                             i.OnStateExit();
//                         }
//                         return true;
//                     }
//                     else //不是标准状态滚
//                     {
//                         base.TryActiveState(stand);
//                     }
//                 }
//                 return false;
//             }
//             return base.TryActiveState(use);
//         }

//         #endregion

//         #region 测试方法

//         [Button("输出全部状态"), FoldoutGroup("状态机测试按钮")]
//         public void Test_OutPutAllStateRunning(string befo = "状态机：")
//         {
//             string all = befo + "现在运行的有：";
//             foreach (var i in MainRunningStates)
//             {
//                 all += i.GetKey() + " , ";
//             }
//             /*Debug.LogWarning(all);*/
//         }
//         #endregion
//     }

//     #endregion


// }
