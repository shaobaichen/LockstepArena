# Gate 13 TCP Live Client Prediction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to execute this plan task-by-task.

**Goal:** Compose the frozen Gate 11 live TCP runtime with the frozen Gate 12 prediction timeline so two real TCP clients share one server authority, predict locally, reconcile clean and Dirty authoritative frames, retain bounded authority, and reconstruct the authoritative result deterministically.

**Architecture:** Add TcpBattleParticipantBinding and TcpSharedBattleSession to the existing Server LiveTcp assembly, and PredictedTcpClientBattleRuntime plus two value types to the existing Client LiveTcp assembly. The server session owns one ProtocolAuthorityProcessor for every participant. The client runtime owns one transport-only TcpClientBattlePump and one ClientPredictionTimeline. Frozen components continue to own framing, protobuf mapping, authority scheduling, deterministic Simulation, Dirty comparison, rollback, and prediction replay.

**Tech Stack:** .NET 8, C# 12, synchronous BCL IPv4 loopback TCP, existing LockstepArena Simulation, StreamFraming, Protocol, FrameSync, ProtocolAuthority, LiveTcp, and ClientPrediction assemblies, plus the existing dependency-free executable test pattern.

**Spec:** Docs/Architecture/GATE13_TCP_LIVE_CLIENT_PREDICTION.md

## Global constraints

- Frozen comparison base: ff9a1a0010daecf2727096c063d62e92575259df.
- Branch: codex/gate13-live-client-prediction.
- Worktree: .worktrees/gate13-live-client-prediction.
- Implementation begins from the independently approved remote Planning HEAD, not from a reset to the frozen base.
- The ordinary checkout must retain exactly:
  - Assets/Settings/Mobile_RPAsset.asset
  - ProjectSettings/ShaderGraphSettings.asset
- Every implementation task follows RED, minimal implementation, GREEN, focused audit, and commit.
- Stop and report any contradiction in the frozen contract. Do not redesign during implementation.
- Do not modify any file outside the exact file map.
- Do not begin Gate 14.

## Exact file map

Create production:

- Server/LockstepArena.Server.LiveTcp/TcpBattleParticipantBinding.cs
- Server/LockstepArena.Server.LiveTcp/TcpSharedBattleSession.cs
- Client/LockstepArena.Client.LiveTcp/PredictedTcpClientBattleRuntime.cs

Modify existing production/project files:

- Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj
- Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj
- Client/LockstepArena.Client.LiveTcp/TcpClientBattlePump.cs

Create tests:

- Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj
- Tests/LockstepArena.LivePrediction.Tests/Program.cs
- Tests/LockstepArena.LivePrediction.Tests/TestAssert.cs
- Tests/LockstepArena.LivePrediction.Tests/Gate13LivePredictionGoldenVector.cs
- Tests/LockstepArena.LivePrediction.Tests/TcpSharedBattleSessionTests.cs
- Tests/LockstepArena.LivePrediction.Tests/PredictedTcpClientBattleRuntimeTests.cs
- Tests/LockstepArena.LivePrediction.Tests/Gate13WeakNetworkGoldenTests.cs

Modify metadata:

- .gitignore

Modify only after all final verification succeeds:

- Docs/Architecture/GATE13_TCP_LIVE_CLIENT_PREDICTION.md

No other production, test, Unity, package, protocol, generated, configuration, or project file is in scope.

## Exact project changes

Server LiveTcp adds exactly one direct ProjectReference to LockstepArena.Protocol.

Client LiveTcp adds exactly one direct ProjectReference to LockstepArena.Client.Prediction.

The new Gate 13 test project contract is:

~~~text
OutputType            Exe
TargetFramework       net8.0
LangVersion           12.0
Nullable              enable
ImplicitUsings        disable
TreatWarningsAsErrors true
BuildInParallel       false
~~~

Its direct ProjectReferences are exactly:

~~~text
Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj
Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj
Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj
Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj
~~~

It does not reference an earlier test project and does not add NUnit, xUnit, MSTest, InternalsVisibleTo, or a production test hook.

The only approved .gitignore change is:

~~~text
!Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj
~~~

Packages/manifest.json and Packages/packages-lock.json remain unchanged.

## Exact frozen production API

Server binding:

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

Server shared session:

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
        public int PumpOnce();
        public void Dispose();
    }
}
~~~

Client transport-only extraction:

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

The existing public TcpClientBattlePump constructor, members, and legacy Simulation behavior remain compatible. TcpServerBattlePump.cs remains byte-for-byte unchanged.

Client prediction types:

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

## Exact operation contracts

TcpBattleParticipantBinding validates only connectedClient null and remains immutable. TcpSharedBattleSession owns roster membership, connected/IPv4 checks, and battle-wide uniqueness.

TcpSharedBattleSession constructor validation order:

~~~text
1. initialState null
2. participants null
3. participant count differs from roster count
4. null participant entry in caller-array order
5. receiveBufferLength < 1
6. receiveOffset < 0
7. receiveReadCapacity < 1
8. receive segment does not fit buffer
9. invalid MaxPayloadLength through StreamFraming
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

Failed construction performs no network I/O and leaves supplied connections caller-owned and usable. Successful construction transfers every connection to the session.

Session PumpOnce priority and order:

~~~text
disposed
sticky fault
for each participant in increasing PlayerSlot:
    at most one bounded readable NetworkStream.Read
    direct decoder.Feed(buffer, receiveOffset, bytesRead)
    parse each submission in decoder order
    verify submitted PlayerId and PlayerSlot against connection binding
    shared ProtocolAuthorityProcessor.SubmitPlayerInputPayload
    immediately broadcast returned authority
after all participants:
    shared ProtocolAuthorityProcessor.PollAuthority exactly once
    broadcast returned authority
return unique authoritative payload count
~~~

Each authority batch is fully framed into local candidate arrays before any byte from that batch is written. Broadcast order is frame-major then PlayerSlot-minor. A later write failure preserves earlier bytes and authority, returns no partial count, sticky-faults the whole session, and rethrows. EOF, parse, framing, spoof, authority, read, and write failures are also whole-session sticky failures. Dispose is idempotent.

Predicted runtime constructor validation order:

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

Remote prediction uses a Slot-indexed InputFrame?[] cache. Before a remote Slot has reconciled authority, MoveX=0, MoveZ=0, and Aim equals the initial PlayerState Aim. Future predictions repeat only the latest successfully reconciled authoritative remote input. Already retained predicted FrameData never changes.

Update priority:

~~~text
disposed
sticky fault
receive or backlog-first bounded reconciliation
optional one-sample prediction
independent result
~~~

If pending authority exists at Update start, perform no network read and reconcile at most MaxAuthoritativeFramesPerUpdate oldest frames. Otherwise perform at most one transport-only receive, atomically admit the complete decoded batch, and reconcile the same bounded prefix.

Authority admission requires:

~~~text
Pending.Count + Candidate.Count <= MaxPendingAuthoritativeFrames
Replay.Count + Pending.Count + Candidate.Count <= MaxReplayFrames
~~~

using widened arithmetic. Roster and contiguous Tick checks occur before one candidate-container replacement. Failure admits and reconciles zero candidates, does not drop/evict/grow, sticky-faults, and throws.

Each authority reconciliation builds candidate Replay, remote cache, and pending containers, calls ClientPredictionTimeline.ReconcileAuthoritative, and commits containers only after success. Earlier successfully reconciled frames stay committed if a later frame fails. The failed frame changes no runtime-owned container; no partial result is returned; the runtime sticky-faults.

After authority work, null sample, terminal predicted Tick, or full prediction capacity creates no prediction and sends nothing. Otherwise Update creates exactly one complete predicted frame at current P, calls Timeline.Predict, and sends only the local InputFrame. Prediction commits before send. Send failure preserves it, sticky-faults, and returns no result.

Replay retains the immutable initial BattleState plus successfully reconciled authoritative FrameData in strict Tick order. It never evicts and cannot exceed MaxReplayFrames. ReconstructAuthoritativeState locally replays all retained authority and compares complete state and Digest with the live authoritative state. Mismatch sticky-faults and throws without repair.

uint.MaxValue - 1 is the final consumable frame Tick; uint.MaxValue is terminal state Tick. No wrap, terminal prediction, or post-terminal authority is allowed.

## Exact test-only reflection contracts

Test 25 accesses the two frozen internal TcpClientBattlePump members from the separate Gate 13 test assembly through reflection only:

~~~text
BindingFlags.Static | BindingFlags.NonPublic
CreateTransportOnly with the exact six-parameter signature

BindingFlags.Instance | BindingFlags.NonPublic
PumpReceiveTransportOnlyOnce with zero parameters
~~~

The fixture invokes those unique internal members and proves that the returned authoritative payload is mapped against expectedRoster, transport-only mode owns no persistent BattleSimulation, and the receive operation never calls BattleSimulation.Step. Test 24 separately proves the legacy public mode still owns and Steps its Simulation. Do not add InternalsVisibleTo, widen either member to public, add a production test hook, or create another test-helper file.

Where exact authority maturity matters, Gate 13 shared-session and Golden tests use an independently implemented test-only reflection fixture in the already-approved Gate 13 test files. The fixture:

~~~text
locates the unique private ProtocolAuthorityProcessor in TcpSharedBattleSession by field type
locates the unique private StopwatchTickDriver in that processor by field type
locates the unique private Stopwatch and long baseline field in that driver by field type
stops the Stopwatch
captures stable Stopwatch.ElapsedTicks
computes the elapsed ticks needed for the requested logical Advances with UInt128 and SimulationConfig.TickRate
sets only the test driver's private baseline so the next PollAuthority performs that deterministic maturity
~~~

Use the same arithmetic principle already approved by Gate 11:

~~~text
requiredElapsed =
ceil(Stopwatch.Frequency * dueAdvances / SimulationConfig.TickRate)
~~~

The helper is Gate 13 test code only and has this test-assembly-internal signature in TcpSharedBattleSessionTests.cs:

~~~csharp
internal static void ForceNextPollAdvances(
    TcpSharedBattleSession session,
    uint dueAdvances);
~~~

It must verify that every reflected field-type match is unique. Do not copy, link, or compile the Gate 11 helper source. Do not add a production clock hook, InternalsVisibleTo, public injection API, Task, Thread, async operation, Thread.Sleep correctness dependency, wall-clock busy-wait oracle, or second timing implementation. The external watchdog remains only a hung-process guard and is never the correctness clock.

## Frozen Golden data

Correct Golden roster:

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

Expected consumer-only result:

~~~text
State101
Slot0 X=-200 Z=0 Aim=10100
Slot1 X= 300 Z=0 Aim=2000
Digest 0x5DB198E1CB8F8ED4
Dirty count 0
A=P=101
Replay count 1
~~~

Wrong-prediction authority:

~~~text
Tick100 Slot0  1, 0, 10100   Slot1 -1, 0, 20100
Tick101 Slot0  0, 1, 10101   Slot1  0,-1, 20101
Tick102 Slot0 -1, 0, 10102   Slot1  1, 0, 20102
Tick103 Slot0  0,-1, 10103   Slot1 -1, 0, 20100
~~~

Exact application-level schedule:

~~~text
1. Client A predicts/sends Slot0 100, 101, 102 without authority
2. Client B sends only Slot1 100
3. session publishes 100 while 101/102 remain incomplete
4. Client A reconciles 100 Dirty
5. Client A predicts 103 using reconciled Slot1 input from 100
6. Client B sends Slot1 101, 102, 103
7. session publishes and broadcasts 101, 102, 103
8. Client A reconciles at most one authority per Update
9. Client B consumes the same authority
10. Replay reconstructs initial state plus authority 100..103
~~~

Consumer-only Digests:

~~~text
Authority State101  0xA64F91C7685696AC
Authority State102  0x8763814FEEAFD898
Authority State103  0x3C3A2EFAE2A06B79
Authority State104  0xC3CAAFCE96D7F832

Pre-authority predicted State103              0x8B8B6B468244E4C7
After Dirty 100 replay, A101/P103             0xEDA7579F3B1F2A20
After prediction 103 using authority 100      0x40EADCE076E81EFD
After Dirty 101, A102/P104                    0xFC6C81B3C49F4B1E
After Dirty 102, A103/P104                    0xC3CAAFCE96D7F832
~~~

Dirty sequence: true, true, true, false.

Final:

~~~text
A=P=104
pending prediction=0
pending authority=0
Replay count=4
Slot0 X=-300 Z=0    Aim=10103
Slot1 X= 200 Z=-100 Aim=20100
Server, Client A authoritative, Client A predicted, Client B, and Replay Digest
= 0xC3CAAFCE96D7F832
~~~

Weak-network schedules:

~~~text
Run A: server read=3, predicted client read=5, second client read=7
Run B: server read=1, predicted client read=2, second client read=3
~~~

Logical inputs and application-level withholding phases are identical. No assertion depends on an individual TCP read size.

## Exact Gate 13 test registry

The dependency-free runner registers exactly:

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

## Task 1: Add participant bindings and shared-session construction

**Files:**

- Create: Server/LockstepArena.Server.LiveTcp/TcpBattleParticipantBinding.cs
- Create: Server/LockstepArena.Server.LiveTcp/TcpSharedBattleSession.cs
- Modify: Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj
- Create: Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj
- Create: Tests/LockstepArena.LivePrediction.Tests/Program.cs
- Create: Tests/LockstepArena.LivePrediction.Tests/TestAssert.cs
- Create: Tests/LockstepArena.LivePrediction.Tests/TcpSharedBattleSessionTests.cs
- Modify: .gitignore

### Step 1: Verify the approved implementation start

~~~powershell
$branch = 'codex/gate13-live-client-prediction'
$base = 'ff9a1a0010daecf2727096c063d62e92575259df'
$remote = ((git ls-remote origin "refs/heads/$branch") -split [char]9)[0]
if ((git branch --show-current) -ne $branch) { throw 'Wrong Gate 13 branch.' }
if ((git rev-parse HEAD) -ne $remote) { throw 'Implementation must start from approved remote Planning HEAD.' }
if ((git rev-parse HEAD^) -ne $base) { throw 'Planning parent is not the frozen Gate 12 baseline.' }
git merge-base --is-ancestor $base HEAD
if ($LASTEXITCODE -ne 0) { throw 'Frozen baseline is not an ancestor.' }
if ((git status --porcelain).Length -ne 0) { throw 'Gate 13 worktree must be clean.' }
~~~

Verify the ordinary checkout contains exactly the two protected user changes.

### Step 2: Write Tests 1-13

Create the exact project contract, runner, assertion helper, and shared-session constructor tests. Add only the approved .gitignore exception.

### Step 3: Prove RED

~~~powershell
dotnet build Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
~~~

Expected: compilation fails only because the two new server types are missing.

### Step 4: Implement the minimum construction boundary

Implement the immutable binding and session constructor through final ownership transfer. Construct exactly one ProtocolAuthorityProcessor. Add only the Protocol ProjectReference to Server LiveTcp. Do not implement final PumpOnce networking behavior yet.

### Step 5: Prove GREEN

~~~powershell
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release
~~~

Required:

- Gate 13 interim RESULT 13/13 passed
- Gate 11 RESULT 32/32 passed

### Step 6: Audit and commit

Confirm one processor, defensive copy, exact validation order, no I/O before ownership transfer, and no connection disposal on failed construction.

~~~powershell
git add -- .gitignore Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj Server/LockstepArena.Server.LiveTcp/TcpBattleParticipantBinding.cs Server/LockstepArena.Server.LiveTcp/TcpSharedBattleSession.cs Tests/LockstepArena.LivePrediction.Tests
git diff --cached --check
git commit -m "feat: add shared live battle session boundary"
~~~

## Task 2: Implement shared fan-in, authority Poll, and broadcast

**Files:**

- Modify: Server/LockstepArena.Server.LiveTcp/TcpSharedBattleSession.cs
- Modify: Tests/LockstepArena.LivePrediction.Tests/TcpSharedBattleSessionTests.cs
- Modify: Tests/LockstepArena.LivePrediction.Tests/Program.cs

### Step 1: Write Tests 14-23

Add the exact real-client fan-in, anti-spoof, slot-order, Poll, broadcast, EOF, and sticky-fault tests. In TcpSharedBattleSessionTests.cs independently implement the frozen test-only ForceNextPollAdvances reflection fixture. Use it whenever an assertion requires an exact next-Poll logical maturity; do not wait for real elapsed wall-clock time.

### Step 2: Prove RED

~~~powershell
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
~~~

Expected: Tests 14-23 fail because the construction-only session does not implement the final PumpOnce transaction.

### Step 3: Implement minimal final PumpOnce

Use per-participant decoder/buffer state, at most one bounded readable read per participant, direct valid-segment Feed, binding validation before shared submission, input-produced authority broadcast before exactly one PollAuthority, and frame-major/slot-minor writes. Pre-frame each returned batch before writing it. Return the unique authority payload count only after every write succeeds. Implement whole-session sticky fail-stop and idempotent Dispose.

### Step 4: Prove GREEN

~~~powershell
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj -c Release
~~~

Required:

- Gate 13 interim RESULT 23/23 passed
- Gate 11 RESULT 32/32 passed
- Gate 6 RESULT 24/24 passed

### Step 5: Audit and commit

Confirm exact Slot order, one shared processor, no second Simulation or scheduler, no swallowed input, and no rollback/retry/reconnect.

~~~powershell
git add -- Server/LockstepArena.Server.LiveTcp/TcpSharedBattleSession.cs Tests/LockstepArena.LivePrediction.Tests/TcpSharedBattleSessionTests.cs Tests/LockstepArena.LivePrediction.Tests/Program.cs
git diff --cached --check
git commit -m "feat: fan in and broadcast shared battle authority"
~~~

## Task 3: Extract transport-only client receive

**Files:**

- Modify: Client/LockstepArena.Client.LiveTcp/TcpClientBattlePump.cs
- Create: Tests/LockstepArena.LivePrediction.Tests/PredictedTcpClientBattleRuntimeTests.cs
- Modify: Tests/LockstepArena.LivePrediction.Tests/Program.cs

### Step 1: Write Tests 24-25

Test the new internal transport-only creation/receive path and protect the legacy Gate 11 Simulation-stepping path. Because the Gate 13 tests are a separate assembly, Test 25 must invoke the unique internal CreateTransportOnly and PumpReceiveTransportOnlyOnce members through test-only reflection with their exact frozen signatures. It must enumerate the transport-only pump's private instance fields and prove none has type BattleSimulation, while the returned mapped frame and unchanged state prove no Step occurred.

### Step 2: Prove RED

~~~powershell
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
~~~

Expected: Test 25 fails because transport-only mapping without persistent Simulation is absent; Test 24 protects existing behavior.

### Step 3: Implement the minimum extraction

Add the exact two internal members. Both modes reuse one private receive/decode/map path. Transport-only mode owns no Simulation and never calls Step. Keep both new members internal; Test 25 reaches them only through reflection. Add no InternalsVisibleTo, public switch, public test hook, interface, callback, or background loop.

### Step 4: Prove GREEN

~~~powershell
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release
~~~

Required:

- Gate 13 interim RESULT 25/25 passed
- Gate 11 RESULT 32/32 passed

### Step 5: Audit and commit

Confirm legacy public API/behavior is compatible, transport-only mode has no Simulation, mapping still enforces expectedRoster, and TcpServerBattlePump.cs is unchanged.

~~~powershell
git add -- Client/LockstepArena.Client.LiveTcp/TcpClientBattlePump.cs Tests/LockstepArena.LivePrediction.Tests/PredictedTcpClientBattleRuntimeTests.cs Tests/LockstepArena.LivePrediction.Tests/Program.cs
git diff --cached --check
git commit -m "refactor: add transport-only client receive mode"
~~~

## Task 4: Add live predicted runtime core

**Files:**

- Create: Client/LockstepArena.Client.LiveTcp/PredictedTcpClientBattleRuntime.cs
- Modify: Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj
- Modify: Tests/LockstepArena.LivePrediction.Tests/PredictedTcpClientBattleRuntimeTests.cs
- Modify: Tests/LockstepArena.LivePrediction.Tests/Program.cs

### Step 1: Write Tests 26-35

Cover identity/capacity validation, authority-before-prediction ordering, one prediction/send, capacity no-send, send-failure preservation, neutral remote seed, newest reconciled remote input, immutable retained predictions, and consecutive Dirty deterministic replay.

### Step 2: Prove RED

~~~powershell
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
~~~

Expected: Tests 26-35 fail only because PredictedTcpClientBattleRuntime and its value types do not exist.

### Step 3: Implement the minimum runtime

Implement exact API and constructor priority. Own one ClientPredictionTimeline and one transport-only pump. Add only the ClientPrediction ProjectReference. Build complete canonical predictions with local sample and the Slot-indexed remote cache. Preserve prediction-before-send and sticky send failure. Reuse Gate 12 reconciliation and never regenerate retained predictions.

### Step 4: Prove GREEN

~~~powershell
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release
~~~

Required:

- Gate 13 interim RESULT 35/35 passed
- Gate 12 RESULT 36/36 passed
- Gate 11 RESULT 32/32 passed

### Step 5: Audit and commit

Confirm the four capacities remain independent, only one prediction Timeline exists, no neutral/repeat-last policy is added beyond the frozen remote seed/cache rule, and no client wall-clock or second Simulation exists.

~~~powershell
git add -- Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj Client/LockstepArena.Client.LiveTcp/PredictedTcpClientBattleRuntime.cs Tests/LockstepArena.LivePrediction.Tests/PredictedTcpClientBattleRuntimeTests.cs Tests/LockstepArena.LivePrediction.Tests/Program.cs
git diff --cached --check
git commit -m "feat: add bounded live client prediction"
~~~

## Task 5: Add bounded authority admission and Replay

**Files:**

- Modify: Client/LockstepArena.Client.LiveTcp/PredictedTcpClientBattleRuntime.cs
- Modify: Tests/LockstepArena.LivePrediction.Tests/PredictedTcpClientBattleRuntimeTests.cs
- Modify: Tests/LockstepArena.LivePrediction.Tests/Program.cs

### Step 1: Write Tests 36-44 and 49

Cover backlog-first receive suppression, per-update catch-up bound, exact-capacity atomic batch admission, over-capacity zero admission, invalid roster/Tick zero mutation, reservation accounting, capacity rejection, no Replay eviction/growth, reconstruction, and reflection-only Replay-corruption sticky fail-stop.

### Step 2: Prove RED

~~~powershell
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
~~~

Expected: new tests fail because candidate admission, bounded queue/Replay, reconstruction, and corruption fail-stop are incomplete.

### Step 3: Implement the minimum bounded authority path

Implement widened capacity checks, full batch validation before one pending-container replacement, candidate-before-commit reconciliation, non-evicting Replay, and local deterministic reconstruction. Test 49 may locate the unique private Replay container by type; do not expose or rename private state for the test.

### Step 4: Prove GREEN

~~~powershell
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release
~~~

Required:

- Gate 13 interim RESULT 45/45 passed, comprising Tests 1-44 plus Test 49
- Gate 12 RESULT 36/36 passed

### Step 5: Audit and commit

Confirm no silent drop or Replay eviction, no unbounded container, no partial batch admission, no production test hook, and no recovery/reset/retry.

~~~powershell
git add -- Client/LockstepArena.Client.LiveTcp/PredictedTcpClientBattleRuntime.cs Tests/LockstepArena.LivePrediction.Tests/PredictedTcpClientBattleRuntimeTests.cs Tests/LockstepArena.LivePrediction.Tests/Program.cs
git diff --cached --check
git commit -m "feat: retain bounded authoritative replay"
~~~

## Task 6: Prove correct and wrong prediction over real shared TCP

**Files:**

- Create: Tests/LockstepArena.LivePrediction.Tests/Gate13LivePredictionGoldenVector.cs
- Create: Tests/LockstepArena.LivePrediction.Tests/Gate13WeakNetworkGoldenTests.cs
- Modify: Tests/LockstepArena.LivePrediction.Tests/Program.cs

### Step 1: Write Tests 45-48 and finalize exact registry order

The actual-only vector independently creates its roster, state, inputs, clients, shared session, predicted runtime, schedules, and actual results. It must not compile, link, or copy earlier test helpers or contain expected state/Digest/pass/fail values. Reuse the single Gate 13 test-only ForceNextPollAdvances fixture from TcpSharedBattleSessionTests.cs to arrange every exact server authority maturity in both Golden schedules.

### Step 2: Prove RED

~~~powershell
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
~~~

Expected: Tests 45-48 fail because the real shared-session Golden is absent.

### Step 3: Implement the minimum Goldens

Implement both frozen weak-network schedules with the same logical inputs and application-level withholding phases. Stop and control only the reflected test Stopwatch/baseline before the relevant shared-session Polls; do not use Thread.Sleep, wall-clock maturity waits, or the external watchdog as a correctness oracle. Prove clean reconciliation, the exact Dirty sequence, bounded one-frame-per-Update catch-up in the wrong case, terminal no-wrap, identical authority/Replay/final state between schedules, and final authoritative/predicted/reconstructed Digest C3CAAFCE96D7F832.

### Step 4: Prove GREEN with an external watchdog

Run Gate 13 with a 90-second process watchdog. The watchdog may detect and terminate a hung test executable; it is not socket, gameplay, missing-input, or product timeout behavior.

Required:

- Gate 13 RESULT 49/49 passed
- named tests 45-49 pass
- correct Golden Digest 5DB198E1CB8F8ED4
- wrong Golden Digests and states exactly match the frozen consumer literals
- final State Tick 104 and Digest C3CAAFCE96D7F832

### Step 5: Focused regression, audit, and commit

~~~powershell
dotnet run --project Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release
git diff --check
~~~

Confirm Gate 11 32/32, Gate 12 36/36, no expected literal in the actual vector, and no TCP read-size assertion.

~~~powershell
git add -- Tests/LockstepArena.LivePrediction.Tests/Gate13LivePredictionGoldenVector.cs Tests/LockstepArena.LivePrediction.Tests/Gate13WeakNetworkGoldenTests.cs Tests/LockstepArena.LivePrediction.Tests/Program.cs
git diff --cached --check
git commit -m "test: prove live prediction under weak network schedules"
~~~

## Task 7: Fresh final verification and evidence

**Files:**

- Modify only after all verification passes: Docs/Architecture/GATE13_TCP_LIVE_CLIENT_PREDICTION.md

### Step 1: Restore-assets preflight

Inspect all build projects for existing restore assets. If one is absent, restore that existing project without changing any package/version contract, then restart the complete build matrix from build 1.

### Step 2: Run exactly 21 sequential Release builds

Each must report 0 warnings and 0 errors.

~~~powershell
dotnet build Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj -c Release --no-restore
dotnet build Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj -c Release --no-restore
dotnet build Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj -c Release --no-restore
dotnet build Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release --no-restore
dotnet build Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release --no-restore
dotnet build Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj -c Release --no-restore
dotnet build Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj -c Release --no-restore
dotnet build Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj -c Release --no-restore
dotnet build Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj -c Release --no-restore
dotnet build Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj -c Release --no-restore
dotnet build Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj -c Release --no-restore
dotnet build Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj -c Release --no-restore
dotnet build Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj -c Release --no-restore
dotnet build Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj -c Release --no-restore
dotnet build Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj -c Release --no-restore
dotnet build Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj -c Release --no-restore
dotnet build Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj -c Release --no-restore
dotnet build Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release --no-restore
dotnet build Packages/com.locksteparena.client-prediction/Runtime/LockstepArena.Client.Prediction.csproj -c Release --no-restore
dotnet build Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release --no-restore
dotnet build Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release --no-restore
~~~

### Step 3: Run Gate 3-13 .NET regressions

~~~powershell
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release --no-build
dotnet run --project Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj -c Release --no-build
~~~

Require:

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

Run Gate 8, Gate 11, and Gate 13 real-TCP suites under their existing external watchdog bounds. No watchdog becomes product timeout behavior.

### Step 4: Regenerate Protocol with the pinned tool

Confirm no PROTOBUF_PROTOC or Protobuf_ProtocFullPath override. Record Grpc.Tools 2.83.0, bundled protoc path/version, exactly one generated .g.cs, and require:

~~~powershell
git diff --exit-code -- Packages/com.locksteparena.protocol/Schema Packages/com.locksteparena.protocol/Runtime/Generated
~~~

### Step 5: Run fresh Unity 6000.3.10f1 regressions

Use Start-Process -Wait, batchmode, nographics, projectPath, runTests, EditMode, frozen assembly/filter, fresh XML, and log file. Do not use -quit. Process exit code alone is not PASS.

Require fresh XML:

- Client Prediction: total=2, passed=2, failed=0
  - UnityClientPredictionGoldenTests.UnityExecutesCorrectPredictionGolden
  - UnityClientPredictionGoldenTests.UnityExecutesDirtyRollbackGolden
- Stream Framing: total=1, passed=1, failed=0
  - UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden
- Protocol: total=2, passed=2, failed=0
  - GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads
  - UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector
- Simulation with filter UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector:
  - total at least 1
  - failed=0
  - required named test Passed

After each run inspect exact worktree-local Assets/ProjectSettings changes and restore only individually confirmed Unity-generated paths. Never use broad reset or clean.

### Step 6: Final protected-boundary audits

Relative to ff9a1a0010daecf2727096c063d62e92575259df require committed diff zero for:

- Packages/com.locksteparena.simulation
- Packages/com.locksteparena.protocol
- Packages/com.locksteparena.stream-framing
- Packages/com.locksteparena.client-prediction
- Server/LockstepArena.Server.FrameSync
- Server/LockstepArena.Server.ProtocolAuthority
- Server/LockstepArena.Server.LiveTcp/TcpServerBattlePump.cs
- all existing Gate 3-12 test projects and Golden vectors
- Assets
- ProjectSettings
- Packages/manifest.json
- Packages/packages-lock.json

Require only these existing production edits:

- TcpClientBattlePump.cs internal transport-only extraction
- Client LiveTcp ClientPrediction ProjectReference
- Server LiveTcp Protocol ProjectReference

Also prove:

- .gitignore has exactly the approved Gate 13 exception.
- One shared server processor/Simulation and one client prediction Timeline.
- Authority/pending/Replay containers are bounded; Replay does not evict.
- Anti-spoof validation occurs before processor submission.
- No silent input/frame drop, copied earlier Golden, production test hook, tracked build artifact, symlink/junction, or copy/sync script.
- Embedded packages contain no bin, obj, or LockstepArena build DLL.
- No KCP/UDP, transport switching, Login/Room/Ready/Host/Settlement, reconnect, heartbeat, retry, AI takeover, server neutral/repeat-last input, adaptive delay, client clock sync, async/Task/Thread networking, periodic Snapshot, product Replay, interpolation, render smoothing, generic framework, DI, or EventBus.
- Gate13LivePredictionGoldenVector.cs contains no expected state/Digest/pass/fail literals.
- git diff --check passes.
- The ordinary checkout still has exactly the two protected user changes.

### Step 7: Append and commit implementation evidence

Only after all fresh checks pass, append an Implementation Evidence section to the Architecture document. Record:

- implementation commit chain;
- 21 builds with 0 warnings/errors;
- Gate 3-13 suite counts;
- Gate 3 Server Golden;
- Gate 13 correct/wrong Golden states, Dirty sequence, Replay, and final Digest;
- Gate 8/11/13 watchdog outcomes;
- pinned regeneration provenance/diff;
- four Unity XML summaries and named tests;
- protected-boundary, dependency, artifact, source uniqueness, worktree, and ordinary-checkout audits.

Do not change the approved contract.

~~~powershell
git add -- Docs/Architecture/GATE13_TCP_LIVE_CLIENT_PREDICTION.md
git diff --cached --check
git diff --cached --name-only
git commit -m "docs: record Gate 13 implementation evidence"
~~~

The staged file list must contain only the Architecture document.

### Step 8: Push and hand off

Use only a normal fast-forward push:

~~~powershell
git push origin codex/gate13-live-client-prediction
~~~

If direct access fails, use only the approved per-command proxy without persistent Git configuration:

~~~powershell
git -c http.proxy=http://127.0.0.1:7897 push origin codex/gate13-live-client-prediction
~~~

Require remote SHA equal to local final HEAD, frozen base ancestor/0 behind, clean Gate 13 worktree, and the unchanged ordinary checkout. Submit the Gate 13 Final Implementation Handoff and STOP.

## Planning self-review

- The Plan and Architecture use identical APIs, constructor order, capacities, session return semantics, and failure rules.
- The project references are exactly the four frozen direct references.
- All exact 49 test names match the Architecture and are assigned to a RED/GREEN task.
- Interim counts are 13, 23, 25, 35, 45, and 49 without deleting a test.
- Expected state/Digest literals remain consumer-test-only.
- The actual Golden owns setup/results only.
- Final verification has exactly 21 Release builds and Gate 3-13 executions.
- Only the three frozen existing production/project edits are permitted.
- No unresolved choice or deferred implementation remains.
- The final action is the Gate 13 handoff and STOP before Gate 14.
