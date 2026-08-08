using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots.LowLevel
{
    /// <summary>
    /// The three dials of a second-order response, in the units the course states them in.
    /// </summary>
    public struct SecondOrderTuning
    {
        /// <summary>Response speed in Hz. Low is heavy and languid; high is snappy. Zero disables the filter.</summary>
        public float Frequency;

        /// <summary>Damping. Below one overshoots and settles, one is critical, above one is stiff.</summary>
        public float Damping;

        /// <summary>Launch character. Zero eases in, one starts immediately, above one leads.</summary>
        public float Response;
    }

    /// <summary>One filtered scalar: its output, that output's velocity, and last tick's input.</summary>
    public struct SecondOrderFloat
    {
        public float Value;
        public float Velocity;
        public float PreviousInput;
    }

    /// <summary>One filtered planar value. Identical maths, run on both components.</summary>
    public struct SecondOrderFloat2
    {
        public float2 Value;
        public float2 Velocity;
        public float2 PreviousInput;
    }

    /// <summary>
    /// The spring-damper filter the course uses as a universal juice layer: feed it a target and
    /// it returns a version of that target with weight, lag, and overshoot.
    /// </summary>
    /// <remarks>
    /// The constants are derived per call rather than baked into <see cref="SecondOrderTuning"/>
    /// on purpose. The stability clamp depends on the timestep, so a filter that baked it would be
    /// correct only at the step it was baked for — and presentation is exactly the layer most
    /// likely to run on a variable frame delta.
    /// </remarks>
    public static class SecondOrderMath
    {
        /// <summary>Seeds a filter at rest on <paramref name="input"/>, so its first tick does not lurch.</summary>
        public static void Reset(ref SecondOrderFloat filter, float input)
        {
            filter.Value = input;
            filter.Velocity = 0f;
            filter.PreviousInput = input;
        }

        /// <inheritdoc cref="Reset(ref SecondOrderFloat, float)"/>
        public static void Reset(ref SecondOrderFloat2 filter, float2 input)
        {
            filter.Value = input;
            filter.Velocity = float2.zero;
            filter.PreviousInput = input;
        }

        public static void Advance(
            ref SecondOrderFloat filter, float input, in SecondOrderTuning tuning, float deltaTime)
        {
            if (!Constants(tuning, deltaTime, out var k1, out var k2Stable, out var k3))
            {
                Reset(ref filter, input);
                return;
            }

            var inputVelocity = (input - filter.PreviousInput) / deltaTime;
            filter.PreviousInput = input;
            filter.Value += deltaTime * filter.Velocity;
            filter.Velocity += deltaTime
                * (input + k3 * inputVelocity - filter.Value - k1 * filter.Velocity) / k2Stable;
        }

        /// <inheritdoc cref="Advance(ref SecondOrderFloat, float, in SecondOrderTuning, float)"/>
        public static void Advance(
            ref SecondOrderFloat2 filter, float2 input, in SecondOrderTuning tuning, float deltaTime)
        {
            if (!Constants(tuning, deltaTime, out var k1, out var k2Stable, out var k3))
            {
                Reset(ref filter, input);
                return;
            }

            var inputVelocity = (input - filter.PreviousInput) / deltaTime;
            filter.PreviousInput = input;
            filter.Value += deltaTime * filter.Velocity;
            filter.Velocity += deltaTime
                * (input + k3 * inputVelocity - filter.Value - k1 * filter.Velocity) / k2Stable;
        }

        /// <summary>
        /// Turns frequency, damping, and response into the three coefficients of the update, with
        /// the timestep-dependent clamp on k2 that keeps a fast filter from exploding at a slow
        /// frame rate. Returns false when there is nothing to filter and the caller should snap.
        /// </summary>
        static bool Constants(
            in SecondOrderTuning tuning, float deltaTime, out float k1, out float k2Stable, out float k3)
        {
            k1 = 0f;
            k2Stable = 1f;
            k3 = 0f;
            if (tuning.Frequency <= 0f || deltaTime <= 0f)
                return false;

            var angularFrequency = 2f * math.PI * tuning.Frequency;
            k1 = tuning.Damping / (math.PI * tuning.Frequency);
            k3 = tuning.Response * tuning.Damping / angularFrequency;
            k2Stable = math.max(
                1f / (angularFrequency * angularFrequency),
                math.max(deltaTime * deltaTime * 0.5f + deltaTime * k1 * 0.5f, deltaTime * k1));
            return true;
        }
    }
}
