using UnityEngine;

namespace ES
{
    public class CircleDependenceSamplePrefabRef : MonoBehaviour
    {
        [Header("Circular Dependency Test")]
        public CircleDenpendceSamples SampleSO;

        [TextArea]
        public string Note;
    }
}
