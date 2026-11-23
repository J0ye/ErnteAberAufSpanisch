using System.IO;
using Cosecha.View;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Cosecha.EditorTools
{
    /// <summary>One-click setup: art, config assets and the game scene. Safe to re-run.</summary>
    public static class ProjectSetup
    {
        const string ConfigPath = "Assets/Config/BalanceConfig.asset";
        const string SpriteSetPath = "Assets/Config/SpriteSet.asset";
        const string ScenePath = "Assets/Scenes/Game.unity";
        const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        [MenuItem("Cosecha/Import TMP Essentials")]
        public static void ImportTmpEssentials()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) != null) { Debug.Log("TMP Essentials already imported."); return; }
            string pkg = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
            AssetDatabase.ImportPackage(pkg, false);
        }

        [MenuItem("Cosecha/Setup Project (art, assets, scene)")]
        public static void SetupAll()
        {
            var cfg = CreateConfig();
            PixelArtGenerator.GenerateAll(cfg);
            var set = CreateSpriteSet();
            BuildScene(cfg, set);
            Debug.Log("Cosecha setup complete: " + ScenePath);
        }

        [MenuItem("Cosecha/Regenerate Placeholder Art")]
        public static void RegenerateArt()
        {
            PixelArtGenerator.GenerateAll(CreateConfig());
            CreateSpriteSet();
        }

        static BalanceConfig CreateConfig()
        {
            Directory.CreateDirectory("Assets/Config");
            var cfg = AssetDatabase.LoadAssetAtPath<BalanceConfig>(ConfigPath);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<BalanceConfig>();
                AssetDatabase.CreateAsset(cfg, ConfigPath);
            }
            return cfg;
        }

        static SpriteSet CreateSpriteSet()
        {
            var set = AssetDatabase.LoadAssetAtPath<SpriteSet>(SpriteSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SpriteSet>();
                AssetDatabase.CreateAsset(set, SpriteSetPath);
            }
            set.pixel = PixelArtGenerator.Load("pixel");
            set.panel = PixelArtGenerator.Load("panel");
            set.button = PixelArtGenerator.Load("button");
            set.sky = PixelArtGenerator.Load("sky");
            set.mesa = PixelArtGenerator.Load("mesa");
            set.house = PixelArtGenerator.Load("house");
            set.cloudSmall = PixelArtGenerator.Load("cloud_small");
            set.cloudMedium = PixelArtGenerator.Load("cloud_medium");
            set.cloudLarge = PixelArtGenerator.Load("cloud_large");
            set.scarecrow = PixelArtGenerator.Load("scarecrow");
            set.boardCell = PixelArtGenerator.Load("board_cell");
            set.boardHighlight = PixelArtGenerator.Load("board_highlight");
            set.iconWheat = PixelArtGenerator.Load("icon_wheat");
            set.iconLavender = PixelArtGenerator.Load("icon_lavender");
            set.shedRoof = PixelArtGenerator.Load("shed_roof");
            set.compost = PixelArtGenerator.Load("compost");
            set.sign = PixelArtGenerator.Load("sign");
            set.articleIcon = PixelArtGenerator.Load("icon_article");
            set.starIcon = PixelArtGenerator.Load("icon_star");
            set.soilTile = PixelArtGenerator.Load("tile_soil");
            set.wheat = PixelArtGenerator.Load("crop_wheat");
            set.lavender = PixelArtGenerator.Load("crop_lavender");
            if (set.font == null) set.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }

        static void BuildScene(BalanceConfig cfg, SpriteSet set)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 270f / 2f / 16f;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(74, 142, 226, 255);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = 16;
            ppc.refResolutionX = 480;
            ppc.refResolutionY = 270;
            // crop to 16:9 and stretch-fill: the camera viewport (and thus the Screen Space - Camera
            // canvas) always matches the 480x270 world area, on any window size.
            ppc.cropFrame = PixelPerfectCamera.CropFrame.StretchFill;

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = Color.white;

            var game = new GameObject("Game");
            var ctl = game.AddComponent<GameController>();
            // reload by path: the instances created above may have been replaced by a reimport
            cfg = AssetDatabase.LoadAssetAtPath<BalanceConfig>(ConfigPath);
            set = AssetDatabase.LoadAssetAtPath<SpriteSet>(SpriteSetPath);
            var so = new SerializedObject(ctl);
            so.FindProperty("config").objectReferenceValue = cfg;
            so.FindProperty("sprites").objectReferenceValue = set;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
