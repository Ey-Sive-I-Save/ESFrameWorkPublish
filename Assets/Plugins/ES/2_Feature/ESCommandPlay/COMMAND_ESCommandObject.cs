using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.ObjectSetActive)]
    public sealed class ESCommand_Object_SetActive : ESCommand
    {
        [LabelText("\u76ee\u6807\u5bf9\u8c61")]
        public GameObject target;

        [LabelText("\u8bbe\u4e3a\u6fc0\u6d3b")]
        public bool active = true;

        public override string CommandName
        {
            get { return active ? "\u6fc0\u6d3b\u5bf9\u8c61" : "\u505c\u7528\u5bf9\u8c61"; }
        }

        public override void Invoke()
        {
            if (target != null)
                target.SetActive(active);
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.ObjectSetBehaviourEnabled)]
    public sealed class ESCommand_Object_SetBehaviourEnabled : ESCommand
    {
        [LabelText("\u76ee\u6807\u884c\u4e3a\u7ec4\u4ef6")]
        public Behaviour target;

        [LabelText("\u8bbe\u4e3a\u542f\u7528")]
        public bool enabledValue = true;

        public override string CommandName
        {
            get { return enabledValue ? "\u542f\u7528\u884c\u4e3a\u7ec4\u4ef6" : "\u7981\u7528\u884c\u4e3a\u7ec4\u4ef6"; }
        }

        public override void Invoke()
        {
            if (target != null)
                target.enabled = enabledValue;
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.ObjectSetActiveList)]
    public sealed class ESCommand_Object_SetActiveList : ESCommand
    {
        [LabelText("\u76ee\u6807\u5bf9\u8c61\u5217\u8868")]
        public List<GameObject> targets = new List<GameObject>(4);

        [LabelText("\u8bbe\u4e3a\u6fc0\u6d3b")]
        public bool active = true;

        public override string CommandName
        {
            get { return active ? "\u6279\u91cf\u6fc0\u6d3b\u5bf9\u8c61" : "\u6279\u91cf\u505c\u7528\u5bf9\u8c61"; }
        }

        public override void Invoke()
        {
            if (targets == null)
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                GameObject target = targets[i];
                if (target != null)
                    target.SetActive(active);
            }
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.ObjectSetBehaviourEnabledList)]
    public sealed class ESCommand_Object_SetBehaviourEnabledList : ESCommand
    {
        [LabelText("\u76ee\u6807\u884c\u4e3a\u7ec4\u4ef6\u5217\u8868")]
        public List<Behaviour> targets = new List<Behaviour>(4);

        [LabelText("\u8bbe\u4e3a\u542f\u7528")]
        public bool enabledValue = true;

        public override string CommandName
        {
            get { return enabledValue ? "\u6279\u91cf\u542f\u7528\u884c\u4e3a\u7ec4\u4ef6" : "\u6279\u91cf\u7981\u7528\u884c\u4e3a\u7ec4\u4ef6"; }
        }

        public override void Invoke()
        {
            if (targets == null)
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                Behaviour target = targets[i];
                if (target != null)
                    target.enabled = enabledValue;
            }
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.ObjectSetLocalPosition)]
    public sealed class ESCommand_Object_SetLocalPosition : ESCommand
    {
        [LabelText("\u76ee\u6807\u53d8\u6362\u7ec4\u4ef6")]
        public Transform target;

        [LabelText("\u672c\u5730\u5750\u6807")]
        public Vector3 localPosition;

        public override string CommandName
        {
            get { return "\u8bbe\u7f6e\u672c\u5730\u5750\u6807"; }
        }

        public override void Invoke()
        {
            if (target != null)
                target.localPosition = localPosition;
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.ObjectSetLocalEulerAngles)]
    public sealed class ESCommand_Object_SetLocalEulerAngles : ESCommand
    {
        [LabelText("\u76ee\u6807\u53d8\u6362\u7ec4\u4ef6")]
        public Transform target;

        [LabelText("\u672c\u5730\u6b27\u62c9\u89d2")]
        public Vector3 localEulerAngles;

        public override string CommandName
        {
            get { return "\u8bbe\u7f6e\u672c\u5730\u65cb\u8f6c"; }
        }

        public override void Invoke()
        {
            if (target != null)
                target.localEulerAngles = localEulerAngles;
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.ObjectSetLocalScale)]
    public sealed class ESCommand_Object_SetLocalScale : ESCommand
    {
        [LabelText("\u76ee\u6807\u53d8\u6362\u7ec4\u4ef6")]
        public Transform target;

        [LabelText("\u672c\u5730\u7f29\u653e")]
        public Vector3 localScale = Vector3.one;

        public override string CommandName
        {
            get { return "\u8bbe\u7f6e\u672c\u5730\u7f29\u653e"; }
        }

        public override void Invoke()
        {
            if (target != null)
                target.localScale = localScale;
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.ObjectSetLocalPositionList)]
    public sealed class ESCommand_Object_SetLocalPositionList : ESCommand
    {
        [LabelText("\u76ee\u6807\u53d8\u6362\u7ec4\u4ef6\u5217\u8868")]
        public List<Transform> targets = new List<Transform>(4);

        [LabelText("\u672c\u5730\u5750\u6807")]
        public Vector3 localPosition;

        public override string CommandName
        {
            get { return "\u6279\u91cf\u8bbe\u7f6e\u672c\u5730\u5750\u6807"; }
        }

        public override void Invoke()
        {
            if (targets == null)
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                Transform target = targets[i];
                if (target != null)
                    target.localPosition = localPosition;
            }
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.ObjectSetLocalEulerAnglesList)]
    public sealed class ESCommand_Object_SetLocalEulerAnglesList : ESCommand
    {
        [LabelText("\u76ee\u6807\u53d8\u6362\u7ec4\u4ef6\u5217\u8868")]
        public List<Transform> targets = new List<Transform>(4);

        [LabelText("\u672c\u5730\u6b27\u62c9\u89d2")]
        public Vector3 localEulerAngles;

        public override string CommandName
        {
            get { return "\u6279\u91cf\u8bbe\u7f6e\u672c\u5730\u65cb\u8f6c"; }
        }

        public override void Invoke()
        {
            if (targets == null)
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                Transform target = targets[i];
                if (target != null)
                    target.localEulerAngles = localEulerAngles;
            }
        }
    }

    [Serializable]
    [TypeRegistryItem(ESCommandTypeName.ObjectSetLocalScaleList)]
    public sealed class ESCommand_Object_SetLocalScaleList : ESCommand
    {
        [LabelText("\u76ee\u6807\u53d8\u6362\u7ec4\u4ef6\u5217\u8868")]
        public List<Transform> targets = new List<Transform>(4);

        [LabelText("\u672c\u5730\u7f29\u653e")]
        public Vector3 localScale = Vector3.one;

        public override string CommandName
        {
            get { return "\u6279\u91cf\u8bbe\u7f6e\u672c\u5730\u7f29\u653e"; }
        }

        public override void Invoke()
        {
            if (targets == null)
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                Transform target = targets[i];
                if (target != null)
                    target.localScale = localScale;
            }
        }
    }
}
