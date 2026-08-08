using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
	/// <summary>
	/// Identifies the owner category of a temporary hard block on Entity control execution.
	/// It does not select the active Player/AI/Network writer and is not a persisted gameplay key.
	/// </summary>
	public enum EntityControlSource : byte
	{
		None = 0,
		Player = 1,
		AI = 2,
		Network = 3,
		Cutscene = 4,
		Replay = 5,
		Vehicle = 6,
	}

	[Serializable, TypeRegistryItem("AI域")]
	public partial class EntityAIDomain : Domain<Entity, EntityAIModuleBase>
	{
		[NonSerialized]
		public EntityInputState inputState;

		[Title("输入执行")]
		[LabelText("输入调度 Tag 条件")]
		[Tooltip("为空时不限制。条件不匹配时当前帧输入不会驱动移动、战斗、技能或交互。")]
		public ESTagConditionConfig dispatchTagCondition = new ESTagConditionConfig();

		[Title("转身模式")]
		// 玩家第三人称默认采用“镜头自由环绕、角色仅随移动方向转身”。
		// FreeLook 仍保留给需要角色朝向独立于移动意图的 AI/瞄准场景显式选择。
		public TurnMode turnMode = TurnMode.MoveDirection;

		[LabelText("转身速率")]
		public float turnSpeed = 12f;

		[LabelText("无输入时立即停下")]
		public bool stopMoveWhenNoInput = true;

		[LabelText("移动死区")]
		public float moveDeadZone = 0.05f;

		[Title("相机控制")]
		[LabelText("启用相机上下视角")]
		public bool enableCameraLook = true;

		[LabelText("AIM（可选）")]
		public Transform aimTransform;

		[Title("瞄准驱动")]
		[LabelText("驱动 AimIK")]
		public bool driveAimIK = true;

		[LabelText("AimIK 权重"), Range(0f, 1f)]
		public float aimIKWeight;

		[LabelText("瞄准目标距离")]
		public float aimTargetDistance = 30f;

		[LabelText("无相机时瞄准高度")]
		public float fallbackAimHeight = 1.5f;

		[LabelText("相机Yaw速率")]
		public float cameraYawSpeed = 220f;

		[LabelText("相机Pitch速率")]
		public float cameraPitchSpeed = 90f;

		[LabelText("水平旋转倍率")]
		public float yawMultiplier = 1f;

		[LabelText("竖直旋转倍率")]
		public float pitchMultiplier = 1f;

		[LabelText("相机Pitch限制")]
		public Vector2 cameraPitchLimit = new Vector2(-80f, 80f);

		[LabelText("Pitch软限制范围")]
		public float cameraPitchSoftZone = 12f;

		[LabelText("Pitch越界矫正速率")]
		public float cameraPitchCorrectionSpeed = 12f;

		[LabelText("相机旋转平滑")]
		public float cameraLookSmooth = 12f;

		[LabelText("相机调试")]
		public bool debugCamera;

		/// <summary>
		/// AIDomain owns the execution gate. LocalControl or a future source arbiter decides who may
		/// write intent; this Permit only decides whether resolved intent may affect the Entity.
		/// </summary>
		[NonSerialized]
		private ESPermitSet controlPermit;

		[Title("控制阻断")]
		[ShowInInspector, ReadOnly, LabelText("控制阻断数")]
		public int ControlBlockCount => controlPermit != null ? controlPermit.Count : 0;

		[ShowInInspector, ReadOnly, LabelText("控制是否被阻断")]
		public bool IsControlBlocked => controlPermit != null && !controlPermit.Value;

		public override void _AwakeRegisterAllModules()
		{
			inputState ??= new EntityInputState();

			base._AwakeRegisterAllModules();
		}

		protected override void Update()
		{
			// Player/AI/other writers update inputState as hosted modules first; the
			// domain-owned executor then turns the resolved state into Entity effects.
			base.Update();
			UpdateInputDispatch();
		}

		protected override void OnDisable()
		{
			ResetControlArbitrationForLifecycle();
			inputState?.ClearAll();
			ResetInputDispatchForDisable();
			base.OnDisable();
		}

		protected override void OnDestroy()
		{
			ResetControlArbitrationForLifecycle();
			DestroyInputDispatchRuntime();
			base.OnDestroy();
		}

		/// <summary>
		/// Acquires a generation-safe hard block for this Entity's resolved control execution.
		/// This does not claim LocalControl or choose an input writer. The returned token is the only
		/// valid release/update identity; owner/source zero is rejected to avoid permanent orphan blocks.
		/// </summary>
		public ESValueChangeToken AcquireControlBlock(
			EntityControlSource source,
			int ownerId,
			int priority = 100)
		{
			if (source == EntityControlSource.None || ownerId == 0)
				return ESValueChangeToken.Invalid;

			controlPermit ??= new ESPermitSet(fallbackValue: true, capacity: 4);
			return controlPermit.Add(
				ESPermitLaw.HardDisable,
				ownerId: ownerId,
				sourceId: (int)source,
				priority: priority,
				enabled: true);
		}

		/// <summary>Updates only the caller's own control block.</summary>
		public bool UpdateControlBlock(ESValueChangeToken token, int priority = 100)
		{
			return controlPermit != null
				&& token.IsValid
				&& controlPermit.Update(token, ESPermitLaw.HardDisable, priority);
		}

		/// <summary>Releases only the caller's own control block; stale generations are rejected.</summary>
		public bool ReleaseControlBlock(ESValueChangeToken token)
		{
			return controlPermit != null
				&& token.IsValid
				&& controlPermit.Release(token);
		}

		/// <summary>
		/// Clears all control leases at disable, destroy, and pool-return boundaries. ResetForReuse
		/// advances the PermitSet token version, so a copied or delayed old token cannot affect the
		/// next renter of this Entity.
		/// </summary>
		public void ResetControlArbitrationForLifecycle()
		{
			controlPermit?.ResetForReuse();
		}

		/// <summary>
		/// Central player-writer gate. LocalControl proves which Entity is locally possessed;
		/// AIDomain additionally proves that no temporary execution block suppresses its resolved intent.
		/// </summary>
		public bool CanPlayerWriteInput()
		{
			return !IsControlBlocked
				&& MyCore != null
				&& ESGameManager.LocalControl != null
				&& ESGameManager.LocalControl.IsLocallyControlled(MyCore);
		}

#if UNITY_EDITOR
		[Button("检查玩家输入链路"), PropertyOrder(-9)]
		public void ValidatePlayerInputModules()
		{
			int writerCount = 0;
			int count = MyModules != null && MyModules.ValuesNow != null ? MyModules.ValuesNow.Count : 0;
			for (int i = 0; i < count; i++)
			{
				if (MyModules.ValuesNow[i] is EntityPlayerInputWriteModule) writerCount++;
			}

			if (writerCount != 1)
			{
				Debug.LogError(
					$"[Entity玩家输入检查] 未通过 | Writer={writerCount}, " +
					$"DomainExecutor=有效, ControlGate={(controlPermit != null ? "有效" : "默认放行")}");
				return;
			}

			Debug.Log("[Entity玩家输入检查] 通过：玩家输入写入与 AI 域实体执行器已统一，控制阻断由 AI 域收口。", MyCore);
		}
#endif
	}
}
