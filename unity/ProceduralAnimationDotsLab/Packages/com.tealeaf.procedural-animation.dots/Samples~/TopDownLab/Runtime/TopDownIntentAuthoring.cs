using UnityEngine;

namespace TopDownLab
{
    /// <summary>Attach beside the package authoring components to steer the demo creature.</summary>
    /// <remarks>
    /// One MonoBehaviour per file, named after its class: Unity resolves a script asset per file,
    /// so classes sharing a file serialize as broken references and never bake.
    /// </remarks>
    public sealed class TopDownIntentAuthoring : MonoBehaviour
    {
        public Vector2 Centre = Vector2.zero;
        [Min(0.01f)] public float Radius = 3.2f;
        [Min(0f)] public float Speed = 1.6f;

        [Tooltip("How fast the creature turns toward the course it wants, per second.")]
        [Min(0f)] public float TurnRate = 2.5f;

        [Header("Recovery response")]
        [Tooltip("Speed multiplier while gait reports it has nowhere legal to step.")]
        [Range(0f, 1f)] public float RecoverySpeedScale = 0.25f;

        [Tooltip("How strongly gait's suggested heading bends the creature, per second.")]
        [Min(0f)] public float RecoveryTurnRate = 2f;
    }
}
