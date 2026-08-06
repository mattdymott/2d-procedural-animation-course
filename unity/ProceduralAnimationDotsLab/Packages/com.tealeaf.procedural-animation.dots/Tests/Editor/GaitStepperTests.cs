using NUnit.Framework;
using Tealeaf.ProceduralAnimation.Dots;
using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots.Tests
{
    public sealed class GaitStepperTests
    {
        static readonly GaitSettings Settings = new()
        {
            Comfort = 0.5f,
            StepDuration = 0.5f,
            StepLead = 0.2f,
            StepHeight = 0.4f,
            MinimumSupport = 0.7f,
            MinimumForward = 0f,
        };

        [Test]
        public void SupportMath_TransformsAndInvertsALocalPlant()
        {
            var pose = new SupportPose
            {
                Position = new float2(3f, 2f),
                RotationRadians = math.PI * 0.5f,
            };
            var localPlant = new float2(1f, 0f);

            var worldPlant = SupportMath.TransformPoint(pose, localPlant);
            var roundTrip = SupportMath.InverseTransformPoint(pose, worldPlant);

            Assert.That(worldPlant.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(worldPlant.y, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(roundTrip.x, Is.EqualTo(localPlant.x).Within(0.0001f));
            Assert.That(roundTrip.y, Is.EqualTo(localPlant.y).Within(0.0001f));
        }

        [Test]
        public void Update_CarriesTheSupportRelationWhenASwingLands()
        {
            var support = new Entity { Index = 7, Version = 1 };
            var leg = new GaitLeg
            {
                State = FootState.Swinging,
                SwingFrom = new float2(0f, -2f),
                SwingTo = new float2(1f, -2f),
                SwingT = 0.9f,
                SwingSupport = support,
                SwingLocalPlant = new float2(0.25f, 0f),
            };

            var target = GaitStepper.Update(
                ref leg,
                FootState.Planted,
                float2.zero,
                float2.zero,
                0f,
                3f,
                Settings,
                0.1f,
                hasFootholdCandidate: false,
                footholdCandidate: default);

            Assert.That(leg.State, Is.EqualTo(FootState.Planted));
            Assert.That(leg.Support, Is.EqualTo(support));
            Assert.That(leg.LocalPlant.x, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(target, Is.EqualTo(leg.Plant));
        }

        [Test]
        public void SupportMath_RoundTripsAWorldProbeThroughSupportLocalCoordinates()
        {
            var pose = new SupportPose
            {
                Position = new float2(2f, 1f),
                RotationRadians = 0f,
            };

            // How a support adapter turns a world probe into the surface-local
            // point it hands back as FootholdCandidate.SupportLocalPoint.
            var localProbe = SupportMath.InverseTransformPoint(pose, new float2(2.5f, -1f));
            var localSurfacePoint = new float2(localProbe.x, 0f);
            var worldSurfacePoint = SupportMath.TransformPoint(pose, localSurfacePoint);

            Assert.That(localSurfacePoint.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(localSurfacePoint.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(worldSurfacePoint.x, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(worldSurfacePoint.y, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void SupportMath_CombinesSupportPointAndBeltVelocity()
        {
            var pose = new SupportPose
            {
                Position = float2.zero,
                RotationRadians = math.PI * 0.5f,
            };
            var kinematics = new SupportKinematics
            {
                LinearVelocity = new float2(0.5f, 0f),
                AngularVelocityRadians = 0.2f,
                SurfaceVelocityLocal = new float2(1f, 0f),
            };

            var velocity = SupportMath.PointVelocity(pose, kinematics, new float2(0f, 1f));

            Assert.That(velocity.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(velocity.y, Is.EqualTo(0.8f).Within(0.0001f));
        }

        [Test]
        public void Update_ClearsTheOldSupportRelationAtLiftoff()
        {
            var support = new Entity { Index = 7, Version = 1 };
            var leg = new GaitLeg
            {
                State = FootState.Planted,
                Plant = new float2(-2f, -2f),
                HomeOffset = new float2(0f, -2f),
                Support = support,
                LocalPlant = new float2(0.25f, 0f),
            };
            var candidate = new FootholdCandidate
            {
                Point = new float2(0.5f, -2f),
                Normal = new float2(0f, 1f),
            };

            GaitStepper.Update(
                ref leg,
                FootState.Planted,
                float2.zero,
                new float2(1f, 0f),
                1f,
                3f,
                Settings,
                0f,
                hasFootholdCandidate: true,
                footholdCandidate: candidate);

            Assert.That(leg.State, Is.EqualTo(FootState.Swinging));
            Assert.That(leg.Support, Is.EqualTo(Entity.Null));
        }

        [Test]
        public void TryChooseFoothold_AcceptsASupportiveReachableForwardHit()
        {
            var candidate = new FootholdCandidate
            {
                Point = new float2(0.5f, -2f),
                Normal = new float2(0f, 1f),
            };

            var accepted = GaitStepper.TryChooseFoothold(
                candidate,
                hip: float2.zero,
                home: new float2(0f, -2f),
                bodyVelocity: new float2(1f, 0f),
                minimumReach: 1f,
                maximumReach: 3f,
                Settings,
                out var foothold);

            Assert.That(accepted, Is.True);
            Assert.That(foothold, Is.EqualTo(candidate.Point));
        }

        [Test]
        public void TryChooseFoothold_RejectsAnUnsupportedHit()
        {
            var candidate = new FootholdCandidate
            {
                Point = new float2(0.5f, -2f),
                Normal = new float2(1f, 0f),
            };

            var accepted = GaitStepper.TryChooseFoothold(
                candidate,
                float2.zero,
                new float2(0f, -2f),
                new float2(1f, 0f),
                1f,
                3f,
                Settings,
                out _);

            Assert.That(accepted, Is.False);
        }

        [Test]
        public void TryChooseFoothold_RejectsAHitOutsideTheIkAnnulus()
        {
            var candidate = new FootholdCandidate
            {
                Point = new float2(0.2f, -0.1f),
                Normal = new float2(0f, 1f),
            };

            var accepted = GaitStepper.TryChooseFoothold(
                candidate,
                float2.zero,
                new float2(0f, -2f),
                new float2(1f, 0f),
                1f,
                3f,
                Settings,
                out _);

            Assert.That(accepted, Is.False);
        }

        [Test]
        public void TryChooseFoothold_RejectsAHitBehindTheForwardPolicy()
        {
            var candidate = new FootholdCandidate
            {
                Point = new float2(-0.5f, -2f),
                Normal = new float2(0f, 1f),
            };

            var accepted = GaitStepper.TryChooseFoothold(
                candidate,
                float2.zero,
                new float2(0f, -2f),
                new float2(1f, 0f),
                1f,
                3f,
                Settings,
                out _);

            Assert.That(accepted, Is.False);
        }

        [Test]
        public void Update_KeepsTheExistingPlantWhenNoFootholdCandidateIsAvailable()
        {
            var leg = new GaitLeg
            {
                State = FootState.Planted,
                Plant = new float2(-2f, -2f),
                HomeOffset = new float2(0f, -2f),
            };
            var target = GaitStepper.Update(
                ref leg,
                FootState.Planted,
                float2.zero,
                new float2(1f, 0f),
                1f,
                3f,
                Settings,
                0.1f,
                hasFootholdCandidate: false,
                footholdCandidate: default);

            Assert.That(leg.State, Is.EqualTo(FootState.Planted));
            Assert.That(target, Is.EqualTo(leg.Plant));
        }

        [Test]
        public void Update_CommitsTheAcceptedFootholdOnlyWhenSwingBegins()
        {
            var leg = new GaitLeg
            {
                State = FootState.Planted,
                Plant = new float2(-2f, -2f),
                HomeOffset = new float2(0f, -2f),
            };
            var firstCandidate = new FootholdCandidate
            {
                Point = new float2(0.5f, -2f),
                Normal = new float2(0f, 1f),
            };
            var secondCandidate = new FootholdCandidate
            {
                Point = new float2(2f, -2f),
                Normal = new float2(0f, 1f),
            };

            GaitStepper.Update(
                ref leg,
                FootState.Planted,
                float2.zero,
                new float2(1f, 0f),
                1f,
                3f,
                Settings,
                0f,
                hasFootholdCandidate: true,
                footholdCandidate: firstCandidate);
            var committedTarget = leg.SwingTo;

            GaitStepper.Update(
                ref leg,
                FootState.Planted,
                float2.zero,
                new float2(-10f, 0f),
                1f,
                3f,
                Settings,
                0.1f,
                hasFootholdCandidate: true,
                footholdCandidate: secondCandidate);

            Assert.That(leg.State, Is.EqualTo(FootState.Swinging));
            Assert.That(leg.SwingTo, Is.EqualTo(committedTarget));
        }

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
