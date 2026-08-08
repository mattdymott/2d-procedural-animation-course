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
            public Vector2 HomeOffset;
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
