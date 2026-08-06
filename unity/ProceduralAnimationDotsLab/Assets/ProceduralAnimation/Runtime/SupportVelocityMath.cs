using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    public static class SupportVelocityMath
    {
        public static float2 PointVelocity(
            in SupportPose pose,
            in SupportMotion motion,
            float2 localPoint)
        {
            var rotationalVelocity = SupportPoseMath.TransformDirection(
                pose,
                new float2(-localPoint.y, localPoint.x)) * motion.AngularVelocity;
            var beltVelocity = SupportPoseMath.TransformDirection(pose, motion.BeltVelocityLocal);
            return motion.WorldVelocity + rotationalVelocity + beltVelocity;
        }
    }
}
