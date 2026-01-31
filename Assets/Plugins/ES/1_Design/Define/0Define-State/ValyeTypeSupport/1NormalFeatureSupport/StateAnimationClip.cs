using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace ES
{
      /// <summary>
    /// 动画片段配置
    /// </summary>
    [System.Serializable]
    public class ClipConfiguration
    {
        [Tooltip("Clip在配置表中的ID")]
        public string ClipID;
        
        [Tooltip("动画片段引用(编辑器预览用)")]
        public AnimationClip Clip;
        
        [Tooltip("播放速度乘数")]
        public float SpeedMultiplier = 1f;
        
        [Tooltip("是否循环")]
        public bool IsLooping = false;
        
        [Tooltip("动画层权重")]
        [Range(0f, 1f)]
        public float LayerWeight = 1f;
        
        // [Tooltip("动画混合模式")]
        // public BlendMode BlendMode = BlendMode.Blend;
        
       // [Tooltip("IK相关配置")]
        //public IKConfiguration IKConfig = new IKConfiguration();
    }
}
