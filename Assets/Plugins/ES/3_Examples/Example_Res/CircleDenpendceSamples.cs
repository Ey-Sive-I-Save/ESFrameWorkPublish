using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
    [CreateAssetMenu(menuName = "ESFrameWork/Examples/Res/CircleDenpendceSamples")]
    public class CircleDenpendceSamples : ScriptableObject
    {
        [Header("Circular Dependency Test")]
        public GameObject PrefabRef;

        [TextArea]
        public string Note;
    }
}
