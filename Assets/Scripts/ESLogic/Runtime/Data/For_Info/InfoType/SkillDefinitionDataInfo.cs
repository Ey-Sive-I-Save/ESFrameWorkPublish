using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [ESCreatePath("鏁版嵁淇℃伅", "瀹屾暣鎶€鑳戒綋鏁版嵁淇℃伅")]
    public class SkillDefinitionDataInfo : SoDataInfo
    {
        [Title("鍩虹")]
        [LabelText("Skill Tags")]
        public List<string> tags = new List<string>();

        [Title("Runtime Key")]
        [HideLabel, InlineProperty]
        public ESSkillConfigKey skillKey = new ESSkillConfigKey();

        [LabelText("缁戝畾杞ㄩ亾杩囩▼")]
        public SkillTrackProcessInfo trackProcess;

        [LabelText("Base State")]
        public StateAniDataInfo baseStateInfo;

        [Title("Unlock And Upgrade")]
        [LabelText("榛樿瑙ｉ攣")]
        public bool unlockedByDefault = true;

        [LabelText("Max Enhance Level")]
        public int maxEnhanceLevel = 1;

        [LabelText("Linked Skills")]
        public List<SkillDefinitionDataInfo> linkedSkills = new List<SkillDefinitionDataInfo>();

        [Title("Resource And Charges")]
        [LabelText("娆℃暟妯″紡")]
        public SkillChargeMode chargeMode = SkillChargeMode.None;

        [ShowIf(nameof(UsesCharges))]
        [LabelText("Max Charges")]
        public int maxCharges = 1;

        [ShowIf(nameof(UsesCharges))]
        [LabelText("鎭㈠闂撮殧")]
        public float rechargeInterval = 1f;

        [LabelText("Shared Resource Group")]
        public string sharedResourceGroup;

        [Title("閲婃斁鎺у埗")]
        [LabelText("鎵撴柇妯″紡")]
        public SkillCastInterruptMode interruptMode = SkillCastInterruptMode.ManualCancelable;

        [LabelText("鍏佽涓诲姩鍙栨秷")]
        public bool canManualCancel = true;

        [LabelText("閲婃斁鍓嶆彁")]
        [SerializeReference]
        public ESGetBoolExpression castCondition;

        [LabelText("Initial Target Expression")]
        [SerializeReference]
        public ESGetGameObjectExpression initialTargetExpression;

        [Title("Value Prepare")]
        [LabelText("鍩虹鍊嶇巼")]
        public float baseMultiplier = 1f;

        [LabelText("Dynamic Multiplier Expression")]
        [SerializeReference]
        public ESGetFloatExpression dynamicMultiplierExpression;

        [Title("杩愯涓婁笅鏂囬濉厖")]
        [LabelText("Prefill User From State Host")]
        public bool prefillUserFromStateHost = true;

        [LabelText("鍒濆鐩爣鍔犲叆鐩爣鍒楄〃")]
        public bool addInitialTargetToList = true;

        [Title("鎵╁睍鏀寔")]
        [LabelText("鐩告満鏀寔")]
        public SkillCameraSupportMode cameraSupport = SkillCameraSupportMode.None;

        [LabelText("Continuous Skill")]
        public bool isContinuousSkill;

        [LabelText("鍚敤鍥炶皟鎸傜偣")]
        public bool enableCallbacks;

        private bool UsesCharges()
        {
            return chargeMode == SkillChargeMode.FixedCharges || chargeMode == SkillChargeMode.RechargeOverTime;
        }
    }

    [Serializable]
    public sealed class SkillRuntimePreparedValues
    {
        public float multiplier = 1f;
        public bool canCast = true;
    }
}
