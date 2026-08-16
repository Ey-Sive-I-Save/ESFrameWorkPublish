using UnityEngine;
using UnityEngine.AI;

namespace ES
{
    /// <summary>正式 World Scene 对 NavMeshData 的稳定引用；运行时只负责装载已烘焙数据。</summary>
    [DisallowMultipleComponent]
    public sealed class ESWorldBakedNavMeshReference : MonoBehaviour
    {
        [SerializeField] private NavMeshData navigationData;
        private NavMeshDataInstance instance;

        public NavMeshData NavigationData => navigationData;
        public void SetNavigationData(NavMeshData value) => navigationData = value;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (navigationData != null && !instance.valid)
                instance = NavMesh.AddNavMeshData(navigationData, transform.position, transform.rotation);
        }

        private void OnDisable()
        {
            if (instance.valid) instance.Remove();
            instance = default;
        }
    }
}
