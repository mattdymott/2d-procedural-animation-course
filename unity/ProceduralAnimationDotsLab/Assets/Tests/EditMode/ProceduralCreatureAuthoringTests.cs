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
    public sealed class ProceduralCreatureAuthoringTests
    {
        [Test]
        public void BakeAndTick_CreatesACompleteCreatureFromTheStableRecipe()
        {
            var gameObject = new GameObject("Authored Creature");
            try
            {
                var authoring = gameObject.AddComponent<ProceduralCreatureAuthoring>();
                authoring.ChainSegmentCount = 4;
                authoring.InitialRootPosition = new Vector2(3f, -1f);
                authoring.LinkLength = 0.75f;
                authoring.Damping = 0.9f;
                authoring.MuscleStrength = 0.1f;
                authoring.Legs = new[]
                {
                    new ProceduralCreatureAuthoring.LegRecipe
                    {
                        AttachmentPointIndex = 1,
                        LengthA = 1f,
                        LengthB = 1.2f,
                        BendSign = -1f,
                        HomeOffset = new Vector2(-0.25f, -1.5f),
                    },
                    new ProceduralCreatureAuthoring.LegRecipe
                    {
                        AttachmentPointIndex = 3,
                        LengthA = 1.1f,
                        LengthB = 1.3f,
                        BendSign = 1f,
                        HomeOffset = new Vector2(0.25f, -1.5f),
                    },
                };
                authoring.ContactPlanes = new[]
                {
                    new ProceduralCreatureAuthoring.ContactPlaneRecipe
                    {
                        Point = new Vector2(0f, -3f),
                        Normal = Vector2.up,
                        Radius = 0.1f,
                        Friction = 0.25f,
                    },
                };

                using var world = Bake(gameObject);
                var entity = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<VerletChain>()).GetSingletonEntity();

                Assert.That(world.EntityManager.GetBuffer<VerletPoint>(entity).Length, Is.EqualTo(4));
                Assert.That(world.EntityManager.GetBuffer<Limb2BoneLeg>(entity).Length, Is.EqualTo(2));
                Assert.That(world.EntityManager.GetBuffer<GaitLeg>(entity).Length, Is.EqualTo(2));
                Assert.That(world.EntityManager.GetBuffer<ContactPlane>(entity).Length, Is.EqualTo(1));
                Assert.That(world.EntityManager.GetBuffer<FootholdCandidate>(entity).Length, Is.EqualTo(0));

                Assert.That(world.EntityManager.HasComponent<CreatureLocomotion>(entity), Is.True,
                    "The package front door must bake package-owned locomotion input for consumers.");

                var firstPoint = world.EntityManager.GetBuffer<VerletPoint>(entity)[0];
                Assert.That(firstPoint.Position, Is.EqualTo(new float2(3f, -1f)));
                Assert.That(firstPoint.PreviousPosition, Is.EqualTo(firstPoint.Position));

                world.SetTime(new TimeData(0.02, 0.02f));
                world.GetOrCreateSystemManaged<ProceduralAnimationSolveSystemGroup>().Update();

                Assert.That(world.EntityManager.GetComponentData<VerletChain>(entity).Time, Is.EqualTo(0.02f).Within(0.0001f));
                Assert.That(math.length(world.EntityManager.GetBuffer<Limb2BoneLeg>(entity)[0].Limb.Foot), Is.GreaterThan(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        static World Bake(GameObject authoring)
        {
            var world = new World("ProceduralCreatureAuthoringTests");
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
