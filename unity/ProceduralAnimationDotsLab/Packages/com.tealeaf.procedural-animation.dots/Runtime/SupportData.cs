using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// Current planar pose supplied by a support adapter.
    /// Rotation is expressed in radians.
    /// </summary>
    public struct SupportPose : IComponentData
    {
        public float2 Position;
        public float RotationRadians;
    }

    /// <summary>
    /// Current support motion supplied by a support adapter.
    /// Surface velocity is measured in support-local coordinates.
    /// </summary>
    public struct SupportKinematics : IComponentData
    {
        public float2 LinearVelocity;
        public float AngularVelocityRadians;
        public float2 SurfaceVelocityLocal;
    }

    /// <summary>
    /// Pure planar support-pose and support-velocity calculations.
    /// </summary>
    public static class SupportMath
    {
        public static float2 TransformPoint(in SupportPose pose, float2 localPoint)
        {
            var sine = math.sin(pose.RotationRadians);
            var cosine = math.cos(pose.RotationRadians);
            return pose.Position + new float2(
                cosine * localPoint.x - sine * localPoint.y,
                sine * localPoint.x + cosine * localPoint.y);
        }

        public static float2 InverseTransformPoint(in SupportPose pose, float2 worldPoint)
        {
            var offset = worldPoint - pose.Position;
            var sine = math.sin(pose.RotationRadians);
            var cosine = math.cos(pose.RotationRadians);
            return new float2(
                cosine * offset.x + sine * offset.y,
                -sine * offset.x + cosine * offset.y);
        }

        public static float2 TransformDirection(in SupportPose pose, float2 localDirection)
        {
            var sine = math.sin(pose.RotationRadians);
            var cosine = math.cos(pose.RotationRadians);
            return new float2(
                cosine * localDirection.x - sine * localDirection.y,
                sine * localDirection.x + cosine * localDirection.y);
        }

        /// <summary>
        /// Returns the world velocity of a support-local contact point, including conveyor travel.
        /// </summary>
        public static float2 PointVelocity(
            in SupportPose pose,
            in SupportKinematics kinematics,
            float2 localPoint)
        {
            var rotationalVelocity = TransformDirection(
                pose,
                new float2(-localPoint.y, localPoint.x)) * kinematics.AngularVelocityRadians;
            var surfaceVelocity = TransformDirection(pose, kinematics.SurfaceVelocityLocal);
            return kinematics.LinearVelocity + rotationalVelocity + surfaceVelocity;
        }
    }
}
