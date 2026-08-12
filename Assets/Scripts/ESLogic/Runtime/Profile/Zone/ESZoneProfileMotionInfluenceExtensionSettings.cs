using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public enum ESZoneMotionInfluenceMode
    {
        VelocityDelta = 0,
        Acceleration = 1,
        Attraction = 2
    }

    [Serializable]
    [TypeRegistryItem("Zone/运动影响")]
    public sealed class ESZoneProfileMotionInfluenceExtensionSettings : ESZoneProfileExtensionSettings
    {
        public const string StableTypeId = "es.zone.motion-influence";
        public const int CurrentSchemaVersion = 1;
        public const int DefaultOrderValue = 100;
        public const string DefaultNameTitle = "运动影响";

        [SerializeField, ReadOnly, LabelText("Schema 版本")]
        private int schemaVersion = CurrentSchemaVersion;
        [SerializeField, LabelText("启用")]
        private bool enabled = true;
        [SerializeField, MinValue(0), LabelText("预热成员容量")]
        private int prewarmMemberCapacity = 4;

        [SerializeField, LabelText("影响模式")]
        private ESZoneMotionInfluenceMode mode = ESZoneMotionInfluenceMode.Acceleration;
        [SerializeField, LabelText("使用 Zone 局部方向")]
        private bool useLocalDirection;
        [SerializeField, ShowIf(nameof(IsVelocityDelta)), LabelText("速度增量 (m/s)")]
        private Vector3 velocityDelta = Vector3.up * 5f;
        [SerializeField, ShowIf(nameof(IsAcceleration)), LabelText("加速度 (m/s²)")]
        private Vector3 acceleration = Vector3.up;
        [SerializeField, ShowIf(nameof(IsAttraction)), LabelText("牵引参数")]
        private ESMotionAttractionSettings attraction = new ESMotionAttractionSettings
        {
            model = ESMotionAttractionModel.TargetVelocity,
            stopRadius = 0.5f,
            maxSpeed = 10f,
            maxAcceleration = 20f,
            response = 2f,
            stiffness = 10f,
            damping = 4f
        };
        [SerializeField, LabelText("运动锁定许可")]
        private ESMotionInfluencePermissions permissions;

        [SerializeField, LabelText("影响 Entity")]
        private bool affectEntity = true;
        [SerializeField, LabelText("影响 Item")]
        private bool affectItem = true;
        [SerializeField, LabelText("影响 Vehicle")]
        private bool affectVehicle = true;

        public override string TypeId => StableTypeId;
        public override int SchemaVersion => schemaVersion;
        public override int SupportedSchemaVersion => CurrentSchemaVersion;
        public override int DefaultOrder => DefaultOrderValue;
        public override string NameTitleDefault => DefaultNameTitle;
        public override bool Enabled => enabled;
        public int PrewarmMemberCapacity => Mathf.Max(0, prewarmMemberCapacity);
        public ESZoneMotionInfluenceMode Mode => mode;
        public ESMotionInfluencePermissions Permissions => permissions;

        private bool IsVelocityDelta => mode == ESZoneMotionInfluenceMode.VelocityDelta;
        private bool IsAcceleration => mode == ESZoneMotionInfluenceMode.Acceleration;
        private bool IsAttraction => mode == ESZoneMotionInfluenceMode.Attraction;

        public override ESZoneProfileExtensionRuntime CreateRuntime()
        {
            return new ESZoneProfileMotionInfluenceExtensionRuntime(this);
        }

        internal Vector3 BuildVelocityDelta(Transform zoneTransform)
        {
            Vector3 value = useLocalDirection && zoneTransform != null
                ? zoneTransform.TransformDirection(velocityDelta)
                : velocityDelta;
            return value;
        }

        internal ESMotionFieldRequest BuildFieldRequest(Transform zoneTransform)
        {
            Vector3 resolvedAcceleration = useLocalDirection && zoneTransform != null
                ? zoneTransform.TransformDirection(acceleration)
                : acceleration;
            return new ESMotionFieldRequest
            {
                kind = mode == ESZoneMotionInfluenceMode.Attraction
                    ? ESMotionFieldKind.Attraction
                    : ESMotionFieldKind.Acceleration,
                acceleration = resolvedAcceleration,
                anchorTransform = mode == ESZoneMotionInfluenceMode.Attraction ? zoneTransform : null,
                anchorPosition = zoneTransform != null ? zoneTransform.position : Vector3.zero,
                attraction = attraction,
                permissions = permissions
            };
        }

        internal bool Allows(IESMotionInfluenceReceiver receiver)
        {
            return (affectEntity && receiver is Entity)
                || (affectItem && receiver is Item)
                || (affectVehicle && receiver is VehicleController);
        }

        protected internal override bool Validate(
            ESZoneProfile profile,
            ESZoneProfileSettings settings,
            List<string> issues)
        {
            if (!Enabled)
                return true;
            if (!affectEntity && !affectItem && !affectVehicle)
            {
                issues?.Add("Motion Influence Extension 至少需要启用一种接收目标。");
                return false;
            }
            if (mode == ESZoneMotionInfluenceMode.Attraction && attraction.maxAcceleration <= 0f)
            {
                issues?.Add("Attraction 的 Max Acceleration 必须大于 0。");
                return false;
            }
            if (mode == ESZoneMotionInfluenceMode.Attraction
                && attraction.model == ESMotionAttractionModel.TargetVelocity
                && (attraction.maxSpeed <= 0f || attraction.response <= 0f))
            {
                issues?.Add("Target Velocity Attraction 的 Max Speed 与 Response 必须大于 0。");
                return false;
            }
            if (mode == ESZoneMotionInfluenceMode.Attraction
                && attraction.model == ESMotionAttractionModel.SpringDamper
                && attraction.stiffness <= 0f)
            {
                issues?.Add("Spring Damper Attraction 的 Stiffness 必须大于 0。");
                return false;
            }
            return true;
        }
    }

    public sealed class ESZoneProfileMotionInfluenceExtensionRuntime : ESZoneProfileExtensionRuntime
    {
        private readonly ESZoneProfileMotionInfluenceExtensionSettings settings;
        private readonly Dictionary<UnityEngine.Object, ESMotionFieldLease> leases;

        public ESZoneProfileMotionInfluenceExtensionRuntime(
            ESZoneProfileMotionInfluenceExtensionSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            int capacity = settings.PrewarmMemberCapacity;
            leases = new Dictionary<UnityEngine.Object, ESMotionFieldLease>(capacity);
        }

        public int ActiveLeaseCount => leases.Count;

        public override void OnProfileAwake(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context)
        {
            context.EnsureMemberCapacity(settings.PrewarmMemberCapacity);
        }

        public override ESZoneMemberEnterResult TryEnterMember(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context,
            ESZoneMember member,
            out string error)
        {
            if (!ESMotionInfluenceReceiverResolver.TryResolve(member.RootObject, out var receiver)
                || !settings.Allows(receiver))
            {
                error = null;
                return ESZoneMemberEnterResult.Ignored;
            }

            if (settings.Mode == ESZoneMotionInfluenceMode.VelocityDelta)
            {
                if (receiver.AddVelocity(
                        settings.BuildVelocityDelta(profile.transform),
                        settings.Permissions))
                {
                    error = null;
                    return ESZoneMemberEnterResult.Entered;
                }

                error = null;
                return ESZoneMemberEnterResult.Ignored;
            }

            if (!receiver.TryAcquireField(
                    settings.BuildFieldRequest(profile.transform),
                    out ESMotionFieldLease lease))
            {
                error = "目标拒绝运动 Field Lease。";
                return ESZoneMemberEnterResult.Failed;
            }

            leases[member.Key] = lease;
            error = null;
            return ESZoneMemberEnterResult.Entered;
        }

        public override void ExitMember(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context,
            ESZoneMember member)
        {
            if (ReferenceEquals(member.Key, null)
                || !leases.TryGetValue(member.Key, out ESMotionFieldLease lease))
                return;
            lease.Dispose();
            leases.Remove(member.Key);
        }

        public override void OnProfileDisable(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context)
        {
            DisposeAllLeases();
        }

        public override void OnProfilePoolDespawned(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context)
        {
            DisposeAllLeases();
        }

        public override void OnProfileDestroy(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context)
        {
            DisposeAllLeases();
        }

        private void DisposeAllLeases()
        {
            foreach (ESMotionFieldLease lease in leases.Values)
                lease.Dispose();
            leases.Clear();
        }
    }
}
