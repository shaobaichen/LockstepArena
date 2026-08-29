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
            AddUInt32(ref hash, checked((uint)state.PlayerCount));

            for (int index = 0; index < state.PlayerCount; index++)
            {
                PlayerSlot slot = new PlayerSlot(index);
                AddUInt64(ref hash, state.Roster.GetPlayerId(slot).Value);
                PlayerState player = state.GetPlayerState(slot);
                AddInt32(ref hash, player.PositionX);
                AddInt32(ref hash, player.PositionZ);
                AddUInt16(ref hash, player.Aim);
            }

            return hash;
        }

        private static void AddUInt64(ref ulong hash, ulong value)
        {
            AddByte(ref hash, (byte)value);
            AddByte(ref hash, (byte)(value >> 8));
            AddByte(ref hash, (byte)(value >> 16));
            AddByte(ref hash, (byte)(value >> 24));
            AddByte(ref hash, (byte)(value >> 32));
            AddByte(ref hash, (byte)(value >> 40));
            AddByte(ref hash, (byte)(value >> 48));
            AddByte(ref hash, (byte)(value >> 56));
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
