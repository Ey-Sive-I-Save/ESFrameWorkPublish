
using UnityEngine;
namespace ES
{
    [ESCreatePath("数据组", "技能数据组")]
    public class SKillDataGroup : SoDataGroup<SkillTrackProcessInfo>
    {
        public override void OnEditorApply()
        {
            base.OnEditorApply();
            foreach (var v in Infos.Values)
            {
                IEditorTrackSupport_GetSequence.AddMenuItem("标准技能INFO", this.name, v.name, v);
            }

        }
    }
}

//ES已修正