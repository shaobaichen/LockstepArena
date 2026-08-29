namespace LockstepArena.Simulation.Verification
{
    public readonly struct Gate2GoldenVectorResult
    {
        public Gate2GoldenVectorResult(BattleState state, ulong digest)
        {
            State = state;
            Digest = digest;
        }

        public BattleState State { get; }

        public ulong Digest { get; }
    }

    public static class Gate2GoldenVector
    {
        public const uint TickCount = 1_000;

        public static Gate2GoldenVectorResult Run()
        {
            BattleSimulation simulation = new BattleSimulation(BattleState.CreateInitial());

            for (uint tick = 0; tick < TickCount; tick++)
            {
                CreateInputs(tick, out InputFrame player0, out InputFrame player1);
                simulation.Step(new FrameData(player0, player1));
            }

            BattleState state = simulation.State;
            return new Gate2GoldenVectorResult(state, StateDigest.Compute(state));
        }

        private static void CreateInputs(
            uint tick,
            out InputFrame player0,
            out InputFrame player1)
        {
            int phase = (int)(tick % 400);
            sbyte player0X = 0;
            sbyte player0Z = 0;
            sbyte player1X = 0;
            sbyte player1Z = 0;

            if (phase < 100)
            {
                player0X = 1;
                player1X = -1;
            }
            else if (phase >= 150 && phase < 250)
            {
                player0X = -1;
                player1X = 1;
            }
            else if (phase >= 250 && phase < 325)
            {
                player0Z = 1;
                player1Z = -1;
            }
            else if (phase >= 325)
            {
                player0Z = -1;
                player1Z = 1;
            }

            ushort player0Aim = unchecked((ushort)((tick * 997U) + 123U));
            ushort player1Aim = unchecked((ushort)((tick * 619U) + 45_678U));
            player0 = new InputFrame(tick, 0, player0X, player0Z, player0Aim);
            player1 = new InputFrame(tick, 1, player1X, player1Z, player1Aim);
        }
    }
}
