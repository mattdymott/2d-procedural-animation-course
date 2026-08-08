using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// Puts a creature on a top-down movement plane: leg homes rotate with heading instead of
    /// hanging below a hip, and a swing target stays planar so visible lift can be presentation.
    /// </summary>
    /// <remarks>
    /// Presence is the mode switch. A creature without this component keeps the side-view gait
    /// exactly as it was — world-space home offsets, a support-normal foothold test, and a swing
    /// arc baked into the target's Y.
    /// </remarks>
    public struct PlanarHeading : IComponentData
    {
        /// <summary>
        /// Unit heading on the movement plane. Locomotion refreshes it from desired velocity and
        /// keeps the last non-zero value, so a creature standing still still has a facing.
        /// </summary>
        public float2 LastForward;
    }

    /// <summary>Which permission rule decides who may begin a swing this tick.</summary>
    public enum GaitCadence : byte
    {
        /// <summary>A leg may step while its authored partner stays planted. The original rule.</summary>
        Partner,

        /// <summary>One highest-urgency leg per tick, and only if enough feet stay planted.</summary>
        Support,

        /// <summary>One diagonal tripod may move while the opposing tripod is fully planted.</summary>
        Tripod,

        /// <summary>An authored cursor permits exactly one leg, and advances only when it lands.</summary>
        Wave,
    }

    /// <summary>
    /// Runtime-owned cadence selection. A change is requested from intent but applied only at a
    /// synchronisation point, so switching policy never rewrites a foot that already promised.
    /// </summary>
    public struct GaitCadenceState : IComponentData
    {
        public GaitCadence Active;
        public GaitCadence Pending;
    }

    /// <summary>
    /// Baked support and cadence policy. Speed thresholds use separate enter and exit values so a
    /// creature loitering at one speed does not flip cadence every tick.
    /// </summary>
    public struct GaitSupportPolicy : IComponentData
    {
        /// <summary>Feet that must remain planted after a lift is granted.</summary>
        public byte MinimumPlantedFeet;

        /// <summary>Cadence requested at or below <see cref="ExitSpeed"/>.</summary>
        public GaitCadence SlowCadence;

        /// <summary>Cadence requested at or above <see cref="EnterSpeed"/>.</summary>
        public GaitCadence FastCadence;

        public float EnterSpeed;
        public float ExitSpeed;
    }

    /// <summary>Runtime cursor into <see cref="WaveOrder"/>. Only meaningful under a wave cadence.</summary>
    public struct WaveGaitState : IComponentData
    {
        public byte Cursor;
    }

    /// <summary>The authored crawl order a wave cadence walks through, one leg index per entry.</summary>
    [InternalBufferCapacity(6)]
    public struct WaveOrder : IBufferElementData
    {
        public byte LegIndex;
    }

    /// <summary>What gait is doing about a leg that has nowhere legal to step.</summary>
    public enum GaitRecovery : byte
    {
        None,
        HoldingForFoothold,
    }

    /// <summary>
    /// Gait's semantic hand-off to locomotion when a permitted leg found no legal foothold.
    /// Gait writes it; your locomotion reads it and decides to slow, turn, or back away. It is
    /// never a licence to invent a foot target.
    /// </summary>
    public struct GaitRecoveryRequest : IComponentData
    {
        public GaitRecovery State;

        /// <summary>Non-zero while gait would like a slower approach.</summary>
        public byte SlowDown;

        /// <summary>A heading gait believes is more likely to expose a legal foothold.</summary>
        public float2 PreferredTurn;

        /// <summary>The leg that could not step, or 255 when nothing is blocked.</summary>
        public byte BlockedLegIndex;
    }
}
