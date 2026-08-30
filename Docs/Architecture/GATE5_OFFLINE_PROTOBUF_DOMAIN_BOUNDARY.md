# Gate 5: Offline Protobuf Domain Boundary & Dual-Runtime Round Trip

## 1. Status and Approved Base

This document is the approved-scope architecture for Gate 5. Planning is based exactly on Gate 4 final commit:

```text
cd09b89739284d5fe36e1d5c825a3fd1578e6768
```

Gate 5 proves that Unity Client and .NET Server can compile and execute one physical set of generated Protobuf C# contracts, map them explicitly into the Gate 3 Domain, and preserve deterministic Simulation state and digest. It remains offline and contains no transport.

## 2. Learning Objective

The proof chain is:

```text
Domain data
    -> explicit Domain-to-Protobuf mapping
    -> Protobuf serialization
    -> Protobuf parsing
    -> strict Protobuf-to-Domain mapping
    -> Gate 3 Domain validation
    -> FrameData
    -> BattleSimulation.Step
    -> identical deterministic BattleState and StateDigest
```

The core boundary is:

```text
one .proto
    -> pinned Grpc.Tools / bundled protoc
    -> one tracked generated C# file
    -> LockstepArena.Protocol
    -> explicit ProtocolMapper
    -> LockstepArena.Simulation Domain
```

Successful Protobuf parsing is not equivalent to successful Domain mapping. Generated messages are mutable transport containers; they do not own roster validity, complete-frame validity, canonical execution order, or Simulation step eligibility.

## 3. Ownership and Dependency Direction

Gate 5 adds one embedded Unity package:

```text
Packages/com.locksteparena.protocol
```

Its production assembly is `LockstepArena.Protocol`. Generated DTOs and the explicit mapper remain in this single assembly for Gate 5.

Dependency direction is strictly:

```text
LockstepArena.Protocol
    -> LockstepArena.Simulation
    -> no reverse dependency
```

`LockstepArena.Protocol` also depends on the pinned `Google.Protobuf` runtime. `LockstepArena.Simulation` remains Unity-free and Protobuf-free. Existing `LockstepArena.Server.FrameSync` production remains Domain-only and gains no Protocol dependency.

The Protocol assembly does not own a coordinator, BattleSimulation instance, state, network connection, clock, or lifecycle. It only converts between wire DTOs and existing immutable Domain values.

## 4. Package and Source Layout

```text
Packages/
  com.locksteparena.protocol/
    package.json
    Third Party Notices.md
    Schema/
      lockstep_arena_protocol.proto
    Runtime/
      Directory.Build.props
      LockstepArena.Protocol.asmdef
      LockstepArena.Protocol.csproj
      Generated/
        LockstepArenaProtocol.g.cs
      Mapping/
        ProtocolMapper.cs
        ProtocolMappingException.cs
      Plugins/
        Google.Protobuf.dll
        Google.Protobuf.dll.meta
    Tests/Editor/
      LockstepArena.Protocol.Editor.Tests.asmdef
      GoogleProtobufDependencyPreflightTests.cs
      UnityProtocolGoldenVectorTests.cs
      Gate5ProtocolGoldenVector.cs

Tools/
  LockstepArena.Protocol.CodeGen/
    LockstepArena.Protocol.CodeGen.csproj

Tests/
  LockstepArena.Server.Protocol.Tests/
    LockstepArena.Server.Protocol.Tests.csproj
    Program.cs
    ProtocolParserTests.cs
    ProtocolRosterMappingTests.cs
    ProtocolInputMappingTests.cs
    ProtocolFrameMappingTests.cs
    ProtocolDeterminismTests.cs
```

Unity-authored package assets that require stable identity or importer configuration have tracked `.meta` files. `Google.Protobuf.dll.meta` is mandatory and records the approved Plugin Importer settings.

There is exactly one tracked `.proto` and exactly one tracked generated `.g.cs`. Unity and .NET compile that same physical generated file. There is no copied schema, copied generated source, symlink, junction, synchronization script, or committed LockstepArena production DLL.

## 5. Unity Package Contract

`package.json` declares:

```json
{
  "name": "com.locksteparena.protocol",
  "version": "0.1.0",
  "displayName": "Lockstep Arena Protocol",
  "unity": "6000.3",
  "dependencies": {
    "com.locksteparena.simulation": "0.1.0"
  }
}
```

The package is embedded under `Packages/`, so `Packages/manifest.json` is not modified. Unity may add only this minimal entry to `Packages/packages-lock.json`:

```json
"com.locksteparena.protocol": {
  "version": "file:com.locksteparena.protocol",
  "depth": 0,
  "source": "embedded",
  "dependencies": {
    "com.locksteparena.simulation": "0.1.0"
  }
}
```

The existing Simulation lock entry remains unchanged. No Google.Protobuf UPM dependency is added. Any other lockfile change stops implementation for investigation.

## 6. Minimal Schema

Gate 5 uses `proto3` with:

```proto
syntax = "proto3";

package locksteparena.protocol;

option csharp_namespace = "LockstepArena.Protocol.Wire";

message RosterEntryMessage {
  uint32 player_slot = 1;
  uint64 player_id = 2;
}

message ActiveRosterMessage {
  repeated RosterEntryMessage players = 1;
}

message InputFrameMessage {
  uint32 tick = 1;
  uint32 player_slot = 2;
  sint32 move_x = 3;
  sint32 move_z = 4;
  uint32 aim = 5;
}

message PlayerInputSubmissionMessage {
  uint64 submitted_player_id = 1;
  InputFrameMessage input = 2;
}

message AuthoritativeFrameMessage {
  uint32 tick = 1;
  ActiveRosterMessage roster = 2;
  repeated InputFrameMessage inputs = 3;
}
```

The schema contains no services, map, Any, oneof, envelope, opcode, protocol version, BattleId, Snapshot, state synchronization, combat, or view data.

Gate 5 authoritative frames carry the complete roster because no Battle/Session protocol context exists yet. Decode still requires an `expectedRoster`; the wire roster is validated against it and never replaces it.

## 7. Wire-to-Domain Type Contract

| Meaning | Protobuf | Generated C# | Domain | Boundary rule |
|---|---|---|---|---|
| PlayerId | `uint64` | `ulong` | `PlayerId` | exact; zero remains valid |
| PlayerSlot | `uint32` | `uint` | `PlayerSlot(int)` | reject above `int.MaxValue`, then checked conversion |
| Tick | `uint32` | `uint` | `uint` | exact |
| MoveX | `sint32` | `int` | `sbyte` | accept only `-1..1` |
| MoveZ | `sint32` | `int` | `sbyte` | accept only `-1..1` |
| Aim | `uint32` | `uint` | `ushort` | reject above `ushort.MaxValue` |

Proto3 scalar defaults intentionally coexist with valid Domain zero values. Scalar fields are not marked optional. Nested `input` and `roster` message presence is checked explicitly.

## 8. Exception Contract

Three categories are frozen:

1. malformed Protobuf bytes fail parsing with `InvalidProtocolBufferException`;
2. null top-level mapper arguments fail with `ArgumentNullException`;
3. parsed DTOs that cannot form valid Domain data fail with `ProtocolMappingException`.

`ProtocolMappingException` is a minimal sealed exception with a message and optional inner exception. It has no error code, validation result, field-path object, localization system, or hierarchy.

The mapper catches only expected Domain `ArgumentException` failures and wraps them. It never uses `catch (Exception)`.

## 9. Input Mapping

`InputFrameMessage` converts in this order:

1. nested message presence;
2. `player_slot <= int.MaxValue`;
3. checked conversion and `PlayerSlot` construction;
4. `move_x` in `-1..1`;
5. `move_z` in `-1..1`;
6. `aim <= ushort.MaxValue`;
7. checked narrowing;
8. `InputFrame` construction.

`PlayerInputSubmissionMessage` first checks its nested input, then returns the exact `PlayerId` and converted `InputFrame`. Mapper code does not validate PlayerId-to-Slot ownership. Gate 3 `StrictFrameCollector` and Gate 4 `AuthoritativeFrameCoordinator` retain that responsibility.

## 10. Active Roster Mapping

Wire roster entries may arrive in any repeated-field order. Mapping uses only local slot-indexed storage:

```text
PlayerId[count] idsBySlot
bool[count] present
```

For each entry, mapping checks `player_slot <= int.MaxValue`, converts it, requires `slot < count`, rejects duplicate presence, and stores the PlayerId at its explicit Slot. A final presence scan rejects missing/non-contiguous Slot values. `ActiveRoster(idsBySlot)` then performs the existing duplicate PlayerId validation and defensive copy.

The mapper does not sort and does not use Dictionary or HashSet enumeration. The successfully mapped Domain roster is independent of subsequent mutation to the generated message.

`RepeatedField<T>` normally rejects null message elements. A defensive null guard may remain, but no test manufactures a null element as if it were valid parsed wire data.

## 11. Authoritative Frame Mapping

The minimal public conversion API is conceptually:

```text
ActiveRosterMessage ToWire(ActiveRoster roster)
ActiveRoster ToDomain(ActiveRosterMessage wire)

PlayerInputSubmissionMessage ToWire(PlayerId submittedPlayerId, InputFrame input)
(PlayerId SubmittedPlayerId, InputFrame Input) ToDomain(PlayerInputSubmissionMessage wire)

AuthoritativeFrameMessage ToWire(FrameData frame)
FrameData ToDomain(AuthoritativeFrameMessage wire, ActiveRoster expectedRoster)
```

Authoritative decode order is fixed:

1. reject null wire;
2. reject null expected roster;
3. require nested wire roster;
4. fully map the wire roster;
5. require `wireRoster.HasSameStructure(expectedRoster)`;
6. on mismatch, reject before processing inputs;
7. convert input representation in received repeated order;
8. do not sort;
9. call `FrameData.Create(expectedRoster, wire.Tick, convertedInputs)` exactly once;
10. wrap expected Domain failures as `ProtocolMappingException`.

Count, input Tick, duplicate Slot, missing Slot, unknown Slot, and canonicalization remain owned by `FrameData.Create`. Successful `FrameData.Roster` is the caller's `expectedRoster`, never the roster constructed from wire.

Domain-to-wire conversion iterates roster and frame inputs in Slot order, performs widening only, does not repeat Domain validation, and does not compute StateDigest.

## 12. Code Generation Contract

`LockstepArena.Protocol.CodeGen.csproj` pins:

```text
Grpc.Tools = 2.83.0
PrivateAssets = all
GrpcServices = None
CompileOutputs = false
OutputOptions = file_extension=.g.cs
```

It has one explicit `.proto` Include and one fixed tracked output directory. It does not use a wildcard or PATH `protoc`. Normal Protocol builds compile the tracked source and do not invoke codegen or modify the source tree.

Codegen evidence records:

- Grpc.Tools resolved package version;
- bundled `protoc --version`;
- resolved absolute protoc executable path;
- absence of `PROTOBUF_PROTOC`, `Protobuf_ProtocFullPath`, or command-line overrides;
- exactly one generated file at the approved path;
- clean Schema and Generated diff after regeneration.

The build-only CodeGen project may produce an empty tool assembly because `CompileOutputs=false`. It is allowed only under `.artifacts/`; it is not a Protocol production DLL and is never placed in the package.

## 13. .NET and Unity Build Topology

`LockstepArena.Protocol.csproj` targets netstandard2.1 with C# 9, nullable enabled, implicit usings disabled, and warnings as errors. Default compile items are disabled. It compiles only `Generated/**/*.cs` and `Mapping/**/*.cs`, references the Simulation project, and references `Google.Protobuf 3.36.0` through NuGet. All output and intermediate files go to repository `.artifacts/`.

`LockstepArena.Protocol.asmdef` is auto-reference disabled, engine-reference disabled, unsafe disabled, and explicitly references both `LockstepArena.Simulation` and the precompiled `Google.Protobuf.dll`.

The .NET consumer is one dependency-free .NET 8 executable under `Tests/LockstepArena.Server.Protocol.Tests`. It references Protocol and Simulation and directly compiles the same physical Gate 5 Golden Vector used by Unity. Existing Server FrameSync production remains unchanged.

## 14. Unity Protobuf Dependency Preflight

The first implementation checkpoint uses only:

```text
Google.Protobuf 3.36.0
lib/netstandard2.0/Google.Protobuf.dll
```

Before schema generation or mapper implementation, the worktree records NuGet source, package/version, SHA-256, assembly identity, BSD-3-Clause license, and the declared `System.Memory >= 4.5.3` and `System.Runtime.CompilerServices.Unsafe >= 4.5.2` dependencies.

The tracked Plugin Importer metadata freezes:

- Any Platform enabled with no exclusions;
- Editor and Standalone CPU/OS Any;
- Auto Reference disabled;
- Validate References enabled;
- no define constraints or asset bundle;
- no ProjectSettings change to Assembly Version Validation.

The initial Editor test uses ByteString, CodedOutputStream, and CodedInputStream. The Unity process, NUnit XML, named test, and Editor log must prove that the candidate DLL loads without missing assembly, version conflict, TypeLoadException, FileNotFoundException, System.Memory, or Unsafe failure.

Any dependency closure failure stops Gate 5 before schema or mapper implementation. No transitive DLL, NuGetForUnity, alternate Protobuf implementation, or disabled validation may be added without an independently approved dependency amendment.

## 15. Golden Vector

`Gate5ProtocolGoldenVector.cs` is a single physical, pure-C# actual vector compiled by Unity and .NET. It contains no NUnit, UnityEngine, UnityEditor, expected state, expected digest, or expected bytes.

It recreates the approved Gate 4 logical facts: the four-player roster, initial state, and complete Frames for Tick 0 through 11. Each run performs Domain-to-wire mapping, repeated-field reordering, serialization, parsing, strict mapping, and Simulation Step.

Wire order A is `2,0,3,1`. Wire order B is `1,3,0,2`. Serialized bytes may differ. Mapped Frames must be canonical Slot `0,1,2,3`; two simulations must match Digest after every Tick.

Unity and .NET consumers independently assert:

```text
Final Tick = 12
Slot0 = X 200,  Z 0,    Aim 11001
Slot1 = X -200, Z 0,    Aim 22002
Slot2 = X 0,    Z 200,  Aim 33003
Slot3 = X 0,    Z -200, Aim 44004
Final Digest = 0x5CFABE84CC00E1C3
```

Tests also prove that different Protobuf bytes which map to the same Domain state produce the same Domain StateDigest. Protobuf serialization bytes are never hashed as Simulation state.

## 16. Frozen Test Matrix

The dependency-free .NET suite registers exactly 35 named cases:

| Group | Count |
|---|---:|
| Parser/API boundary | 3 |
| ActiveRoster mapping | 8 |
| Input submission/narrowing | 9 |
| Authoritative frame mapping | 12 |
| Wire bytes / Domain Digest separation | 2 |
| 12-Tick Golden | 1 |
| Total | 35 |

Expected result is `RESULT 35/35 passed`.

The Gate 5 Unity assembly is run independently with assembly filter:

```text
LockstepArena.Protocol.Editor.Tests
```

Its XML must report exactly two tests, both passed:

```text
GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads
UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector
```

Gate 3 Unity Golden is run separately through `LockstepArena.Simulation.Editor.Tests`; the Gate 5 count is not inferred from all project EditMode tests.

## 17. Regression and Acceptance Matrix

Final evidence requires:

| Evidence | Required result |
|---|---|
| Approved base | `cd09b89739284d5fe36e1d5c825a3fd1578e6768` |
| Existing Release builds | 0 warnings / 0 errors |
| CodeGen, Protocol, Protocol Tests builds | 0 warnings / 0 errors |
| Gate 3 suite | 38/38 |
| Gate 4 suite | 32/32 |
| Gate 3 Server Golden | `0x89A7DD66F8D9E871` |
| Gate 3 Unity Golden | named test Passed, 0 failed |
| Gate 5 .NET suite | 35/35 |
| Gate 5 Unity suite | assembly-filtered 2/2, 0 failed |
| Codegen provenance | package, version, resolved path, bundled protoc version, no override |
| Regenerate | exactly one output and Schema/Generated diff clean |
| Dependency provenance | source, SHA-256, assembly identity, license, declared dependencies |
| Golden order | both mapped sequences canonical Slot 0..3 |
| Golden determinism | per-Tick Digests equal |
| Final state | Tick 12 and approved four Slot states |
| Final Digest | `0x5CFABE84CC00E1C3` |
| Simulation package | committed diff from approved base = 0 |
| Server FrameSync production | committed diff from approved base = 0 |
| manifest | committed diff = 0 |
| packages-lock | only approved embedded Protocol entry |
| Assets/ProjectSettings | committed diff = 0 |
| Package artifacts | no bin, obj, or LockstepArena DLL |
| Source topology | one proto, one generated source, no link/copy/sync mechanism |
| Normal checkout | preserves only the two user-owned changes |

The intentional third-party `Google.Protobuf.dll` and its tracked `.meta` are provenance-controlled package inputs, not build artifacts. A CodeGen empty tool assembly is permitted only below `.artifacts/`.

## 18. Additive and Non-Breaking Impact

Gate 5 adds one embedded package, one build-only CodeGen project, one dependency-free .NET test executable, three exact `.gitignore` exceptions for authored projects, and one minimal package-lock entry.

Planning inspection confirmed that all three approved authored `.csproj` paths currently match the repository's existing `.gitignore` rule `*.csproj`. Implementation therefore requires exactly the three path-specific negated exceptions named in the Implementation Plan; no broader ignore change is justified.

Gate 3/4 Simulation APIs, digests, vectors, Server FrameSync APIs, Assets, ProjectSettings, and manifest remain unchanged.

## 19. Explicitly Out of Scope

Gate 5 does not implement TCP, UDP, KCP, Socket, send/receive loops, packet framing, opcode, router, envelope, connection, retry, Login, Room, Session, BattleId lifecycle, TickClock, fixed InputDelay, timeout, missing-input replacement, Prediction, Dirty Frame, Snapshot, Rollback, Replay protocol, State Sync, Combat, Unity View, IL2CPP Player build, protocol negotiation, compatibility framework, generic mapper, DI, factory, schema registry, publishing pipeline, or codegen service.

## 20. Planning and Exit Contract

Planning isolation is:

```text
Worktree: .worktrees/gate5-protobuf-domain-boundary
Branch: codex/gate5-protobuf-domain-boundary
Exact base: cd09b89739284d5fe36e1d5c825a3fd1578e6768
```

The planning commit contains only this architecture document and the approved Implementation Plan. Protocol package files, dependencies, tools, tests, `.gitignore`, packages-lock, Shared, Server production, Assets, ProjectSettings, and manifest changes wait for independent Planning PASS.

Gate 5 implementation may be submitted for final approval only after dependency preflight, all acceptance rows, fresh final verification, evidence documentation, remote SHA equality, and a clean worktree. Work stops before networking or Gate 6 work begins.

Implementation evidence is intentionally absent from the planning commit and may be added only after fresh implementation verification succeeds.

## 21. Implementation Evidence

Gate 5 implementation and final verification completed on 2026-08-30 in the approved isolated worktree. The approved base is `cd09b89739284d5fe36e1d5c825a3fd1578e6768`; the planning commit is `3d51fc61fa0226d788d51cbdcd404ae5fb89246f`; and the verified implementation HEAD before this evidence-only commit is `08f0e94e26df637122b71f6ce5c9bd9d6cdfccac`.

The five implementation slices are:

```text
57bc18c build: preflight Unity protobuf runtime dependency
e801dc3 build: generate shared protobuf wire contracts
e874e50 feat: map protobuf roster and input domain values
bf1d1d0 feat: map authoritative protobuf frames
08f0e94 test: prove dual-runtime protobuf round trip
```

### 21.1 Dependency Preflight and Provenance

The isolated Unity dependency preflight passed before schema, generated DTO, mapper, or Golden implementation began. Unity 6000.3.10f1 loaded and exercised `Google.Protobuf 3.36.0` using the package's `lib/netstandard2.0/Google.Protobuf.dll` without a missing `System.Memory`, missing `System.Runtime.CompilerServices.Unsafe`, assembly version conflict, `FileNotFoundException`, `TypeLoadException`, or Google.Protobuf load failure. No transitive managed DLL, NuGetForUnity package, alternate Protobuf implementation, or disabled reference validation was added.

Fresh final provenance checks reported:

```text
Google.Protobuf package: 3.36.0
Tracked DLL SHA-256: 436C5DF3C47CA325AE0F2D2F57BB924C224CD81977CCFE0141699C5B2000988E
Assembly identity: Google.Protobuf, Version=3.36.0.0, Culture=neutral, PublicKeyToken=a7d26565bac4d604
Tracked DLL size: 501248 bytes
License: BSD-3-Clause
Declared netstandard2.0 dependencies: System.Memory >= 4.5.3; System.Runtime.CompilerServices.Unsafe >= 4.5.2
```

The recomputed SHA-256 and assembly identity exactly match `Third Party Notices.md`. The intentional Google.Protobuf DLL is the only tracked DLL and the only DLL under the Protocol package.

### 21.2 Pinned Code Generation

Fresh deterministic regeneration used:

```text
Grpc.Tools: 2.83.0
Resolved protoc: C:\Users\张晨旭\.nuget\packages\grpc.tools\2.83.0\tools\windows_x64\protoc.exe
Bundled protoc version: libprotoc 35.1
Resolved protoc SHA-256: EA33FADF8FC93D8445D3F39A98E265224F53B1B5DB4196DE0B03B5724120F767
PROTOBUF_PROTOC override: absent
Protobuf_ProtocFullPath override: absent
```

The generator contract remains `PrivateAssets=all`, `GrpcServices=None`, `CompileOutputs=false`, and `OutputOptions=file_extension=.g.cs`, with one explicit proto Include. Regeneration produced exactly one tracked `.g.cs` from exactly one tracked `.proto`; no gRPC stub was generated. The generated file SHA-256 was `4E33AA2ACBE634572B8BBF3D99F6F9549B103AE62CB60F8566BB81E9FE6F09D1` before and after regeneration, and the Schema plus Generated `git diff --exit-code` passed. The permitted empty CodeGen tool assembly was emitted only below `.artifacts/`.

### 21.3 Fresh Release Builds and .NET Results

All eight approved projects were built individually in Release configuration. Every build completed with 0 warnings and 0 errors:

```text
Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj
Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj
Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj
Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj
Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj
Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj
Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj
Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj
```

Fresh runtime results were:

```text
Gate 3 Simulation suite: RESULT 38/38 passed
Gate 4 FrameSync suite: RESULT 32/32 passed
Gate 3 Server Golden: PASS Gate3GoldenVector Tick=1000 Players=4 Digest=89A7DD66F8D9E871
Gate 5 Protocol suite: RESULT 35/35 passed
```

The Gate 5 suite independently asserts that wire input orders `2,0,3,1` and `1,3,0,2` produce different serialized bytes, map to canonical Slot `0,1,2,3` Frames, and produce equal Domain Digest after every Tick. Both 12-Tick runs finished at:

```text
Tick = 12
Slot0 = X 200,  Z 0,    Aim 11001
Slot1 = X -200, Z 0,    Aim 22002
Slot2 = X 0,    Z 200,  Aim 33003
Slot3 = X 0,    Z -200, Aim 44004
Digest = 0x5CFABE84CC00E1C3
```

The pure shared Golden vector contains actual inputs and actual results only. The .NET and Unity consumers each own their expected state and Digest literals. StateDigest continues to consume only canonical Domain state and never Protobuf bytes.

### 21.4 Fresh Unity Results

Both final Unity jobs used Unity 6000.3.10f1 (`e35f0c77bd8e`) in the Gate 5 worktree and returned process exit code 0. NUnit XML, rather than process exit alone, supplied the acceptance evidence.

Gate 5 used only assembly filter `LockstepArena.Protocol.Editor.Tests`:

```text
XML: .artifacts/gate5/final/gate5-results.xml
Log: .artifacts/gate5/final/gate5-editor.log
total=2 passed=2 failed=0
GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads = Passed
UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector = Passed
```

Gate 3 was executed separately through `LockstepArena.Simulation.Editor.Tests` with its named Golden filter:

```text
XML: .artifacts/gate5/final/gate3-results.xml
Log: .artifacts/gate5/final/gate3-editor.log
total=1 passed=1 failed=0
UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector = Passed
```

Each Unity run generated the same worktree-local `Assets/Settings/Mobile_RPAsset.asset` serialization upgrade. Each exact diff was inspected and restored by its specific path. No broad reset or clean was used, and the final Gate 5 worktree has no Assets or ProjectSettings modification.

### 21.5 Boundary and Topology Audits

Committed-diff checks relative to the approved Gate 4 baseline reported zero changed paths under:

```text
Packages/com.locksteparena.simulation/
Server/LockstepArena.Server.FrameSync/
Assets/
ProjectSettings/
Packages/manifest.json
```

`Packages/packages-lock.json` contains only the added embedded `com.locksteparena.protocol` entry, whose dependency is `com.locksteparena.simulation: 0.1.0`; it matches the Protocol package manifest. Protocol csproj, asmdef, and package metadata depend on Simulation, while Simulation and Server FrameSync contain no Protocol or Google.Protobuf dependency.

Topology and artifact audits confirmed:

- exactly one tracked `.proto`;
- exactly one tracked generated `.g.cs`;
- exactly one physical `Gate5ProtocolGoldenVector.cs`, directly compiled by Unity and the .NET Protocol tests;
- no generated gRPC service stubs;
- no copy, sync, or codegen wrapper scripts;
- no tracked symlink or junction;
- no package `bin` or `obj` directory;
- no precompiled LockstepArena DLL in the Protocol package;
- no unapproved third-party DLL;
- no TCP, UDP, KCP, Socket, Room, Login, Session, TickClock, timeout, Prediction, Snapshot, Rollback, Replay, Combat, or other excluded production type.

The ordinary checkout preservation audit reported exactly the two user-owned changes and nothing else:

```text
 M Assets/Settings/Mobile_RPAsset.asset
 M ProjectSettings/ShaderGraphSettings.asset
```

Gate 5 therefore remains an offline, additive Protocol-to-Domain boundary proof. It adds neither transport nor any Gate 6 capability.
