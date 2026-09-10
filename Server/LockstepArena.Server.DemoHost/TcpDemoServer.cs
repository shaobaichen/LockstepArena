using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;

namespace LockstepArena.Server.DemoHost
{
    public sealed class TcpDemoServer : IDisposable
    {
        private readonly DemoServerOptions _options;
        private readonly List<DemoSession> _sessions = new List<DemoSession>();
        private readonly List<DemoRoom> _rooms = new List<DemoRoom>();
        private ulong _nextSessionId = 1;
        private ulong _nextRoomId = 1;
        private ulong _nextBattleId = 1;
        private bool _disposed;

        public TcpDemoServer(DemoServerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            ControlPort = options.ControlPort;
            BattlePort = options.BattlePort;
        }

        public int ControlPort { get; }
        public int BattlePort { get; }
        public int SessionCount => _sessions.Count;
        public int RoomCount => _rooms.Count;

        public DemoServerPumpResult PumpOnce()
        {
            ThrowIfDisposed();
            return new DemoServerPumpResult(0, 0, 0, 0, 0, 0);
        }

        internal DemoSession EnterSession(string nickname)
        {
            ThrowIfDisposed();
            string normalized = ValidateName(nickname, 32, nameof(nickname));
            if (_sessions.Count >= _options.MaxSessions) throw new InvalidOperationException("Session capacity is full.");
            for (int index = 0; index < _sessions.Count; index++)
            {
                if (string.Equals(_sessions[index].Nickname, normalized, StringComparison.Ordinal)) throw new InvalidOperationException("Nickname is already active.");
            }

            ulong id = PeekNext(_nextSessionId, "Session identifiers are exhausted.");
            var session = new DemoSession(id, normalized);
            session.Queue(new ServerControlEventMessage
            {
                SessionEntered = new SessionEnteredEventMessage { SessionId = id, Nickname = normalized },
            });
            _sessions.Add(session);
            CommitNext(ref _nextSessionId, id);
            return session;
        }

        internal DemoRoom CreateRoom(ulong sessionId, string roomName, int capacity)
        {
            ThrowIfDisposed();
            DemoSession host = GetSession(sessionId);
            RequirePhase(host, DemoSessionPhase.Lobby);
            string normalized = ValidateName(roomName, 48, nameof(roomName));
            if (capacity < 2 || capacity > _options.MaxRoomCapacity) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (_rooms.Count >= _options.MaxRooms) throw new InvalidOperationException("Room capacity is full.");
            ulong id = PeekNext(_nextRoomId, "Room identifiers are exhausted.");

            host.RoomId = id;
            host.JoinOrdinal = 0;
            host.IsReady = false;
            host.Phase = DemoSessionPhase.InRoom;
            var room = new DemoRoom(id, normalized, sessionId, capacity, host);
            _rooms.Add(room);
            CommitNext(ref _nextRoomId, id);
            BroadcastSnapshot(room);
            return room;
        }

        internal RoomSummaryMessage[] GetRoomSummaries()
        {
            ThrowIfDisposed();
            var result = new RoomSummaryMessage[_rooms.Count];
            for (int index = 0; index < _rooms.Count; index++)
            {
                DemoRoom room = _rooms[index];
                result[index] = new RoomSummaryMessage
                {
                    RoomId = room.RoomId,
                    RoomName = room.RoomName,
                    HostNickname = room.GetParticipant(0).Nickname,
                    ParticipantCount = checked((uint)room.ParticipantCount),
                    Capacity = checked((uint)room.Capacity),
                    Lifecycle = ServerControlProtocol.ToWire(room.Lifecycle),
                };
            }

            return result;
        }

        internal void JoinRoom(ulong sessionId, ulong roomId)
        {
            ThrowIfDisposed();
            DemoSession session = GetSession(sessionId);
            RequirePhase(session, DemoSessionPhase.Lobby);
            DemoRoom room = GetRoom(roomId);
            if (room.Lifecycle != DemoRoomLifecycle.Open) throw new InvalidOperationException("Room is not open.");
            if (room.IndexOf(sessionId) >= 0) throw new InvalidOperationException("Session is already in the room.");
            if (room.ParticipantCount >= room.Capacity) throw new InvalidOperationException("Room is full.");

            session.RoomId = roomId;
            session.JoinOrdinal = room.ParticipantCount;
            session.IsReady = false;
            session.Phase = DemoSessionPhase.InRoom;
            room.Add(session);
            BroadcastSnapshot(room);
        }

        internal void SetReady(ulong sessionId, bool isReady)
        {
            ThrowIfDisposed();
            DemoSession session = GetSession(sessionId);
            RequirePhase(session, DemoSessionPhase.InRoom);
            DemoRoom room = GetRoom(session.RoomId);
            if (room.Lifecycle != DemoRoomLifecycle.Open) throw new InvalidOperationException("Room is not open.");
            session.IsReady = isReady;
            BroadcastSnapshot(room);
        }

        internal void LeaveRoom(ulong sessionId)
        {
            ThrowIfDisposed();
            DemoSession session = GetSession(sessionId);
            RequirePhase(session, DemoSessionPhase.InRoom);
            DemoRoom room = GetRoom(session.RoomId);
            if (room.Lifecycle != DemoRoomLifecycle.Open) throw new InvalidOperationException("Room membership is frozen.");
            if (session.SessionId == room.HostSessionId)
            {
                RemoveOpenHostRoom(room, session.SessionId);
                return;
            }

            int participantIndex = room.IndexOf(sessionId);
            if (participantIndex < 0) throw new InvalidOperationException("Session is not in its room.");
            room.RemoveAt(participantIndex);
            MoveToLobby(session);
            BroadcastSnapshot(room);
        }

        internal BattlePreparation StartBattle(ulong sessionId)
        {
            ThrowIfDisposed();
            DemoSession requester = GetSession(sessionId);
            RequirePhase(requester, DemoSessionPhase.InRoom);
            DemoRoom room = GetRoom(requester.RoomId);
            if (room.Lifecycle != DemoRoomLifecycle.Open) throw new InvalidOperationException("Room is not open.");
            if (room.HostSessionId != sessionId) throw new InvalidOperationException("Only the host may start.");
            if (room.ParticipantCount != room.Capacity) throw new InvalidOperationException("Room is not full.");
            DemoSession[] participants = room.CopyParticipants();
            for (int index = 0; index < participants.Length; index++)
            {
                if (!participants[index].IsReady) throw new InvalidOperationException("Every participant must be ready.");
                if (participants[index].Phase != DemoSessionPhase.InRoom) throw new InvalidOperationException("Participant control session is not active.");
            }

            if (room.Preparation is not null) throw new InvalidOperationException("Room is already prepared.");
            ulong battleId = PeekNext(_nextBattleId, "Battle identifiers are exhausted.");
            ulong finalTickWide = _options.BattleDurationTicks;
            if (finalTickWide > uint.MaxValue) throw new InvalidOperationException("Battle duration exceeds Tick range.");
            if (_options.SpawnStateCount < participants.Length) throw new InvalidOperationException("Spawn configuration is incomplete.");

            var playerIds = new PlayerId[participants.Length];
            var states = new PlayerState[participants.Length];
            for (int index = 0; index < participants.Length; index++)
            {
                playerIds[index] = new PlayerId(participants[index].SessionId);
                states[index] = _options.GetSpawnState(new PlayerSlot(index));
            }

            var roster = new ActiveRoster(playerIds);
            BattleState initialState = BattleState.CreateInitial(roster, states);
            uint finalStateTick = checked(initialState.Tick + _options.BattleDurationTicks);
            BattleBootstrapMessage bootstrap = ProtocolMapper.ToWireBattleBootstrap(battleId, initialState, _options.BattleDurationTicks, _options.InputDelayTicks, finalStateTick);
            var tickets = new byte[participants.Length][];
            var events = new BattlePreparingEventMessage[participants.Length];
            for (int index = 0; index < participants.Length; index++)
            {
                byte[] ticket = CreateUniqueTicket(tickets, index);
                tickets[index] = ticket;
                events[index] = new BattlePreparingEventMessage
                {
                    RoomId = room.RoomId,
                    BattleId = battleId,
                    BattleTicket = ByteString.CopyFrom(ticket),
                    BattlePort = checked((uint)BattlePort),
                    LocalPlayerId = participants[index].SessionId,
                    LocalPlayerSlot = checked((uint)index),
                    Bootstrap = bootstrap.Clone(),
                };
            }

            var preparation = new BattlePreparation(battleId, initialState, finalStateTick, participants, tickets, events);
            for (int index = 0; index < participants.Length; index++)
            {
                participants[index].Phase = DemoSessionPhase.PreparingBattle;
                participants[index].Queue(new ServerControlEventMessage { BattlePreparing = events[index].Clone() });
            }

            room.Preparation = preparation;
            room.Lifecycle = DemoRoomLifecycle.PreparingBattle;
            CommitNext(ref _nextBattleId, battleId);
            return preparation;
        }

        internal DemoRoom GetRoom(ulong roomId)
        {
            for (int index = 0; index < _rooms.Count; index++) if (_rooms[index].RoomId == roomId) return _rooms[index];
            throw new InvalidOperationException("Room was not found.");
        }

        internal DemoSession GetSession(ulong sessionId)
        {
            for (int index = 0; index < _sessions.Count; index++) if (_sessions[index].SessionId == sessionId) return _sessions[index];
            throw new InvalidOperationException("Session was not found.");
        }

        internal void CloseSession(ulong sessionId)
        {
            ThrowIfDisposed();
            DemoSession session = GetSession(sessionId);
            if (session.Phase == DemoSessionPhase.InRoom)
            {
                DemoRoom room = GetRoom(session.RoomId);
                if (room.Lifecycle == DemoRoomLifecycle.Open && room.HostSessionId == sessionId) RemoveOpenHostRoom(room, sessionId);
                else if (room.Lifecycle == DemoRoomLifecycle.Open)
                {
                    int index = room.IndexOf(sessionId);
                    if (index >= 0) room.RemoveAt(index);
                    BroadcastSnapshot(room);
                }
            }

            session.Phase = DemoSessionPhase.Closed;
            _sessions.Remove(session);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _rooms.Clear();
            _sessions.Clear();
        }

        private void RemoveOpenHostRoom(DemoRoom room, ulong hostSessionId)
        {
            DemoSession[] participants = room.CopyParticipants();
            room.Lifecycle = DemoRoomLifecycle.Removed;
            _rooms.Remove(room);
            for (int index = 0; index < participants.Length; index++)
            {
                if (participants[index].SessionId != hostSessionId && participants[index].Phase != DemoSessionPhase.Closed) MoveToLobby(participants[index]);
            }

            DemoSession host = GetSession(hostSessionId);
            MoveToLobby(host);
        }

        private static void MoveToLobby(DemoSession session)
        {
            session.RoomId = 0;
            session.JoinOrdinal = 0;
            session.IsReady = false;
            session.Phase = DemoSessionPhase.Lobby;
            session.Queue(new ServerControlEventMessage { LobbyEntered = new LobbyEnteredEventMessage() });
        }

        private static string ValidateName(string value, int maximumUtf8Bytes, string parameterName)
        {
            if (value is null) throw new ArgumentNullException(parameterName);
            string normalized = value.Trim();
            if (normalized.Length == 0) throw new ArgumentException("Name cannot be empty.", parameterName);
            for (int index = 0; index < normalized.Length; index++) if (char.IsControl(normalized[index])) throw new ArgumentException("Name cannot contain control characters.", parameterName);
            int byteCount = Encoding.UTF8.GetByteCount(normalized);
            if (byteCount < 1 || byteCount > maximumUtf8Bytes) throw new ArgumentOutOfRangeException(parameterName);
            return normalized;
        }

        private static ulong PeekNext(ulong value, string message)
        {
            if (value == 0) throw new InvalidOperationException(message);
            return value;
        }

        private static void CommitNext(ref ulong counter, ulong issued)
        {
            counter = issued == ulong.MaxValue ? 0 : issued + 1;
        }

        private static byte[] CreateUniqueTicket(byte[][] preceding, int count)
        {
            while (true)
            {
                var ticket = new byte[16];
                RandomNumberGenerator.Fill(ticket);
                bool duplicate = false;
                for (int index = 0; index < count && !duplicate; index++) duplicate = CryptographicOperations.FixedTimeEquals(ticket, preceding[index]);
                if (!duplicate) return ticket;
            }
        }

        private static void RequirePhase(DemoSession session, DemoSessionPhase phase)
        {
            if (session.Phase != phase) throw new InvalidOperationException("Command is invalid for the current session phase.");
        }

        private static RoomSnapshotEventMessage CreateSnapshot(DemoRoom room)
        {
            var message = new RoomSnapshotEventMessage
            {
                RoomId = room.RoomId,
                RoomName = room.RoomName,
                HostSessionId = room.HostSessionId,
                Capacity = checked((uint)room.Capacity),
                Lifecycle = ServerControlProtocol.ToWire(room.Lifecycle),
            };
            for (int index = 0; index < room.ParticipantCount; index++)
            {
                DemoSession participant = room.GetParticipant(index);
                message.Participants.Add(new RoomParticipantMessage
                {
                    SessionId = participant.SessionId,
                    Nickname = participant.Nickname,
                    IsHost = participant.SessionId == room.HostSessionId,
                    IsReady = participant.IsReady,
                    JoinOrdinal = checked((uint)participant.JoinOrdinal),
                });
            }

            return message;
        }

        private static void BroadcastSnapshot(DemoRoom room)
        {
            RoomSnapshotEventMessage candidate = CreateSnapshot(room);
            for (int index = 0; index < room.ParticipantCount; index++) room.GetParticipant(index).Queue(new ServerControlEventMessage { RoomSnapshot = candidate.Clone() });
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TcpDemoServer));
        }
    }
}
