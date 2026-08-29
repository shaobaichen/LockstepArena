using System;
using System.Collections.Generic;

namespace LockstepArena.Simulation
{
    public sealed class BattleState
    {
        private readonly PlayerState[] _players;

        public BattleState(
            uint tick,
            ActiveRoster roster,
            IReadOnlyList<PlayerState> statesInSlotOrder)
        {
            Roster = roster ?? throw new ArgumentNullException(nameof(roster));
            if (statesInSlotOrder is null)
            {
                throw new ArgumentNullException(nameof(statesInSlotOrder));
            }

            if (statesInSlotOrder.Count != roster.Count)
            {
                throw new ArgumentException(
                    "Player state count must match the active roster.",
                    nameof(statesInSlotOrder));
            }

            Tick = tick;
            _players = new PlayerState[statesInSlotOrder.Count];
            for (int index = 0; index < statesInSlotOrder.Count; index++)
            {
                _players[index] = statesInSlotOrder[index];
            }
        }

        public uint Tick { get; }

        public ActiveRoster Roster { get; }

        public int PlayerCount => _players.Length;

        public static BattleState CreateInitial(
            ActiveRoster roster,
            IReadOnlyList<PlayerState> statesInSlotOrder)
        {
            return new BattleState(0, roster, statesInSlotOrder);
        }

        public PlayerState GetPlayerState(PlayerSlot slot)
        {
            if (slot.Value >= _players.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), "Player slot is outside the battle roster.");
            }

            return _players[slot.Value];
        }
    }
}
