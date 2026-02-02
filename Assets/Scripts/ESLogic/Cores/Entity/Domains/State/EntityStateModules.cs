using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
	[Serializable, TypeRegistryItem("状态域模块基类")]
	public abstract class EntityStateModuleBase : Module<Entity, EntityStateDomain>
	{
		public sealed override Type TableKeyType => GetType();
	}

	// 状态机已由状态域直接持有并驱动

	[Serializable, TypeRegistryItem("状态机Playable测试模块")]
	public class EntityStatePlayableTestModule : EntityStateModuleBase
	{
		[Title("开关")]
		[LabelText("启用测试模式")]
		public bool enableTest;

		[Title("测试动画")]
		[LabelText("动画Clip")]
		public AnimationClip testClip;

		[LabelText("循环播放")]
		public bool loop = true;

		[LabelText("速度倍率")]
		public float speed = 1f;

		[LabelText("起始归一化时间")]
		[Range(0f, 1f)]
		public float startNormalizedTime;

		[Title("偏移")]
		[LabelText("应用偏移")]
		public bool applyOffset;

		[LabelText("位置偏移")]
		public Vector3 positionOffset;

		[LabelText("旋转偏移(欧拉)")]
		public Vector3 rotationOffsetEuler;

		[Title("调试")]
		[LabelText("自动日志")]
		public bool logStatus;

		[Button("立即开始测试")]
		private void StartTestButton() => StartTest();

		[Button("立即停止测试")]
		private void StopTestButton() => StopTest();

		[Button("打印状态机Playable状态")]
		private void LogStateMachineStatus()
		{
			if (MyDomain?.stateMachine == null)
			{
				Debug.LogWarning("[StateTest] StateMachine 未初始化。");
				return;
			}
			Debug.Log($"[StateTest] GraphValid={MyDomain.stateMachine.IsPlayableGraphValid}, Playing={MyDomain.stateMachine.IsPlayableGraphPlaying}, Animator={(MyDomain.stateMachine.BoundAnimator != null ? MyDomain.stateMachine.BoundAnimator.name : "null")}");
		}

		[NonSerialized] private PlayableGraph _graph;
		[NonSerialized] private AnimationClipPlayable _clipPlayable;
		[NonSerialized] private AnimationPlayableOutput _output;
		[NonSerialized] private bool _isTesting;
		[NonSerialized] private float _testStartTime;
		[NonSerialized] private Vector3 _cachedLocalPosition;
		[NonSerialized] private Quaternion _cachedLocalRotation;
		[NonSerialized] private bool _offsetApplied;

		protected override void OnEnable()
		{
			base.OnEnable();
			if (enableTest)
			{
				StartTest();
			}
		}

		protected override void OnDisable()
		{
			StopTest();
			base.OnDisable();
		}

		protected override void Update()
		{
			base.Update();

			if (enableTest && !_isTesting)
			{
				StartTest();
			}
			else if (!enableTest && _isTesting)
			{
				StopTest();
			}

			if (!_isTesting) return;

			ApplyRuntimeSettings();
		}

		private void StartTest()
		{
			if (_isTesting) return;
			if (testClip == null || MyCore == null || MyCore.animator == null)
			{
				if (logStatus)
				{
					Debug.LogWarning("[StateTest] 未设置测试Clip或Animator。");
				}
				return;
			}

			StopStateMachineIfNeeded();

			_graph = PlayableGraph.Create("StateTest_SingleClip");
			_graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
			_clipPlayable = AnimationClipPlayable.Create(_graph, testClip);
			_clipPlayable.SetSpeed(0f);
			_clipPlayable.SetDuration(testClip.length);
			_clipPlayable.SetTime(startNormalizedTime * testClip.length);
			_clipPlayable.SetApplyFootIK(true);

			if (applyOffset)
			{
				_offsetApplied = true;
				_cachedLocalPosition = MyCore.transform.localPosition;
				_cachedLocalRotation = MyCore.transform.localRotation;
				MyCore.transform.localPosition = _cachedLocalPosition + positionOffset;
				MyCore.transform.localRotation = _cachedLocalRotation * Quaternion.Euler(rotationOffsetEuler);
			}

			_output = AnimationPlayableOutput.Create(_graph, "StateTest", MyCore.animator);
			_output.SetSourcePlayable(_clipPlayable);
			_graph.Play();
			_isTesting = true;
			_testStartTime = Time.time;

			if (logStatus)
			{
				Debug.Log("[StateTest] 单Clip测试已启动。");
			}
		}

		private void StopTest()
		{
			if (!_isTesting) return;

			if (_graph.IsValid())
			{
				_graph.Stop();
				_graph.Destroy();
			}

			if (_offsetApplied)
			{
				MyCore.transform.localPosition = _cachedLocalPosition;
				MyCore.transform.localRotation = _cachedLocalRotation;
				_offsetApplied = false;
			}

			_isTesting = false;
			ResumeStateMachineIfNeeded();

			if (logStatus)
			{
				Debug.Log("[StateTest] 单Clip测试已停止。");
			}
		}

		private void ApplyRuntimeSettings()
		{
			if (!_clipPlayable.IsValid() || testClip == null) return;

			float clipLength = testClip.length;
			float elapsed = (Time.time - _testStartTime) * speed;
			float baseTime = Mathf.Clamp01(startNormalizedTime) * clipLength + elapsed;
			float time = loop ? Mathf.Repeat(baseTime, clipLength) : Mathf.Min(baseTime, clipLength);
			_clipPlayable.SetTime(time);
		}

		private void StopStateMachineIfNeeded()
		{
			if (MyDomain?.stateMachine == null) return;
			if (MyDomain.stateMachine.IsPlayableGraphPlaying)
			{
				MyDomain.stateMachine.StopStateMachine();
			}
		}

		private void ResumeStateMachineIfNeeded()
		{
			if (MyDomain?.stateMachine == null) return;
			if (MyDomain.stateMachine.IsPlayableGraphValid)
			{
				MyDomain.stateMachine.StartStateMachine();
			}
		}
	}
}
