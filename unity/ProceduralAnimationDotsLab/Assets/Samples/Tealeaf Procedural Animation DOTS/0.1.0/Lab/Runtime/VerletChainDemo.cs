using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Tealeaf.ProceduralAnimation.Dots;

namespace ProceduralAnimationDotsLab
{
    /// <summary>
    /// Read-only presentation for the baked lesson creature. It draws resolved package
    /// output and sample-side debug data; it never creates or writes solver state.
    /// </summary>
    public sealed class VerletChainDemo : MonoBehaviour
    {
        EntityManager entityManager;
        EntityQuery chainQuery;
        EntityQuery supportQuery;
        Camera demoCamera;
        LineRenderer chainLine;
        LineRenderer targetMarker;
        LineRenderer[] legLines;
        LineRenderer[] footTargetMarkers;
        LineRenderer[] contactSurfaceLines;
        LineRenderer[] contactNormalLines;
        LineRenderer[] groundProbeMarkers;
        LineRenderer[] groundHitMarkers;
        LineRenderer[] groundNormalLines;
        LineRenderer supportLine;
        LineRenderer beltArrowLine;
        bool isReady;

        void Start()
        {
            ConfigureCamera();
            chainLine = CreateLine("Chain", new Color(0.4f, 0.9f, 1f), 0.13f);
            targetMarker = CreateLine("Muscle Target", new Color(1f, 0.65f, 0.25f), 0.06f);
            legLines = new[]
            {
                CreateLine("Left Leg", new Color(0.96f, 0.4f, 0.65f), 0.11f),
                CreateLine("Right Leg", new Color(0.63f, 0.53f, 1f), 0.11f),
            };
            footTargetMarkers = new[]
            {
                CreateLine("Left Foot Target", new Color(0.98f, 0.86f, 0.4f), 0.06f),
                CreateLine("Right Foot Target", new Color(0.98f, 0.86f, 0.4f), 0.06f),
            };
            contactSurfaceLines = new[]
            {
                CreateLine("Ground Contact", new Color(0.35f, 0.85f, 0.48f), 0.08f),
                CreateLine("Wall Contact", new Color(0.35f, 0.85f, 0.48f), 0.08f),
            };
            contactNormalLines = new[]
            {
                CreateLine("Ground Contact Normal", new Color(0.95f, 0.95f, 0.45f), 0.05f),
                CreateLine("Wall Contact Normal", new Color(0.95f, 0.95f, 0.45f), 0.05f),
            };
            groundProbeMarkers = new[]
            {
                CreateLine("Left Ground Probe", new Color(0.45f, 0.95f, 0.9f), 0.04f),
                CreateLine("Right Ground Probe", new Color(0.45f, 0.95f, 0.9f), 0.04f),
            };
            groundHitMarkers = new[]
            {
                CreateLine("Left Ground Hit", new Color(0.45f, 1f, 0.45f), 0.06f),
                CreateLine("Right Ground Hit", new Color(0.45f, 1f, 0.45f), 0.06f),
            };
            groundNormalLines = new[]
            {
                CreateLine("Left Ground Normal", new Color(1f, 0.9f, 0.35f), 0.04f),
                CreateLine("Right Ground Normal", new Color(1f, 0.9f, 0.35f), 0.04f),
            };
            supportLine = CreateLine("Moving Support", new Color(0.8f, 0.4f, 1f), 0.1f);
            beltArrowLine = CreateLine("Conveyor Direction", new Color(1f, 0.75f, 0.25f), 0.05f);
            TryBindWorld();
        }

        void Update()
        {
            if (!TryBindWorld())
                return;

            // The creature is baked in a sub scene, so it appears a frame or more after load.
            if (chainQuery.IsEmptyIgnoreFilter)
                return;

            var entity = chainQuery.GetSingletonEntity();
            var points = entityManager.GetBuffer<VerletPoint>(entity, true);
            var target = entityManager.GetComponentData<ChainTarget>(entity).Position;
            var body = entityManager.GetComponentData<CreatureBody>(entity);
            UpdateCamera(body);
            var limbs = entityManager.GetBuffer<Limb2BoneLeg>(entity, true);
            var gaitLegs = entityManager.GetBuffer<GaitLeg>(entity, true);
            var contacts = entityManager.GetBuffer<ContactPlane>(entity, true);
            var groundDebugHits = entityManager.GetBuffer<GroundQueryDebugHit>(entity, true);

            chainLine.positionCount = points.Length;
            for (var index = 0; index < points.Length; index++)
                chainLine.SetPosition(index, new Vector3(points[index].Position.x, points[index].Position.y, 0f));

            targetMarker.positionCount = 2;
            targetMarker.SetPosition(0, new Vector3(target.x - 0.16f, target.y, 0f));
            targetMarker.SetPosition(1, new Vector3(target.x + 0.16f, target.y, 0f));

            var legCount = math.min(limbs.Length, legLines.Length);
            for (var index = 0; index < legCount; index++)
            {
                var limb = limbs[index].Limb;
                var isSwinging = index < gaitLegs.Length && gaitLegs[index].State == FootState.Swinging;
                var legColor = isSwinging ? new Color(1f, 0.7f, 0.25f) : index == 0 ? new Color(0.96f, 0.4f, 0.65f) : new Color(0.63f, 0.53f, 1f);
                legLines[index].startColor = legColor;
                legLines[index].endColor = legColor;
                legLines[index].positionCount = 3;
                legLines[index].SetPosition(0, new Vector3(limb.Root.x, limb.Root.y, 0f));
                legLines[index].SetPosition(1, new Vector3(limb.Knee.x, limb.Knee.y, 0f));
                legLines[index].SetPosition(2, new Vector3(limb.Foot.x, limb.Foot.y, 0f));

                footTargetMarkers[index].positionCount = 2;
                footTargetMarkers[index].SetPosition(0, new Vector3(limb.Target.x - 0.18f, limb.Target.y, 0f));
                footTargetMarkers[index].SetPosition(1, new Vector3(limb.Target.x + 0.18f, limb.Target.y, 0f));
            }

            UpdateContactPresentation(contacts);
            UpdateGroundPresentation(groundDebugHits);
            UpdateSupportPresentation();
        }

        bool TryBindWorld()
        {
            if (isReady)
                return true;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            entityManager = world.EntityManager;
            supportQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<DemoMovingSupport>(),
                ComponentType.ReadOnly<SupportKinematics>(),
                ComponentType.ReadOnly<SupportPose>());
            chainQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<VerletChain>(),
                ComponentType.ReadOnly<VerletPoint>(),
                ComponentType.ReadOnly<ChainTarget>(),
                ComponentType.ReadOnly<CreatureLocomotion>(),
                ComponentType.ReadOnly<CreatureBody>(),
                ComponentType.ReadOnly<GaitSettings>(),
                ComponentType.ReadOnly<GaitLeg>(),
                ComponentType.ReadOnly<Limb2BoneLeg>(),
                ComponentType.ReadOnly<ContactPlane>(),
                ComponentType.ReadOnly<FootholdCandidate>(),
                ComponentType.ReadOnly<GroundQueryDebugHit>());

            isReady = true;
            return true;
        }

        void UpdateSupportPresentation()
        {
            if (supportQuery.IsEmptyIgnoreFilter)
            {
                supportLine.enabled = false;
                beltArrowLine.enabled = false;
                return;
            }

            var pose = entityManager.GetComponentData<SupportPose>(supportQuery.GetSingletonEntity());
            supportLine.enabled = true;
            supportLine.positionCount = 2;
            var left = SupportMath.TransformPoint(pose, new float2(-1.35f, 0f));
            var right = SupportMath.TransformPoint(pose, new float2(1.35f, 0f));
            supportLine.SetPosition(0, new Vector3(left.x, left.y, 0f));
            supportLine.SetPosition(1, new Vector3(right.x, right.y, 0f));

            var arrowStart = SupportMath.TransformPoint(pose, new float2(-0.35f, 0.16f));
            var arrowTip = SupportMath.TransformPoint(pose, new float2(0.35f, 0.16f));
            var arrowUpper = SupportMath.TransformPoint(pose, new float2(0.2f, 0.28f));
            var arrowLower = SupportMath.TransformPoint(pose, new float2(0.2f, 0.04f));
            beltArrowLine.enabled = true;
            beltArrowLine.positionCount = 4;
            beltArrowLine.SetPosition(0, new Vector3(arrowStart.x, arrowStart.y, 0f));
            beltArrowLine.SetPosition(1, new Vector3(arrowTip.x, arrowTip.y, 0f));
            beltArrowLine.SetPosition(2, new Vector3(arrowUpper.x, arrowUpper.y, 0f));
            beltArrowLine.SetPosition(3, new Vector3(arrowTip.x, arrowTip.y, 0f));
        }

        void UpdateGroundPresentation(DynamicBuffer<GroundQueryDebugHit> hits)
        {
            for (var index = 0; index < groundProbeMarkers.Length; index++)
            {
                var hit = FindGroundDebugHit(hits, (byte)index);
                var active = hit.Exists != 0;
                groundProbeMarkers[index].enabled = active;
                groundHitMarkers[index].enabled = active;
                groundNormalLines[index].enabled = active;
                if (!active)
                    continue;

                groundProbeMarkers[index].positionCount = 2;
                groundProbeMarkers[index].SetPosition(0, new Vector3(hit.Probe.x - 0.14f, hit.Probe.y, 0f));
                groundProbeMarkers[index].SetPosition(1, new Vector3(hit.Probe.x + 0.14f, hit.Probe.y, 0f));

                groundHitMarkers[index].positionCount = 2;
                groundHitMarkers[index].SetPosition(0, new Vector3(hit.Point.x - 0.14f, hit.Point.y, 0f));
                groundHitMarkers[index].SetPosition(1, new Vector3(hit.Point.x + 0.14f, hit.Point.y, 0f));

                var normal = math.normalizesafe(hit.Normal, new float2(0f, 1f));
                groundNormalLines[index].positionCount = 2;
                groundNormalLines[index].SetPosition(0, new Vector3(hit.Point.x, hit.Point.y, 0f));
                groundNormalLines[index].SetPosition(1, new Vector3(hit.Point.x + normal.x * 0.42f, hit.Point.y + normal.y * 0.42f, 0f));
            }
        }

        static GroundQueryDebugHit FindGroundDebugHit(DynamicBuffer<GroundQueryDebugHit> hits, byte legIndex)
        {
            for (var index = 0; index < hits.Length; index++)
            {
                if (hits[index].LegIndex == legIndex)
                    return hits[index];
            }

            return default;
        }

        void UpdateContactPresentation(DynamicBuffer<ContactPlane> contacts)
        {
            for (var index = 0; index < contactSurfaceLines.Length; index++)
            {
                var active = index < contacts.Length;
                contactSurfaceLines[index].enabled = active;
                contactNormalLines[index].enabled = active;
                if (!active)
                    continue;

                var contact = contacts[index];
                var normal = math.normalizesafe(contact.Normal, new float2(0f, 1f));
                var tangent = new float2(-normal.y, normal.x);
                var boundary = contact.Point + normal * math.max(contact.Radius, 0f);
                var surfaceStart = boundary - tangent * 8f;
                var surfaceEnd = boundary + tangent * 8f;
                contactSurfaceLines[index].positionCount = 2;
                contactSurfaceLines[index].SetPosition(0, new Vector3(surfaceStart.x, surfaceStart.y, 0f));
                contactSurfaceLines[index].SetPosition(1, new Vector3(surfaceEnd.x, surfaceEnd.y, 0f));

                contactNormalLines[index].positionCount = 2;
                contactNormalLines[index].SetPosition(0, new Vector3(boundary.x, boundary.y, 0f));
                contactNormalLines[index].SetPosition(1, new Vector3(boundary.x + normal.x * 0.45f, boundary.y + normal.y * 0.45f, 0f));
            }
        }

        static LineRenderer CreateLine(string lineName, Color color, float width)
        {
            var line = new GameObject(lineName).AddComponent<LineRenderer>();
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
            line.numCornerVertices = 6;
            line.numCapVertices = 6;
            line.useWorldSpace = true;
            return line;
        }

        static void ConfigureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Demo Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
            }

            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.backgroundColor = new Color(0.06f, 0.09f, 0.14f);
        }

        void UpdateCamera(CreatureBody body)
        {
            if (demoCamera == null)
                demoCamera = Camera.main;

            if (demoCamera == null)
                return;

            var position = demoCamera.transform.position;
            position.x = Mathf.Lerp(position.x, body.RootPosition.x + 3f, 0.08f);
            demoCamera.transform.position = position;
        }
    }
}
