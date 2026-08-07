using System;
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
    }
}
