using Unity.Entities;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>Runs the package-owned fixed-step creature solve in its required order.</summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class ProceduralAnimationSolveSystemGroup : ComponentSystemGroup
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            AddSystemToUpdateList(World.GetOrCreateSystem<CreatureLocomotionSystem>());
            AddSystemToUpdateList(World.GetOrCreateSystem<VerletChainSystem>());
            AddSystemToUpdateList(World.GetOrCreateSystem<GaitSystem>());
            AddSystemToUpdateList(World.GetOrCreateSystem<TwoBoneIkSystem>());
            AddSystemToUpdateList(World.GetOrCreateSystem<HardResolveSystem>());
            SortSystems();
        }
    }
}
