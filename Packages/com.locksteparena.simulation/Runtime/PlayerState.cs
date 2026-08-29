namespace LockstepArena.Simulation
{
    public readonly struct PlayerState
    {
        public PlayerState(int positionX, int positionZ, ushort aim)
        {
            PositionX = positionX;
            PositionZ = positionZ;
            Aim = aim;
        }

        public int PositionX { get; }

        public int PositionZ { get; }

        public ushort Aim { get; }
    }
}
