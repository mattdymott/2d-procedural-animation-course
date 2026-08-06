using NUnit.Framework;
using Unity.Mathematics;

namespace ProceduralAnimationDotsLab.Tests
{
    public sealed class VerletChainSolverTests
    {
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
            var limb = new Limb2Bone
            {
                Root = float2.zero,
                Target = new float2(2.5f, 0f),
                LengthA = 2f,
                LengthB = 2f,
                BendSign = 1f,
            };

            TwoBoneIkSolver.Solve(ref limb);

            Assert.That(math.distance(limb.Root, limb.Knee), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(math.distance(limb.Knee, limb.Foot), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(limb.Knee.y, Is.GreaterThan(0f));
        }

        [Test]
        public void Solve_ClampsAnUnreachableTargetToTheOuterReach()
        {
            var limb = new Limb2Bone
            {
                Root = float2.zero,
                Target = new float2(10f, 0f),
                LengthA = 2f,
                LengthB = 1f,
                BendSign = -1f,
            };

            TwoBoneIkSolver.Solve(ref limb);

            Assert.That(math.distance(limb.Root, limb.Foot), Is.LessThan(3f));
            Assert.That(math.distance(limb.Root, limb.Foot), Is.GreaterThan(2.999f));
        }

        [Test]
        public void Solve_UsesAStableFallbackDirectionAtTheRoot()
        {
            var limb = new Limb2Bone
            {
                Root = new float2(3f, 4f),
                Target = new float2(3f, 4f),
                LengthA = 3f,
                LengthB = 1f,
                BendSign = 1f,
            };

            TwoBoneIkSolver.Solve(ref limb);

            Assert.That(math.distance(limb.Root, limb.Foot), Is.GreaterThan(2f));
            Assert.That(limb.Foot.x, Is.GreaterThan(limb.Root.x));
        }
    }
}
