#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    //[CreateAssetMenu(fileName = "ShaderAutoUpdater", menuName = "Gaskellgames/ShaderAutoUpdater")]
    public class ShaderAutoUpdater_SO : GgScriptableObject
    {
        #region Variables

        private enum Pipeline
        {
            Unknown,
            BIRP,
            URP,
            HDRP,
            Other
        }
        
        [Title("Shaders", "Shader references for each Render Pipeline.")]
        [SerializeField, ReadOnly]
        [Tooltip("The current render pipeline target.")]
        private Pipeline targetRenderPipeline = Pipeline.Unknown;
        
        [SerializeField, Required]
        [Tooltip("Reference to shader used for Built In Render Pipeline (BIRP).")]
        private Shader BIRP;
        
        [SerializeField, Required]
        [Tooltip("Reference to shader used for Universal Render Pipeline (URP).")]
        private Shader URP;
        
        [SerializeField, Required]
        [Tooltip("Reference to shader used for High Definition Render Pipeline (HDRP).")]
        private Shader HDRP;
        
        [Title("Materials", "Material References.")]
        [SerializeField, Required]
        [Tooltip("Reference to all materials to be auto-updated based on render pipeline.")]
        private List<Material> materials;

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Functions

        /// <summary>
        /// Update the shader used in each material based on the passed pipeline.
        /// </summary>
        /// <param name="pipeline"></param>
        private void UpdateMaterials(Pipeline pipeline)
        {
            switch (pipeline)
            {
                case Pipeline.BIRP:
                    if (!BIRP)
                    {
                        Log(GgLogType.Warning, "Warning: Missing shader reference: ShaderAutoUpdater failed to update shaders for the current pipeline.");
                        break;
                    }
                    for (int i = materials.Count - 1; i >= 0; i--)
                    {
                        if (!materials[i])
                        {
                            materials.RemoveAt(i);
                            continue;
                        }
                        if (materials[i].shader == BIRP) { continue; }
                        materials[i].shader = BIRP;
                        Log(GgLogType.Info, "Material [{0}] updated to use shader [{1}].", materials[i].name, BIRP);
                    }
                    break;
                
                case Pipeline.URP:
                    if (!URP)
                    {
                        Log(GgLogType.Warning, "Warning: Missing shader reference: ShaderAutoUpdater failed to update shaders for the current pipeline.");
                        break;
                    }
                    for (int i = materials.Count - 1; i >= 0; i--)
                    {
                        if (!materials[i])
                        {
                            materials.RemoveAt(i);
                            continue;
                        }
                        if (materials[i].shader == URP) { continue; }
                        materials[i].shader = URP;
                        Log(GgLogType.Info, "Material [{0}] updated to use shader [{1}].", materials[i].name, URP);
                    }
                    break;
                
                case Pipeline.HDRP:
                    if (!HDRP)
                    {
                        Log(GgLogType.Warning, "Warning: Missing shader reference: ShaderAutoUpdater failed to update shaders for the current pipeline.");
                        break;
                    }
                    for (int i = materials.Count - 1; i >= 0; i--)
                    {
                        if (!materials[i])
                        {
                            materials.RemoveAt(i);
                            continue;
                        }
                        if (materials[i].shader == HDRP) { continue; }
                        materials[i].shader = HDRP;
                        Log(GgLogType.Info, "Material [{0}] updated to use shader [{1}].", materials[i].name, HDRP);
                    }
                    break;
                
                case Pipeline.Other:
                case Pipeline.Unknown:
                default:
                    Log(GgLogType.Warning, "Warning: Other or Unknown pipeline detected. ShaderAutoUpdater failed to update shaders for the current pipeline.");
                    break;
            }
        }

        /// <summary>
        /// Use <see cref="GraphicsSettings.currentRenderPipeline"/> to determine the current render pipeline.
        /// </summary>
        /// <returns></returns>
        private Pipeline GetCurrentRenderPipeline()
        {
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                return Pipeline.BIRP;
            }

            if (GraphicsSettings.currentRenderPipeline.GetType().Name == "HDRenderPipelineAsset")
            {
                return Pipeline.HDRP;
            }

            if (GraphicsSettings.currentRenderPipeline.GetType().Name == "UniversalRenderPipelineAsset")
            {
                return Pipeline.URP;
            }

            return Pipeline.Other;
        }
        
        /// <summary>
        /// Get the pipeline for the current target.
        /// </summary>
        /// <returns></returns>
        private Pipeline GetTargetRenderPipeline()
        {
            if (GetOverrideRenderPipeline(out Pipeline pipeline))
            {
                Log(GgLogType.Info, "Using override render pipeline: {0}", pipeline);
                return pipeline;
            }
            
            pipeline = GetDefaultRenderPipeline();
            Log(GgLogType.Info, "Using default render pipeline: {0}", pipeline);
            return pipeline;
        }

        /// <summary>
        /// Use <see cref="GraphicsSettings.defaultRenderPipeline"/> to determine the default render pipeline.
        /// </summary>
        private Pipeline GetDefaultRenderPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                return Pipeline.BIRP;
            }

            if (GraphicsSettings.defaultRenderPipeline.GetType().Name == "HDRenderPipelineAsset")
            {
                return Pipeline.HDRP;
            }

            if (GraphicsSettings.defaultRenderPipeline.GetType().Name == "UniversalRenderPipelineAsset")
            {
                return Pipeline.URP;
            }

            return Pipeline.Other;
        }

        /// <summary>
        /// Use <see cref="QualitySettings.renderPipeline"/> to determine the overriding render pipeline for the current quality level.
        /// </summary>
        /// <param name="pipeline"></param>
        /// <returns></returns>
        private bool GetOverrideRenderPipeline(out Pipeline pipeline)
        {
            if (QualitySettings.renderPipeline == null)
            {
                // False: The active render pipeline is the default render pipeline
                pipeline = Pipeline.Unknown;
                return false;
            }

            // True: The active render pipeline is the overriding render pipeline...
            if (QualitySettings.renderPipeline.GetType().Name == "HDRenderPipelineAsset")
            {
                // ... HDRP
                pipeline = Pipeline.HDRP;
                return true;
            }

            if (QualitySettings.renderPipeline.GetType().Name == "UniversalRenderPipelineAsset")
            {
                // ... URP
                pipeline = Pipeline.URP;
                return true;
            }

            // ... Other
            pipeline = Pipeline.Other;
            return true;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Internal Functions
        
        internal void UpdateMaterialsForCurrentTargetPipeline()
        {
            targetRenderPipeline = GetTargetRenderPipeline();
            UpdateMaterials(targetRenderPipeline);
        }
        
        internal void UpdateMaterialsForBIRP()
        {
            targetRenderPipeline = Pipeline.BIRP;
            UpdateMaterials(targetRenderPipeline);
        }
        
        internal void UpdateMaterialsForURP()
        {
            targetRenderPipeline = Pipeline.URP;
            UpdateMaterials(targetRenderPipeline);
        }
        
        internal void UpdateMaterialsForHDRP()
        {
            targetRenderPipeline = Pipeline.HDRP;
            UpdateMaterials(targetRenderPipeline);
        }

        #endregion
        
    } // class end 
}
#endif