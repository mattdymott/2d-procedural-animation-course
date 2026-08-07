using UnityEngine;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// Alternating stepping for the limbs authored by <see cref="LegsAuthoring"/>.
    /// Tuning only: leg count, home offsets, and partner pairing are derived from the legs
    /// themselves, so the two buffers can never disagree about how many legs there are.
    /// </summary>
    /// <remarks>The defaults already walk; every field below is a refinement, not a requirement.</remarks>
    [AddComponentMenu("Tealeaf/Procedural Animation/Gait")]
    [RequireComponent(typeof(LegsAuthoring))]
    public sealed class GaitAuthoring : MonoBehaviour
    {
        [Min(0f)] public float Comfort = 0.32f;
        [Min(0.001f)] public float StepDuration = 0.34f;
        public float StepLead = 0.12f;
        [Min(0f)] public float StepHeight = 0.42f;

        [Header("Foothold policy")]
        [Min(0f)] public float MinimumSupport = 0.7f;
        [Min(0f)] public float MinimumForward = 0.03f;
    }
}
