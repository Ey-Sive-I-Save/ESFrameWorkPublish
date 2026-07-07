
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    [ESCreatePath("数据信息", "技能数据信息")]
    public class SKillDataInfo : SoDataInfo, IEditorTrackSupport_GetSequence
    {
        [Title("释放")]
        [LabelText("释放按键")]
        public KeyCode releaseKey = KeyCode.None;

        [LabelText("强制进入状态")]
        public bool forceEnterState = false;

        [Title("状态")]
        [LabelText("基础状态")]
        [Required("技能运行需要一个基础状态模板，用于状态机层级、混合、打断等规则。")]
        public StateAniDataInfo baseStateInfo;

        [LabelText("覆盖状态层级")]
        public bool overrideStateLayer = false;

        [ShowIf(nameof(overrideStateLayer))]
        [LabelText("状态层级")]
        public StateLayerType stateLayer = StateLayerType.Main;

        [LabelText("标准技能序列")]
        public SkillTrackSequence sequence = new SkillTrackSequence();

        public ITrackSequence Sequence => sequence;

        public TrackItemType trackItemType => TrackItemType.Skill;

        public string trackName=> name;

        public StateLayerType GetRuntimeLayer()
        {
            if (overrideStateLayer)
                return stateLayer;

            var sharedData = baseStateInfo != null ? baseStateInfo.sharedData : null;
            var basicConfig = sharedData != null ? sharedData.basicConfig : null;
            return basicConfig != null ? basicConfig.layerType : StateLayerType.Main;
        }

        public override void OnEditorApply()
        {
            base.OnEditorApply();
            
        }
    }
    }

//ES已修正
