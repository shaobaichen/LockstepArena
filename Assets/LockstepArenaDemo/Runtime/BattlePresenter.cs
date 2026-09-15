#nullable enable
using System.Collections.Generic;
using LockstepArena.Simulation;
using UnityEngine;

namespace LockstepArena.Demo
{
    public sealed class BattlePresenter : MonoBehaviour
    {
        public const float WorldUnitsPerSimulationUnit = 1000f;
        private const float SimulationToWorld = 1f / WorldUnitsPerSimulationUnit;
        private readonly Dictionary<int, GameObject> _players = new Dictionary<int, GameObject>();
        private readonly Dictionary<ulong, GameObject> _projectiles = new Dictionary<ulong, GameObject>();
        private readonly List<ulong> _staleProjectileIds = new List<ulong>();
        private GameObject? _arenaRoot;
        private BattleDefinition? _definition;

        public Camera? GameplayCamera { get; private set; }

        public void Configure(BattleDefinition definition)
        {
            if (_definition is not null) return;
            _definition = definition;
            CreateArena(definition);
            CreateCamera();
        }

        public void Present(BattleState state, PlayerSlot? localSlot)
        {
            if (!state.IsGameplayEnabled) return;
            Configure(state.Definition!);
            for (int index = 0; index < state.PlayerCount; index++)
            {
                if (!_players.TryGetValue(index, out GameObject player))
                {
                    player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    player.name = "Player " + (index + 1);
                    player.transform.SetParent(transform, false);
                    player.GetComponent<Renderer>().material.color =
                        localSlot.HasValue && localSlot.Value.Value == index
                            ? new Color(0.15f, 0.65f, 1f)
                            : new Color(1f, 0.3f, 0.2f);
                    float diameter = state.Definition!.Gameplay.PlayerRadiusUnits * 2 * SimulationToWorld;
                    player.transform.localScale = new Vector3(diameter, 0.75f, diameter);
                    _players.Add(index, player);
                }
                PlayerState value = state.GetPlayerState(new PlayerSlot(index));
                player.transform.position = ToWorld(value.PositionX, 0.75f, value.PositionZ);
                BattleSimulation.GetAimDirection(value.Aim, out int directionX, out int directionZ);
                player.transform.rotation = Quaternion.LookRotation(new Vector3(directionX, 0f, directionZ));
                player.SetActive(value.HitPoints > 0);
            }

            _staleProjectileIds.Clear();
            foreach (ulong id in _projectiles.Keys) _staleProjectileIds.Add(id);
            for (int index = 0; index < state.ProjectileCount; index++)
            {
                ProjectileState value = state.GetProjectile(index);
                if (!_projectiles.TryGetValue(value.ProjectileId, out GameObject projectile))
                {
                    projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    projectile.name = "Projectile " + value.ProjectileId;
                    projectile.transform.SetParent(transform, false);
                    projectile.GetComponent<Renderer>().material.color = new Color(1f, 0.85f, 0.1f);
                    float diameter = state.Definition!.Gameplay.ProjectileRadiusUnits * 2 * SimulationToWorld;
                    projectile.transform.localScale = Vector3.one * diameter;
                    _projectiles.Add(value.ProjectileId, projectile);
                }
                projectile.transform.position = ToWorld(value.PositionX, 0.35f, value.PositionZ);
                _staleProjectileIds.Remove(value.ProjectileId);
            }
            for (int index = 0; index < _staleProjectileIds.Count; index++)
            {
                ulong id = _staleProjectileIds[index];
                Destroy(_projectiles[id]);
                _projectiles.Remove(id);
            }
        }

        public void Clear()
        {
            foreach (GameObject player in _players.Values) if (player is not null) Destroy(player);
            foreach (GameObject projectile in _projectiles.Values) if (projectile is not null) Destroy(projectile);
            _players.Clear();
            _projectiles.Clear();
            _staleProjectileIds.Clear();
            if (_arenaRoot is not null) Destroy(_arenaRoot);
            if (GameplayCamera is not null) Destroy(GameplayCamera.gameObject);
            _arenaRoot = null;
            GameplayCamera = null;
            _definition = null;
        }

        private void CreateArena(BattleDefinition definition)
        {
            _arenaRoot = new GameObject("Arena View");
            _arenaRoot.transform.SetParent(transform, false);
            ArenaRectangle bounds = definition.Arena.Bounds;
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Arena Floor";
            floor.transform.SetParent(_arenaRoot.transform, false);
            floor.transform.position = ToWorld((bounds.MinX + bounds.MaxX) / 2, -0.1f, (bounds.MinZ + bounds.MaxZ) / 2);
            floor.transform.localScale = new Vector3((bounds.MaxX - bounds.MinX) * SimulationToWorld,
                0.2f, (bounds.MaxZ - bounds.MinZ) * SimulationToWorld);
            floor.GetComponent<Renderer>().material.color = new Color(0.18f, 0.24f, 0.2f);
            for (int index = 0; index < definition.Arena.ObstacleCount; index++)
            {
                ArenaRectangle obstacle = definition.Arena.GetObstacle(index);
                GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cover.name = "Cover " + index;
                cover.transform.SetParent(_arenaRoot.transform, false);
                cover.transform.position = ToWorld((obstacle.MinX + obstacle.MaxX) / 2, 0.75f, (obstacle.MinZ + obstacle.MaxZ) / 2);
                cover.transform.localScale = new Vector3((obstacle.MaxX - obstacle.MinX) * SimulationToWorld,
                    1.5f, (obstacle.MaxZ - obstacle.MinZ) * SimulationToWorld);
                cover.GetComponent<Renderer>().material.color = new Color(0.35f, 0.38f, 0.42f);
            }
        }

        private void CreateCamera()
        {
            Camera? existing = Camera.main;
            if (existing is not null) { GameplayCamera = existing; return; }
            var cameraObject = new GameObject("Gameplay Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.position = new Vector3(0f, 10f, -8f);
            cameraObject.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            GameplayCamera = cameraObject.AddComponent<Camera>();
        }

        private static Vector3 ToWorld(int x, float y, int z) =>
            new Vector3(x * SimulationToWorld, y, z * SimulationToWorld);
    }
}
