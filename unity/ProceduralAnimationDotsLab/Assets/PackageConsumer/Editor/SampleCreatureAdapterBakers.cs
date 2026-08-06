using Unity.Entities;

namespace ProceduralAnimationPackageConsumer
{
    sealed class SampleCreaturePatrolBaker : Baker<SampleCreaturePatrolAuthoring>
    {
        public override void Bake(SampleCreaturePatrolAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.None), new SampleCreaturePatrol
            {
                Speed = authoring.Speed,
                Direction = 1f,
                MinimumX = authoring.MinimumX,
                MaximumX = authoring.MaximumX,
            });
        }
    }

    sealed class SampleFlatGroundBaker : Baker<SampleFlatGroundAuthoring>
    {
        public override void Bake(SampleFlatGroundAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.None), new SampleFlatGround { Height = authoring.Height });
        }
    }
}
