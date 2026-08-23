using System.Collections;
using UnityEngine;

namespace ES
{
    public enum ESWeaponShotProfilerScenarioState : byte
    {
        WaitingForResources = 0,
        Preparing = 1,
        WarmingUp = 2,
        Sampling = 3,
        WaitingForDespawn = 4,
        Recycling = 5,
        Completed = 6,
        Failed = 7
    }

    /// <summary>
    /// Weapon/Shot 专用测试场驱动。它只提交正式 Combat/Equipment 请求并读取诊断，
    /// 不接管 ResourcePlan、Pool、Shot 模拟或伤害结算权威。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("【ES】/开发与验证/诊断/Weapon Shot Profiler Scenario")]
    public sealed class ESWeaponShotProfilerScenario : MonoBehaviour
    {
        private const string PlanCheckId = "weapon-plan-ready";
        private const string DefinitionCheckId = "weapon-definitions-ready";
        private const string EquippedCheckId = "weapon-runtime-view-equipped";
        private const string DamageCheckId = "weapon-damage-consumed";
        private const string CapacityCheckId = "weapon-capacity-stable";
        private const string RecycleCheckId = "weapon-view-recycled";

        [Header("权威引用")]
        [SerializeField] private ESResourcePlanBinder resourcePlanBinder;
        [SerializeField] private ESSceneValidationGuide validationGuide;
        [SerializeField] private ItemDataInfo shotDefinition;
        [SerializeField] private ItemDataInfo weaponDefinition;
        [SerializeField] private Entity shooter;
        [SerializeField] private Entity target;
        [SerializeField] private int weaponSlotIndex;

        [Header("采样")]
        [SerializeField, Min(1)] private int warmupFireCount = 32;
        [SerializeField, Min(1)] private int measuredFireCount = 512;
        [SerializeField, Min(0.01f)] private float fireInterval = 0.12f;
        [SerializeField, Min(1f)] private float initializationTimeout = 30f;
        [SerializeField, Min(1f)] private float despawnTimeout = 6f;
        [SerializeField] private bool autoRun = true;

        private EntityBasicCombatModule combat;
        private EntityBasicHealthModule targetHealth;
        private EntityEquipmentDomain equipment;
        private EntityEquipmentSlotModule slots;
        private float nextFireAt;
        private float despawnDeadline;
        private int phaseSuccessCount;
        private float initialTargetHealth;
        private bool initialized;
        private bool recycled;
        private string failureCheckId = PlanCheckId;

        public ESWeaponShotProfilerScenarioState State { get; private set; }
        public int WarmupSuccessCount { get; private set; }
        public int MeasuredSuccessCount { get; private set; }
        public int FireFailureCount { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public bool RuntimeWeaponViewRecycled => recycled;

        public void ConfigureForAuthoring(
            ESResourcePlanBinder binder,
            ESSceneValidationGuide guide,
            ItemDataInfo shotInfo,
            ItemDataInfo weaponInfo,
            Entity shooterEntity,
            Entity targetEntity,
            int slotIndex,
            int warmupCount = 32,
            int sampleCount = 512,
            float interval = 0.12f)
        {
            resourcePlanBinder = binder;
            validationGuide = guide;
            shotDefinition = shotInfo;
            weaponDefinition = weaponInfo;
            shooter = shooterEntity;
            target = targetEntity;
            weaponSlotIndex = Mathf.Max(0, slotIndex);
            warmupFireCount = Mathf.Max(1, warmupCount);
            measuredFireCount = Mathf.Max(1, sampleCount);
            fireInterval = Mathf.Max(0.01f, interval);
        }

        private IEnumerator Start()
        {
            State = ESWeaponShotProfilerScenarioState.WaitingForResources;
            failureCheckId = PlanCheckId;
            Report(PlanCheckId, ESSceneValidationCheckState.Pending, "等待正式 ResourcePlan 和 ActivePlan 资产表。");
            if (!autoRun)
                yield break;

            yield return InitializeScenario();
        }

        private IEnumerator InitializeScenario()
        {
            if (!ValidateAuthoringReferences(out string referenceError))
            {
                Fail(referenceError);
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + initializationTimeout;
            while (!resourcePlanBinder.IsReady)
            {
                ESResourcePlanReport report = resourcePlanBinder.LastReport;
                if (report != null
                    && (report.State == ESResourcePlanState.Failed
                        || report.State == ESResourcePlanState.Canceled))
                {
                    Fail("ResourcePlan 未进入 Ready，状态=" + report.State + "。");
                    yield break;
                }
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Fail("等待 ResourcePlan Ready 超时。请先检查正式 Provider、Manifest/Table 与预热配置。");
                    yield break;
                }
                yield return null;
            }

            Report(PlanCheckId, ESSceneValidationCheckState.Passed, "ResourcePlan 已 Ready，正式 Weapon/Shot Prefab 已进入 ActivePlan。");
            State = ESWeaponShotProfilerScenarioState.Preparing;
            failureCheckId = DefinitionCheckId;
            while (ESRuntimeDataGameCore.Items.IsBuilding
                   || ESRuntimeDataGameCore.Shots.IsBuilding
                   || ESRuntimeDataGameCore.Weapons.IsBuilding)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    Fail("等待 GameCore 内容构建事务结束超时。");
                    yield break;
                }
                yield return null;
            }

            try
            {
                ESItemGameCoreTable.Inject(shotDefinition);
                ESItemGameCoreTable.Inject(weaponDefinition);
            }
            catch (System.Exception exception)
            {
                Fail("Weapon/Shot Definition 注入失败：" + exception.Message);
                yield break;
            }

            // All scene Awakes run before Start, but Start order across objects is unspecified.
            // Defer one frame so Entity domains have completed their normal module Start path.
            yield return null;

            combat = shooter.basicDomain?.FindMyModule<EntityBasicCombatModule>();
            equipment = shooter.equipmentDomain;
            slots = equipment?.Slots;
            targetHealth = target.basicDomain?.FindMyModule<EntityBasicHealthModule>();
            if (combat == null || equipment == null || slots == null || targetHealth == null)
            {
                Fail("测试场缺少 Combat、Equipment Slot 或目标 Health 消费者。");
                yield break;
            }

            failureCheckId = EquippedCheckId;
            if (!equipment.TryPrepareConfiguredWeaponItems(out string prepareError)
                || !slots.TryGetBoundItem(weaponSlotIndex, out _)
                || !slots.TrySetActiveWeaponSlot(weaponSlotIndex))
            {
                Fail("Weapon Prefab -> Item 实例 -> 装备槽准备失败：" + prepareError);
                yield break;
            }

            slots.SetWeaponInHand(true);
            combat.defaultAimTarget = target.transform;
            initialTargetHealth = targetHealth.CurrentHealth;
            ESShotSimulationBatch.Internal_ResetDiagnostics();
            initialized = true;
            recycled = false;
            Report(DefinitionCheckId, ESSceneValidationCheckState.Passed, "Shot 与 Weapon Prepared Data 已由正式提交门禁冻结。");
            Report(EquippedCheckId, ESSceneValidationCheckState.Passed, "Weapon Prefab 已经 Pool 借出并绑定到 Item 实例和装备槽。");
            BeginPhase(ESWeaponShotProfilerScenarioState.WarmingUp);
        }

        private void Update()
        {
            if (!initialized)
                return;

            if (State == ESWeaponShotProfilerScenarioState.WarmingUp
                || State == ESWeaponShotProfilerScenarioState.Sampling)
            {
                TickFirePhase();
                return;
            }

            if (State == ESWeaponShotProfilerScenarioState.WaitingForDespawn)
                TickWaitForDespawn();
        }

        private void TickFirePhase()
        {
            if (Time.time < nextFireAt)
                return;

            nextFireAt = Time.time + fireInterval;
            if (!combat.TryExecutePrimaryAttack())
            {
                FireFailureCount++;
                Fail("正式 Combat 开火失败：" + combat.lastPrimaryAttackFailureReason);
                return;
            }

            phaseSuccessCount++;
            if (State == ESWeaponShotProfilerScenarioState.WarmingUp)
            {
                WarmupSuccessCount++;
                if (phaseSuccessCount < warmupFireCount)
                    return;

                ESShotSimulationBatch.Internal_ResetDiagnostics();
                BeginPhase(ESWeaponShotProfilerScenarioState.Sampling);
                return;
            }

            MeasuredSuccessCount++;
            if (phaseSuccessCount < measuredFireCount)
                return;

            bool damageConsumed = targetHealth.CurrentHealth < initialTargetHealth;
            Report(DamageCheckId,
                damageConsumed ? ESSceneValidationCheckState.Passed : ESSceneValidationCheckState.Failed,
                "目标生命：" + initialTargetHealth + " -> " + targetHealth.CurrentHealth + "。");
            if (!damageConsumed)
            {
                failureCheckId = DamageCheckId;
                Fail("采样期开火没有进入真实 Health/Damage 消费者。");
                return;
            }

            failureCheckId = CapacityCheckId;
            State = ESWeaponShotProfilerScenarioState.WaitingForDespawn;
            despawnDeadline = Time.realtimeSinceStartup + despawnTimeout;
        }

        private void TickWaitForDespawn()
        {
            if (ESShotSimulationBatch.ActiveCount > 0)
            {
                if (Time.realtimeSinceStartup >= despawnDeadline)
                    Fail("采样结束后仍有 Shot 未在期限内停止并归池，Active=" + ESShotSimulationBatch.ActiveCount + "。");
                return;
            }

            State = ESWeaponShotProfilerScenarioState.Recycling;
            bool capacityStable = ESShotSimulationBatch.CapacityRejectCount == 0
                                  && combat.ShotPatternCapacityRejectCount == 0
                                  && combat.weaponFireHitOverflowCount == 0
                                  && ESShotSimulationBatch.HitQueryOverflowCount == 0
                                  && ESShotSimulationBatch.HitOverflowStopCount == 0
                                  && ESShotSimulationBatch.ResolvedColliderCapacityRejectCount == 0
                                  && ESShotSimulationBatch.ImpactQueryOverflowCount == 0;
            Report(CapacityCheckId,
                capacityStable ? ESSceneValidationCheckState.Passed : ESSceneValidationCheckState.Failed,
                "Shot峰值=" + ESShotSimulationBatch.HighWatermark
                + "，Batch拒绝=" + ESShotSimulationBatch.CapacityRejectCount
                + "，图案拒绝=" + combat.ShotPatternCapacityRejectCount
                + "，HitScan缓存溢出=" + combat.weaponFireHitOverflowCount
                + "，Shot查询溢出=" + ESShotSimulationBatch.HitQueryOverflowCount
                + "，饱和停止=" + ESShotSimulationBatch.HitOverflowStopCount
                + "，去重容量拒绝=" + ESShotSimulationBatch.ResolvedColliderCapacityRejectCount
                + "，范围查询溢出=" + ESShotSimulationBatch.ImpactQueryOverflowCount + "。");
            if (!capacityStable)
            {
                Fail("Weapon/Shot 容量门禁在采样期发生拒绝或溢出。");
                return;
            }

            failureCheckId = RecycleCheckId;
            if (!TryRecycleWeaponView(out string recycleError))
            {
                Fail(recycleError);
                return;
            }

            recycled = true;
            State = ESWeaponShotProfilerScenarioState.Completed;
            Report(RecycleCheckId, ESSceneValidationCheckState.Passed, "卸下后 Item 实例已移除，Weapon 运行时视图已归还 Pool。");
        }

        private void BeginPhase(ESWeaponShotProfilerScenarioState state)
        {
            State = state;
            failureCheckId = DamageCheckId;
            phaseSuccessCount = 0;
            nextFireAt = Time.time + fireInterval;
        }

        private bool TryRecycleWeaponView(out string error)
        {
            error = null;
            combat?.Internal_CancelPrimaryAttack();
            if (equipment == null || slots == null)
                return true;
            if (!slots.TryGetBoundItem(weaponSlotIndex, out _))
                return true;
            if (!equipment.TryUnequipItem(
                    weaponSlotIndex,
                    out _,
                    out int inventorySlot,
                    out error))
                return false;
            if (equipment.Inventory == null
                || !equipment.Inventory.TryRemoveItem(inventorySlot, out _))
            {
                error = "Weapon 已卸下，但 Item 实例未能从测试背包移除。";
                return false;
            }
            if (slots.TryGetBoundItem(weaponSlotIndex, out _))
            {
                error = "Weapon 卸下后装备槽仍保留旧 Item 句柄。";
                return false;
            }
            return true;
        }

        private bool ValidateAuthoringReferences(out string error)
        {
            if (resourcePlanBinder == null
                || validationGuide == null
                || shotDefinition == null
                || weaponDefinition == null
                || shooter == null
                || target == null)
            {
                error = "Weapon/Shot Profiler 场景引用不完整，请重新运行官方场景构建器。";
                return false;
            }
            error = null;
            return true;
        }

        private void Fail(string error)
        {
            LastError = string.IsNullOrEmpty(error) ? "未知失败。" : error;
            State = ESWeaponShotProfilerScenarioState.Failed;
            initialized = false;
            Report(failureCheckId,
                ESSceneValidationCheckState.Failed,
                LastError);
        }

        private void Report(string checkId, ESSceneValidationCheckState state, string detail)
        {
            validationGuide?.ReportCheck(checkId, state, detail);
        }

        private void OnDisable()
        {
            if (!recycled)
                TryRecycleWeaponView(out _);
            initialized = false;
        }
    }
}
