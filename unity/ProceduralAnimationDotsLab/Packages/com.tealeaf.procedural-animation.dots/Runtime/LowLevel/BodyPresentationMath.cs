using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots.LowLevel
{
    /// <summary>Authored body language. Like <see cref="FootPresentationPolicy"/>, none of it is simulation input.</summary>
    public struct BodyPresentationPolicy
    {
        /// <summary>How the bank angle chases its target. Low frequency reads as mass.</summary>
        public SecondOrderTuning BankResponse;

        /// <summary>How the weight shift chases its target.</summary>
        public SecondOrderTuning WeightShiftResponse;

        /// <summary>Radians of bank per radian-per-second of resolved turn rate.</summary>
        public float BankGain;

        /// <summary>Bank is clamped to this, in radians, however hard the creature turns.</summary>
        public float MaxBank;

        /// <summary>Longitudinal stretch per unit of resolved planar speed.</summary>
        public float StretchGain;

        /// <summary>Radians of wind-up held against a requested turn before it resolves.</summary>
        public float AnticipationBank;

        /// <summary>How long that wind-up lasts. Zero disables anticipation entirely.</summary>
        public float AnticipationSeconds;

        /// <summary>World units the drawn body lags behind its resolved point under acceleration.</summary>
        public float WeightShiftDistance;
    }

    /// <summary>
    /// Presentation's own memory. Every previous-frame value the body language needs lives here
    /// rather than on locomotion or gait: an effect that can be deleted must not have left a field
    /// behind in the simulation that outlives it.
    /// </summary>
    public struct BodyPresentationState
    {
        public float2 PreviousPosition;
        public float2 PreviousVelocity;
        public float2 PreviousForward;
        public float PreviousRequestedTurnSign;

        /// <summary>Seconds of anticipation left to run. Counted down by the filter, nothing else.</summary>
        public float AnticipationRemaining;

        /// <summary>
        /// The sign that opened the current anticipation window, held for its whole duration. The
        /// request that started it is free to be withdrawn a tick later; the cue it triggered is
        /// not, or a one-tick request would lean the body to neutral instead of away.
        /// </summary>
        public float AnticipationSign;

        public SecondOrderFloat Bank;
        public SecondOrderFloat2 WeightShift;

        /// <summary>Zero until the first tick has seeded the previous-frame values.</summary>
        public byte Seeded;
    }

    /// <summary>Transient view data derived from one resolved body pose.</summary>
    public struct BodyPresentation
    {
        /// <summary>The filtered lean, in radians. Positive is a counter-clockwise roll on screen.</summary>
        public float BankRadians;

        /// <summary>Heading angle plus bank — what a sprite or mesh is actually rotated by.</summary>
        public float RotationRadians;

        /// <summary>Longitudinal stretch and its volume-preserving lateral squash.</summary>
        public float2 Scale;

        /// <summary>The render-only offset from the resolved body point.</summary>
        public float2 WeightShift;

        /// <summary>Where the body is drawn: the resolved point, offset. The resolved point does not move.</summary>
        public float2 RenderPosition;
    }

    /// <summary>
    /// Body language for a top-down creature: bank into a turn, stretch with speed, wind up before
    /// an intended turn, and lag under acceleration. Every one of those reads resolved simulation
    /// output and returns a picture. Nothing here is ever read back by gait, IK, or collision —
    /// deleting the whole file must change flavour and nothing else.
    /// </summary>
    /// <remarks>
    /// The pieces are exposed individually because each lesson's acceptance test is "switch this
    /// one effect off and the plants, heading, and collision are identical." A single monolithic
    /// derive would make that awkward to demonstrate.
    /// </remarks>
    public static class BodyPresentationMath
    {
        /// <summary>The screen angle of a heading, for handing to a sprite or transform.</summary>
        public static float HeadingAngle(float2 forward) => math.atan2(forward.y, forward.x);

        /// <summary>
        /// Signed turn rate in radians per second, from two resolved headings. This is evidence,
        /// measured after the fact — it is not the turn locomotion asked for.
        /// </summary>
        public static float TurnRate(float2 previousForward, float2 forward, float deltaTime)
        {
            if (deltaTime <= 0f)
                return 0f;

            var from = math.normalizesafe(previousForward, new float2(1f, 0f));
            var to = math.normalizesafe(forward, from);
            var cross = from.x * to.y - from.y * to.x;
            var dot = math.dot(from, to);
            return math.atan2(cross, dot) / deltaTime;
        }

        /// <summary>
        /// Lesson 26. The body leans <em>into</em> the turn: a left turn rolls the picture right,
        /// the way a rider's weight goes.
        /// </summary>
        public static float BankTarget(float turnRate, float bankGain, float maxBank) =>
            math.clamp(-turnRate * bankGain, -maxBank, maxBank);

        /// <summary>Lesson 26. Longitudinal stretch as a commitment cue, with a lateral squash to match.</summary>
        public static float2 StretchScale(float speed, float stretchGain)
        {
            var stretch = math.max(1f + speed * stretchGain, 0.0001f);
            return new float2(stretch, 1f / stretch);
        }

        /// <summary>
        /// Lesson 27. The wind-up: a bank held <em>against</em> the turn that has been requested but
        /// not yet resolved. <see cref="BankTarget"/> negates its turn rate, so this one does not —
        /// the two are deliberately opposite, which is what makes the cue read as anticipation
        /// rather than as an early copy of the turn itself.
        /// </summary>
        public static float AnticipationBankTarget(float requestedTurnSign, float anticipationBank, float maxBank) =>
            math.clamp(math.sign(requestedTurnSign) * anticipationBank, -maxBank, maxBank);

        /// <summary>
        /// Lesson 28. The drawn body lags the direction it is being accelerated in, so braking
        /// carries it forward and setting off leaves it behind. It never changes where the body
        /// legally stopped.
        /// </summary>
        public static float2 WeightShiftTarget(float2 acceleration, float weightShiftDistance) =>
            -math.normalizesafe(acceleration, float2.zero) * weightShiftDistance;

        /// <summary>
        /// Runs one presentation tick and returns the whole picture.
        /// </summary>
        /// <param name="state">Presentation's own memory, advanced in place.</param>
        /// <param name="resolvedPosition">The body point simulation settled on this tick.</param>
        /// <param name="resolvedForward">The heading locomotion settled on this tick.</param>
        /// <param name="requestedTurnSign">
        /// The semantic intent from <see cref="CreatureLocomotion.RequestedTurnSign"/>: positive to
        /// turn left, negative to turn right, zero for no request. Read only, and only for timing.
        /// </param>
        public static BodyPresentation Advance(
            ref BodyPresentationState state,
            float2 resolvedPosition,
            float2 resolvedForward,
            float requestedTurnSign,
            in BodyPresentationPolicy policy,
            float deltaTime)
        {
            var forward = math.normalizesafe(resolvedForward, new float2(1f, 0f));
            if (state.Seeded == 0 || deltaTime <= 0f)
                return Seed(ref state, resolvedPosition, forward, requestedTurnSign, policy);

            // Resolved velocity and acceleration, differenced here rather than read from
            // locomotion: intent is what the creature asked for, and this layer draws what it got.
            var velocity = (resolvedPosition - state.PreviousPosition) / deltaTime;
            var acceleration = (velocity - state.PreviousVelocity) / deltaTime;
            var turnRate = TurnRate(state.PreviousForward, forward, deltaTime);

            // A request is an edge, not a level. Holding the wind-up for as long as the request
            // stands would bank the body the wrong way through the entire turn.
            if (requestedTurnSign != 0f && requestedTurnSign != state.PreviousRequestedTurnSign)
            {
                state.AnticipationRemaining = policy.AnticipationSeconds;
                state.AnticipationSign = requestedTurnSign;
            }

            var bankTarget = BankTarget(turnRate, policy.BankGain, policy.MaxBank);
            if (state.AnticipationRemaining > 0f)
            {
                state.AnticipationRemaining = math.max(state.AnticipationRemaining - deltaTime, 0f);
                bankTarget = AnticipationBankTarget(
                    state.AnticipationSign, policy.AnticipationBank, policy.MaxBank);
            }

            SecondOrderMath.Advance(ref state.Bank, bankTarget, policy.BankResponse, deltaTime);
            SecondOrderMath.Advance(
                ref state.WeightShift,
                WeightShiftTarget(acceleration, policy.WeightShiftDistance),
                policy.WeightShiftResponse,
                deltaTime);

            state.PreviousPosition = resolvedPosition;
            state.PreviousVelocity = velocity;
            state.PreviousForward = forward;
            state.PreviousRequestedTurnSign = requestedTurnSign;

            return Compose(state, resolvedPosition, forward, math.length(velocity), policy);
        }

        static BodyPresentation Seed(
            ref BodyPresentationState state,
            float2 resolvedPosition,
            float2 forward,
            float requestedTurnSign,
            in BodyPresentationPolicy policy)
        {
            state.PreviousPosition = resolvedPosition;
            state.PreviousVelocity = float2.zero;
            state.PreviousForward = forward;
            state.PreviousRequestedTurnSign = requestedTurnSign;
            state.AnticipationRemaining = 0f;
            state.AnticipationSign = 0f;
            SecondOrderMath.Reset(ref state.Bank, 0f);
            SecondOrderMath.Reset(ref state.WeightShift, float2.zero);
            state.Seeded = 1;

            return Compose(state, resolvedPosition, forward, speed: 0f, policy);
        }

        static BodyPresentation Compose(
            in BodyPresentationState state,
            float2 resolvedPosition,
            float2 forward,
            float speed,
            in BodyPresentationPolicy policy) => new()
        {
            BankRadians = state.Bank.Value,
            RotationRadians = HeadingAngle(forward) + state.Bank.Value,
            Scale = StretchScale(speed, policy.StretchGain),
            WeightShift = state.WeightShift.Value,
            RenderPosition = resolvedPosition + state.WeightShift.Value,
        };
    }
}
