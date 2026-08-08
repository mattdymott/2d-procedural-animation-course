using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// Moves a creature onto a top-down movement plane. Adding this component changes how the
    /// same gait reads its data — leg home offsets become heading-relative, footholds are judged
    /// by walkability rather than a floor normal, and <see cref="GaitAuthoring.StepHeight"/>
    /// stops being world geometry and becomes a drawing instruction.
    /// </summary>
    /// <remarks>
    /// Set <see cref="VerletChainAuthoring.Gravity"/> to zero as well: on a movement plane there
    /// is no down for a body to sag toward. Home offsets bake unchanged because the initial
    /// heading is +X, so a creature authored facing right starts exactly where it did.
    /// </remarks>
    [AddComponentMenu("Tealeaf/Procedural Animation/Planar Gait")]
    [RequireComponent(typeof(GaitAuthoring))]
    public sealed class PlanarGaitAuthoring : MonoBehaviour
    {
        [Tooltip("Heading before the creature first moves. Leg home offsets are authored against it.")]
        public Vector2 InitialForward = Vector2.right;

        [Header("Support policy")]
        [Tooltip("Feet that must stay planted after a lift is granted. 0 keeps the partner rule alone.")]
        [Min(0)] public int MinimumPlantedFeet;

        [Header("Cadence")]
        public GaitCadence Cadence = GaitCadence.Partner;

        [Tooltip("Requested at or below Exit Speed — the careful policy.")]
        public GaitCadence SlowCadence = GaitCadence.Wave;

        [Tooltip("Requested at or above Enter Speed — the quick policy.")]
        public GaitCadence FastCadence = GaitCadence.Tripod;

        [Tooltip("Speed that requests Fast Cadence. Keep it above Exit Speed so the choice cannot flicker.")]
        [Min(0f)] public float EnterSpeed = 1.2f;

        [Tooltip("Speed that requests Slow Cadence.")]
        [Min(0f)] public float ExitSpeed = 0.7f;

        [Tooltip("Crawl order for the wave cadence: leg indices, in the order they are permitted to step.")]
        public int[] WaveOrder = Array.Empty<int>();

        sealed class PlanarGaitBaker : Baker<PlanarGaitAuthoring>
        {
            public override void Bake(PlanarGaitAuthoring authoring)
            {
                var legsAuthoring = GetComponent<LegsAuthoring>();
                if(!legsAuthoring)
                    return;

                var entity = GetEntity(TransformUsageFlags.None);
                var legCount = (legsAuthoring.Legs ?? Array.Empty<LegsAuthoring.LegRecipe>()).Length;

                AddComponent(entity, new PlanarHeading
                {
                    LastForward = math.normalizesafe(
                        new float2(authoring.InitialForward.x, authoring.InitialForward.y),
                        new float2(1f, 0f)),
                });

                // Enter must sit above exit or the two requests overlap and the cadence flickers
                // at the threshold — the one failure this pair of fields exists to prevent.
                var exitSpeed = math.max(0f, authoring.ExitSpeed);
                var enterSpeed = math.max(exitSpeed, authoring.EnterSpeed);

                AddComponent(entity, new GaitSupportPolicy
                {
                    MinimumPlantedFeet = (byte)math.clamp(authoring.MinimumPlantedFeet, 0, math.max(0, legCount - 1)),
                    SlowCadence = authoring.SlowCadence,
                    FastCadence = authoring.FastCadence,
                    EnterSpeed = enterSpeed,
                    ExitSpeed = exitSpeed,
                });

                AddComponent(entity, new GaitCadenceState
                {
                    Active = authoring.Cadence,
                    Pending = authoring.Cadence,
                });

                AddComponent(entity, new GaitRecoveryRequest
                {
                    State = GaitRecovery.None,
                    BlockedLegIndex = 255,
                    PreferredTurn = float2.zero,
                });

                AddComponent(entity, new WaveGaitState { Cursor = 0 });
                var waveOrder = AddBuffer<WaveOrder>(entity);
                var authoredOrder = authoring.WaveOrder ?? Array.Empty<int>();
                for(var index = 0; index < authoredOrder.Length; index++)
                {
                    var legIndex = authoredOrder[index];
                    if(legIndex < 0 || legIndex >= legCount)
                        continue;

                    waveOrder.Add(new WaveOrder { LegIndex = (byte)legIndex });
                }

                // An unauthored crawl order still has to name every leg once, or the cursor would
                // permit nothing and the creature would stand still looking like a bug.
                if(waveOrder.Length == 0)
                {
                    for(var index = 0; index < legCount; index++)
                        waveOrder.Add(new WaveOrder { LegIndex = (byte)index });
                }
            }
        }
    }
}
