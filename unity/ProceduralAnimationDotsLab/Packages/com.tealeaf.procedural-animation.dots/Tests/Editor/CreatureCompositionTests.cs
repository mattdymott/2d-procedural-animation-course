using System;
using System.Reflection;
using NUnit.Framework;
using Tealeaf.ProceduralAnimation.Dots;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Tealeaf.ProceduralAnimation.Dots.Tests
{
    /// <summary>
    /// The creature is whichever components it carries. These tests compose the package's
    /// authoring components in different combinations and check that each entity ends up with
    /// exactly the features that were asked for — and that the solve group runs either way.
    /// </summary>
    public sealed class CreatureCompositionTests
    {
        [Test]
        public void BakeAndTick_ComposesTheFullWalkingCreature()
        {
            var gameObject = new GameObject("Authored Creature");
            try
            {
                var chain = gameObject.AddComponent<VerletChainAuthoring>();
                chain.ChainSegmentCount = 4;
                chain.InitialRootPosition = new Vector2(3f, -1f);
                chain.RestLength = 0.75f;
                chain.Damping = 0.9f;
                gameObject.AddComponent<MusclesAuthoring>().Strength = 0.1f;

                gameObject.AddComponent<LegsAuthoring>().Legs = new[]
                {
                    new LegsAuthoring.LegRecipe
                    {
                        AttachmentPointIndex = 1, LengthA = 1f, LengthB = 1.2f,
                        BendSign = -1f, HomeOffset = new Vector2(-0.25f, -1.5f),
                    },
                    new LegsAuthoring.LegRecipe
                    {
                        AttachmentPointIndex = 3, LengthA = 1.1f, LengthB = 1.3f,
                        BendSign = 1f, HomeOffset = new Vector2(0.25f, -1.5f),
                    },
                };
                gameObject.AddComponent<GaitAuthoring>();
                gameObject.AddComponent<ContactPlanesAuthoring>().ContactPlanes = new[]
                {
                    new ContactPlanesAuthoring.ContactPlaneRecipe
                    {
                        Point = new Vector2(0f, -3f), Normal = Vector2.up,
                        Radius = 0.1f, Friction = 0.25f,
                    },
                };

                using var world = Bake(gameObject);
                var entity = SingleCreature(world);
                var manager = world.EntityManager;

                Assert.That(manager.GetBuffer<VerletPoint>(entity).Length, Is.EqualTo(4));
                Assert.That(manager.GetBuffer<Limb2BoneLeg>(entity).Length, Is.EqualTo(2));
                Assert.That(manager.GetBuffer<GaitLeg>(entity).Length, Is.EqualTo(2));
                Assert.That(manager.GetBuffer<ContactPlane>(entity).Length, Is.EqualTo(1));
                Assert.That(manager.GetBuffer<FootholdCandidate>(entity).Length, Is.EqualTo(0));
                Assert.That(manager.HasComponent<CreatureLocomotion>(entity), Is.True,
                    "The chain authoring must bake package-owned locomotion input for consumers.");

                var firstPoint = manager.GetBuffer<VerletPoint>(entity)[0];
                Assert.That(firstPoint.Position, Is.EqualTo(new float2(3f, -1f)));
                Assert.That(firstPoint.PreviousPosition, Is.EqualTo(firstPoint.Position));

                Tick(world);

                Assert.That(manager.GetComponentData<VerletChain>(entity).Time, Is.EqualTo(0.02f).Within(0.0001f));
                Assert.That(math.length(manager.GetBuffer<Limb2BoneLeg>(entity)[0].Limb.Foot), Is.GreaterThan(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void BakeAndTick_ComposesAChainWithNoLegsGaitOrContacts()
        {
            var gameObject = new GameObject("Rope");
            try
            {
                var chain = gameObject.AddComponent<VerletChainAuthoring>();
                chain.ChainSegmentCount = 6;
                chain.InitialRootPosition = new Vector2(0f, 0f);
                chain.RestLength = 0.5f;

                using var world = Bake(gameObject);
                var entity = SingleCreature(world);
                var manager = world.EntityManager;

                Assert.That(manager.HasBuffer<Limb2BoneLeg>(entity), Is.False, "A chain alone must not carry limbs.");
                Assert.That(manager.HasComponent<Gait>(entity), Is.False, "A chain alone must not carry gait.");
                Assert.That(manager.HasBuffer<GaitLeg>(entity), Is.False);
                Assert.That(manager.HasBuffer<FootholdCandidate>(entity), Is.False);
                Assert.That(manager.HasBuffer<ContactPlane>(entity), Is.False, "Contact planes are opt-in.");

                Tick(world);

                var points = manager.GetBuffer<VerletPoint>(entity);
                Assert.That(points.Length, Is.EqualTo(6));
                Assert.That(points[0].Position, Is.EqualTo(float2.zero),
                    "With no authored bob the root pins exactly to the body position.");
                Assert.That(points[5].Position.y, Is.LessThan(0f),
                    "The chain must still simulate: gravity should have pulled the free end down.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void HardResolve_TreatsAnAbsentContactBufferLikeAnEmptyOne()
        {
            // Contact planes became opt-in when the recipe was split, so the final constraint pass
            // has to reach creatures that carry no ContactPlane buffer at all. Same chain, same
            // tick, with and without the buffer: the resolved points must agree exactly.
            var withoutBuffer = BuildChain("Chain Without Contacts", addEmptyContacts: false);
            var withEmptyBuffer = BuildChain("Chain With Empty Contacts", addEmptyContacts: true);
            try
            {
                using var worldA = Bake(withoutBuffer);
                using var worldB = Bake(withEmptyBuffer);
                Tick(worldA);
                Tick(worldB);

                var a = worldA.EntityManager.GetBuffer<VerletPoint>(SingleCreature(worldA));
                var b = worldB.EntityManager.GetBuffer<VerletPoint>(SingleCreature(worldB));

                Assert.That(worldB.EntityManager.HasBuffer<ContactPlane>(SingleCreature(worldB)), Is.True);
                Assert.That(worldA.EntityManager.HasBuffer<ContactPlane>(SingleCreature(worldA)), Is.False);
                Assert.That(a.Length, Is.EqualTo(b.Length));
                for (var index = 0; index < a.Length; index++)
                {
                    Assert.That(math.distance(a[index].Position, b[index].Position), Is.LessThan(0.000001f),
                        $"Point {index} diverged when the contact buffer was absent.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(withoutBuffer);
                UnityEngine.Object.DestroyImmediate(withEmptyBuffer);
            }
        }

        [Test]
        public void ZeroGravityChainWithNoMusclesStaysExactlyAtRest()
        {
            // Nothing in the package moves a chain unless the author asked for it: no built-in
            // gravity, no built-in bob, no built-in target. This is the assertion that proves
            // the old hardcoded constants are actually gone.
            var gameObject = new GameObject("Weightless Rope");
            try
            {
                var chain = gameObject.AddComponent<VerletChainAuthoring>();
                chain.ChainSegmentCount = 6;
                chain.InitialRootPosition = new Vector2(2f, 1f);
                chain.RestLength = 0.5f;
                chain.Gravity = Vector2.zero;

                using var world = Bake(gameObject);
                var entity = SingleCreature(world);
                var rest = new float2[6];
                var baked = world.EntityManager.GetBuffer<VerletPoint>(entity);
                for (var index = 0; index < baked.Length; index++) rest[index] = baked[index].Position;

                for (var step = 0; step < 12; step++) Tick(world);

                var points = world.EntityManager.GetBuffer<VerletPoint>(entity);
                for (var index = 0; index < points.Length; index++)
                {
                    Assert.That(math.distance(points[index].Position, rest[index]), Is.LessThan(0.0001f),
                        $"Point {index} moved on its own with zero gravity and no muscles.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void MusclesDrawTheTipTowardTheTargetTheConsumerWrites()
        {
            var gameObject = new GameObject("Tentacle");
            try
            {
                var chain = gameObject.AddComponent<VerletChainAuthoring>();
                chain.ChainSegmentCount = 6;
                chain.InitialRootPosition = Vector2.zero;
                chain.RestLength = 0.5f;
                chain.Gravity = Vector2.zero;
                gameObject.AddComponent<MusclesAuthoring>().Strength = 0.5f;

                using var world = Bake(gameObject);
                var entity = SingleCreature(world);
                var manager = world.EntityManager;

                Assert.That(manager.HasComponent<ChainTarget>(entity), Is.True);

                // The consumer owns the target; the package must never overwrite it.
                var aim = new float2(0f, 2.5f);
                manager.SetComponentData(entity, new ChainTarget { Position = aim, Strength = 0.5f });
                var before = manager.GetBuffer<VerletPoint>(entity)[5].Position;

                for (var step = 0; step < 8; step++) Tick(world);

                var after = manager.GetBuffer<VerletPoint>(entity)[5].Position;
                Assert.That(manager.GetComponentData<ChainTarget>(entity).Position, Is.EqualTo(aim),
                    "The package must not write ChainTarget.");
                Assert.That(math.distance(after, aim), Is.LessThan(math.distance(before, aim)),
                    "Muscles must draw the tip toward the target.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AWalkingChainWithoutMusclesIsNotDraggedTowardItsBakeTimeTip()
        {
            // The reason muscles are opt-in rather than always-baked: a target nobody writes
            // would anchor the tip to a stale world point and hold the creature back.
            var gameObject = new GameObject("Walker");
            try
            {
                var chain = gameObject.AddComponent<VerletChainAuthoring>();
                chain.ChainSegmentCount = 6;
                chain.InitialRootPosition = Vector2.zero;
                chain.RestLength = 0.5f;
                chain.Gravity = Vector2.zero;

                using var world = Bake(gameObject);
                var entity = SingleCreature(world);
                var manager = world.EntityManager;
                var bakedTip = manager.GetBuffer<VerletPoint>(entity)[5].Position;

                manager.SetComponentData(entity, new CreatureLocomotion { DesiredVelocity = new float2(4f, 0f) });
                for (var step = 0; step < 25; step++)
                {
                    manager.SetComponentData(entity, new CreatureLocomotion { DesiredVelocity = new float2(4f, 0f) });
                    Tick(world);
                }

                var root = manager.GetComponentData<CreatureBody>(entity).RootPosition;
                var tip = manager.GetBuffer<VerletPoint>(entity)[5].Position;

                Assert.That(root.x, Is.GreaterThan(1f), "The body should have walked.");
                Assert.That(tip.x, Is.GreaterThan(bakedTip.x + 1f),
                    "The tip must travel with the body, not stay anchored to its bake-time position.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        static GameObject BuildChain(string name, bool addEmptyContacts)
        {
            var gameObject = new GameObject(name);
            var chain = gameObject.AddComponent<VerletChainAuthoring>();
            chain.ChainSegmentCount = 6;
            chain.InitialRootPosition = new Vector2(0f, 0f);
            chain.RestLength = 0.5f;
            if (addEmptyContacts)
                gameObject.AddComponent<ContactPlanesAuthoring>().ContactPlanes = Array.Empty<ContactPlanesAuthoring.ContactPlaneRecipe>();
            return gameObject;
        }

        [Test]
        public void BakeAndTick_ComposesLegsWithoutGaitSoTheConsumerAimsThem()
        {
            var gameObject = new GameObject("Reaching Limbs");
            try
            {
                var chain = gameObject.AddComponent<VerletChainAuthoring>();
                chain.ChainSegmentCount = 4;
                chain.InitialRootPosition = Vector2.zero;
                chain.RestLength = 1f;

                gameObject.AddComponent<LegsAuthoring>().Legs = new[]
                {
                    new LegsAuthoring.LegRecipe
                    {
                        AttachmentPointIndex = 2, LengthA = 1f, LengthB = 1f,
                        BendSign = 1f, HomeOffset = new Vector2(0f, -1.5f),
                    },
                };

                using var world = Bake(gameObject);
                var entity = SingleCreature(world);
                var manager = world.EntityManager;

                Assert.That(manager.GetBuffer<Limb2BoneLeg>(entity).Length, Is.EqualTo(1));
                Assert.That(manager.HasComponent<Gait>(entity), Is.False);

                // With no gait, the consumer owns the limb target; IK still resolves it.
                var limbs = manager.GetBuffer<Limb2BoneLeg>(entity);
                var limb = limbs[0];
                limb.Limb.Target = new float2(2f, -1f);
                limbs[0] = limb;

                Tick(world);

                var solved = manager.GetBuffer<Limb2BoneLeg>(entity)[0].Limb;
                Assert.That(math.distance(solved.Root, solved.Knee), Is.EqualTo(1f).Within(0.001f));
                Assert.That(math.distance(solved.Knee, solved.Foot), Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void GaitBaker_PairsLegsForAlternationFromTheLegRecipe()
        {
            var gameObject = new GameObject("Four Legs");
            try
            {
                gameObject.AddComponent<VerletChainAuthoring>().ChainSegmentCount = 8;
                var legs = new LegsAuthoring.LegRecipe[4];
                for (var index = 0; index < legs.Length; index++)
                {
                    legs[index] = new LegsAuthoring.LegRecipe
                    {
                        AttachmentPointIndex = index + 1, LengthA = 1f, LengthB = 1f,
                        BendSign = 1f, HomeOffset = new Vector2(0f, -1.5f),
                    };
                }
                gameObject.AddComponent<LegsAuthoring>().Legs = legs;
                gameObject.AddComponent<GaitAuthoring>();

                using var world = Bake(gameObject);
                var entity = SingleCreature(world);
                var gaitLegs = world.EntityManager.GetBuffer<GaitLeg>(entity);
                var limbs = world.EntityManager.GetBuffer<Limb2BoneLeg>(entity);

                Assert.That(gaitLegs.Length, Is.EqualTo(limbs.Length),
                    "The gait and limb buffers must stay index-aligned.");
                Assert.That(gaitLegs[0].PartnerIndex, Is.EqualTo(1));
                Assert.That(gaitLegs[1].PartnerIndex, Is.EqualTo(0));
                Assert.That(gaitLegs[2].PartnerIndex, Is.EqualTo(3));
                Assert.That(gaitLegs[3].PartnerIndex, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void AddingGaitPullsInTheLegsAndChainItDependsOn()
        {
            // The pit of success: the deepest component alone yields a complete creature.
            // [RequireComponent] has to resolve transitively for that claim to hold.
            var gameObject = new GameObject("Gait Only");
            try
            {
                gameObject.AddComponent<GaitAuthoring>();

                Assert.That(gameObject.GetComponent<LegsAuthoring>(), Is.Not.Null,
                    "GaitAuthoring must pull in the legs it steps with.");
                Assert.That(gameObject.GetComponent<VerletChainAuthoring>(), Is.Not.Null,
                    "GaitAuthoring must pull in the chain its legs hang from, transitively.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        static Entity SingleCreature(World world) =>
            world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<VerletChain>()).GetSingletonEntity();

        static void Tick(World world)
        {
            world.SetTime(new TimeData(0.02, 0.02f));
            world.GetOrCreateSystemManaged<ProceduralAnimationSolveSystemGroup>().Update();
        }

        static World Bake(GameObject authoring)
        {
            var world = new World("CreatureCompositionTests");
            var hybridAssembly = Assembly.Load("Unity.Entities.Hybrid");
            var bakingSettingsType = hybridAssembly.GetType("Unity.Entities.BakingSettings", throwOnError: true);
            var bakingUtilityType = hybridAssembly.GetType("Unity.Entities.BakingUtility", throwOnError: true);
            var bakeGameObjects = bakingUtilityType.GetMethod("BakeGameObjects", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(bakeGameObjects, Is.Not.Null, "Unity.Entities must expose its baking pipeline to editor tests.");
            bakeGameObjects.Invoke(null, new[] { world, new[] { authoring }, Activator.CreateInstance(bakingSettingsType) });
            return world;
        }
    }
}
