using System;
using System.Reflection;
using NUnit.Framework;
using Tealeaf.ProceduralAnimation.Dots;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralAnimationDotsLab.Tests
{
    /// <summary>
    /// Traces the lesson sample through the package's public interface: the creature is
    /// composed from <see cref="VerletChainAuthoring"/>, <see cref="LegsAuthoring"/>, and
    /// <see cref="GaitAuthoring"/>, the sample supplies locomotion and world facts, and the
    /// package solve group owns everything after that.
    /// </summary>
    public sealed class LabSampleBakingTests
    {
        [Test]
        public void BakeAndTick_ServesTheMovingSupportAndTheLessonRampFromSampleAdapters()
        {
            var creature = BuildCreature();
            var support = BuildMovingSupport();
            try
            {
                using var world = Bake(creature, support);
                var entity = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<VerletChain>()).GetSingletonEntity();
                var supportEntity = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<DemoMovingSupport>()).GetSingletonEntity();

                world.SetTime(new TimeData(0.02, 0.02f));
                world.GetOrCreateSystem<MovingSupportSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<LabCreaturePatrolSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<GroundQuerySystem>().Update(world.Unmanaged);
                world.GetOrCreateSystemManaged<ProceduralAnimationSolveSystemGroup>().Update();

                var candidates = world.EntityManager.GetBuffer<FootholdCandidate>(entity);
                Assert.That(candidates.Length, Is.EqualTo(2));

                // The near leg probes over the elevator, so its evidence is support-relative.
                Assert.That(candidates[0].Support, Is.EqualTo(supportEntity));
                Assert.That(candidates[0].SupportLocalPoint.y, Is.EqualTo(0f).Within(0.0001f));

                // The far leg probes past the elevator and lands on the lesson ramp instead.
                Assert.That(candidates[1].Support, Is.EqualTo(Entity.Null));
                Assert.That(candidates[1].Point.y, Is.EqualTo(-1.89f).Within(0.0001f));

                // Probe markers are sample-side debug data, never package output.
                Assert.That(world.EntityManager.GetBuffer<GroundQueryDebugHit>(entity).Length, Is.EqualTo(2));

                // The package owns the body root; the sample only asked it to walk right.
                Assert.That(world.EntityManager.GetComponentData<CreatureLocomotion>(entity).DesiredVelocity.x, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(world.EntityManager.GetComponentData<CreatureBody>(entity).RootPosition.x, Is.GreaterThan(-3.5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(creature);
                UnityEngine.Object.DestroyImmediate(support);
            }
        }

        /// <summary>
        /// Debug data must never gate the simulation. The probe-marker buffer is authored by an
        /// optional component, so a creature without it has to receive exactly the same footholds
        /// — asking for that buffer in the adapter's query tuple would silently stop it walking.
        /// </summary>
        [Test]
        public void ACreatureWithoutTheDebugBufferReceivesTheSameFootholds()
        {
            var withDebug = BuildCreature(recordProbes: true);
            var withoutDebug = BuildCreature(recordProbes: false);
            var supportA = BuildMovingSupport();
            var supportB = BuildMovingSupport();
            try
            {
                using var worldA = Bake(withDebug, supportA);
                using var worldB = Bake(withoutDebug, supportB);

                var candidatesA = ServeFootholds(worldA);
                var candidatesB = ServeFootholds(worldB);

                // Both empty would satisfy the comparison below without proving anything.
                Assert.That(candidatesA.Length, Is.EqualTo(2), "The adapter served no footholds at all.");
                Assert.That(candidatesB.Length, Is.EqualTo(candidatesA.Length),
                    "The optional debug buffer changed how many footholds the adapter served.");
                for (var index = 0; index < candidatesA.Length; index++)
                {
                    Assert.That(candidatesB[index].Point.x, Is.EqualTo(candidatesA[index].Point.x).Within(0.0001f));
                    Assert.That(candidatesB[index].Point.y, Is.EqualTo(candidatesA[index].Point.y).Within(0.0001f));
                    Assert.That(candidatesB[index].LegIndex, Is.EqualTo(candidatesA[index].LegIndex));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(withDebug);
                UnityEngine.Object.DestroyImmediate(withoutDebug);
                UnityEngine.Object.DestroyImmediate(supportA);
                UnityEngine.Object.DestroyImmediate(supportB);
            }
        }

        /// <summary>
        /// The package's gait buffers do not identify a creature as this sample's. A top-down
        /// creature carries every one of them and is served by its own adapter, so this one has to
        /// leave anything it was not opted into alone — overwriting it would freeze that creature's
        /// feet with side-view ground it can never reach.
        /// </summary>
        [Test]
        public void ACreatureWithoutTheAdapterMarkerIsLeftAlone()
        {
            var creature = BuildCreature();
            var stranger = BuildCreature();
            UnityEngine.Object.DestroyImmediate(stranger.GetComponent<LabTerrainAdapterAuthoring>());
            try
            {
                using var world = Bake(creature, stranger, BuildMovingSupport());
                var manager = world.EntityManager;

                var entities = manager.CreateEntityQuery(ComponentType.ReadOnly<VerletChain>())
                    .ToEntityArray(Unity.Collections.Allocator.Temp);
                var unmarked = Entity.Null;
                for (var index = 0; index < entities.Length; index++)
                {
                    if (!manager.HasComponent<LabTerrainAdapter>(entities[index]))
                        unmarked = entities[index];
                }

                Assert.That(unmarked, Is.Not.EqualTo(Entity.Null), "The fixture must contain an unmarked creature.");

                // A sentinel only this test could have written.
                var sentinel = new FootholdCandidate { LegIndex = 9, Point = new float2(-99f, -99f) };
                manager.GetBuffer<FootholdCandidate>(unmarked).Add(sentinel);

                world.SetTime(new TimeData(0.02, 0.02f));
                world.GetOrCreateSystem<MovingSupportSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<GroundQuerySystem>().Update(world.Unmanaged);

                var candidates = manager.GetBuffer<FootholdCandidate>(unmarked);
                Assert.That(candidates.Length, Is.EqualTo(1), "The adapter served a creature it was never opted into.");
                Assert.That(candidates[0].Point.x, Is.EqualTo(-99f).Within(0.0001f));
                entities.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(creature);
                UnityEngine.Object.DestroyImmediate(stranger);
            }
        }

        static DynamicBuffer<FootholdCandidate> ServeFootholds(World world)
        {
            var entity = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<VerletChain>()).GetSingletonEntity();
            world.SetTime(new TimeData(0.02, 0.02f));
            world.GetOrCreateSystem<MovingSupportSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<LabCreaturePatrolSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<GroundQuerySystem>().Update(world.Unmanaged);
            return world.EntityManager.GetBuffer<FootholdCandidate>(entity);
        }

        [Test]
        public void GroundQuery_RejectsAProbeBeyondTheSupportEdge()
        {
            var pose = new SupportPose { Position = new float2(-1.1f, -1.75f) };

            var found = GroundQuery.TrySampleSupport(
                0,
                new float2(1.5f, -2.1f),
                new Entity { Index = 3, Version = 1 },
                pose,
                out _);

            Assert.That(found, Is.False);
        }

        static GameObject BuildCreature(bool recordProbes = true)
        {
            var creature = new GameObject("Lab Creature");
            var chain = creature.AddComponent<VerletChainAuthoring>();
            chain.ChainSegmentCount = 16;
            chain.InitialRootPosition = new Vector2(-3.5f, 0.5f);
            chain.RestLength = 0.48f;
            chain.RootBobAmplitude = 0.35f;
            chain.RootBobFrequency = 0.9f;
            creature.AddComponent<MusclesAuthoring>().Strength = 0.08f;
            creature.AddComponent<LegsAuthoring>().Legs = new[]
            {
                new LegsAuthoring.LegRecipe { AttachmentPointIndex = 5, LengthA = 1.2f, LengthB = 1.45f, BendSign = -1f, HomeOffset = new Vector2(-0.2f, -2.6f) },
                new LegsAuthoring.LegRecipe { AttachmentPointIndex = 10, LengthA = 1.2f, LengthB = 1.45f, BendSign = 1f, HomeOffset = new Vector2(0.2f, -2.6f) },
            };
            creature.AddComponent<GaitAuthoring>();
            creature.AddComponent<LabCreaturePatrolAuthoring>();
            creature.AddComponent<LabTerrainAdapterAuthoring>().RecordProbes = recordProbes;
            return creature;
        }

        static GameObject BuildMovingSupport()
        {
            var support = new GameObject("Lab Moving Support");
            support.AddComponent<LabMovingSupportAuthoring>();
            return support;
        }

        static World Bake(params GameObject[] authoring)
        {
            var world = new World("LabSampleBakingTests");
            var hybridAssembly = Assembly.Load("Unity.Entities.Hybrid");
            var bakingSettingsType = hybridAssembly.GetType("Unity.Entities.BakingSettings", throwOnError: true);
            var bakingUtilityType = hybridAssembly.GetType("Unity.Entities.BakingUtility", throwOnError: true);
            var bakeGameObjects = bakingUtilityType.GetMethod("BakeGameObjects", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(bakeGameObjects, Is.Not.Null, "Unity.Entities must expose its baking pipeline to editor tests.");
            bakeGameObjects.Invoke(null, new object[] { world, authoring, Activator.CreateInstance(bakingSettingsType) });
            return world;
        }
    }
}
