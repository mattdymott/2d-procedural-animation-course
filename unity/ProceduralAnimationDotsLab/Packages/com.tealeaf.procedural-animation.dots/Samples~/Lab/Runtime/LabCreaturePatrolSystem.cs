using Unity.Entities;
using Unity.Mathematics;
using Tealeaf.ProceduralAnimation.Dots;

namespace ProceduralAnimationDotsLab
{
    /// <summary>
    /// Turns the lesson patrol policy into the package's locomotion input. The package
    /// applies it — and owns <c>CreatureBody</c> — inside its own solve group.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(ProceduralAnimationSolveSystemGroup))]
    public partial struct LabCreaturePatrolSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (body, locomotion, patrol) in SystemAPI.Query<RefRO<CreatureBody>, RefRW<CreatureLocomotion>, RefRW<LabCreaturePatrol>>())
            {
                var direction = patrol.ValueRO.Direction;
                if (body.ValueRO.RootPosition.x <= patrol.ValueRO.MinimumX)
                    direction = 1f;
                else if (body.ValueRO.RootPosition.x >= patrol.ValueRO.MaximumX)
                    direction = -1f;

                patrol.ValueRW.Direction = direction;
                locomotion.ValueRW.DesiredVelocity = new float2(patrol.ValueRO.Speed * direction, 0f);
            }
        }
    }
}
