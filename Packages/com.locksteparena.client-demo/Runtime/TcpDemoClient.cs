using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Protocol.Wire;
using LockstepArena.StreamFraming;

namespace LockstepArena.Client.Demo
{
    public sealed class TcpDemoClient : IDisposable
    {
        private readonly DemoClientOptions _options;
        private readonly LengthPrefixedFrameDecoder _decoder;
        private readonly byte[] _receiveBuffer;
        private readonly Queue<byte[]> _outbound = new Queue<byte[]>();
        private readonly Queue<byte[]> _inbound = new Queue<byte[]>();
        private TcpClient? _client;
        private NetworkStream? _stream;
        private byte[]? _sending;
        private int _sendOffset;
        private int _pendingControlBytes;
        private DemoClientPhase _phase;
        private ulong _sessionId;
        private string _nickname = string.Empty;
        private ulong _roomId;
        private string _roomName = string.Empty;
        private ulong _battleId;
        private string _lastRejection = string.Empty;
        private bool _disposed;

        public TcpDemoClient(DemoClientOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _decoder = new LengthPrefixedFrameDecoder(options.MaxControlPayloadLength);
            _receiveBuffer = new byte[options.ControlReceiveBufferLength];
            _phase = DemoClientPhase.Disconnected;
        }

        public DemoClientPhase Phase => _phase;
        public DemoClientSnapshot Snapshot => new DemoClientSnapshot(_phase, _sessionId, _nickname, _roomId, _roomName, _battleId, _lastRejection);

        public void BeginConnect()
        {
            RequirePhase(DemoClientPhase.Disconnected);
            _phase = DemoClientPhase.ConnectingControl;
            try
            {
                var client = new TcpClient(AddressFamily.InterNetwork);
                client.Connect(IPAddress.Loopback, _options.ControlPort);
                _client = client;
                _stream = client.GetStream();
                _phase = DemoClientPhase.AwaitingSessionEntry;
            }
            catch
            {
                _phase = DemoClientPhase.Faulted;
                throw;
            }
        }

        public void EnterSession(string nickname)
        {
            RequirePhase(DemoClientPhase.AwaitingSessionEntry);
            Queue(new ClientControlCommandMessage { EnterSession = new EnterSessionCommandMessage { Nickname = nickname } });
        }

        public void RequestRoomList()
        {
            RequireAnyPhase(DemoClientPhase.Lobby, DemoClientPhase.Room);
            Queue(new ClientControlCommandMessage { RequestRoomList = new RequestRoomListCommandMessage() });
        }

        public void CreateRoom(string roomName, int capacity)
        {
            RequirePhase(DemoClientPhase.Lobby);
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Queue(new ClientControlCommandMessage { CreateRoom = new CreateRoomCommandMessage { RoomName = roomName, Capacity = checked((uint)capacity) } });
        }

        public void JoinRoom(ulong roomId)
        {
            RequirePhase(DemoClientPhase.Lobby);
            Queue(new ClientControlCommandMessage { JoinRoom = new JoinRoomCommandMessage { RoomId = roomId } });
        }

        public void LeaveRoom()
        {
            RequirePhase(DemoClientPhase.Room);
            Queue(new ClientControlCommandMessage { LeaveRoom = new LeaveRoomCommandMessage() });
        }

        public void SetReady(bool isReady)
        {
            RequirePhase(DemoClientPhase.Room);
            Queue(new ClientControlCommandMessage { SetReady = new SetReadyCommandMessage { IsReady = isReady } });
        }

        public void StartBattle()
        {
            RequirePhase(DemoClientPhase.Room);
            Queue(new ClientControlCommandMessage { StartBattle = new StartBattleCommandMessage() });
        }

        public void ReturnToLobby()
        {
            RequirePhase(DemoClientPhase.Settlement);
            Queue(new ClientControlCommandMessage { ReturnToLobby = new ReturnToLobbyCommandMessage() });
        }

        public void Exit()
        {
            if (_phase == DemoClientPhase.Disposed) throw new ObjectDisposedException(nameof(TcpDemoClient));
            if (_phase == DemoClientPhase.Faulted) throw new InvalidOperationException("The client is faulted.");
            Queue(new ClientControlCommandMessage { ExitSession = new ExitSessionCommandMessage() });
        }

        public DemoClientPumpResult PumpOnce(LocalInputSample? localInput)
        {
            ThrowIfUnavailable();
            try
            {
                int sent = PumpSend();
                int processed = PumpReceive();
                return new DemoClientPumpResult(sent, processed, 0, false);
            }
            catch
            {
                _phase = DemoClientPhase.Faulted;
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _phase = DemoClientPhase.Disposed;
            try { _stream?.Dispose(); }
            finally { _client?.Dispose(); }
        }

        private void Queue(ClientControlCommandMessage command)
        {
            byte[] framed = ClientControlProtocol.Frame(command, _options.MaxControlPayloadLength);
            if ((long)_pendingControlBytes + framed.Length > _options.MaxPendingControlBytes) throw new InvalidOperationException("The pending control send capacity is full.");
            _outbound.Enqueue(framed);
            _pendingControlBytes += framed.Length;
        }

        private int PumpSend()
        {
            if (_client is null || _stream is null || !_client.Client.Poll(0, SelectMode.SelectWrite)) return 0;
            if (_sending is null)
            {
                if (_outbound.Count == 0) return 0;
                _sending = _outbound.Dequeue();
                _sendOffset = 0;
            }

            int count = Math.Min(_options.MaxControlSendBytesPerPump, _sending.Length - _sendOffset);
            _stream.Write(_sending, _sendOffset, count);
            _sendOffset += count;
            _pendingControlBytes -= count;
            if (_sendOffset == _sending.Length)
            {
                _sending = null;
                _sendOffset = 0;
            }

            return count;
        }

        private int PumpReceive()
        {
            if (_client is null || _stream is null) return 0;
            if (_client.Client.Poll(0, SelectMode.SelectRead))
            {
                if (_client.Client.Available == 0) throw new EndOfStreamException("Control connection ended.");
                int read = _stream.Read(_receiveBuffer, _options.ControlReceiveOffset, _options.ControlReceiveReadCapacity);
                if (read == 0) throw new EndOfStreamException("Control connection ended.");
                byte[][] payloads = _decoder.Feed(_receiveBuffer, _options.ControlReceiveOffset, read);
                for (int index = 0; index < payloads.Length; index++) _inbound.Enqueue(payloads[index]);
            }

            int processed = 0;
            while (processed < _options.MaxControlMessagesPerPump && _inbound.Count > 0)
            {
                ProcessEvent(ServerControlEventMessage.Parser.ParseFrom(_inbound.Dequeue()));
                processed++;
            }
            return processed;
        }

        private void ProcessEvent(ServerControlEventMessage message)
        {
            switch (message.EventCase)
            {
                case ServerControlEventMessage.EventOneofCase.SessionEntered:
                    _sessionId = message.SessionEntered.SessionId;
                    _nickname = message.SessionEntered.Nickname;
                    _phase = DemoClientPhase.Lobby;
                    break;
                case ServerControlEventMessage.EventOneofCase.RoomSnapshot:
                    _roomId = message.RoomSnapshot.RoomId;
                    _roomName = message.RoomSnapshot.RoomName;
                    _phase = DemoClientPhase.Room;
                    break;
                case ServerControlEventMessage.EventOneofCase.BattlePreparing:
                    _battleId = message.BattlePreparing.BattleId;
                    _phase = DemoClientPhase.PreparingBattle;
                    break;
                case ServerControlEventMessage.EventOneofCase.BattleStarted:
                    if (message.BattleStarted.BattleId != _battleId) throw new InvalidDataException("BattleStarted does not match the prepared battle.");
                    _phase = DemoClientPhase.InBattle;
                    break;
                case ServerControlEventMessage.EventOneofCase.BattleSettlement:
                    if (message.BattleSettlement.BattleId != _battleId) throw new InvalidDataException("Settlement does not match the active battle.");
                    _phase = DemoClientPhase.Settlement;
                    break;
                case ServerControlEventMessage.EventOneofCase.CommandRejected:
                    _lastRejection = message.CommandRejected.Reason.ToString();
                    break;
                case ServerControlEventMessage.EventOneofCase.LobbyEntered:
                    _roomId = 0;
                    _roomName = string.Empty;
                    _battleId = 0;
                    _phase = DemoClientPhase.Lobby;
                    break;
                case ServerControlEventMessage.EventOneofCase.RoomList:
                case ServerControlEventMessage.EventOneofCase.BattleStatus:
                    break;
                default:
                    throw new InvalidDataException("Control event union is missing.");
            }
        }

        private void RequirePhase(DemoClientPhase phase)
        {
            ThrowIfUnavailable();
            if (_phase != phase) throw new InvalidOperationException("Command is invalid for the current client phase.");
        }

        private void RequireAnyPhase(DemoClientPhase first, DemoClientPhase second)
        {
            ThrowIfUnavailable();
            if (_phase != first && _phase != second) throw new InvalidOperationException("Command is invalid for the current client phase.");
        }

        private void ThrowIfUnavailable()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TcpDemoClient));
            if (_phase == DemoClientPhase.Faulted) throw new InvalidOperationException("The client is faulted.");
        }
    }
}
