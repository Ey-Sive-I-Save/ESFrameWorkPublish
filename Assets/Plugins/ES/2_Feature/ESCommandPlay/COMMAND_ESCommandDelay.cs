using System;
using Sirenix.OdinInspector;

namespace ES
{
    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.Delay)]
    public sealed class ESCommand_Delay : ESCommand, IESCommandPlayable
    {
        [LabelText("延时秒数")]
        [MinValue(0f)]
        public float seconds = 1f;

        private float remainSeconds;

        public override string CommandName
        {
            get { return "延时"; }
        }

        public override void Invoke()
        {
        }

        public void OnPlayStart(ESCommandPlayer player)
        {
            remainSeconds = seconds;
        }

        public ESRunState TickPlay(ESCommandPlayer player, ref ESCommandPlayFrame frame)
        {
            if (frame.cancelRequested)
                return ESRunState.Canceled;

            remainSeconds -= frame.deltaTime;
            return remainSeconds > 0f ? ESRunState.Running : ESRunState.Succeeded;
        }

        public void OnPlayCancel(ESCommandPlayer player)
        {
            remainSeconds = 0f;
        }
    }
}
