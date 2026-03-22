using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Sirenix.OdinInspector;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
   // ==================== 2D笛卡尔混合 ====================

        /// <summary>
        /// 2D笛卡尔混合 - 适用于瞄准偏移
        /// 典型应用: Aim Offset (Yaw/Pitch独立混合)
        /// 性能: O(1)查找最近4个点 + O(1)双线性插值
        /// </summary>
        [Serializable, TypeRegistryItem("2D混合树(笛卡尔型)")]
        // 对应枚举：BlendTree2D_Cartesian
        public class StateAnimationMixCalculatorForBlendTree2DFreeformCartesian : StateAnimationMixCalculatorForBlendTree2D
        {
            public override StateAnimationMixerKind CalculatorKind => StateAnimationMixerKind.BlendTree2D_Cartesian;

            [NonSerialized] private bool _gridPrepared;
            [NonSerialized] private float[] _gridX;
            [NonSerialized] private float[] _gridY;
            [NonSerialized] private int[,] _gridIndex;

            protected override string GetUsageHelp()
            {
                 return "适用：瞄准偏移（Yaw/Pitch）或视角偏移。\n" +
                        "必填：采样点（二维位置 + 动画片段），以及 2 个用于驱动的 float 参数（横向/纵向）。\n" +
                        "建议：采样点构成规则网格，便于插值。";
            }

            protected override string GetCalculatorDisplayName()
            {
                return "2D混合树(笛卡尔型)";
            }

            public override void InitializeCalculator()
            {
                base.InitializeCalculator();
                PrepareGridCache();
            }

            protected override void ComputeDelaunayTriangulation()
            {
                // 笛卡尔模式使用网格结构,不需要三角化
                _sharedTriangles = null;
            }

            protected override void CalculateWeights2D(AnimationCalculatorRuntime runtime, Vector2 input, float deltaTime)
            {
                // 先计算目标权重，全部置零
                for (int i = 0; i < samples.Length; i++)
                    runtime.weightTargetCache[i] = 0f;

                // 找到最近的4个点(形成矩形)
                if (!_gridPrepared)
                    PrepareGridCache();

                bool found = TryGetCellIndices(input, out int i0, out int i1, out int i2, out int i3);
                if (!found)
                {
                    FindClosestGridPoints(input, out i0, out i1, out i2, out i3);
                }

                if (i0 < 0)
                {
                    // 找最近的单点
                    int nearest = FindNearestSample(input);
                    runtime.weightTargetCache[nearest] = 1f;
                }
                else
                {
                    // 双线性插值
                    Vector2 p0 = samples[i0].position;
                    Vector2 p1 = samples[i1].position;
                    Vector2 p2 = samples[i2].position;
                    Vector2 p3 = samples[i3].position;

                    // 计算插值权重
                    float tx = (input.x - p0.x) / (p1.x - p0.x);
                    float ty = (input.y - p0.y) / (p2.y - p0.y);
                    tx = Mathf.Clamp01(tx);
                    ty = Mathf.Clamp01(ty);

                    runtime.weightTargetCache[i0] = (1 - tx) * (1 - ty);
                    runtime.weightTargetCache[i1] = tx * (1 - ty);
                    runtime.weightTargetCache[i2] = (1 - tx) * ty;
                    runtime.weightTargetCache[i3] = tx * ty;
                }

                // 平滑过渡到目标权重
                float smoothingSpeed = smoothTime > 0.001f ? smoothTime * 0.5f : 0.001f;
                for (int i = 0; i < samples.Length; i++)
                {
                    if (runtime.useSmoothing)
                    {
                        runtime.weightCache[i] = Mathf.SmoothDamp(
                            runtime.weightCache[i],
                            runtime.weightTargetCache[i],
                            ref runtime.weightVelocityCache[i],
                            smoothingSpeed,
                            float.MaxValue,
                            deltaTime
                        );
                    }
                    else
                    {
                        runtime.weightCache[i] = runtime.weightTargetCache[i];
                    }
                    runtime.mixer.SetInputWeight(i, runtime.weightCache[i]);
                }
            }

            private void PrepareGridCache()
            {
                _gridPrepared = true;
                _gridX = null;
                _gridY = null;
                _gridIndex = null;

                if (samples == null || samples.Length < 4)
                    return;

                var xList = new List<float>();
                var yList = new List<float>();
                for (int i = 0; i < samples.Length; i++)
                {
                    float x = samples[i].position.x;
                    float y = samples[i].position.y;
                    if (!xList.Contains(x)) xList.Add(x);
                    if (!yList.Contains(y)) yList.Add(y);
                }

                xList.Sort();
                yList.Sort();

                _gridX = xList.ToArray();
                _gridY = yList.ToArray();
                _gridIndex = new int[_gridX.Length, _gridY.Length];

                for (int xi = 0; xi < _gridX.Length; xi++)
                    for (int yi = 0; yi < _gridY.Length; yi++)
                        _gridIndex[xi, yi] = -1;

                for (int i = 0; i < samples.Length; i++)
                {
                    int xi = Array.IndexOf(_gridX, samples[i].position.x);
                    int yi = Array.IndexOf(_gridY, samples[i].position.y);
                    if (xi >= 0 && yi >= 0)
                        _gridIndex[xi, yi] = i;
                }
            }

            private bool TryGetCellIndices(Vector2 input, out int i0, out int i1, out int i2, out int i3)
            {
                i0 = i1 = i2 = i3 = -1;
                if (_gridX == null || _gridY == null || _gridX.Length < 2 || _gridY.Length < 2)
                    return false;

                int x0 = FindGridIndex(_gridX, input.x);
                int y0 = FindGridIndex(_gridY, input.y);

                if (x0 < 0 || y0 < 0 || x0 >= _gridX.Length - 1 || y0 >= _gridY.Length - 1)
                    return false;

                int x1 = x0 + 1;
                int y1 = y0 + 1;

                i0 = _gridIndex[x0, y0];
                i1 = _gridIndex[x1, y0];
                i2 = _gridIndex[x0, y1];
                i3 = _gridIndex[x1, y1];

                return i0 >= 0 && i1 >= 0 && i2 >= 0 && i3 >= 0;
            }

            private int FindGridIndex(float[] arr, float value)
            {
                for (int i = 0; i < arr.Length - 1; i++)
                {
                    if (value >= arr[i] && value <= arr[i + 1])
                        return i;
                }
                return -1;
            }

            private void FindClosestGridPoints(Vector2 input, out int i0, out int i1, out int i2, out int i3)
            {
                // 简化实现:找最近的4个点
                i0 = i1 = i2 = i3 = -1;

                float minDist0 = float.MaxValue;
                float minDist1 = float.MaxValue;
                float minDist2 = float.MaxValue;
                float minDist3 = float.MaxValue;

                for (int i = 0; i < samples.Length; i++)
                {
                    float dist = Vector2.SqrMagnitude(input - samples[i].position);

                    if (dist < minDist0)
                    {
                        minDist3 = minDist2;
                        i3 = i2;
                        minDist2 = minDist1;
                        i2 = i1;
                        minDist1 = minDist0;
                        i1 = i0;
                        minDist0 = dist;
                        i0 = i;
                    }
                    else if (dist < minDist1)
                    {
                        minDist3 = minDist2;
                        i3 = i2;
                        minDist2 = minDist1;
                        i2 = i1;
                        minDist1 = dist;
                        i1 = i;
                    }
                    else if (dist < minDist2)
                    {
                        minDist3 = minDist2;
                        i3 = i2;
                        minDist2 = dist;
                        i2 = i;
                    }
                    else if (dist < minDist3)
                    {
                        minDist3 = dist;
                        i3 = i;
                    }
                }
            }

            private int FindNearestSample(Vector2 input)
            {
                float minDist = float.MaxValue;
                int nearest = 0;

                for (int i = 0; i < samples.Length; i++)
                {
                    float dist = Vector2.SqrMagnitude(input - samples[i].position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = i;
                    }
                }

                return nearest;
            }
        }

}
