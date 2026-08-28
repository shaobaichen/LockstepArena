namespace LockstepArena.Simulation
{
    public static class StateDigest
    {
        private const ulong OffsetBasis = 14_695_981_039_346_656_037UL;
        private const ulong Prime = 1_099_511_628_211UL;

        public static ulong Compute(BattleState state)
        {
            ulong hash = OffsetBasis;
            AddUInt32(ref hash, state.Tick);
            AddInt32(ref hash, state.Player0.PositionX);
            AddInt32(ref hash, state.Player0.PositionZ);
            AddUInt16(ref hash, state.Player0.Aim);
            AddInt32(ref hash, state.Player1.PositionX);
            AddInt32(ref hash, state.Player1.PositionZ);
            AddUInt16(ref hash, state.Player1.Aim);
            return hash;
        }

        private static void AddInt32(ref ulong hash, int value)
        {
            AddUInt32(ref hash, unchecked((uint)value));
        }

        private static void AddUInt32(ref ulong hash, uint value)
        {
            AddByte(ref hash, (byte)value);
            AddByte(ref hash, (byte)(value >> 8));
            AddByte(ref hash, (byte)(value >> 16));
            AddByte(ref hash, (byte)(value >> 24));
        }

        private static void AddUInt16(ref ulong hash, ushort value)
        {
            AddByte(ref hash, (byte)value);
            AddByte(ref hash, (byte)(value >> 8));
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash = unchecked((hash ^ value) * Prime);
        }
    }
}
