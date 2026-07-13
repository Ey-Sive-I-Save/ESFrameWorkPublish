using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("技能轨道运行时测试模块")]
    public sealed class EntityStateSkillRuntimeTestModule : EntityStateModuleBase
    {
        [TitleGroup("技能轨道运行时测试", "只测试 SkillTrackProcessInfo.sequence 注册为临时状态，不经过完整 SkillDefinitionDataInfo。")]
        [LabelText("启用按键测试")]
        public bool enableKeyRelease = true;

        [TitleGroup("技能轨道运行时测试")]
        [LabelText("测试技能轨道流程")]
        [ListDrawerSettings(DefaultExpandedState = true, ShowIndexLabels = true)]
        public List<SkillTrackProcessInfo> trackProcesses = new List<SkillTrackProcessInfo>();

        [TitleGroup("运行状态")]
        [ShowInInspector, ReadOnly, LabelText("上次播放索引")]
        public int lastPlayedIndex = -1;

        [TitleGroup("运行状态")]
        [ShowInInspector, ReadOnly, LabelText("上次播放流程")]
        public SkillTrackProcessInfo lastPlayedTrackProcess;

        [TitleGroup("运行状态")]
        [ShowInInspector, ReadOnly, LabelText("上次临时状态Key")]
        public string lastTemporaryKey;

        protected override void Update()
        {
            base.Update();

            if (!enableKeyRelease || trackProcesses == null)
                return;

            int count = trackProcesses.Count;
            for (int i = 0; i < count; i++)
            {
                SkillTrackProcessInfo trackProcess = trackProcesses[i];
                if (trackProcess == null || trackProcess.releaseKey == KeyCode.None)
                    continue;

                if (Input.GetKeyDown(trackProcess.releaseKey))
                    PlayTrackProcess(i);
            }
        }

        [TitleGroup("调试操作")]
        [Button("播放第一个轨道流程", ButtonSizes.Medium)]
        [GUIColor(0.45f, 0.85f, 1f)]
        public bool PlayFirstTrackProcess()
        {
            return PlayTrackProcess(0);
        }

        [TitleGroup("调试操作")]
        [Button("停止上次临时状态", ButtonSizes.Medium)]
        [GUIColor(1f, 0.65f, 0.35f)]
        public bool StopLastTemporaryState()
        {
            if (string.IsNullOrEmpty(lastTemporaryKey))
            {
                Debug.LogWarning("[SkillRuntimeTestModule] 没有上次临时状态Key。");
                return false;
            }

            StateMachine stateMachine = ResolveStateMachine();
            if (stateMachine == null)
                return false;

            bool success = stateMachine.RemoveTemporaryAnimation(lastTemporaryKey);
            Debug.Log($"[SkillRuntimeTestModule] 停止临时技能轨道状态：Key={lastTemporaryKey} Success={success}");
            return success;
        }

        public bool PlayTrackProcess(int index)
        {
            if (trackProcesses == null || index < 0 || index >= trackProcesses.Count)
                return false;

            return PlayTrackProcess(trackProcesses[index], index);
        }

        public bool PlayTrackProcess(SkillTrackProcessInfo trackProcess)
        {
            return PlayTrackProcess(trackProcess, FindTrackProcessIndex(trackProcess));
        }

        private bool PlayTrackProcess(SkillTrackProcessInfo trackProcess, int index)
        {
            if (trackProcess == null || trackProcess.sequence == null)
            {
                Debug.LogWarning("[SkillRuntimeTestModule] SkillTrackProcessInfo 或 sequence 为空。");
                return false;
            }

            StateMachine stateMachine = ResolveStateMachine();
            if (stateMachine == null)
                return false;

            StateLayerType layer = trackProcess.GetRuntimeLayer();
            string tempKey = BuildTemporarySkillKey(trackProcess, index);
            bool success = stateMachine.AddTemporarySkillSequence(
                tempKey,
                trackProcess.sequence,
                trackProcess.baseStateInfo,
                layer,
                trackProcess.forceEnterState);

            if (!success)
            {
                Debug.LogWarning($"[SkillRuntimeTestModule] 播放技能轨道流程失败：Key={tempKey} Layer={layer}");
                return false;
            }

            lastPlayedIndex = index;
            lastPlayedTrackProcess = trackProcess;
            lastTemporaryKey = tempKey;
            Debug.Log($"[SkillRuntimeTestModule] 播放技能轨道流程：Key={tempKey} Layer={layer}");
            return true;
        }

        private StateMachine ResolveStateMachine()
        {
            EntityStateDomain domain = MyDomain;
            StateMachine stateMachine = domain != null ? domain.stateMachine : null;
            if (stateMachine != null)
                return stateMachine;

            Debug.LogWarning("[SkillRuntimeTestModule] StateDomain 或 StateMachine 为空。");
            return null;
        }

        private int FindTrackProcessIndex(SkillTrackProcessInfo trackProcess)
        {
            if (trackProcesses == null || trackProcess == null)
                return -1;

            int count = trackProcesses.Count;
            for (int i = 0; i < count; i++)
            {
                if (trackProcesses[i] == trackProcess)
                    return i;
            }

            return -1;
        }

        private static string BuildTemporarySkillKey(SkillTrackProcessInfo trackProcess, int index)
        {
            string skillName = trackProcess != null && !string.IsNullOrEmpty(trackProcess.name)
                ? trackProcess.name
                : "SkillTrackProcess";

            return index >= 0 ? skillName + "_TrackProcess_" + index : skillName + "_TrackProcess";
        }
    }
}
