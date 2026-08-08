using System;
using Unity.Entities;
using Unity.Mathematics;
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

        private sealed class ContactPlanesBaker : Baker<ContactPlanesAuthoring>
        {
            public override void Bake(ContactPlanesAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                var contacts = AddBuffer<ContactPlane>(entity);
                var recipes = authoring.ContactPlanes ?? Array.Empty<ContactPlaneRecipe>();

                for(var index = 0; index < recipes.Length; index++)
                {
                    var recipe = recipes[index];
                    contacts.Add(new ContactPlane
                    {
                        Point = new float2(recipe.Point.x, recipe.Point.y),
                        Normal = math.normalizesafe(new float2(recipe.Normal.x, recipe.Normal.y), new float2(0f, 1f)),
                        Radius = math.max(0f, recipe.Radius),
                        Friction = math.clamp(recipe.Friction, 0f, 1f),
                    });
                }
            }
        }
    }
}
