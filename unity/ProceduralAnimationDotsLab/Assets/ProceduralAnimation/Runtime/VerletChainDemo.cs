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

            chainLine.positionCount = points.Length;
            for (var index = 0; index < points.Length; index++)
                chainLine.SetPosition(index, new Vector3(points[index].Position.x, points[index].Position.y, 0f));

            targetMarker.positionCount = 2;
            targetMarker.SetPosition(0, new Vector3(target.x - 0.16f, target.y, 0f));
            targetMarker.SetPosition(1, new Vector3(target.x + 0.16f, target.y, 0f));
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
                ComponentType.ReadWrite<ChainTarget>());
            entityManager.SetComponentData(entity, new VerletChain
            {
                LinkLength = 0.48f,
                Damping = 0.992f,
                MuscleStrength = 0.08f,
            });

            var points = entityManager.GetBuffer<VerletPoint>(entity);
            for (var index = 0; index < 16; index++)
            {
                var position = new float2(-3.5f + index * 0.48f, 0.5f);
                points.Add(new VerletPoint { Position = position, PreviousPosition = position });
            }

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
