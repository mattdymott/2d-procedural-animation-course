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
}
