using DG.Tweening;
using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace ES {
    [Serializable,TypeRegistryItem("0※UI操作-Dotween",icon:SdfIconType.BatteryCharging)]
    public class UITweenOutputOperation : IOutputOperationUI
    {
        [SerializeReference,LabelText("使用的Tween")]
        public UITweenGetter TweenGetter;

        [ToggleGroup("enableCancel", "可取消的")]
        public bool enableCancel = true;
        [ToggleGroup("enableCancel")]//可取消的话-就会存入缓冲并且可以移除掉
        public UITweenType uiTweenType;
        [ToggleGroup("enableCancel"),LabelText("重启时尽可能选择重头开始(存储初始态)")]
        public bool alwaysReStart = false;
        [ToggleGroup("enableCancel"), LabelText("取消时直接完成")]
        public bool cancelToComplete= true;
        [ToggleGroup("enableCancel"), LabelText("取消时完成-会使用回调"),ShowIf("cancelToComplete")]
        public bool cancelToCompleteAndCallBack = true;


        [ToggleGroup("enableEase", "带曲线的")]
        public bool enableEase = false;
        [ToggleGroup("enableEase"), LabelText("完全自定义曲线")]
        public bool SelfDefine = false;
        [ToggleGroup("enableEase"), LabelText("曲线类型"),ShowIf("@!SelfDefine")]
        public Ease ease = Ease.Unset;
        [ToggleGroup("enableEase"), LabelText("完全自定义曲线"), ShowIf("SelfDefine")]
        public AnimationCurve SelfDefineCurve = AnimationCurve.Linear(0,0,1,1);

        [ToggleGroup("enableTimeControl", "时间扩展控制")]
        public bool enableTimeControl = false;
        [ToggleGroup("enableTimeControl"),LabelText("延迟时间")]
        public float delay = -1;
        [ToggleGroup("enableTimeControl"), LabelText("忽略Unity的TimeScale")]
        public bool ignoreUnityTimeScale = false;

        [ToggleGroup("enableOther", "其他设置")]
        public bool enableOther = false;
        [ToggleGroup("enableOther"), LabelText("是相对的<Reletive>")]
        public bool rele = false;
        public void TryOperation(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            
            if (enableCancel)//可取消，则会缓存内容
            {
                var t = on.CachingUITweens.GetElement(uiTweenType, this);
                if (t == null)
                {
                    var newT = GetAndDecorateNewTween(on, from);
                    newT.SetAutoKill(false);
                    on.CachingUITweens.TryAddOnly(uiTweenType, this, newT);
                }
                else
                {
                    if (alwaysReStart||t.IsComplete())
                    {
                        t.Restart();
                    }
                }
            }
            else
            {
                GetAndDecorateNewTween(on,from);//自动就执行了哈-√
            }
        }

        public void TryCancel(ESUIElement on, ESUIElement from, ILink_UI_OperationOptions with)
        {
            if (enableCancel)//可取消，则会缓存内容
            {
                var t = on.CachingUITweens.GetElement(uiTweenType, this);
                if (t != null)
                {
                    if (t.IsComplete())
                    {

                    }
                    else
                    {
                        //没完成
                        if (cancelToComplete)
                        {
                            t.Complete(cancelToCompleteAndCallBack);
                        }
                    }
                }
            }
            //滚-不支持取消
        }
        public Tween GetAndDecorateNewTween(ESUIElement on, ESUIElement from)
        {
            Tween tween = TweenGetter.GetTween(on,from);
            if (tween != null)
            {
                if (enableEase)
                {
                    if (SelfDefine) tween.SetEase(SelfDefineCurve);
                    else tween.SetEase(ease);
                }

                if (enableTimeControl)
                {
                    if (delay > 0) tween.SetDelay(delay);
                    if (ignoreUnityTimeScale) tween.SetUpdate(true);
                }
                if (enableOther)
                {
                    if (rele) tween.SetRelative();
                }
            }

            return tween;
        }
    }
}

