# Gate 8: Minimal Real TCP End-to-End

## 1. Status and Frozen Baseline

This document consolidates the independently approved Gate 8 Direction, Architecture Section 1, Section 1 Amendment, Architecture Section 2, and Architecture Closure.

Gate 8 is:

```text
Minimal Real TCP End-to-End
```

Frozen implementation and comparison base:

```text
0a73d924944a192c16c12260447c63272b727899
```

Gate 8 proves that the complete-payload boundary established by Gates 5-7 works over one real operating-system TCP byte stream. It adds no production transport code.

## 2. Exact Proof Chain

The approved proof is:

```text
frozen Gate 6 logical submissions
-> PlayerInputSubmission protobuf payloads
-> Gate 7 length-prefix framing
-> one continuous client-to-server byte stream
-> real synchronous IPv4 loopback TCP
-> incremental NetworkStream.Read
-> decoder.Feed(receiveBuffer, offset, bytesRead)
-> complete submission payloads
-> frozen ProtocolAuthorityProcessor
-> three independent authoritative payloads for Ticks 100, 101, 102
-> Gate 7 length-prefix framing
-> one continuous server-to-client byte stream on the same connection
-> real synchronous IPv4 loopback TCP
-> incremental NetworkStream.Read
-> decoder.Feed(receiveBuffer, offset, bytesRead)
-> complete authoritative payloads
-> Client Parse / ProtocolMapper / BattleSimulation
-> ServerState == ClientState
-> State Tick 103
-> StateDigest 0x386C4BB11A7EB7E0
```

No test may assume that one `NetworkStream.Write` corresponds to one `NetworkStream.Read`, and no test may assert an exact individual `Read` return size.

## 3. Ownership and File Layout

Gate 8 creates exactly one dependency-free test executable project:

```text
Tests/LockstepArena.TcpEndToEnd.Tests/
|- LockstepArena.TcpEndToEnd.Tests.csproj
|- Program.cs
|- LoopbackTcpGoldenVector.cs
`- LoopbackTcpEndToEndTests.cs
```

There is no Gate 8 production assembly, package, Unity assembly, shared helper, transport adapter, or external test dependency. All TCP symbols introduced by Gate 8 must exist only below this new test-project directory.

The project contract is:

```text
OutputType            Exe
TargetFramework       net8.0
LangVersion           12.0
Nullable              enable
ImplicitUsings        disable
TreatWarningsAsErrors true
BuildInParallel       false
```

Its direct `ProjectReference` entries are exactly:

```text
../../Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj
../../Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj
../../Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj
../../Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj
```

It has no direct FrameSync reference, NuGet test framework, package reference, external `Compile Include`, linked Gate 6/Gate 7 helper, wildcard compile rule, or reference to an existing test project.

## 4. Real TCP Endpoint Contract

Each successful Golden run creates exactly one listener and one accepted connection:

```csharp
var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start(1);
var listenerEndpoint = (IPEndPoint)listener.LocalEndpoint;

var client = new TcpClient(AddressFamily.InterNetwork);
client.Connect(IPAddress.Loopback, listenerEndpoint.Port);
TcpClient acceptedClient = listener.AcceptTcpClient();
```

The run records and the consumer asserts:

- listener address is exactly `IPAddress.Loopback`;
- the operating-system-assigned port is greater than zero;
- the explicit IPv4 client connects to that exact port;
- the Client remote address is `IPAddress.Loopback`;
- the Client remote port equals the listener port.

Two runs are not required to receive different ephemeral ports.

The execution model is one process, synchronous, one accepted connection, and the same connection used bidirectionally. Scoped disposal owns both `TcpClient` instances and both `NetworkStream` instances. The listener is stopped in `finally`. There is no background receive loop, async/Task/thread model, connection abstraction, retry, reconnect, cancellation protocol, or recovery policy.

## 5. Successful Run Order and Completion

The successful run order is:

```text
start listener
-> connect explicit IPv4 client
-> accept one Server-side TcpClient
-> Client writes one continuous framed stream containing all 12 submissions
-> Server incrementally reads and recovers exactly 12 payloads
-> Server submits recovered payloads to ProtocolAuthorityProcessor in order
-> Server obtains three independent authority payloads
-> Server writes one continuous framed stream containing those three payloads
-> Client incrementally reads and recovers exactly three payloads
-> Client parses, maps, and Steps its BattleSimulation in order
-> compare actual Server and Client results
-> dispose connection resources and stop listener
```

The successful Server read loop completes on exactly 12 recovered submission payloads. The successful Client read loop completes on exactly three recovered authoritative payloads. It does not wait for connection close after either expected count has been reached.

If `NetworkStream.Read` returns zero before the relevant expected message count, the loop throws `EndOfStreamException`. Framing, protobuf parsing, mapping, authority, Simulation, socket, and I/O exceptions propagate unchanged.

## 6. Receive Buffer and Decoder Contracts

The test-only framing maximum is:

```text
MaxPayloadLength = 4096
```

Client-to-Server reads use exactly:

```text
receive buffer length = 16
valid segment offset  = 3
maximum Read count    = 3
```

The Server loop calls:

```csharp
int bytesRead = serverStream.Read(receiveBuffer, 3, 3);
byte[][] payloads = submissionDecoder.Feed(receiveBuffer, 3, bytesRead);
```

Server-to-Client reads use exactly:

```text
receive buffer length = 16
valid segment offset  = 5
maximum Read count    = 5
```

The Client loop calls:

```csharp
int bytesRead = clientStream.Read(receiveBuffer, 5, 5);
byte[][] payloads = authoritativeDecoder.Feed(receiveBuffer, 5, bytesRead);
```

Each side reuses its receive buffer. No exact-sized intermediary input array is allocated between `Read` and `Feed`, and only the returned valid segment is passed to the Decoder. The deliberately small Read windows make the fixed streams require more than one Read without assuming how TCP segments data.

Only Test 2 owns the assertion `ServerReadCallCount > 1`. Only Test 5 owns the assertion `ClientReadCallCount > 1`.

## 7. Frozen Roster, Initial State, and Inputs

Gate 8 independently recreates the logical Gate 6 Golden. It does not compile, link, copy, or reference a Gate 6 or Gate 7 test helper.

Roster in Slot order:

```text
Slot0 PlayerId = 0x0102030405060708
Slot1 PlayerId = 0x000000000000002A
Slot2 PlayerId = 0xFFEEDDCCBBAA0099
Slot3 PlayerId = 0x00000000000F4243
```

Initial State at Tick 100:

```text
Slot0 = X -300, Z 0,    Aim 1000
Slot1 = X 300,  Z 0,    Aim 2000
Slot2 = X 0,    Z -300, Aim 3000
Slot3 = X 0,    Z 300,  Aim 4000
```

Tick 100 inputs:

```text
Slot0 = MoveX  1, MoveZ  0, Aim 10100
Slot1 = MoveX -1, MoveZ  0, Aim 20100
Slot2 = MoveX  0, MoveZ  1, Aim 30100
Slot3 = MoveX  0, MoveZ -1, Aim 40100
```

Tick 101 inputs:

```text
Slot0 = MoveX  0, MoveZ  1, Aim 10101
Slot1 = MoveX  0, MoveZ -1, Aim 20101
Slot2 = MoveX  1, MoveZ  0, Aim 30101
Slot3 = MoveX -1, MoveZ  0, Aim 40101
```

Tick 102 inputs:

```text
Slot0 = MoveX -1, MoveZ  0, Aim 10102
Slot1 = MoveX  1, MoveZ  0, Aim 20102
Slot2 = MoveX  0, MoveZ -1, Aim 30102
Slot3 = MoveX  0, MoveZ  1, Aim 40102
```

The exact submission order is:

```text
Tick100: Slot 0, 2, 1
Tick101: Slot 3, 1, 0, 2
Tick102: Slot 2, 0, 3, 1
Tick100: Slot 3
```

Each logical input is converted with the frozen `ProtocolMapper`, serialized as one complete `PlayerInputSubmissionMessage` payload, framed independently, and concatenated with the other frames into one continuous Client-to-Server byte stream.

## 8. Frozen Authority and Client Results

The first 11 Processor calls return zero payloads. The twelfth submission fills Tick 100's gap and returns exactly three independent payload arrays. Parsed and mapped authoritative Ticks are exactly:

```text
100, 101, 102
```

Those three payloads are framed independently and concatenated into one continuous Server-to-Client byte stream.

After authoritative Tick 100, State Tick 101 is:

```text
Slot0 = X -200, Z 0,    Aim 10100
Slot1 = X 200,  Z 0,    Aim 20100
Slot2 = X 0,    Z -200, Aim 30100
Slot3 = X 0,    Z 200,  Aim 40100
Digest = 0xD95809E1EB5CDDAA
```

After authoritative Tick 101, State Tick 102 is:

```text
Slot0 = X -200, Z 100,  Aim 10101
Slot1 = X 200,  Z -100, Aim 20101
Slot2 = X 100,  Z -200, Aim 30101
Slot3 = X -100, Z 200,  Aim 40101
Digest = 0xA96B83267DD72A7D
```

After authoritative Tick 102, final State Tick 103 is:

```text
Slot0 = X -300, Z 100,  Aim 10102
Slot1 = X 300,  Z -100, Aim 20102
Slot2 = X 100,  Z -300, Aim 30102
Slot3 = X -100, Z 300,  Aim 40102
Digest = 0x386C4BB11A7EB7E0
```

The final Server and Client rosters are structurally equal, their full states are equal, `NextPublishTick == 103`, and both final Digests equal `0x386C4BB11A7EB7E0`. TCP and framing bytes do not enter `StateDigest`.

## 9. Actual-Only Golden API

The exact test-side API is:

```csharp
internal static class LoopbackTcpGoldenVector
{
    internal static LoopbackTcpGoldenResult Run();
}

internal sealed class LoopbackTcpGoldenResult
{
    public IPAddress ListenerAddress { get; }
    public int ListenerPort { get; }
    public IPAddress ClientRemoteAddress { get; }
    public int ClientRemotePort { get; }
    public int ServerReadCallCount { get; }
    public int ClientReadCallCount { get; }
    public byte[][] SubmissionPayloads { get; }
    public byte[][] RecoveredSubmissionPayloads { get; }
    public int[] ProcessorOutputCounts { get; }
    public byte[][] AuthoritativePayloads { get; }
    public byte[][] RecoveredAuthoritativePayloads { get; }
    public FrameData[] AuthoritativeFrames { get; }
    public BattleState[] ClientStates { get; }
    public ulong[] ClientDigests { get; }
    public BattleState ServerState { get; }
    public BattleState ClientState { get; }
    public uint NextPublishTick { get; }
}
```

The vector owns only actual roster/state/input construction, serialization, framing, real TCP execution, decoding, Processor calls, Client Simulation, and result capture. It contains no expected Digest, expected final-state literal, expected result array, assertion, pass/fail logic, test framework, time, environment read, randomness, or file input.

The finite control counts 12 submissions and three authoritative payloads are allowed in the vector because they terminate the approved scenario; expected correctness remains independently asserted by the consumer tests.

## 10. Exact Eight-Test Matrix

The dependency-free runner registers exactly:

```text
1. ListenerBindsIpv4LoopbackOnOsAssignedPort
2. ServerReadLoopUsesReusableOffsetSegmentAcrossMultipleReads
3. ContinuousClientStreamRecoversTwelveSubmissionPayloadsInOrder
4. GapFillPublishesTicks100Through102AsIndependentPayloads
5. ContinuousServerStreamRecoversThreeAuthoritativePayloadsInOrder
6. ServerReadZeroBeforeTwelvePayloadsThrowsEndOfStreamException
7. ClientReadZeroBeforeThreePayloadsThrowsEndOfStreamException
8. RealTcpRoundTripMatchesApprovedAuthoritySequenceStatesAndDigests
```

Their non-overlapping primary responsibilities are:

1. assert the IPv4 loopback address, positive OS-assigned port, and exact Client remote endpoint;
2. assert only the Server-side reusable offset/count loop performs more than one `Read`;
3. assert all 12 recovered submission payloads equal the 12 original payloads byte-for-byte and in order;
4. assert Processor output counts are zero for calls 1-11 and three for call 12, returned payload arrays are independent, and mapped Frame Ticks are 100, 101, 102;
5. assert the Client performs more than one `Read` and the three recovered authority payloads equal the originals byte-for-byte and in order;
6. assert deterministic Server-side early EOF throws `EndOfStreamException`;
7. assert deterministic Client-side early EOF throws `EndOfStreamException`;
8. assert all three mapped Frames and Client intermediate states/Digests, `NextPublishTick == 103`, complete final state, Server/Client equality, and final Digest.

No test asserts an exact individual `NetworkStream.Read` return size. The exact suite result is:

```text
RESULT 8/8 passed
```

## 11. Deterministic EOF Fixtures

EOF fixture helpers are private members of `LoopbackTcpEndToEndTests.cs`; they are not part of the Golden API or production.

Server EOF fixture:

```text
obtain the actual 12 submission payloads from the independently owned Gate 8 vector
-> open a fresh IPv4 loopback listener and connection
-> Client writes the first 11 complete framed submissions
-> client.Client.Shutdown(SocketShutdown.Send)
-> Server recovers exactly 11 payloads
-> the next Server Read returns 0
-> throw EndOfStreamException because 12 were required
```

Client EOF fixture:

```text
obtain the actual three authoritative payloads from a successful LoopbackTcpGoldenVector.Run
-> open a fresh IPv4 loopback listener and connection
-> Server writes the first two complete framed authoritative payloads
-> acceptedClient.Client.Shutdown(SocketShutdown.Send)
-> Client recovers exactly two payloads
-> the next Client Read returns 0
-> throw EndOfStreamException because three were required
```

`Shutdown(SocketShutdown.Send)` exists only to generate deterministic EOF in these tests. It defines no production disconnect, half-close, or connection-lifecycle semantics. Fixtures do not hardcode protobuf wire bytes.

## 12. Success, Failure, and Resource Lifetime

Success requires all expected payload counts, all test assertions, and orderly disposal. A zero-byte read before the required count is failure. All other exceptions propagate and fail the test runner.

No socket receive/send timeout, gameplay timeout, wall-clock policy, retry, sleep-based synchronization, reconnect, recovery, or partial-success result is introduced. A bounded outer test-process watchdog may be used by the implementation procedure to detect a hung synchronous test executable; it is verification tooling outside product/test socket semantics and may terminate only the captured Gate 8 test process tree.

## 13. Ignore, Lockfile, and Protected Paths

Relative to the frozen Gate 7 base, `.gitignore` adds exactly one line:

```gitignore
!Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj
```

No other `.gitignore` change is allowed.

`Packages/manifest.json` and `Packages/packages-lock.json` committed diffs remain zero. Gate 8 adds no package and does not run Unity package import to create a lockfile entry.

Relative to `0a73d924944a192c16c12260447c63272b727899`, committed diff must be zero for:

```text
Packages/com.locksteparena.simulation/
Packages/com.locksteparena.protocol/
Packages/com.locksteparena.stream-framing/
Server/LockstepArena.Server.FrameSync/
Server/LockstepArena.Server.ProtocolAuthority/
Assets/
ProjectSettings/
Packages/manifest.json
Packages/packages-lock.json
all pre-existing Tests/ projects and sources
```

Only the new Gate 8 test project, the one exact `.gitignore` exception, and the Gate 8 Architecture/Plan/Evidence documentation may differ.

The ordinary checkout must retain exactly these user-owned modifications, untouched and uncommitted by Gate 8:

```text
 M Assets/Settings/Mobile_RPAsset.asset
 M ProjectSettings/ShaderGraphSettings.asset
```

## 14. Restore-Assets Preflight

Before the final 13-build `--no-restore` matrix, the implementation procedure resolves and verifies each project's effective `ProjectAssetsFile`.

If an existing project's restore asset is missing, it runs `dotnet restore` for that exact project using its existing frozen project contract. It must not change a package, version, project reference, dependency source, or generated source. A network/restore failure stops the Gate. Any unexpected repository diff is investigated and rejected. If any restore occurs, the complete 13-build matrix restarts at build 1.

## 15. Exact Release Build Matrix

The following 13 projects are built separately in Release with `--no-restore`; every build must report zero warnings and zero errors:

```text
1.  Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj
2.  Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj
3.  Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj
4.  Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj
5.  Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj
6.  Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj
7.  Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj
8.  Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj
9.  Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj
10. Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj
11. Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj
12. Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj
13. Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj
```

## 16. .NET and Unity Regression Matrix

Fresh .NET results must be:

```text
Gate 3 Simulation:         RESULT 38/38 passed
Gate 4 FrameSync:          RESULT 32/32 passed
Gate 5 Protocol:           RESULT 35/35 passed
Gate 6 ProtocolAuthority:  RESULT 24/24 passed
Gate 7 StreamFraming:      RESULT 32/32 passed
Gate 8 TCP End-to-End:     RESULT 8/8 passed
Gate 3 Server Golden:      Tick=1000 Players=4 Digest=89A7DD66F8D9E871
Gate 8 final State Tick:   103
Gate 8 final Digest:       386C4BB11A7EB7E0
```

Unity 6000.3.10f1 runs three separate fresh EditMode jobs from the Gate 8 worktree. Process exit code alone is insufficient; every result is parsed from a newly generated NUnit XML file.

Gate 7 framing regression:

```text
assembly = LockstepArena.StreamFraming.Editor.Tests
total=1 passed=1 failed=0
UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden = Passed
```

Gate 5 protocol regression:

```text
assembly = LockstepArena.Protocol.Editor.Tests
total=2 passed=2 failed=0
GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads = Passed
UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector = Passed
```

Gate 3 Simulation regression:

```text
assembly = LockstepArena.Simulation.Editor.Tests
total>=1 failed=0
UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector = Passed
```

After each Unity run, only inspected, confirmed worktree-local Unity serialization changes under Assets/ProjectSettings may be restored. Broad reset/clean is forbidden. The ordinary checkout is never used as a test workaround.

## 17. Final Audits

Final acceptance audits prove:

- all 13 builds have zero warnings/errors;
- exact .NET and Unity results match Section 16;
- final Server and Client Tick/State/Digest match Section 8;
- project XML and four-file layout match Section 3;
- the `.gitignore` diff is exactly one approved exception;
- manifest, lockfile, protected production, and pre-existing tests have zero committed diff;
- no package `bin`, `obj`, LockstepArena build DLL, unexpected generated source, or tracked build artifact exists;
- no copied Gate 6/Gate 7 helper or second Golden source exists;
- no symlink/junction, copy/sync/cleanup script, or external test dependency was added;
- all `TcpListener`, `TcpClient`, `NetworkStream`, `IPAddress`, `IPEndPoint`, `AddressFamily`, `SocketShutdown`, and related TCP symbols introduced by Gate 8 occur only under the new Gate 8 test directory;
- no TCP/Socket symbol entered production;
- the ordinary checkout still contains only its two user-owned modifications.

## 18. Explicit Exclusions

Gate 8 does not add:

- TCP, UDP, KCP, Socket, or NetworkStream production code;
- Unity TCP code or public/LAN deployment;
- async, Task, threads, background receive loops, or connection/session abstractions;
- retry, reconnect, heartbeat, recovery, cancellation protocol, or disconnect policy;
- opcode, envelope, message type registry, router, or dispatcher;
- Login, Room, Session, account, or authentication;
- TickClock, fixed InputDelay, wall-clock/gameplay timeout, or missing-input replacement;
- Prediction, Dirty Frame, Snapshot, Rollback, Replay, View, or Combat;
- TLS, compression, encryption, pooling, generic transport abstraction, middleware, event bus, DI, or framework expansion.

Gate 8 ends after proving the frozen finite scenario over real loopback TCP and submitting its Final Implementation Handoff. It does not begin Gate 9 or any production TCP work.

## 19. Implementation Evidence

### 19.1 Commit and scope identity

- Frozen Gate 7 comparison base: `0a73d924944a192c16c12260447c63272b727899`.
- Final approved Gate 8 Planning HEAD: `58f3c1678c3efa2506c9489842716a200d5b5698`.
- Successful-path implementation: `f7ebdcc` (`test: prove real TCP authority round trip`).
- Deterministic EOF implementation and evidence-commit parent: `ed1b5deb9e094cce40b6856a4b688f3274de9d01` (`test: prove deterministic TCP end-of-stream failures`).
- The implementation adds only the four-file `Tests/LockstepArena.TcpEndToEnd.Tests/` project and the single approved `.gitignore` exception. It adds no production assembly or Unity TCP code.

### 19.2 Restore and Release builds

The final restore-assets preflight resolved the effective `ProjectAssetsFile` for all 13 frozen projects. Missing assets were restored only through each existing project contract, so `restoreOccurred=True`; restore produced no repository diff and the complete build matrix restarted at build 1.

All 13 independent Release `--no-restore` builds completed with `0 warnings / 0 errors`:

```text
1.  LockstepArena.Simulation
2.  LockstepArena.Server.FrameSync
3.  LockstepArena.Server.Verification
4.  LockstepArena.Simulation.Tests
5.  LockstepArena.Server.FrameSync.Tests
6.  LockstepArena.Protocol.CodeGen
7.  LockstepArena.Protocol
8.  LockstepArena.Server.Protocol.Tests
9.  LockstepArena.Server.ProtocolAuthority
10. LockstepArena.Server.ProtocolAuthority.Tests
11. LockstepArena.StreamFraming
12. LockstepArena.StreamFraming.Tests
13. LockstepArena.TcpEndToEnd.Tests
```

### 19.3 Fresh .NET execution

```text
Gate 3 Simulation:         RESULT 38/38 passed
Gate 4 FrameSync:          RESULT 32/32 passed
Gate 5 Protocol:           RESULT 35/35 passed
Gate 6 ProtocolAuthority:  RESULT 24/24 passed
Gate 7 StreamFraming:      RESULT 32/32 passed
Gate 8 TCP End-to-End:     RESULT 8/8 passed
Gate 3 Server Golden:      Tick=1000 Players=4 Digest=89A7DD66F8D9E871
```

The Gate 8 executable ran under the external 30-second process watchdog and completed in `0.426` seconds. The watchdog did not fire, and neither production nor tests set a socket or gameplay timeout.

The passing Gate 8 assertions prove:

- the listener bound IPv4 `IPAddress.Loopback` to an OS-assigned port greater than zero and the explicit IPv4 client connected to that exact port;
- the Server and Client each performed more than one real `NetworkStream.Read`, without asserting any individual Read size;
- one continuous Client-to-Server stream recovered all 12 submission payloads byte-for-byte and in order;
- processor calls 1 through 11 published zero payloads and call 12 published three independent authoritative payloads for Ticks 100, 101, and 102;
- one continuous Server-to-Client stream recovered those three payloads byte-for-byte and in order;
- both EOF fixtures recovered exactly 11 submissions or two authoritative payloads before the next zero-byte Read raised `EndOfStreamException`;
- two independent real-TCP Golden executions produced field-for-field equal authoritative Domain Frame sequences.

The Client state oracle passed at every authoritative Tick:

```text
State Tick 101
Slot0 X=-200 Z=0    Aim=10100
Slot1 X=200  Z=0    Aim=20100
Slot2 X=0    Z=-200 Aim=30100
Slot3 X=0    Z=200  Aim=40100
Digest D95809E1EB5CDDAA

State Tick 102
Slot0 X=-200 Z=100  Aim=10101
Slot1 X=200  Z=-100 Aim=20101
Slot2 X=100  Z=-200 Aim=30101
Slot3 X=-100 Z=200  Aim=40101
Digest A96B83267DD72A7D

State Tick 103
Slot0 X=-300 Z=100  Aim=10102
Slot1 X=300  Z=-100 Aim=20102
Slot2 X=100  Z=-300 Aim=30102
Slot3 X=-100 Z=300  Aim=40102
Digest 386C4BB11A7EB7E0
```

Final Server and Client rosters and full states were equal, both simulations were at Tick 103 with Digest `386C4BB11A7EB7E0`, and `NextPublishTick` was 103.

### 19.4 Fresh Unity 6000.3.10f1 execution

The approved Planning command contained `-quit`. On Unity 6000.3.10f1 it exited after initial batch import before Test Runner execution. No NUnit XML was generated. An independent execution amendment approved removing only `-quit`. Final Unity regressions used the same frozen project path, platform, assembly filters, Gate 3 test filter, result paths, and expectations with hidden `Start-Process -Wait`. The failed `-quit` run is not treated as test evidence.

Each amended run created and then passed strict parsing of a fresh NUnit XML file:

```text
Gate 7 XML: .artifacts/gate8-unity-amended/gate7/results.xml
Fresh UTC:  2026-09-04T06:26:49.5952429Z
total=1 passed=1 failed=0
UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden = Passed

Gate 5 XML: .artifacts/gate8-unity-amended/gate5/results.xml
Fresh UTC:  2026-09-04T06:29:20.5714526Z
total=2 passed=2 failed=0
GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads = Passed
UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector = Passed

Gate 3 XML: .artifacts/gate8-unity-amended/gate3/results.xml
Fresh UTC:  2026-09-04T06:30:37.7424502Z
total=1 passed=1 failed=0
UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector = Passed
```

The Unity process exit codes were zero but were not used as the PASS criterion. Each PASS came from its fresh XML totals and unique named `test-case`. After every successful Unity run, the exact worktree status and Assets/ProjectSettings diff were inspected; no serialization change remained and no restore was required. The ordinary checkout was never used for Unity verification.

### 19.5 Final audits

- Simulation, Protocol, StreamFraming, FrameSync, ProtocolAuthority, Assets, ProjectSettings, manifest, packages-lock, and every pre-existing test have zero frozen-base committed and working-tree diff.
- `.gitignore` has exactly one added line and no deletion: `!Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj`.
- The Gate 8 project has exactly four tracked authored files and exactly the four approved direct ProjectReferences. It has no direct FrameSync reference, PackageReference, external Compile Include, or test framework dependency.
- All 27 TCP source-symbol matches are below `Tests/LockstepArena.TcpEndToEnd.Tests/`; production has no TCP, Socket, NetworkStream, async/thread, timeout, UDP, or KCP addition.
- `SocketShutdown.Send` occurs exactly twice, only in the two private deterministic EOF fixtures. No socket timeout property is set.
- Every successful Read feeds the same reusable receive buffer, offset, and returned `bytesRead` directly into the decoder. No exact-sized intermediary receive array is created.
- No copied or linked Gate 6/Gate 7 helper, external Golden, symlink/junction, copy/sync/cleanup script, package `bin/obj`, package LockstepArena DLL, or tracked build artifact exists.
- The Gate 8 worktree was clean before this evidence-only edit. The ordinary checkout still contains exactly the two untouched user-owned modifications to `Assets/Settings/Mobile_RPAsset.asset` and `ProjectSettings/ShaderGraphSettings.asset`.

Gate 8 stops after this evidence and Final Implementation Handoff. It does not begin Gate 9, production TCP, KCP, TickClock, or InputDelay.
