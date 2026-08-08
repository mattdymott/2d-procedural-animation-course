using Tealeaf.ProceduralAnimation.Dots;
using Tealeaf.ProceduralAnimation.Dots.LowLevel;
using Unity.Entities;
using Unity.Mathematics;

namespace TopDownLab
{
    /// <summary>
    /// The consumer side of the top-down seam: a steering loop that follows a circuit, and the
    /// only thing allowed to act on a recovery request. Gait says "I have nowhere legal to step";
    /// this system decides what that means — slow down and bend away — and the creature's path
    /// genuinely changes as a result. It never touches a plant or a foot target.
    /// </summary>
    /// <remarks>
    /// Steering has to change the course, not just the facing. An earlier version bent the
    /// heading while a fixed circuit kept driving the body, so a creature that ran out of legal
    /// ground could ask for help forever and never actually move away from the obstacle.
    /// </remarks>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(ProceduralAnimationSolveSystemGroup))]
    public partial struct TopDownIntentSystem : ISystem
    {
        /// <summary>How far outside a blocked region the body keeps its own centre.</summary>
        const float BodyClearance = 0.9f;

        /// <summary>
        /// How far off the nose the chosen course has to be before it counts as a decided turn
        /// worth announcing — about twenty degrees.
        /// </summary>
        /// <remarks>
        /// Steering lags its course by roughly speed / (radius · turn rate) the whole way round an
        /// ordinary circuit, which here is some eleven degrees. A threshold under that leaves the
        /// request permanently raised, and a cue that never falls silent announces nothing: this
        /// sits above the cruise lag so only a real change of course — an avoidance, a recovery —
        /// gets a wind-up.
        /// </remarks>
        const float TurnRequestCosine = 0.94f;

        EntityQuery islandQuery;

        public void OnCreate(ref SystemState state)
        {
            islandQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlanarIsland>());
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var island = islandQuery.IsEmptyIgnoreFilter
                ? new PlanarIsland { Radius = -1f }
                : islandQuery.GetSingleton<PlanarIsland>();

            foreach (var (intent, locomotion, heading, body, entity) in
                     SystemAPI.Query<RefRO<TopDownIntent>, RefRW<CreatureLocomotion>, RefRO<PlanarHeading>,
                         RefRO<CreatureBody>>().WithEntityAccess())
            {
                var settings = intent.ValueRO;
                var forward = math.normalizesafe(heading.ValueRO.LastForward, new float2(1f, 0f));

                var recovering = SystemAPI.HasComponent<GaitRecoveryRequest>(entity)
                    && SystemAPI.GetComponent<GaitRecoveryRequest>(entity).State != GaitRecovery.None;

                var course = CircuitCourse(settings, body.ValueRO.RootPosition, forward);
                var turnRate = settings.TurnRate;
                if (recovering)
                {
                    // Gait's suggestion is a heading that would bring the blocked foot back over
                    // ground it can stand on. Taking it is this system's choice, not gait's.
                    var request = SystemAPI.GetComponent<GaitRecoveryRequest>(entity);
                    course = math.normalizesafe(request.PreferredTurn, course);
                    turnRate = settings.RecoveryTurnRate;
                }

                // Body avoidance is applied last so it survives a recovery turn: gait's suggestion
                // is about where a foot can land, and must never steer the body into the obstacle.
                course = AvoidIsland(course, island, body.ValueRO.RootPosition);

                var speed = recovering
                    ? settings.Speed * math.max(0f, settings.RecoverySpeedScale)
                    : settings.Speed;

                var steered = math.normalizesafe(
                    forward + (course - forward) * math.saturate(turnRate * deltaTime),
                    forward);

                locomotion.ValueRW.DesiredVelocity = steered * speed;
                locomotion.ValueRW.DesiredHeading = steered;

                // The same decision, published a second time as a semantic fact. Steering already
                // happened on the line above; this only says which way, and only while the course
                // is somewhere the heading has not caught up to yet.
                locomotion.ValueRW.RequestedTurnSign = math.dot(course, forward) < TurnRequestCosine
                    ? math.sign(math.dot(course, PlanarMath.Perpendicular(forward)))
                    : 0f;
            }
        }

        /// <summary>
        /// Keeps the body itself out of blocked space. This is locomotion's job, not gait's: a
        /// creature that walks its own centre into an obstacle leaves every leg with nowhere legal
        /// to step, and no amount of foot policy can rescue it.
        /// </summary>
        static float2 AvoidIsland(float2 course, in PlanarIsland island, float2 position)
        {
            if (island.Radius <= 0f)
                return course;

            var away = position - island.Centre;
            var distance = math.length(away);
            var clearance = island.Radius + BodyClearance;
            if (distance >= clearance)
                return course;

            var push = math.normalizesafe(away, course) * ((clearance - distance) / clearance) * 2f;
            return math.normalizesafe(course + push, course);
        }

        /// <summary>
        /// The heading that follows the circuit: its tangent, corrected back toward the authored
        /// radius so a creature pushed off course rejoins instead of spiralling away.
        /// </summary>
        static float2 CircuitCourse(in TopDownIntent settings, float2 position, float2 forward)
        {
            var offset = position - settings.Centre;
            var outward = math.normalizesafe(offset, -forward);
            var tangent = PlanarMath.Perpendicular(outward);
            var radialError = math.length(offset) - settings.Radius;
            return math.normalizesafe(tangent - outward * math.clamp(radialError, -1f, 1f), tangent);
        }
    }
}
