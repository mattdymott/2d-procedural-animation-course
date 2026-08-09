using UnityEngine;

namespace TopDownLab
{
    /// <summary>
    /// Attach beside the package authoring components so the planar query adapter records what it
    /// offered this tick. Nothing in the simulation reads it back: a creature without this
    /// component receives exactly the same footholds. <see cref="TopDownLabDemo"/> does require
    /// the buffer to bind, so the lesson scene would draw nothing at all without it.
    /// </summary>
    public sealed class PlanarQueryDebugAuthoring : MonoBehaviour
    {
    }
}
