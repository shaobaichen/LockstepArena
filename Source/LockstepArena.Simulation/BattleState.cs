namespace LockstepArena.Simulation
{
    public readonly struct BattleState
    {
        public BattleState(uint tick, PlayerState player0, PlayerState player1)
        {
            Tick = tick;
            Player0 = player0;
            Player1 = player1;
        }

        public uint Tick { get; }

        public PlayerState Player0 { get; }

        public PlayerState Player1 { get; }

        public static BattleState CreateInitial()
        {
            return new BattleState(
                0,
                new PlayerState(SimulationConfig.Player0SpawnX, SimulationConfig.PlayerSpawnZ, 0),
                new PlayerState(SimulationConfig.Player1SpawnX, SimulationConfig.PlayerSpawnZ, 0));
        }
    }
}
