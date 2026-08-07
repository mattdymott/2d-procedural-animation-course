using System;
using Tealeaf.ProceduralAnimation.Dots.LowLevel;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// One baker per authoring component. Each owns the components its own feature needs, so the
    /// entity ends up carrying exactly the features that were composed onto the GameObject.
    /// Bakers read sibling authoring components rather than each other's baked output, which is
    /// what keeps them independent of baking order.
    /// </summary>
    sealed class VerletChainBaker : Baker<VerletChainAuthoring>
    {
        public override void Bake(VerletChainAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var segmentCount = math.max(2, authoring.ChainSegmentCount);
            var root = ToFloat2(authoring.InitialRootPosition);
            var restLength = math.max(0.001f, authoring.RestLength);

            AddComponent(entity, new VerletChain
            {
                RestLength = restLength,
                Damping = math.clamp(authoring.Damping, 0f, 1f),
                Gravity = ToFloat2(authoring.Gravity),
                RootBobAmplitude = math.max(0f, authoring.RootBobAmplitude),
                RootBobFrequency = math.max(0f, authoring.RootBobFrequency),
            });
            AddComponent(entity, new CreatureBody { RootPosition = root });
            AddComponent<CreatureLocomotion>(entity);

            var points = AddBuffer<VerletPoint>(entity);
            for (var index = 0; index < segmentCount; index++)
            {
                var position = CreatureLayout.PointPosition(root, restLength, index);
                points.Add(new VerletPoint { Position = position, PreviousPosition = position });
            }
        }

        static float2 ToFloat2(Vector2 value) => new(value.x, value.y);
    }

    sealed class MusclesBaker : Baker<MusclesAuthoring>
    {
        public override void Bake(MusclesAuthoring authoring)
        {
            var chain = GetComponent<VerletChainAuthoring>();
            if (chain == null)
                return;

            // Seed the target on the tip's own rest position so an unwritten target is a no-op
            // rather than a yank toward the world origin on the first tick.
            var root = new float2(chain.InitialRootPosition.x, chain.InitialRootPosition.y);
            var restLength = math.max(0.001f, chain.RestLength);
            var tipIndex = math.max(2, chain.ChainSegmentCount) - 1;

            AddComponent(GetEntity(TransformUsageFlags.None), new ChainTarget
            {
                Position = CreatureLayout.PointPosition(root, restLength, tipIndex),
                Strength = math.saturate(authoring.Strength),
            });
        }
    }

    sealed class LegsBaker : Baker<LegsAuthoring>
    {
        public override void Bake(LegsAuthoring authoring)
        {
            var chain = GetComponent<VerletChainAuthoring>();
            if (chain == null)
                return;

            var entity = GetEntity(TransformUsageFlags.None);
            var legs = authoring.Legs ?? Array.Empty<LegsAuthoring.LegRecipe>();
            var limbs = AddBuffer<Limb2BoneLeg>(entity);

            for (var index = 0; index < legs.Length; index++)
            {
                var recipe = legs[index];
                var attachmentIndex = CreatureBakerMath.AttachmentPointIndex(chain, recipe.AttachmentPointIndex);

                limbs.Add(new Limb2BoneLeg
                {
                    RootPointIndex = attachmentIndex,
                    Limb = new Limb2Bone
                    {
                        Target = CreatureBakerMath.RestFoot(chain, attachmentIndex, recipe.HomeOffset),
                        LengthA = math.max(0.001f, recipe.LengthA),
                        LengthB = math.max(0.001f, recipe.LengthB),
                        BendSign = recipe.BendSign < 0f ? -1f : 1f,
                    },
                });
            }
        }
    }

    sealed class GaitBaker : Baker<GaitAuthoring>
    {
        public override void Bake(GaitAuthoring authoring)
        {
            var chain = GetComponent<VerletChainAuthoring>();
            var legsAuthoring = GetComponent<LegsAuthoring>();
            if (chain == null || legsAuthoring == null)
                return;

            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new Gait
            {
                Comfort = math.max(0f, authoring.Comfort),
                StepDuration = math.max(0.001f, authoring.StepDuration),
                StepLead = authoring.StepLead,
                StepHeight = math.max(0f, authoring.StepHeight),
                MinimumSupport = math.max(0f, authoring.MinimumSupport),
                MinimumForward = math.max(0f, authoring.MinimumForward),
            });

            // Leg count, home offsets, and partner pairing all come from the leg recipe, so the
            // gait buffer stays index-aligned with the limb buffer by construction.
            var legs = legsAuthoring.Legs ?? Array.Empty<LegsAuthoring.LegRecipe>();
            var gaitLegs = AddBuffer<GaitLeg>(entity);
            for (var index = 0; index < legs.Length; index++)
            {
                var recipe = legs[index];
                var attachmentIndex = CreatureBakerMath.AttachmentPointIndex(chain, recipe.AttachmentPointIndex);
                var homeOffset = new float2(recipe.HomeOffset.x, recipe.HomeOffset.y);

                gaitLegs.Add(new GaitLeg
                {
                    State = FootState.Planted,
                    Plant = CreatureBakerMath.RestFoot(chain, attachmentIndex, recipe.HomeOffset),
                    HomeOffset = homeOffset,
                    PartnerIndex = (index ^ 1) < legs.Length ? (sbyte)(index ^ 1) : (sbyte)-1,
                });
            }

            AddBuffer<FootholdCandidate>(entity);
        }
    }

    sealed class ContactPlanesBaker : Baker<ContactPlanesAuthoring>
    {
        public override void Bake(ContactPlanesAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var contacts = AddBuffer<ContactPlane>(entity);
            var recipes = authoring.ContactPlanes ?? Array.Empty<ContactPlanesAuthoring.ContactPlaneRecipe>();

            for (var index = 0; index < recipes.Length; index++)
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

    /// <summary>Rest-pose maths the leg and gait bakers must agree on.</summary>
    static class CreatureBakerMath
    {
        public static int AttachmentPointIndex(VerletChainAuthoring chain, int authored) =>
            math.clamp(authored, 0, math.max(2, chain.ChainSegmentCount) - 1);

        public static float2 RestFoot(VerletChainAuthoring chain, int attachmentIndex, Vector2 homeOffset)
        {
            var root = new float2(chain.InitialRootPosition.x, chain.InitialRootPosition.y);
            var restLength = math.max(0.001f, chain.RestLength);
            return CreatureLayout.PointPosition(root, restLength, attachmentIndex)
                + new float2(homeOffset.x, homeOffset.y);
        }
    }
}
