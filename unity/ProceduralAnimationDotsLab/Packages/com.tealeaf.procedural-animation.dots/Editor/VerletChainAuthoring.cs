using Tealeaf.ProceduralAnimation.Dots.LowLevel;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// The chain body: a creature's spine and the root everything else hangs from.
    /// This is the base of the package's component set — legs index into its points, so any
    /// creature that has legs has a chain first.
    /// </summary>
    /// <remarks>
    /// Add this alone for a soft rope, tail, or tentacle. Add <see cref="MusclesAuthoring"/> to
    /// draw its tip toward a target you write, <see cref="LegsAuthoring"/> for limbs,
    /// <see cref="GaitAuthoring"/> for stepping, and <see cref="ContactPlanesAuthoring"/> for
    /// static geometry the body must not sink through.
    /// </remarks>
    [AddComponentMenu("Tealeaf/Procedural Animation/Verlet Chain")]
    public sealed class VerletChainAuthoring : MonoBehaviour
    {
        [Min(2)] public int ChainSegmentCount = 16;
        public Vector2 InitialRootPosition = new(-3.5f, 0.5f);

        [FormerlySerializedAs("LinkLength")]
        [Min(0.001f)] public float RestLength = 0.48f;

        [Range(0f, 1f)] public float Damping = 0.992f;

        /// <summary>Acceleration on every point but the pinned root. Set to zero for a chain in free space.</summary>
        public Vector2 Gravity = new(0f, -3.5f);

        [Header("Root bob")]
        [Tooltip("Decorative vertical oscillation of the pinned root. Zero amplitude means the root does not bob.")]
        [Min(0f)] public float RootBobAmplitude;
        [Min(0f)] public float RootBobFrequency;

        private sealed class VerletChainBaker : Baker<VerletChainAuthoring>
        {
            public override void Bake(VerletChainAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var segmentCount = math.max(2, authoring.ChainSegmentCount);
                var root = ToFloat2(authoring.InitialRootPosition);
                var restLength = math.max(0.001f, authoring.RestLength);

                AddComponent(entity, new VerletChain
                {
                    RestLength = restLength,
                    Damping = math.clamp(authoring.Damping, 0f, 1f),
                    Gravity = ToFloat2(authoring.Gravity),
                    RootBobAmplitude = math.max(0f, authoring.RootBobAmplitude),
                    RootBobFrequency = math.max(0f, authoring.RootBobFrequency),
                });
                AddComponent(entity, new CreatureBody { RootPosition = root });
                AddComponent<CreatureLocomotion>(entity);

                var points = AddBuffer<VerletPoint>(entity);
                for (var index = 0; index < segmentCount; index++)
                {
                    var position = CreatureLayout.PointPosition(root, restLength, index);
                    points.Add(new VerletPoint { Position = position, PreviousPosition = position });
                }
            }

            private static float2 ToFloat2(Vector2 value) => new(value.x, value.y);
        }
    }
}
