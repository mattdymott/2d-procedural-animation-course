using NUnit.Framework;
using Tealeaf.ProceduralAnimation.Dots.LowLevel;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots.Tests
{
    /// <summary>
    /// Lessons 26–28. Body language is a filter over resolved motion, so everything worth pinning
    /// is about what it reads and how it behaves in time — never about what it writes, because it
    /// writes nothing but a picture.
    /// </summary>
    public sealed class BodyPresentationTests
    {
        const float DeltaTime = 1f / 60f;

        static readonly BodyPresentationPolicy DefaultPolicy = new()
        {
            BankResponse = new SecondOrderTuning { Frequency = 6f, Damping = 0.8f, Response = 0f },
            WeightShiftResponse = new SecondOrderTuning { Frequency = 3f, Damping = 0.9f, Response = 0f },
            BankGain = 0.25f,
            MaxBank = 0.5f,
            StretchGain = 0.1f,
            AnticipationBank = 0.3f,
            AnticipationSeconds = 0.2f,
            WeightShiftDistance = 0.2f,
        };

        // ----------------------------------------------------------------------------------
        // Lesson 6's filter, arriving now because body language is the first thing that needs it
        // ----------------------------------------------------------------------------------

        [Test]
        public void ASecondOrderFilterSettlesOnItsTarget()
        {
            var tuning = new SecondOrderTuning { Frequency = 2f, Damping = 1f, Response = 0f };
            var filter = default(SecondOrderFloat);
            SecondOrderMath.Reset(ref filter, 0f);

            for (var tick = 0; tick < 600; tick++)
                SecondOrderMath.Advance(ref filter, 1f, tuning, DeltaTime);

            Assert.That(filter.Value, Is.EqualTo(1f).Within(0.001f));
            Assert.That(filter.Velocity, Is.EqualTo(0f).Within(0.001f), "Critically damped means it arrives and stops.");
        }

        [Test]
        public void AFastFilterOnASlowFrameStaysFinite()
        {
            // The stability clamp is the reason presentation can run on a variable frame delta at
            // all. Without it, a 20 Hz filter stepped at 15 fps diverges within a few ticks.
            var tuning = new SecondOrderTuning { Frequency = 20f, Damping = 0.5f, Response = 0f };
            var filter = default(SecondOrderFloat);
            SecondOrderMath.Reset(ref filter, 0f);

            for (var tick = 0; tick < 200; tick++)
                SecondOrderMath.Advance(ref filter, 1f, tuning, 1f / 15f);

            Assert.That(math.isfinite(filter.Value), Is.True);
            Assert.That(math.abs(filter.Value), Is.LessThan(4f), "A clamped filter overshoots; it does not explode.");
        }

        [Test]
        public void AFilterWithNoFrequencySnapsAndCostsNothing()
        {
            var tuning = new SecondOrderTuning { Frequency = 0f, Damping = 1f, Response = 0f };
            var filter = default(SecondOrderFloat2);

            SecondOrderMath.Advance(ref filter, new float2(3f, -1f), tuning, DeltaTime);

            Assert.That(filter.Value, Is.EqualTo(new float2(3f, -1f)),
                "Turning an effect off by zeroing its frequency has to leave the raw value behind.");
        }

        // ----------------------------------------------------------------------------------
        // Lesson 26 — bank and stretch, read from resolved motion
        // ----------------------------------------------------------------------------------

        [Test]
        public void TurnRateIsSignedEvidenceMeasuredAfterTheFact()
        {
            var quarterTurnLeft = BodyPresentationMath.TurnRate(
                new float2(1f, 0f), new float2(0f, 1f), deltaTime: 0.5f);

            Assert.That(quarterTurnLeft, Is.EqualTo(math.PI).Within(0.0001f));
            Assert.That(
                BodyPresentationMath.TurnRate(new float2(0f, 1f), new float2(1f, 0f), 0.5f),
                Is.EqualTo(-math.PI).Within(0.0001f));
        }

        [Test]
        public void BankLeansIntoTheTurnAndStopsAtItsLimit()
        {
            Assert.That(BodyPresentationMath.BankTarget(turnRate: 2f, bankGain: 0.25f, maxBank: 1f),
                Is.LessThan(0f), "Turning left rolls the picture right, the way a rider's weight goes.");
            Assert.That(BodyPresentationMath.BankTarget(-2f, 0.25f, 1f), Is.GreaterThan(0f));
            Assert.That(BodyPresentationMath.BankTarget(50f, 0.25f, 0.4f), Is.EqualTo(-0.4f).Within(0.0001f),
                "However hard the creature turns, the lean is authored, not emergent.");
        }

        [Test]
        public void StretchIsVolumePreservingAndNeutralAtRest()
        {
            var still = BodyPresentationMath.StretchScale(speed: 0f, stretchGain: 0.1f);
            var moving = BodyPresentationMath.StretchScale(speed: 4f, stretchGain: 0.1f);

            Assert.That(still, Is.EqualTo(new float2(1f, 1f)));
            Assert.That(moving.x, Is.GreaterThan(1f));
            Assert.That(moving.x * moving.y, Is.EqualTo(1f).Within(0.0001f));
        }

        // ----------------------------------------------------------------------------------
        // Lesson 27 — a requested turn is a timing cue, not a steering input
        // ----------------------------------------------------------------------------------

        [Test]
        public void AnticipationBanksAgainstTheTurnAndThenHandsOverToIt()
        {
            var state = default(BodyPresentationState);
            var position = float2.zero;
            var forward = new float2(1f, 0f);
            BodyPresentationMath.Advance(ref state, position, forward, 0f, DefaultPolicy, DeltaTime);

            // The intent has been published; the heading has not moved yet. This is the window the
            // whole lesson is about.
            for (var tick = 0; tick < 6; tick++)
                BodyPresentationMath.Advance(ref state, position, forward, 1f, DefaultPolicy, DeltaTime);

            var windUp = state.Bank.Value;
            Assert.That(windUp, Is.GreaterThan(0f),
                "Before the turn, the body leans away from it — the opposite of where it will end up.");

            // Now the turn actually resolves, and the request is spent.
            for (var tick = 0; tick < 60; tick++)
            {
                forward = Rotate(forward, 0.03f);
                BodyPresentationMath.Advance(ref state, position, forward, 0f, DefaultPolicy, DeltaTime);
            }

            Assert.That(state.Bank.Value, Is.LessThan(0f),
                "Once the heading is really turning, the bank follows the resolved turn rate.");
        }

        [Test]
        public void AStandingRequestDoesNotHoldTheWindUpOpen()
        {
            // A level-triggered wind-up would bank the body backwards for the whole turn. The
            // request is an edge: it starts a short window and that window expires on its own.
            var state = default(BodyPresentationState);
            var forward = new float2(1f, 0f);
            BodyPresentationMath.Advance(ref state, float2.zero, forward, 0f, DefaultPolicy, DeltaTime);

            for (var tick = 0; tick < 60; tick++)
            {
                forward = Rotate(forward, 0.03f);
                BodyPresentationMath.Advance(ref state, float2.zero, forward, 1f, DefaultPolicy, DeltaTime);
            }

            Assert.That(state.AnticipationRemaining, Is.EqualTo(0f));
            Assert.That(state.Bank.Value, Is.LessThan(0f),
                "The request never went away, but the cue it triggered did.");
        }

        [Test]
        public void AOneTickRequestStillLeansAwayForTheWholeWindow()
        {
            // The window belongs to the cue, not to the request that opened it. Reading the live
            // request instead means a request withdrawn a tick later leaves the body chasing
            // neutral for the rest of the window: a wind-up that unwinds instead of anticipating.
            var state = default(BodyPresentationState);
            var forward = new float2(1f, 0f);
            BodyPresentationMath.Advance(ref state, float2.zero, forward, 0f, DefaultPolicy, DeltaTime);
            BodyPresentationMath.Advance(ref state, float2.zero, forward, 1f, DefaultPolicy, DeltaTime);

            // The request is gone from here on, but the heading has not started moving either.
            for (var tick = 0; tick < 10; tick++)
                BodyPresentationMath.Advance(ref state, float2.zero, forward, 0f, DefaultPolicy, DeltaTime);

            Assert.That(state.AnticipationRemaining, Is.GreaterThan(0f), "This assertion needs the window still open.");
            Assert.That(state.Bank.Value, Is.GreaterThan(0.02f),
                "The cue keeps leaning away from the turn it was raised for.");
        }

        // ----------------------------------------------------------------------------------
        // Lesson 28 — weight shift, and the toggle every one of these effects has to pass
        // ----------------------------------------------------------------------------------

        [Test]
        public void WeightShiftCarriesTheDrawnBodyIntoABrake()
        {
            var braking = BodyPresentationMath.WeightShiftTarget(
                acceleration: new float2(-4f, 0f), weightShiftDistance: 0.2f);

            Assert.That(braking.x, Is.EqualTo(0.2f).Within(0.0001f),
                "Decelerating along +X drifts the picture further along +X.");
            Assert.That(BodyPresentationMath.WeightShiftTarget(float2.zero, 0.2f), Is.EqualTo(float2.zero),
                "Cruising at a steady speed is not a weight shift.");
        }

        [Test]
        public void ADisabledEffectLeavesTheResolvedPictureExactlyWhereItWas()
        {
            // The authority test the lessons all state the same way: switch the effect off and the
            // creature is unchanged. Here that is visible as an exact equality, because the only
            // thing presentation ever produced was an offset from the resolved point.
            var policy = DefaultPolicy;
            policy.WeightShiftDistance = 0f;

            var state = default(BodyPresentationState);
            var position = float2.zero;
            var velocity = new float2(3f, 0f);
            var presentation = default(BodyPresentation);

            for (var tick = 0; tick < 30; tick++)
            {
                // Brake hard: this is the moment the effect would be largest if it were enabled.
                velocity = math.lerp(velocity, float2.zero, 0.3f);
                position += velocity * DeltaTime;
                presentation = BodyPresentationMath.Advance(
                    ref state, position, new float2(1f, 0f), 0f, policy, DeltaTime);
            }

            Assert.That(presentation.WeightShift, Is.EqualTo(float2.zero));
            Assert.That(presentation.RenderPosition, Is.EqualTo(position));
        }

        [Test]
        public void TheDrawnBodyIsAlwaysTheResolvedBodyPlusItsOwnOffset()
        {
            var state = default(BodyPresentationState);
            var position = float2.zero;
            var velocity = float2.zero;

            for (var tick = 0; tick < 30; tick++)
            {
                velocity = math.lerp(velocity, new float2(4f, 1f), 0.25f);
                position += velocity * DeltaTime;
                var presentation = BodyPresentationMath.Advance(
                    ref state, position, math.normalize(velocity), 0f, DefaultPolicy, DeltaTime);

                Assert.That(math.distance(presentation.RenderPosition, position + presentation.WeightShift),
                    Is.LessThan(0.0001f),
                    "There is one resolved point and one offset from it. There is never a second body.");
                // An under-damped filter overshoots its target by design, so the leash is a small
                // multiple of the authored distance rather than the distance itself. What matters
                // is that there is a leash at all: the offset is bounded by policy, not integrated.
                Assert.That(math.distance(presentation.RenderPosition, position),
                    Is.LessThan(DefaultPolicy.WeightShiftDistance * 2f),
                    "The picture is on a leash: it can lag the truth, never wander off from it.");
            }

            Assert.That(math.lengthsq(state.WeightShift.Value), Is.GreaterThan(0f),
                "This assertion needs the effect to have actually done something.");
        }

        static float2 Rotate(float2 value, float radians) => new(
            value.x * math.cos(radians) - value.y * math.sin(radians),
            value.x * math.sin(radians) + value.y * math.cos(radians));
    }
}
