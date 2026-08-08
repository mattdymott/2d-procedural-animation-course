using Tealeaf.ProceduralAnimation.Dots.LowLevel;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// Draws the chain tip toward a target each tick — a jellyfish pulse, a reaching tentacle,
    /// a tail that follows a lure.
    /// </summary>
    /// <remarks>
    /// The target itself is yours: write <see cref="ChainTarget"/> from your own system before
    /// the solve group. Composing without this component is what makes a plain rope a plain
    /// rope — nothing pulls its tip anywhere.
    /// </remarks>
    [AddComponentMenu("Tealeaf/Procedural Animation/Muscles")]
    [RequireComponent(typeof(VerletChainAuthoring))]
    public sealed class MusclesAuthoring : MonoBehaviour
    {
        [Tooltip("Fraction of the remaining distance the tip closes on the target each tick.")]
        [Range(0f, 1f)] public float Strength = 0.08f;

        private sealed class MusclesBaker : Baker<MusclesAuthoring>
        {
            public override void Bake(MusclesAuthoring authoring)
            {
                var chain = GetComponent<VerletChainAuthoring>();
                if(!chain)
                    return;

                // Seed the target on the tip's own rest position so an unwritten target is a no-op
                // rather than a yank toward the world origin on the first tick.
                var root = new float2(chain.InitialRootPosition.x, chain.InitialRootPosition.y);
                var restLength = math.max(0.001f, chain.RestLength);
                var tipIndex = math.max(2, chain.ChainSegmentCount) - 1;

                AddComponent(GetEntity(TransformUsageFlags.None), new ChainTarget
                {
                    Position = CreatureLayout.PointPosition(root, restLength, tipIndex),
                    Strength = math.saturate(authoring.Strength),
                });
            }
        }
    }
}
