using Unity.Collections;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// The selection half of the gait decision: given every leg's state and how far its plant has
    /// drifted from home, which legs are permitted to <em>ask</em> for a swing this tick.
    ///
    /// Stress asks, support permits. Whether the permitted leg can actually land is a separate
    /// question answered per leg against world facts — a group or a cursor never overrules it,
    /// and a leg that cannot land holds rather than handing its turn to a neighbour.
    /// </summary>
    internal static class GaitPermission
    {
        /// <summary>Legs beyond this cannot be expressed in the returned mask.</summary>
        public const int MaximumLegs = 32;

        /// <summary>
        /// Returns a bitmask of the legs permitted to begin a swing this tick.
        /// </summary>
        /// <param name="legs">The creature's leg buffer, aliased for read-only use.</param>
        /// <param name="urgency">Distance from each plant to its home; negative for a leg that may not step at all.</param>
        /// <param name="comfort">Drift a plant is allowed before it wants to move.</param>
        /// <param name="cadence">Which permission rule applies.</param>
        /// <param name="minimumPlantedFeet">Feet that must remain planted after every grant.</param>
        /// <param name="cursorLegIndex">The wave cadence's permitted leg, or -1.</param>
        public static uint Permitted(
            in NativeArray<GaitLeg> legs,
            in NativeArray<float> urgency,
            float comfort,
            GaitCadence cadence,
            int minimumPlantedFeet,
            int cursorLegIndex)
        {
            var legCount = math.min(legs.Length, math.min(urgency.Length, MaximumLegs));
            if (legCount <= 0)
                return 0u;

            var plantedCount = 0;
            var eligible = 0u;
            for (var index = 0; index < legCount; index++)
            {
                if (legs[index].State != FootState.Planted)
                    continue;

                plantedCount++;
                if (urgency[index] > comfort)
                    eligible |= 1u << index;
            }

            if (eligible == 0u)
                return 0u;

            switch (cadence)
            {
                case GaitCadence.Support:
                    return PermitHighestUrgency(eligible, urgency, plantedCount, minimumPlantedFeet);
                case GaitCadence.Tripod:
                    return PermitTripod(legs, urgency, legCount, eligible);
                case GaitCadence.Wave:
                    return PermitCursor(eligible, plantedCount, minimumPlantedFeet, cursorLegIndex, legCount);
                default:
                    return PermitPartners(legs, legCount, eligible, plantedCount, minimumPlantedFeet);
            }
        }

        /// <summary>
        /// The original rule: step while your partner keeps the ground. Legs are considered in
        /// index order and a partner already granted this tick counts as gone, which is what stops
        /// a pair from lifting together.
        /// </summary>
        static uint PermitPartners(
            in NativeArray<GaitLeg> legs,
            int legCount,
            uint eligible,
            int plantedCount,
            int minimumPlantedFeet)
        {
            var granted = 0u;
            var grantedCount = 0;
            for (var index = 0; index < legCount; index++)
            {
                if ((eligible & (1u << index)) == 0u)
                    continue;

                var partnerIndex = legs[index].PartnerIndex;
                if (partnerIndex >= 0 && partnerIndex < legCount)
                {
                    if (legs[partnerIndex].State != FootState.Planted)
                        continue;
                    if ((granted & (1u << partnerIndex)) != 0u)
                        continue;
                }

                if (plantedCount - grantedCount - 1 < minimumPlantedFeet)
                    continue;

                granted |= 1u << index;
                grantedCount++;
            }

            return granted;
        }

        /// <summary>One leg per tick — the most stressed one that still leaves a legal base.</summary>
        static uint PermitHighestUrgency(
            uint eligible,
            in NativeArray<float> urgency,
            int plantedCount,
            int minimumPlantedFeet)
        {
            if (plantedCount - 1 < minimumPlantedFeet)
                return 0u;

            var bestIndex = -1;
            var bestUrgency = float.NegativeInfinity;
            for (var index = 0; index < MaximumLegs; index++)
            {
                if ((eligible & (1u << index)) == 0u)
                    continue;
                if (urgency[index] <= bestUrgency)
                    continue;

                bestUrgency = urgency[index];
                bestIndex = index;
            }

            return bestIndex < 0 ? 0u : 1u << bestIndex;
        }

        /// <summary>
        /// A whole diagonal tripod may move, but only while the opposing tripod is entirely
        /// planted. The opposing group is the base; permitting a group never wakes it.
        /// </summary>
        /// <remarks>
        /// Stress selects the <em>group</em>, and then every planted leg in it is permitted —
        /// including ones that have not drifted past comfort yet. Gating each leg individually
        /// instead staggers the group's liftoffs and landings, and a tripod that is never all
        /// planted at the same instant never lets its opposite take a turn: three legs walk and
        /// three legs drag.
        /// </remarks>
        static uint PermitTripod(
            in NativeArray<GaitLeg> legs,
            in NativeArray<float> urgency,
            int legCount,
            uint eligible)
        {
            var plantedMaskA = 0u;
            var plantedMaskB = 0u;
            var allPlantedA = true;
            var allPlantedB = true;
            var urgencyA = float.NegativeInfinity;
            var urgencyB = float.NegativeInfinity;

            for (var index = 0; index < legCount; index++)
            {
                var inGroupA = legs[index].TripodGroup == 0;
                var bit = 1u << index;
                if (legs[index].State == FootState.Planted)
                {
                    if (inGroupA)
                        plantedMaskA |= bit;
                    else
                        plantedMaskB |= bit;
                }
                else if (inGroupA)
                {
                    allPlantedA = false;
                }
                else
                {
                    allPlantedB = false;
                }

                if ((eligible & bit) == 0u)
                    continue;

                if (inGroupA)
                    urgencyA = math.max(urgencyA, urgency[index]);
                else
                    urgencyB = math.max(urgencyB, urgency[index]);
            }

            // A group may start only when the other one is a complete base.
            var allowedA = (eligible & plantedMaskA) != 0u && allPlantedB;
            var allowedB = (eligible & plantedMaskB) != 0u && allPlantedA;
            if (allowedA && allowedB)
                return urgencyA >= urgencyB ? plantedMaskA : plantedMaskB;

            if (allowedA)
                return plantedMaskA;

            return allowedB ? plantedMaskB : 0u;
        }

        /// <summary>The wave cadence: the cursor names the only leg allowed to ask.</summary>
        static uint PermitCursor(
            uint eligible,
            int plantedCount,
            int minimumPlantedFeet,
            int cursorLegIndex,
            int legCount)
        {
            if (cursorLegIndex < 0 || cursorLegIndex >= legCount)
                return 0u;
            if ((eligible & (1u << cursorLegIndex)) == 0u)
                return 0u;
            if (plantedCount - 1 < minimumPlantedFeet)
                return 0u;

            return 1u << cursorLegIndex;
        }
    }
}
