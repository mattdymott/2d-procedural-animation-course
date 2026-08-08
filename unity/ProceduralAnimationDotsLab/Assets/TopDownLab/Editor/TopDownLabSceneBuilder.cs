using System.IO;
using Tealeaf.ProceduralAnimation.Dots;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TopDownLab
{
    /// <summary>
    /// Builds the top-down demo scenes from code so the setup is reproducible and reviewable.
    /// Run it from <c>Tealeaf/Rebuild Top-Down Lab Scenes</c>, or call <see cref="Build"/>.
    ///
    /// The creature is composed entirely from the package's authoring components — this builder
    /// never constructs plants, swing state, or chain points itself.
    ///
    /// NOTE: the package's authoring components compile into an editor-only assembly, and Unity
    /// refuses to <c>AddComponent</c> an editor script. Scenes that already reference them load
    /// fine, so the built scenes work — but to re-run this builder you must first drop
    /// <c>"includePlatforms": ["Editor"]</c> from
    /// <c>Tealeaf.ProceduralAnimation.Dots.Editor.asmdef</c>, and restore it afterwards.
    /// </summary>
    public static class TopDownLabSceneBuilder
    {
        const string SceneFolder = "Assets/TopDownLab/Scenes";
        const string CreatureScenePath = SceneFolder + "/TopDownCreature.unity";
        const string HostScenePath = SceneFolder + "/TopDownLab.unity";

        // The circuit the creature walks, and the blocked patch sitting just outside it so the
        // legs on the outer flank are the ones that run out of legal ground.
        static readonly Vector2 CircuitCentre = Vector2.zero;
        const float CircuitRadius = 3.2f;
        const float CircuitSpeed = 1.6f;
        static readonly Vector2 IslandCentre = new(4.0f, 0f);
        const float IslandRadius = 0.7f;

        const float RestLength = 0.8f;
        const int BodyPointCount = 3;

        [MenuItem("Tealeaf/Rebuild Top-Down Lab Scenes")]
        public static void Build()
        {
            Directory.CreateDirectory(SceneFolder);
            BuildCreatureScene();
            BuildHostScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Top-down lab scenes rebuilt: {HostScenePath}");
        }

        static void BuildCreatureScene()
        {
            // Each scene is built as the single open scene and saved before the next one starts:
            // Unity refuses to open a scene additively while an unsaved untitled scene is open.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var creature = new GameObject("Top-Down Creature");
            // The creature starts at the bottom of the circuit heading +X, which is the direction
            // the chain lays its points out in — so its baked rest pose and its heading agree.
            creature.transform.position = new Vector3(0f, -CircuitRadius, 0f);

            var chain = creature.AddComponent<VerletChainAuthoring>();
            chain.ChainSegmentCount = BodyPointCount;
            chain.InitialRootPosition = new Vector2(0f, -CircuitRadius);
            chain.RestLength = RestLength;
            chain.Damping = 0.98f;
            chain.Gravity = Vector2.zero;          // a movement plane has no down
            chain.RootBobAmplitude = 0f;
            chain.RootBobFrequency = 0f;

            var legs = creature.AddComponent<LegsAuthoring>();
            legs.Legs = new[]
            {
                Leg(attachment: 2, forward: 0.45f, lateral: 0.80f, tripodGroup: 0),   // front left
                Leg(attachment: 2, forward: 0.45f, lateral: -0.80f, tripodGroup: 1),  // front right
                Leg(attachment: 1, forward: 0.10f, lateral: 0.85f, tripodGroup: 1),   // middle left
                Leg(attachment: 1, forward: 0.10f, lateral: -0.85f, tripodGroup: 0),  // middle right
                Leg(attachment: 0, forward: -0.35f, lateral: 0.80f, tripodGroup: 0),  // rear left
                Leg(attachment: 0, forward: -0.35f, lateral: -0.80f, tripodGroup: 1), // rear right
            };

            var gait = creature.AddComponent<GaitAuthoring>();
            gait.Comfort = 0.35f;
            gait.StepDuration = 0.25f;
            gait.StepLead = 0.12f;
            gait.StepHeight = 0.35f;
            gait.MinimumSupport = 0f;              // a planar candidate carries no useful normal
            gait.MinimumForward = 0f;

            var planar = creature.AddComponent<PlanarGaitAuthoring>();
            planar.InitialForward = Vector2.right;
            planar.MinimumPlantedFeet = 3;
            planar.Cadence = GaitCadence.Tripod;
            planar.SlowCadence = GaitCadence.Wave;
            planar.FastCadence = GaitCadence.Tripod;
            planar.EnterSpeed = 1.2f;
            planar.ExitSpeed = 0.6f;
            // Diagonal crawl order: the wave cadence never steps two neighbours in a row.
            planar.WaveOrder = new[] { 0, 3, 4, 5, 2, 1 };

            var intent = creature.AddComponent<TopDownIntentAuthoring>();
            intent.Centre = CircuitCentre;
            intent.Radius = CircuitRadius;
            intent.Speed = CircuitSpeed;
            intent.TurnRate = 2.5f;
            intent.RecoverySpeedScale = 0.35f;
            intent.RecoveryTurnRate = 3.5f;

            creature.AddComponent<PlanarQueryDebugAuthoring>();

            var island = new GameObject("Blocked Island");
            island.transform.position = new Vector3(IslandCentre.x, IslandCentre.y, 0f);
            var islandAuthoring = island.AddComponent<PlanarIslandAuthoring>();
            islandAuthoring.Centre = IslandCentre;
            islandAuthoring.Radius = IslandRadius;

            EditorSceneManager.SaveScene(scene, CreatureScenePath);
        }

        static void BuildHostScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Top-Down Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.09f, 0.14f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var presentation = new GameObject("Presentation");
            presentation.AddComponent<TopDownLabDemo>().VisualStepHeight = 0.3f;

            var subSceneObject = new GameObject("Creature SubScene");
            var subScene = subSceneObject.AddComponent<SubScene>();
            subScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(CreatureScenePath);
            subScene.AutoLoadScene = true;

            EditorSceneManager.SaveScene(scene, HostScenePath);
        }

        static LegsAuthoring.LegRecipe Leg(int attachment, float forward, float lateral, int tripodGroup) => new()
        {
            AttachmentPointIndex = attachment,
            LengthA = 0.6f,
            LengthB = 0.6f,
            BendSign = lateral >= 0f ? 1f : -1f,
            // x runs along the heading and y across it, so these rotate with the body.
            HomeOffset = new Vector2(forward, lateral),
            TripodGroup = tripodGroup,
        };
    }
}
