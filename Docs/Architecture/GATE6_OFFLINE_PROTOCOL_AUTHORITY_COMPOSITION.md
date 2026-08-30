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
