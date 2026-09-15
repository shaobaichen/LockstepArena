# Gate 6: Offline Protocol-Aware Authority Composition

## 1. Status and Approved Baseline

Gate 6 architecture is approved for planning only.

```text
Approved Base: 72764ebcd2f0fbfa9f74ad95e4e61bf12c9709b2
Scope: Offline Protocol-Aware Authority Composition
```

This Gate composes the already approved Gate 5 Protocol boundary, Gate 4 authoritative multi-Tick coordinator, and Shared BattleSimulation into one offline Server authority application path. It does not add transport or time policy.

## 2. Learning Objective

Gate 3 proved strict complete multiplayer FrameData. Gate 4 proved continuous authoritative publication across multiple Ticks. Gate 5 proved explicit Protobuf-to-Domain mapping and dual-runtime deterministic round trips.

Gate 6 proves the next composition fact:

```text
complete PlayerInputSubmission protobuf payload
→ parse and map
→ authoritative coordinator submission
→ zero, one, or many newly published Frames
→ Server Simulation consumption
→ one independent authoritative protobuf payload per Frame
→ offline Client parse, map, and Simulation consumption
→ identical final Server and Client deterministic state
```

The input is one complete Protobuf payload supplied by the caller. Gate 6 does not discover message boundaries in a byte stream.

## 3. Production Ownership and Dependency Topology

The only new production assembly is:

```text
Server/LockstepArena.Server.ProtocolAuthority/
└── LockstepArena.Server.ProtocolAuthority
```

Its direct dependencies are honest and explicit:

```text
LockstepArena.Server.ProtocolAuthority
├── LockstepArena.Protocol
├── LockstepArena.Server.FrameSync
└── LockstepArena.Simulation
```

No existing production assembly changes dependency direction. In particular:

```text
Simulation -/→ Protocol
FrameSync -/→ Protocol
FrameSync -/→ ProtocolAuthority
Protocol -/→ FrameSync
```

The Gate 3 Simulation package, Gate 4 FrameSync production assembly, and Gate 5 Protocol package remain committed-diff clean relative to the approved base.

## 4. ProtocolAuthorityProcessor

The only initial production type is:

```csharp
namespace LockstepArena.Server.ProtocolAuthority
{
    public sealed class ProtocolAuthorityProcessor
}
```

Its complete production state is:

```csharp
private readonly AuthoritativeFrameCoordinator _coordinator;
private readonly BattleSimulation _serverSimulation;
private bool _faulted;
```

Its complete public API is:

```csharp
public ProtocolAuthorityProcessor(
    BattleState initialState,
    uint maxFutureTickOffset,
    int authoritativeHistoryCapacity);

public BattleState ServerState { get; }

public uint NextPublishTick { get; }

public byte[][] SubmitPlayerInputPayload(byte[] completePayload);
```

The Processor does not expose its Coordinator or BattleSimulation. It does not expose Server state history, Digest history, callbacks, events, observers, a fault property, or a recovery API. Its caller uses it serially; no locking or concurrency abstraction is added.

## 5. Bootstrap Contract

`initialState` must be non-null. The Processor constructs both owned components from the same immutable bootstrap facts:

```text
Coordinator roster = initialState.Roster
Coordinator NextPublishTick = initialState.Tick
Server Simulation state = initialState
```

Therefore construction establishes:

```text
ServerState.Tick == NextPublishTick
ServerState.Roster structurally equals Coordinator roster
```

The Processor delegates `maxFutureTickOffset` and `authoritativeHistoryCapacity` validation to the existing Coordinator constructor. It does not copy or restate Gate 4 admission rules. `initialState.Tick == uint.MaxValue` represents the existing exhausted state and does not create a second exhaustion flag.

## 6. Submission Operation Order

The exact operation order is:

```text
1. sticky-fault check
2. completePayload null check
3. PlayerInputSubmissionMessage.Parser.ParseFrom
4. ProtocolMapper.ToDomain
5. AuthoritativeFrameCoordinator.Submit
6. return Array.Empty<byte[]>() when publication is empty
7. process a non-empty publication inside the sticky-fault protected block
8. return the complete output array only after all Frames succeed
```

Each Frame in a non-empty publication is processed in the Coordinator-provided array order:

```text
BattleSimulation.Step(frame)
→ ProtocolMapper.ToWire(frame)
→ serialize one AuthoritativeFrameMessage with ToByteArray()
```

A Coordinator `FrameData[]` publication batch is not a wire batch. A publication `[100, 101, 102]` produces three independent payloads, not an `AuthoritativeFrameBatchMessage`, packet, or envelope.

Normal successful completion re-establishes:

```text
ServerState.Tick == NextPublishTick
output payload count == publication Frame count
output authoritative Ticks are strictly continuous and increasing
```

## 7. Exception and Sticky-Fault Contract

Pre-publication failures retain their existing exception categories and do not fault the Processor:

```text
null payload → ArgumentNullException
malformed protobuf → InvalidProtocolBufferException
invalid mapped representation → ProtocolMappingException
valid Domain input rejected by authority → existing Coordinator exception
```

Once `Coordinator.Submit` returns a non-empty publication, authority has already advanced. Any subsequent failure while allocating the output, stepping Server Simulation, mapping an authoritative Frame, or serializing its payload sets the private sticky fault and rethrows the original exception:

```csharp
catch
{
    _faulted = true;
    throw;
}
```

There is no Coordinator rollback, no rollback of already stepped Server Frames, and no partial return. Every later Submit checks `_faulted` before null validation or parsing and immediately throws `InvalidOperationException`.

This is invariant containment, not recovery. There is no reset, retry, compensation, rollback log, transaction coordinator, or fault hierarchy.

## 8. Output Ownership

An empty publication may return the shared `Array.Empty<byte[]>()` instance. Every non-empty call owns a new outer array, and every element is the distinct result of one `ToByteArray()` call.

The Processor retains no generated message or payload reference. A caller may replace outer-array elements or mutate returned bytes without changing Server state, Coordinator authority, retained history, or another payload in the same result.

Immutable FrameData objects are reused as produced by the Coordinator; they are not deep-copied.

## 9. Offline Client Proof

Gate 6 creates no production Client assembly. Test code owns an independent Client ActiveRoster and BattleState with the same structure and values as the Server bootstrap but different object instances.

Each returned authoritative payload is consumed as:

```text
AuthoritativeFrameMessage.Parser.ParseFrom
→ ProtocolMapper.ToDomain(clientExpectedRoster)
→ Client BattleSimulation.Step
→ StateDigest.Compute
```

The Client compares each intermediate Digest against a frozen independent oracle. The Processor exposes only its final ServerState after the complete publication method returns. Gate 6 does not claim that production exposes intermediate Server states.

Gate 5 already proved Protocol and Simulation execution in Unity. Gate 6 retains the Gate 5 Unity and Gate 3 Unity regressions but does not make Unity reference a Server assembly and does not create a Gate 6 Unity test assembly.

## 10. Frozen Gap-Fill Golden Vector

### Roster

| Slot | PlayerId |
|---:|---:|
| 0 | `0x0102030405060708` |
| 1 | `0x000000000000002A` |
| 2 | `0xFFEEDDCCBBAA0099` |
| 3 | `0x00000000000F4243` |

PlayerId numeric order intentionally differs from PlayerSlot order.

### Initial State

```text
Tick = 100
Slot0 = X -300, Z 0,    Aim 1000
Slot1 = X 300,  Z 0,    Aim 2000
Slot2 = X 0,    Z -300, Aim 3000
Slot3 = X 0,    Z 300,  Aim 4000
```

### Logical Inputs

| Frame Tick | Slot | MoveX | MoveZ | Aim |
|---:|---:|---:|---:|---:|
| 100 | 0 | 1 | 0 | 10100 |
| 100 | 1 | -1 | 0 | 20100 |
| 100 | 2 | 0 | 1 | 30100 |
| 100 | 3 | 0 | -1 | 40100 |
| 101 | 0 | 0 | 1 | 10101 |
| 101 | 1 | 0 | -1 | 20101 |
| 101 | 2 | 1 | 0 | 30101 |
| 101 | 3 | -1 | 0 | 40101 |
| 102 | 0 | -1 | 0 | 10102 |
| 102 | 1 | 1 | 0 | 20102 |
| 102 | 2 | 0 | -1 | 30102 |
| 102 | 3 | 0 | 1 | 40102 |

Every logical input is carried by its own complete serialized `PlayerInputSubmissionMessage` payload.

### Approved Arrival Order

```text
Tick100: Slot 0, 2, 1
Tick101: Slot 3, 1, 0, 2
Tick102: Slot 2, 0, 3, 1
Tick100: Slot 3
```

All calls before the final Tick100 Slot3 submission return empty output. The final call returns exactly three independent authoritative payloads in Tick order `100, 101, 102`.

## 11. Frozen State and Digest Oracles

The approved Digests use the Gate 3 canonical 80-byte state stream and explicit little-endian FNV-1a64.

After authoritative Frame Tick100:

```text
State Tick = 101
Slot0 = X -200, Z 0,    Aim 10100
Slot1 = X 200,  Z 0,    Aim 20100
Slot2 = X 0,    Z -200, Aim 30100
Slot3 = X 0,    Z 200,  Aim 40100
Digest = 0xD95809E1EB5CDDAA
```

After authoritative Frame Tick101:

```text
State Tick = 102
Slot0 = X -200, Z 100,  Aim 10101
Slot1 = X 200,  Z -100, Aim 20101
Slot2 = X 100,  Z -200, Aim 30101
Slot3 = X -100, Z 200,  Aim 40101
Digest = 0xA96B83267DD72A7D
```

After authoritative Frame Tick102:

```text
State Tick = 103
Slot0 = X -300, Z 100,  Aim 10102
Slot1 = X 300,  Z -100, Aim 20102
Slot2 = X 100,  Z -300, Aim 30102
Slot3 = X -100, Z 300,  Aim 40102
Final Digest = 0x386C4BB11A7EB7E0
```

The Golden vector contains actual inputs and actual outputs only. Expected states and Digests live in the test consumer, never in production or the vector.

## 12. Test Matrix

The dependency-free Gate 6 .NET suite registers exactly 24 named tests:

| Group | Count |
|---|---:|
| Bootstrap and public contract | 4 |
| Parse, mapping, and authority rejection | 7 |
| 0/1/N publication and ownership | 7 |
| Sticky invariant containment | 2 |
| Gap-fill Golden and determinism | 4 |
| Total | 24 |

The post-publication failure test locates the Processor's unique private `BattleSimulation` field by type, not by private field name. It deliberately advances that Simulation before authority publication, then proves through public `NextPublishTick` that publication occurred before the original Simulation exception escaped. No production test hook or `InternalsVisibleTo` is added.

Expected final result:

```text
RESULT 24/24 passed
```

## 13. Project Layout and Build Contracts

Production:

```text
Server/LockstepArena.Server.ProtocolAuthority/
├── LockstepArena.Server.ProtocolAuthority.csproj
└── ProtocolAuthorityProcessor.cs
```

Tests:

```text
Tests/LockstepArena.Server.ProtocolAuthority.Tests/
├── LockstepArena.Server.ProtocolAuthority.Tests.csproj
├── Program.cs
├── ProtocolAuthorityProcessorTests.cs
├── ProtocolAuthorityErrorTests.cs
└── Gate6GapFillGoldenVector.cs
```

Both projects use .NET 8, C# 12, nullable enabled, implicit usings disabled, and warnings as errors. The test executable also sets `BuildInParallel=false`, following the existing dependency-free test pattern. No solution, package, asmdef, extra NuGet package, Directory.Build.props, or build framework is added.

The approved baseline's `*.csproj` ignore rule requires exactly these two exceptions:

```gitignore
!Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj
!Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj
```

No broader ignore exception is permitted.

## 14. Regression and Acceptance Matrix

Final evidence requires ten individual Release builds with zero warnings and zero errors: the Gate 5 eight-project matrix plus the new production and test projects.

Runtime evidence requires:

```text
Gate 3 Simulation suite: RESULT 38/38 passed
Gate 4 FrameSync suite: RESULT 32/32 passed
Gate 5 Protocol suite: RESULT 35/35 passed
Gate 6 ProtocolAuthority suite: RESULT 24/24 passed
Gate 3 Server Golden: Digest=89A7DD66F8D9E871
Gate 5 Unity assembly-filtered suite: 2/2 passed, 0 failed
Gate 3 Unity named Golden: 1/1 passed, 0 failed
Gate 5 pinned Protobuf regeneration: exactly one proto, one generated .g.cs, diff clean
```

Unity acceptance uses fresh NUnit XML and named test results, not process exit alone. A Unity license or instance-lock failure stops verification; the ordinary checkout is not used as a workaround.

## 15. Committed-Diff and Audit Contract

Relative to `72764ebcd2f0fbfa9f74ad95e4e61bf12c9709b2`, committed diff must remain empty under:

```text
Packages/com.locksteparena.simulation/
Server/LockstepArena.Server.FrameSync/
Packages/com.locksteparena.protocol/
Assets/
ProjectSettings/
Packages/manifest.json
Packages/packages-lock.json
```

Implementation changes are limited to the two Gate 6 project directories and two exact `.gitignore` exceptions. Final audits also require no copy/sync wrapper, symlink, junction, new DLL, Client production assembly, network type, time policy, generic handler, router, event system, DI, middleware, transaction, or recovery framework.

The ordinary checkout must continue to contain exactly the two user-owned modifications:

```text
 M Assets/Settings/Mobile_RPAsset.asset
 M ProjectSettings/ShaderGraphSettings.asset
```

## 16. Explicitly Out of Scope

Gate 6 does not implement TCP, UDP, KCP, Socket, stream framing, length prefix, envelope, opcode, connection, Session, Login, Room, TickClock, InputDelay, timeout, missing-input replacement, Prediction, Dirty Frame, Snapshot, Rollback, Replay, State Sync, reconnect, heartbeat, Client production/network lifecycle, Unity View, Combat, router, handler framework, event bus, middleware, scheduler, DI, factory, transaction coordinator, retry, or recovery.

## 17. Planning and Exit Contract

Planning isolation is:

```text
Worktree: .worktrees/gate6-protocol-authority-composition
Branch: codex/gate6-protocol-authority-composition
Exact Base: 72764ebcd2f0fbfa9f74ad95e4e61bf12c9709b2
```

The planning commit contains only this architecture document and the approved implementation plan. No `.gitignore`, project, production, test, package, Unity, generated, dependency, or configuration implementation change is permitted before independent Planning PASS.

Implementation evidence is intentionally absent from the planning commit. After planning is pushed, work stops until independent approval.

## 18. Implementation Evidence

Gate 6 was implemented and verified in the isolated worktree on 2026-08-31. The approved comparison baseline remained:

```text
72764ebcd2f0fbfa9f74ad95e4e61bf12c9709b2
```

The approved planning commits and implementation checkpoints were:

```text
9ec1f2c63ae804be74b266a111cf060ddce6036b docs: plan Gate 6 protocol authority composition
f9e4ee65de5ccc7037453d4672e9e54a679fd5c0 docs: amend Gate 6 implementation start state
d44ee6a463f763429c64c21e1c59482d0d0ddba8 build: add server protocol authority projects
f1abb1ef042156d4359b7039a36a21eb5cce2e1c feat: compose protobuf inputs into authority outputs
29022e3a11a5667d3e071e4953c87b2aab073078 feat: contain post-publication authority failures
5fdfe98f1b0caaec7c769751de16e78975ba3fb7 test: prove offline protocol authority composition
```

### 18.1 Fresh Pinned Protocol Regeneration

The final verification began from clean implementation HEAD `5fdfe98f1b0caaec7c769751de16e78975ba3fb7`. Both `PROTOBUF_PROTOC` and `Protobuf_ProtocFullPath` overrides were absent. Fresh deterministic regeneration used:

```text
Grpc.Tools: 2.83.0
Resolved protoc: C:\Users\张晨旭\.nuget\packages\grpc.tools\2.83.0\tools\windows_x64\protoc.exe
Bundled protoc version: libprotoc 35.1
Resolved protoc SHA-256: EA33FADF8FC93D8445D3F39A98E265224F53B1B5DB4196DE0B03B5724120F767
Proto files: 1
Tracked generated .g.cs files: 1
Physical generated .g.cs files: 1
Generated Git blob: 32993ff553600d4fec0a1e2275f50317afda1fd5
Tracked Git blob: 32993ff553600d4fec0a1e2275f50317afda1fd5
```

The CodeGen rebuild completed with 0 warnings and 0 errors. Schema plus Generated `git diff --exit-code` passed. The generated output remained the one tracked physical source and no global PATH protoc or override was used.

### 18.2 Fresh Release Builds and .NET Results

All ten approved projects were built individually in Release configuration. Every build completed with 0 warnings and 0 errors:

```text
Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj
Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj
Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj
Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj
Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj
Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj
Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj
Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj
Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj
Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj
```

Fresh runtime results were:

```text
Gate 3 Simulation: RESULT 38/38 passed
Gate 4 FrameSync: RESULT 32/32 passed
Gate 5 Protocol: RESULT 35/35 passed
Gate 6 ProtocolAuthority: RESULT 24/24 passed
Gate 3 Server Golden: Tick=1000 Players=4 Digest=89A7DD66F8D9E871
```

The Gate 6 named Golden tests independently verified the three Client post-Step Digests:

```text
State Tick 101: D95809E1EB5CDDAA
State Tick 102: A96B83267DD72A7D
State Tick 103: 386C4BB11A7EB7E0
```

The final Server and Client states both reached Tick 103 with `NextPublishTick == 103`:

```text
Slot0 = X -300, Z 100,  Aim 10102
Slot1 = X 300,  Z -100, Aim 20102
Slot2 = X 100,  Z -300, Aim 30102
Slot3 = X -100, Z 300,  Aim 40102
Final Digest = 0x386C4BB11A7EB7E0
```

The approved and alternate submission orders produced the same canonical authoritative Domain Frame sequence and final Server state. The pure `Gate6GapFillGoldenVector.cs` contains none of the expected state or Digest literals; those literals remain only in the independent consumer tests.

### 18.3 Composition, Ownership, and Sticky-Fault Evidence

The 24-test suite proves:

- null, malformed Protobuf, mapper, old/future Tick, ownership, and duplicate-submission failures occur before authority publication and do not fault the Processor;
- incomplete input returns `Array.Empty<byte[]>()`, while a gap fill returns one independent payload per published Frame in Coordinator order;
- output arrays and per-Frame byte buffers are independently owned, and caller mutation cannot affect retained Server state;
- after non-empty authority publication, a later Server `Step` failure rethrows its original exception, leaves authority advanced, returns no partial output, and sets the private sticky fault;
- the faulted Processor rejects the next call with `InvalidOperationException` before even null-payload validation;
- the fault test locates the unique private `BattleSimulation` field by type and does not freeze a private field name or require a production test hook.

Production remains one `ProtocolAuthorityProcessor` with exactly three private fields: the Coordinator, Server Simulation, and sticky fault flag. Its public surface is the constructor, `ServerState`, `NextPublishTick`, and `SubmitPlayerInputPayload`. It consumes the Coordinator publication array in order and serializes every immutable Frame independently.

### 18.4 Fresh Unity NUnit Evidence

Both final Unity jobs used Unity 6000.3.10f1 (`e35f0c77bd8e`) in the Gate 6 worktree. Each log recorded `Test run completed. Exiting with code 0 (Ok).` Acceptance was based on fresh NUnit XML rather than process launch or exit alone.

Gate 5 ran only assembly `LockstepArena.Protocol.Editor.Tests`:

```text
XML: .artifacts/gate6/final-resume/gate5-results.xml
Log: .artifacts/gate6/final-resume/gate5-editor.log
total=2 passed=2 failed=0 result=Passed
GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads = Passed
UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector = Passed
```

Gate 3 ran assembly `LockstepArena.Simulation.Editor.Tests` with the named Golden filter:

```text
XML: .artifacts/gate6/final-resume/gate3-results.xml
Log: .artifacts/gate6/final-resume/gate3-editor.log
total=1 passed=1 failed=0 result=Passed
UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector = Passed
```

After the first run, the exact Unity-generated diffs to `Assets/Settings/Mobile_RPAsset.asset`, `ProjectSettings/EditorBuildSettings.asset`, and `ProjectSettings/ShaderGraphSettings.asset` were inspected and restored individually. After the second run, the exact `Assets/Settings/Mobile_RPAsset.asset` serialization upgrade was inspected and restored individually. No broad reset or clean was used.

### 18.5 Final Boundary and Scope Audits

Relative to the approved baseline, committed and working-tree checks reported zero changes under Simulation, Server FrameSync, Protocol, Assets, ProjectSettings, `Packages/manifest.json`, and `Packages/packages-lock.json`. The `.gitignore` diff contains only the two exact authored Gate 6 csproj exceptions.

The final audits also confirmed:

- production has direct references only to Protocol, Server FrameSync, and Simulation;
- exactly one Gate 6 production class and one production assembly were added;
- no package, asmdef, solution, Directory.Build.props, generated source, third-party dependency, DLL, script, symlink, junction, or Client production assembly was added;
- no package-local `bin`, `obj`, or LockstepArena DLL exists;
- no `InternalsVisibleTo`, injectable serializer, callback, observer, interface, factory, DI, router, handler framework, event bus, middleware, transaction, retry, recovery, or other speculative abstraction was introduced;
- no TCP, UDP, KCP, Socket, framing, Room, Login, Session, TickClock, InputDelay, timeout, missing-input replacement, Prediction, Snapshot, Rollback, Replay, reconnect, heartbeat, Unity View, or Combat implementation exists;
- all three corrected Digest values are present and none of the three rejected Digest values occurs in Architecture, Planning, production, tests, or evidence.

The final ordinary-checkout audit still reported exactly the two user-owned changes and nothing else:

```text
 M Assets/Settings/Mobile_RPAsset.asset
 M ProjectSettings/ShaderGraphSettings.asset
```

Gate 6 remains strictly offline and stops at protocol-aware authority composition. Gate 7 work has not begun.
