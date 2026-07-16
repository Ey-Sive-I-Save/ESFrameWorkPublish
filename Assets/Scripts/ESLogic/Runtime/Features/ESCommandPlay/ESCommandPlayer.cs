using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public sealed class ESCommandPlayer : MonoBehaviour
    {
        [LabelText("\u542f\u52a8\u65f6\u64ad\u653e")]
        public bool playOnStart;

        [LabelText("\u542f\u7528\u65f6\u81ea\u52a8\u6ce8\u518c")]
        public bool registerOnEnable = true;

        [LabelText("\u7981\u7528\u65f6\u505c\u6b62\u64ad\u653e")]
        public bool stopOnDisable = true;

        [LabelText("\u64ad\u653e\u4e8b\u4ef6")]
        public ESCommandEvent eventToPlay = new ESCommandEvent();

        private ESCommandEvent playingEvent;
        private int playingIndex;
        private ESRunState state = ESRunState.None;
        private bool cancelRequested;
        private IESCommandPlayable currentPlayable;

        public ESRunState State
        {
            get { return state; }
        }

        public bool IsPlaying
        {
            get { return state == ESRunState.Running; }
        }

        private void OnEnable()
        {
            if (registerOnEnable && state == ESRunState.Running)
                ESCommandPlayerRunner.Register(this);
        }

        private void Start()
        {
            if (playOnStart)
                Play(eventToPlay);
        }

        private void OnDisable()
        {
            if (stopOnDisable && state == ESRunState.Running)
                Stop();
            else
                ESCommandPlayerRunner.Unregister(this);
        }

        [Button("\u64ad\u653e")]
        public void Play()
        {
            Play(eventToPlay);
        }

        public void Play(ESCommandEvent commandEvent)
        {
            playingEvent = commandEvent;
            playingIndex = 0;
            cancelRequested = false;
            currentPlayable = null;
            state = commandEvent == null || commandEvent.Count == 0
                ? ESRunState.Skipped
                : ESRunState.Running;

            if (state == ESRunState.Running)
                ESCommandPlayerRunner.Register(this);
        }

        [Button("\u53d6\u6d88")]
        public void Cancel()
        {
            if (state != ESRunState.Running)
                return;

            cancelRequested = true;
        }

        [Button("\u505c\u6b62")]
        public void Stop()
        {
            CancelCurrentPlayable();
            playingEvent = null;
            playingIndex = 0;
            cancelRequested = false;
            state = ESRunState.Canceled;
            ESCommandPlayerRunner.Unregister(this);
        }

        public ESRunState Tick(float time, float deltaTime)
        {
            if (state != ESRunState.Running)
                return state;

            ESCommandPlayFrame frame = new ESCommandPlayFrame(time, deltaTime, cancelRequested);
            if (frame.cancelRequested)
            {
                CancelCurrentPlayable();
                state = ESRunState.Canceled;
                return state;
            }

            while (playingEvent != null && playingIndex < playingEvent.Count)
            {
                ESCommand command = playingEvent.commands[playingIndex];
                if (command == null || !command.enabled)
                {
                    playingIndex++;
                    continue;
                }

                IESCommandPlayable playable = command as IESCommandPlayable;
                if (playable != null)
                {
                    if (currentPlayable != playable)
                    {
                        currentPlayable = playable;
                        currentPlayable.OnPlayStart(this);
                    }

                    ESRunState playState = currentPlayable.TickPlay(this, ref frame);
                    if (playState == ESRunState.Running)
                        return ESRunState.Running;

                    currentPlayable = null;
                    if (playState == ESRunState.Failed || playState == ESRunState.Canceled)
                    {
                        state = playState;
                        return state;
                    }

                    playingIndex++;
                    continue;
                }

                ESRunState commandState = command.InvokeCommand();
                if (commandState == ESRunState.Failed || commandState == ESRunState.Canceled)
                {
                    state = commandState;
                    return state;
                }

                playingIndex++;
            }

            state = ESRunState.Succeeded;
            return state;
        }

        private void CancelCurrentPlayable()
        {
            if (currentPlayable != null)
            {
                currentPlayable.OnPlayCancel(this);
                currentPlayable = null;
            }
        }
    }
}
