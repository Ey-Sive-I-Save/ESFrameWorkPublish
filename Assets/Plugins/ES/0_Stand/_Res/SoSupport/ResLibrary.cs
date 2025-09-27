using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public class ResLibrary : SoLibrary<ResBook>
    {

    }
    [Serializable]
    public class ResBook : Book<ResPage>
    {

    }
    [Serializable]
    public class ResPage : IString
    {
        [LabelText("资源页名")]
        public string Name = "资源页名";
        public string GetSTR()
        {
            return GetSTR();
        }

        public void SetSTR(string str)
        {
            Name = str;
        }
    }
}

