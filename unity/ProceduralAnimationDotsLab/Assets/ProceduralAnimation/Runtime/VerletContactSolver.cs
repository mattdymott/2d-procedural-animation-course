using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    public static class VerletContactSolver
    {
        public static bool ProjectAgainstPlane(ref VerletPoint point, in ContactPlane plane)
        {
            var normal = math.normalizesafe(plane.Normal, new float2(0f, 1f));
            var radius = math.max(plane.Radius, 0f);
            var signedDistance = math.dot(point.Position - plane.Point, normal);
            var penetration = radius - signedDistance;
            if (penetration <= 0f)
                return false;

            point.Position += normal * penetration;

            var velocity = point.Position - point.PreviousPosition;
            var normalVelocity = math.dot(velocity, normal);
            var tangentVelocity = velocity - normal * normalVelocity;
            if (normalVelocity < 0f)
                normalVelocity = 0f;

            var friction = math.saturate(plane.Friction);
            point.PreviousPosition = point.Position - (normal * normalVelocity + tangentVelocity * (1f - friction));
            return true;
        }
    }
}
