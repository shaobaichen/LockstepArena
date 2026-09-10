using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.Server.DemoHost
{
    public sealed class TcpDemoServer : IDisposable
    {
        private readonly DemoServerOptions _options;
        private readonly List<DemoSession> _sessions = new List<DemoSession>();
        private readonly List<DemoRoom> _rooms = new List<DemoRoom>();
        private readonly TcpListener _controlListener;
        private readonly TcpListener _battleListener;
        private readonly List<ControlConnection> _controlConnections = new List<ControlConnection>();
        private readonly List<BattleAttachment> _battleAttachments = new List<BattleAttachment>();
        private ulong _nextSessionId = 1;
        private ulong _nextRoomId = 1;
        private ulong _nextBattleId = 1;
        private bool _disposed;

        public TcpDemoServer(DemoServerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            var controlListener = new TcpListener(IPAddress.Loopback, options.ControlPort);
            var battleListener = new TcpListener(IPAddress.Loopback, options.BattlePort);
            try
            {
                controlListener.Start();
                battleListener.Start();
            }
            catch
            {
                controlListener.Stop();
                battleListener.Stop();
                throw;
            }

            _controlListener = controlListener;
            _battleListener = battleListener;
            ControlPort = ((IPEndPoint)controlListener.LocalEndpoint).Port;
            BattlePort = ((IPEndPoint)battleListener.LocalEndpoint).Port;
        }

        public int ControlPort { get; }
        public int BattlePort { get; }
        public int SessionCount => _sessions.Count;
        public int RoomCount => _rooms.Count;

        public DemoServerPumpResult PumpOnce()
        {
            ThrowIfDisposed();
            int accepted = AcceptControlOnce();
            int acceptedBattle = AcceptBattleOnce();
            int processed = 0;
            ControlConnection[] connections = _controlConnections.ToArray();
            for (int index = 0; index < connections.Length; index++)
            {
                ControlConnection connection = connections[index];
                if (connection.Closed) continue;
                try
                {
                    connection.PumpSend(_options.MaxControlSendBytesPerPump);
                    if (connection.PumpRead())
                    {
                        int remaining = _options.MaxControlMessagesPerPump;
                        while (remaining > 0 && connection.TryDequeuePayload(out byte[]? payload))
                        {
                            ProcessCommand(connection, payload!);
                            processed++;
                            remaining--;
                        }
                    }
                }
                catch
                {
                    CloseControl(connection);
                }
            }

            DrainAllSessionEvents();
            ProgressBattleAttachments();
            int published = PumpBattles(out int aborted);
            DrainAllSessionEvents();
            return new DemoServerPumpResult(accepted, acceptedBattle, processed, published, 0, aborted);
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
            if (session.Phase == DemoSessionPhase.PreparingBattle || session.Phase == DemoSessionPhase.InBattle)
            {
                AbortRoom(GetRoom(session.RoomId), "A participant control connection ended.");
            }
            else if (session.Phase == DemoSessionPhase.InRoom)
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
            _controlListener.Stop();
            _battleListener.Stop();
            for (int index = 0; index < _controlConnections.Count; index++) _controlConnections[index].Dispose();
            _controlConnections.Clear();
            for (int index = 0; index < _battleAttachments.Count; index++) _battleAttachments[index].Dispose();
            _battleAttachments.Clear();
            for (int index = 0; index < _rooms.Count; index++) _rooms[index].Preparation?.Invalidate();
            _rooms.Clear();
            _sessions.Clear();
        }

        private int AcceptControlOnce()
        {
            if (_controlConnections.Count >= _options.MaxSessions || !_controlListener.Server.Poll(0, SelectMode.SelectRead)) return 0;
            TcpClient client = _controlListener.AcceptTcpClient();
            if (client.Client.AddressFamily != AddressFamily.InterNetwork)
            {
                client.Dispose();
                return 0;
            }

            _controlConnections.Add(new ControlConnection(client, _options));
            return 1;
        }

        private int AcceptBattleOnce()
        {
            if (!_battleListener.Server.Poll(0, SelectMode.SelectRead)) return 0;
            TcpClient client = _battleListener.AcceptTcpClient();
            if (client.Client.AddressFamily != AddressFamily.InterNetwork)
            {
                client.Dispose();
                return 0;
            }

            _battleAttachments.Add(new BattleAttachment(client));
            return 1;
        }

        private void ProgressBattleAttachments()
        {
            BattleAttachment[] attachments = _battleAttachments.ToArray();
            for (int index = 0; index < attachments.Length; index++)
            {
                BattleAttachment attachment = attachments[index];
                if (attachment.Closed) continue;
                try
                {
                    if (!attachment.Progress(this)) continue;
                    _battleAttachments.Remove(attachment);
                    BattlePreparation preparation = attachment.Preparation!;
                    preparation.CommitAttachment(attachment.Slot, attachment.DetachClient());
                    if (preparation.AttachedCount == preparation.ParticipantCount)
                    {
                        preparation.Activate(_options);
                        DemoRoom room = GetRoomForPreparation(preparation);
                        room.Lifecycle = DemoRoomLifecycle.InBattle;
                        DemoSession[] participants = room.CopyParticipants();
                        for (int participantIndex = 0; participantIndex < participants.Length; participantIndex++)
                        {
                            participants[participantIndex].Phase = DemoSessionPhase.InBattle;
                            participants[participantIndex].Queue(new ServerControlEventMessage
                            {
                                BattleStarted = new BattleStartedEventMessage { BattleId = preparation.BattleId },
                            });
                        }
                    }
                }
                catch
                {
                    attachment.ReleaseReservation();
                    attachment.Dispose();
                    _battleAttachments.Remove(attachment);
                }
            }
        }

        private int PumpBattles(out int aborted)
        {
            int published = 0;
            aborted = 0;
            DemoRoom[] rooms = _rooms.ToArray();
            for (int index = 0; index < rooms.Length; index++)
            {
                DemoRoom room = rooms[index];
                if (room.Lifecycle != DemoRoomLifecycle.InBattle || room.Preparation?.SharedSession is null) continue;
                try
                {
                    published = checked(published + room.Preparation.SharedSession.PumpOnce());
                }
                catch
                {
                    AbortRoom(room, "Battle session failed.");
                    aborted++;
                }
            }
            return published;
        }

        private bool TryReserveTicket(byte[] ticket, out BattlePreparation? preparation, out PlayerSlot slot)
        {
            for (int index = 0; index < _rooms.Count; index++)
            {
                BattlePreparation? candidate = _rooms[index].Preparation;
                if (candidate is not null && candidate.TryReserveTicket(ticket, out slot))
                {
                    preparation = candidate;
                    return true;
                }
            }
            preparation = null;
            slot = default;
            return false;
        }

        private DemoRoom GetRoomForPreparation(BattlePreparation preparation)
        {
            for (int index = 0; index < _rooms.Count; index++) if (ReferenceEquals(_rooms[index].Preparation, preparation)) return _rooms[index];
            throw new InvalidOperationException("Preparation no longer belongs to an active room.");
        }

        private void AbortRoom(DemoRoom room, string detail)
        {
            BattlePreparation? preparation = room.Preparation;
            preparation?.Invalidate();
            for (int index = _battleAttachments.Count - 1; index >= 0; index--)
            {
                if (ReferenceEquals(_battleAttachments[index].Preparation, preparation))
                {
                    _battleAttachments[index].Dispose();
                    _battleAttachments.RemoveAt(index);
                }
            }

            DemoSession[] participants = room.CopyParticipants();
            room.Lifecycle = DemoRoomLifecycle.Removed;
            _rooms.Remove(room);
            for (int index = 0; index < participants.Length; index++)
            {
                DemoSession session = participants[index];
                if (session.Phase == DemoSessionPhase.Closed) continue;
                session.Phase = DemoSessionPhase.Settlement;
                session.Queue(new ServerControlEventMessage
                {
                    BattleSettlement = new BattleSettlementEventMessage
                    {
                        BattleId = preparation?.BattleId ?? 0,
                        Reason = BattleSettlementReasonMessage.BattleSettlementReasonAborted,
                        Detail = detail,
                    },
                });
            }
        }

        private void ProcessCommand(ControlConnection connection, byte[] payload)
        {
            ClientControlCommandMessage command = ClientControlCommandMessage.Parser.ParseFrom(payload);
            if (command.CommandCase == ClientControlCommandMessage.CommandOneofCase.None) throw new InvalidDataException("Control command union is missing.");
            try
            {
                switch (command.CommandCase)
                {
                    case ClientControlCommandMessage.CommandOneofCase.EnterSession:
                        if (connection.Session is not null) throw new InvalidOperationException("Session was already entered.");
                        connection.Session = EnterSession(command.EnterSession.Nickname);
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.RequestRoomList:
                        RequireNamed(connection).Queue(new ServerControlEventMessage { RoomList = CreateRoomList() });
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.CreateRoom:
                        DemoSession creator = RequireNamed(connection);
                        CreateRoom(creator.SessionId, command.CreateRoom.RoomName, checked((int)command.CreateRoom.Capacity));
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.JoinRoom:
                        DemoSession joiner = RequireNamed(connection);
                        JoinRoom(joiner.SessionId, command.JoinRoom.RoomId);
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.LeaveRoom:
                        DemoSession leaver = RequireNamed(connection);
                        LeaveRoom(leaver.SessionId);
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.SetReady:
                        DemoSession ready = RequireNamed(connection);
                        SetReady(ready.SessionId, command.SetReady.IsReady);
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.StartBattle:
                        DemoSession starter = RequireNamed(connection);
                        StartBattle(starter.SessionId);
                        break;
                    case ClientControlCommandMessage.CommandOneofCase.ReturnToLobby:
                        throw new InvalidOperationException("Return is only valid after settlement.");
                    case ClientControlCommandMessage.CommandOneofCase.ExitSession:
                        CloseControl(connection);
                        break;
                    default:
                        throw new InvalidDataException("Unknown control command.");
                }
            }
            catch (ArgumentException exception)
            {
                QueueRejection(connection, ControlRejectReasonMessage.ControlRejectReasonInvalidValue, exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                QueueRejection(connection, ControlRejectReasonMessage.ControlRejectReasonInvalidPhase, exception.Message);
            }
        }

        private void DrainAllSessionEvents()
        {
            for (int index = 0; index < _controlConnections.Count; index++)
            {
                ControlConnection connection = _controlConnections[index];
                DemoSession? session = connection.Session;
                if (connection.Closed || session is null) continue;
                while (session.TryDequeueEvent(out ServerControlEventMessage? message)) connection.Queue(message!);
            }
        }

        private void CloseControl(ControlConnection connection)
        {
            if (connection.Closed) return;
            DemoSession? session = connection.Session;
            connection.Dispose();
            _controlConnections.Remove(connection);
            if (session is not null && session.Phase != DemoSessionPhase.Closed) CloseSession(session.SessionId);
        }

        private static DemoSession RequireNamed(ControlConnection connection)
        {
            return connection.Session ?? throw new InvalidOperationException("Session entry is required.");
        }

        private RoomListEventMessage CreateRoomList()
        {
            var result = new RoomListEventMessage();
            result.Rooms.Add(GetRoomSummaries());
            return result;
        }

        private static void QueueRejection(ControlConnection connection, ControlRejectReasonMessage reason, string detail)
        {
            if (connection.Session is null) throw new InvalidDataException(detail);
            connection.Session.Queue(new ServerControlEventMessage
            {
                CommandRejected = new CommandRejectedEventMessage { Reason = reason, Detail = detail },
            });
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

        private sealed class ControlConnection : IDisposable
        {
            private readonly TcpClient _client;
            private readonly NetworkStream _stream;
            private readonly LengthPrefixedFrameDecoder _decoder;
            private readonly byte[] _receiveBuffer;
            private readonly int _receiveOffset;
            private readonly int _receiveCapacity;
            private readonly int _maxPayloadLength;
            private readonly int _maxPendingBytes;
            private readonly Queue<byte[]> _incoming = new Queue<byte[]>();
            private readonly Queue<byte[]> _outgoing = new Queue<byte[]>();
            private byte[]? _sending;
            private int _sendOffset;
            private int _pendingBytes;

            internal ControlConnection(TcpClient client, DemoServerOptions options)
            {
                _client = client;
                _stream = client.GetStream();
                _decoder = new LengthPrefixedFrameDecoder(options.MaxControlPayloadLength);
                _receiveBuffer = new byte[options.ControlReceiveBufferLength];
                _receiveOffset = options.ControlReceiveOffset;
                _receiveCapacity = options.ControlReceiveReadCapacity;
                _maxPayloadLength = options.MaxControlPayloadLength;
                _maxPendingBytes = options.MaxPendingControlBytesPerSession;
            }

            internal DemoSession? Session { get; set; }
            internal bool Closed { get; private set; }

            internal void PumpSend(int maximumBytes)
            {
                if (!_client.Client.Poll(0, SelectMode.SelectWrite)) return;
                if (_sending is null)
                {
                    if (_outgoing.Count == 0) return;
                    _sending = _outgoing.Dequeue();
                    _sendOffset = 0;
                }

                int count = Math.Min(maximumBytes, _sending.Length - _sendOffset);
                _stream.Write(_sending, _sendOffset, count);
                _sendOffset += count;
                _pendingBytes -= count;
                if (_sendOffset == _sending.Length)
                {
                    _sending = null;
                    _sendOffset = 0;
                }
            }

            internal bool PumpRead()
            {
                if (!_client.Client.Poll(0, SelectMode.SelectRead)) return false;
                if (_client.Client.Available == 0) throw new EndOfStreamException("Control connection ended.");
                int read = _stream.Read(_receiveBuffer, _receiveOffset, _receiveCapacity);
                if (read == 0) throw new EndOfStreamException("Control connection ended.");
                byte[][] payloads = _decoder.Feed(_receiveBuffer, _receiveOffset, read);
                for (int index = 0; index < payloads.Length; index++) _incoming.Enqueue(payloads[index]);
                return true;
            }

            internal bool TryDequeuePayload(out byte[]? payload)
            {
                if (_incoming.Count == 0)
                {
                    payload = null;
                    return false;
                }
                payload = _incoming.Dequeue();
                return true;
            }

            internal void Queue(ServerControlEventMessage message)
            {
                byte[] framed = LengthPrefixedFrameEncoder.Encode(message.ToByteArray(), _maxPayloadLength);
                if ((long)_pendingBytes + framed.Length > _maxPendingBytes) throw new InvalidOperationException("Pending control send capacity is full.");
                _outgoing.Enqueue(framed);
                _pendingBytes += framed.Length;
            }

            public void Dispose()
            {
                if (Closed) return;
                Closed = true;
                try { _stream.Dispose(); }
                finally { _client.Dispose(); }
            }
        }

        private sealed class BattleAttachment : IDisposable
        {
            private readonly byte[] _ticket = new byte[16];
            private TcpClient? _client;
            private readonly NetworkStream _stream;
            private int _ticketOffset;

            internal BattleAttachment(TcpClient client)
            {
                _client = client;
                _stream = client.GetStream();
            }

            internal BattlePreparation? Preparation { get; private set; }
            internal PlayerSlot Slot { get; private set; }
            internal bool Closed { get; private set; }

            internal bool Progress(TcpDemoServer server)
            {
                TcpClient client = _client ?? throw new InvalidOperationException("Attachment client ownership was transferred.");
                if (Preparation is null)
                {
                    if (!client.Client.Poll(0, SelectMode.SelectRead)) return false;
                    if (client.Client.Available == 0) throw new EndOfStreamException("Battle ticket ended early.");
                    int read = _stream.Read(_ticket, _ticketOffset, _ticket.Length - _ticketOffset);
                    if (read == 0) throw new EndOfStreamException("Battle ticket ended early.");
                    _ticketOffset += read;
                    if (_ticketOffset == _ticket.Length)
                    {
                        if (!server.TryReserveTicket(_ticket, out BattlePreparation? preparation, out PlayerSlot slot)) throw new InvalidDataException("Battle ticket is invalid.");
                        Preparation = preparation;
                        Slot = slot;
                    }
                    return false;
                }

                if (!client.Client.Poll(0, SelectMode.SelectWrite)) return false;
                _stream.WriteByte(0x01);
                return true;
            }

            internal TcpClient DetachClient()
            {
                TcpClient result = _client ?? throw new InvalidOperationException("Attachment ownership was already transferred.");
                _client = null;
                return result;
            }

            internal void ReleaseReservation()
            {
                Preparation?.ReleaseReservation(Slot);
            }

            public void Dispose()
            {
                if (Closed) return;
                Closed = true;
                try { _stream.Dispose(); }
                finally
                {
                    _client?.Dispose();
                    _client = null;
                }
            }
        }
    }
}
