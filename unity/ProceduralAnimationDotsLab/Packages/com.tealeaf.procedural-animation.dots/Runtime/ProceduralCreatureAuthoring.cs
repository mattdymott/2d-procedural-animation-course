using System;
using UnityEngine;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// The package front door for a 2D grounded creature recipe.
    /// It stores designer intent only; its Baker creates all solver history.
    /// </summary>
    public sealed class ProceduralCreatureAuthoring : MonoBehaviour
    {
        [Min(2)] public int ChainSegmentCount = 16;
        public Vector2 InitialRootPosition = new(-3.5f, 0.5f);
        [Min(0.001f)] public float LinkLength = 0.48f;
        [Range(0f, 1f)] public float Damping = 0.992f;
        [Min(0f)] public float MuscleStrength = 0.08f;

        [Header("Gait")]
        [Min(0f)] public float Comfort = 0.32f;
        [Min(0.001f)] public float StepDuration = 0.34f;
        public float StepLead = 0.12f;
        [Min(0f)] public float StepHeight = 0.42f;
        [Min(0f)] public float MinimumSupport = 0.7f;
        [Min(0f)] public float MinimumForward = 0.03f;

        public LegRecipe[] Legs = Array.Empty<LegRecipe>();
        public ContactPlaneRecipe[] ContactPlanes = Array.Empty<ContactPlaneRecipe>();

        [Serializable]
        public struct LegRecipe
        {
            public int AttachmentPointIndex;
            [Min(0.001f)] public float LengthA;
            [Min(0.001f)] public float LengthB;
            public float BendSign;
            public Vector2 HomeOffset;
        }

        [Serializable]
        public struct ContactPlaneRecipe
        {
            public Vector2 Point;
            public Vector2 Normal;
            [Min(0f)] public float Radius;
            [Range(0f, 1f)] public float Friction;
        }
    }
}
