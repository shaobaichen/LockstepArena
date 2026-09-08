# Gate 11: Minimal Live Network Battle Runtime Composition

## Status and baseline

- Frozen Gate 10 base: `4b46f241fe2a98a84841264da7ee50720b425a08`
- Direction: TCP-only single-threaded live battle pump composition
- Scope: synchronous caller-driven live TCP composition only
- KCP and UDP remain deferred.

## 1. Learning objective

Gate 11 proves that real client input and real server Stopwatch pacing can
coexist in one continuously pumped battle without creating a second authority
timeline:

```text
caller-provided PlayerId + InputFrame
-> Protocol protobuf
-> StreamFraming
-> real TCP
-> server incremental decode
-> scheduled ProtocolAuthorityProcessor
-> one TickDrivenFramePublisher <- one StopwatchTickDriver
-> authoritative FrameData
-> server BattleSimulation
-> Protocol protobuf / StreamFraming / real TCP
-> client incremental decode / ProtocolMapper
-> client BattleSimulation
-> equal deterministic state
```

## 2. Production assemblies and dependencies

Gate 11 adds two .NET 8 production assemblies.

`Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <BuildInParallel>false</BuildInParallel>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\LockstepArena.Server.ProtocolAuthority\LockstepArena.Server.ProtocolAuthority.csproj" />
    <ProjectReference Include="..\..\Packages\com.locksteparena.stream-framing\Runtime\LockstepArena.StreamFraming.csproj" />
    <ProjectReference Include="..\..\Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj" />
  </ItemGroup>
</Project>
```

`Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <BuildInParallel>false</BuildInParallel>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Google.Protobuf" Version="3.36.0" />
    <ProjectReference Include="..\..\Packages\com.locksteparena.stream-framing\Runtime\LockstepArena.StreamFraming.csproj" />
    <ProjectReference Include="..\..\Packages\com.locksteparena.protocol\Runtime\LockstepArena.Protocol.csproj" />
    <ProjectReference Include="..\..\Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj" />
  </ItemGroup>
</Project>
```

The Server TCP assembly does not depend directly on Protocol or FrameSync
because its source consumes the Processor's byte-oriented API. The Client
assembly has no Server dependency. No common transport assembly is added.

## 3. ProtocolAuthorityProcessor modes

The existing legacy constructor remains unchanged:

```csharp
public ProtocolAuthorityProcessor(
    BattleState initialState,
    uint maxFutureTickOffset,
    int authoritativeHistoryCapacity);
```

Legacy mode owns one `AuthoritativeFrameCoordinator` and one
`BattleSimulation`. It preserves Gate 6 immediate continuous publication.

The scheduled overload is:

```csharp
public ProtocolAuthorityProcessor(
    BattleState initialState,
    uint inputDelayTicks,
    uint maxFutureTickOffset,
    int authoritativeHistoryCapacity);
```

Scheduled mode owns one `TickDrivenFramePublisher`, one
`StopwatchTickDriver` operating on that Publisher, and one
`BattleSimulation`. The Publisher remains the sole owner of its internal
Coordinator. No instance owns two active authority timelines, and mode cannot
change after construction.

Public API:

```csharp
public BattleState ServerState { get; }
public uint NextPublishTick { get; }
public byte[][] SubmitPlayerInputPayload(byte[] completePayload);
public byte[][] PollAuthority();
```

`CollectionTick` and `EligibilityCeiling` are not duplicated through this API.
Legacy `PollAuthority()` throws `InvalidOperationException` without mutation
or fault transition.

Scheduled constructor validation/creation order is:

```text
initialState null
-> construct BattleSimulation
-> construct TickDrivenFramePublisher using the frozen Gate 9 validation
-> construct StopwatchTickDriver last
-> return without a logical Advance
```

No exception is wrapped and no additional relationship between InputDelay and
the Gate 4 future window is introduced.

## 4. Shared authority publication path

Legacy Submit, scheduled Submit, and scheduled Poll use one private path:

```text
FrameData[] in authority order
-> BattleSimulation.Step each Frame
-> ProtocolMapper.ToWire
-> ToByteArray
-> fresh byte[][]
```

An empty publication returns `Array.Empty<byte[]>()`. One Frame creates one
independent payload; N Frames create N independent payloads in the same order.
There is no wire batch, envelope, opcode, or router. The outer array and each
payload buffer are independently owned.

The complete batch must succeed before it is returned. A failure returns no
partial array. Authority publication and earlier successful Simulation Steps
are not rolled back.

## 5. Processor fail-stop contract

Both public operations check `_faulted` first.

Submit parse, mapping, or transactional authority rejection before publication
preserves its original exception and does not fault the Processor.

Any exception escaping scheduled `StopwatchTickDriver.Poll()` faults the
Processor and rethrows the original exception because logical Advances may
already be committed. Simulation or serialization failure after a nonempty
publication also faults, rethrows, returns no partial output, and preserves
earlier publication/Step effects.

After fault, Submit and Poll immediately throw `InvalidOperationException`.
There is no retry, reset, replacement Publisher, or recovery path.

## 6. Server TCP battle pump

```csharp
namespace LockstepArena.Server.LiveTcp;

public sealed class TcpServerBattlePump : IDisposable
{
    public TcpServerBattlePump(
        TcpClient connectedClient,
        BattleState initialState,
        uint inputDelayTicks,
        uint maxFutureTickOffset,
        int authoritativeHistoryCapacity,
        int maxPayloadLength,
        int receiveBufferLength,
        int receiveOffset,
        int receiveReadCapacity);

    public BattleState ServerState { get; }
    public uint NextPublishTick { get; }
    public int PumpOnce();
    public void Dispose();
}
```

It owns the connected `TcpClient`, its `NetworkStream`, one framing decoder,
one scheduled Processor, its own receive array and layout values, plus disposed
and sticky-fault flags. It does not own a listener.

Constructor validation/creation order:

```text
connectedClient null
-> initialState null
-> receiveBufferLength >= 1
-> receiveOffset >= 0
-> receiveReadCapacity >= 1
-> receiveOffset <= receiveBufferLength - receiveReadCapacity
-> construct decoder to validate MaxPayloadLength
-> require IPv4 AddressFamily.InterNetwork
-> require an already-connected client and obtain NetworkStream
-> allocate receive array
-> construct scheduled Processor last, starting its Stopwatch baseline
-> ownership transfers only after successful construction
```

The layout comparison uses subtraction, not unchecked addition.

## 7. Server PumpOnce

Operation validation/order:

```text
disposed
-> sticky fault
-> Socket.Poll(0, SelectRead)
-> optional one bounded NetworkStream.Read
-> decoder Feed(buffer, offset, bytesRead)
-> process every complete submission from that Read in wire order
-> immediately frame/write Submit-produced authority payloads
-> ProtocolAuthorityProcessor.PollAuthority exactly once
-> frame/write Poll-produced authority payloads
-> return successful authoritative-message write count
```

If Poll reports no readable state, no Read occurs and authority Poll still
occurs exactly once. If Poll reports readable and `Socket.Available == 0`, or
if Read returns zero, `EndOfStreamException` is thrown before authority Poll.
At most one Read occurs per call. Only the valid read segment is fed to the
decoder; no exact-sized input array is created.

The conclusive one-Read test uses receive capacity 3 and queues exactly the
four-byte big-endian zero-length prefix `00 00 00 00`. A bounded test-only
readiness loop first requires `Socket.Available >= 4`. One correct Read can
consume at most three prefix bytes, so no submission completes and the Pump
returns normally. An incorrect second Read necessarily consumes the final
prefix byte, completes a zero-length payload, and reaches the observable
protobuf/Mapper rejection path. The test asserts no individual Read size and
adds no production I/O hook.

One Read may produce zero, one, or multiple submissions. All are processed
before the Tick Poll. Submit-produced authority is written before Poll-produced
authority from the same call.

Each payload is independently framed and passed to one synchronous
`NetworkStream.Write`. The successful count increments only after the complete
framed buffer's Write returns successfully. A later failure may leave earlier
writes committed but returns no partial count.

The readiness/read path is bounded, but synchronous Write may block under TCP
backpressure. Gate 11 does not claim `PumpOnce()` is wholly non-blocking.

Any escaping EOF, framing, parse, mapping, authority, Simulation,
serialization, or socket error permanently faults the pump and propagates the
original exception. Later operations allow Dispose only.

## 8. Client TCP battle pump

```csharp
namespace LockstepArena.Client.LiveTcp;

public sealed class TcpClientBattlePump : IDisposable
{
    public TcpClientBattlePump(
        TcpClient connectedClient,
        BattleState initialState,
        int maxPayloadLength,
        int receiveBufferLength,
        int receiveOffset,
        int receiveReadCapacity);

    public BattleState ClientState { get; }
    public void SendInput(PlayerId submittedPlayerId, InputFrame input);
    public FrameData[] PumpReceiveOnce();
    public void Dispose();
}
```

It owns the connected client/stream, decoder, one client Simulation, receive
array/layout, MaxPayloadLength, disposed flag, and sticky-fault flag. It does
not create input Ticks and never performs speculative Simulation Steps.

Constructor validation order is identical to the Server through connected
stream acquisition, then it constructs `BattleSimulation` and transfers
ownership. It does not start a clock.

`SendInput` order:

```text
disposed
-> sticky fault
-> ProtocolMapper.ToWire(PlayerId, InputFrame)
-> ToByteArray
-> LengthPrefixedFrameEncoder.Encode
-> one synchronous NetworkStream.Write
```

Mapping, serialization, or framing rejection before Write does not fault. A
Write failure faults and rethrows.

`PumpReceiveOnce` order:

```text
disposed
-> sticky fault
-> zero-duration readiness / EOF check
-> optional one bounded Read
-> decoder Feed exact segment
-> for each authoritative payload in wire order:
     parse AuthoritativeFrameMessage
     ProtocolMapper.ToDomain(message, ClientState.Roster)
     BattleSimulation.Step
-> fresh FrameData[]
```

No readable data or no complete frame returns `Array.Empty<FrameData>()`.
Failure after data consumption preserves earlier Steps, returns no partial
array, faults the pump, and rethrows.

## 9. Pump disposal and lifecycle

Both pump operation APIs validate disposed state before sticky fault. Calls
after Dispose throw `ObjectDisposedException`. Dispose is idempotent, closes
the owned stream/client, and remains permitted after fault.

The external caller creates/listens/connects/accepts TCP. After accept, it
supplies structurally equal initial BattleStates and constructs the two pumps.
The Server Processor's final construction starts the Stopwatch baseline but
does not advance time. The caller repeatedly invokes Server and Client pumps.

Normal completion is caller-owned Dispose after the required authority
sequence. EOF before completion is failure. There is no handshake, half-close,
battle-end packet, connection manager, reconnect, or graceful shutdown
protocol.

## 10. Mature incomplete frames

Eligibility may advance while `NextPublishTick` is incomplete. Poll then sends
nothing and does not Step Server Simulation. A later valid Submit can release
the already-mature continuous prefix through the same Publisher. Gate 11 adds
no neutral input, timeout, repeat-last, prediction, or dropped-frame rule.

## 11. Deterministic reflection fixture

Gate 11 tests may use one test-only reflection fixture. It locates fields by
unique field type rather than private field name:

```text
ProtocolAuthorityProcessor -> unique TickDrivenFramePublisher
ProtocolAuthorityProcessor -> unique StopwatchTickDriver
StopwatchTickDriver -> unique Stopwatch and unique long baseline field
TickDrivenFramePublisher -> unique AuthoritativeFrameCoordinator
Coordinator -> unique Dictionary<uint, StrictFrameCollector>
```

To make exactly N Advances due without sleeping, the fixture first calls
`stopwatch.Stop()` on the reflected Stopwatch and captures its now-stable
`stableElapsed = stopwatch.ElapsedTicks`. With `F = Stopwatch.Frequency`, it
uses `UInt128` ceiling arithmetic to compute:

```text
requiredElapsed = ceil(F * N / SimulationConfig.TickRate)
                = (UInt128(F) * N + TickRate - 1) / TickRate
baseline = stableElapsed - checked((long)requiredElapsed)
```

It writes that checked bounded baseline into the unique reflected `long`
field. Because the Stopwatch remains stopped, Poll reads exactly
`stableElapsed`, so its elapsed delta is exactly `requiredElapsed`. A successful
Poll commits its baseline to that same stopped value, and a later Poll without
another fixture call observes exactly zero elapsed delta.

`ScheduledPollPublishesAtExactMaturity`,
`PollFailureFaultsAndRethrowsOriginal`,
`ServerPumpNoReadableBytesStillPollsExactlyOnce`, and
`SubmitOutputsAreWrittenBeforeSamePumpPollOutputs` all use this fixture. The
real live loopback Golden uses a genuinely running Stopwatch and never uses the
stopped-clock fixture. Production pacing code is unchanged.

The existing Gate 10 pending-collector corruption pattern may create one
deterministic later-Poll failure. Reflection exists only in Gate 11 tests. No
clock interface, delegate, test hook, `InternalsVisibleTo`, or production API
is added.

## 12. Same-Pump ordering fixture

The deterministic ordering test prepares the scheduled Publisher with:

```text
Tick101 complete
Tick102 complete
Tick100 Slots 0,2,1 accepted
EligibilityCeiling = 101
```

The TCP read then supplies Tick100 Slot3. Submit publishes 100,101. The
reflection fixture makes exactly one subsequent Poll Advance due; Poll
publishes 102. The client-side decoder must recover 100,101,102, proving
Submit output was completely written before same-Pump Poll output.

## 13. Gate 11 Golden

Configuration:

```text
initial Tick 100; InputDelayTicks 2
maxFutureTickOffset 8; history capacity 5
MaxPayloadLength 1048576
Server receive length 16 / offset 3 / capacity 3
Client receive length 16 / offset 5 / capacity 5
```

Roster:

```text
S0 PlayerId 0x0102030405060708
S1 PlayerId 0x000000000000002A
S2 PlayerId 0xFFEEDDCCBBAA0099
S3 PlayerId 0x00000000000F4243
```

Initial State100:

```text
S0 X=-300 Z=0    Aim=1000
S1 X=300  Z=0    Aim=2000
S2 X=0    Z=-300 Aim=3000
S3 X=0    Z=300  Aim=4000
```

Inputs and submission order:

```text
Tick100: S0( 1, 0,10100), S2(0, 1,30100), S1(-1,0,20100)
Tick101: S3(-1, 0,40101), S1(0,-1,20101), S0( 0,1,10101), S2(1,0,30101)
Tick102: S2( 0,-1,30102), S0(-1,0,10102), S3( 0,1,40102), S1(1,0,20102)
Tick100: S3( 0,-1,40100)
```

The caller sends all 12 messages. A single-threaded loop alternates Server and
Client pumps until three authoritative frames are recovered. A test-only
external Stopwatch deadline contains hangs without defining product timeout.
Tests assert no exact Read size, Pump count, elapsed duration, or per-real-Poll
publication count.

Expected flattened authority is Tick100,101,102. Final State103 is:

```text
S0 X=-300 Z=100  Aim=10102
S1 X=300  Z=-100 Aim=20102
S2 X=100  Z=-300 Aim=30102
S3 X=-100 Z=300  Aim=40102
Digest 386C4BB11A7EB7E0
```

The Golden vector returns actual results only. Expected state and Digest
literals remain in consumer tests.

## 14. Gate 11 test project

`Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj` uses:

```text
OutputType Exe; TargetFramework net8.0; LangVersion 12.0
Nullable enable; ImplicitUsings disable; TreatWarningsAsErrors true
BuildInParallel false
```

Direct dependencies are exactly Google.Protobuf 3.36.0 and ProjectReferences
to Server.LiveTcp, Client.LiveTcp, ProtocolAuthority, FrameSync, Protocol,
Simulation, and StreamFraming. It uses the existing dependency-free runner
pattern and no NUnit/xUnit/MSTest package.

Exact result target is `RESULT 32/32 passed`:

```text
1.  LegacyModeRetainsImmediateContinuousPublication
2.  ScheduledModeStartsWithoutLogicalAdvance
3.  ScheduledModeUsesOnePublisherForSubmitAndPoll
4.  CompleteFrameBeforeMaturityReturnsEmpty
5.  ScheduledPollPublishesAtExactMaturity
6.  MatureGapCompletionPublishesImmediatelyOnSubmit
7.  PollSerializesZeroFramesAsEmpty
8.  PollSerializesOneFrameAsOnePayload
9.  PollSerializesMultipleFramesAsIndependentOrderedPayloads
10. ParseFailureBeforePublicationDoesNotFault
11. MappingFailureBeforePublicationDoesNotFault
12. SubmitRejectionBeforePublicationDoesNotFault
13. PollFailureFaultsAndRethrowsOriginal
14. SimulationFailureAfterPublicationReturnsNoPartialPayloadAndFaults
15. FaultedProcessorRejectsSubmitAndPollFirst
16. LegacyAndScheduledProcessorsHaveIndependentAuthorityTimelines
17. ServerPumpNoReadableBytesStillPollsExactlyOnce
18. ServerPumpPerformsAtMostOneBoundedRead
19. OneServerReadProcessesMultipleSubmissionsInWireOrder
20. SubmitOutputsAreWrittenBeforeSamePumpPollOutputs
21. ZeroAuthorityOutputWritesNothing
22. ServerEofFaultsSessionBeforePolling
23. ServerOperationalFailureIsFailStop
24. ClientSendWritesOneFramedSubmission
25. ClientReceiveNoReadableBytesReturnsEmpty
26. OneClientReadProcessesMultipleAuthorityPayloadsInOrder
27. ClientUsesExpectedRosterAndStepsOnlyAuthoritativeFrames
28. ClientEofOrMalformedAuthorityIsFailStop
29. DisposedPumpsRejectFurtherOperations
30. LiveLoopbackGoldenReachesTick103AndApprovedDigest
31. MatureIncompleteTickSendsNothingUntilLateGapCompletion
32. TwoLiveRunsProduceSameFlattenedAuthoritySequenceAndState
```

## 15. Authored files and ignore contract

Implementation scope is limited to:

```text
Modify .gitignore only for exact authored csproj exceptions if required
Modify Server/LockstepArena.Server.ProtocolAuthority/ProtocolAuthorityProcessor.cs
Create Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj
Create Server/LockstepArena.Server.LiveTcp/TcpServerBattlePump.cs
Create Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj
Create Client/LockstepArena.Client.LiveTcp/TcpClientBattlePump.cs
Create Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj
Create Tests/LockstepArena.LiveTcp.Tests/Program.cs
Create Tests/LockstepArena.LiveTcp.Tests/ScheduledProtocolAuthorityTests.cs
Create Tests/LockstepArena.LiveTcp.Tests/TcpServerBattlePumpTests.cs
Create Tests/LockstepArena.LiveTcp.Tests/TcpClientBattlePumpTests.cs
Create Tests/LockstepArena.LiveTcp.Tests/LiveBattleLoopbackTests.cs
Create Tests/LockstepArena.LiveTcp.Tests/Gate11LiveBattleGoldenVector.cs
```

Because the repository ignores `*.csproj`, `.gitignore` may add exactly:

```text
!Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj
!Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj
!Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj
```

No other ignore change is allowed.

## 16. Protected boundaries

Relative to Gate 10, committed diff must be zero for Coordinator, Publisher,
ElapsedTickPacer, StopwatchTickDriver, Simulation, Protocol/schema/generated
source, StreamFraming, Gate 8 TCP, every existing Gate 3-10 test, Assets,
ProjectSettings, manifest, and packages-lock.

The only existing production source eligible for change is
`ProtocolAuthorityProcessor.cs`. TCP symbols may exist only in the two new
LiveTcp assemblies and Gate 11 tests.

## 17. Explicit exclusions

Gate 11 adds no KCP/UDP, background Thread, Task/async loop, generic transport
or session framework, opcode/envelope/router, automatic client Tick generation
or synchronization, Prediction/Dirty Frame, Snapshot/Rollback/Replay,
adaptive InputDelay, timeout/neutral/repeat-last, reconnect/heartbeat/retry,
Login/Room/matchmaking/persistence, Unity Update/FixedUpdate integration,
TLS/compression, DI/EventBus, recovery, or replacement authority timeline.

Gate 11 ends after evidence and independent review. It does not begin Gate 12.

## 18. Implementation Evidence

Gate 11 was implemented and verified from the following frozen history:

```text
Frozen Gate 10 base  4b46f241fe2a98a84841264da7ee50720b425a08
Planning foundation   0a06caf7f3fb5fb206e7c4766e160125dae096c6
Approved Planning     2c282482d13ded8fb22f3fef5b44dc0c340fd7b2
Task 1                5d0e338d881e7e45ca492cc6fae4a32201c0582f
Task 2                21fd9381b412f4ad9a0c98e3c12021baa0a42ce8
Task 3                2d76e3dfc6936a15c2eb07828e38075b24cf18bb
Task 4 / evidence parent
                      7baa8a94bdf585e093c7602620b16f37a3854370
```

### 18.1 Build and .NET execution evidence

The restore-assets preflight found missing assets for five existing projects
(Server Verification, Simulation Tests, FrameSync Tests, Protocol CodeGen,
and TickAuthority Tests). Each was restored using its existing project
contract, with no dependency or version change, and the complete build matrix
was restarted from build 1.

All 18 independent Release builds completed with zero warnings and zero
errors. Fresh execution results were:

```text
Gate 3 Simulation                   RESULT 38/38 passed
Gate 4 FrameSync                    RESULT 32/32 passed
Gate 5 Protocol                     RESULT 35/35 passed
Gate 6 ProtocolAuthority            RESULT 24/24 passed
Gate 7 StreamFraming                RESULT 32/32 passed
Gate 3 Server Golden                Tick=1000 Digest=89A7DD66F8D9E871
Gate 9 TickAuthority                RESULT 27/27 passed
Gate 10 TickPacing                  RESULT 27/27 passed
Gate 8 real TCP, 30-second watchdog RESULT 8/8 passed
Gate 11 live TCP, 60-second watchdog
                                      RESULT 32/32 passed
```

The Gate 11 suite proved legacy/scheduled Processor isolation, one scheduled
Publisher shared by Submit and Poll, stopped-Stopwatch deterministic maturity,
pre-publication rejection versus sticky post-publication failure, and no
partial payload return after a failing publication. It also proved one bounded
Read per Pump, Submit outputs written before same-Pump Poll outputs, no write
for zero authority output, deterministic EOF/fail-stop behavior, exactly one
synchronous write per framed payload, and idempotent disposal.

The live loopback Golden produced authoritative Tick100, Tick101, Tick102 in
order. Server and authoritative-only Client both reached State103 with Digest
`386C4BB11A7EB7E0`. Two independent real-TCP runs produced field-for-field
equal authority sequences and final states. A separate mature-incomplete
fixture sent no authority until the late Tick100 Slot3 input closed the gap.

### 18.2 Protocol regeneration and Unity evidence

Pinned Protocol regeneration used the existing Grpc.Tools contract with no
`PROTOBUF_PROTOC` or `Protobuf_ProtocFullPath` override. It produced exactly
the one tracked `LockstepArenaProtocol.g.cs`; Schema and Generated paths were
diff-clean afterward.

Three independent Unity 6000.3.10f1 EditMode runs used the frozen assembly
filters, no `-quit`, and `Start-Process -Wait`. Each stale XML was deleted
before its run, and pass status was established from newly generated NUnit XML
rather than process exit code alone:

```text
LockstepArena.StreamFraming.Editor.Tests
  total=1 passed=1 failed=0
  UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden
  result=Passed

LockstepArena.Protocol.Editor.Tests
  total=2 passed=2 failed=0
  GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads
  result=Passed
  UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector
  result=Passed

LockstepArena.Simulation.Editor.Tests
  filter=UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector
  total=1 passed=1 failed=0
  required named test result=Passed
```

Earlier attempts that ended during Unity Package Manager initialization
produced no NUnit XML and were not used as passing evidence. The authorized
retry retained the frozen `com.coplaydev.unity-mcp` URL/version/source and
changed no manifest, lockfile, dependency, production source, or test source.
After every accepted Unity run, exact worktree-local Assets/ProjectSettings
serialization changes were inspected and only the individually confirmed
paths were restored.

### 18.3 Boundary and repository audits

Relative to the frozen Gate 10 base, committed diff is zero for the four Gate
4/9/10 FrameSync production files, Simulation, Protocol/schema/generated
source, StreamFraming, Gate 8 TCP tests, all pre-existing tests, Assets,
ProjectSettings, manifest, and packages-lock. The only modified existing
production source is `ProtocolAuthorityProcessor.cs`; new production source is
limited to the Server and Client LiveTcp directories.

The final dependency audit found exactly 3/3/7 ProjectReferences in Server,
Client, and Tests respectively. Client and Tests use the existing pinned
Google.Protobuf 3.36.0 dependency; Client has no Server reference. The Gate 11
test project contains exactly seven tracked files and exactly 32 registered
tests. `.gitignore` adds only the three approved authored-csproj exceptions.

Source and artifact audits found no KCP/UDP, background Thread, Task/async
loop, Timer, retry/reconnect, missing-input replacement, prediction, snapshot,
rollback, replay, router/envelope, DI/EventBus, production reflection or test
injection hook. Reflection remains test-only, the Golden remains actual-only,
and no new package, symlink/junction, copy/sync/cleanup script, tracked
bin/obj, LockstepArena build DLL, or embedded-package build artifact exists.
Both worktree and committed diffs passed `git diff --check`. The Gate 11
worktree was clean before this evidence update, while the ordinary checkout
still contained exactly its two protected user-owned modifications:

```text
Assets/Settings/Mobile_RPAsset.asset
ProjectSettings/ShaderGraphSettings.asset
```
