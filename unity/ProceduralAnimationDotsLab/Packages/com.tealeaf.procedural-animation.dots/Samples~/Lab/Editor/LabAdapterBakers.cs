using Tealeaf.ProceduralAnimation.Dots;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralAnimationDotsLab
{
    sealed class LabCreaturePatrolBaker : Baker<LabCreaturePatrolAuthoring>
    {
        public override void Bake(LabCreaturePatrolAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.None), new LabCreaturePatrol
            {
                Speed = authoring.Speed,
                Direction = 1f,
                MinimumX = authoring.MinimumX,
                MaximumX = authoring.MaximumX,
            });
        }
    }

    sealed class LabTerrainAdapterBaker : Baker<LabTerrainAdapterAuthoring>
    {
        public override void Bake(LabTerrainAdapterAuthoring authoring)
        {
            AddBuffer<GroundQueryDebugHit>(GetEntity(TransformUsageFlags.None));
        }
    }

    sealed class LabMovingSupportBaker : Baker<LabMovingSupportAuthoring>
    {
        public override void Bake(LabMovingSupportAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var origin = ToFloat2(authoring.Origin);

            AddComponent(entity, new DemoMovingSupport
            {
                Origin = origin,
                Amplitude = ToFloat2(authoring.Amplitude),
                Frequency = authoring.Frequency,
                SurfaceVelocityLocal = ToFloat2(authoring.SurfaceVelocityLocal),
            });
            AddComponent(entity, new SupportPose { Position = origin, RotationRadians = 0f });
            AddComponent<SupportKinematics>(entity);
        }

        static float2 ToFloat2(Vector2 value) => new(value.x, value.y);
    }
}
