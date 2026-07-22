using UnityEngine;

namespace ES.Samples{
    public class CircleDependenceSamplePrefabRef : MonoBehaviour
    {
        [Header("Circular Dependency Test")]
        public CircleDenpendceSamples SampleSO;

        public ESAssetReferPrefab refer;

        [TextArea]
        public string Note;

        void Update()
        {
            Debug.Log("Circular Dependence Sample Prefab Ref Update");
        }
    }
}
