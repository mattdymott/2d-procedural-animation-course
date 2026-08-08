using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// Alternating stepping for the limbs authored by <see cref="LegsAuthoring"/>.
    /// Tuning only: leg count, home offsets, and partner pairing are derived from the legs
    /// themselves, so the two buffers can never disagree about how many legs there are.
    /// </summary>
    /// <remarks>
    /// The defaults already walk; every field below is a refinement, not a requirement.
    /// Add <see cref="PlanarGaitAuthoring"/> beside it to move the same rules onto a top-down
    /// movement plane.
    /// </remarks>
    [AddComponentMenu("Tealeaf/Procedural Animation/Gait")]
    [RequireComponent(typeof(LegsAuthoring))]
    public sealed class GaitAuthoring : MonoBehaviour
    {
        [Min(0f)] public float Comfort = 0.32f;
        [Min(0.001f)] public float StepDuration = 0.34f;
        public float StepLead = 0.12f;
        [Min(0f)] public float StepHeight = 0.42f;

        [Header("Foothold policy")]
        [Tooltip("How upward a surface normal must be to hold a foot. Side-view only — a planar " +
                 "creature judges a foothold by walkability instead.")]
        [Min(0f)] public float MinimumSupport = 0.7f;
        [Min(0f)] public float MinimumForward = 0.03f;

        private sealed class GaitBaker : Baker<GaitAuthoring>
        {
            public override void Bake(GaitAuthoring authoring)
            {
                var chain = GetComponent<VerletChainAuthoring>();
                var legsAuthoring = GetComponent<LegsAuthoring>();
                if(!chain || !legsAuthoring)
                    return;

                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new Gait
                {
                    Comfort = math.max(0f, authoring.Comfort),
                    StepDuration = math.max(0.001f, authoring.StepDuration),
                    StepLead = authoring.StepLead,
                    StepHeight = math.max(0f, authoring.StepHeight),
                    MinimumSupport = math.max(0f, authoring.MinimumSupport),
                    MinimumForward = math.max(0f, authoring.MinimumForward),
                });

                // Leg count, home offsets, and partner pairing all come from the leg recipe, so the
                // gait buffer stays index-aligned with the limb buffer by construction.
                var legs = legsAuthoring.Legs ?? Array.Empty<LegsAuthoring.LegRecipe>();
                var gaitLegs = AddBuffer<GaitLeg>(entity);
                for(var index = 0; index < legs.Length; index++)
                {
                    var recipe = legs[index];
                    var attachmentIndex = CreatureBakerMath.AttachmentPointIndex(chain, recipe.AttachmentPointIndex);
                    var homeOffset = new float2(recipe.HomeOffset.x, recipe.HomeOffset.y);

                    gaitLegs.Add(new GaitLeg
                    {
                        State = FootState.Planted,
                        Plant = CreatureBakerMath.RestFoot(chain, attachmentIndex, recipe.HomeOffset),
                        HomeOffset = homeOffset,
                        PartnerIndex = (index ^ 1) < legs.Length ? (sbyte)(index ^ 1) : (sbyte)-1,
                        TripodGroup = (byte)math.clamp(recipe.TripodGroup, 0, 1),
                    });
                }

                AddBuffer<FootholdCandidate>(entity);
            }
        }
    }
}
