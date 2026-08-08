using NUnit.Framework;
using Tealeaf.ProceduralAnimation.Dots.LowLevel;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots.Tests
{
    /// <summary>
    /// The top-down branch. A planar creature keeps every promise the side-view one made — a
    /// committed plant, a led target, a swing phase — and changes only the frame those promises
    /// live in: heading-relative homes, walkability instead of a floor normal, and lift that is
    /// drawn rather than simulated.
    /// </summary>
    public sealed class PlanarGaitTests
    {
        static readonly Gait DefaultGait = new()
        {
            Comfort = 0.5f,
            StepDuration = 0.4f,
            StepLead = 0.1f,
            StepHeight = 0.4f,
            MinimumSupport = 0.7f,
            MinimumForward = 0f,
        };

        // ----------------------------------------------------------------------------------
        // Lesson 16 — the plane is ground
        // ----------------------------------------------------------------------------------

        [Test]
        public void PlanarHome_RotatesWithTheBodyHeading()
        {
            var hip = new float2(2f, 3f);
            var localHome = new float2(1f, 0.5f);

            var facingRight = PlanarMath.Home(hip, localHome, new float2(1f, 0f));
            var facingUp = PlanarMath.Home(hip, localHome, new float2(0f, 1f));

            // Facing +X the authored offset is world-aligned, which is why baked rest plants
            // survive the switch to a planar creature untouched.
            Assert.That(facingRight.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(facingRight.y, Is.EqualTo(3.5f).Within(0.0001f));

            // A quarter turn takes the whole home with it: forward becomes +Y, lateral becomes -X.
            Assert.That(facingUp.x, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(facingUp.y, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void PlanarHeading_KeepsTheLastFacingWhenTheCreatureStandsStill()
        {
            var previous = new float2(0f, 1f);

            var standingStill = PlanarMath.Advance(previous, float2.zero, float2.zero);
            var travelling = PlanarMath.Advance(previous, new float2(-2f, 0f), float2.zero);
            var aimed = PlanarMath.Advance(previous, new float2(-2f, 0f), new float2(0f, -1f));

            Assert.That(standingStill, Is.EqualTo(previous));
            Assert.That(travelling.x, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(aimed.y, Is.EqualTo(-1f).Within(0.0001f), "An explicit facing outranks travel.");
        }

        // ----------------------------------------------------------------------------------
        // Lesson 17 — path-aware footholds
        // ----------------------------------------------------------------------------------

        [Test]
        public void PlanarFoothold_RejectsBlockedUnreachableAndBackwardCandidates()
        {
            var hip = float2.zero;
            var home = new float2(1f, 0f);
            var velocity = new float2(1f, 0f);

            Assert.That(Accepts(Candidate(new float2(1.2f, 0f), walkable: 1, pathClear: 1)), Is.True);
            Assert.That(Accepts(Candidate(new float2(1.2f, 0f), walkable: 0, pathClear: 1)), Is.False,
                "A blocked tile is not a foot target, whatever its geometry says.");
            Assert.That(Accepts(Candidate(new float2(1.2f, 0f), walkable: 1, pathClear: 0)), Is.False,
                "A point across a wall is close but unreachable.");
            Assert.That(Accepts(Candidate(new float2(9f, 0f), walkable: 1, pathClear: 1)), Is.False,
                "Outside the IK annulus is an impossible promise.");
            Assert.That(Accepts(Candidate(new float2(0.2f, 0f), walkable: 1, pathClear: 1)), Is.False,
                "Behind the movement policy is a pointless step.");

            // The side-view test is the one thing that does not carry over: a planar candidate
            // carries no meaningful normal, so nothing here depends on one.
            bool Accepts(FootholdCandidate candidate) =>
                GaitStepper.TryChoosePlanarFoothold(
                    candidate, hip, home, velocity,
                    minimumReach: 0.5f, maximumReach: 2f, DefaultGait, out _);
        }

        [Test]
        public void SideViewFootholds_IgnoreTheWalkabilityFactsTheyWereNeverGiven()
        {
            // Existing world-query adapters fill only point and normal. If the side-view path
            // started reading the new bytes, every one of those creatures would freeze.
            var candidate = new FootholdCandidate
            {
                Point = new float2(1.2f, 0f),
                Normal = new float2(0f, 1f),
            };

            var accepted = GaitStepper.TryChooseFoothold(
                candidate, float2.zero, new float2(1f, 0f), new float2(1f, 0f),
                minimumReach: 0.5f, maximumReach: 2f, DefaultGait, out _);

            Assert.That(accepted, Is.True);
        }

        // ----------------------------------------------------------------------------------
        // Lesson 18 / 22 / 23 — who may ask
        // ----------------------------------------------------------------------------------

        [Test]
        public void PartnerRule_StillStopsAPairFromLiftingTogether()
        {
            using var legs = Legs(
                Planted(partner: 1),
                Planted(partner: 0),
                Planted(partner: 3),
                Planted(partner: 2));
            using var urgency = Urgency(1f, 0.9f, 0.8f, 0.7f);

            var permitted = GaitPermission.Permitted(
                legs, urgency, comfort: 0.5f, GaitCadence.Partner, minimumPlantedFeet: 0, cursorLegIndex: -1);

            Assert.That(permitted, Is.EqualTo(0b0101u),
                "One leg of each pair may step; its partner keeps the ground.");
        }

        [Test]
        public void SupportPolicy_GrantsTheMostStressedLegAndOnlyThatLeg()
        {
            using var legs = Legs(Planted(), Planted(), Planted(), Planted());
            using var urgency = Urgency(0.6f, 1.4f, 0.9f, 0.55f);

            var permitted = GaitPermission.Permitted(
                legs, urgency, comfort: 0.5f, GaitCadence.Support, minimumPlantedFeet: 3, cursorLegIndex: -1);

            Assert.That(permitted, Is.EqualTo(0b0010u), "Exactly one commitment per tick: the most urgent.");
        }

        [Test]
        public void SupportPolicy_RefusesTheLiftThatWouldLoseTheBase()
        {
            using var legs = Legs(Planted(), Planted(), Swinging(), Swinging());
            using var urgency = Urgency(1.4f, 1.2f, -1f, -1f);

            var permitted = GaitPermission.Permitted(
                legs, urgency, comfort: 0.5f, GaitCadence.Support, minimumPlantedFeet: 2, cursorLegIndex: -1);

            Assert.That(permitted, Is.EqualTo(0u),
                "A stressed foot waits rather than leaving fewer feet planted than the policy allows.");
        }

        [Test]
        public void Tripod_MovesOneDiagonalGroupAndNeverWakesTheBase()
        {
            // Six legs, alternating diagonal groups: 0, 3, 4 against 1, 2, 5.
            using var legs = Legs(
                Planted(group: 0), Planted(group: 1), Planted(group: 1),
                Planted(group: 0), Planted(group: 0), Planted(group: 1));
            using var urgency = Urgency(1.2f, 0.8f, 0.9f, 1.1f, 0.7f, 0.6f);

            var permitted = GaitPermission.Permitted(
                legs, urgency, comfort: 0.5f, GaitCadence.Tripod, minimumPlantedFeet: 0, cursorLegIndex: -1);

            Assert.That(permitted, Is.EqualTo(0b011001u),
                "The whole urgent tripod is permitted together, and only that tripod.");
        }

        [Test]
        public void Tripod_MovesTheWholeGroupIncludingItsUnstressedLegs()
        {
            // Only leg 0 has drifted past comfort, but a tripod is a rhythm: its legs lift and
            // land together, or the group is never all planted at once and its opposite starves.
            using var legs = Legs(
                Planted(group: 0), Planted(group: 1), Planted(group: 1),
                Planted(group: 0), Planted(group: 0), Planted(group: 1));
            using var urgency = Urgency(1.2f, 0.05f, 0.02f, 0.1f, 0.04f, 0.01f);

            var permitted = GaitPermission.Permitted(
                legs, urgency, comfort: 0.5f, GaitCadence.Tripod, minimumPlantedFeet: 0, cursorLegIndex: -1);

            Assert.That(permitted, Is.EqualTo(0b011001u));
        }

        [Test]
        public void Tripod_HoldsTheOpposingGroupWhileTheFirstIsAirborne()
        {
            using var legs = Legs(
                Swinging(group: 0), Planted(group: 1), Planted(group: 1),
                Planted(group: 0), Planted(group: 0), Planted(group: 1));
            using var urgency = Urgency(-1f, 5f, 5f, 1.1f, 0.7f, 5f);

            var permitted = GaitPermission.Permitted(
                legs, urgency, comfort: 0.5f, GaitCadence.Tripod, minimumPlantedFeet: 0, cursorLegIndex: -1);

            Assert.That(permitted & 0b100110u, Is.EqualTo(0u),
                "However stressed group B is, it is the base until group A lands.");
            Assert.That(permitted, Is.EqualTo(0b011000u),
                "The rest of the airborne group may still join its own tripod.");
        }

        [Test]
        public void WaveCursor_PermitsItsOwnLegAndNoOther()
        {
            using var legs = Legs(Planted(), Planted(), Planted(), Planted());
            using var urgency = Urgency(2f, 0.9f, 3f, 2.5f);

            var permitted = GaitPermission.Permitted(
                legs, urgency, comfort: 0.5f, GaitCadence.Wave, minimumPlantedFeet: 0, cursorLegIndex: 1);

            Assert.That(permitted, Is.EqualTo(0b0010u),
                "The cursor names the leg; stress elsewhere does not jump the queue.");
        }

        // ----------------------------------------------------------------------------------
        // Lesson 19 — lift is a picture
        // ----------------------------------------------------------------------------------

        [Test]
        public void PlanarSwingTarget_NeverLeavesTheCommittedSegment()
        {
            var leg = new GaitLeg
            {
                SwingFrom = new float2(-1f, -2f),
                SwingTo = new float2(3f, 1f),
            };
            var segment = math.normalize(leg.SwingTo - leg.SwingFrom);

            for (var step = 0; step <= 20; step++)
            {
                leg.SwingT = step / 20f;
                var target = GaitStepper.EvaluatePlanarSwingTarget(leg);
                var offset = target - leg.SwingFrom;
                var deviation = offset.x * segment.y - offset.y * segment.x;

                Assert.That(deviation, Is.EqualTo(0f).Within(0.0001f),
                    "A top-down swing target is on the plane at every phase; its arc is drawn, not simulated.");
            }

            leg.SwingT = 0.5f;
            Assert.That(
                GaitStepper.EvaluateSwingTarget(leg, DefaultGait).y,
                Is.GreaterThan(GaitStepper.EvaluatePlanarSwingTarget(leg).y),
                "The side-view arc is unchanged: there, the lift really is world geometry.");
        }

        [Test]
        public void FootPresentation_LiftsTheSpriteWithoutMovingTheFoot()
        {
            var planarFoot = new float2(2f, -1f);
            var policy = new FootPresentationPolicy
            {
                VisualStepHeight = 0.5f,
                ScreenUp = new float2(0f, 1f),
                SortScale = 1f,
                SwingSortBias = 10f,
            };

            var start = FootPresentationMath.Derive(planarFoot, FootState.Swinging, 0f, policy);
            var middle = FootPresentationMath.Derive(planarFoot, FootState.Swinging, 0.5f, policy);
            var planted = FootPresentationMath.Derive(planarFoot, FootState.Planted, 0f, policy);

            Assert.That(start.VisualLift, Is.EqualTo(0f).Within(0.0001f), "Lift is zero at liftoff.");
            Assert.That(middle.VisualLift, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(middle.ShadowPoint, Is.EqualTo(planarFoot), "The shadow stays on the committed point.");
            Assert.That(middle.FootPoint.y, Is.EqualTo(planarFoot.y + 0.5f).Within(0.0001f));

            // The sort key comes from the planar point, so a foot cannot sort itself behind a wall
            // halfway through its own arc.
            Assert.That(middle.SortKey - policy.SwingSortBias, Is.EqualTo(planted.SortKey).Within(0.0001f));
        }

        // ----------------------------------------------------------------------------------
        // Lessons 20 / 21 / 23 / 24 / 25 — the whole tick
        // ----------------------------------------------------------------------------------

        [Test]
        public void TurningOnTheSpotMakesOneStressedFootStepWhileTheRestHold()
        {
            using var world = new World(nameof(TurningOnTheSpotMakesOneStressedFootStepWhileTheRestHold));
            var entity = CreateHexapod(world, GaitCadence.Support, minimumPlantedFeet: 4);
            var manager = world.EntityManager;

            // No translation at all — only a quarter turn. Heading-relative homes swing away from
            // the plants, which is the whole reason a stationary turn needs a step.
            manager.SetComponentData(entity, new CreatureLocomotion { DesiredHeading = new float2(0f, 1f) });

            // One tick with no candidates published: the heading settles and nothing may step,
            // so the adapter below samples the homes gait is about to use.
            Tick(world);

            var plantsBefore = Plants(manager, entity);
            PublishCandidatesAtHomes(manager, entity);
            Tick(world);

            var legs = manager.GetBuffer<GaitLeg>(entity);
            var swinging = 0;
            for (var index = 0; index < legs.Length; index++)
            {
                if (legs[index].State == FootState.Swinging)
                {
                    swinging++;
                    continue;
                }

                Assert.That(legs[index].Plant, Is.EqualTo(plantsBefore[index]),
                    "A planted foot does not slide toward its new home; it waits for its turn.");
            }

            Assert.That(swinging, Is.EqualTo(1), "Exactly one commitment per tick under the support policy.");
        }

        [Test]
        public void ABlockedCursorLegHoldsItsPlantAndAsksLocomotionForRecovery()
        {
            using var world = new World(nameof(ABlockedCursorLegHoldsItsPlantAndAsksLocomotionForRecovery));
            var entity = CreateHexapod(world, GaitCadence.Wave, minimumPlantedFeet: 0);
            var manager = world.EntityManager;
            manager.SetComponentData(entity, new CreatureLocomotion { DesiredHeading = new float2(0f, 1f) });

            // One tick with no candidates published: the heading settles and nothing may step,
            // so the adapter below samples the homes gait is about to use.
            Tick(world);

            var cursorLeg = manager.GetBuffer<WaveOrder>(entity)[0].LegIndex;
            var plantBefore = manager.GetBuffer<GaitLeg>(entity)[cursorLeg].Plant;

            for (var tick = 0; tick < 5; tick++)
            {
                PublishCandidatesAtHomes(manager, entity, blockedLegIndex: cursorLeg);
                Tick(world);
            }

            var legs = manager.GetBuffer<GaitLeg>(entity);
            var recovery = manager.GetComponentData<GaitRecoveryRequest>(entity);

            Assert.That(legs[cursorLeg].State, Is.EqualTo(FootState.Planted));
            Assert.That(legs[cursorLeg].Plant, Is.EqualTo(plantBefore),
                "No legal foothold means the old promise stands — not a teleport to the nearest blocked point.");
            Assert.That(manager.GetComponentData<WaveGaitState>(entity).Cursor, Is.EqualTo(0),
                "The cursor holds its place rather than skipping to a foot that can move.");
            Assert.That(recovery.State, Is.EqualTo(GaitRecovery.HoldingForFoothold));
            Assert.That(recovery.SlowDown, Is.EqualTo(1));
            Assert.That(recovery.BlockedLegIndex, Is.EqualTo(cursorLeg));

            // The suggested heading has to lean away from the side that ran out of ground. Turning
            // to relieve the blocked leg's stress instead points back into whatever blocked it.
            var forward = manager.GetComponentData<PlanarHeading>(entity).LastForward;
            var blockedSide = PlanarMath.Perpendicular(forward)
                              * math.sign(legs[cursorLeg].HomeOffset.y);
            Assert.That(math.dot(recovery.PreferredTurn, blockedSide), Is.LessThan(0f),
                "Recovery should turn away from the blocked leg, not toward it.");

            for (var index = 0; index < legs.Length; index++)
                Assert.That(legs[index].State, Is.EqualTo(FootState.Planted),
                    "The wave cadence never lets another leg take the blocked leg's turn.");
        }

        [Test]
        public void TheWaveCursorAdvancesOnlyWhenItsLegLands()
        {
            using var world = new World(nameof(TheWaveCursorAdvancesOnlyWhenItsLegLands));
            var entity = CreateHexapod(world, GaitCadence.Wave, minimumPlantedFeet: 0);
            var manager = world.EntityManager;
            manager.SetComponentData(entity, new CreatureLocomotion { DesiredHeading = new float2(0f, 1f) });

            // One tick with no candidates published: the heading settles and nothing may step,
            // so the adapter below samples the homes gait is about to use.
            Tick(world);

            var order = manager.GetBuffer<WaveOrder>(entity);
            var firstLeg = order[0].LegIndex;
            var secondLeg = order[1].LegIndex;

            PublishCandidatesAtHomes(manager, entity);
            Tick(world);

            Assert.That(manager.GetBuffer<GaitLeg>(entity)[firstLeg].State, Is.EqualTo(FootState.Swinging));
            Assert.That(manager.GetComponentData<WaveGaitState>(entity).Cursor, Is.EqualTo(0),
                "A swing in progress does not move the cursor.");

            // Run the swing out. The cursor may move exactly once, at the landing.
            for (var tick = 0; tick < 30; tick++)
            {
                PublishCandidatesAtHomes(manager, entity);
                Tick(world);
                if (manager.GetComponentData<WaveGaitState>(entity).Cursor != 0)
                    break;
            }

            Assert.That(manager.GetComponentData<WaveGaitState>(entity).Cursor, Is.EqualTo(1));
            Assert.That(manager.GetBuffer<GaitLeg>(entity)[secondLeg].State, Is.EqualTo(FootState.Swinging),
                "The next leg in the authored crawl order takes its turn.");
        }

        [Test]
        public void ACadenceChangeWaitsForEveryFootToLandAndKeepsEveryPlant()
        {
            using var world = new World(nameof(ACadenceChangeWaitsForEveryFootToLandAndKeepsEveryPlant));
            var entity = CreateHexapod(world, GaitCadence.Wave, minimumPlantedFeet: 0);
            var manager = world.EntityManager;
            manager.SetComponentData(entity, new GaitSupportPolicy
            {
                MinimumPlantedFeet = 0,
                SlowCadence = GaitCadence.Wave,
                FastCadence = GaitCadence.Tripod,
                EnterSpeed = 1.2f,
                ExitSpeed = 0.7f,
            });
            manager.SetComponentData(entity, new CreatureLocomotion { DesiredHeading = new float2(0f, 1f) });

            // One tick with no candidates published: the heading settles and nothing may step,
            // so the adapter below samples the homes gait is about to use.
            Tick(world);

            PublishCandidatesAtHomes(manager, entity);
            Tick(world);
            Assert.That(AnySwinging(manager, entity), Is.True, "This test needs a foot in the air.");

            // Acceleration arrives mid-swing.
            manager.SetComponentData(entity, new CreatureLocomotion
            {
                DesiredVelocity = new float2(0f, 2f),
                DesiredHeading = new float2(0f, 1f),
            });
            PublishCandidatesAtHomes(manager, entity);
            Tick(world);

            var duringSwing = manager.GetComponentData<GaitCadenceState>(entity);
            var plantsDuringSwing = Plants(manager, entity);
            Assert.That(duringSwing.Active, Is.EqualTo(GaitCadence.Wave), "The active policy waits.");
            Assert.That(duringSwing.Pending, Is.EqualTo(GaitCadence.Tripod), "The request is recorded, not applied.");

            for (var tick = 0; tick < 40 && AnySwinging(manager, entity); tick++)
            {
                PublishCandidatesAtHomes(manager, entity);
                Tick(world);
            }

            var afterLanding = manager.GetComponentData<GaitCadenceState>(entity);
            Assert.That(afterLanding.Active, Is.EqualTo(GaitCadence.Tripod),
                "The switch happens at an honest synchronisation point.");

            var legs = manager.GetBuffer<GaitLeg>(entity);
            for (var index = 0; index < legs.Length; index++)
            {
                if (legs[index].State != FootState.Planted)
                    continue;

                // The leg that was airborne landed on its own committed target; every other plant
                // is untouched by the policy change.
                if (math.all(plantsDuringSwing[index] == legs[index].Plant))
                    continue;

                Assert.That(legs[index].Plant, Is.EqualTo(legs[index].SwingTo),
                    "Only a landing may change a plant — never a cadence switch.");
            }
        }

        [Test]
        public void APlanarPlantFollowsItsMovingSupportWithoutAFreshQuery()
        {
            using var world = new World(nameof(APlanarPlantFollowsItsMovingSupportWithoutAFreshQuery));
            var entity = CreateHexapod(world, GaitCadence.Support, minimumPlantedFeet: 5);
            var manager = world.EntityManager;

            var support = manager.CreateEntity();
            manager.AddComponentData(support, new SupportPose { Position = float2.zero, RotationRadians = 0f });
            manager.AddComponentData(support, new SupportKinematics
            {
                LinearVelocity = new float2(1f, 0f),
                SurfaceVelocityLocal = new float2(0f, 0.5f),
            });

            var legs = manager.GetBuffer<GaitLeg>(entity);
            var leg = legs[0];
            var localPlant = leg.Plant;
            leg.Support = support;
            leg.LocalPlant = localPlant;
            legs[0] = leg;

            const float deltaTime = 0.02f;
            Tick(world, deltaTime);
            manager.SetComponentData(support, new SupportPose
            {
                Position = new float2(deltaTime, 0f),
                RotationRadians = 0f,
            });
            Tick(world, deltaTime);

            var planted = manager.GetBuffer<GaitLeg>(entity)[0];
            var expected = localPlant + new float2(deltaTime, 2f * deltaTime * 0.5f);

            Assert.That(planted.State, Is.EqualTo(FootState.Planted));
            Assert.That(planted.Plant.x, Is.EqualTo(expected.x).Within(0.0001f),
                "The support moved the coordinates the plant is expressed in.");
            Assert.That(planted.Plant.y, Is.EqualTo(expected.y).Within(0.0001f),
                "The belt moved material through them, and the foot travelled with it.");
            Assert.That(manager.GetBuffer<Limb2BoneLeg>(entity)[0].Limb.Target, Is.EqualTo(planted.Plant),
                "IK chases the resolved plant, not a remembered world point.");
        }

        [Test]
        public void ASideViewCreatureStillWalksThroughTheRewrittenGaitStage()
        {
            // The regression that matters most: the shipped creature has none of the planar
            // components, and its adapter fills only point and normal. It must walk exactly as
            // it did — alternating, arcing over the ground, landing on its committed target.
            using var world = new World(nameof(ASideViewCreatureStillWalksThroughTheRewrittenGaitStage));
            var entity = CreateSideViewBiped(world);
            var manager = world.EntityManager;
            manager.SetComponentData(entity, new CreatureLocomotion { DesiredVelocity = new float2(1f, 0f) });

            var swingingLeg = -1;
            for (var tick = 0; tick < 120 && swingingLeg < 0; tick++)
            {
                PublishSideViewCandidates(manager, entity);
                Tick(world);
                var legs = manager.GetBuffer<GaitLeg>(entity);
                for (var index = 0; index < legs.Length; index++)
                {
                    if (legs[index].State == FootState.Swinging)
                        swingingLeg = index;
                }
            }

            Assert.That(swingingLeg, Is.GreaterThanOrEqualTo(0), "Walking forward has to make a foot step.");
            Assert.That(manager.GetBuffer<GaitLeg>(entity)[swingingLeg ^ 1].State, Is.EqualTo(FootState.Planted),
                "The partner guard still keeps one foot on the ground.");

            PublishSideViewCandidates(manager, entity);
            Tick(world);

            var swinging = manager.GetBuffer<GaitLeg>(entity)[swingingLeg];
            var onSegment = math.lerp(
                swinging.SwingFrom, swinging.SwingTo, math.smoothstep(0f, 1f, swinging.SwingT));
            Assert.That(swinging.SwingT, Is.GreaterThan(0f).And.LessThan(1f), "This assertion needs a live swing.");
            Assert.That(manager.GetBuffer<Limb2BoneLeg>(entity)[swingingLeg].Limb.Target.y,
                Is.GreaterThan(onSegment.y),
                "A side-view swing still arcs over the ground it steps across.");

            var committed = swinging.SwingTo;
            for (var tick = 0; tick < 60; tick++)
            {
                PublishSideViewCandidates(manager, entity);
                Tick(world);
                if (manager.GetBuffer<GaitLeg>(entity)[swingingLeg].State == FootState.Planted)
                    break;
            }

            var landed = manager.GetBuffer<GaitLeg>(entity)[swingingLeg];
            Assert.That(landed.State, Is.EqualTo(FootState.Planted));
            Assert.That(landed.Plant, Is.EqualTo(committed), "A foot lands on the target it committed to.");
        }

        // ----------------------------------------------------------------------------------
        // Lessons 29 / 30 — the plant contract, stated as assertions
        // ----------------------------------------------------------------------------------

        [Test]
        public void ATurningBodyMovesEveryHomeAndNoCommittedPlant()
        {
            // Lesson 29's experiment, run as a test. The support policy is set tight enough that
            // no leg may lift at all, which leaves nothing on stage but the contract: homes are
            // recomputed from the resolved body, plants are remembered from a past landing.
            using var world = new World(nameof(ATurningBodyMovesEveryHomeAndNoCommittedPlant));
            var entity = CreateHexapod(world, GaitCadence.Support, minimumPlantedFeet: 6);
            var manager = world.EntityManager;

            var plantsBefore = Plants(manager, entity);
            var homesBefore = Homes(manager, entity);

            // A quarter turn on the spot. No candidates are published, because a planted foot is
            // not supposed to need one.
            manager.SetComponentData(entity, new CreatureLocomotion { DesiredHeading = new float2(0f, 1f) });
            for (var tick = 0; tick < 5; tick++)
                Tick(world);

            var legs = manager.GetBuffer<GaitLeg>(entity);
            var homesAfter = Homes(manager, entity);

            for (var index = 0; index < legs.Length; index++)
            {
                Assert.That(legs[index].State, Is.EqualTo(FootState.Planted), "Nothing was permitted to step.");
                Assert.That(legs[index].Plant, Is.EqualTo(plantsBefore[index]),
                    "A committed plant is a world point. Recomputing it from the body is the skating bug.");
                Assert.That(math.distance(homesAfter[index], homesBefore[index]),
                    Is.GreaterThan(DefaultGait.Comfort),
                    "The home rotated with the heading — that gap is the evidence gait reads.");
                Assert.That(math.distance(legs[index].Plant, homesAfter[index]),
                    Is.GreaterThan(DefaultGait.Comfort),
                    "Stress past comfort is a reason to request a swing, not permission to move a plant.");
            }
        }

        [Test]
        public void AReservedFootholdSurvivesATurnAndABetterLateOption()
        {
            using var world = new World(nameof(AReservedFootholdSurvivesATurnAndABetterLateOption));
            var entity = CreateHexapod(world, GaitCadence.Support, minimumPlantedFeet: 0);
            var manager = world.EntityManager;
            manager.SetComponentData(entity, new CreatureLocomotion { DesiredHeading = new float2(0f, 1f) });

            // One tick with no candidates published: the heading settles and nothing may step,
            // so the adapter below samples the homes gait is about to use.
            Tick(world);

            PublishCandidatesAtHomes(manager, entity);
            Tick(world);

            var swingingLeg = FirstSwingingLeg(manager, entity);
            Assert.That(swingingLeg, Is.GreaterThanOrEqualTo(0), "This test needs a foot in the air.");
            var reserved = manager.GetBuffer<GaitLeg>(entity)[swingingLeg].SwingTo;

            // Mid-swing the world changes its mind twice over: the creature keeps turning, and the
            // only candidate on offer is now a different, perfectly legal point. A foot that
            // re-queries while airborne chases it; a foot that reserved one point does not.
            manager.SetComponentData(entity, new CreatureLocomotion { DesiredHeading = new float2(-1f, 0f) });
            var tempting = reserved + new float2(0.35f, -0.35f);

            var landed = false;
            for (var tick = 0; tick < 60 && !landed; tick++)
            {
                PublishOneCandidate(manager, entity, swingingLeg, tempting);
                Tick(world);

                var leg = manager.GetBuffer<GaitLeg>(entity)[swingingLeg];
                Assert.That(leg.SwingTo, Is.EqualTo(reserved),
                    "A query is a transition, not a subscription.");
                landed = leg.State == FootState.Planted;
            }

            Assert.That(landed, Is.True, "The swing has to finish for the landing assertion to mean anything.");
            Assert.That(manager.GetBuffer<GaitLeg>(entity)[swingingLeg].Plant, Is.EqualTo(reserved),
                "Landing is the one ordinary transition that replaces a plant — with exactly the reserved point.");
        }

        // ----------------------------------------------------------------------------------
        // Lesson 27 — the new fact has to be inert
        // ----------------------------------------------------------------------------------

        [Test]
        public void ARequestedTurnPublishesIntentAndSteersNothing()
        {
            // A field that presentation reads is a field a solver could start reading too. This is
            // the guard: publishing a hard left changes the heading by nothing at all.
            using var world = new World(nameof(ARequestedTurnPublishesIntentAndSteersNothing));
            var entity = CreateHexapod(world, GaitCadence.Support, minimumPlantedFeet: 0);
            var manager = world.EntityManager;

            var headingBefore = manager.GetComponentData<PlanarHeading>(entity).LastForward;
            var plantsBefore = Plants(manager, entity);

            manager.SetComponentData(entity, new CreatureLocomotion { RequestedTurnSign = 1f });
            for (var tick = 0; tick < 10; tick++)
            {
                PublishCandidatesAtHomes(manager, entity);
                Tick(world);
            }

            Assert.That(manager.GetComponentData<PlanarHeading>(entity).LastForward, Is.EqualTo(headingBefore),
                "A requested turn is a fact for the picture. Turning is still locomotion's own job.");
            Assert.That(Plants(manager, entity), Is.EqualTo(plantsBefore));
            Assert.That(manager.GetComponentData<CreatureBody>(entity).RootPosition, Is.EqualTo(float2.zero));
        }

        // ----------------------------------------------------------------------------------
        // Fixtures
        // ----------------------------------------------------------------------------------

        /// <summary>
        /// The pre-existing adapter shape: a point and a normal, and neither of the two facts a
        /// planar creature reads. They stay at their zero defaults on purpose — if the side-view
        /// path ever started testing them, every creature built before this change would freeze.
        /// </summary>
        static void PublishSideViewCandidates(EntityManager manager, Entity entity)
        {
            var gait = manager.GetComponentData<Gait>(entity);
            var velocity = manager.GetComponentData<CreatureLocomotion>(entity).DesiredVelocity;
            var legs = manager.GetBuffer<GaitLeg>(entity);
            var limbs = manager.GetBuffer<Limb2BoneLeg>(entity);
            var points = manager.GetBuffer<VerletPoint>(entity);
            var candidates = manager.GetBuffer<FootholdCandidate>(entity);
            candidates.Clear();

            for (var index = 0; index < legs.Length; index++)
            {
                var hip = points[limbs[index].RootPointIndex].Position;
                candidates.Add(new FootholdCandidate
                {
                    LegIndex = (byte)index,
                    Point = hip + legs[index].HomeOffset + velocity * gait.StepLead,
                    Normal = new float2(0f, 1f),
                });
            }
        }

        static Entity CreateSideViewBiped(World world)
        {
            const float restLength = 1f;
            var manager = world.EntityManager;
            var entity = manager.CreateEntity();

            manager.AddComponentData(entity, new VerletChain
            {
                RestLength = restLength,
                Damping = 1f,
                Gravity = float2.zero,
            });
            manager.AddComponentData(entity, new CreatureBody { RootPosition = float2.zero });
            manager.AddComponentData(entity, new CreatureLocomotion());
            manager.AddComponentData(entity, DefaultGait);

            manager.AddBuffer<VerletPoint>(entity);
            manager.AddBuffer<Limb2BoneLeg>(entity);
            manager.AddBuffer<GaitLeg>(entity);
            manager.AddBuffer<FootholdCandidate>(entity);

            var points = manager.GetBuffer<VerletPoint>(entity);
            for (var index = 0; index < 3; index++)
            {
                var position = CreatureLayout.PointPosition(float2.zero, restLength, index);
                points.Add(new VerletPoint { Position = position, PreviousPosition = position });
            }

            var limbs = manager.GetBuffer<Limb2BoneLeg>(entity);
            var gaitLegs = manager.GetBuffer<GaitLeg>(entity);
            var homeOffset = new float2(0f, -1.5f);
            for (var index = 0; index < 2; index++)
            {
                var hip = CreatureLayout.PointPosition(float2.zero, restLength, index);
                limbs.Add(new Limb2BoneLeg
                {
                    RootPointIndex = index,
                    Limb = new Limb2Bone { LengthA = 1f, LengthB = 1f, BendSign = 1f, Target = hip + homeOffset },
                });
                gaitLegs.Add(new GaitLeg
                {
                    State = FootState.Planted,
                    Plant = hip + homeOffset,
                    HomeOffset = homeOffset,
                    PartnerIndex = (sbyte)(index ^ 1),
                });
            }

            return entity;
        }

        static bool AnySwinging(EntityManager manager, Entity entity)
        {
            var legs = manager.GetBuffer<GaitLeg>(entity);
            for (var index = 0; index < legs.Length; index++)
            {
                if (legs[index].State == FootState.Swinging)
                    return true;
            }

            return false;
        }

        static float2[] Plants(EntityManager manager, Entity entity)
        {
            var legs = manager.GetBuffer<GaitLeg>(entity);
            var plants = new float2[legs.Length];
            for (var index = 0; index < legs.Length; index++)
                plants[index] = legs[index].Plant;

            return plants;
        }

        /// <summary>
        /// Where neutral feet would prefer to be right now — derived from the resolved body and
        /// heading, and therefore expected to move whenever either of them does.
        /// </summary>
        static float2[] Homes(EntityManager manager, Entity entity)
        {
            var forward = manager.GetComponentData<PlanarHeading>(entity).LastForward;
            var legs = manager.GetBuffer<GaitLeg>(entity);
            var limbs = manager.GetBuffer<Limb2BoneLeg>(entity);
            var points = manager.GetBuffer<VerletPoint>(entity);
            var homes = new float2[legs.Length];
            for (var index = 0; index < legs.Length; index++)
            {
                var hip = points[limbs[index].RootPointIndex].Position;
                homes[index] = PlanarMath.Home(hip, legs[index].HomeOffset, forward);
            }

            return homes;
        }

        static int FirstSwingingLeg(EntityManager manager, Entity entity)
        {
            var legs = manager.GetBuffer<GaitLeg>(entity);
            for (var index = 0; index < legs.Length; index++)
            {
                if (legs[index].State == FootState.Swinging)
                    return index;
            }

            return -1;
        }

        /// <summary>
        /// A world that offers one leg exactly one legal point. Used to tempt a foot that is
        /// already airborne, which is the only way to catch a swing that re-queries.
        /// </summary>
        static void PublishOneCandidate(EntityManager manager, Entity entity, int legIndex, float2 point)
        {
            var candidates = manager.GetBuffer<FootholdCandidate>(entity);
            candidates.Clear();
            candidates.Add(new FootholdCandidate
            {
                LegIndex = (byte)legIndex,
                Point = point,
                Normal = new float2(0f, 1f),
                Walkable = 1,
                PathClear = 1,
            });
        }

        /// <summary>
        /// Stands in for a planar world-query adapter: one candidate per leg at that leg's
        /// current heading-relative home, optionally with one leg's only option blocked.
        /// </summary>
        static void PublishCandidatesAtHomes(EntityManager manager, Entity entity, int blockedLegIndex = -1)
        {
            var forward = manager.GetComponentData<PlanarHeading>(entity).LastForward;
            var legs = manager.GetBuffer<GaitLeg>(entity);
            var limbs = manager.GetBuffer<Limb2BoneLeg>(entity);
            var points = manager.GetBuffer<VerletPoint>(entity);
            var candidates = manager.GetBuffer<FootholdCandidate>(entity);
            candidates.Clear();

            for (var index = 0; index < legs.Length; index++)
            {
                var hip = points[limbs[index].RootPointIndex].Position;
                candidates.Add(new FootholdCandidate
                {
                    LegIndex = (byte)index,
                    Point = PlanarMath.Home(hip, legs[index].HomeOffset, forward),
                    Normal = new float2(0f, 1f),
                    Walkable = (byte)(index == blockedLegIndex ? 0 : 1),
                    PathClear = 1,
                });
            }
        }

        /// <summary>
        /// A six-legged creature on a three-point body, built without going through baking so the
        /// test exercises the runtime contract rather than the authoring one.
        /// </summary>
        static Entity CreateHexapod(World world, GaitCadence cadence, int minimumPlantedFeet)
        {
            const float restLength = 1f;
            const int pointCount = 3;
            var manager = world.EntityManager;
            var entity = manager.CreateEntity();

            manager.AddComponentData(entity, new VerletChain
            {
                RestLength = restLength,
                Damping = 1f,
                Gravity = float2.zero,
            });
            manager.AddComponentData(entity, new CreatureBody { RootPosition = float2.zero });
            manager.AddComponentData(entity, new CreatureLocomotion());
            manager.AddComponentData(entity, DefaultGait);
            manager.AddComponentData(entity, new PlanarHeading { LastForward = new float2(1f, 0f) });
            manager.AddComponentData(entity, new GaitCadenceState { Active = cadence, Pending = cadence });
            manager.AddComponentData(entity, new GaitSupportPolicy
            {
                MinimumPlantedFeet = (byte)minimumPlantedFeet,
                SlowCadence = cadence,
                FastCadence = cadence,
                EnterSpeed = float.MaxValue,
                ExitSpeed = 0f,
            });
            manager.AddComponentData(entity, new WaveGaitState { Cursor = 0 });
            manager.AddComponentData(entity, new GaitRecoveryRequest { BlockedLegIndex = 255 });

            // Every buffer is created before any is filled: adding one is a structural change,
            // and a DynamicBuffer fetched before it goes stale.
            manager.AddBuffer<VerletPoint>(entity);
            manager.AddBuffer<Limb2BoneLeg>(entity);
            manager.AddBuffer<GaitLeg>(entity);
            manager.AddBuffer<FootholdCandidate>(entity);
            manager.AddBuffer<WaveOrder>(entity);

            var points = manager.GetBuffer<VerletPoint>(entity);
            for (var index = 0; index < pointCount; index++)
            {
                var position = CreatureLayout.PointPosition(float2.zero, restLength, index);
                points.Add(new VerletPoint { Position = position, PreviousPosition = position });
            }

            var limbs = manager.GetBuffer<Limb2BoneLeg>(entity);
            var gaitLegs = manager.GetBuffer<GaitLeg>(entity);
            for (var index = 0; index < 6; index++)
            {
                var attachment = index / 2;
                var lateral = (index % 2 == 0 ? 1f : -1f) * 0.8f;
                var localHome = new float2(0f, lateral);
                var hip = CreatureLayout.PointPosition(float2.zero, restLength, attachment);

                limbs.Add(new Limb2BoneLeg
                {
                    RootPointIndex = attachment,
                    Limb = new Limb2Bone { LengthA = 1f, LengthB = 1f, BendSign = 1f, Target = hip + localHome },
                });
                gaitLegs.Add(new GaitLeg
                {
                    State = FootState.Planted,
                    Plant = hip + localHome,
                    HomeOffset = localHome,
                    PartnerIndex = (sbyte)(index ^ 1),
                    // Alternating diagonal tripods: front-left, middle-right, rear-left.
                    TripodGroup = (byte)((index / 2 + index % 2) % 2),
                });
            }

            var waveOrder = manager.GetBuffer<WaveOrder>(entity);
            foreach (var legIndex in new byte[] { 0, 3, 4, 5, 2, 1 })
                waveOrder.Add(new WaveOrder { LegIndex = legIndex });

            return entity;
        }

        static void Tick(World world, float deltaTime = 0.02f)
        {
            world.SetTime(new TimeData(0.02, deltaTime));
            world.GetOrCreateSystemManaged<ProceduralAnimationSolveSystemGroup>().Update();
        }

        static FootholdCandidate Candidate(float2 point, byte walkable, byte pathClear) => new()
        {
            Point = point,
            Normal = new float2(0f, 1f),
            Walkable = walkable,
            PathClear = pathClear,
        };

        static GaitLeg Planted(int partner = -1, int group = 0) => new()
        {
            State = FootState.Planted,
            PartnerIndex = (sbyte)partner,
            TripodGroup = (byte)group,
        };

        static GaitLeg Swinging(int partner = -1, int group = 0) => new()
        {
            State = FootState.Swinging,
            PartnerIndex = (sbyte)partner,
            TripodGroup = (byte)group,
        };

        static NativeArray<GaitLeg> Legs(params GaitLeg[] legs) =>
            new(legs, Allocator.Temp);

        static NativeArray<float> Urgency(params float[] urgency) =>
            new(urgency, Allocator.Temp);
    }
}
