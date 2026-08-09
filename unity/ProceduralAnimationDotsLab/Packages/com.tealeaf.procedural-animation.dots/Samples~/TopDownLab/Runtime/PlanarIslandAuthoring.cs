using UnityEngine;

namespace TopDownLab
{
    /// <summary>A circular patch of the movement plane the planar query reports as blocked.</summary>
    public sealed class PlanarIslandAuthoring : MonoBehaviour
    {
        public Vector2 Centre = new(3.75f, 0f);
        [Min(0f)] public float Radius = 0.8f;
    }
}
