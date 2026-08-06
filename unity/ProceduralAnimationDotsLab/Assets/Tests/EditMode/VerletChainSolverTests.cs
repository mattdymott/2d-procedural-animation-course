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
}
