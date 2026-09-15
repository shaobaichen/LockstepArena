using System;

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
