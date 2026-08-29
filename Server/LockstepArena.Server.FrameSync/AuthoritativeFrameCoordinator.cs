using System;
using System.Collections.Generic;
using LockstepArena.Simulation;

namespace LockstepArena.Server.FrameSync
{
    public sealed class AuthoritativeFrameCoordinator
    {
        private readonly uint _maxFutureTickOffset;
        private readonly int _authoritativeHistoryCapacity;
        private Dictionary<uint, StrictFrameCollector> _pendingByTick;
        private Queue<FrameData> _authoritativeHistory;

        public AuthoritativeFrameCoordinator(
            ActiveRoster roster,
            uint initialPublishTick,
            uint maxFutureTickOffset,
            int authoritativeHistoryCapacity)
        {
            Roster = roster ?? throw new ArgumentNullException(nameof(roster));
            if (authoritativeHistoryCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(authoritativeHistoryCapacity));
            }

            NextPublishTick = initialPublishTick;
            _maxFutureTickOffset = maxFutureTickOffset;
            _authoritativeHistoryCapacity = authoritativeHistoryCapacity;
            _pendingByTick = new Dictionary<uint, StrictFrameCollector>();
            _authoritativeHistory = new Queue<FrameData>();
        }

        public ActiveRoster Roster { get; }

        public uint NextPublishTick { get; private set; }

        public FrameData[] GetAuthoritativeHistorySnapshot()
        {
            return _authoritativeHistory.ToArray();
        }
    }
}
