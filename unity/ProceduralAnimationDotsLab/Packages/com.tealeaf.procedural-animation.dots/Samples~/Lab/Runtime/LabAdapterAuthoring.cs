using UnityEngine;

namespace ProceduralAnimationDotsLab
{
    /// <summary>Attach beside <c>ProceduralCreatureAuthoring</c> to drive the lesson patrol.</summary>
    public sealed class LabCreaturePatrolAuthoring : MonoBehaviour
    {
        public float Speed = 0.8f;
        public float MinimumX = -4f;
        public float MaximumX = 0f;
    }

    /// <summary>
    /// Attach beside <c>ProceduralCreatureAuthoring</c> to have the lesson terrain adapter
    /// serve this creature's footholds and record its probe results for presentation.
    /// </summary>
    public sealed class LabTerrainAdapterAuthoring : MonoBehaviour
    {
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
