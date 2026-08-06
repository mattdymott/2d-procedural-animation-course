using System;
using System.Reflection;
using NUnit.Framework;
using Tealeaf.ProceduralAnimation.Dots;
using Unity.Core;
using Unity.Entities;
using UnityEngine;

namespace ProceduralAnimationPackageConsumer.Tests
{
    public sealed class PackageConsumerBakingTests
    {
        [Test]
        public void BakeAndTick_UsesOnlyPackageAuthoringLocomotionAndWorldFacts()
        {
            var creature = new GameObject("Package Consumer Creature");
            var ground = new GameObject("Package Consumer Ground");
            try
            {
                var recipe = creature.AddComponent<ProceduralCreatureAuthoring>();
                recipe.ChainSegmentCount = 4;
                recipe.InitialRootPosition = new Vector2(-3f, 0f);
                recipe.Legs = new[]
                {
                    new ProceduralCreatureAuthoring.LegRecipe { AttachmentPointIndex = 1, LengthA = 1f, LengthB = 1f, BendSign = -1f, HomeOffset = new Vector2(-0.3f, -1.5f) },
                    new ProceduralCreatureAuthoring.LegRecipe { AttachmentPointIndex = 3, LengthA = 1f, LengthB = 1f, BendSign = 1f, HomeOffset = new Vector2(0.3f, -1.5f) },
                };
                creature.AddComponent<SampleCreaturePatrolAuthoring>().Speed = 1f;
                ground.AddComponent<SampleFlatGroundAuthoring>().Height = -2.75f;

                using var world = Bake(creature, ground);
                var entity = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<VerletChain>()).GetSingletonEntity();

                Assert.That(world.EntityManager.HasComponent<CreatureLocomotion>(entity), Is.True);
                Assert.That(world.EntityManager.HasComponent<SampleCreaturePatrol>(entity), Is.True);

                world.SetTime(new TimeData(0.02, 0.02f));
                world.GetOrCreateSystem<SampleCreaturePatrolSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<FlatGroundFootholdAdapterSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystemManaged<ProceduralAnimationSolveSystemGroup>().Update();

                Assert.That(world.EntityManager.GetComponentData<CreatureBody>(entity).RootPosition.x, Is.GreaterThan(-3f));
                var footholds = world.EntityManager.GetBuffer<FootholdCandidate>(entity);
                Assert.That(footholds.Length, Is.EqualTo(2));
                Assert.That(footholds[0].Point.y, Is.EqualTo(-2.75f));
                Assert.That(footholds[1].Point.y, Is.EqualTo(-2.75f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(creature);
                UnityEngine.Object.DestroyImmediate(ground);
            }
        }

        static World Bake(params GameObject[] authoring)
        {
            var world = new World("PackageConsumerBakingTests");
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
