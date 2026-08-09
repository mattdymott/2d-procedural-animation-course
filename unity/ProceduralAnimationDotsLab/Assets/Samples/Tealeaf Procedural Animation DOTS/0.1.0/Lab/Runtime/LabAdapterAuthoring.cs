using UnityEngine;

namespace ProceduralAnimationDotsLab
{
    /// <summary>Attach beside the package authoring components to drive the lesson patrol.</summary>
    public sealed class LabCreaturePatrolAuthoring : MonoBehaviour
    {
        public float Speed = 0.8f;
        public float MinimumX = -4f;
        public float MaximumX = 0f;
    }

    /// <summary>
    /// Attach beside the package authoring components to have the lesson terrain adapter serve
    /// this creature's footholds. This is what scopes the adapter — a creature without it is left
    /// alone, which is how a top-down creature sharing the world keeps its own footholds.
    /// </summary>
    public sealed class LabTerrainAdapterAuthoring : MonoBehaviour
    {
        [Tooltip("Record each probe result for the lesson visualiser. Debug data only — the " +
                 "adapter serves the same footholds either way — but VerletChainDemo requires " +
                 "the buffer to bind, so the lesson scene draws nothing without it.")]
        public bool RecordProbes = true;
    }

    /// <summary>Authors the lesson elevator/conveyor that writes the package support seam.</summary>
    public sealed class LabMovingSupportAuthoring : MonoBehaviour
    {
        public Vector2 Origin = new(-1.1f, -1.75f);
        public Vector2 Amplitude = new(0f, 0.28f);
        public float Frequency = 1.1f;
        public Vector2 SurfaceVelocityLocal = new(0.55f, 0f);
    }
}
