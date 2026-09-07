# Gate 11 Live TCP Battle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Compose caller-provided client input, real synchronous TCP, one scheduled server authority timeline, real Stopwatch pacing, and authoritative-only client Simulation in a continuously pumped finite battle.

**Architecture:** Extend `ProtocolAuthorityProcessor` with a once-selected scheduled mode that owns one `TickDrivenFramePublisher`, one driver for that Publisher, and one server Simulation while preserving its legacy mode. Add separate Server and Client TCP pump assemblies so Client never depends on Server; each performs at most one readiness-guarded Read per pump call and uses the existing framing and protocol boundaries directly.

**Tech Stack:** .NET 8, C# 12, BCL `TcpClient`/`NetworkStream`/`Socket.Poll`, Google.Protobuf 3.36.0, existing LockstepArena Simulation/Protocol/StreamFraming/FrameSync assemblies, dependency-free executable tests.

**Spec:** `Docs/Architecture/GATE11_MINIMAL_LIVE_NETWORK_BATTLE_RUNTIME.md`

## Global Constraints

- Frozen base is `4b46f241fe2a98a84841264da7ee50720b425a08`.
- Implementation begins from the independently approved Planning commit on `codex/gate11-live-tcp-battle`, never by resetting to the frozen base.
- Legacy `ProtocolAuthorityProcessor` behavior and Gate 6 API remain regression-compatible.
- Scheduled Submit and Stopwatch Poll converge on exactly one `TickDrivenFramePublisher`.
- Server/client code is synchronous and caller-driven; no Thread, Task, async loop, Timer, sleep, or background receive loop.
- One Pump call performs at most one bounded Read; synchronous Write may block.
- No client Tick generation, prediction, timeout fill, neutral input, repeat-last, retry, reconnect, or recovery.
- No KCP, UDP, opcode, envelope, router, generic transport/session abstraction, DI, or EventBus.
- Do not modify the ordinary checkout or its two user-owned files.

---

## Final file map

Modify only:

```text
.gitignore
Server/LockstepArena.Server.ProtocolAuthority/ProtocolAuthorityProcessor.cs
Docs/Architecture/GATE11_MINIMAL_LIVE_NETWORK_BATTLE_RUNTIME.md (evidence only in final task)
```

Create:

```text
Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj
Server/LockstepArena.Server.LiveTcp/TcpServerBattlePump.cs
Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj
Client/LockstepArena.Client.LiveTcp/TcpClientBattlePump.cs
Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj
Tests/LockstepArena.LiveTcp.Tests/Program.cs
Tests/LockstepArena.LiveTcp.Tests/ScheduledProtocolAuthorityTests.cs
Tests/LockstepArena.LiveTcp.Tests/TcpServerBattlePumpTests.cs
Tests/LockstepArena.LiveTcp.Tests/TcpClientBattlePumpTests.cs
Tests/LockstepArena.LiveTcp.Tests/LiveBattleLoopbackTests.cs
Tests/LockstepArena.LiveTcp.Tests/Gate11LiveBattleGoldenVector.cs
```

Final test project direct ProjectReferences are exactly:

```text
Server/LockstepArena.Server.LiveTcp
Client/LockstepArena.Client.LiveTcp
Server/LockstepArena.Server.ProtocolAuthority
Server/LockstepArena.Server.FrameSync
Packages/com.locksteparena.protocol/Runtime
Packages/com.locksteparena.simulation/Runtime
Packages/com.locksteparena.stream-framing/Runtime
```

It has one direct PackageReference: `Google.Protobuf 3.36.0`.

## Required implementation-start verification

- [ ] Verify the final Planning commit supplied by independent review is local and remote HEAD, its ancestry includes the frozen base, the worktree is clean, and the ordinary checkout remains untouched.

```powershell
$branch = 'codex/gate11-live-tcp-battle'
$base = '4b46f241fe2a98a84841264da7ee50720b425a08'
if ((git branch --show-current) -ne $branch) { throw 'Wrong Gate 11 branch.' }
$local = (git rev-parse HEAD).Trim()
$remote = ((git ls-remote --heads origin "refs/heads/$branch") -split '\s+')[0]
if ($local -ne $remote) { throw 'Implementation must start at approved remote Planning HEAD.' }
git merge-base --is-ancestor $base HEAD
if ($LASTEXITCODE -ne 0) { throw 'Frozen Gate 10 base is not an ancestor.' }
if ((git status --porcelain).Length -ne 0) { throw 'Gate 11 worktree must be clean.' }
$ordinary = @(git -C 'E:/unityproject/LockstepArena' status --short)
$expected = @(
    ' M Assets/Settings/Mobile_RPAsset.asset',
    ' M ProjectSettings/ShaderGraphSettings.asset'
)
if ((Compare-Object $expected $ordinary).Count -ne 0) {
    throw 'Ordinary checkout does not match its protected state.'
}
```

Expected: approved Planning HEAD, clean Gate 11 worktree, exactly the two ordinary-checkout changes.

---

## Task 1: Add scheduled ProtocolAuthorityProcessor mode

**Commit:** `feat: add scheduled protocol authority mode`

**Files:**

- Modify: `.gitignore`
- Modify: `Server/LockstepArena.Server.ProtocolAuthority/ProtocolAuthorityProcessor.cs`
- Create: `Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj`
- Create: `Tests/LockstepArena.LiveTcp.Tests/Program.cs`
- Create: `Tests/LockstepArena.LiveTcp.Tests/ScheduledProtocolAuthorityTests.cs`

**Interfaces:**

- Consumes existing legacy Processor API, `TickDrivenFramePublisher`, `StopwatchTickDriver`, `ProtocolMapper`, and `BattleSimulation`.
- Produces the scheduled four-argument constructor and `byte[][] PollAuthority()` while preserving existing members.

### 1.1 Project and runner

- [ ] Add only the test-project ignore exception needed in this task:

```text
!Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj
```

- [ ] Create the test project initially with direct references to ProtocolAuthority, FrameSync, Protocol, Simulation, and StreamFraming plus the pinned Google.Protobuf package. Use the frozen final property group:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net8.0</TargetFramework>
  <LangVersion>12.0</LangVersion>
  <Nullable>enable</Nullable>
  <ImplicitUsings>disable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <BuildInParallel>false</BuildInParallel>
</PropertyGroup>
```

- [ ] Create `Program.cs` with the existing `TestCase`, `TestAssert`, and fail-count runner pattern. It must support `Equal`, `True`, `Same`, `NotSame`, byte sequence comparison, and `ThrowsAndReturn<TException>`.

### 1.2 RED: exact Processor tests 1-16

- [ ] Add these exact tests in this order:

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
```

Use Gate 6's fixed two/four-player helpers locally; do not compile or link an existing test helper. Expected protobuf payloads must be parsed back through `AuthoritativeFrameMessage.Parser` and `ProtocolMapper` rather than inspected as arbitrary bytes.

- [ ] Implement one test-only reflection fixture inside this test file. Locate private fields by unique type, never by field name:

```csharp
private static FieldInfo GetUniquePrivateField(Type owner, Type fieldType);
private static TickDrivenFramePublisher GetScheduledPublisher(
    ProtocolAuthorityProcessor processor);
private static StopwatchTickDriver GetScheduledDriver(
    ProtocolAuthorityProcessor processor);
private static void ForceNextPollAdvances(
    ProtocolAuthorityProcessor processor,
    uint dueAdvances);
```

`ForceNextPollAdvances` obtains the driver's unique `Stopwatch` and unique
`long` baseline field. It must call `stopwatch.Stop()` first, then capture:

```text
stableElapsed = stopwatch.ElapsedTicks
```

With `F = Stopwatch.Frequency`, compute:

```text
requiredElapsed = ceil(F * dueAdvances / SimulationConfig.TickRate)
                = (UInt128(F) * dueAdvances + TickRate - 1) / TickRate
baseline = checked(stableElapsed - checked((long)requiredElapsed))
```

Write that value into the unique reflected `long` baseline field. The stopped
Stopwatch makes the next Poll's delta exactly `requiredElapsed`. After a
successful Poll, the driver baseline equals the same `stableElapsed`, so a
later Poll without another fixture call observes exactly zero elapsed delta.
Only small bounded `dueAdvances` values are used.

`ScheduledPollPublishesAtExactMaturity`,
`PollFailureFaultsAndRethrowsOriginal`,
`ServerPumpNoReadableBytesStillPollsExactlyOnce`, and
`SubmitOutputsAreWrittenBeforeSamePumpPollOutputs` must use this stopped-clock
fixture. Do not sleep or assert real elapsed time. The real live loopback
Golden must use its genuinely running Stopwatch and must not call this fixture.

For test 13, reproduce the Gate 10 fault fixture: validly create pending complete frames, locate the unique Coordinator/pending dictionary/collector by type, corrupt only the future completed Frame's Tick, force the later Poll, and assert original exception plus sticky rejection.

For test 14, reflection may make the Processor Simulation Tick inconsistent after authority collection; assert already-published authority remains, no payload array is assigned, and both later APIs reject. Do not add a production hook.

- [ ] Run RED:

```powershell
dotnet build Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release --nologo
```

Expected: compile failure only because the scheduled constructor and `PollAuthority` do not exist.

### 1.3 Minimal Processor implementation

- [ ] Refactor fields into immutable alternatives:

```csharp
private readonly AuthoritativeFrameCoordinator? _legacyCoordinator;
private readonly TickDrivenFramePublisher? _scheduledPublisher;
private readonly StopwatchTickDriver? _scheduledDriver;
private readonly BattleSimulation _serverSimulation;
private bool _faulted;
```

- [ ] Keep the legacy constructor signature and behavior. Add the scheduled constructor exactly as specified. Construct the scheduled driver last.

- [ ] Make `NextPublishTick` read from the active authority owner. Keep `_faulted` first in Submit. Parse/map before selecting legacy `Submit` or scheduled `Submit`.

- [ ] Add `PollAuthority()` with this exact control flow:

```text
faulted -> throw
legacy mode -> InvalidOperationException without fault
scheduled driver Poll inside try
any Poll exception -> fault + rethrow
successful FrameData[] -> shared publication method
```

- [ ] Extract one private publication method. Empty returns `Array.Empty<byte[]>()`; nonempty Step/serialize is inside a catch that sets `_faulted` and rethrows. Assign and return the outer array only after the complete loop.

### 1.4 GREEN, regressions, audit, commit

- [ ] Run:

```powershell
dotnet run --project Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj -c Release
```

Require interim Gate 11 `16/16`, Gate 6 `24/24`, and Gate 10 `27/27`.

- [ ] Audit that the Processor has one legacy Coordinator field, one scheduled Publisher field, one driver field, one Simulation, no interface/delegate/injected clock, and no instance constructs both authority modes.

- [ ] Commit only Task 1 files:

```powershell
git diff --check
git add .gitignore `
  Server/LockstepArena.Server.ProtocolAuthority/ProtocolAuthorityProcessor.cs `
  Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj `
  Tests/LockstepArena.LiveTcp.Tests/Program.cs `
  Tests/LockstepArena.LiveTcp.Tests/ScheduledProtocolAuthorityTests.cs
git commit -m "feat: add scheduled protocol authority mode"
```

---

## Task 2: Add the Server live TCP pump

**Commit:** `feat: pump scheduled authority over tcp`

**Files:**

- Modify: `.gitignore`
- Create: `Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj`
- Create: `Server/LockstepArena.Server.LiveTcp/TcpServerBattlePump.cs`
- Modify: `Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj`
- Modify: `Tests/LockstepArena.LiveTcp.Tests/Program.cs`
- Create: `Tests/LockstepArena.LiveTcp.Tests/TcpServerBattlePumpTests.cs`

**Interfaces:**

- Consumes scheduled Processor, StreamFraming, Simulation, and an already-connected IPv4 `TcpClient`.
- Produces `TcpServerBattlePump`, `ServerState`, `NextPublishTick`, `int PumpOnce()`, and idempotent Dispose.

### 2.1 RED: tests 17-23

- [ ] Add the Server csproj exception and the exact Server project XML from the Spec. Add its ProjectReference to the Gate 11 test project.

- [ ] Add exact tests:

```text
17. ServerPumpNoReadableBytesStillPollsExactlyOnce
18. ServerPumpPerformsAtMostOneBoundedRead
19. OneServerReadProcessesMultipleSubmissionsInWireOrder
20. SubmitOutputsAreWrittenBeforeSamePumpPollOutputs
21. ZeroAuthorityOutputWritesNothing
22. ServerEofFaultsSessionBeforePolling
23. ServerOperationalFailureIsFailStop
```

All socket fixtures use `TcpListener(IPAddress.Loopback, 0)`, explicit
`TcpClient(AddressFamily.InterNetwork)`, the OS-assigned port, one accepted
connection, and scoped disposal.

Test 17 accesses the unique scheduled Processor/Publisher through the approved
reflection fixture, forces exactly one due Advance, calls `PumpOnce()` with no
readable network data, and asserts Publisher `CollectionTick` increased once.

Test 18 uses receive length 16, offset 3, capacity 3. Queue exactly one complete
four-byte framed stream `00 00 00 00`, the big-endian prefix for a legal
zero-length payload. Before calling the Pump, a bounded test-only readiness
loop must require `Socket.Available >= 4`.

The correct single Read consumes at most three bytes, leaves the prefix
incomplete, processes no submission, and returns normally. An incorrect second
Read necessarily consumes the remaining byte, completes the zero-length
payload, parses the protobuf default submission, and reaches an observable
`ProtocolMapper` rejection because the nested Input is missing. Assert this
normal-versus-failure distinction without asserting an individual `Read`
return size and without adding production I/O injection.

Test 19 waits with a test-only bounded readiness loop until the complete two-
submission stream is queued, then uses a read capacity large enough for the
queued stream. It asserts recovered effects in wire order, not an exact Read
length.

Test 20 prepares complete Tick101/102, partial Tick100, and eligibility 101 via
the reflection fixture. The one network submission completes Tick100;
Submit writes authority 100/101. Exactly one forced Poll Advance then writes
Tick102. Decode the peer stream and require Tick100,101,102.

Test 22 performs `client.Client.Shutdown(SocketShutdown.Send)`, asserts
`EndOfStreamException`, no CollectionTick change, then sticky rejection.
Test 23 sends one complete malformed framed submission, asserts the original
parse error, then immediate sticky rejection.

- [ ] Run RED and require missing `TcpServerBattlePump` only:

```powershell
dotnet build Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release --nologo
```

### 2.2 Minimal Server implementation

- [ ] Implement the exact constructor validation order and fields from the Spec. Ownership transfers only after successful construction; Processor construction is last.

- [ ] Implement `PumpOnce()` with disposed then faulted checks outside the operational try/catch. Inside the try: zero-duration `Socket.Poll`, EOF check, at most one Read, exact-segment Feed, process all decoded submissions, write their outputs, Poll exactly once, then write Poll outputs.

- [ ] Use one private write helper:

```csharp
private int WritePayloads(byte[][] payloads)
```

For each payload, call `LengthPrefixedFrameEncoder.Encode`, then one
`NetworkStream.Write`; increment count only after Write returns. A later error
propagates and causes `PumpOnce()` to fault without returning the partial count.

- [ ] Implement idempotent Dispose. Disposed operations throw
`ObjectDisposedException` before checking sticky fault.

### 2.3 GREEN, focused audit, commit

- [ ] Run and require interim Gate 11 `23/23`, Gate 8 `8/8`, Gate 10 `27/27`:

```powershell
dotnet run --project Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj -c Release
```

- [ ] Audit one `NetworkStream.Read` call site, direct decoder segment Feed, Submit writes before Poll, Poll exactly once, and no Task/Thread/async/sleep.

- [ ] Commit Task 2 files only:

```powershell
git diff --check
git add .gitignore `
  Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj `
  Server/LockstepArena.Server.LiveTcp/TcpServerBattlePump.cs `
  Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj `
  Tests/LockstepArena.LiveTcp.Tests/Program.cs `
  Tests/LockstepArena.LiveTcp.Tests/TcpServerBattlePumpTests.cs
git commit -m "feat: pump scheduled authority over tcp"
```

---

## Task 3: Add the Client live TCP pump

**Commit:** `feat: consume authoritative tcp frames on client`

**Files:**

- Modify: `.gitignore`
- Create: `Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj`
- Create: `Client/LockstepArena.Client.LiveTcp/TcpClientBattlePump.cs`
- Modify: `Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj`
- Modify: `Tests/LockstepArena.LiveTcp.Tests/Program.cs`
- Create: `Tests/LockstepArena.LiveTcp.Tests/TcpClientBattlePumpTests.cs`

**Interfaces:**

- Consumes an already-connected IPv4 `TcpClient`, Protocol/Google.Protobuf, StreamFraming, and initial Simulation state.
- Produces `TcpClientBattlePump`, authoritative-only `ClientState`, `SendInput`, `PumpReceiveOnce`, and Dispose.

### 3.1 RED: tests 24-29

- [ ] Add the Client csproj exception, exact Client project XML, and its final direct test ProjectReference.

- [ ] Add exact tests:

```text
24. ClientSendWritesOneFramedSubmission
25. ClientReceiveNoReadableBytesReturnsEmpty
26. OneClientReadProcessesMultipleAuthorityPayloadsInOrder
27. ClientUsesExpectedRosterAndStepsOnlyAuthoritativeFrames
28. ClientEofOrMalformedAuthorityIsFailStop
29. DisposedPumpsRejectFurtherOperations
```

Test 24 reads and decodes the peer stream, parses the one submission, and
asserts PlayerId and all InputFrame fields. Test 25 asserts empty and unchanged
ClientState. Test 26 queues complete Tick100/101 authority payloads before the
readiness-bounded receive and asserts ordered returned Frames and State102.

Test 27 first sends no local speculative Step, then supplies one structurally
matching authoritative frame and asserts exactly one Step. A structurally
different wire roster must fault through expected-roster mapping.

Test 28 covers deterministic EOF via `Shutdown(SocketShutdown.Send)` and a
separate malformed authoritative payload; each fresh pump propagates the
original error and rejects the next operation. Test 29 verifies idempotent
Dispose plus `ObjectDisposedException` priority for Server and Client pumps.

- [ ] Run RED and require missing `TcpClientBattlePump` only:

```powershell
dotnet build Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release --nologo
```

### 3.2 Minimal Client implementation

- [ ] Implement constructor validation in the frozen order and take ownership only on success.

- [ ] Implement `SendInput` with disposed/fault checks, map/serialize/encode before the write try, and sticky fault only if synchronous Write throws.

- [ ] Implement `PumpReceiveOnce` with one readiness-guarded bounded Read, exact Feed segment, parse/map/Step in wire order, fresh return container, no local prediction, and fail-stop after any consumed-stream processing failure.

- [ ] Implement idempotent Dispose and disposed-first operation rejection.

### 3.3 GREEN, focused audit, commit

- [ ] Run and require interim Gate 11 `29/29`, Gate 5 `35/35`, Gate 7 `32/32`:

```powershell
dotnet run --project Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj -c Release
```

- [ ] Audit that Client has no Server ProjectReference, creates no input Tick, steps only decoded authority, and has one Read/one Write call site.

- [ ] Commit Task 3 files only:

```powershell
git diff --check
git add .gitignore `
  Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj `
  Client/LockstepArena.Client.LiveTcp/TcpClientBattlePump.cs `
  Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj `
  Tests/LockstepArena.LiveTcp.Tests/Program.cs `
  Tests/LockstepArena.LiveTcp.Tests/TcpClientBattlePumpTests.cs
git commit -m "feat: consume authoritative tcp frames on client"
```

---

## Task 4: Prove the continuously pumped live Golden

**Commit:** `test: prove live tcp battle composition`

**Files:**

- Modify: `Tests/LockstepArena.LiveTcp.Tests/Program.cs`
- Create: `Tests/LockstepArena.LiveTcp.Tests/Gate11LiveBattleGoldenVector.cs`
- Create: `Tests/LockstepArena.LiveTcp.Tests/LiveBattleLoopbackTests.cs`

**Interfaces:**

- Consumes both production pumps and the frozen Tick100-102 vector.
- Produces actual-only `Gate11LiveBattleGoldenResult` and final tests 30-32.

### 4.1 RED: actual-only result and tests 30-32

- [ ] Define this exact actual-only shape:

```csharp
internal sealed class Gate11LiveBattleGoldenResult
{
    public FrameData[] AuthoritativeFrames { get; }
    public BattleState ServerState { get; }
    public BattleState ClientState { get; }
    public int AuthoritativeMessageWriteCount { get; }
    public uint ServerNextPublishTick { get; }
}
```

The vector contains the approved roster, initial state, 12 inputs, submission
order, pump configuration, loopback setup, actual execution, and no expected
state, Digest, batch-size, or pass/fail literal.

- [ ] Add exact tests:

```text
30. LiveLoopbackGoldenReachesTick103AndApprovedDigest
31. MatureIncompleteTickSendsNothingUntilLateGapCompletion
32. TwoLiveRunsProduceSameFlattenedAuthoritySequenceAndState
```

Test 30 owns the consumer literals Tick100/101/102, final State103, and
`0x386C4BB11A7EB7E0`. It asserts ServerState equals ClientState field-for-field.

Test 31 uses the first 11 submissions, test-only reflection to mature through
Tick102, and real TCP pumps. It asserts no authority network output and
unchanged State100 until Tick100 Slot3 is sent, after which the flattened
sequence becomes 100,101,102. It adds no timeout or neutral policy.

Test 32 performs two independent `Run()` executions and compares every
authoritative Frame Tick, roster entry, and InputFrame field plus final Server
and Client states. It does not compare Pump counts or elapsed time.

- [ ] Run RED and require missing Golden types only:

```powershell
dotnet build Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release --nologo
```

### 4.2 Minimal Golden implementation

- [ ] Create real loopback with `TcpListener(IPAddress.Loopback, 0)`, explicit IPv4 client, exact OS port, and one accepted connection.

- [ ] Construct pumps with:

```text
initialTick 100; InputDelay 2; future offset 8; history 5
MaxPayloadLength 1048576
Server buffer 16 / offset 3 / capacity 3
Client buffer 16 / offset 5 / capacity 5
```

- [ ] Send all 12 approved inputs using `TcpClientBattlePump.SendInput`. In a single caller thread, alternate `server.PumpOnce()` then `client.PumpReceiveOnce()` until three Frames are recovered.

- [ ] Bound the test fixture with an external ten-second Stopwatch deadline. Do not sleep, inject a clock, or interpret expiry as gameplay timeout. Throw only a test failure if three frames are not observed.

### 4.3 GREEN, Golden audit, commit

- [ ] Run:

```powershell
dotnet build Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release --nologo
dotnet run --project Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj -c Release
```

Require Gate 11 `RESULT 32/32 passed`, Gate 6 `24/24`, and Gate 10 `27/27`.

- [ ] Require exactly seven Gate 11 test project files, seven direct ProjectReferences, one pinned Google.Protobuf PackageReference, and no expected Digest in the Golden vector.

- [ ] Commit:

```powershell
git diff --check
git add Tests/LockstepArena.LiveTcp.Tests/Program.cs `
  Tests/LockstepArena.LiveTcp.Tests/Gate11LiveBattleGoldenVector.cs `
  Tests/LockstepArena.LiveTcp.Tests/LiveBattleLoopbackTests.cs
git commit -m "test: prove live tcp battle composition"
```

---

## Task 5: Fresh final verification, evidence, push, and STOP

**Commit:** `docs: record Gate 11 implementation evidence`

**Files:**

- Modify: `Docs/Architecture/GATE11_MINIMAL_LIVE_NETWORK_BATTLE_RUNTIME.md`

### 5.1 Restore-assets preflight

- [ ] Resolve `ProjectAssetsFile` for these exact 18 projects. Restore only a missing asset under its existing frozen project contract. If any restore occurs, restart the complete build matrix at build 1.

```powershell
$projects = @(
 'Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj',
 'Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj',
 'Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj',
 'Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj',
 'Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj',
 'Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj',
 'Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj',
 'Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj',
 'Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj',
 'Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj',
 'Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj',
 'Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj',
 'Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj',
 'Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj',
 'Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj',
 'Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj',
 'Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj',
 'Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj'
)
$restoreOccurred = $false
foreach ($project in $projects) {
  $output = & dotnet msbuild $project -nologo -verbosity:quiet -getProperty:ProjectAssetsFile
  if ($LASTEXITCODE -ne 0) { throw "Could not resolve assets: $project" }
  $asset = ($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -Last 1).Trim()
  if (-not [IO.Path]::IsPathRooted($asset)) {
    $asset = Join-Path (Split-Path -Parent $project) $asset
  }
  if (-not (Test-Path -LiteralPath $asset)) {
    dotnet restore $project --nologo
    if ($LASTEXITCODE -ne 0) { throw "Restore failed: $project" }
    $restoreOccurred = $true
  }
}
```

### 5.2 Exact 18 Release builds

- [ ] Run independently and sequentially, all with zero warnings/errors:

```powershell
dotnet build Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj -c Release --no-restore --nologo
dotnet build Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj -c Release --no-restore --nologo
dotnet build Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release --no-restore --nologo
dotnet build Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj -c Release --no-restore --nologo
dotnet build Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj -c Release --no-restore --nologo
dotnet build Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj -c Release --no-restore --nologo
dotnet build Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj -c Release --no-restore --nologo
dotnet build Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj -c Release --no-restore --nologo
dotnet build Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release --no-restore --nologo
```

### 5.3 .NET execution matrix

- [ ] Run Gate 3-7, Server Golden, Gate 9, Gate 10, and Gate 11:

```powershell
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj -c Release --no-build
dotnet run --project Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj -c Release --no-build
```

Require `38/38`, `32/32`, `35/35`, `24/24`, `32/32`, Server Golden
`89A7DD66F8D9E871`, `27/27`, and `27/27` respectively.

- [ ] Run frozen Gate 8 with its existing external 30-second watchdog and require `RESULT 8/8 passed`.

- [ ] Run Gate 11 with an external 60-second watchdog:

```powershell
$stdout = '.artifacts/gate11-live-tcp.stdout.txt'
$stderr = '.artifacts/gate11-live-tcp.stderr.txt'
$args = @('run','--project','Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj','-c','Release','--no-build')
$process = Start-Process dotnet -ArgumentList $args -PassThru -WindowStyle Hidden `
  -RedirectStandardOutput $stdout -RedirectStandardError $stderr
if (-not $process.WaitForExit(60000)) {
  & taskkill.exe /PID $process.Id /T /F | Out-Null
  throw 'Gate 11 exceeded the external 60-second test bound.'
}
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Gate 11 exited $($process.ExitCode)." }
Get-Content -Raw $stdout
```

Require `RESULT 32/32 passed`, flattened Tick100-102, final Tick103, and
Digest `386C4BB11A7EB7E0`.

### 5.4 Pinned Protocol regeneration

- [ ] Require no protoc override, rebuild pinned CodeGen, exactly one generated source, and diff-clean Schema/Generated paths:

```powershell
if ($env:PROTOBUF_PROTOC) { throw 'PROTOBUF_PROTOC override is not allowed.' }
if ($env:Protobuf_ProtocFullPath) { throw 'Protobuf_ProtocFullPath override is not allowed.' }
dotnet build Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj -c Release --no-restore --nologo
$generated = @(git ls-files 'Packages/com.locksteparena.protocol/Runtime/Generated/*.g.cs')
if ($generated.Length -ne 1 -or
    $generated[0] -ne 'Packages/com.locksteparena.protocol/Runtime/Generated/LockstepArenaProtocol.g.cs') {
  throw 'Unexpected generated Protocol source set.'
}
git diff --exit-code -- Packages/com.locksteparena.protocol/Schema `
  Packages/com.locksteparena.protocol/Runtime/Generated
```

### 5.5 Fresh Unity regressions

- [ ] Run three independent Unity 6000.3.10f1 `Start-Process -Wait` commands without `-quit`. Delete old XML first and require fresh NUnit XML, not process exit code alone.

```text
LockstepArena.StreamFraming.Editor.Tests
  exact total=1, failed=0
  UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden Passed

LockstepArena.Protocol.Editor.Tests
  exact total=2, failed=0
  GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads Passed
  UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector Passed

LockstepArena.Simulation.Editor.Tests
  filter UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector
  total>=1, failed=0, required named test Passed
```

Use `.artifacts/gate11-unity/` XML/log paths. After each run inspect exact
Assets/ProjectSettings diff and restore only an individually confirmed
Unity-generated worktree-local path. Never broad reset/clean.

### 5.6 Final audits

- [ ] Require protected diff zero from the frozen base:

```powershell
$base = '4b46f241fe2a98a84841264da7ee50720b425a08'
git diff --exit-code $base -- Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs
git diff --exit-code $base -- Server/LockstepArena.Server.FrameSync/TickDrivenFramePublisher.cs
git diff --exit-code $base -- Server/LockstepArena.Server.FrameSync/ElapsedTickPacer.cs
git diff --exit-code $base -- Server/LockstepArena.Server.FrameSync/StopwatchTickDriver.cs
git diff --exit-code $base -- Packages/com.locksteparena.simulation
git diff --exit-code $base -- Packages/com.locksteparena.protocol
git diff --exit-code $base -- Packages/com.locksteparena.stream-framing
git diff --exit-code $base -- Tests/LockstepArena.TcpEndToEnd.Tests
git diff --exit-code $base -- Assets ProjectSettings Packages/manifest.json Packages/packages-lock.json
git diff --exit-code $base -- Tests ':(exclude)Tests/LockstepArena.LiveTcp.Tests/**'
```

- [ ] Require the only existing production-file diff to be
`ProtocolAuthorityProcessor.cs`; new production files only under the two
LiveTcp directories; exactly three `.gitignore` added exceptions.

- [ ] Require Server LiveTcp exactly three ProjectReferences, Client LiveTcp
exactly three ProjectReferences plus Google.Protobuf 3.36.0, and Tests exactly
seven ProjectReferences plus the same pinned PackageReference.

- [ ] Search Gate 11 production diff for KCP, UDP, Thread, Task, async, Timer,
sleep, retry, reconnect, timeout fill, neutral/repeat-last, prediction,
snapshot, rollback, replay, router, envelope, DI, EventBus, interface, clock
injection, delegate injection, and `InternalsVisibleTo`; require no match.

- [ ] Require reflection only in Gate 11 tests, no expected Digest in the
actual-only Golden, no symlink/junction, copy/sync/cleanup script, new package,
tracked bin/obj/LockstepArena build DLL, or embedded-package artifact.

- [ ] Run `git diff --check`, inspect the complete base diff, scan both Gate 11
documents for unresolved planning markers, stale test counts, or contradictions,
and require the ordinary checkout's exact two protected changes.

### 5.7 Evidence commit, push, and STOP

- [ ] Append fresh Implementation Evidence to the Architecture document:

```text
base, Planning HEAD, implementation SHAs, evidence parent
restore-assets result
18 independent build results
Gate 3-11 exact totals and Server Golden
Gate 8 and Gate 11 watchdog results
legacy/scheduled isolation and deterministic reflection evidence
same-Pump Submit-before-Poll evidence
EOF/fail-stop and synchronous-write count evidence
live Tick100-102, State103, Digest and two-run equality
pinned regeneration
three fresh Unity XML results
protected/dependency/scope/artifact/ordinary-checkout audits
```

- [ ] Commit only evidence:

```powershell
git add Docs/Architecture/GATE11_MINIMAL_LIVE_NETWORK_BATTLE_RUNTIME.md
git commit -m "docs: record Gate 11 implementation evidence"
```

- [ ] Push only after the user-authorized implementation workflow permits it, then prove remote/local equality and cleanliness:

```powershell
$branch = 'codex/gate11-live-tcp-battle'
git push origin $branch
$local = (git rev-parse HEAD).Trim()
$remote = ((git ls-remote --heads origin "refs/heads/$branch") -split '\s+')[0]
if ($local -ne $remote) { throw 'Remote Gate 11 SHA mismatch.' }
if ((git status --porcelain).Length -ne 0) { throw 'Gate 11 worktree is not clean.' }
```

- [ ] Confirm the ordinary checkout one last time, submit the Gate 11 Final Implementation Handoff, and STOP. Do not begin Gate 12 or KCP.
