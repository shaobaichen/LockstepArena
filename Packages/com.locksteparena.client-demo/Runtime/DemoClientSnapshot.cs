namespace LockstepArena.Client.Demo
{
    public sealed class DemoClientSnapshot
    {
        internal DemoClientSnapshot(DemoClientPhase phase, ulong sessionId, string nickname, ulong roomId, string roomName, ulong battleId, string lastRejection)
        {
            Phase = phase;
            SessionId = sessionId;
            Nickname = nickname;
            RoomId = roomId;
            RoomName = roomName;
            BattleId = battleId;
            LastRejection = lastRejection;
        }

        public DemoClientPhase Phase { get; }
        public ulong SessionId { get; }
        public string Nickname { get; }
        public ulong RoomId { get; }
        public string RoomName { get; }
        public ulong BattleId { get; }
        public string LastRejection { get; }
    }
}
