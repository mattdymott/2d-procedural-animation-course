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

        static GameObject BuildCreature()
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
            creature.AddComponent<LabTerrainAdapterAuthoring>();
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
