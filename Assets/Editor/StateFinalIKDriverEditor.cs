using UnityEditor;
using UnityEngine;

using System;

namespace ES
{
    [UnityEditor.CustomEditor(typeof(StateFinalIKDriver))]
    internal sealed class StateFinalIKDriverEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                var driver = (StateFinalIKDriver)target;

                var ready = driver.IsReady;
                var bound = driver.IsBound;

                string status = ready ? "Ready" : (bound ? "Bound(但未就绪)" : "未绑定");
                EditorGUILayout.LabelField("运行状态", status);

                if (!string.IsNullOrEmpty(driver.LastBindError))
                {
                    EditorGUILayout.HelpBox(driver.LastBindError, MessageType.Warning);
                }

                EditorGUILayout.LabelField("Pose有权重", driver.HasPoseWeight ? "是" : "否");

                var pose = driver.CurrentPose;
                EditorGUILayout.LabelField("LeftFoot.weight", pose.leftFoot.weight.ToString("F3"));
                EditorGUILayout.LabelField("RightFoot.weight", pose.rightFoot.weight.ToString("F3"));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Bind尝试/成功", $"{driver.BindTryCount} / {driver.BindSuccessCount}");
                EditorGUILayout.LabelField("Apply次数", driver.ApplyCount.ToString());
                EditorGUILayout.LabelField("Solver更新次数", driver.SolverUpdateCount.ToString());
                EditorGUILayout.LabelField("最后Apply时间", driver.LastApplyTime.ToString("F3"));
                EditorGUILayout.LabelField("最后Solver时间", driver.LastSolverUpdateTime.ToString("F3"));

                EditorGUILayout.Space(6);
                DrawBipedIKSnapshot(driver);

                EditorGUILayout.Space(6);
                DrawGoalTargetTransforms(driver);

                EditorGUILayout.Space(6);
                DrawFixupButtons(driver);

                EditorGUILayout.Space(6);
                DrawFootPlacementDebug(driver);
            }
        }

        private static void DrawFootPlacementDebug(StateFinalIKDriver driver)
        {
            var animator = driver != null ? driver.GetComponent<Animator>() : null;
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("台阶贴合(脚贴合)调试", EditorStyles.boldLabel);

                if (animator == null)
                {
                    EditorGUILayout.HelpBox("未找到Animator组件", MessageType.Info);
                    return;
                }

                var entity = animator.GetComponentInParent<Entity>();
                if (entity == null)
                {
                    EditorGUILayout.HelpBox("未找到 Entity(Core) 组件：无法从基础域读取脚贴合模块的统计/曲线。", MessageType.Info);
                    return;
                }

                var domain = entity.basicDomain;
                if (domain == null || domain.MyModules == null || domain.MyModules.ValuesNow == null)
                {
                    EditorGUILayout.HelpBox("Entity.basicDomain 未初始化或无模块列表。", MessageType.Info);
                    return;
                }

                EntityBasicFootPlacementModule module = null;
                var list = domain.MyModules.ValuesNow;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is EntityBasicFootPlacementModule m)
                    {
                        module = m;
                        break;
                    }
                }

                if (module == null)
                {
                    EditorGUILayout.HelpBox("未找到 EntityBasicFootPlacementModule（基础台阶脚贴合模块）。", MessageType.Info);
                    return;
                }

                EditorGUILayout.LabelField("debugRuntimeState", module.debugRuntimeState ? "是" : "否");
                EditorGUILayout.LabelField("supportShareParamName", string.IsNullOrEmpty(module.supportShareParamName) ? "<空>" : module.supportShareParamName);
                EditorGUILayout.LabelField("supportShareBlend", module.supportShareBlend.ToString("F2"));
                EditorGUILayout.LabelField("supportShareMaxDeltaPerSec", module.supportShareMaxDeltaPerSec.ToString("F2"));
                EditorGUILayout.LabelField("AnimShare输入", module.DebugLastAnimShareInput.ToString("F3"));
                EditorGUILayout.LabelField("使用AnimShare", module.DebugLastUsedAnimShare ? "是" : "否");

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("过滤统计(累计)", EditorStyles.boldLabel);
                    if (GUILayout.Button("Reset", GUILayout.Width(60)))
                    {
                        module.DebugResetInstrumentation();
                        EditorUtility.SetDirty(animator.gameObject);
                    }
                }

                EditorGUILayout.LabelField("NoHit", module.DebugRejectNoHit.ToString());
                EditorGUILayout.LabelField("StepUpExceeded", module.DebugRejectStepUp.ToString());
                EditorGUILayout.LabelField("StepDownExceeded", module.DebugRejectStepDown.ToString());
                EditorGUILayout.LabelField("NotGrounded", module.DebugRejectNotGrounded.ToString());
                EditorGUILayout.LabelField("NotMoving", module.DebugRejectNotMoving.ToString());
                EditorGUILayout.LabelField("SuppressedLeft", module.DebugRejectSuppressedLeft.ToString());
                EditorGUILayout.LabelField("SuppressedRight", module.DebugRejectSuppressedRight.ToString());
                EditorGUILayout.LabelField("ExistingIK", module.DebugRejectExistingIK.ToString());
                if (!string.IsNullOrEmpty(module.DebugLastRejectReason))
                {
                    EditorGUILayout.HelpBox($"最后原因: {module.DebugLastRejectReason}", MessageType.None);
                }

                EditorGUILayout.Space(6);
                DrawWeightHistoryGraph(module);
            }
        }

        private static void DrawWeightHistoryGraph(EntityBasicFootPlacementModule module)
        {
            EditorGUILayout.LabelField("权重曲线(total/share/left/right)", EditorStyles.boldLabel);

            if (!module.debugRuntimeState)
            {
                EditorGUILayout.HelpBox("开启脚贴合模块的 debugRuntimeState 后才会记录曲线历史。", MessageType.Info);
                return;
            }

            var total = module.DebugHistTotal;
            var share = module.DebugHistShare;
            var left = module.DebugHistLeft;
            var right = module.DebugHistRight;
            var animShare = module.DebugHistAnimShare;
            var usedAnim = module.DebugHistUsedAnim;
            int count = module.DebugHistoryCount;
            int cap = module.DebugHistoryCapacityValue;
            int head = module.DebugHistoryHead;

            if (total == null || share == null || left == null || right == null || count <= 1 || cap <= 1)
            {
                EditorGUILayout.HelpBox("暂无足够曲线数据（至少需要2帧）。", MessageType.Info);
                return;
            }

            var rect = GUILayoutUtility.GetRect(10, 72, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none);

            Handles.BeginGUI();
            try
            {
                DrawSeries(rect, total, head, count, cap, new Color(1f, 1f, 1f, 0.9f));
                DrawSeries(rect, share, head, count, cap, new Color(0.3f, 0.9f, 1f, 0.9f));
                DrawSeries(rect, left, head, count, cap, new Color(0.3f, 1f, 0.4f, 0.9f));
                DrawSeries(rect, right, head, count, cap, new Color(1f, 0.4f, 0.9f, 0.9f));

                // AnimShare：仅在“确实使用AnimShare”的帧上绘制（usedAnim=1）。
                if (animShare != null && usedAnim != null)
                {
                    DrawSeriesMasked(rect, animShare, usedAnim, head, count, cap, new Color(1f, 0.75f, 0.2f, 0.9f));
                }
            }
            finally
            {
                Handles.EndGUI();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLegend("total", new Color(1f, 1f, 1f, 0.9f));
                DrawLegend("share(L)", new Color(0.3f, 0.9f, 1f, 0.9f));
                DrawLegend("left", new Color(0.3f, 1f, 0.4f, 0.9f));
                DrawLegend("right", new Color(1f, 0.4f, 0.9f, 0.9f));
                DrawLegend("animShare", new Color(1f, 0.75f, 0.2f, 0.9f));
            }
        }

        private static void DrawLegend(string label, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUILayout.Label("■", GUILayout.Width(12));
            GUI.color = old;
            GUILayout.Label(label, GUILayout.Width(60));
        }

        private static void DrawSeries(Rect rect, float[] buf, int head, int count, int cap, Color color)
        {
            Handles.color = color;

            int start = head - count;
            while (start < 0) start += cap;

            Vector3 prev = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                int idx = (start + i) % cap;
                float v = Mathf.Clamp01(buf[idx]);

                float x = rect.xMin + (i / (float)(count - 1)) * rect.width;
                float y = rect.yMax - v * rect.height;
                var p = new Vector3(x, y, 0f);

                if (i > 0)
                {
                    Handles.DrawLine(prev, p);
                }
                prev = p;
            }
        }

        private static void DrawSeriesMasked(Rect rect, float[] buf, float[] maskBuf, int head, int count, int cap, Color color)
        {
            Handles.color = color;

            int start = head - count;
            while (start < 0) start += cap;

            bool hasPrev = false;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                int idx = (start + i) % cap;

                bool enabled = maskBuf[idx] > 0.5f;
                float raw = buf[idx];

                // AnimShare输入无效时通常为 -1：直接断开线段
                if (!enabled || raw < 0f)
                {
                    hasPrev = false;
                    continue;
                }

                float v = Mathf.Clamp01(raw);
                float x = rect.xMin + (i / (float)(count - 1)) * rect.width;
                float y = rect.yMax - v * rect.height;
                var p = new Vector3(x, y, 0f);

                if (hasPrev)
                {
                    Handles.DrawLine(prev, p);
                }

                prev = p;
                hasPrev = true;
            }
        }

        private static void DrawGoalTargetTransforms(StateFinalIKDriver driver)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("目标Transform", EditorStyles.boldLabel);

                EditorGUILayout.LabelField("启用", "是(强制)");
                EditorGUILayout.LabelField("从Pose驱动", driver.DriveGoalTargetsFromPose ? "是" : "否");

                DrawTargetRow("LeftFoot", driver.LeftFootTarget);
                DrawTargetRow("RightFoot", driver.RightFootTarget);
                DrawTargetRow("LeftHand", driver.LeftHandTarget);
                DrawTargetRow("RightHand", driver.RightHandTarget);
            }
        }

        private static void DrawTargetRow(string label, Transform t)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(70));
                EditorGUILayout.ObjectField(t, typeof(Transform), allowSceneObjects: true);
                using (new EditorGUI.DisabledScope(t == null))
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                    {
                        Selection.activeObject = t.gameObject;
                        EditorGUIUtility.PingObject(t.gameObject);
                    }
                }
            }
        }

        private static void DrawBipedIKSnapshot(StateFinalIKDriver driver)
        {
            var animator = driver != null ? driver.GetComponent<Animator>() : null;
            if (animator == null) return;

            var bipedIK = animator.GetComponent<RootMotion.FinalIK.BipedIK>();
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("BipedIK快照", EditorStyles.boldLabel);
                if (bipedIK == null)
                {
                    EditorGUILayout.LabelField("存在BipedIK", "否");
                    return;
                }

                EditorGUILayout.LabelField("存在BipedIK", "是");
                bool refsFilled = bipedIK.references != null && bipedIK.references.isFilled;
                EditorGUILayout.LabelField("References.isFilled", refsFilled ? "是" : "否");

                DrawGoal(bipedIK, AvatarIKGoal.LeftFoot, "LeftFoot");
                DrawGoal(bipedIK, AvatarIKGoal.RightFoot, "RightFoot");
                DrawGoal(bipedIK, AvatarIKGoal.LeftHand, "LeftHand");
                DrawGoal(bipedIK, AvatarIKGoal.RightHand, "RightHand");
            }
        }

        private static void DrawGoal(RootMotion.FinalIK.BipedIK bipedIK, AvatarIKGoal goal, string label)
        {
            float w = bipedIK.GetIKPositionWeight(goal);
            var p = bipedIK.GetIKPosition(goal);
            var r = bipedIK.GetIKRotation(goal);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("weight", w.ToString("F3"));
                EditorGUILayout.Vector3Field("position", p);
                var euler = r.eulerAngles;
                EditorGUILayout.Vector3Field("rotation(euler)", euler);
            }
        }

        private static void DrawFixupButtons(StateFinalIKDriver driver)
        {
            // 这些按钮只做“可选辅助”，不强制修改场景。
            var animator = driver != null ? driver.GetComponent<Animator>() : null;
            if (animator == null)
            {
                EditorGUILayout.HelpBox("未找到Animator组件（StateFinalIKDriver应挂在Animator同物体上）", MessageType.Info);
                return;
            }

            var bipedIK = animator.GetComponent<RootMotion.FinalIK.BipedIK>();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("添加BipedIK"))
                {
                    if (bipedIK == null)
                    {
                        Undo.AddComponent<RootMotion.FinalIK.BipedIK>(animator.gameObject);
                        EditorUtility.SetDirty(animator.gameObject);
                    }
                }

                if (GUILayout.Button("自动识别References"))
                {
                    bipedIK = animator.GetComponent<RootMotion.FinalIK.BipedIK>();
                    if (bipedIK != null)
                    {
                        Undo.RecordObject(bipedIK, "AutoDetect Biped References");
                        RootMotion.BipedReferences.AutoDetectReferences(
                            ref bipedIK.references,
                            bipedIK.transform,
                            new RootMotion.BipedReferences.AutoDetectParams(legsParentInSpine: false, includeEyes: true));
                        EditorUtility.SetDirty(bipedIK);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("InitiateBipedIK"))
                {
                    bipedIK = animator.GetComponent<RootMotion.FinalIK.BipedIK>();
                    if (bipedIK != null)
                    {
                        bipedIK.InitiateBipedIK();
                        EditorUtility.SetDirty(bipedIK);
                    }
                }

                if (GUILayout.Button("选中Animator"))
                {
                    Selection.activeObject = animator.gameObject;
                    EditorGUIUtility.PingObject(animator.gameObject);
                }
            }
        }
    }
}
