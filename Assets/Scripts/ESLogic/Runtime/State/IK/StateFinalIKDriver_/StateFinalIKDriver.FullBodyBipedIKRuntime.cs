using UnityEngine;
using RootMotion.FinalIK;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        private void ApplyDriverFullBodyBipedIKSettings(FullBodyBipedIK fullBodyBipedIK)
        {
            if (fullBodyBipedIK == null || !useDriverFullBodyBipedIKSetup)
                return;

            var solver = fullBodyBipedIK.solver;
            solver.IKPositionWeight = Mathf.Clamp01(driverFullBodyIKWeight);
            solver.iterations = Mathf.Clamp(driverFullBodyIterations, 0, 10);
            solver.FABRIKPass = driverFullBodyFABRIKPass;
            solver.spineStiffness = Mathf.Clamp01(driverFullBodySpineStiffness);
            solver.pullBodyVertical = Mathf.Clamp(driverFullBodyPullBodyVertical, -1f, 1f);
            solver.pullBodyHorizontal = Mathf.Clamp(driverFullBodyPullBodyHorizontal, -1f, 1f);

            var spineMapping = solver.GetSpineMapping();
            if (spineMapping != null)
            {
                spineMapping.iterations = Mathf.Clamp(driverFullBodySpineMappingIterations, 1, 3);
                spineMapping.twistWeight = Mathf.Clamp01(driverFullBodySpineTwistWeight);
            }

            ApplyDriverFullBodyChainSettings(solver, FullBodyBipedChain.LeftArm, driverFullBodyArmPull, driverFullBodyArmReach, driverFullBodyArmPush, driverFullBodyArmMapping, driverFullBodyArmBendWeight);
            ApplyDriverFullBodyChainSettings(solver, FullBodyBipedChain.RightArm, driverFullBodyArmPull, driverFullBodyArmReach, driverFullBodyArmPush, driverFullBodyArmMapping, driverFullBodyArmBendWeight);
            ApplyDriverFullBodyChainSettings(solver, FullBodyBipedChain.LeftLeg, driverFullBodyLegPull, driverFullBodyLegReach, driverFullBodyLegPush, driverFullBodyLegMapping, driverFullBodyLegBendWeight);
            ApplyDriverFullBodyChainSettings(solver, FullBodyBipedChain.RightLeg, driverFullBodyLegPull, driverFullBodyLegReach, driverFullBodyLegPush, driverFullBodyLegMapping, driverFullBodyLegBendWeight);

            var bodyEffector = solver.bodyEffector;
            if (bodyEffector != null)
            {
                bodyEffector.effectChildNodes = driverFullBodyBodyAffectsThighs;
            }
        }

        private static void ApplyDriverFullBodyChainSettings(IKSolverFullBodyBiped solver, FullBodyBipedChain chainType, float pull, float reach, float push, float mappingWeight, float bendWeight)
        {
            var chain = solver.GetChain(chainType);
            if (chain != null)
            {
                chain.pull = Mathf.Clamp01(pull);
                chain.reach = Mathf.Clamp01(reach);
                chain.push = Mathf.Clamp01(push);
            }

            var mapping = solver.GetLimbMapping(chainType);
            if (mapping != null)
                mapping.weight = Mathf.Clamp01(mappingWeight);

            var bend = solver.GetBendConstraint(chainType);
            if (bend != null)
                bend.weight = Mathf.Clamp01(bendWeight);
        }

        private void ApplyRuntimeFullBodyBipedIKOffsets(IKSolverFullBodyBiped solver)
        {
            if (_fullBodyBodyOffsetWeight <= 0.001f)
                return;

            solver.bodyEffector.positionOffset += transform.TransformVector(_fullBodyBodyOffsetLocal) * Mathf.Clamp01(_fullBodyBodyOffsetWeight);
        }

        public bool IKSetFullBodyBodyOffset(Vector3 localOffset, float weight = 1f)
        {
            if (!_fullBodyBipedIKReady)
                return false;

            _fullBodyBodyOffsetLocal = localOffset;
            _fullBodyBodyOffsetWeight = Mathf.Clamp01(weight);
            return true;
        }

        public bool IKClearFullBodyBodyOffset()
        {
            if (!_fullBodyBipedIKReady)
                return false;

            _fullBodyBodyOffsetLocal = Vector3.zero;
            _fullBodyBodyOffsetWeight = 0f;
            return true;
        }

        public bool IKSetFullBodyEffector(FullBodyBipedEffector effectorType, Vector3 worldPosition, float positionWeight, Quaternion worldRotation, float rotationWeight)
        {
            if (!_fullBodyBipedIKReady)
                return false;

            var effector = _refs.fullBodyBipedIK.solver.GetEffector(effectorType);
            effector.position = worldPosition;
            effector.positionWeight = Mathf.Clamp01(positionWeight);
            effector.rotation = worldRotation;
            effector.rotationWeight = Mathf.Clamp01(rotationWeight);
            return true;
        }

        public bool IKSetFullBodyEffectorWeight(FullBodyBipedEffector effectorType, float positionWeight, float rotationWeight = 0f)
        {
            if (!_fullBodyBipedIKReady)
                return false;

            var effector = _refs.fullBodyBipedIK.solver.GetEffector(effectorType);
            effector.positionWeight = Mathf.Clamp01(positionWeight);
            effector.rotationWeight = Mathf.Clamp01(rotationWeight);
            return true;
        }

        public bool IKSetFullBodyChainWeights(FullBodyBipedChain chainType, float pull, float reach, float push)
        {
            if (!_fullBodyBipedIKReady)
                return false;

            var chain = _refs.fullBodyBipedIK.solver.GetChain(chainType);
            chain.pull = Mathf.Clamp01(pull);
            chain.reach = Mathf.Clamp01(reach);
            chain.push = Mathf.Clamp01(push);
            return true;
        }
    }
}
