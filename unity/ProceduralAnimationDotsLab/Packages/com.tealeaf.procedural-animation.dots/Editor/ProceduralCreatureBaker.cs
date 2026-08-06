using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Tealeaf.ProceduralAnimation.Dots
{
    sealed class ProceduralCreatureBaker : Baker<ProceduralCreatureAuthoring>
    {
        public override void Bake(ProceduralCreatureAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var segmentCount = math.max(2, authoring.ChainSegmentCount);
            var root = ToFloat2(authoring.InitialRootPosition);
            var linkLength = math.max(0.001f, authoring.LinkLength);

            AddComponent(entity, new VerletChain
            {
                LinkLength = linkLength,
                Damping = math.clamp(authoring.Damping, 0f, 1f),
                MuscleStrength = math.max(0f, authoring.MuscleStrength),
            });
            AddComponent(entity, new CreatureBody { RootPosition = root });
            AddComponent<CreatureLocomotion>(entity);

            var points = AddBuffer<VerletPoint>(entity);
            for (var index = 0; index < segmentCount; index++)
            {
                var position = root + new float2(index * linkLength, 0f);
                points.Add(new VerletPoint { Position = position, PreviousPosition = position });
            }

            AddComponent(entity, new ChainTarget { Position = points[points.Length - 1].Position });

            var legs = authoring.Legs ?? Array.Empty<ProceduralCreatureAuthoring.LegRecipe>();
            var limbs = AddBuffer<Limb2BoneLeg>(entity);
            var gaitLegs = AddBuffer<GaitLeg>(entity);
            for (var index = 0; index < legs.Length; index++)
            {
                var recipe = legs[index];
                var attachmentIndex = math.clamp(recipe.AttachmentPointIndex, 0, segmentCount - 1);
                var homeOffset = ToFloat2(recipe.HomeOffset);
                var initialPlant = points[attachmentIndex].Position + homeOffset;
                var partnerIndex = (index ^ 1) < legs.Length ? (sbyte)(index ^ 1) : (sbyte)-1;

                limbs.Add(new Limb2BoneLeg
                {
                    RootPointIndex = attachmentIndex,
                    Limb = new Limb2Bone
                    {
                        Target = initialPlant,
                        LengthA = math.max(0.001f, recipe.LengthA),
                        LengthB = math.max(0.001f, recipe.LengthB),
                        BendSign = recipe.BendSign < 0f ? -1f : 1f,
                    },
                });
                gaitLegs.Add(new GaitLeg
                {
                    State = FootState.Planted,
                    Plant = initialPlant,
                    HomeOffset = homeOffset,
                    PartnerIndex = partnerIndex,
                });
            }

            AddComponent(entity, new GaitSettings
            {
                Comfort = math.max(0f, authoring.Comfort),
                StepDuration = math.max(0.001f, authoring.StepDuration),
                StepLead = authoring.StepLead,
                StepHeight = math.max(0f, authoring.StepHeight),
                MinimumSupport = math.max(0f, authoring.MinimumSupport),
                MinimumForward = math.max(0f, authoring.MinimumForward),
            });

            var contacts = AddBuffer<ContactPlane>(entity);
            var contactRecipes = authoring.ContactPlanes ?? Array.Empty<ProceduralCreatureAuthoring.ContactPlaneRecipe>();
            for (var index = 0; index < contactRecipes.Length; index++)
            {
                var recipe = contactRecipes[index];
                contacts.Add(new ContactPlane
                {
                    Point = ToFloat2(recipe.Point),
                    Normal = math.normalizesafe(ToFloat2(recipe.Normal), new float2(0f, 1f)),
                    Radius = math.max(0f, recipe.Radius),
                    Friction = math.clamp(recipe.Friction, 0f, 1f),
                });
            }

            AddBuffer<FootholdCandidate>(entity);
        }

        static float2 ToFloat2(Vector2 value) => new(value.x, value.y);
    }
}
