using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WellFired.Command.Unity.Runtime.UI
{
	public class Scroller
	{
		public enum ScrollerState
		{
			Idle,
			Scrolling,
			Decelerating
		}

		private const int SpeedSampleLenght = 5;

		private readonly float _maxReleaseSpeed;
		private readonly float _decelerationSpeed;
		private Vector2 _speed;
		private readonly Queue<Vector2> _touchSpeedQueue = new Queue<Vector2>();
		public ScrollerState State { get; private set; }
		private Vector2 _lastTouchPosition;

		public Scroller(float maxReleaseSpeed, float decelerationSpeed)
		{
			_maxReleaseSpeed = maxReleaseSpeed;
			_decelerationSpeed = decelerationSpeed;
		}

		public void DoTouch(Vector2 touchPosition)
		{
			_touchSpeedQueue.Clear();
			State = ScrollerState.Scrolling;
			_speed = Vector2.zero;
			_lastTouchPosition = touchPosition;
		}

		public void DoUntouch()
		{
			State = ScrollerState.Decelerating;
			_speed = GetReleaseSpeed();
			_speed = Vector2.ClampMagnitude(_speed, _maxReleaseSpeed);
		}

		public Vector2 Update(Vector2 touchPosition, float deltaTime)
		{
			var delta = Vector2.zero;

			switch (State)
			{
				case ScrollerState.Idle:
					break;

				case ScrollerState.Scrolling:
					delta = touchPosition - _lastTouchPosition;
					RecordSpeed(delta / deltaTime);
					_lastTouchPosition = touchPosition;

					break;

				case ScrollerState.Decelerating:
					delta = GetDeceleratingSpeed(deltaTime) * deltaTime;
					_speed = delta / deltaTime;

					if (Mathf.Approximately(_speed.sqrMagnitude, 0f))
					{
						State = ScrollerState.Idle;
					}

					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			return delta;
		}

		public void ZeroSpeed()
		{
			_speed = Vector2.zero;
			State = ScrollerState.Idle;
		}

		private Vector2 GetReleaseSpeed()
		{
			if (_touchSpeedQueue.Count == 0)
			{
				return Vector2.zero;
			}
            
			var speed = _touchSpeedQueue.Aggregate(Vector2.zero, (current, touchSpeed) => current + touchSpeed);
			speed /= _touchSpeedQueue.Count;
			return speed;
		}

		private void RecordSpeed(Vector2 speed)
		{
			_touchSpeedQueue.Enqueue(speed);

			if (_touchSpeedQueue.Count > SpeedSampleLenght)
			{
				_touchSpeedQueue.Dequeue();
			}
		}

		private Vector2 GetDeceleratingSpeed(float deltaTime)
		{
			return Vector2.MoveTowards(_speed, Vector2.zero, deltaTime * _decelerationSpeed);
		}
	}
}