using System;
using System.Collections.Generic;

namespace LockstepArena.Simulation
{
    public sealed class BattleSimulation
    {
        public BattleSimulation(BattleState initialState)
        {
            State = initialState ?? throw new ArgumentNullException(nameof(initialState));
        }

        public BattleState State { get; private set; }

        public void Step(FrameData frame)
        {
            if (frame is null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            BattleState current = State;
            if (frame.Tick != current.Tick)
            {
                throw new ArgumentException("Frame tick must match the current simulation tick.", nameof(frame));
            }

            if (!current.Roster.HasSameStructure(frame.Roster))
            {
                throw new ArgumentException("Frame roster must match the simulation roster.", nameof(frame));
            }

            if (current.IsGameplayEnabled)
            {
                State = StepGameplay(current, frame);
                return;
            }

            PlayerState[] nextPlayers = new PlayerState[current.PlayerCount];
            for (int index = 0; index < nextPlayers.Length; index++)
            {
                PlayerSlot slot = new PlayerSlot(index);
                nextPlayers[index] = Move(current.GetPlayerState(slot), frame.GetInput(slot));
            }

            uint nextTick = checked(current.Tick + 1);
            BattleState nextState = new BattleState(nextTick, current.Roster, nextPlayers);
            State = nextState;
        }

        private static BattleState StepGameplay(BattleState current, FrameData frame)
        {
            uint nextTick = checked(current.Tick + 1);
            BattleDefinition definition = current.Definition!;
            GameplayConfig config = definition.Gameplay;

            if (current.Phase == BattlePhase.MatchEnded)
                return Copy(current, nextTick, current.Phase, current.PhaseTicksRemaining, current.RoundTicksRemaining,
                    current.RoundResult, current.MatchWinnerSlot, CopyPlayers(current), CopyProjectiles(current), current.NextProjectileId);

            if (current.Phase == BattlePhase.RoundCountdown)
            {
                uint remaining = current.PhaseTicksRemaining > 0 ? current.PhaseTicksRemaining - 1 : 0;
                BattlePhase phase = remaining == 0 ? BattlePhase.Playing : BattlePhase.RoundCountdown;
                return Copy(current, nextTick, phase, remaining, current.RoundTicksRemaining,
                    current.RoundResult, null, CopyPlayers(current), CopyProjectiles(current), current.NextProjectileId);
            }

            if (current.Phase == BattlePhase.RoundEnded)
            {
                uint remaining = current.PhaseTicksRemaining > 0 ? current.PhaseTicksRemaining - 1 : 0;
                if (remaining == 0) return ResetRound(current, nextTick);
                return Copy(current, nextTick, BattlePhase.RoundEnded, remaining, current.RoundTicksRemaining,
                    current.RoundResult, null, CopyPlayers(current), Array.Empty<ProjectileState>(), current.NextProjectileId);
            }

            PlayerState[] players = CopyPlayers(current);
            for (int index = 0; index < players.Length; index++)
            {
                InputFrame input = frame.GetInput(new PlayerSlot(index));
                PlayerState player = players[index];
                ArenaPoint moved = MoveGameplay(player, input, definition);
                uint cooldown = player.FireCooldownTicks > 0 ? player.FireCooldownTicks - 1 : 0;
                players[index] = new PlayerState(moved.X, moved.Z, input.Aim, player.HitPoints, player.RoundWins, cooldown);
            }

            var projectiles = new List<ProjectileState>(current.ProjectileCount + current.PlayerCount);
            for (int index = 0; index < current.ProjectileCount; index++) projectiles.Add(current.GetProjectile(index));
            ulong nextProjectileId = current.NextProjectileId;
            for (int index = 0; index < players.Length; index++)
            {
                InputFrame input = frame.GetInput(new PlayerSlot(index));
                PlayerState player = players[index];
                if (!input.Fire || player.FireCooldownTicks != 0) continue;
                GetAimDirection(input.Aim, out int directionX, out int directionZ);
                int spawnX = AddScaled(player.PositionX, directionX, config.MuzzleOffsetUnits);
                int spawnZ = AddScaled(player.PositionZ, directionZ, config.MuzzleOffsetUnits);
                projectiles.Add(new ProjectileState(nextProjectileId, current.Roster.GetPlayerId(new PlayerSlot(index)),
                    spawnX, spawnZ, directionX, directionZ, config.ProjectileLifetimeTicks));
                nextProjectileId = checked(nextProjectileId + 1);
                players[index] = new PlayerState(player.PositionX, player.PositionZ, player.Aim,
                    player.HitPoints, player.RoundWins, config.FireIntervalTicks);
            }

            var survivors = new List<ProjectileState>(projectiles.Count);
            var damage = new long[players.Length];
            for (int index = 0; index < projectiles.Count; index++)
            {
                ProjectileState projectile = projectiles[index];
                int deltaX = Scale(projectile.DirectionX, config.ProjectileUnitsPerTick);
                int deltaZ = Scale(projectile.DirectionZ, config.ProjectileUnitsPerTick);
                int hitSlot = Sweep(projectile, deltaX, deltaZ, current.Roster, players, definition);
                if (hitSlot >= 0)
                {
                    damage[hitSlot] = checked(damage[hitSlot] + config.ProjectileDamage);
                    continue;
                }
                if (hitSlot == ObstacleHit) continue;
                uint lifetime = projectile.LifetimeTicksRemaining - 1;
                if (lifetime == 0) continue;
                survivors.Add(new ProjectileState(projectile.ProjectileId, projectile.OwnerPlayerId,
                    checked(projectile.PositionX + deltaX), checked(projectile.PositionZ + deltaZ),
                    projectile.DirectionX, projectile.DirectionZ, lifetime));
            }

            bool anyDead = false;
            for (int index = 0; index < players.Length; index++)
            {
                PlayerState player = players[index];
                int hitPoints = damage[index] >= player.HitPoints ? 0 : player.HitPoints - checked((int)damage[index]);
                players[index] = new PlayerState(player.PositionX, player.PositionZ, player.Aim,
                    hitPoints, player.RoundWins, player.FireCooldownTicks);
                anyDead |= hitPoints == 0;
            }

            if (anyDead)
                return EndRound(current, nextTick, players, DetermineDeathResult(players), nextProjectileId);

            uint roundTicks = current.RoundTicksRemaining > 0 ? current.RoundTicksRemaining - 1 : 0;
            if (roundTicks == 0)
                return EndRound(current, nextTick, players, DetermineTimeoutResult(players), nextProjectileId);

            return Copy(current, nextTick, BattlePhase.Playing, 0, roundTicks, RoundResult.None, null,
                players, survivors.ToArray(), nextProjectileId);
        }

        private const int NoHit = -1;
        private const int ObstacleHit = -2;

        private static int Sweep(
            ProjectileState projectile,
            int deltaX,
            int deltaZ,
            ActiveRoster roster,
            PlayerState[] players,
            BattleDefinition definition)
        {
            int steps = Math.Max(Math.Abs(deltaX), Math.Abs(deltaZ));
            if (steps == 0) return NoHit;
            int projectileRadius = definition.Gameplay.ProjectileRadiusUnits;
            for (int step = 1; step <= steps; step++)
            {
                int x = checked(projectile.PositionX + (int)((long)deltaX * step / steps));
                int z = checked(projectile.PositionZ + (int)((long)deltaZ * step / steps));
                if (HitsArena(x, z, projectileRadius, definition.Arena)) return ObstacleHit;
                for (int slotValue = 0; slotValue < players.Length; slotValue++)
                {
                    var slot = new PlayerSlot(slotValue);
                    if (roster.GetPlayerId(slot) == projectile.OwnerPlayerId) continue;
                    PlayerState player = players[slotValue];
                    if (player.HitPoints > 0 && HitsCircle(x, z, player.PositionX, player.PositionZ,
                        checked(projectileRadius + definition.Gameplay.PlayerRadiusUnits))) return slotValue;
                }
            }
            return NoHit;
        }

        private static bool HitsArena(int x, int z, int radius, ArenaConfig arena)
        {
            ArenaRectangle bounds = arena.Bounds;
            if ((long)x - radius < bounds.MinX || (long)x + radius > bounds.MaxX ||
                (long)z - radius < bounds.MinZ || (long)z + radius > bounds.MaxZ) return true;
            for (int index = 0; index < arena.ObstacleCount; index++)
            {
                ArenaRectangle obstacle = arena.GetObstacle(index);
                if ((long)x >= (long)obstacle.MinX - radius && (long)x <= (long)obstacle.MaxX + radius &&
                    (long)z >= (long)obstacle.MinZ - radius && (long)z <= (long)obstacle.MaxZ + radius) return true;
            }
            return false;
        }

        private static bool HitsCircle(int x, int z, int centerX, int centerZ, int radius)
        {
            ulong dx = (ulong)Math.Abs((long)x - centerX);
            ulong dz = (ulong)Math.Abs((long)z - centerZ);
            ulong r = checked((ulong)radius);
            if (dx > r || dz > r) return false;
            ulong limit = r * r;
            ulong xSquared = dx * dx;
            return xSquared <= limit && dz * dz <= limit - xSquared;
        }

        private static ArenaPoint MoveGameplay(PlayerState player, InputFrame input, BattleDefinition definition)
        {
            int speed = definition.Gameplay.MoveUnitsPerTick;
            int deltaX = input.MoveX == 0 ? 0 : input.MoveX * (input.MoveZ == 0 ? speed : speed * 707 / 1000);
            int deltaZ = input.MoveZ == 0 ? 0 : input.MoveZ * (input.MoveX == 0 ? speed : speed * 707 / 1000);
            int x = ResolveX(player.PositionX, player.PositionZ, deltaX, definition);
            int z = ResolveZ(x, player.PositionZ, deltaZ, definition);
            return new ArenaPoint(x, z);
        }

        private static int ResolveX(int oldX, int z, int delta, BattleDefinition definition)
        {
            int radius = definition.Gameplay.PlayerRadiusUnits;
            ArenaRectangle bounds = definition.Arena.Bounds;
            int candidate = Clamp(checked(oldX + delta), checked(bounds.MinX + radius), checked(bounds.MaxX - radius));
            if (delta == 0) return candidate;
            for (int index = 0; index < definition.Arena.ObstacleCount; index++)
            {
                ArenaRectangle obstacle = definition.Arena.GetObstacle(index);
                if ((long)z < (long)obstacle.MinZ - radius || (long)z > (long)obstacle.MaxZ + radius) continue;
                int minimum = checked(obstacle.MinX - radius);
                int maximum = checked(obstacle.MaxX + radius);
                if (delta > 0 && oldX <= minimum && candidate > minimum) candidate = Math.Min(candidate, minimum);
                else if (delta < 0 && oldX >= maximum && candidate < maximum) candidate = Math.Max(candidate, maximum);
            }
            return candidate;
        }

        private static int ResolveZ(int x, int oldZ, int delta, BattleDefinition definition)
        {
            int radius = definition.Gameplay.PlayerRadiusUnits;
            ArenaRectangle bounds = definition.Arena.Bounds;
            int candidate = Clamp(checked(oldZ + delta), checked(bounds.MinZ + radius), checked(bounds.MaxZ - radius));
            if (delta == 0) return candidate;
            for (int index = 0; index < definition.Arena.ObstacleCount; index++)
            {
                ArenaRectangle obstacle = definition.Arena.GetObstacle(index);
                if ((long)x < (long)obstacle.MinX - radius || (long)x > (long)obstacle.MaxX + radius) continue;
                int minimum = checked(obstacle.MinZ - radius);
                int maximum = checked(obstacle.MaxZ + radius);
                if (delta > 0 && oldZ <= minimum && candidate > minimum) candidate = Math.Min(candidate, minimum);
                else if (delta < 0 && oldZ >= maximum && candidate < maximum) candidate = Math.Max(candidate, maximum);
            }
            return candidate;
        }

        private static RoundResult DetermineDeathResult(PlayerState[] players)
        {
            int survivor = -1;
            for (int index = 0; index < players.Length; index++)
            {
                if (players[index].HitPoints == 0) continue;
                if (survivor >= 0) return RoundResult.Draw;
                survivor = index;
            }
            return survivor < 0 ? RoundResult.Draw : RoundResult.PlayerWin(new PlayerSlot(survivor));
        }

        private static RoundResult DetermineTimeoutResult(PlayerState[] players)
        {
            int winner = -1;
            int highest = -1;
            bool tied = false;
            for (int index = 0; index < players.Length; index++)
            {
                if (players[index].HitPoints > highest) { highest = players[index].HitPoints; winner = index; tied = false; }
                else if (players[index].HitPoints == highest) tied = true;
            }
            return tied ? RoundResult.Draw : RoundResult.PlayerWin(new PlayerSlot(winner));
        }

        private static BattleState EndRound(BattleState current, uint nextTick, PlayerState[] players, RoundResult result, ulong nextProjectileId)
        {
            PlayerSlot? matchWinner = null;
            if (result.HasWinner)
            {
                int winnerIndex = result.WinnerSlot.Value;
                PlayerState winner = players[winnerIndex];
                winner = new PlayerState(winner.PositionX, winner.PositionZ, winner.Aim,
                    winner.HitPoints, checked(winner.RoundWins + 1), winner.FireCooldownTicks);
                players[winnerIndex] = winner;
                if (winner.RoundWins >= current.Definition!.Gameplay.RoundsToWin) matchWinner = result.WinnerSlot;
            }
            BattlePhase phase = matchWinner.HasValue ? BattlePhase.MatchEnded : BattlePhase.RoundEnded;
            uint delay = matchWinner.HasValue ? 0 : current.Definition!.Gameplay.RoundEndDelayTicks;
            return Copy(current, nextTick, phase, delay, 0, result, matchWinner,
                players, Array.Empty<ProjectileState>(), nextProjectileId);
        }

        private static BattleState ResetRound(BattleState current, uint nextTick)
        {
            BattleDefinition definition = current.Definition!;
            var players = new PlayerState[current.PlayerCount];
            for (int index = 0; index < players.Length; index++)
            {
                ArenaPoint spawn = definition.Arena.GetSpawn(index);
                int wins = current.GetPlayerState(new PlayerSlot(index)).RoundWins;
                players[index] = new PlayerState(spawn.X, spawn.Z, index == 0 ? (ushort)0 : (ushort)32_768,
                    definition.Gameplay.MaxHitPoints, wins, 0);
            }
            return Copy(current, nextTick, BattlePhase.RoundCountdown, definition.Gameplay.RoundCountdownTicks,
                definition.Gameplay.RoundDurationTicks, RoundResult.None, null, players,
                Array.Empty<ProjectileState>(), current.NextProjectileId);
        }

        private static BattleState Copy(BattleState current, uint tick, BattlePhase phase, uint phaseTicks,
            uint roundTicks, RoundResult roundResult, PlayerSlot? matchWinner, PlayerState[] players,
            ProjectileState[] projectiles, ulong nextProjectileId)
        {
            return new BattleState(tick, current.Roster, players, current.Definition!, phase, phaseTicks,
                roundTicks, roundResult, matchWinner, projectiles, nextProjectileId);
        }

        private static PlayerState[] CopyPlayers(BattleState state)
        {
            var players = new PlayerState[state.PlayerCount];
            for (int index = 0; index < players.Length; index++) players[index] = state.GetPlayerState(new PlayerSlot(index));
            return players;
        }

        private static ProjectileState[] CopyProjectiles(BattleState state)
        {
            var projectiles = new ProjectileState[state.ProjectileCount];
            for (int index = 0; index < projectiles.Length; index++) projectiles[index] = state.GetProjectile(index);
            return projectiles;
        }

        private static void GetAimDirection(ushort aim, out int x, out int z)
        {
            switch (((aim + 4_096) / 8_192) & 7)
            {
                case 0: x = 1000; z = 0; break;
                case 1: x = 707; z = 707; break;
                case 2: x = 0; z = 1000; break;
                case 3: x = -707; z = 707; break;
                case 4: x = -1000; z = 0; break;
                case 5: x = -707; z = -707; break;
                case 6: x = 0; z = -1000; break;
                default: x = 707; z = -707; break;
            }
        }

        private static int AddScaled(int origin, int direction, int distance) => checked(origin + Scale(direction, distance));
        private static int Scale(int direction, int distance) => checked((int)((long)direction * distance / 1000));

        private static PlayerState Move(PlayerState player, InputFrame input)
        {
            int positionX = Clamp(
                player.PositionX + (input.MoveX * SimulationConfig.MoveUnitsPerTick),
                SimulationConfig.ArenaMinX,
                SimulationConfig.ArenaMaxX);
            int positionZ = Clamp(
                player.PositionZ + (input.MoveZ * SimulationConfig.MoveUnitsPerTick),
                SimulationConfig.ArenaMinZ,
                SimulationConfig.ArenaMaxZ);

            return new PlayerState(positionX, positionZ, input.Aim);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }
    }
}
