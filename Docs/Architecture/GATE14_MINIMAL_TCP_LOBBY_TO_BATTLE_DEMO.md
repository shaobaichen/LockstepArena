# Gate 14: Minimal TCP Lobby-to-Battle Demo and v1 Closure

## Status and baseline

Planning contract only. Implementation is not authorized until this Planning commit receives independent PASS.

- Frozen Gate 13 baseline: `57243ae82ff87a5f51dc86d34342d697963b34dc`
- Branch: `codex/gate14-v1-demo-closure`
- Gate 14 is the final v1 Gate. There is no Gate 15.
- Final scope: a loopback TCP Debug demo that composes the frozen Gate 13 server authority, predicted clients, rollback/replay, and deterministic Simulation into a complete lobby-to-settlement flow.

The v1 flow is:

```text
nickname/session entry
-> room list
-> create/join room
-> participant snapshot
-> Ready/Unready
-> host Start
-> freeze roster and PlayerSlots
-> issue battle bootstrap and one-time tickets
-> attach separate battle TCP connections
-> one Gate 13 TcpSharedBattleSession
-> one Gate 13 PredictedTcpClientBattleRuntime per client
-> Tick-limit completion
-> final Tick/state/digest settlement verification
-> ReturnToLobby or Exit
```

## 1. Ownership and exact physical layout

### Server application

New .NET 8/C# 12 executable assembly `LockstepArena.Server.DemoHost`:

```text
Server/LockstepArena.Server.DemoHost/
  LockstepArena.Server.DemoHost.csproj
  Program.cs
  DemoServerOptions.cs
  DemoServerPumpResult.cs
  TcpDemoServer.cs
  DemoSession.cs
  DemoRoom.cs
  BattlePreparation.cs
  ServerControlProtocol.cs
```

Its direct ProjectReferences are exactly `LockstepArena.Server.LiveTcp`, `LockstepArena.Protocol`, `LockstepArena.StreamFraming`, and `LockstepArena.Simulation`. It owns the loopback control and battle listeners, process-local sessions/rooms, battle preparation, and one Gate 13 `TcpSharedBattleSession` per active room. It does not reimplement authority, Simulation, prediction, rollback, framing, or battle broadcast.

### Gate 13 Client LiveTcp physical migration

Move by Git rename:

```text
Client/LockstepArena.Client.LiveTcp/
  TcpClientBattlePump.cs
  PredictedTcpClientBattleRuntime.cs
  LockstepArena.Client.LiveTcp.csproj
```

to the single embedded package:

```text
Packages/com.locksteparena.client-live-tcp/
  package.json
  Runtime/
    Directory.Build.props
    TcpClientBattlePump.cs
    PredictedTcpClientBattleRuntime.cs
    LockstepArena.Client.LiveTcp.asmdef
    LockstepArena.Client.LiveTcp.csproj
```

The two production `.cs` blobs remain byte-identical to Gate 13. Unity and .NET compile these same physical files. The old `Client/` implementation is removed; no source copy, sync script, symlink/junction, Unity-specific networking implementation, or precompiled LockstepArena DLL is allowed.

Package version is `0.1.0`. Dependencies are `com.locksteparena.client-prediction`, `com.locksteparena.protocol`, `com.locksteparena.simulation`, and `com.locksteparena.stream-framing`, each `0.1.0`.

The runtime asmdef is named `LockstepArena.Client.LiveTcp`; references exactly `LockstepArena.Client.Prediction`, `LockstepArena.Protocol`, `LockstepArena.Simulation`, and `LockstepArena.StreamFraming`; uses `overrideReferences: true`, `precompiledReferences: ["Google.Protobuf.dll"]`, `autoReferenced: false`, `allowUnsafeCode: false`, and `noEngineReferences: true`.

The csproj targets `netstandard2.1`, C# 9, nullable enabled, implicit usings disabled, warnings as errors, `BuildInParallel=false`, Google.Protobuf `3.36.0`, and only its Runtime sources. Package-local `Directory.Build.props` routes all build artifacts outside the package.

Only these existing projects retarget their ProjectReference path; their test source does not change:

```text
Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj
Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj
```

### Client Demo package

New embedded package and assembly `LockstepArena.Client.Demo`:

```text
Packages/com.locksteparena.client-demo/
  package.json
  Runtime/
    Directory.Build.props
    DemoClientOptions.cs
    DemoClientPhase.cs
    DemoClientSnapshot.cs
    DemoClientPumpResult.cs
    TcpDemoClient.cs
    ClientControlProtocol.cs
    LockstepArena.Client.Demo.asmdef
    LockstepArena.Client.Demo.csproj
```

Package version is `0.1.0`; package dependencies are Client LiveTcp, Protocol, Simulation, and StreamFraming at `0.1.0`. The asmdef references those four assemblies, uses the existing Google.Protobuf precompiled-reference contract, is not auto-referenced, forbids unsafe code, and has no engine references.

The csproj compiles the same Runtime sources as the asmdef and is exactly `netstandard2.1`, C# 9, nullable enabled, implicit usings disabled, warnings as errors, `BuildInParallel=false`; it directly ProjectReferences the four package Runtime csprojs and uses Google.Protobuf `3.36.0`. It does not compile metadata, Editor, Unity presentation, or Server files.

It owns control connection/presentation state, battle attachment, exactly one Gate 13 predicted runtime during battle, pending settlement verification, and cleanup. It does not duplicate domain systems.

### Unity Debug presentation

```text
Assets/LockstepArenaDemo/
  Runtime/
    LockstepArena.Demo.asmdef
    LockstepArenaDemoController.cs
  Scenes/
    LockstepArenaDemo.unity
  Tests/Editor/
    LockstepArena.Demo.Editor.Tests.asmdef
    UnityDemoSceneTests.cs
    UnityDemoAssemblyTests.cs
    UnityDemoPresentationTests.cs
```

The runtime presentation asmdef references Client Demo, Client LiveTcp, and Simulation. It contains one minimal `MonoBehaviour` using IMGUI. It does not make Transform/View state authoritative and does not introduce a UI framework.

### Repository integration

`Packages/manifest.json` remains unchanged. `Packages/packages-lock.json` may add only embedded entries for `com.locksteparena.client-live-tcp` and `com.locksteparena.client-demo`, with dependencies matching the manifests above. No other lockfile change is allowed.

`.gitignore` may add only these three precise authored-project exceptions:

```gitignore
!Packages/com.locksteparena.client-demo/Runtime/LockstepArena.Client.Demo.csproj
!Server/LockstepArena.Server.DemoHost/LockstepArena.Server.DemoHost.csproj
!Tests/LockstepArena.DemoFlow.Tests/LockstepArena.DemoFlow.Tests.csproj
```

The renamed Client LiveTcp csproj remains tracked by rename and needs no exception.

## 2. Exact options and public APIs

Namespace `LockstepArena.Server.DemoHost`:

```csharp
public sealed class DemoServerOptions
{
    public DemoServerOptions(
        int controlPort,
        int battlePort,
        int maxSessions,
        int maxRooms,
        int maxRoomCapacity,
        PlayerState[] spawnStatesInSlotOrder,
        uint inputDelayTicks,
        uint maxFutureTickOffset,
        int authoritativeHistoryCapacity,
        uint battleDurationTicks,
        int maxControlPayloadLength,
        int maxPendingControlBytesPerSession,
        int controlReceiveBufferLength,
        int controlReceiveOffset,
        int controlReceiveReadCapacity,
        int maxControlMessagesPerPump,
        int maxControlSendBytesPerPump,
        int maxBattlePayloadLength,
        int battleReceiveBufferLength,
        int battleReceiveOffset,
        int battleReceiveReadCapacity);

    public int ControlPort { get; }
    public int BattlePort { get; }
    public int MaxSessions { get; }
    public int MaxRooms { get; }
    public int MaxRoomCapacity { get; }
    public int SpawnStateCount { get; }
    public uint InputDelayTicks { get; }
    public uint MaxFutureTickOffset { get; }
    public int AuthoritativeHistoryCapacity { get; }
    public uint BattleDurationTicks { get; }
    public int MaxControlPayloadLength { get; }
    public int MaxPendingControlBytesPerSession { get; }
    public int ControlReceiveBufferLength { get; }
    public int ControlReceiveOffset { get; }
    public int ControlReceiveReadCapacity { get; }
    public int MaxControlMessagesPerPump { get; }
    public int MaxControlSendBytesPerPump { get; }
    public int MaxBattlePayloadLength { get; }
    public int BattleReceiveBufferLength { get; }
    public int BattleReceiveOffset { get; }
    public int BattleReceiveReadCapacity { get; }
    public PlayerState GetSpawnState(PlayerSlot slot);
}

public readonly struct DemoServerPumpResult
{
    public int AcceptedControlConnections { get; }
    public int AcceptedBattleConnections { get; }
    public int ProcessedControlCommands { get; }
    public int PublishedAuthoritativeFrames { get; }
    public int CompletedBattles { get; }
    public int AbortedBattles { get; }
}

public sealed class TcpDemoServer : IDisposable
{
    public TcpDemoServer(DemoServerOptions options);
    public int ControlPort { get; }
    public int BattlePort { get; }
    public int SessionCount { get; }
    public int RoomCount { get; }
    public DemoServerPumpResult PumpOnce();
}
```

Validation is completed before listeners or state exist. In parameter order: ports are `0..65535` and distinct when both nonzero; `MaxSessions >= 1`; `MaxRooms >= 1`; `MaxRoomCapacity >= 2`; `MaxRoomCapacity <= MaxSessions`; spawn array is non-null and length is at least `MaxRoomCapacity`; every spawn is within Simulation arena bounds; `InputDelayTicks` and `MaxFutureTickOffset` remain independent; `AuthoritativeHistoryCapacity >= 1`; `BattleDurationTicks >= 1`; each payload maximum is `1..int.MaxValue-4`; pending control bytes are at least prefix plus maximum control payload; all buffers have positive lengths/capacities and valid offsets checked with subtraction form; per-pump message/send bounds are positive. Invalid construction throws the matching `ArgumentOutOfRangeException` (null array uses `ArgumentNullException`) before external mutation. `GetSpawnState` range-checks the slot. Spawn storage is defensively copied.

Namespace `LockstepArena.Client.Demo`:

```csharp
public sealed class DemoClientOptions
{
    public DemoClientOptions(
        int controlPort,
        int maxControlPayloadLength,
        int maxPendingControlBytes,
        int controlReceiveBufferLength,
        int controlReceiveOffset,
        int controlReceiveReadCapacity,
        int maxControlMessagesPerPump,
        int maxControlSendBytesPerPump,
        int maxPredictionTicks,
        int maxAuthoritativeFramesPerUpdate,
        int maxPendingAuthoritativeFrames,
        int maxReplayFrames,
        int maxBattlePayloadLength,
        int battleReceiveBufferLength,
        int battleReceiveOffset,
        int battleReceiveReadCapacity);

    public int ControlPort { get; }
    public int MaxControlPayloadLength { get; }
    public int MaxPendingControlBytes { get; }
    public int ControlReceiveBufferLength { get; }
    public int ControlReceiveOffset { get; }
    public int ControlReceiveReadCapacity { get; }
    public int MaxControlMessagesPerPump { get; }
    public int MaxControlSendBytesPerPump { get; }
    public int MaxPredictionTicks { get; }
    public int MaxAuthoritativeFramesPerUpdate { get; }
    public int MaxPendingAuthoritativeFrames { get; }
    public int MaxReplayFrames { get; }
    public int MaxBattlePayloadLength { get; }
    public int BattleReceiveBufferLength { get; }
    public int BattleReceiveOffset { get; }
    public int BattleReceiveReadCapacity { get; }
}

public enum DemoClientPhase
{
    Disconnected,
    ConnectingControl,
    AwaitingSessionEntry,
    Lobby,
    Room,
    PreparingBattle,
    InBattle,
    SettlementPendingAuthority,
    Settlement,
    Faulted,
    Disposed
}

public sealed class TcpDemoClient : IDisposable
{
    public TcpDemoClient(DemoClientOptions options);
    public DemoClientPhase Phase { get; }
    public DemoClientSnapshot Snapshot { get; }
    public void BeginConnect();
    public void EnterSession(string nickname);
    public void RequestRoomList();
    public void CreateRoom(string roomName, int capacity);
    public void JoinRoom(ulong roomId);
    public void LeaveRoom();
    public void SetReady(bool isReady);
    public void StartBattle();
    public void ReturnToLobby();
    public void Exit();
    public DemoClientPumpResult PumpOnce(LocalInputSample? localInput);
}
```

Client validation order follows constructor parameter order. `ControlPort` is `1..65535`; payload maxima use `1..int.MaxValue-4`; pending bytes cover prefix plus maximum payload; all receive buffer triples are valid using subtraction-form overflow-safe checks; all control, prediction, authority, Replay, and battle capacities are at least one. Construction validates everything before assigning state or opening a socket. Control connects only to `IPAddress.Loopback`; BattlePort exists only in a validated `BattlePreparing` event. Snapshot exposes copied/immutable presentation values, never mutable generated messages. Commands queue immutable command payloads; capacity failure precedes mutation.

## 3. Identity, names, sessions, and rooms

SessionId, RoomId, and BattleId are separate nonzero process-local `ulong` counters beginning at 1. After issuing `ulong.MaxValue`, the next field becomes zero as an exhaustion sentinel; later allocation throws `InvalidOperationException` before any mutation. There is no wrap or reuse. During battle, `PlayerId.Value == SessionId`; identifier ordering never chooses PlayerSlot.

Nickname validation order is null, `Trim()`, empty, any `char.IsControl`, UTF-8 byte count, active uniqueness. The stored trimmed value is 1–32 UTF-8 bytes; uniqueness is ordinal, with no normalization or case folding. A rejection consumes no SessionId. Room names use the same trim/control checks and 1–48 UTF-8 bytes; names need not be unique.

Session phases are:

```text
AwaitingNickname -> Lobby -> InRoom -> PreparingBattle -> InBattle
                 -> Settlement -> Lobby or Closed
```

Allowed commands are: AwaitingNickname: EnterSession/Exit; Lobby: list/create/join/Exit; InRoom/Open: list/leave/ready/start/Exit; PreparingBattle and InBattle: Exit only; Settlement: ReturnToLobby/Exit. Invalid-phase commands yield `CommandRejected` without state change. Malformed framing/Protobuf, oversize payload, or missing union/message presence closes only the offending session.

Rooms are held in one in-memory directory and contain RoomId, trimmed name, host SessionId, configured capacity, stable join-order participants, readiness, and lifecycle:

```text
Open -> PreparingBattle -> InBattle -> Settled -> Removed
```

Only Open allows membership change. Explicit Leave in PreparingBattle/InBattle is rejected. Non-host Open leave sends `LobbyEntered` to the leaver and updated `RoomSnapshot` to survivors. Host Open leave or host control loss removes the room and sends `LobbyEntered` to live survivors. A control loss during preparation/battle invalidates tickets, closes prepared sockets, disposes the Gate 13 session if active, queues Aborted settlement to survivors when possible, and removes the room after session retention. Stable join ordinal, never dictionary/network/ready order, drives participant order.

Every control transaction follows parse -> union presence -> phase -> identifier/ownership -> business preconditions -> complete candidate values/events -> serialize -> validate every outbound capacity -> commit state references -> append bytes. Before commit, any error leaves sessions, rooms, readiness, membership, counters, tickets, and queues unchanged. A later send failure becomes control-session loss and does not roll back a committed transaction.

## 4. Start, roster freeze, and battle attachment

Host Start validates, in order: active requester, room membership, Open lifecycle, host ownership, participant count equals capacity, all ready, all control connections live, no prior preparation, BattleId availability, duration arithmetic, and sufficient configured spawns.

It then constructs all candidates before commit: copies stable join order; maps SessionId to PlayerId; assigns continuous slots `0..N-1`; creates exactly one immutable `ActiveRoster` and `BattleState`; computes duration bounds with widened arithmetic; allocates one BattleId; generates one ticket per participant; builds immutable bootstrap records; serializes all `BattlePreparing` events; proves every control queue has capacity. Only then does Open become PreparingBattle. Failed Start consumes no BattleId and changes no readiness.

Each ticket is exactly 16 bytes from `RandomNumberGenerator.Fill`, compared byte-for-byte, scoped to BattleId and SessionId, single-use, and invalidated on successful attach, abort, completion, or disposal. It is only a local-demo connection-binding capability; without TLS it claims neither confidentiality nor public-network authentication.

The separate IPv4 battle listener accepts candidates nonblockingly. Each candidate owns a 16-byte buffer and offset. At most one readiness-guarded read per server Pump requests only `16-offset`, so no byte from a following Gate 13 frame is consumed. EOF early or invalid/expired/used/wrong-scope ticket closes only that candidate. A valid ticket reserves one participant. The server writes one acceptance byte `0x01` with bounded progress; ownership transfers only after it is fully sent.

The client writes only the ticket with partial-send support, reads exactly one acceptance byte, sends no battle frame before acceptance, and requires both acceptance and matching `BattleStarted`. When all participants attach, the server constructs exactly one slot-ordered `TcpBattleParticipantBinding[]` and one Gate 13 `TcpSharedBattleSession`. Each client transfers exactly one battle `TcpClient` into one Gate 13 `PredictedTcpClientBattleRuntime`.

Control sockets remain owned by Demo server/client across normal battle completion. Preparation owns candidate battle sockets until transfer. Gate 13 server/client runtimes own battle sockets after transfer. No socket has two disposal owners.

## 5. Additive control Protocol

The existing `lockstep_arena_protocol.proto` remains the sole schema; existing battle messages and field numbers are unchanged. Codegen remains pinned to Grpc.Tools `2.83.0`, `GrpcServices=None`, `CompileOutputs=false`, `file_extension=.g.cs`, and the bundled protoc.

Client union:

```proto
message ClientControlCommandMessage {
  oneof command {
    EnterSessionCommandMessage enter_session = 1;
    RequestRoomListCommandMessage request_room_list = 2;
    CreateRoomCommandMessage create_room = 3;
    JoinRoomCommandMessage join_room = 4;
    LeaveRoomCommandMessage leave_room = 5;
    SetReadyCommandMessage set_ready = 6;
    StartBattleCommandMessage start_battle = 7;
    ReturnToLobbyCommandMessage return_to_lobby = 8;
    ExitSessionCommandMessage exit_session = 9;
  }
}
```

Payload fields: EnterSession `{string nickname=1}`; CreateRoom `{string room_name=1; uint32 capacity=2}`; JoinRoom `{uint64 room_id=1}`; SetReady `{bool is_ready=1}`; the other five command messages are empty.

Server union preserves fields 1–8 and adds the approved success event at field 9:

```proto
message ServerControlEventMessage {
  oneof event {
    SessionEnteredEventMessage session_entered = 1;
    RoomListEventMessage room_list = 2;
    RoomSnapshotEventMessage room_snapshot = 3;
    BattlePreparingEventMessage battle_preparing = 4;
    BattleStartedEventMessage battle_started = 5;
    BattleStatusEventMessage battle_status = 6;
    BattleSettlementEventMessage battle_settlement = 7;
    CommandRejectedEventMessage command_rejected = 8;
    LobbyEnteredEventMessage lobby_entered = 9;
  }
}
message LobbyEnteredEventMessage {}
```

Exact supporting DTOs:

```proto
enum RoomLifecycleMessage { ROOM_LIFECYCLE_UNSPECIFIED=0; ROOM_LIFECYCLE_OPEN=1; ROOM_LIFECYCLE_PREPARING_BATTLE=2; ROOM_LIFECYCLE_IN_BATTLE=3; ROOM_LIFECYCLE_SETTLED=4; }
message SessionEnteredEventMessage { uint64 session_id=1; string nickname=2; }
message RoomSummaryMessage { uint64 room_id=1; string room_name=2; string host_nickname=3; uint32 participant_count=4; uint32 capacity=5; RoomLifecycleMessage lifecycle=6; }
message RoomListEventMessage { repeated RoomSummaryMessage rooms=1; }
message RoomParticipantMessage { uint64 session_id=1; string nickname=2; bool is_host=3; bool is_ready=4; uint32 join_ordinal=5; }
message RoomSnapshotEventMessage { uint64 room_id=1; string room_name=2; uint64 host_session_id=3; uint32 capacity=4; RoomLifecycleMessage lifecycle=5; repeated RoomParticipantMessage participants=6; }
message InitialPlayerStateMessage { uint32 player_slot=1; sint32 position_x=2; sint32 position_z=3; uint32 aim=4; }
message BattleBootstrapMessage { uint64 battle_id=1; ActiveRosterMessage roster=2; repeated InitialPlayerStateMessage player_states=3; uint32 initial_tick=4; uint32 battle_duration_ticks=5; uint32 input_delay_ticks=6; uint32 final_state_tick=7; }
message BattlePreparingEventMessage { uint64 room_id=1; uint64 battle_id=2; bytes battle_ticket=3; uint32 battle_port=4; uint64 local_player_id=5; uint32 local_player_slot=6; BattleBootstrapMessage bootstrap=7; }
message BattleStartedEventMessage { uint64 battle_id=1; }
message BattleStatusEventMessage { uint64 battle_id=1; uint32 server_state_tick=2; uint32 next_publish_tick=3; }
enum BattleSettlementReasonMessage { BATTLE_SETTLEMENT_REASON_UNSPECIFIED=0; BATTLE_SETTLEMENT_REASON_TICK_LIMIT_REACHED=1; BATTLE_SETTLEMENT_REASON_ABORTED=2; }
message SettlementPlayerStateMessage { uint32 player_slot=1; uint64 player_id=2; sint32 position_x=3; sint32 position_z=4; uint32 aim=5; }
message FinalBattleStateMessage { uint32 tick=1; fixed64 state_digest=2; repeated SettlementPlayerStateMessage player_states=3; }
message BattleSettlementEventMessage { uint64 battle_id=1; BattleSettlementReasonMessage reason=2; FinalBattleStateMessage final_state=3; string detail=4; }
enum ControlRejectReasonMessage { CONTROL_REJECT_REASON_UNSPECIFIED=0; CONTROL_REJECT_REASON_INVALID_PHASE=1; CONTROL_REJECT_REASON_INVALID_VALUE=2; CONTROL_REJECT_REASON_NICKNAME_IN_USE=3; CONTROL_REJECT_REASON_ROOM_NOT_FOUND=4; CONTROL_REJECT_REASON_ROOM_FULL=5; CONTROL_REJECT_REASON_NOT_HOST=6; CONTROL_REJECT_REASON_NOT_READY=7; CONTROL_REJECT_REASON_RESOURCE_LIMIT=8; }
message CommandRejectedEventMessage { ControlRejectReasonMessage reason=1; string detail=2; }
```

Generated DTOs never become domain storage. Bootstrap mapping checks nested presence, nonzero BattleId, roster structure, exact state count, canonical Slot order, missing/duplicate/unknown/non-contiguous Slot, checked uint32-to-int Slot, arena bounds, Aim <= `ushort.MaxValue`, positive duration, and Tick arithmetic before constructing `BattleState`. It also requires `localPlayerId == roster[localPlayerSlot]`. Bootstrap is startup data, not runtime Snapshot/state sync. There is no request ID, generic opcode, registry, router, or middleware.

## 6. Pump ordering and lifecycle

Each `TcpDemoServer.PumpOnce()` performs exactly: reject disposed/faulted; accept at most one ready control connection; accept at most one ready battle connection; process sessions in stable SessionId order with at most one bounded send segment, one readiness-guarded read, and `MaxControlMessagesPerPump` commands; progress each attachment by one bounded operation; process active rooms in stable RoomId order with exactly one Gate 13 Pump; queue BattleStatus only when server Tick or `NextPublishTick` changes; detect completion/failure; commit settlement/abort and queue events; return result. Accept/Read never occurs without readiness. Sends retain offsets and are bounded by the configured send limit.

Each `TcpDemoClient.PumpOnce(localInput)` performs exactly: reject disposed/faulted; progress nonblocking control connect; one bounded send segment; one readiness-guarded bounded read; bounded complete control events; one attachment operation if preparing; construct one predicted runtime only after ticket acceptance and matching BattleStarted; in battle call one Gate 13 Update, boundedly reconciling authority and passing local input only through FinalInputTick; verify pending settlement only when authority reaches FinalTick; publish one immutable snapshot; return result. No exact read-size assumption or background work exists.

Writing `LeaveRoom` or `ReturnToLobby` does not change the client phase. `CommandRejected` keeps the phase. `LobbyEntered` clears room presentation and applicable battle/bootstrap/ticket/settlement presentation, enters Lobby, and queues the normal room-list refresh. ReturnToLobby is valid only from verified normal or accepted Aborted settlement. Exit closes control/battle ownership and returns to Disconnected. Server disposal stops accepts, invalidates tickets, disposes prepared sockets, Gate 13 sessions, control sockets, then listeners.

Given initial Tick `S` and positive duration `D`, widened arithmetic must produce `FinalStateTick=S+D` and `FinalInputTick=FinalStateTick-1` without uint overflow. Clients never submit beyond FinalInputTick but continue `Update(null)` to drain authority. Server normal completion requires exact equality with FinalStateTick; exceeding it is impossible/fatal. Simulation remains duration-unaware.

TickLimit settlement includes BattleId, reason, a present final Tick/digest/per-Slot state. Aborted contains no final-state claim. An early normal settlement remains in `SettlementPendingAuthority`; at equal authority Tick the client validates roster, every state, digest, zero missing prior authority, and predicted convergence. Authority beyond FinalTick or any mismatch is fail-stop. Successful verification disposes battle runtime/socket but preserves the control session and Settlement presentation until confirmed ReturnToLobby.

## 7. Frozen two-client Golden

Bravo connects first (`SessionId=1`); Alpha second (`SessionId=2`) creates RoomId 1 capacity 2 and is host; Bravo joins; BattleId is 1. Stable join order freezes Slot0 -> PlayerId2 Alpha and Slot1 -> PlayerId1 Bravo.

```text
Initial Tick 0; BattleDurationTicks 4; FinalInputTick 3; FinalStateTick 4
InputDelayTicks 2; MaxPredictionTicks 4
Initial Slot0: X=-300 Z=0 Aim=1000
Initial Slot1: X= 300 Z=0 Aim=2000
Initial Digest: D08BA63E403C71AF
```

| Tick | Slot0 input | Slot1 input |
|---:|---|---|
| 0 | Move(-1,0), Aim 10100 | Move(1,0), Aim 20100 |
| 1 | Move(0,1), Aim 10101 | Move(0,-1), Aim 20101 |
| 2 | Move(1,0), Aim 10102 | Move(-1,0), Aim 20102 |
| 3 | Move(0,-1), Aim 10103 | Move(0,1), Aim 20103 |

```text
State1: Slot0(-400,0,10100), Slot1(400,0,20100), Digest F82D3BE4A98024B5
State2: Slot0(-400,100,10101), Slot1(400,-100,20101), Digest A4DAA5FBE7E940ED
State3: Slot0(-300,100,10102), Slot1(300,-100,20102), Digest 7A653646579E6AEC
State4: Slot0(-300,0,10103), Slot1(300,0,20103), Digest D8E54FF828A4C670
```

Both clients predict 0–3 before authority; unknown remote input is initially neutral with remote initial Aim, while actual remote input differs. Expected Dirty on Alpha and Bravo is `[true,true,true,true]`. Server, both authoritative states, both predicted states, and both Replay reconstructions converge at State Tick 4 and digest `D8E54FF828A4C670`. Ticket bytes are never Golden constants. The Golden vector returns actual results only; expected IDs/states/Dirty/digests exist only in consumer tests.

## 8. Exact automated acceptance

New dependency-free .NET executable `Tests/LockstepArena.DemoFlow.Tests` contains `Program.cs`, `TestAssert.cs`, `ControlProtocolTests.cs`, `SessionRoomTests.cs`, `BattlePreparationTests.cs`, `SettlementLifecycleTests.cs`, `Gate14DemoGoldenVector.cs`, and `Gate14DemoGoldenTests.cs`. Its direct references are exactly DemoHost, Client Demo, Protocol, and Simulation. Result is exactly `48/48` with this registry:

1. `ControlCommandOneOfRoundTripsEveryApprovedVariant`
2. `ControlEventOneOfRoundTripsEveryApprovedVariant`
3. `ExistingBattleMessagesRemainWireCompatible`
4. `BattleBootstrapRoundTripPreservesCanonicalState`
5. `BattleBootstrapRejectsMissingRosterOrState`
6. `BattleBootstrapRejectsDuplicateMissingUnknownOrNoncontiguousSlot`
7. `BattleBootstrapRejectsOutOfRangePositionOrAim`
8. `WireIdentifierCapacityAndPortNarrowingRejectBeforeMutation`
9. `NicknameValidationUsesTrimControlUtf8AndOrdinalRules`
10. `RoomNameValidationUsesTrimControlAndUtf8Rules`
11. `SessionIdsStartAtOneIncreaseAndAreNeverReused`
12. `SessionIdExhaustionRejectsWithoutWrapOrMutation`
13. `DuplicateActiveNicknameRejectsWithoutAllocatingSession`
14. `CreateRoomAutoJoinsHostAtJoinOrdinalZero`
15. `CreateRoomCapacityUsesConfiguredSpawnBoundNotOneVsOne`
16. `RoomIdExhaustionRejectsWithoutRoomMutation`
17. `RoomListUsesStableCreationOrderAndCompleteSummaries`
18. `JoinAppendsStableJoinOrderAndBroadcastsSnapshot`
19. `JoinRejectsMissingClosedFullOrAlreadyJoinedRoom`
20. `ReadyAndUnreadyBroadcastAtomicRoomSnapshots`
21. `OpenNonHostLeaveRemovesOnlyThatParticipant`
22. `OpenHostLossClosesRoomAndReturnsSurvivorsToLobby`
23. `StartRejectsNonHostWithoutMutation`
24. `StartRejectsNonFullRoomWithoutMutation`
25. `StartRejectsAnyUnreadyParticipantWithoutMutation`
26. `StartAtomicallyFreezesRosterBootstrapTicketsAndEvents`
27. `RosterSlotsFollowJoinOrderRatherThanPlayerId`
28. `BattleIdExhaustionRejectsWithoutPreparationMutation`
29. `TicketsAreSixteenByteRandomScopedUniqueAndSingleUse`
30. `BattleAttachmentAccumulatesPartialTicketWithoutOverread`
31. `InvalidExpiredReusedOrWrongScopeTicketClosesOnlyCandidate`
32. `ClientWaitsForAcceptanceAndBattleStartedBeforeBattleInput`
33. `PreparingBattleExplicitLeaveIsRejected`
34. `InBattleExplicitLeaveIsRejected`
35. `PreparingControlLossInvalidatesTicketsAndClosesPreparedSockets`
36. `AllAttachmentsCreateExactlyOneGate13SharedBattleSession`
37. `ServerAndClientPumpsPerformOnlyFrozenBoundedWork`
38. `ClientNeverSubmitsInputPastFinalInputTick`
39. `TickLimitCreatesOneNormalSettlementAtExactFinalTick`
40. `EarlySettlementRemainsPendingUntilFinalAuthority`
41. `MatchingSettlementVerifiesCompleteStateAndDigest`
42. `SettlementMismatchOrImpossibleTickFailsStop`
43. `NormalSettlementDisposesBattleButPreservesControlSession`
44. `BattleFailureAbortsRoomAndNotifiesSurvivingControls`
45. `GoldenCompletesFullNicknameRoomReadyStartBattleFlow`
46. `GoldenBothClientsDirtyRollbackAndConvergeAtStateFour`
47. `GoldenSettlementReturnRemovesRoomAndPreservesSessions`
48. `AlternateControlAndBattleSegmentationProducesSameGoldenResult`

Tests 2, 21, 22, and 47 include the exact `LobbyEntered` behavior. Existing constructor/resource coverage proves history zero/negative rejection; Test 15 proves `MaxRoomCapacity > MaxSessions` rejection, equal-boundary success, and spawn coverage, without adding tests. Narrow reflection may set the exact three ID counters for exhaustion; no production hook exists.

Unity assembly `LockstepArena.Demo.Editor.Tests` must report exactly `3/3`:

1. `UnityDemoSceneTests.DemoSceneContainsSingleDebugController`
2. `UnityDemoAssemblyTests.UnityLoadsMigratedGate13ClientLiveTcpTypes`
3. `UnityDemoPresentationTests.UnityFormatsFrozenGoldenSettlementAndDiagnostics`

## 9. Manual v1 acceptance and Debug visibility

Frozen demo values are loopback `127.0.0.1`, control 46000, battle 46001, capacity 2, InputDelay 2, duration 4, scripted Golden input enabled. The exact 20-step run is: start DemoHost; open demo scene; launch one Player plus Editor client; connect Bravo first then Alpha; Alpha creates `Golden Room`; Bravo lists/joins; confirm participants/host; prove Bravo Start rejects NOT_HOST; prove Start rejects while Bravo unready; ready Bravo; Alpha starts; verify Slot/PlayerId mapping; verify both attachments; verify both predicted runtimes; inspect Tick/pending/Replay/Dirty; verify both Dirty sequences; verify Tick4/digest settlement; Return both and ensure Room1 absent; Exit both and stop server.

The Debug view exposes SessionId, RoomId, BattleId, host/capacity/readiness, PlayerId/Slot, server/authoritative/predicted Tick, pending prediction/authority, Replay count, latest/cumulative Dirty, authoritative/predicted digest, blocked NextPublishTick, and settlement verification. It is presentation only, not telemetry infrastructure.

## 10. Final verification and repository closure

Exactly 24 sequential Release builds are required: Simulation; StreamFraming; Protocol; ClientPrediction; migrated Client LiveTcp; Client Demo; Server FrameSync; Server ProtocolAuthority; Server LiveTcp; Server Verification; Server DemoHost; Protocol CodeGen; then the Simulation, FrameSync, Protocol, ProtocolAuthority, StreamFraming, TCP EndToEnd, TickAuthority, TickPacing, ClientPrediction, LiveTcp, LivePrediction, and DemoFlow test projects. Every build is `0 warnings / 0 errors`; if a missing restore asset is recreated using the frozen project contract, restart at build 1.

Fresh .NET results are Gate3 38/38, Gate4 32/32, Gate5 35/35, Gate6 24/24, Gate7 32/32, Gate8 8/8, Gate9 27/27, Gate10 27/27, Gate11 32/32, Gate12 36/36, Gate13 49/49, Gate14 48/48, plus Server Golden `89A7DD66F8D9E871`. Real-TCP Gates 8/11/13/14 run under an external bounded watchdog, not a product timeout.

Fresh Unity 6000.3.10f1 NUnit XML is required for Gate14 Demo 3/3, Gate12 Prediction 2/2, Gate7 StreamFraming 1/1, Gate5 Protocol 2/2, and Gate3 named Golden passed with zero failures. Process exit alone is not evidence. Inspect and restore only individually confirmed Unity-generated worktree serialization diffs.

Pinned Protocol regeneration records Grpc.Tools version, bundled protoc path/version, no overrides, exactly one `.proto`, exactly one `.g.cs`, and schema/generated diff clean.

Final authored closure also updates `README.md`, adds `Docs/Architecture/LOCKSTEP_ARENA_V1_ARCHITECTURE.md` with Mermaid data flow, and appends Implementation Evidence here. README records prerequisites, versions, restore/build/run commands, scene/build steps, exact two-client demo, diagnostics, Gate0–14 capabilities, limitations, TCP-only scope, and deterministic verification. No screenshot/binary evidence is committed without separate approval.

## 11. Protected boundaries and exclusions

Relative to Gate 13, Shared Simulation, ClientPrediction, StreamFraming, Server FrameSync, Server ProtocolAuthority, Server LiveTcp, every Gate3–13 existing test source/Golden, and existing Unity scenes/assets/settings remain committed-diff zero. Exceptions are only additive Protocol schema/generated/mapping, the blob-identical Client LiveTcp rename/package metadata, two test csproj path retargets, new DemoHost/Client Demo/Demo Unity/Gate14 tests/docs, exact lockfile, and exact `.gitignore` changes.

Package audit forbids `bin`, `obj`, LockstepArena build DLLs, symlink/junction, sync/copy scripts, duplicate generated code, or duplicate Gate 13 runtime.

Excluded from v1: MySQL/persistence/accounts/passwords, chat, matchmaking, room passwords/invites/spectators/host migration, KCP/UDP/transport switching, reconnect/resume/heartbeat/retry, TLS/certificates/NAT/public deployment/clustering, adaptive delay, timeout neutral/repeat-last input, server prediction, another rollback/Snapshot/Replay system, interpolation/gameplay view framework, combat/damage/scoring/winners/rewards/currency/ranking/inventory, ECS, generic networking/session framework, DI/EventBus/middleware/router/plugin registry, and Gate 15.

## 12. Implementation Evidence

Fresh Gate 14 verification was completed on 2026-09-12 from the isolated `codex/gate14-v1-demo-closure` worktree. The implementation checkpoint immediately before this evidence update was `58e4e9afabcbd94ec4f807dee6bbe5c0019229ff`.

### Build and automated execution

- All 24 frozen Release builds completed sequentially with `0 warnings / 0 errors`.
- Dependency-free .NET suites passed: Gate3 `38/38`, Gate4 `32/32`, Gate5 `35/35`, Gate6 `24/24`, Gate7 `32/32`, Gate8 `8/8`, Gate9 `27/27`, Gate10 `27/27`, Gate11 `32/32`, Gate12 `36/36`, Gate13 `49/49`, and Gate14 `48/48`.
- The Server Golden completed at Tick 1000 with four players and digest `89A7DD66F8D9E871`.
- The real-TCP Gates 8, 11, 13, and 14 completed under the external bounded watchdog; no product timeout was added.
- A final focused Gate14 execution after the manual run again reported `RESULT 48/48 passed`.

### Deterministic v1 Golden

The actual-only Gate14 vector completed the nickname/session, room, Ready/Start, ticketed BATTLE attachment, Gate13 predicted live runtime, settlement, ReturnToLobby, and Exit flow. Both clients produced Dirty `[true,true,true,true]`; server, authoritative client states, predicted client states, and Replay converged at State Tick 4 and digest `D8E54FF828A4C670`. The frozen intermediate digests remained `D08BA63E403C71AF`, `F82D3BE4A98024B5`, `A4DAA5FBE7E940ED`, `7A653646579E6AEC`, and `D8E54FF828A4C670`.

### Unity execution

Unity `6000.3.10f1` produced fresh NUnit XML for every required assembly. Gate14 Demo reported `3/3`, Gate12 Prediction `2/2`, Gate7 StreamFraming `1/1`, Gate5 Protocol `2/2`, and the Gate3 named Golden `1/1`; every required named test was `Passed` with zero failures. Process exit codes were not used as the test oracle. Each run's exact worktree-local serialization diff was inspected and individually restored. The Windows Player was built from the dedicated Gate14 scene at `.artifacts/Gate14DemoPlayer/LockstepArenaDemo.exe`.

### Manual Player plus Editor acceptance

One Windows Player (`Bravo`) and one Editor client (`Alpha`) used the real loopback DemoHost on CONTROL `46000` and BATTLE `46001`. The observed flow covered room creation/join, host and participant presentation, both Ready states, host Start, frozen `Slot0/PlayerId2` and `Slot1/PlayerId1`, both Gate13 battle clients, four Dirty reconciliations, settlement at Tick 4 with both digests `D8E54FF828A4C670` and `SettlementVerified=True`, Return of both sessions, refreshed `Rooms=[]`, Exit of both clients, and normal server stop. The operator did not separately capture the transient `NOT_HOST` and `NOT_READY` strings; their exact no-mutation rejection contracts remain covered by Gate14 automated tests `StartRejectsNonHostWithoutMutation` and `StartRejectsAnyUnreadyParticipantWithoutMutation`.

### Protocol and repository audits

- Pinned regeneration used Grpc.Tools `2.83.0` and bundled `protoc` `libprotoc 35.1` at `C:\Users\张晨旭\.nuget\packages\grpc.tools\2.83.0\tools\windows_x64\protoc.exe`; no `PROTOBUF_PROTOC` or `Protobuf_ProtocFullPath` override was present.
- Exactly one `.proto` and one tracked `.g.cs` remained. Generated-source SHA-256 was unchanged at `786B1454EEC2DC1DF1F9B263904833EC96FBC2114B70F0CEAE309F2A285B33D5`, and schema/generated regeneration diff was clean.
- Relative to frozen Gate13 baseline `57243ae82ff87a5f51dc86d34342d697963b34dc`, protected Simulation, ClientPrediction, StreamFraming, Server FrameSync, ProtocolAuthority, Server LiveTcp, ProjectSettings, manifest, and pre-existing tests had zero committed diff.
- The two migrated Gate13 client runtime blobs matched their originals exactly, the old paths were absent, and only one physical copy of each source existed.
- Package manifests and the two embedded lockfile entries matched their declared dependency graphs. `.gitignore` added exactly the three approved authored-project exceptions.
- No package-local `bin`, `obj`, or LockstepArena build DLL existed. No Git symlink, filesystem junction/reparse point, copy/sync script, duplicate generated source, or disallowed Gate14 Task/Thread/async/KCP/UDP/MySQL/DI/EventBus dependency was found.
- `git diff --check` passed. The isolated worktree contained no Unity recovery or nested-project artifacts after exact cleanup. The ordinary checkout retained only the user-owned `Assets/Settings/Mobile_RPAsset.asset` and `ProjectSettings/ShaderGraphSettings.asset` modifications.
