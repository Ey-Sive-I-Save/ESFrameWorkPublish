using UnityEngine;

namespace ES
{
    public static class ProjectileMotionSolver
    {
        public static ProjectileMotionResult Step(
            ref ProjectileMotionState state,
            in ProjectileMotionConfig config,
            float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            state.previousPosition = state.currentPosition;
            state.elapsedTime += deltaTime;

            if (state.elapsedTime < Mathf.Max(0f, config.launchDelay))
                return BuildStaticResult(state, ProjectileMotionKind.Delayed);

            if (state.elapsedTime < Mathf.Max(0f, config.launchDelay) + Mathf.Max(0f, config.warmupTime))
                return BuildStaticResult(state, ProjectileMotionKind.Warmup);

            Vector3 desiredVelocity = ResolveDesiredVelocity(state, config);
            if (config.acceleration > 0f)
                state.velocity = Vector3.MoveTowards(state.velocity, desiredVelocity, config.acceleration * deltaTime);
            else
                state.velocity = desiredVelocity;

            if ((config.flags & ProjectileMotionFlags.UseGravity) != 0)
                state.velocity += config.gravity * deltaTime;

            if ((config.flags & ProjectileMotionFlags.UseDrag) != 0 && config.drag > 0f)
                state.velocity *= 1f / (1f + config.drag * deltaTime);

            if ((config.flags & ProjectileMotionFlags.ClampSpeed) != 0 && config.maxSpeed > 0f)
                state.velocity = Vector3.ClampMagnitude(state.velocity, config.maxSpeed);

            state.currentPosition += state.velocity * deltaTime;
            state.currentRotation = StepRotation(state.currentRotation, state.velocity, config, deltaTime);

            float remainingDistance = ResolveRemainingDistance(state);
            ProjectileMotionKind kind = ResolveKind(state, config, remainingDistance);

            return new ProjectileMotionResult
            {
                kind = kind,
                previousPosition = state.previousPosition,
                currentPosition = state.currentPosition,
                currentRotation = state.currentRotation,
                velocity = state.velocity,
                elapsedTime = state.elapsedTime,
                remainingDistance = remainingDistance,
                hasHitCandidate = false,
                hitCandidate = default
            };
        }

        private static Vector3 ResolveDesiredVelocity(in ProjectileMotionState state, in ProjectileMotionConfig config)
        {
            Vector3 direction = state.direction;
            if (state.hasTarget && IsTrackingEnabled(state.elapsedTime, config))
            {
                Vector3 toTarget = state.targetPosition - state.currentPosition;
                if (toTarget.sqrMagnitude > 0.0001f)
                    direction = toTarget.normalized;
            }

            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.forward;

            float speed = config.speed > 0f ? config.speed : config.maxSpeed;
            return direction.normalized * Mathf.Max(0f, speed);
        }

        private static ProjectileMotionResult BuildStaticResult(in ProjectileMotionState state, ProjectileMotionKind kind)
        {
            return new ProjectileMotionResult
            {
                kind = kind,
                previousPosition = state.previousPosition,
                currentPosition = state.currentPosition,
                currentRotation = state.currentRotation,
                velocity = state.velocity,
                elapsedTime = state.elapsedTime,
                remainingDistance = ResolveRemainingDistance(state),
                hasHitCandidate = false,
                hitCandidate = default
            };
        }

        private static bool IsTrackingEnabled(float elapsedTime, in ProjectileMotionConfig config)
        {
            float start = Mathf.Max(0f, config.launchDelay) + Mathf.Max(0f, config.trackingStartTime);
            if (elapsedTime < start)
                return false;

            return config.trackingDuration < 0f || elapsedTime <= start + config.trackingDuration;
        }

        private static Quaternion StepRotation(Quaternion currentRotation, Vector3 velocity, in ProjectileMotionConfig config, float deltaTime)
        {
            if ((config.flags & ProjectileMotionFlags.OrientToVelocity) == 0 || velocity.sqrMagnitude <= 0.0001f)
                return currentRotation;

            Quaternion target = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            if (config.turnSpeedDegrees <= 0f)
                return target;

            return Quaternion.RotateTowards(currentRotation, target, config.turnSpeedDegrees * deltaTime);
        }

        private static float ResolveRemainingDistance(in ProjectileMotionState state)
        {
            if (!state.hasTarget)
                return float.PositiveInfinity;

            return Vector3.Distance(state.currentPosition, state.targetPosition);
        }

        private static ProjectileMotionKind ResolveKind(in ProjectileMotionState state, in ProjectileMotionConfig config, float remainingDistance)
        {
            if (config.maxLifetime > 0f && state.elapsedTime >= config.maxLifetime)
                return ProjectileMotionKind.Expired;

            if (state.hasTarget && remainingDistance <= Mathf.Max(0f, config.arriveDistance))
                return ProjectileMotionKind.Arrived;

            return ProjectileMotionKind.Moving;
        }
    }
}
