using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Lightweight serializable command.
    /// Commands own their typed parameters as fields; do not use object[] argument bags.
    /// </summary>
    [Serializable]
    public abstract class ESCommand
    {
        [LabelText("启用")]
        public bool enabled = true;

        [LabelText("备注")]
        public string remark;

        public virtual string CommandName
        {
            get { return GetType().Name; }
        }

        public ESRunState InvokeCommand()
        {
            if (!enabled)
                return ESRunState.Skipped;

            Invoke();
            return ESRunState.Succeeded;
        }

        public abstract void Invoke();
    }

    [Serializable]
    public sealed class ESCommandEvent
    {
        [LabelText("命令列表")]
        [SerializeReference]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        public List<ESCommand> commands = new List<ESCommand>(4);

        public int Count
        {
            get { return commands == null ? 0 : commands.Count; }
        }

        public ESRunState Invoke()
        {
            if (commands == null || commands.Count == 0)
                return ESRunState.Skipped;

            ESRunState finalState = ESRunState.Skipped;
            for (int i = 0; i < commands.Count; i++)
            {
                ESCommand command = commands[i];
                if (command == null)
                    continue;

                ESRunState state = command.InvokeCommand();
                if (state == ESRunState.Failed || state == ESRunState.Canceled)
                    return state;

                if (state == ESRunState.Succeeded)
                    finalState = ESRunState.Succeeded;
            }

            return finalState;
        }

        public void Clear()
        {
            if (commands != null)
                commands.Clear();
        }
    }
}
