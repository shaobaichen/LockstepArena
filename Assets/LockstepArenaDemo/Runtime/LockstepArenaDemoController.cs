#nullable enable
using System;
using System.Globalization;
using LockstepArena.Client.Demo;
using LockstepArena.Client.LiveTcp;
using UnityEngine;

namespace LockstepArena.Demo
{
    public sealed class LockstepArenaDemoController : MonoBehaviour
    {
        [SerializeField] private string nickname = "Alpha";
        [SerializeField] private string roomName = "Golden Room";
        [SerializeField] private string roomId = "1";
        [SerializeField] private string roomCapacity = "2";
        [SerializeField] private int controlPort = 46000;
        [SerializeField] private bool scriptedGoldenInput = true;

        private TcpDemoClient? _client;
        private string _lastError = string.Empty;

        private void Update()
        {
            if (_client is null ||
                _client.Phase == DemoClientPhase.Disconnected ||
                _client.Phase == DemoClientPhase.Faulted ||
                _client.Phase == DemoClientPhase.Disposed) return;
            try
            {
                LocalInputSample? input = _client.Phase == DemoClientPhase.InBattle && scriptedGoldenInput
                    ? CreateScriptedInput(_client.Snapshot.PredictedTick)
                    : null;
                _client.PumpOnce(input);
            }
            catch (Exception exception)
            {
                _lastError = exception.Message;
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 900, 700), GUI.skin.box);
            GUILayout.Label("Lockstep Arena v1 Debug Demo");
            nickname = LabeledText("Nickname", nickname);
            roomName = LabeledText("Room name", roomName);
            roomId = LabeledText("Room id", roomId);
            roomCapacity = LabeledText("Capacity", roomCapacity);
            GUILayout.BeginHorizontal();
            Button("Connect", Connect);
            Button("Enter", () => _client!.EnterSession(nickname));
            Button("List", () => _client!.RequestRoomList());
            Button("Create", () => _client!.CreateRoom(roomName, ParsePositive(roomCapacity)));
            Button("Join", () => _client!.JoinRoom(ulong.Parse(roomId, CultureInfo.InvariantCulture)));
            Button("Ready", () => _client!.SetReady(true));
            Button("Unready", () => _client!.SetReady(false));
            Button("Start", () => _client!.StartBattle());
            Button("Return", () => _client!.ReturnToLobby());
            Button("Exit", () => _client!.Exit());
            GUILayout.EndHorizontal();
            if (_client is not null) GUILayout.TextArea(FormatDiagnostics(_client.Snapshot));
            if (_lastError.Length > 0) GUILayout.Label("Error: " + _lastError);
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            _client?.Dispose();
        }

        public static string FormatDiagnostics(DemoClientSnapshot snapshot)
        {
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            return $"Phase={snapshot.Phase} Session={snapshot.SessionId} Room={snapshot.RoomId} Battle={snapshot.BattleId} " +
                $"ServerTick={snapshot.ServerStateTick} NextPublishTick={snapshot.NextPublishTick} " +
                $"AuthorityTick={snapshot.AuthoritativeTick} PredictedTick={snapshot.PredictedTick} " +
                $"PendingPredictions={snapshot.PendingPredictionCount} PendingAuthority={snapshot.PendingAuthoritativeFrameCount} " +
                $"Replay={snapshot.ReplayFrameCount} LatestDirty={snapshot.LatestDirty} " +
                $"CumulativeDirty={snapshot.CumulativeDirtyFrameCount} AuthorityDigest={snapshot.AuthoritativeDigest:X16} " +
                $"PredictedDigest={snapshot.PredictedDigest:X16} SettlementVerified={snapshot.SettlementVerified}";
        }

        private void Connect()
        {
            _client?.Dispose();
            _client = new TcpDemoClient(new DemoClientOptions(
                controlPort,
                4096,
                16384,
                1024,
                7,
                257,
                8,
                512,
                8,
                8,
                16,
                32,
                4096,
                1024,
                11,
                251));
            _client.BeginConnect();
            _lastError = string.Empty;
        }

        private static LocalInputSample CreateScriptedInput(uint tick)
        {
            return (tick % 4U) switch
            {
                0U => new LocalInputSample(1, 0, checked((ushort)(1000U + tick))),
                1U => new LocalInputSample(0, 1, checked((ushort)(1000U + tick))),
                2U => new LocalInputSample(-1, 0, checked((ushort)(1000U + tick))),
                _ => new LocalInputSample(0, -1, checked((ushort)(1000U + tick))),
            };
        }

        private static string LabeledText(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(90));
            string result = GUILayout.TextField(value);
            GUILayout.EndHorizontal();
            return result;
        }

        private static int ParsePositive(string value)
        {
            int result = int.Parse(value, CultureInfo.InvariantCulture);
            if (result < 1) throw new ArgumentOutOfRangeException(nameof(value));
            return result;
        }

        private void Button(string label, Action action)
        {
            if (!GUILayout.Button(label)) return;
            try
            {
                action();
                _lastError = string.Empty;
            }
            catch (Exception exception)
            {
                _lastError = exception.Message;
            }
        }
    }
}
