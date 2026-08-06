using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralAnimationDotsLab
{
    public sealed class VerletChainDemo : MonoBehaviour
    {
        EntityManager entityManager;
        EntityQuery chainQuery;
        LineRenderer chainLine;
        LineRenderer targetMarker;
        LineRenderer[] legLines;
        LineRenderer[] footTargetMarkers;
        bool isReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateDemo()
        {
            if (FindAnyObjectByType<VerletChainDemo>() == null)
                new GameObject("Verlet Chain Demo").AddComponent<VerletChainDemo>();
        }

        void Start()
        {
            ConfigureCamera();
            chainLine = CreateLine("Chain", new Color(0.4f, 0.9f, 1f), 0.13f);
            targetMarker = CreateLine("Muscle Target", new Color(1f, 0.65f, 0.25f), 0.06f);
            legLines = new[]
            {
                CreateLine("Left Leg", new Color(0.96f, 0.4f, 0.65f), 0.11f),
                CreateLine("Right Leg", new Color(0.63f, 0.53f, 1f), 0.11f),
            };
            footTargetMarkers = new[]
            {
                CreateLine("Left Foot Target", new Color(0.98f, 0.86f, 0.4f), 0.06f),
                CreateLine("Right Foot Target", new Color(0.98f, 0.86f, 0.4f), 0.06f),
            };
            CreateChainWhenWorldIsReady();
        }

        void Update()
        {
            if (!isReady)
            {
                CreateChainWhenWorldIsReady();
                return;
            }

            if (chainQuery.IsEmptyIgnoreFilter)
                return;

            var entity = chainQuery.GetSingletonEntity();
            var points = entityManager.GetBuffer<VerletPoint>(entity, true);
            var target = entityManager.GetComponentData<ChainTarget>(entity).Position;
            var limbs = entityManager.GetBuffer<Limb2BoneLeg>(entity, true);
            var gaitLegs = entityManager.GetBuffer<GaitLeg>(entity, true);

            chainLine.positionCount = points.Length;
            for (var index = 0; index < points.Length; index++)
                chainLine.SetPosition(index, new Vector3(points[index].Position.x, points[index].Position.y, 0f));

            targetMarker.positionCount = 2;
            targetMarker.SetPosition(0, new Vector3(target.x - 0.16f, target.y, 0f));
            targetMarker.SetPosition(1, new Vector3(target.x + 0.16f, target.y, 0f));

            var legCount = math.min(limbs.Length, legLines.Length);
            for (var index = 0; index < legCount; index++)
            {
                var limb = limbs[index].Limb;
                var isSwinging = index < gaitLegs.Length && gaitLegs[index].State == FootState.Swinging;
                var legColor = isSwinging ? new Color(1f, 0.7f, 0.25f) : index == 0 ? new Color(0.96f, 0.4f, 0.65f) : new Color(0.63f, 0.53f, 1f);
                legLines[index].startColor = legColor;
                legLines[index].endColor = legColor;
                legLines[index].positionCount = 3;
                legLines[index].SetPosition(0, new Vector3(limb.Root.x, limb.Root.y, 0f));
                legLines[index].SetPosition(1, new Vector3(limb.Knee.x, limb.Knee.y, 0f));
                legLines[index].SetPosition(2, new Vector3(limb.Foot.x, limb.Foot.y, 0f));

                footTargetMarkers[index].positionCount = 2;
                footTargetMarkers[index].SetPosition(0, new Vector3(limb.Target.x - 0.18f, limb.Target.y, 0f));
                footTargetMarkers[index].SetPosition(1, new Vector3(limb.Target.x + 0.18f, limb.Target.y, 0f));
            }
        }

        void CreateChainWhenWorldIsReady()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return;

            entityManager = world.EntityManager;
            chainQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<VerletChain>(),
                ComponentType.ReadOnly<VerletPoint>(),
                ComponentType.ReadOnly<ChainTarget>());

            if (!chainQuery.IsEmptyIgnoreFilter)
            {
                isReady = true;
                return;
            }

            var entity = entityManager.CreateEntity(
                ComponentType.ReadWrite<VerletChain>(),
                ComponentType.ReadWrite<VerletPoint>(),
                ComponentType.ReadWrite<ChainTarget>(),
                ComponentType.ReadWrite<Limb2BoneLeg>(),
                ComponentType.ReadWrite<GaitLeg>(),
                ComponentType.ReadWrite<GaitSettings>());
            entityManager.SetComponentData(entity, new VerletChain
            {
                LinkLength = 0.48f,
                Damping = 0.992f,
                MuscleStrength = 0.08f,
            });
            entityManager.SetComponentData(entity, new GaitSettings
            {
                Comfort = 0.32f,
                StepDuration = 0.34f,
                StepLead = 0.12f,
                StepHeight = 0.42f,
            });

            var points = entityManager.GetBuffer<VerletPoint>(entity);
            for (var index = 0; index < 16; index++)
            {
                var position = new float2(-3.5f + index * 0.48f, 0.5f);
                points.Add(new VerletPoint { Position = position, PreviousPosition = position });
            }

            var limbs = entityManager.GetBuffer<Limb2BoneLeg>(entity);
            limbs.Add(new Limb2BoneLeg
            {
                Limb = new Limb2Bone
                {
                    Target = new float2(-1.3f, -2.1f),
                    LengthA = 1.2f,
                    LengthB = 1.45f,
                    BendSign = -1f,
                },
                RootPointIndex = 5,
            });
            limbs.Add(new Limb2BoneLeg
            {
                Limb = new Limb2Bone
                {
                    Target = new float2(1.5f, -2.1f),
                    LengthA = 1.2f,
                    LengthB = 1.45f,
                    BendSign = 1f,
                },
                RootPointIndex = 10,
            });

            var gaitLegs = entityManager.GetBuffer<GaitLeg>(entity);
            gaitLegs.Add(new GaitLeg
            {
                State = FootState.Planted,
                Plant = new float2(-1.3f, -2.1f),
                HomeOffset = new float2(-0.2f, -2.6f),
                PartnerIndex = 1,
            });
            gaitLegs.Add(new GaitLeg
            {
                State = FootState.Planted,
                Plant = new float2(1.5f, -2.1f),
                HomeOffset = new float2(0.2f, -2.6f),
                PartnerIndex = 0,
            });

            isReady = true;
        }

        static LineRenderer CreateLine(string lineName, Color color, float width)
        {
            var line = new GameObject(lineName).AddComponent<LineRenderer>();
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
            line.numCornerVertices = 6;
            line.numCapVertices = 6;
            line.useWorldSpace = true;
            return line;
        }

        static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Demo Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
            }

            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.backgroundColor = new Color(0.06f, 0.09f, 0.14f);
        }
    }
}
