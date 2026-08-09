using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TopDownLab
{
    sealed class TopDownIntentBaker : Baker<TopDownIntentAuthoring>
    {
        public override void Bake(TopDownIntentAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.None), new TopDownIntent
            {
                Centre = new float2(authoring.Centre.x, authoring.Centre.y),
                Radius = math.max(0.01f, authoring.Radius),
                Speed = math.max(0f, authoring.Speed),
                TurnRate = math.max(0f, authoring.TurnRate),
                RecoverySpeedScale = math.saturate(authoring.RecoverySpeedScale),
                RecoveryTurnRate = math.max(0f, authoring.RecoveryTurnRate),
            });
        }
    }

    sealed class PlanarIslandBaker : Baker<PlanarIslandAuthoring>
    {
        public override void Bake(PlanarIslandAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.None), new PlanarIsland
            {
                Centre = new float2(authoring.Centre.x, authoring.Centre.y),
                Radius = math.max(0f, authoring.Radius),
            });
        }
    }

    sealed class PlanarQueryDebugBaker : Baker<PlanarQueryDebugAuthoring>
    {
        public override void Bake(PlanarQueryDebugAuthoring authoring)
        {
            AddBuffer<PlanarQueryDebugHit>(GetEntity(TransformUsageFlags.None));
        }
    }
}
