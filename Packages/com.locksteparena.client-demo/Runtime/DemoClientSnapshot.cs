namespace LockstepArena.Client.Demo
{
    public sealed class DemoClientSnapshot
    {
        internal DemoClientSnapshot(
            DemoClientPhase phase,
            ulong sessionId,
            string nickname,
            ulong roomId,
            string roomName,
            ulong battleId,
            string lastRejection,
            uint serverStateTick,
            uint nextPublishTick,
            uint authoritativeTick,
            uint predictedTick,
            int pendingPredictionCount,
            int pendingAuthoritativeFrameCount,
            int replayFrameCount,
            bool latestDirty,
            int cumulativeDirtyFrameCount,
            ulong authoritativeDigest,
            ulong predictedDigest,
            bool settlementVerified)
        {
            Phase = phase;
            SessionId = sessionId;
            Nickname = nickname;
            RoomId = roomId;
            RoomName = roomName;
            BattleId = battleId;
            LastRejection = lastRejection;
            ServerStateTick = serverStateTick;
            NextPublishTick = nextPublishTick;
            AuthoritativeTick = authoritativeTick;
            PredictedTick = predictedTick;
            PendingPredictionCount = pendingPredictionCount;
            PendingAuthoritativeFrameCount = pendingAuthoritativeFrameCount;
            ReplayFrameCount = replayFrameCount;
            LatestDirty = latestDirty;
            CumulativeDirtyFrameCount = cumulativeDirtyFrameCount;
            AuthoritativeDigest = authoritativeDigest;
            PredictedDigest = predictedDigest;
            SettlementVerified = settlementVerified;
        }

        public DemoClientPhase Phase { get; }
        public ulong SessionId { get; }
        public string Nickname { get; }
        public ulong RoomId { get; }
        public string RoomName { get; }
        public ulong BattleId { get; }
        public string LastRejection { get; }
        public uint ServerStateTick { get; }
        public uint NextPublishTick { get; }
        public uint AuthoritativeTick { get; }
        public uint PredictedTick { get; }
        public int PendingPredictionCount { get; }
        public int PendingAuthoritativeFrameCount { get; }
        public int ReplayFrameCount { get; }
        public bool LatestDirty { get; }
        public int CumulativeDirtyFrameCount { get; }
        public ulong AuthoritativeDigest { get; }
        public ulong PredictedDigest { get; }
        public bool SettlementVerified { get; }
    }
}
