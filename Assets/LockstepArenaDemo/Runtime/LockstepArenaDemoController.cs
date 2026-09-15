#nullable enable
using System;
using System.Globalization;
using LockstepArena.Client.Demo;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LockstepArena.Demo
{
    public sealed class LockstepArenaDemoController : MonoBehaviour
    {
        private const string LobbySceneName = "LobbyScene";
        private const string BattleSceneName = "BattleScene";
        private static LockstepArenaDemoController? _instance;

        [SerializeField] private string serverAddress = "127.0.0.1";
        [SerializeField] private string nickname = "Alpha";
        [SerializeField] private string roomName = "Arena Room";
        [SerializeField] private string roomId = "1";
        [SerializeField] private string roomCapacity = "2";
        [SerializeField] private int controlPort = 46000;

        private readonly BattleDefinition _definition = BattleDefinition.CreateDefault();
        private TcpDemoClient? _client;
        private BattlePresenter? _presenter;
        private string _lastError = string.Empty;
        private bool _sceneReadySent;
        private bool _debugVisible;

        private void Awake()
        {
            if (_instance is not null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (_instance != this) return;
            Keyboard? keyboard = Keyboard.current;
            if (keyboard is not null && keyboard.f1Key.wasPressedThisFrame) _debugVisible = !_debugVisible;
            try
            {
                SynchronizeScene();
                if (_client is null || _client.Phase == DemoClientPhase.Disconnected ||
                    _client.Phase == DemoClientPhase.Faulted || _client.Phase == DemoClientPhase.Disposed) return;
                LocalInputSample? input = _client.Phase == DemoClientPhase.InBattle ? ReadGameplayInput() : null;
                _client.PumpOnce(input);
                SynchronizeScene();
                if (_client.Phase == DemoClientPhase.InBattle && _client.PredictedBattleState is not null)
                    EnsurePresenter().Present(_client.PredictedBattleState, _client.LocalPlayerSlot);
            }
            catch (Exception exception)
            {
                _lastError = exception.Message;
            }
        }

        private void SynchronizeScene()
        {
            if (_client is null) return;
            string active = SceneManager.GetActiveScene().name;
            bool battleSceneRequired = _client.Phase == DemoClientPhase.PreparingBattle ||
                _client.Phase == DemoClientPhase.InBattle ||
                _client.Phase == DemoClientPhase.SettlementPendingAuthority ||
                _client.Phase == DemoClientPhase.Settlement;
            if (battleSceneRequired && active != BattleSceneName)
            {
                _sceneReadySent = false;
                SceneManager.LoadScene(BattleSceneName);
                return;
            }
            bool lobbyRequired = _client.Phase == DemoClientPhase.Lobby || _client.Phase == DemoClientPhase.Room;
            if (lobbyRequired && active != LobbySceneName)
            {
                _presenter?.Clear();
                _presenter = null;
                SceneManager.LoadScene(LobbySceneName);
                return;
            }
            if (_client.Phase == DemoClientPhase.PreparingBattle && active == BattleSceneName && !_sceneReadySent)
            {
                _client.MarkBattleSceneReady();
                _sceneReadySent = true;
            }
        }

        private LocalInputSample ReadGameplayInput()
        {
            Keyboard? keyboard = Keyboard.current;
            sbyte moveX = (sbyte)(((keyboard?.dKey.isPressed ?? false) ? 1 : 0) -
                ((keyboard?.aKey.isPressed ?? false) ? 1 : 0));
            sbyte moveZ = (sbyte)(((keyboard?.wKey.isPressed ?? false) ? 1 : 0) -
                ((keyboard?.sKey.isPressed ?? false) ? 1 : 0));
            bool fire = Mouse.current?.leftButton.isPressed ?? false;
            return new LocalInputSample(moveX, moveZ, GetCurrentAim(), fire);
        }

        private ushort GetCurrentAim()
        {
            BattleState? state = _client?.PredictedBattleState;
            PlayerSlot? localSlot = _client?.LocalPlayerSlot;
            if (state is null || !localSlot.HasValue) return 0;
            PlayerState player = state.GetPlayerState(localSlot.Value);
            Camera? camera = EnsurePresenter().GameplayCamera;
            if (camera is null) return player.Aim;
            Vector2 screenPosition = Mouse.current?.position.ReadValue() ??
                new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Ray ray = camera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float distance)) return player.Aim;
            Vector3 point = ray.GetPoint(distance);
            return BattleSimulation.GetAimToward(
                new ArenaPoint(player.PositionX, player.PositionZ),
                new ArenaPoint(
                    Mathf.RoundToInt(point.x * BattlePresenter.WorldUnitsPerSimulationUnit),
                    Mathf.RoundToInt(point.z * BattlePresenter.WorldUnitsPerSimulationUnit)));
        }

        private BattlePresenter EnsurePresenter()
        {
            if (_presenter is not null) return _presenter;
            _presenter = GetComponent<BattlePresenter>();
            if (_presenter is null) _presenter = gameObject.AddComponent<BattlePresenter>();
            _presenter.Configure(_definition);
            return _presenter;
        }

        private void OnGUI()
        {
            if (_instance != this) return;
            GUILayout.BeginArea(new Rect(16, 16, 720, 700), GUI.skin.box);
            GUILayout.Label("Lockstep Arena Gameplay Sample v1");
            DemoClientPhase phase = _client?.Phase ?? DemoClientPhase.Disconnected;
            if (phase == DemoClientPhase.Disconnected || phase == DemoClientPhase.ConnectingControl ||
                phase == DemoClientPhase.AwaitingSessionEntry || phase == DemoClientPhase.Lobby ||
                phase == DemoClientPhase.Room)
                DrawLobby(phase);
            else
                DrawBattleHud();
            if (_debugVisible && _client is not null) GUILayout.TextArea(FormatDiagnostics(_client.Snapshot));
            if (_lastError.Length > 0) GUILayout.Label("Error: " + _lastError);
            GUILayout.EndArea();
        }

        private void DrawLobby(DemoClientPhase phase)
        {
            serverAddress = LabeledText("Server", serverAddress);
            nickname = LabeledText("Nickname", nickname);
            roomName = LabeledText("Room name", roomName);
            roomId = LabeledText("Room id", roomId);
            roomCapacity = LabeledText("Capacity", roomCapacity);
            GUILayout.BeginHorizontal();
            Button("Connect", phase == DemoClientPhase.Disconnected, Connect);
            Button("Enter", phase == DemoClientPhase.AwaitingSessionEntry, () => _client!.EnterSession(nickname));
            Button("List", phase == DemoClientPhase.Lobby || phase == DemoClientPhase.Room, () => _client!.RequestRoomList());
            Button("Create", phase == DemoClientPhase.Lobby, () => _client!.CreateRoom(roomName, ParsePositive(roomCapacity)));
            Button("Join", phase == DemoClientPhase.Lobby, () => _client!.JoinRoom(ulong.Parse(roomId, CultureInfo.InvariantCulture)));
            Button("Leave", phase == DemoClientPhase.Room, () => _client!.LeaveRoom());
            Button("Ready", phase == DemoClientPhase.Room, () => _client!.SetReady(true));
            Button("Unready", phase == DemoClientPhase.Room, () => _client!.SetReady(false));
            Button("Start", phase == DemoClientPhase.Room, () => _client!.StartBattle());
            GUILayout.EndHorizontal();
            if (_client is null) return;
            DemoClientSnapshot snapshot = _client.Snapshot;
            GUILayout.Label("Phase: " + snapshot.Phase);
            GUILayout.Label("Rooms: " + snapshot.RoomList);
            GUILayout.Label("Participants: " + snapshot.RoomParticipants);
            if (snapshot.LastRejection.Length > 0) GUILayout.Label("Last response: " + snapshot.LastRejection);
        }

        private void DrawBattleHud()
        {
            if (_client is null) return;
            BattleState? state = _client.PredictedBattleState;
            if (state is not null && state.IsGameplayEnabled)
            {
                PlayerState first = state.GetPlayerState(new PlayerSlot(0));
                PlayerState second = state.GetPlayerState(new PlayerSlot(1));
                int round = first.RoundWins + second.RoundWins + 1;
                GUILayout.Label($"P1 HP {first.HitPoints}     P2 HP {second.HitPoints}");
                GUILayout.Label($"Round {round}     Score {first.RoundWins} - {second.RoundWins}     Time {(state.RoundTicksRemaining + 29U) / 30U}s");
                if (state.Phase == BattlePhase.RoundCountdown)
                    GUILayout.Label($"{Math.Max(1U, (state.PhaseTicksRemaining + 29U) / 30U)}");
                else if (state.Phase == BattlePhase.Playing) GUILayout.Label("FIGHT");
                else if (state.Phase == BattlePhase.RoundEnded)
                    GUILayout.Label(state.RoundResult.HasWinner ? $"Round winner: P{state.RoundResult.WinnerSlot.Value + 1}" : "DRAW");
            }
            if (_client.Phase == DemoClientPhase.PreparingBattle) GUILayout.Label("Loading arena / waiting for all players...");
            if (_client.Phase == DemoClientPhase.SettlementPendingAuthority) GUILayout.Label("Verifying final authority...");
            if (_client.Phase == DemoClientPhase.Settlement)
            {
                DemoClientSnapshot snapshot = _client.Snapshot;
                string winner = snapshot.WinnerPlayerId == snapshot.SessionId ? "YOU WIN" :
                    snapshot.WinnerPlayerId == 0 ? "DRAW" : "YOU LOSE";
                GUILayout.Label($"{winner}   Final score {snapshot.Slot0RoundWins} - {snapshot.Slot1RoundWins}");
                GUILayout.Label(snapshot.SettlementReason.ToString());
                Button("Return To Lobby", true, () => _client.ReturnToLobby());
            }
            GUILayout.Label("WASD move | Mouse aim | Hold left mouse to fire | F1 debug");
        }

        public static string FormatDiagnostics(DemoClientSnapshot snapshot)
        {
            if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
            return $"Phase={snapshot.Phase} Session={snapshot.SessionId} Room={snapshot.RoomId} Battle={snapshot.BattleId} " +
                $"Rooms=[{snapshot.RoomList}] Participants=[{snapshot.RoomParticipants}] Roster=[{snapshot.BattleRoster}] " +
                $"Rejection={snapshot.LastRejection} ServerTick={snapshot.ServerStateTick} NextPublishTick={snapshot.NextPublishTick} " +
                $"AuthorityTick={snapshot.AuthoritativeTick} PredictedTick={snapshot.PredictedTick} " +
                $"PendingPredictions={snapshot.PendingPredictionCount} PendingAuthority={snapshot.PendingAuthoritativeFrameCount} " +
                $"Replay={snapshot.ReplayFrameCount} LatestDirty={snapshot.LatestDirty} CumulativeDirty={snapshot.CumulativeDirtyFrameCount} " +
                $"AuthorityDigest={snapshot.AuthoritativeDigest:X16} PredictedDigest={snapshot.PredictedDigest:X16} " +
                $"SettlementVerified={snapshot.SettlementVerified} Settlement={snapshot.SettlementReason} " +
                $"Winner={snapshot.WinnerPlayerId} Score={snapshot.Slot0RoundWins}-{snapshot.Slot1RoundWins}";
        }

        private void Connect()
        {
            _client?.Dispose();
            _client = new TcpDemoClient(new DemoClientOptions(
                controlPort, 4096, 32768, 1024, 7, 257, 8, 512,
                12, 16, 64, 4096, 4096, 1024, 11, 251,
                serverAddress, _definition));
            _client.BeginConnect();
            _lastError = string.Empty;
            _sceneReadySent = false;
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

        private void Button(string label, bool enabled, Action action)
        {
            bool previous = GUI.enabled;
            GUI.enabled = enabled;
            if (GUILayout.Button(label))
            {
                try { action(); _lastError = string.Empty; }
                catch (Exception exception) { _lastError = exception.Message; }
            }
            GUI.enabled = previous;
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            _client?.Dispose();
            _instance = null;
        }
    }
}
