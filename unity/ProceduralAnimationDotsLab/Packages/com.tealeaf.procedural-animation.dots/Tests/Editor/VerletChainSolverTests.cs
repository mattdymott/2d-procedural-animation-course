using NUnit.Framework;
using Tealeaf.ProceduralAnimation.Dots;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots.Tests
{
    public sealed class VerletChainSolverTests
    {
        [Test]
        public void Pin_SetsPositionAndPreviousPositionToTheAuthoritativeRoot()
        {
            var point = new VerletPoint
            {
                Position = new float2(4f, -2f),
                PreviousPosition = new float2(1f, 3f),
            };

            VerletChainSolver.Pin(ref point, new float2(-1f, 2f));

            Assert.That(point.Position, Is.EqualTo(new float2(-1f, 2f)));
            Assert.That(point.PreviousPosition, Is.EqualTo(new float2(-1f, 2f)));
        }

        [Test]
        public void SatisfyDistance_RestoresTheConfiguredLinkLength()
        {
            var first = new VerletPoint { Position = new float2(0f, 0f) };
            var second = new VerletPoint { Position = new float2(4f, 0f) };

            VerletChainSolver.SatisfyDistance(ref first, ref second, 1.5f);

            Assert.That(math.distance(first.Position, second.Position), Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void SatisfyDistance_LeavesCoincidentPointsUntouched()
        {
            var first = new VerletPoint { Position = new float2(2f, 3f) };
            var second = new VerletPoint { Position = new float2(2f, 3f) };

            VerletChainSolver.SatisfyDistance(ref first, ref second, 1f);

            Assert.That(first.Position, Is.EqualTo(new float2(2f, 3f)));
            Assert.That(second.Position, Is.EqualTo(new float2(2f, 3f)));
        }
    }

    public sealed class TwoBoneIkSolverTests
    {
        [Test]
        public void Solve_ProducesBothConfiguredBoneLengthsForAReachableTarget()
        {
            var request = new TwoBoneIkRequest
            {
                Root = float2.zero,
                Target = new float2(2.5f, 0f),
                LengthA = 2f,
                LengthB = 2f,
                BendSign = 1f,
            };

            var pose = TwoBoneIk.Solve(request);

            Assert.That(math.distance(request.Root, pose.Knee), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(math.distance(pose.Knee, pose.Foot), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(pose.Knee.y, Is.GreaterThan(0f));
        }

        [Test]
        public void Solve_ClampsAnUnreachableTargetToTheOuterReach()
        {
            var request = new TwoBoneIkRequest
            {
                Root = float2.zero,
                Target = new float2(10f, 0f),
                LengthA = 2f,
                LengthB = 1f,
                BendSign = -1f,
            };

            var pose = TwoBoneIk.Solve(request);

            Assert.That(math.distance(request.Root, pose.Foot), Is.LessThan(3f));
            Assert.That(math.distance(request.Root, pose.Foot), Is.GreaterThan(2.999f));
        }

        [Test]
        public void Solve_UsesAStableFallbackDirectionAtTheRoot()
        {
            var request = new TwoBoneIkRequest
            {
                Root = new float2(3f, 4f),
                Target = new float2(3f, 4f),
                LengthA = 3f,
                LengthB = 1f,
                BendSign = 1f,
            };

            var pose = TwoBoneIk.Solve(request);

            Assert.That(math.distance(request.Root, pose.Foot), Is.GreaterThan(2f));
            Assert.That(pose.Foot.x, Is.GreaterThan(request.Root.x));
        }
    }

    public sealed class VerletContactSolverTests
    {
        [Test]
        public void ProjectAgainstPlane_PushesPenetrationOutAndRemovesIntoSurfaceVelocity()
        {
            var point = new VerletPoint
            {
                Position = new float2(2f, -1f),
                PreviousPosition = new float2(1f, 1f),
            };
            var plane = new ContactPlane
            {
                Point = float2.zero,
                Normal = new float2(0f, 1f),
                Radius = 0.25f,
                Friction = 0.25f,
            };

            var projected = VerletContactSolver.ProjectAgainstPlane(ref point, plane);

            Assert.That(projected, Is.True);
            Assert.That(point.Position, Is.EqualTo(new float2(2f, 0.25f)));
            Assert.That(point.Position.y - point.PreviousPosition.y, Is.GreaterThanOrEqualTo(0f));
            Assert.That(point.Position.x - point.PreviousPosition.x, Is.EqualTo(0.75f).Within(0.0001f));
        }

        [Test]
        public void ProjectAgainstPlane_LeavesALegalPointUntouched()
        {
            var point = new VerletPoint
            {
                Position = new float2(2f, 0.5f),
                PreviousPosition = new float2(1f, 0.25f),
            };
            var original = point;
            var plane = new ContactPlane
            {
                Point = float2.zero,
                Normal = new float2(0f, 1f),
                Radius = 0.25f,
                Friction = 1f,
            };

            var projected = VerletContactSolver.ProjectAgainstPlane(ref point, plane);

            Assert.That(projected, Is.False);
            Assert.That(point.Position, Is.EqualTo(original.Position));
            Assert.That(point.PreviousPosition, Is.EqualTo(original.PreviousPosition));
        }
    }
}
