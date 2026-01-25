using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    public abstract class LibConsumer<Lib> : ESSO
    {
        [LabelText("命名")]
        public string Name = "LibConsumer Name";

        [LabelText("版本号")]
        public string Version = "1.0.0";

        [LabelText("描述")]
        public string Desc = "描述：这个使用者是干啥的";

        [LabelText("使用者列表")]
        public List<Lib> ConsumerLibFolders = new List<Lib>();

        
    }
}
