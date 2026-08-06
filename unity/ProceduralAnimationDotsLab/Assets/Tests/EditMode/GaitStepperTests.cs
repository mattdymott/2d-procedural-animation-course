using NUnit.Framework;
using Unity.Mathematics;

namespace ProceduralAnimationDotsLab.Tests
{
    public sealed class GaitStepperTests
    {
        static readonly GaitSettings Settings = new()
        {
            Comfort = 0.5f,
            StepDuration = 0.5f,
            StepLead = 0.2f,
            StepHeight = 0.4f,
        };

        [Test]
        public void Update_KeepsAStressedFootPlantedWhileItsPartnerSwings()
        {
            var leg = new GaitLeg
            {
                State = FootState.Planted,
                Plant = new float2(-2f, 0f),
                HomeOffset = float2.zero,
            };

            var target = GaitStepper.Update(
                ref leg,
                FootState.Swinging,
                float2.zero,
                new float2(3f, 0f),
                3f,
                Settings,
                0.1f);

            Assert.That(leg.State, Is.EqualTo(FootState.Planted));
            Assert.That(target, Is.EqualTo(leg.Plant));
        }

        [Test]
        public void Update_PreservesTheCommittedSwingTargetAfterTheBodyVelocityChanges()
        {
            var leg = new GaitLeg
            {
                State = FootState.Planted,
                Plant = new float2(-2f, 0f),
                HomeOffset = float2.zero,
            };

            GaitStepper.Update(
                ref leg,
                FootState.Planted,
                float2.zero,
                new float2(2f, 0f),
                3f,
                Settings,
                0f);
            var committedTarget = leg.SwingTo;

            GaitStepper.Update(
                ref leg,
                FootState.Planted,
                new float2(4f, 0f),
                new float2(-10f, 0f),
                3f,
                Settings,
                0.1f);

            Assert.That(leg.State, Is.EqualTo(FootState.Swinging));
            Assert.That(leg.SwingTo, Is.EqualTo(committedTarget));
        }

        [Test]
        public void EvaluateSwingTarget_UsesTheCommittedEndpointsAndParabolicApex()
        {
            var leg = new GaitLeg
            {
                SwingFrom = new float2(-1f, -2f),
                SwingTo = new float2(3f, -2f),
            };

            leg.SwingT = 0f;
            Assert.That(GaitStepper.EvaluateSwingTarget(leg, Settings), Is.EqualTo(leg.SwingFrom));

            leg.SwingT = 0.5f;
            var apex = GaitStepper.EvaluateSwingTarget(leg, Settings);
            Assert.That(apex.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(apex.y, Is.EqualTo(-1.6f).Within(0.0001f));

            leg.SwingT = 1f;
            Assert.That(GaitStepper.EvaluateSwingTarget(leg, Settings), Is.EqualTo(leg.SwingTo));
        }
    }
}
