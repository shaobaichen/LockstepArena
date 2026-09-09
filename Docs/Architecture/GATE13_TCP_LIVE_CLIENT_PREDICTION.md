# Gate 13: TCP Live Client Prediction and Shared Battle Authority

## Status and baseline

This document consolidates the independently approved Gate 13 Direction,
Direction Amendment, and Section 1 Architecture Contract.

Frozen Gate 12 baseline:

~~~text
ff9a1a0010daecf2727096c063d62e92575259df
~~~

Gate 13 scope:

~~~text
TCP live client prediction
+ real shared multi-client battle authority
+ bounded authority catch-up
+ authoritative replay
+ deterministic weak-network proof
~~~

Gate 13 remains TCP-only. Gate 14 owns Login, Room, Ready, Host Start,
Settlement, presentation, and final v1 acceptance. There is no Gate 15.

## 1. Production ownership and authored files

No new production assembly is introduced.

Server production remains in:

~~~text
Server/LockstepArena.Server.LiveTcp/
~~~

New Server sources:

~~~text
TcpBattleParticipantBinding.cs
TcpSharedBattleSession.cs
~~~

The existing Server LiveTcp project adds one direct reference to
LockstepArena.Protocol so the shared session can validate the identity and
Slot carried by a complete submission before delegating it to the one shared
ProtocolAuthorityProcessor.

Client production remains in:

~~~text
Client/LockstepArena.Client.LiveTcp/
~~~

New Client source:

~~~text
PredictedTcpClientBattleRuntime.cs
~~~

That file owns LocalInputSample, PredictedClientUpdateResult, and
PredictedTcpClientBattleRuntime. The Client LiveTcp project adds one direct
reference to LockstepArena.Client.Prediction.

TcpClientBattlePump.cs receives only the frozen internal transport-only
extraction. Its Gate 11 public constructor, public members, and legacy
Simulation behavior remain compatible. TcpServerBattlePump.cs is unchanged.

No new embedded package, asmdef, protocol schema, generated source, Unity
production assembly, transport abstraction, or production test hook exists.

## 2. Participant binding

~~~csharp
namespace LockstepArena.Server.LiveTcp
{
    public sealed class TcpBattleParticipantBinding
    {
        public TcpBattleParticipantBinding(
            TcpClient connectedClient,
            PlayerId playerId,
            PlayerSlot playerSlot);

        public TcpClient ConnectedClient { get; }
        public PlayerId PlayerId { get; }
        public PlayerSlot PlayerSlot { get; }
    }
}
~~~

The binding is immutable. Its constructor rejects a null TcpClient with
ArgumentNullException. Roster membership, IPv4, connected state, and
battle-wide uniqueness belong to TcpSharedBattleSession.

## 3. Shared battle session API

~~~csharp
namespace LockstepArena.Server.LiveTcp
{
    public sealed class TcpSharedBattleSession : IDisposable
    {
        public TcpSharedBattleSession(
            BattleState initialState,
            TcpBattleParticipantBinding[] participants,
            uint inputDelayTicks,
            uint maxFutureTickOffset,
            int authoritativeHistoryCapacity,
            int maxPayloadLength,
            int receiveBufferLength,
            int receiveOffset,
            int receiveReadCapacity);

        public BattleState ServerState { get; }
        public uint NextPublishTick { get; }
        public int ParticipantCount { get; }

        // Unique authoritative payloads broadcast, not socket writes.
        public int PumpOnce();

        public void Dispose();
    }
}
~~~

initialState.Roster is the sole active roster. The session is single-threaded.

## 4. Shared session construction

Validation and operation order:

~~~text
1. initialState null
2. participants null
3. participant count differs from roster count
4. null participant entry in caller-array order
5. receiveBufferLength < 1
6. receiveOffset < 0
7. receiveReadCapacity < 1
8. receive segment does not fit buffer
9. invalid MaxPayloadLength through the frozen framing contract
10. PlayerSlot outside roster
11. duplicate PlayerSlot
12. duplicate PlayerId
13. duplicate TcpClient reference
14. roster PlayerId differs from binding
15. participant is not IPv4
16. participant is not connected
17. locally construct streams, decoders, and receive buffers
18. locally construct exactly one ProtocolAuthorityProcessor
19. defensively copy bindings into increasing PlayerSlot order
20. final field assignment and ownership transfer
~~~

Null values use ArgumentNullException. Scalar/range failures use
ArgumentOutOfRangeException. Duplicate or mismatched bindings use
ArgumentException. A disconnected participant uses InvalidOperationException.
Framing and ProtocolAuthorityProcessor remain the validators of their own
configuration.

Before final assignment there is no network I/O, caller connection disposal,
or externally visible active session. Failed construction leaves all supplied
connections caller-owned and usable. Successful construction transfers all
participant connections to the session.

## 5. One shared Server authority

TcpSharedBattleSession owns exactly one ProtocolAuthorityProcessor. That
processor remains the owner of:

- the authority coordinator and scheduled publisher;
- the authority timing driver;
- the authoritative BattleSimulation.

The session must not create another AuthoritativeFrameCoordinator,
TickDrivenFramePublisher, StopwatchTickDriver, or BattleSimulation.

~~~text
TcpSharedBattleSession
└── ProtocolAuthorityProcessor
    ├── shared authority scheduling/timeline
    └── shared authoritative BattleSimulation
~~~

## 6. Per-participant receive state and anti-spoofing

For each participant, the session privately owns the TcpClient, NetworkStream,
LengthPrefixedFrameDecoder, reusable receive buffer, receive offset/read
capacity, and bound PlayerId/PlayerSlot. The private participant array is in
increasing PlayerSlot order.

Each successful nonterminal PumpOnce visits every participant in Slot order,
performs at most one bounded readable NetworkStream.Read, and directly feeds:

~~~csharp
decoder.Feed(buffer, receiveOffset, bytesRead)
~~~

No exact-sized input copy and no TCP read-size assumption are allowed.

Every recovered PlayerInputSubmissionMessage is parsed and mapped with the
existing ProtocolMapper. Before delegation, require:

~~~text
submitted PlayerId == connection-bound PlayerId
input PlayerSlot    == connection-bound PlayerSlot
roster[bound Slot] == bound PlayerId
~~~

Only then may the original complete payload reach the shared
ProtocolAuthorityProcessor. The processor intentionally retains its existing
parse/map/Domain validation. Gate 13 does not duplicate narrowing,
canonicalization, duplicate-input, complete-frame, or future-window rules.

## 7. Exact Server pump and broadcast transaction

Pump order:

~~~text
for each participant in increasing PlayerSlot:
    at most one bounded readable receive
    for each recovered submission in decoder order:
        validate connection binding
        call shared SubmitPlayerInputPayload
        immediately broadcast returned authority

after all participant fan-in:
    call shared PollAuthority exactly once
    broadcast returned authority
~~~

Submission authority precedes same-Pump poll authority.

For each returned byte[][], the session first frames every payload into local
candidate arrays. If framing fails, no payload from that returned batch is
written. It then writes frame-major and participant-minor:

~~~text
authority payload 0 -> Slot0, Slot1, ... SlotN-1
authority payload 1 -> Slot0, Slot1, ... SlotN-1
...
~~~

PumpOnce returns only after all writes succeed and returns the count of unique
authoritative payloads, not participant write operations.

If a later write fails, earlier bytes and already-created authority remain
committed. No partial return, rollback, resend, or compensation occurs. The
whole session sticky-faults and rethrows the original exception.

## 8. Shared session failure and lifetime

Pump priority:

~~~text
disposed
-> sticky fault
-> participant fan-in
-> submission broadcasts
-> exactly one Poll
-> Poll broadcasts
-> successful count
~~~

Participant EOF, framing failure, malformed protobuf, identity spoof,
authority failure, read failure, write failure, or partial broadcast failure
sticky-faults the entire session. Later mutation/pump calls reject first with
InvalidOperationException.

Read-only ServerState, NextPublishTick, and ParticipantCount remain observable
as last committed values. Dispose is idempotent and disposes every owned
connection. One participant failure never synthesizes input, removes a roster
member, performs AI takeover, or allows the remaining battle to continue.

## 9. TcpClientBattlePump transport-only extraction

The frozen public Gate 11 API and behavior remain unchanged.

Gate 13 adds only:

~~~csharp
internal static TcpClientBattlePump CreateTransportOnly(
    TcpClient connectedClient,
    ActiveRoster expectedRoster,
    int maxPayloadLength,
    int receiveBufferLength,
    int receiveOffset,
    int receiveReadCapacity);

internal FrameData[] PumpReceiveTransportOnlyOnce();
~~~

Legacy mode still owns and Steps its BattleSimulation. Transport-only mode
owns framing, protobuf parsing, and expected-roster mapping, but no
BattleSimulation and never calls Step. Both modes reuse one private
receive/decode/map path. No public mode switch or generic transport interface
is introduced.

## 10. Predicted client value types and API

~~~csharp
namespace LockstepArena.Client.LiveTcp
{
    public readonly struct LocalInputSample
    {
        public LocalInputSample(sbyte moveX, sbyte moveZ, ushort aim);
        public sbyte MoveX { get; }
        public sbyte MoveZ { get; }
        public ushort Aim { get; }
    }

    public readonly struct PredictedClientUpdateResult
    {
        public int ReconciledAuthoritativeFrameCount { get; }
        public int DirtyFrameCount { get; }
        public bool LocalPredictionSent { get; }
    }

    public sealed class PredictedTcpClientBattleRuntime : IDisposable
    {
        public PredictedTcpClientBattleRuntime(
            TcpClient connectedClient,
            BattleState initialState,
            PlayerId localPlayerId,
            PlayerSlot localPlayerSlot,
            int maxPredictionTicks,
            int maxAuthoritativeFramesPerUpdate,
            int maxPendingAuthoritativeFrames,
            int maxReplayFrames,
            int maxPayloadLength,
            int receiveBufferLength,
            int receiveOffset,
            int receiveReadCapacity);

        public PlayerId LocalPlayerId { get; }
        public PlayerSlot LocalPlayerSlot { get; }
        public BattleState AuthoritativeState { get; }
        public BattleState PredictedState { get; }
        public int PendingPredictionCount { get; }
        public int MaxPredictionTicks { get; }
        public int PendingAuthoritativeFrameCount { get; }
        public int ReplayFrameCount { get; }
        public int MaxAuthoritativeFramesPerUpdate { get; }
        public int MaxPendingAuthoritativeFrames { get; }
        public int MaxReplayFrames { get; }

        public PredictedClientUpdateResult Update(LocalInputSample? localSample);
        public BattleState ReconstructAuthoritativeState();
        public void Dispose();
    }
}
~~~

LocalInputSample validates MoveX and MoveZ as -1, 0, or 1. It intentionally
has no Tick, PlayerId, or PlayerSlot. A null sample means authority-only work.
Prediction capacity is full exactly when:

~~~text
PendingPredictionCount == MaxPredictionTicks
~~~

MaxPredictionTicks is the constructor value and remains readable after sticky
fault.

## 11. Predicted runtime construction and ownership

Constructor order:

~~~text
1. connectedClient null
2. initialState null
3. local PlayerSlot outside roster
4. roster PlayerId differs from local PlayerId
5. maxPredictionTicks < 1
6. maxAuthoritativeFramesPerUpdate < 1
7. maxPendingAuthoritativeFrames < 1
8. maxReplayFrames < 1
9. frozen transport/framing parameter validation
10. IPv4 and connected-state validation
11. locally create one ClientPredictionTimeline
12. locally create transport-only TcpClientBattlePump
13. locally create empty queue/replay/cache containers
14. final field assignment
~~~

The four capacities are independent.

The runtime owns exactly one transport-only pump, one
ClientPredictionTimeline, local identity, an InputFrame?[] remote-authority
cache, a FrameData[] pending-authority container, a FrameData[] authoritative
Replay history, the immutable initial state, capacities, and one sticky-fault
flag. ClientPredictionTimeline is the only persistent prediction Simulation.

## 12. Remote prediction and immutable retained frames

The remote cache is Slot-indexed. The local entry is unused.

Before a remote Slot has reconciled authority:

~~~text
MoveX = 0
MoveZ = 0
Aim   = initial PlayerState.Aim
~~~

Afterward, a newly created prediction copies the latest successfully
reconciled authoritative remote MoveX/MoveZ/Aim and rewrites only Tick to the
current predicted Tick P. The local Slot uses the caller sample. FrameData is
created in increasing Slot order through the existing Domain API.

Prediction policy is evaluated only when a new predicted FrameData is created.
Once retained by ClientPredictionTimeline, that frame is immutable. Later
authority does not regenerate or replace it; Dirty reconciliation replays it
exactly. Only future predictions use a newer remote-authority cache. Several
following authority frames may therefore independently be Dirty.

## 13. Update transaction

Update order:

~~~text
disposed
-> sticky fault
-> receive or backlog-first bounded reconciliation
-> optional one-sample prediction
-> independent result value
~~~

If pending authority is non-empty at receive phase start, perform no network
read and reconcile at most MaxAuthoritativeFramesPerUpdate oldest frames.
Otherwise perform at most one transport-only receive, atomically admit the
decoded batch, and reconcile the same bounded prefix.

After authority work:

- null sample performs no prediction;
- terminal P performs no prediction;
- full prediction capacity performs no prediction or send;
- otherwise build exactly one complete Frame at current P;
- call Timeline.Predict;
- send only the local InputFrame.

Prediction commits before send. Send failure preserves the prediction,
sticky-faults the runtime, performs no rollback/retry, and returns no result.

## 14. Authority admission and replay reservation

Pending authority is a private FrameData[] with these invariants:

~~~text
Count <= MaxPendingAuthoritativeFrames
structurally equal roster
strictly contiguous Ticks
first Tick == AuthoritativeState.Tick
~~~

Admission order:

~~~text
candidate sanity
-> pending capacity
-> Replay reservation capacity
-> full roster validation
-> full contiguous Tick validation
-> construct candidate pending array
-> one container replacement
~~~

Capacity uses widened arithmetic:

~~~text
Pending.Count + Candidate.Count <= MaxPendingAuthoritativeFrames

Replay.Count + Pending.Count + Candidate.Count <= MaxReplayFrames
~~~

If either check fails, admit/reconcile zero candidates, mutate no queue or
Timeline state, drop/evict/grow nothing, sticky-fault, and throw
InvalidOperationException. The framing decoder may already have consumed the
TCP bytes because overflow is fail-stop.

## 15. Bounded reconciliation

For each oldest pending frame, up to the per-update work limit:

1. build candidate Replay with the frame appended;
2. build candidate remote cache from that authority;
3. build candidate pending array without the oldest frame;
4. call Timeline.ReconcileAuthoritative;
5. after success, replace Replay, cache, and pending containers;
6. increment reconciled and Dirty counts.

Each authority frame commits independently. If a later frame fails, earlier
successful frames remain committed, no partial update result is returned,
the failed frame changes no runtime-owned containers, and the runtime
sticky-faults without retry or rollback.

## 16. Authoritative Replay

Replay owns:

~~~text
initial BattleState
+ successfully reconciled authoritative FrameData in strict Tick order
~~~

It never evicts and never stores predicted mistakes, Gate 12 rollback
snapshots, transient Dirty states, protobuf bytes, framed bytes, or TCP chunk
boundaries.

ReconstructAuthoritativeState creates a local BattleSimulation from the
initial state, Steps Replay frames in ascending Tick order, and compares the
complete reconstructed state and StateDigest with the live authoritative
Timeline state.

A mismatch sticky-faults the runtime and throws InvalidOperationException.
Timeline state and last committed runtime containers remain unchanged. Later
Update rejects immediately. No repair/reset/retry is provided.

## 17. Terminal behavior

uint.MaxValue - 1 remains the final consumable Frame Tick and uint.MaxValue
the terminal state Tick. No server publication or client prediction consumes
Frame Tick uint.MaxValue. A predicted terminal frontier creates/sends no new
input. Its pending final authority may still reconcile. No Tick wraps.
Unexpected post-terminal authority is consumed invalid network data and
fail-stops the predicted runtime.

## 18. Frozen Goldens

### Correct live prediction

Roster:

~~~text
Slot0 PlayerId 0x0102030405060708
Slot1 PlayerId 0x000000000000002A
~~~

Initial:

~~~text
Tick100
Slot0 X=-300 Z=0 Aim=1000
Slot1 X= 300 Z=0 Aim=2000
~~~

Tick100:

~~~text
Slot0 MoveX=1 MoveZ=0 Aim=10100
Slot1 MoveX=0 MoveZ=0 Aim=2000
~~~

Client A predicts, Client B submits the neutral authority, the shared session
broadcasts, and Client A reconciles cleanly.

~~~text
State101
Slot0 X=-200 Z=0 Aim=10100
Slot1 X= 300 Z=0 Aim=2000
Digest 0x5DB198E1CB8F8ED4
Dirty count 0
A=P=101
Replay count 1
~~~

### Wrong remote prediction and immutable retention

Authority:

~~~text
Tick100 Slot0  1, 0, 10100   Slot1 -1, 0, 20100
Tick101 Slot0  0, 1, 10101   Slot1  0,-1, 20101
Tick102 Slot0 -1, 0, 10102   Slot1  1, 0, 20102
Tick103 Slot0  0,-1, 10103   Slot1 -1, 0, 20100
~~~

Exact schedule:

1. predicted Client A predicts/sends Slot0 100, 101, 102 without authority;
2. real Client B sends only Slot1 100;
3. the session publishes 100 while 101/102 remain incomplete;
4. Client A reconciles 100 Dirty;
5. Client A creates predicted 103 using reconciled Slot1 input from 100;
6. Client B sends Slot1 101, 102, 103;
7. the session publishes and broadcasts 101, 102, 103;
8. Client A reconciles at most one authority per Update;
9. Client B consumes the same authority;
10. Replay reconstructs from initial state plus authority 100..103.

Frozen state Digests:

~~~text
Authority State101  0xA64F91C7685696AC
Authority State102  0x8763814FEEAFD898
Authority State103  0x3C3A2EFAE2A06B79
Authority State104  0xC3CAAFCE96D7F832

Pre-authority predicted State103               0x8B8B6B468244E4C7
After Dirty 100 replay, A101/P103              0xEDA7579F3B1F2A20
After new prediction 103 uses authority 100   0x40EADCE076E81EFD
After Dirty 101, A102/P104                     0xFC6C81B3C49F4B1E
After Dirty 102, A103/P104                     0xC3CAAFCE96D7F832
~~~

Dirty sequence:

~~~text
true, true, true, false
~~~

Final:

~~~text
A=P=104
pending prediction=0
pending authority=0
Replay count=4

Slot0 X=-300 Z=0    Aim=10103
Slot1 X= 200 Z=-100 Aim=20100

Server/ClientA authoritative/ClientA predicted/ClientB/Replay Digest
= 0xC3CAAFCE96D7F832
~~~

All two-player Digests use the frozen 44-byte little-endian StateDigest
schema and were independently precomputed outside production Simulation.

### Weak-network schedules

The wrong-prediction vector runs twice over real IPv4 loopback TCP.

~~~text
Run A: server read=3, predicted client read=5, second client read=7
Run B: server read=1, predicted client read=2, second client read=3
~~~

Both use the same explicit application-level withholding phases. They use no
random timing, Thread.Sleep correctness assumption, or public Internet oracle.
An external watchdog may detect a hung test but is not product timeout policy.
No assertion depends on an individual TCP read size.

## 19. Gate 13 test project and exact registry

New project:

~~~text
Tests/LockstepArena.LivePrediction.Tests/
├── LockstepArena.LivePrediction.Tests.csproj
├── Program.cs
├── TestAssert.cs
├── Gate13LivePredictionGoldenVector.cs
├── TcpSharedBattleSessionTests.cs
├── PredictedTcpClientBattleRuntimeTests.cs
└── Gate13WeakNetworkGoldenTests.cs
~~~

Project contract:

~~~text
OutputType            Exe
TargetFramework       net8.0
LangVersion           12.0
Nullable              enable
ImplicitUsings        disable
TreatWarningsAsErrors true
BuildInParallel       false
~~~

Direct ProjectReferences exactly:

~~~text
Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj
Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj
Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj
Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj
~~~

No NUnit/xUnit/MSTest, earlier test-project reference, copied old Golden,
InternalsVisibleTo, or production test hook is allowed. The Golden vector
contains actual setup/results only. Expected constants remain in consumer
tests.

Exact result:

~~~text
RESULT 49/49 passed
~~~

Exact tests:

1. ParticipantBindingRejectsNullTcpClient
2. SessionRejectsNullInitialState
3. SessionRejectsNullParticipantArray
4. SessionRejectsWrongParticipantCount
5. SessionRejectsNullParticipantEntry
6. SessionRejectsDuplicateTcpClient
7. SessionRejectsDuplicatePlayerId
8. SessionRejectsDuplicatePlayerSlot
9. SessionRejectsOutOfRosterOrMismatchedBinding
10. SessionRejectsDisconnectedParticipant
11. SessionRejectsNonIpv4Participant
12. ConstructionFailureLeavesCallerConnectionsUsable
13. SharedSessionUsesExactlyOneProtocolAuthorityProcessor
14. TwoRealClientsFeedOneSharedAuthorityTimeline
15. PlayerIdSpoofIsRejectedBeforeAuthorityMutation
16. PlayerSlotSpoofIsRejectedBeforeAuthorityMutation
17. ParticipantsAreReadInIncreasingPlayerSlotOrder
18. SuccessfulNonterminalPumpPollsAuthorityExactlyOnce
19. SubmissionAuthorityPrecedesSamePumpPollAuthority
20. AuthorityBroadcastsToEveryParticipantInIdenticalOrder
21. ParticipantEofFaultsEntireSession
22. BroadcastFailureFaultsSessionWithoutAuthorityRollback
23. StickySessionFaultRejectsBeforeFurtherIo
24. LegacyTcpClientBattlePumpStillStepsItsOwnSimulation
25. TransportOnlyReceiveMapsWithoutPersistentSimulation
26. PredictedRuntimeRejectsLocalIdentityMismatch
27. PredictedRuntimeRejectsNonPositiveCapacityLimits
28. UpdateReconcilesAuthorityBeforeCreatingPrediction
29. UpdateCreatesAtMostOnePredictionAndSendsOnlyLocalInput
30. PredictionCapacityReturnsNotSentWithoutNetworkWrite
31. SendFailureAfterPredictionFaultsWithoutUndoingPrediction
32. NeutralRemoteSeedUsesInitialPlayerAim
33. NewPredictionRepeatsLatestSuccessfullyReconciledRemoteInput
34. RetainedPredictionsRemainFieldForFieldUnchangedAfterNewAuthority
35. ConsecutiveDirtyAuthoritiesReplayDeterministically
36. ExistingAuthorityBacklogSkipsNetworkRead
37. CatchUpNeverExceedsMaxAuthoritativeFramesPerUpdate
38. CoalescedExactCapacityBatchIsAdmittedAtomicallyInTickOrder
39. OverCapacityBatchFaultsWithZeroAdmissionAndReconciliation
40. InvalidRosterOrTickBatchFaultsBeforeQueueOrTimelineMutation
41. ReplayReservationCountsHistoryPendingAndCandidateFrames
42. ReplayCapacityRejectionAdmitsNoCandidateFrame
43. ReplayHistoryNeverEvictsOrGrowsBeyondCapacity
44. ReplayReconstructsAuthoritativeStateAndDigest
45. CorrectLivePredictionGoldenConvergesCleanly
46. WrongRemotePredictionGoldenRollsBackAndConverges
47. TerminalPredictionAndFinalAuthorityDoNotWrap
48. TwoWeakNetworkSchedulesProduceIdenticalAuthorityReplayAndFinalDigest
49. ReplayCorruptionEntersStickyFailStop

Test 49 may use reflection to corrupt the unique private Replay container. It
must prove valid pre-state, InvalidOperationException, sticky fault,
unchanged observable committed states, and immediate later Update rejection.
It adds no production hook or injectable Replay engine.

## 20. Repository integration and final verification

.gitignore may add exactly:

~~~text
!Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj
~~~

No manifest or packages-lock change is allowed.

Final verification requires a restore-assets preflight followed by 21
sequential Release builds with --no-restore, each with zero warnings/errors.

Required executions:

~~~text
Gate 3  38/38
Gate 4  32/32
Gate 5  35/35
Gate 6  24/24
Gate 7  32/32
Gate 8   8/8
Gate 9  27/27
Gate 10 27/27
Gate 11 32/32
Gate 12 36/36
Gate 13 49/49
Gate 3 Server Golden 89A7DD66F8D9E871
Gate 13 final Digest C3CAAFCE96D7F832
~~~

Gate 8, Gate 11, and Gate 13 real TCP suites run under existing external
watchdogs. Pinned Grpc.Tools 2.83.0 regeneration must leave the one generated
.g.cs and Schema/Generated diff clean.

Fresh Unity 6000.3.10f1 XML evidence:

~~~text
Gate 12 Client Prediction: exact 2/2, both named tests Passed
Gate 7 Stream Framing: exact 1/1, named test Passed
Gate 5 Protocol: exact 2/2, both named tests Passed
Gate 3 Simulation Golden: total>=1, failed=0, named test Passed
~~~

Gate 13 adds no Unity test assembly.

## 21. Protected boundaries

Committed diff from the frozen Gate 12 baseline must be zero for:

- all four existing embedded packages;
- Server FrameSync and ProtocolAuthority;
- TcpServerBattlePump.cs;
- all existing Gate 3-12 tests and Goldens;
- Assets and ProjectSettings;
- Packages/manifest.json and Packages/packages-lock.json.

Only these existing production edits are allowed:

- internal transport-only extraction in TcpClientBattlePump.cs;
- Client LiveTcp ClientPrediction ProjectReference;
- Server LiveTcp Protocol ProjectReference.

Audits must prove one shared Server processor/Simulation, one client
prediction Timeline, bounded authority/Replay containers, no Replay eviction,
anti-spoofing before processor submission, no silent drops, no test hooks,
no copied Golden, no tracked build artifacts, no symlink/junction/copy/sync
script, and preservation of the ordinary checkout’s two user-owned changes.

## 22. Explicit exclusions

Gate 13 excludes KCP/UDP, transport switching, Login, Room, Ready, Host,
matchmaking, Settlement, reconnect, heartbeat, retry, player replacement,
AI takeover, server neutral/repeat-last input, adaptive InputDelay, clock
synchronization, RTT compensation, async/Task/Thread networking, prediction
history regeneration, periodic Snapshot, persistent/seekable Replay,
interpolation, render smoothing, combat/ECS expansion, router/opcode/envelope
frameworks, generic netcode/transport abstractions, DI, EventBus, Gate 14
implementation, and any Gate 15.

## Implementation Evidence

Implementation evidence is intentionally absent from the Planning commit.
It may be appended only after independently authorized implementation and a
fresh successful final verification matrix.
