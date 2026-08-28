using System;

namespace LockstepArena.Simulation
{
    public sealed class BattleSimulation
    {
        public BattleSimulation(BattleState initialState)
        {
            State = initialState;
        }

        public BattleState State { get; private set; }

        public void Step(FrameData frame)
        {
            if (frame.Tick != State.Tick)
            {
                throw new ArgumentException("Frame tick must match the current simulation tick.", nameof(frame));
            }

            PlayerState player0 = Move(State.Player0, frame.Player0Input);
            PlayerState player1 = Move(State.Player1, frame.Player1Input);
            State = new BattleState(checked(State.Tick + 1), player0, player1);
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
