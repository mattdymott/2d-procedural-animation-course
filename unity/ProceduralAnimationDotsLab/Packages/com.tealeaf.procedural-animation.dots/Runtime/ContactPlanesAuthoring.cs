using System;
using UnityEngine;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// Static one-sided planes the chain body must not sink through — a floor, a wall.
    /// Independent of footholds: a contact plane stops the body, a foothold candidate offers a
    /// place to step. Omit this component and the chain still solves; it just has nothing to
    /// collide with.
    /// </summary>
    [AddComponentMenu("Tealeaf/Procedural Animation/Contact Planes")]
    [RequireComponent(typeof(VerletChainAuthoring))]
    public sealed class ContactPlanesAuthoring : MonoBehaviour
    {
        public ContactPlaneRecipe[] ContactPlanes = Array.Empty<ContactPlaneRecipe>();

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
