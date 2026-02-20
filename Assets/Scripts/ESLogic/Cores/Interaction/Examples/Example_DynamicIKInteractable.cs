using UnityEngine;

namespace ES
{
    /// <summary>
    /// 案例：交互状态“已经激活/正在运行”之后，交互物体仍然可以持续提供（并动态更新）IK 目标。
    ///
    /// 你要解决的问题本质是：
    /// - 状态在 Enter 的那一刻只能读到一次 IK 目标？
    /// - 如果目标在交互过程中会移动（门把手/抽屉/手柄/移动的 UI 点），IK 能不能跟着变？
    ///
    /// 结论：可以。
    ///
    /// 为什么可以：
    /// - 交互模块（EntityBasicInteractionModule）在交互进行期间，会每帧从 ESInteractable 读取：
    ///   - ikGoal（用哪个肢体）
    ///   - ikTarget（手/脚要去哪里）
    ///   - ikHintTarget（肘/膝的提示点，可选）
    /// - 然后每帧把这些值写进“正在 Running 的交互状态”的 runtime IK：
    ///   SetIKGoal / SetIKHintPosition
    /// - 状态机再把所有 Running state 的 runtime IK 聚合到 finalIKPose
    /// - LateUpdate 的 FinalIK Driver 再把 finalIKPose 输出给 FinalIK（BipedIK.UpdateBipedIK）
    ///
    /// 所以：只要你在运行时持续更新 ikTarget/ikHintTarget 的 Transform（位置/旋转），即使状态已经激活，IK 也会实时跟随。
    ///
    /// 用法（最短）：
    /// 1) 把本脚本挂到“可交互物体”上（它继承 ESInteractable）
    /// 2) 配好 interactionStateInfo（交互时要注入/进入哪个状态）
    /// 3) 配 ikGoal（默认 RightHand）+ ikWeight + useIKRotation
    /// 4) 可选：指定 driveTarget/driveHint（为空则用本物体 transform）；它们代表“真实在动的目标点”
    /// 5) 交互开始后，你移动 driveTarget（比如门把手在动画里动），角色手 IK 会跟着动
    /// </summary>
    public sealed class Example_DynamicIKInteractable : ESInteractable
    {
        [Header("权重（可选：用曲线动态改变）")]
        public bool useWeightCurves = false;

        public AnimationCurve limbWeightCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        public AnimationCurve targetWeightCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("动态目标来源（你可以在运行时移动它）")]
        public Transform driveTarget;

        public Transform driveHint;

        [Header("偏移（相对 driveTarget/driveHint 的本地偏移）")]
        public Vector3 targetLocalOffset;

        public Vector3 hintLocalOffset;

        [Header("演示：让目标自动移动（可选）")]
        public bool demoMoveTarget = false;

        public float demoRadius = 0.15f;

        public float demoSpeed = 1.5f;

        private Transform _defaultDrive;
        private Vector3 _demoBasePos;

        private void Awake()
        {
            _defaultDrive = transform;

            // 关键：ikTarget / ikHintTarget 是“交互模块每帧读取的输出口”。
            // 如果你忘了在 Inspector 里指定，这里自动创建子节点，避免空引用导致 IK 不工作。
            if (ikTarget == null)
            {
                ikTarget = CreateChild("IKTarget");
            }

            if (ikHintTarget == null)
            {
                ikHintTarget = CreateChild("IKHint");
            }

            _demoBasePos = (driveTarget != null ? driveTarget.position : transform.position);
        }

        public override float EvaluateIKLimbWeight(Entity entity, float normalized01)
        {
            if (!useWeightCurves) return base.EvaluateIKLimbWeight(entity, normalized01);
            return Mathf.Clamp01(limbWeightCurve != null ? limbWeightCurve.Evaluate(normalized01) : ikWeight);
        }

        public override float EvaluateIKTargetWeight(Entity entity, float normalized01)
        {
            if (!useWeightCurves) return base.EvaluateIKTargetWeight(entity, normalized01);
            return Mathf.Clamp01(targetWeightCurve != null ? targetWeightCurve.Evaluate(normalized01) : ikTargetWeight);
        }

        public override void OnInteractUpdate(Entity entity, float deltaTime)
        {
            // 交互进行中：即使状态已经激活，你仍然可以每帧更新 ikTarget/ikHintTarget。
            // 交互模块会继续读取这些 Transform 并写入到 Running 的状态 IK。
            UpdateIKTargetTransforms();
        }

        private void Update()
        {
            // 说明：这个 Update() 纯粹是“演示用”，让你不触发交互也能看到目标点在动。
            // 正式项目里，如果你担心开销，可以只在 OnInteractUpdate()（交互进行中）更新 ikTarget/ikHintTarget。
            // 因为交互结束后一般不需要继续提供 IK。

            // 即使不在交互中，也可以提前更新目标点（交互一开始就能立即生效）。
            if (demoMoveTarget)
            {
                var t = driveTarget != null ? driveTarget : _defaultDrive;
                var angle = Time.time * demoSpeed;
                var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * demoRadius;
                t.position = _demoBasePos + offset;
            }

            UpdateIKTargetTransforms();
        }

        private void UpdateIKTargetTransforms()
        {
            // 这里把“真实会动的 driveTarget/driveHint”映射到 ikTarget/ikHintTarget。
            // 你可以把 driveTarget 看成“门把手/操纵杆/抓取点”的 Transform。
            var t = driveTarget != null ? driveTarget : _defaultDrive;
            ikTarget.position = t.TransformPoint(targetLocalOffset);
            ikTarget.rotation = t.rotation;

            var h = driveHint != null ? driveHint : t;
            ikHintTarget.position = h.TransformPoint(hintLocalOffset);
        }

        private Transform CreateChild(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }
    }
}
