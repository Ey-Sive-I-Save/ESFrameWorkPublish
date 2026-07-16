using RootMotion.FinalIK;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        internal BipedIK presetBipedIK = null;
        internal GrounderBipedIK presetGrounderBipedIK = null;
        internal LookAtIK presetLookAtIK = null;
        internal AimIK presetAimIK = null;
        internal FullBodyBipedIK presetFullBodyBipedIK = null;
        internal HitReaction presetHitReaction = null;
        internal Recoil presetRecoil = null;
    }
}
