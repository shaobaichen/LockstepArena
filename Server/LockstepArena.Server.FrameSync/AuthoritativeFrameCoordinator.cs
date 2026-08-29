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

        public FrameData[] Submit(PlayerId submittedPlayerId, InputFrame input)
        {
            if (input.Tick == uint.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(input),
                    "uint.MaxValue cannot be a consumable frame Tick.");
            }

            if (NextPublishTick == uint.MaxValue)
            {
                throw new InvalidOperationException("The coordinator has no publishable Tick remaining.");
            }

            if (input.Tick < NextPublishTick)
            {
                throw new ArgumentOutOfRangeException(nameof(input), "Input Tick is older than NextPublishTick.");
            }

            ulong upperBound = Math.Min(
                (ulong)NextPublishTick + _maxFutureTickOffset,
                (ulong)uint.MaxValue - 1UL);
            if ((ulong)input.Tick > upperBound)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(input),
                    "Input Tick exceeds the accepted future window.");
            }

            if (!_pendingByTick.TryGetValue(input.Tick, out StrictFrameCollector? collector))
            {
                StrictFrameCollector candidate = new StrictFrameCollector(Roster, input.Tick);
                candidate.Submit(submittedPlayerId, input);
                _pendingByTick.Add(input.Tick, candidate);
                collector = candidate;
            }
            else
            {
                collector.Submit(submittedPlayerId, input);
            }

            if (input.Tick != NextPublishTick || !collector.IsComplete)
            {
                return Array.Empty<FrameData>();
            }

            FrameData frame = collector.GetCompletedFrame();
            _pendingByTick.Remove(NextPublishTick);
            _authoritativeHistory.Enqueue(frame);
            while (_authoritativeHistory.Count > _authoritativeHistoryCapacity)
            {
                _authoritativeHistory.Dequeue();
            }

            NextPublishTick = checked(NextPublishTick + 1U);
            return new[] { frame };
        }

        public FrameData[] GetAuthoritativeHistorySnapshot()
        {
            return _authoritativeHistory.ToArray();
        }
    }
}
