#nullable enable

using System;
using System.IO;
using System.Linq;
using LockstepArena.Simulation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LockstepArena.Demo.Editor
{
    public static class V3BBattlePresentationBuilder
    {
        private const string Root = "Assets/LockstepArenaDemo/Resources/BattlePresentation";
        private const string CatalogPath = Root + "/V3BBattlePresentationCatalog.asset";
        private const string RobotPath = "Assets/Art/Characters/StylooRobot/robot.fbx";
        private const string WeaponPath = "Assets/Art/Weapons/SciFiGun/LongPistol_small.fbx";

        [MenuItem("Tools/Lockstep Arena/Build v3-B Battle Presentation")]
        public static void BuildAssets()
        {
            EnsureFolder("Assets/LockstepArenaDemo/Resources", "BattlePresentation");
            ConfigureModel(RobotPath, true);
            ConfigureModel(WeaponPath, false);
            ConfigureNormalTexture("Assets/Art/Characters/StylooRobot/robot_normal.png");

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit shader is unavailable.");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? throw new InvalidOperationException("URP Unlit shader is unavailable.");
            Shader particle = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? unlit;

            Material robot = MaterialAt("RobotGunmetal", unlit, new Color(0.68f, 0.74f, 0.8f));
            robot.SetTexture("_BaseMap", Load<Texture2D>("Assets/Art/Characters/StylooRobot/robot_color.png"));
            robot.SetFloat("_Cull", 0f);

            Material weapon = MaterialAt("WeaponGunmetal", lit, new Color(0.14f, 0.18f, 0.24f));
            weapon.SetFloat("_Cull", 0f);
            weapon.SetFloat("_Metallic", 0.68f);
            weapon.SetFloat("_Smoothness", 0.52f);
            Material floor = MaterialAt("ArenaFloor", lit, new Color(0.055f, 0.075f, 0.085f));
            floor.SetFloat("_Metallic", 0.48f);
            floor.SetFloat("_Smoothness", 0.32f);
            Material wall = MaterialAt("ArenaWall", lit, new Color(0.1f, 0.13f, 0.17f));
            Material cover = MaterialAt("ArenaCover", lit, new Color(0.18f, 0.22f, 0.27f));
            cover.SetFloat("_Metallic", 0.65f);
            Material cyan = EmissiveAt("TeamCyan", lit, new Color(0.04f, 0.52f, 0.82f), new Color(0.08f, 0.72f, 1f) * 2.4f);
            Material red = EmissiveAt("TeamRed", lit, new Color(0.72f, 0.07f, 0.045f), new Color(1f, 0.18f, 0.12f) * 2.1f);
            Material projectile = EmissiveAt("EnergyBolt", unlit, Color.white, new Color(0.2f, 0.86f, 1f) * 3f);
            Material muzzle = MaterialAt("MuzzleFlash", particle, Color.white);
            muzzle.SetTexture("_BaseMap", Load<Texture2D>("Assets/Art/VFX/Kenney/muzzle_03.png"));
            Material particles = MaterialAt("ParticleSpark", particle, Color.white);
            particles.SetTexture("_BaseMap", Load<Texture2D>("Assets/Art/VFX/Kenney/spark_05.png"));

            BattlePresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<BattlePresentationCatalog>(CatalogPath);
            if (catalog is null)
            {
                catalog = ScriptableObject.CreateInstance<BattlePresentationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.RobotPrefab = Load<GameObject>(RobotPath);
            catalog.WeaponPrefab = Load<GameObject>(WeaponPath);
            catalog.PlatformPrefab = Load<GameObject>("Assets/Art/Environment/SciFiArena/Platform_Simple.fbx");
            catalog.DoorFramePrefab = Load<GameObject>("Assets/Art/Environment/SciFiArena/Door_Frame_Square.fbx");
            catalog.ColumnPrefab = Load<GameObject>("Assets/Art/Environment/SciFiArena/Column_Simple.fbx");
            catalog.ComputerPrefab = Load<GameObject>("Assets/Art/Environment/SciFiArena/Prop_Computer.fbx");
            catalog.CratePrefab = Load<GameObject>("Assets/Art/Props/Prop_Crate.fbx");
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(RobotPath).OfType<AnimationClip>()
                .Where(value => !value.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            catalog.IdleClip = FindClip(clips, "iddle", "idle");
            catalog.RunClip = FindClip(clips, "walking", "walk");
            catalog.ShootClip = FindClip(clips, "attackminiguns", "attack");
            catalog.RobotMaterial = robot;
            catalog.WeaponMaterial = weapon;
            catalog.FloorMaterial = floor;
            catalog.WallMaterial = wall;
            catalog.CoverMaterial = cover;
            catalog.CyanMaterial = cyan;
            catalog.RedMaterial = red;
            catalog.ProjectileMaterial = projectile;
            catalog.MuzzleMaterial = muzzle;
            catalog.ParticleMaterial = particles;
            catalog.PanelSprite = Load<Sprite>("Assets/Art/UI/SciFi/panel_dark_frame.png");
            catalog.ButtonSprite = Load<Sprite>("Assets/Art/UI/SciFi/button_primary.png");
            catalog.Font = TMP_Settings.defaultFontAsset;
            catalog.ShootSound = Load<AudioClip>("Assets/Art/Audio/Kenney/laserSmall_003.ogg");
            catalog.PlayerHitSound = Load<AudioClip>("Assets/Art/Audio/Kenney/impactMetal_001.ogg");
            catalog.EnvironmentHitSound = Load<AudioClip>("Assets/Art/Audio/Kenney/impactMetal_light_001.ogg");
            catalog.DeathSound = Load<AudioClip>("Assets/Art/Audio/Kenney/lowFrequency_explosion_000.ogg");
            catalog.CountdownSound = Load<AudioClip>("Assets/Art/Audio/Kenney/tone1.ogg");
            catalog.FightSound = Load<AudioClip>("Assets/Art/Audio/Kenney/twoTone1.ogg");
            catalog.VictorySound = Load<AudioClip>("Assets/Art/Audio/Kenney/powerUp3.ogg");
            catalog.ClickSound = Load<AudioClip>("Assets/Art/Audio/Kenney/click3.ogg");
            catalog.AmbientSound = Load<AudioClip>("Assets/Art/Audio/Kenney/spaceEngineLow_000.ogg");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Built v3-B battle presentation catalog with clips: {string.Join(", ", clips.Select(value => value.name))}");
        }

        [MenuItem("Tools/Lockstep Arena/Capture v3-B Battle Preview")]
        public static void CapturePreview()
        {
            BuildAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BattlePresentationCatalog catalog = BattlePresentationCatalog.LoadRequired();
            var root = new GameObject("v3-B Preview");
            BattlePresenter presenter = root.AddComponent<BattlePresenter>();
            presenter.Configure(BattleDefinition.CreateDefault());
            ActiveRoster roster = new ActiveRoster(new[] { new PlayerId(11), new PlayerId(22) });
            var players = new[]
            {
                new PlayerState(-2800, -650, 5200, 75, 1, 0),
                new PlayerState(2700, 650, 38000, 50, 0, 0),
            };
            var projectiles = new[]
            {
                new ProjectileState(21, new PlayerId(11), -900, -280, 1000, 120, 20),
                new ProjectileState(22, new PlayerId(22), 1350, 420, -1000, -80, 20),
            };
            BattleState state = new BattleState(570, roster, players, BattleDefinition.CreateDefault(),
                BattlePhase.Playing, 0, 1230, RoundResult.None, null, projectiles, 23);
            presenter.Present(state, new PlayerSlot(0));
            foreach (PlayerPresentation player in root.GetComponentsInChildren<PlayerPresentation>())
            {
                player.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                BakePreviewSkins(player);
            }

            var hudRoot = new GameObject("v3-B Preview HUD");
            BattleHudView hud = hudRoot.AddComponent<BattleHudView>();
            hud.Configure(catalog);
            hud.PresentPreview(state);
            Camera camera = presenter.GameplayCamera!;
            Canvas canvas = hudRoot.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.5f;

            const int width = 1280;
            const int height = 720;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            RenderTexture.active = target;
            camera.Render();
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            string directory = Path.GetFullPath("Docs/Evidence/V3B");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "v3b-battle-preview.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(hudRoot);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            Debug.Log("Captured v3-B preview: " + path);
        }

        private static void BakePreviewSkins(PlayerPresentation player)
        {
            foreach (SkinnedMeshRenderer skin in player.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh { name = skin.name + " Preview Mesh" };
                skin.BakeMesh(mesh, true);
                var baked = new GameObject(skin.name + " Preview Geometry", typeof(MeshFilter), typeof(MeshRenderer));
                baked.transform.SetParent(skin.transform, false);
                baked.GetComponent<MeshFilter>().sharedMesh = mesh;
                baked.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                skin.enabled = false;
            }
        }

        private static void ConfigureModel(string path, bool animations)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer) throw new InvalidOperationException("Missing model importer: " + path);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = animations;
            if (animations)
            {
                importer.animationType = ModelImporterAnimationType.Legacy;
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    string lower = clip.name.ToLowerInvariant();
                    clip.loopTime = lower.Contains("iddle") || lower.Contains("idle") || lower.Contains("walking");
                    clip.loopPose = clip.loopTime;
                }
                importer.clipAnimations = clips;
            }
            importer.SaveAndReimport();
        }

        private static void ConfigureNormalTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        private static Material MaterialAt(string name, Shader shader, Color color)
        {
            string path = $"{Root}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material is null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EmissiveAt(string name, Shader shader, Color baseColor, Color emission)
        {
            Material material = MaterialAt(name, shader, baseColor);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
            return material;
        }

        private static AnimationClip FindClip(AnimationClip[] clips, params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                AnimationClip? clip = clips.FirstOrDefault(value => value.name.ToLowerInvariant().Contains(candidate));
                if (clip is not null) return clip;
            }
            throw new InvalidOperationException("Required robot animation clip was not found: " + string.Join("/", candidates));
        }

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
