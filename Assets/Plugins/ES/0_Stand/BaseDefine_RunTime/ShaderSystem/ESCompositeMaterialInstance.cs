using UnityEngine;
using UnityEngine.UI;

namespace ES
{
    /// <summary>
    /// 为 Renderer 材质槽或 UI Graphic 持有一个可恢复、可销毁的运行时材质实例。
    /// Renderer 的普通数值优先使用 MaterialPropertyBlock；只有关键词和渲染状态需要实例。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("【ES】/相机与表现/Composite Material Instance")]
    public sealed class ESCompositeMaterialInstance : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Graphic targetGraphic;
        [SerializeField, Min(0)] private int materialIndex;
        [SerializeField] private bool instantiateOnEnable = true;
        [SerializeField] private bool restoreOnDisable = true;

        private Material sourceMaterial;
        private Material runtimeMaterial;

        public Material SourceMaterial => sourceMaterial;
        public Material RuntimeMaterial => runtimeMaterial;
        public bool HasInstance => runtimeMaterial != null;

        private void Reset()
        {
            ResolveTargets();
        }

        private void OnEnable()
        {
            if (instantiateOnEnable && Application.isPlaying)
                Acquire();
        }

        private void OnDisable()
        {
            if (restoreOnDisable)
                Release();
        }

        private void OnDestroy()
        {
            Release();
        }

        private void OnValidate()
        {
            materialIndex = Mathf.Max(0, materialIndex);
        }

        public void Configure(Renderer renderer, int index = 0)
        {
            if (targetRenderer == renderer && targetGraphic == null && materialIndex == Mathf.Max(0, index))
                return;

            Release();
            targetRenderer = renderer;
            targetGraphic = null;
            materialIndex = Mathf.Max(0, index);
        }

        public void Configure(Graphic graphic)
        {
            if (targetGraphic == graphic && targetRenderer == null)
                return;

            Release();
            targetRenderer = null;
            targetGraphic = graphic;
            materialIndex = 0;
        }

        public Material Acquire()
        {
            ResolveTargets();
            if (runtimeMaterial != null && IsRuntimeMaterialAssigned())
                return runtimeMaterial;

            DestroyRuntimeMaterial();
            sourceMaterial = ResolveSourceMaterial();
            if (!IsCompositeMaterial(sourceMaterial))
            {
                sourceMaterial = null;
                return null;
            }

            runtimeMaterial = new Material(sourceMaterial)
            {
                name = sourceMaterial.name + " (ES Runtime)",
                hideFlags = HideFlags.DontSave
            };
            AssignRuntimeMaterial();
            return runtimeMaterial;
        }

        public void Release()
        {
            RestoreSourceMaterial();
            DestroyRuntimeMaterial();
            sourceMaterial = null;
        }

        public static bool IsCompositeMaterial(Material material)
        {
            return material != null
                && material.shader != null
                && material.shader.name.StartsWith("ES/", System.StringComparison.Ordinal)
                && material.shader.name.IndexOf("Composite URP", System.StringComparison.Ordinal) >= 0;
        }

        private void ResolveTargets()
        {
            if (targetRenderer == null && targetGraphic == null)
            {
                targetRenderer = GetComponent<Renderer>();
                if (targetRenderer == null)
                    targetGraphic = GetComponent<Graphic>();
            }
        }

        private Material ResolveSourceMaterial()
        {
            if (targetGraphic != null)
                return targetGraphic.material;

            if (targetRenderer == null)
                return null;

            Material[] materials = targetRenderer.sharedMaterials;
            return materialIndex >= 0 && materialIndex < materials.Length ? materials[materialIndex] : null;
        }

        private bool IsRuntimeMaterialAssigned()
        {
            if (targetGraphic != null)
                return targetGraphic.material == runtimeMaterial;

            if (targetRenderer == null)
                return false;

            Material[] materials = targetRenderer.sharedMaterials;
            return materialIndex >= 0
                && materialIndex < materials.Length
                && materials[materialIndex] == runtimeMaterial;
        }

        private void AssignRuntimeMaterial()
        {
            if (targetGraphic != null)
            {
                targetGraphic.material = runtimeMaterial;
                targetGraphic.SetMaterialDirty();
                return;
            }

            if (targetRenderer == null)
                return;

            Material[] materials = targetRenderer.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= materials.Length)
                return;

            materials[materialIndex] = runtimeMaterial;
            targetRenderer.sharedMaterials = materials;
        }

        private void RestoreSourceMaterial()
        {
            if (runtimeMaterial == null || sourceMaterial == null)
                return;

            if (targetGraphic != null)
            {
                if (targetGraphic.material == runtimeMaterial)
                {
                    targetGraphic.material = sourceMaterial;
                    targetGraphic.SetMaterialDirty();
                }
                return;
            }

            if (targetRenderer == null)
                return;

            Material[] materials = targetRenderer.sharedMaterials;
            if (materialIndex >= 0
                && materialIndex < materials.Length
                && materials[materialIndex] == runtimeMaterial)
            {
                materials[materialIndex] = sourceMaterial;
                targetRenderer.sharedMaterials = materials;
            }
        }

        private void DestroyRuntimeMaterial()
        {
            if (runtimeMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(runtimeMaterial);
            else
                DestroyImmediate(runtimeMaterial);
            runtimeMaterial = null;
        }
    }
}
