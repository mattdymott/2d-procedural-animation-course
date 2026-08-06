using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
    public static class VerletChainSolver
    {
        public static float2 ResolveRoot(float2 bodyPosition, float time) => bodyPosition + new float2(0f, math.sin(time * 0.9f) * 0.35f);
        public static void Pin(ref VerletPoint point, float2 position) { point.Position = position; point.PreviousPosition = position; }
        public static void SatisfyDistance(ref VerletPoint first, ref VerletPoint second, float restLength)
        {
            var offset = second.Position - first.Position;
            var distance = math.length(offset);
            if (distance < 0.0001f) return;
            var correction = offset * ((distance - restLength) / distance);
            first.Position += correction * 0.5f;
            second.Position -= correction * 0.5f;
        }
    }

    public static class VerletContactSolver
    {
        public static bool ProjectAgainstPlane(ref VerletPoint point, in ContactPlane plane)
        {
            var normal = math.normalizesafe(plane.Normal, new float2(0f, 1f));
            var penetration = math.max(plane.Radius, 0f) - math.dot(point.Position - plane.Point, normal);
            if (penetration <= 0f) return false;
            point.Position += normal * penetration;
            var velocity = point.Position - point.PreviousPosition;
            var normalVelocity = math.max(math.dot(velocity, normal), 0f);
            var tangentVelocity = velocity - normal * math.dot(velocity, normal);
            point.PreviousPosition = point.Position - (normal * normalVelocity + tangentVelocity * (1f - math.saturate(plane.Friction)));
            return true;
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(TwoBoneIkSystem))]
    public partial struct HardResolveSystem : ISystem
    {
        const int ConstraintIterations = 2;
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (chain, body, points, contacts) in SystemAPI.Query<RefRO<VerletChain>, RefRO<CreatureBody>, DynamicBuffer<VerletPoint>, DynamicBuffer<ContactPlane>>())
            {
                if (points.Length < 2) continue;
                var mutablePoints = points;
                var root = VerletChainSolver.ResolveRoot(body.ValueRO.RootPosition, chain.ValueRO.Time);
                for (var iteration = 0; iteration < ConstraintIterations; iteration++)
                {
                    var pinnedRoot = mutablePoints[0]; VerletChainSolver.Pin(ref pinnedRoot, root); mutablePoints[0] = pinnedRoot;
                    for (var index = 0; index < mutablePoints.Length - 1; index++)
                    {
                        var first = mutablePoints[index]; var second = mutablePoints[index + 1];
                        VerletChainSolver.SatisfyDistance(ref first, ref second, chain.ValueRO.LinkLength);
                        if (index == 0) VerletChainSolver.Pin(ref first, root);
                        mutablePoints[index] = first; mutablePoints[index + 1] = second;
                    }
                    for (var pointIndex = 1; pointIndex < mutablePoints.Length; pointIndex++)
                    {
                        var point = mutablePoints[pointIndex];
                        for (var contactIndex = 0; contactIndex < contacts.Length; contactIndex++) VerletContactSolver.ProjectAgainstPlane(ref point, contacts[contactIndex]);
                        mutablePoints[pointIndex] = point;
                    }
                }
            }
        }
    }
}
