#nullable enable

using System;
using System.Collections.Generic;
using LockstepArena.Simulation;
using UnityEngine;

namespace LockstepArena.Demo
{
    public sealed class BattlePresenter : MonoBehaviour
    {
        public const float WorldUnitsPerSimulationUnit = 1000f;
        private const float SimulationToWorld = 1f / WorldUnitsPerSimulationUnit;
        private readonly Dictionary<int, PlayerPresentation> _players = new Dictionary<int, PlayerPresentation>();
        private readonly Dictionary<ulong, GameObject> _projectiles = new Dictionary<ulong, GameObject>();
        private readonly Dictionary<ulong, Vector3> _lastProjectilePositions = new Dictionary<ulong, Vector3>();
        private readonly List<ulong> _staleProjectileIds = new List<ulong>();
        private readonly BattlePresentationTracker _tracker = new BattlePresentationTracker();
        private GameObject? _arenaRoot;
        private BattleDefinition? _definition;
        private BattlePresentationCatalog? _catalog;
        private AudioSource? _ambientSource;
        private AudioSource? _uiSource;
        private BattleAnnouncementKind _lastAnnouncement;

        public Camera? GameplayCamera { get; private set; }

        public void Configure(BattleDefinition definition)
        {
            if (_definition is not null) return;
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _catalog = BattlePresentationCatalog.LoadRequired();
            CreateArena(definition);
            CreateCameraAndLighting();
            CreateAudio();
        }

        public void Present(BattleState state, PlayerSlot? localSlot)
        {
            if (!state.IsGameplayEnabled) return;
            _ = localSlot;
            Configure(state.Definition!);
            BattlePresentationEvents events = _tracker.Observe(state);
            for (int index = 0; index < state.PlayerCount; index++)
            {
                if (!_players.TryGetValue(index, out PlayerPresentation player))
                {
                    var playerObject = new GameObject($"P{index + 1} Presentation");
                    playerObject.transform.SetParent(transform, false);
                    player = playerObject.AddComponent<PlayerPresentation>();
                    player.Configure(new PlayerSlot(index), _catalog!);
                    _players.Add(index, player);
                }
                player.Present(state.GetPlayerState(new PlayerSlot(index)));
            }

            foreach (ulong id in events.SpawnedProjectileIds)
            {
                ProjectileState projectile = FindProjectile(state, id);
                if (state.Roster.TryGetSlot(projectile.OwnerPlayerId, out PlayerSlot owner) &&
                    _players.TryGetValue(owner.Value, out PlayerPresentation player))
                {
                    player.TriggerShoot();
                    SpawnBurst(player.Muzzle.position, BattlePresentationReadModel.GetTeamColor(owner), 7, 0.75f, _catalog!.MuzzleMaterial);
                    PlayWorld(_catalog!.ShootSound, player.Muzzle.position, 0.42f);
                }
            }

            foreach (int slot in events.DamagedSlots)
            {
                if (!_players.TryGetValue(slot, out PlayerPresentation player)) continue;
                player.TriggerHitFlash();
                SpawnBurst(player.transform.position + Vector3.up * 0.65f,
                    BattlePresentationReadModel.GetTeamColor(new PlayerSlot(slot)), 12, 1.05f);
                PlayWorld(_catalog!.PlayerHitSound, player.transform.position, 0.55f);
                if (state.GetPlayerState(new PlayerSlot(slot)).HitPoints == 0)
                    PlayWorld(_catalog.DeathSound, player.transform.position, 0.55f);
            }

            foreach (ulong id in events.RemovedProjectileIds)
            {
                if (!_lastProjectilePositions.TryGetValue(id, out Vector3 position)) continue;
                bool hitPlayer = events.DamagedSlots.Length > 0;
                SpawnBurst(position, hitPlayer ? Color.white : new Color(1f, 0.72f, 0.25f), hitPlayer ? (short)10 : (short)7, 0.8f);
                PlayWorld(hitPlayer ? _catalog!.PlayerHitSound : _catalog!.EnvironmentHitSound, position, 0.35f);
            }

            SynchronizeProjectiles(state);
            PresentAnnouncementSound(state);
        }

        public void Clear()
        {
            foreach (PlayerPresentation player in _players.Values) if (player is not null) DestroyView(player.gameObject);
            foreach (GameObject projectile in _projectiles.Values) if (projectile is not null) DestroyView(projectile);
            _players.Clear();
            _projectiles.Clear();
            _lastProjectilePositions.Clear();
            _staleProjectileIds.Clear();
            if (_arenaRoot is not null) DestroyView(_arenaRoot);
            if (GameplayCamera is not null) DestroyView(GameplayCamera.gameObject);
            if (_ambientSource is not null) DestroyView(_ambientSource.gameObject);
            if (_uiSource is not null) DestroyView(_uiSource.gameObject);
            _arenaRoot = null;
            GameplayCamera = null;
            _definition = null;
            _catalog = null;
        }

        public static Vector3 ToWorld(int x, float y, int z) => new Vector3(x * SimulationToWorld, y, z * SimulationToWorld);

        private void SynchronizeProjectiles(BattleState state)
        {
            _staleProjectileIds.Clear();
            foreach (ulong id in _projectiles.Keys) _staleProjectileIds.Add(id);
            for (int index = 0; index < state.ProjectileCount; index++)
            {
                ProjectileState value = state.GetProjectile(index);
                if (!_projectiles.TryGetValue(value.ProjectileId, out GameObject projectile))
                {
                    projectile = CreateProjectile(value.ProjectileId);
                    _projectiles.Add(value.ProjectileId, projectile);
                }
                Vector3 position = ToWorld(value.PositionX, 0.42f, value.PositionZ);
                projectile.transform.position = position;
                projectile.transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(value.DirectionX, 0f, value.DirectionZ));
                _lastProjectilePositions[value.ProjectileId] = position;
                _staleProjectileIds.Remove(value.ProjectileId);
            }
            foreach (ulong id in _staleProjectileIds)
            {
                DestroyView(_projectiles[id]);
                _projectiles.Remove(id);
            }
        }

        private GameObject CreateProjectile(ulong id)
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            projectile.name = "Energy Bolt " + id;
            projectile.transform.SetParent(transform, false);
            projectile.transform.localScale = new Vector3(0.055f, 0.12f, 0.055f);
            DestroyView(projectile.GetComponent<Collider>());
            projectile.GetComponent<Renderer>().sharedMaterial = _catalog!.ProjectileMaterial;
            TrailRenderer trail = projectile.AddComponent<TrailRenderer>();
            trail.time = 0.13f;
            trail.startWidth = 0.075f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.025f;
            trail.sharedMaterial = _catalog.ProjectileMaterial;
            trail.startColor = Color.white;
            trail.endColor = new Color(0.08f, 0.72f, 1f, 0f);
            return projectile;
        }

        private void CreateArena(BattleDefinition definition)
        {
            _arenaRoot = new GameObject("Sci-Fi Duel Arena");
            _arenaRoot.transform.SetParent(transform, false);
            ArenaRectangle bounds = definition.Arena.Bounds;
            float width = (bounds.MaxX - bounds.MinX) * SimulationToWorld;
            float depth = (bounds.MaxZ - bounds.MinZ) * SimulationToWorld;
            CreateBox("Combat Floor", new Vector3(0f, -0.12f, 0f), new Vector3(width, 0.2f, depth), _catalog!.FloorMaterial!);
            for (int index = 0; index < definition.Arena.ObstacleCount; index++)
            {
                ArenaRectangle obstacle = definition.Arena.GetObstacle(index);
                Vector3 position = ToWorld((obstacle.MinX + obstacle.MaxX) / 2, index == 0 ? 0.62f : 0.45f,
                    (obstacle.MinZ + obstacle.MaxZ) / 2);
                Vector3 scale = new Vector3((obstacle.MaxX - obstacle.MinX) * SimulationToWorld,
                    index == 0 ? 1.24f : 0.9f, (obstacle.MaxZ - obstacle.MinZ) * SimulationToWorld);
                CreateBox(index == 0 ? "Deterministic Central Reactor" : $"Deterministic Energy Relay {index}",
                    position, scale, _catalog.CoverMaterial!);
                GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cube);
                core.name = "Neutral Energy Strip";
                core.transform.SetParent(_arenaRoot.transform, false);
                core.transform.position = position + Vector3.up * (scale.y * 0.5f + 0.015f);
                core.transform.localScale = new Vector3(scale.x * 0.66f, 0.03f, scale.z * 0.22f);
                DestroyView(core.GetComponent<Collider>());
                core.GetComponent<Renderer>().sharedMaterial = _catalog.CyanMaterial;
            }

            float backZ = bounds.MaxZ * SimulationToWorld + 0.14f;
            float frontZ = bounds.MinZ * SimulationToWorld - 0.14f;
            float leftX = bounds.MinX * SimulationToWorld - 0.14f;
            float rightX = bounds.MaxX * SimulationToWorld + 0.14f;
            CreateBox("Back Wall", new Vector3(0f, 0.9f, backZ), new Vector3(width + 0.5f, 1.8f, 0.28f), _catalog.WallMaterial!);
            CreateBox("Left Wall", new Vector3(leftX, 0.65f, 0f), new Vector3(0.28f, 1.3f, depth), _catalog.WallMaterial!);
            CreateBox("Right Wall", new Vector3(rightX, 0.65f, 0f), new Vector3(0.28f, 1.3f, depth), _catalog.WallMaterial!);
            CreateBox("Low Front Edge", new Vector3(0f, 0.14f, frontZ), new Vector3(width + 0.5f, 0.28f, 0.24f), _catalog.WallMaterial!);
            CreateSpawnMarker(definition.Arena.GetSpawn(0), _catalog.CyanMaterial!, "P1 Spawn Marker");
            CreateSpawnMarker(definition.Arena.GetSpawn(1), _catalog.RedMaterial!, "P2 Spawn Marker");
            AddDecoration(_catalog.DoorFramePrefab, "Back Door Frame", new Vector3(0f, 0f, backZ - 0.12f), Quaternion.Euler(0f, 180f, 0f), 1.5f);
            AddDecoration(_catalog.ColumnPrefab, "Left Boundary Column", new Vector3(leftX - 0.2f, 0f, backZ), Quaternion.identity, 1.6f);
            AddDecoration(_catalog.ColumnPrefab, "Right Boundary Column", new Vector3(rightX + 0.2f, 0f, backZ), Quaternion.identity, 1.6f);
            AddDecoration(_catalog.ComputerPrefab, "Wall Computer", new Vector3(-3.6f, 0f, backZ - 0.2f), Quaternion.Euler(0f, 180f, 0f), 0.8f);
            AddDecoration(_catalog.CratePrefab, "Outside Crate", new Vector3(4.4f, 0f, backZ + 0.45f), Quaternion.identity, 0.65f);
        }

        private void CreateCameraAndLighting()
        {
            Camera? existing = Camera.main;
            if (existing is null)
            {
                var cameraObject = new GameObject("Gameplay Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(transform, false);
                GameplayCamera = cameraObject.AddComponent<Camera>();
            }
            else GameplayCamera = existing;
            ApplyGameplayCamera(GameplayCamera);
            var lightObject = new GameObject("Cool Arena Key Light");
            lightObject.transform.SetParent(_arenaRoot!.transform, false);
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.72f, 0.84f, 1f);
            light.intensity = 1.15f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.22f, 0.28f);
        }

        public static void ApplyGameplayCamera(Camera camera)
        {
            if (camera is null) throw new ArgumentNullException(nameof(camera));
            camera.transform.position = new Vector3(0f, 9.2f, -8.2f);
            camera.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
            camera.fieldOfView = 44f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.055f, 0.085f);
        }

        private void CreateAudio()
        {
            var ambient = new GameObject("Arena Ambient Audio");
            ambient.transform.SetParent(transform, false);
            _ambientSource = ambient.AddComponent<AudioSource>();
            _ambientSource.clip = _catalog!.AmbientSound;
            _ambientSource.loop = true;
            _ambientSource.volume = 0.13f;
            if (_ambientSource.clip is not null) _ambientSource.Play();
            var ui = new GameObject("Battle UI Audio");
            ui.transform.SetParent(transform, false);
            _uiSource = ui.AddComponent<AudioSource>();
            _uiSource.spatialBlend = 0f;
        }

        private void PresentAnnouncementSound(BattleState state)
        {
            BattlePresentationSnapshot model = BattlePresentationReadModel.Create(state);
            if (model.Announcement == BattleAnnouncementKind.None || model.Announcement == _lastAnnouncement) return;
            AudioClip? clip = model.Announcement switch
            {
                BattleAnnouncementKind.Countdown1 or BattleAnnouncementKind.Countdown2 or BattleAnnouncementKind.Countdown3 => _catalog!.CountdownSound,
                BattleAnnouncementKind.Fight => _catalog!.FightSound,
                BattleAnnouncementKind.RoundWin or BattleAnnouncementKind.Draw or BattleAnnouncementKind.MatchVictory => _catalog!.VictorySound,
                _ => null,
            };
            if (clip is not null) _uiSource?.PlayOneShot(clip, 0.45f);
            _lastAnnouncement = model.Announcement;
        }

        private void SpawnBurst(Vector3 position, Color color, short count, float speed, Material? material = null)
        {
            var effect = new GameObject("Presentation Burst");
            effect.transform.position = position;
            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.duration = 0.12f;
            main.startLifetime = 0.2f;
            main.startSpeed = speed;
            main.startSize = 0.13f;
            main.startColor = color;
            main.maxParticles = 24;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.06f;
            effect.GetComponent<ParticleSystemRenderer>().sharedMaterial = material ?? _catalog!.ParticleMaterial;
            particles.Play();
        }

        private void PlayWorld(AudioClip? clip, Vector3 position, float volume)
        {
            if (clip is null) return;
            var audio = new GameObject("Transient Battle Audio");
            audio.transform.position = position;
            AudioSource source = audio.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 0.8f;
            source.minDistance = 2f;
            source.maxDistance = 18f;
            source.pitch = 0.97f + UnityEngine.Random.value * 0.06f;
            source.Play();
            Destroy(audio, clip.length + 0.1f);
        }

        private GameObject CreateBox(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(_arenaRoot!.transform, false);
            box.transform.position = position;
            box.transform.localScale = scale;
            DestroyView(box.GetComponent<Collider>());
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        private void CreateSpawnMarker(ArenaPoint spawn, Material material, string name)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.transform.SetParent(_arenaRoot!.transform, false);
            marker.transform.position = ToWorld(spawn.X, 0.012f, spawn.Z);
            marker.transform.localScale = new Vector3(0.48f, 0.006f, 0.48f);
            DestroyView(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void AddDecoration(GameObject? prefab, string name, Vector3 position, Quaternion rotation, float targetHeight)
        {
            if (prefab is null) return;
            var holder = new GameObject(name);
            holder.transform.SetParent(_arenaRoot!.transform, false);
            holder.transform.position = position;
            holder.transform.rotation = rotation;
            GameObject instance = Instantiate(prefab, holder.transform, false);
            instance.name = name + " Model";
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) DestroyView(collider);
            foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true)) DestroyView(body);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            if (bounds.size.y > 0.001f) instance.transform.localScale *= targetHeight / bounds.size.y;
            bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            instance.transform.position += holder.transform.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (Renderer renderer in renderers) renderer.sharedMaterial = _catalog!.WallMaterial;
        }

        private static ProjectileState FindProjectile(BattleState state, ulong id)
        {
            for (int index = 0; index < state.ProjectileCount; index++)
            {
                ProjectileState projectile = state.GetProjectile(index);
                if (projectile.ProjectileId == id) return projectile;
            }
            throw new InvalidOperationException("A spawned projectile was not present in the observed state.");
        }

        private static void DestroyView(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
