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
    }
}
