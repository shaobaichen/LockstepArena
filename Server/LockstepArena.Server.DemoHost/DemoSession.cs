using System;
using System.Collections.Generic;
using LockstepArena.Protocol.Wire;

namespace LockstepArena.Server.DemoHost
{
    internal enum DemoSessionPhase
    {
        Lobby,
        InRoom,
        PreparingBattle,
        InBattle,
        Settlement,
        Closed
    }

    internal sealed class DemoSession
    {
        private readonly List<ServerControlEventMessage> _events = new List<ServerControlEventMessage>();

        internal DemoSession(ulong sessionId, string nickname)
        {
            SessionId = sessionId;
            Nickname = nickname;
            Phase = DemoSessionPhase.Lobby;
        }

        internal ulong SessionId { get; }
        internal string Nickname { get; }
        internal DemoSessionPhase Phase { get; set; }
        internal ulong RoomId { get; set; }
        internal int JoinOrdinal { get; set; }
        internal bool IsReady { get; set; }
        internal int EventCount => _events.Count;
        internal ServerControlEventMessage LastEvent => _events.Count == 0 ? throw new InvalidOperationException("Session has no events.") : _events[_events.Count - 1];

        internal void Queue(ServerControlEventMessage message)
        {
            _events.Add(message ?? throw new ArgumentNullException(nameof(message)));
        }

        internal void ClearEvents()
        {
            _events.Clear();
        }

        internal bool TryDequeueEvent(out ServerControlEventMessage? message)
        {
            if (_events.Count == 0)
            {
                message = null;
                return false;
            }

            message = _events[0];
            _events.RemoveAt(0);
            return true;
        }
    }
}
