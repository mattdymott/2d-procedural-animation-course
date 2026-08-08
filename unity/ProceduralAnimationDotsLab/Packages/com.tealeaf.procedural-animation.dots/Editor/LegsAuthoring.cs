using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// Two-bone limbs hanging off the chain. Without <see cref="GaitAuthoring"/> the limbs
    /// still solve every tick — your own system writes each limb's target.
    /// </summary>
    [AddComponentMenu("Tealeaf/Procedural Animation/Legs")]
    [RequireComponent(typeof(VerletChainAuthoring))]
    public sealed class LegsAuthoring : MonoBehaviour
    {
        public LegRecipe[] Legs = Array.Empty<LegRecipe>();

        [Serializable]
        public struct LegRecipe
        {
            public int AttachmentPointIndex;
            [Min(0.001f)] public float LengthA;
            [Min(0.001f)] public float LengthB;
            public float BendSign;

            [Tooltip("Where this foot wants to stand, relative to its hip. A planar creature reads " +
                     "it as x along the heading and y across it.")]
            public Vector2 HomeOffset;

            [Tooltip("Alternating tripod this leg belongs to: 0 or 1. Only the tripod cadence reads it. " +
                     "Per-leg gait data lives here so it cannot fall out of step with the leg list.")]
            [Range(0, 1)] public int TripodGroup;
        }

        private sealed class LegsBaker : Baker<LegsAuthoring>
        {
            public override void Bake(LegsAuthoring authoring)
            {
                var chain = GetComponent<VerletChainAuthoring>();
                if(!chain)
                    return;

                var entity = GetEntity(TransformUsageFlags.None);
                var legs = authoring.Legs ?? Array.Empty<LegsAuthoring.LegRecipe>();
                var limbs = AddBuffer<Limb2BoneLeg>(entity);

                for(var index = 0; index < legs.Length; index++)
                {
                    var recipe = legs[index];
                    var attachmentIndex = CreatureBakerMath.AttachmentPointIndex(chain, recipe.AttachmentPointIndex);

                    limbs.Add(new Limb2BoneLeg
                    {
                        RootPointIndex = attachmentIndex,
                        Limb = new Limb2Bone
                        {
                            Target = CreatureBakerMath.RestFoot(chain, attachmentIndex, recipe.HomeOffset),
                            LengthA = math.max(0.001f, recipe.LengthA),
                            LengthB = math.max(0.001f, recipe.LengthB),
                            BendSign = recipe.BendSign < 0f ? -1f : 1f,
                        },
                    });
                }
            }
        }
    }
}
