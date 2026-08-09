using Tealeaf.ProceduralAnimation.Dots;
using Tealeaf.ProceduralAnimation.Dots.LowLevel;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TopDownLab
{
    /// <summary>
    /// Read-only presentation for the top-down creature. Every line here is derived from resolved
    /// simulation output; deleting this component changes nothing about how the creature walks.
    ///
    /// It is also where Lesson 19 is visible: the shadow sits on the committed planar point and
    /// the foot marker is the same point pushed along screen-up by a curve that is zero at both
    /// ends of the swing. The lift never travels back into the simulation.
    ///
    /// Lesson 29's debug layer is the same idea turned inward — it draws the gait's own promise
    /// rather than the picture derived from it, in the authority order the lesson lists.
    /// </summary>
    public sealed class TopDownLabDemo : MonoBehaviour
    {
        const int MaximumLegs = 6;
        const int RingSegments = 24;

        [Tooltip("Peak screen-space rise of a swinging foot. Presentation only.")]
        [Min(0f)] public float VisualStepHeight = 0.35f;

        [Tooltip("Orthographic half-height of the following camera.")]
        [Min(0.5f)] public float ViewSize = 3.2f;

        [Tooltip("Lesson 29: draw each committed world plant and its stress line to the current home.")]
        public bool ShowGaitDebug = true;

        [Header("Body language (Lessons 26–28) — presentation only")]
        [Tooltip("Switch the whole filter off. Feet, plants, heading, and collision must be identical either way.")]
        public bool ShowBodyLanguage = true;

        [Tooltip("Degrees of bank per radian per second of resolved turn rate.")]
        [Min(0f)] public float BankGain = 12f;

        [Tooltip("Hard limit on the drawn lean.")]
        [Min(0f)] public float MaxBankDegrees = 22f;

        [Tooltip("Longitudinal stretch per unit of resolved planar speed.")]
        [Min(0f)] public float StretchGain = 0.09f;

        [Tooltip("Lesson 27: degrees of wind-up held against a turn that has been requested but not resolved.")]
        [Min(0f)] public float AnticipationDegrees = 20f;

        [Tooltip("How long that wind-up lasts. Zero disables anticipation.")]
        [Min(0f)] public float AnticipationSeconds = 0.6f;

        [Tooltip("Lesson 28: world units the drawn body lags under acceleration.")]
        [Min(0f)] public float WeightShiftDistance = 0.22f;

        EntityManager entityManager;
        EntityQuery creatureQuery;
        EntityQuery islandQuery;
        LineRenderer bodyLine;
        LineRenderer headingArrow;
        LineRenderer islandRing;
        Camera demoCamera;
        LineRenderer[] legLines;
        LineRenderer[] homeRings;
        LineRenderer[] shadowMarkers;
        LineRenderer[] footMarkers;
        LineRenderer[] candidateFans;
        LineRenderer[] plantMarkers;
        LineRenderer[] stressLines;
        LineRenderer bankArrow;
        BodyPresentationState bodyPresentationState;
        bool isReady;

        static readonly Color PlantedColor = new(0.55f, 0.78f, 1f);
        static readonly Color SwingColor = new(1f, 0.72f, 0.26f);
        static readonly Color HomeColor = new(0.95f, 0.83f, 0.35f);
        static readonly Color ShadowColor = new(0.25f, 0.3f, 0.4f);
        static readonly Color BlockedColor = new(0.92f, 0.38f, 0.3f);
        static readonly Color PlantColor = new(0.85f, 0.94f, 1f);
        static readonly Color ComfortableColor = new(0.48f, 0.79f, 0.76f);
        static readonly Color BodyColor = new(0.4f, 0.9f, 1f);
        static readonly Color BankColor = new(0.86f, 0.6f, 0.95f);

        void Start()
        {
            ConfigureCamera();
            bodyLine = CreateLine("Body", BodyColor, 0.16f);
            headingArrow = CreateLine("Heading", new Color(0.35f, 0.95f, 0.75f), 0.06f);
            bankArrow = CreateLine("Render Bank", BankColor, 0.045f);
            islandRing = CreateLine("Blocked Island", BlockedColor, 0.08f);

            legLines = new LineRenderer[MaximumLegs];
            homeRings = new LineRenderer[MaximumLegs];
            shadowMarkers = new LineRenderer[MaximumLegs];
            footMarkers = new LineRenderer[MaximumLegs];
            candidateFans = new LineRenderer[MaximumLegs];
            plantMarkers = new LineRenderer[MaximumLegs];
            stressLines = new LineRenderer[MaximumLegs];
            for (var index = 0; index < MaximumLegs; index++)
            {
                legLines[index] = CreateLine($"Leg {index}", PlantedColor, 0.07f);
                homeRings[index] = CreateLine($"Home {index}", HomeColor, 0.02f);
                shadowMarkers[index] = CreateLine($"Shadow {index}", ShadowColor, 0.05f);
                footMarkers[index] = CreateLine($"Foot {index}", PlantedColor, 0.07f);
                candidateFans[index] = CreateLine($"Candidates {index}", HomeColor, 0.015f);
                plantMarkers[index] = CreateLine($"Plant {index}", PlantColor, 0.035f);
                stressLines[index] = CreateLine($"Stress {index}", ComfortableColor, 0.025f);
            }

            TryBindWorld();
        }

        void Update()
        {
            if (!TryBindWorld() || creatureQuery.IsEmptyIgnoreFilter)
                return;

            var entity = creatureQuery.GetSingletonEntity();
            var points = entityManager.GetBuffer<VerletPoint>(entity, true);
            var limbs = entityManager.GetBuffer<Limb2BoneLeg>(entity, true);
            var gaitLegs = entityManager.GetBuffer<GaitLeg>(entity, true);
            var debugHits = entityManager.GetBuffer<PlanarQueryDebugHit>(entity, true);
            var heading = entityManager.GetComponentData<PlanarHeading>(entity).LastForward;
            var body = entityManager.GetComponentData<CreatureBody>(entity);
            var comfort = entityManager.GetComponentData<Gait>(entity).Comfort;
            var requestedTurn = entityManager.GetComponentData<CreatureLocomotion>(entity).RequestedTurnSign;
            var recovering = entityManager.HasComponent<GaitRecoveryRequest>(entity)
                && entityManager.GetComponentData<GaitRecoveryRequest>(entity).State != GaitRecovery.None;

            // Lessons 26–28. Everything after this line reads `bodyPresentation` to place pixels
            // and nothing writes back into the entity — the creature has already finished walking.
            var bodyPresentation = BodyPresentationMath.Advance(
                ref bodyPresentationState,
                body.RootPosition,
                heading,
                ShowBodyLanguage ? requestedTurn : 0f,
                BuildBodyPolicy(),
                Time.deltaTime);

            DrawIsland();
            DrawBody(points, body, heading, bodyPresentation, recovering);
            FollowCreature(body);

            var policy = new FootPresentationPolicy
            {
                VisualStepHeight = VisualStepHeight,
                ScreenUp = new float2(0f, 1f),
                SortScale = 1f,
            };

            var legCount = math.min(math.min(limbs.Length, gaitLegs.Length), MaximumLegs);
            for (var index = 0; index < MaximumLegs; index++)
            {
                var active = index < legCount;
                legLines[index].enabled = active;
                homeRings[index].enabled = active;
                shadowMarkers[index].enabled = active;
                footMarkers[index].enabled = active;
                candidateFans[index].enabled = active;
                plantMarkers[index].enabled = active && ShowGaitDebug;
                stressLines[index].enabled = active && ShowGaitDebug;
                if (!active)
                    continue;

                var limb = limbs[index].Limb;
                var leg = gaitLegs[index];
                var swinging = leg.State == FootState.Swinging;

                // One planar truth in, two visible things out.
                var presentation = FootPresentationMath.Derive(limb.Foot, leg.State, leg.SwingT, policy);

                SetColor(legLines[index], swinging ? SwingColor : PlantedColor);
                legLines[index].positionCount = 3;
                legLines[index].SetPosition(0, Flat(limb.Root));
                legLines[index].SetPosition(1, Flat(limb.Knee));
                legLines[index].SetPosition(2, Flat(limb.Foot));

                DrawCross(shadowMarkers[index], presentation.ShadowPoint, 0.09f);
                SetColor(footMarkers[index], swinging ? SwingColor : PlantedColor);
                DrawRing(footMarkers[index], presentation.FootPoint, 0.07f);

                var hip = points[limbs[index].RootPointIndex].Position;
                var home = PlanarMath.Home(hip, leg.HomeOffset, heading);
                DrawRing(homeRings[index], home, 0.11f);
                DrawCandidateFan(candidateFans[index], debugHits, (byte)index);

                if (ShowGaitDebug)
                    DrawGaitDebug(index, leg, home, comfort);
            }
        }

        /// <summary>
        /// Lesson 29. The pale ring is the gait's own world promise — the committed plant, or the
        /// candidate a swinging foot reserved — and it is deliberately <em>not</em> the shadow
        /// above, which draws the IK-resolved foot and so is clamped to the limb's reach.
        ///
        /// The line to the gold home is the stress that makes a step necessary. It turning red is
        /// a reason to request a swing; it staying short through a sharp turn is the skating bug,
        /// because the only thing that could have shortened it is a plant moving with the body.
        /// </summary>
        void DrawGaitDebug(int index, in GaitLeg leg, float2 home, float comfort)
        {
            var swinging = leg.State == FootState.Swinging;
            var committed = swinging ? leg.SwingTo : leg.Plant;

            SetColor(plantMarkers[index], swinging ? SwingColor : PlantColor);
            DrawRing(plantMarkers[index], committed, 0.05f);

            SetColor(stressLines[index],
                math.distance(committed, home) > comfort ? BlockedColor : ComfortableColor);
            stressLines[index].positionCount = 2;
            stressLines[index].SetPosition(0, Flat(committed));
            stressLines[index].SetPosition(1, Flat(home));
        }

        /// <summary>
        /// Lessons 26–28 are visible here as a deliberate disagreement: the green arrow is the
        /// heading locomotion resolved, drawn on the body point it resolved to, and the purple one
        /// is where the picture is currently leaning. The spine bends, stretches, and lags between
        /// them. Legs, plants, and homes below are drawn from simulation truth and share none of it.
        /// </summary>
        void DrawBody(
            DynamicBuffer<VerletPoint> points,
            CreatureBody body,
            float2 heading,
            in BodyPresentation presentation,
            bool recovering)
        {
            bodyLine.positionCount = points.Length;
            for (var index = 0; index < points.Length; index++)
                bodyLine.SetPosition(index, Flat(Shape(points[index].Position, body.RootPosition, heading, presentation)));

            // Turns red the moment gait reports it has nowhere legal to step — the failure is
            // shown, not disguised.
            SetColor(bodyLine, recovering ? BlockedColor : BodyColor);

            var tip = body.RootPosition;
            var side = PlanarMath.Perpendicular(heading);
            headingArrow.positionCount = 4;
            headingArrow.SetPosition(0, Flat(tip));
            headingArrow.SetPosition(1, Flat(tip + heading * 0.9f));
            headingArrow.SetPosition(2, Flat(tip + heading * 0.65f + side * 0.15f));
            headingArrow.SetPosition(3, Flat(tip + heading * 0.9f));

            bankArrow.enabled = ShowBodyLanguage;
            if (!ShowBodyLanguage)
                return;

            var banked = new float2(
                math.cos(presentation.RotationRadians), math.sin(presentation.RotationRadians));
            bankArrow.positionCount = 2;
            bankArrow.SetPosition(0, Flat(presentation.RenderPosition));
            bankArrow.SetPosition(1, Flat(presentation.RenderPosition + banked * 0.9f));
        }

        /// <summary>
        /// Places one body point in the picture: stretched along the heading, leaned by the filtered
        /// bank, and offset by the weight shift. Feeding a foot target through this would be the
        /// bug all three lessons warn about, so nothing but the spine goes through it.
        /// </summary>
        static float2 Shape(float2 point, float2 root, float2 heading, in BodyPresentation presentation)
        {
            var side = PlanarMath.Perpendicular(heading);
            var offset = point - root;
            var shaped = heading * (math.dot(offset, heading) * presentation.Scale.x)
                         + side * (math.dot(offset, side) * presentation.Scale.y);

            var cos = math.cos(presentation.BankRadians);
            var sin = math.sin(presentation.BankRadians);
            return presentation.RenderPosition
                   + new float2(shaped.x * cos - shaped.y * sin, shaped.x * sin + shaped.y * cos);
        }

        BodyPresentationPolicy BuildBodyPolicy() => new()
        {
            // Heavy and slightly under-damped: the body should arrive at its lean a beat late and
            // settle past it, which is the whole reason this is a filter and not a formula.
            BankResponse = new SecondOrderTuning { Frequency = 2.2f, Damping = 0.7f, Response = 0f },
            WeightShiftResponse = new SecondOrderTuning { Frequency = 1.6f, Damping = 0.85f, Response = 0f },
            BankGain = ShowBodyLanguage ? math.radians(BankGain) : 0f,
            MaxBank = math.radians(MaxBankDegrees),
            StretchGain = ShowBodyLanguage ? StretchGain : 0f,
            AnticipationBank = math.radians(AnticipationDegrees),
            AnticipationSeconds = AnticipationSeconds,
            WeightShiftDistance = ShowBodyLanguage ? WeightShiftDistance : 0f,
        };

        void FollowCreature(CreatureBody body)
        {
            if (demoCamera == null)
                demoCamera = Camera.main;

            if (demoCamera == null)
                return;

            demoCamera.orthographicSize = ViewSize;
            var position = demoCamera.transform.position;
            position.x = Mathf.Lerp(position.x, body.RootPosition.x, 0.08f);
            position.y = Mathf.Lerp(position.y, body.RootPosition.y, 0.08f);
            demoCamera.transform.position = position;
        }

        void DrawIsland()
        {
            if (islandQuery.IsEmptyIgnoreFilter)
            {
                islandRing.enabled = false;
                return;
            }

            var island = islandQuery.GetSingleton<PlanarIsland>();
            islandRing.enabled = true;
            DrawRing(islandRing, island.Centre, island.Radius);
        }

        static void DrawCandidateFan(LineRenderer line, DynamicBuffer<PlanarQueryDebugHit> hits, byte legIndex)
        {
            var count = 0;
            var anyLegal = false;
            for (var index = 0; index < hits.Length; index++)
            {
                if (hits[index].LegIndex != legIndex)
                    continue;

                line.positionCount = count + 1;
                line.SetPosition(count, Flat(hits[index].Point));
                anyLegal |= hits[index].Legal != 0;
                count++;
            }

            line.enabled = count > 0;
            SetColor(line, anyLegal ? HomeColor : BlockedColor);
        }

        static void DrawRing(LineRenderer line, float2 centre, float radius)
        {
            line.positionCount = RingSegments + 1;
            for (var index = 0; index <= RingSegments; index++)
            {
                var angle = index / (float)RingSegments * math.PI * 2f;
                line.SetPosition(index, new Vector3(
                    centre.x + math.cos(angle) * radius,
                    centre.y + math.sin(angle) * radius,
                    0f));
            }
        }

        static void DrawCross(LineRenderer line, float2 centre, float size)
        {
            line.positionCount = 3;
            line.SetPosition(0, new Vector3(centre.x - size, centre.y, 0f));
            line.SetPosition(1, new Vector3(centre.x + size, centre.y, 0f));
            line.SetPosition(2, new Vector3(centre.x, centre.y, 0f));
        }

        bool TryBindWorld()
        {
            if (isReady)
                return true;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            entityManager = world.EntityManager;
            creatureQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlanarHeading>(),
                ComponentType.ReadOnly<CreatureBody>(),
                ComponentType.ReadOnly<Gait>(),
                ComponentType.ReadOnly<CreatureLocomotion>(),
                ComponentType.ReadOnly<VerletPoint>(),
                ComponentType.ReadOnly<GaitLeg>(),
                ComponentType.ReadOnly<Limb2BoneLeg>(),
                ComponentType.ReadOnly<PlanarQueryDebugHit>());
            islandQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlanarIsland>());
            isReady = true;
            return true;
        }

        static Vector3 Flat(float2 point) => new(point.x, point.y, 0f);

        static void SetColor(LineRenderer line, Color color)
        {
            line.startColor = color;
            line.endColor = color;
        }

        static LineRenderer CreateLine(string lineName, Color color, float width)
        {
            var line = new GameObject(lineName).AddComponent<LineRenderer>();
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
            line.numCornerVertices = 4;
            line.numCapVertices = 4;
            line.useWorldSpace = true;
            return line;
        }

        static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Top-Down Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
            }

            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, -3.2f, -10f);
            camera.backgroundColor = new Color(0.06f, 0.09f, 0.14f);
        }
    }
}
