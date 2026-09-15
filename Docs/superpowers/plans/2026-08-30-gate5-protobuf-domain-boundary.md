# Gate 5 Offline Protobuf Domain Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove that Unity 6000.3.10f1 and .NET can serialize, parse, validate, and map the same tracked Protobuf contracts into the existing Gate 3 deterministic Domain, then obtain identical 12-Tick Simulation state and Digest without introducing transport.

**Architecture:** Add one embedded `com.locksteparena.protocol` package containing the sole `.proto`, sole tracked generated C# output, one explicit Domain mapper, and the official Google.Protobuf runtime asset. A pinned build-only Grpc.Tools project regenerates that source. Unity and .NET compile those same physical files; malformed representation is rejected at the mapper boundary and accepted values are delegated to existing Simulation constructors and `FrameData.Create`.

**Tech Stack:** Unity 6000.3.10f1, C# 9 / netstandard2.1 Protocol runtime, .NET 8 test and code-generation projects, Google.Protobuf 3.36.0, Grpc.Tools 2.83.0, proto3, existing dependency-free test-runner pattern, NUnit Unity EditMode tests.

**Spec:** `Docs/Architecture/GATE5_OFFLINE_PROTOBUF_DOMAIN_BOUNDARY.md`

## Global Constraints

- Exact approved base: `cd09b89739284d5fe36e1d5c825a3fd1578e6768`.
- Work only in `.worktrees/gate5-protobuf-domain-boundary` on `codex/gate5-protobuf-domain-boundary`.
- The first implementation action is the isolated Unity Google.Protobuf dependency preflight. If Unity reports missing `System.Memory`, missing `System.Runtime.CompilerServices.Unsafe`, a version conflict, or an assembly-load failure, stop and submit a precise dependency amendment. Do not continue to schema or mapper work.
- Add no arbitrary DLL, NuGetForUnity package, alternate Protobuf implementation, transitive vendored DLL, copy/sync script, symlink, junction, or precompiled LockstepArena assembly.
- Keep exactly one `.proto` and exactly one tracked generated `.g.cs`; both runtimes compile the same physical generated file.
- Pin code generation to Grpc.Tools `2.83.0`; do not call a PATH-installed `protoc`.
- Keep `Packages/com.locksteparena.simulation/` and `Server/LockstepArena.Server.FrameSync/` production committed diffs at zero relative to the approved base.
- Keep `Packages/manifest.json`, `Assets/`, and `ProjectSettings/` committed diffs at zero. `Packages/packages-lock.json` may gain only the approved embedded Protocol entry.
- Protocol depends on Simulation; Simulation and Server FrameSync do not depend on Protocol or Google.Protobuf.
- Mapper code validates wire representation and narrowing, then delegates Domain invariants to Gate 3 constructors and `FrameData.Create`; do not duplicate the Domain model.
- Protobuf bytes never enter `StateDigest`.
- Do not add transport, packet framing, opcode, Room, Login, Session, TickClock, input delay, timeout, missing-input replacement, Prediction, Snapshot, Rollback, Replay, View, Combat, DI, factory, generic mapper, schema registry, or protocol-version framework.
- Preserve the ordinary checkout's user-owned changes to `Assets/Settings/Mobile_RPAsset.asset` and `ProjectSettings/ShaderGraphSettings.asset`.
- Commit in the six approved slices listed below. After final evidence is pushed, stop before Gate 6.

## Planned File Structure

~~~text
.gitignore
Docs/Architecture/GATE5_OFFLINE_PROTOBUF_DOMAIN_BOUNDARY.md
Packages/
  packages-lock.json
  com.locksteparena.protocol/
    package.json
    package.json.meta
    Third Party Notices.md
    Third Party Notices.md.meta
    Runtime.meta
    Runtime/
      Directory.Build.props
      Directory.Build.props.meta
      LockstepArena.Protocol.asmdef
      LockstepArena.Protocol.asmdef.meta
      LockstepArena.Protocol.csproj
      LockstepArena.Protocol.csproj.meta
      Generated.meta
      Generated/LockstepArenaProtocol.g.cs
      Generated/LockstepArenaProtocol.g.cs.meta
      Mapping.meta
      Mapping/ProtocolMapper.cs
      Mapping/ProtocolMapper.cs.meta
      Mapping/ProtocolMappingException.cs
      Mapping/ProtocolMappingException.cs.meta
      Plugins.meta
      Plugins/Google.Protobuf.dll
      Plugins/Google.Protobuf.dll.meta
    Schema.meta
    Schema/lockstep_arena_protocol.proto
    Schema/lockstep_arena_protocol.proto.meta
    Tests.meta
    Tests/Editor.meta
    Tests/Editor/
      LockstepArena.Protocol.Editor.Tests.asmdef
      LockstepArena.Protocol.Editor.Tests.asmdef.meta
      GoogleProtobufDependencyPreflightTests.cs
      GoogleProtobufDependencyPreflightTests.cs.meta
      UnityProtocolGoldenVectorTests.cs
      UnityProtocolGoldenVectorTests.cs.meta
      Gate5ProtocolGoldenVector.cs
      Gate5ProtocolGoldenVector.cs.meta
Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj
Tests/LockstepArena.Server.Protocol.Tests/
  LockstepArena.Server.Protocol.Tests.csproj
  Program.cs
  ProtocolParserContractTests.cs
  ActiveRosterProtocolTests.cs
  InputFrameProtocolTests.cs
  AuthoritativeFrameProtocolTests.cs
  ProtocolDeterminismTests.cs
~~~

Unity-generated `.meta` GUIDs are authored once in the isolated worktree and tracked. No solution, Server Host, transport project, generated-code copy, or package publishing configuration is added.

---

## Task 1: Preflight the Official Unity Protobuf Runtime

**Commit:** `build: preflight Unity protobuf runtime dependency`

**Files:**

- Modify: `.gitignore`
- Modify: `Packages/packages-lock.json`
- Create: `Packages/com.locksteparena.protocol/package.json`
- Create: `Packages/com.locksteparena.protocol/Third Party Notices.md`
- Create: `Packages/com.locksteparena.protocol/Runtime/Directory.Build.props`
- Create: `Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj`
- Create: `Packages/com.locksteparena.protocol/Runtime/Plugins/Google.Protobuf.dll`
- Create: `Packages/com.locksteparena.protocol/Runtime/Plugins/Google.Protobuf.dll.meta`
- Create: `Packages/com.locksteparena.protocol/Tests/Editor/LockstepArena.Protocol.Editor.Tests.asmdef`
- Create: `Packages/com.locksteparena.protocol/Tests/Editor/GoogleProtobufDependencyPreflightTests.cs`
- Create: required Unity `.meta` files for the new package paths

- [ ] **Step 1: Reconfirm isolation, base ancestry, baseline regressions, and authored-project ignore behavior**

Run from the Gate 5 worktree:

~~~powershell
if ((git rev-parse --abbrev-ref HEAD) -ne 'codex/gate5-protobuf-domain-boundary') { throw 'Wrong Gate 5 branch.' }
if ((git merge-base HEAD cd09b89739284d5fe36e1d5c825a3fd1578e6768) -ne 'cd09b89739284d5fe36e1d5c825a3fd1578e6768') { throw 'Approved Gate 4 base is not an ancestor.' }
git status --short
git check-ignore -v Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj
git check-ignore -v Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj
git check-ignore -v Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj
dotnet build Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj -c Release
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release
dotnet run --project Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj -c Release
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release
~~~

Expected: the approved planning commit is already present and the implementation worktree is clean; all three future csproj paths match `*.csproj`; existing builds have 0 warnings / 0 errors, Gate 3 reports `RESULT 38/38 passed`, Gate 4 reports `RESULT 32/32 passed`, and Server Golden reports `Digest=89A7DD66F8D9E871`.

- [ ] **Step 2: Add only the three exact authored-project exceptions**

Add beside existing csproj exceptions:

~~~gitignore
!Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj
!Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj
!Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj
~~~

Verify that each exact path is now trackable and that no broader ignore rule changed.

- [ ] **Step 3: Create the complete embedded package shell and scoped .NET output contract**

Use this package manifest:

~~~json
{
  "name": "com.locksteparena.protocol",
  "version": "0.1.0",
  "displayName": "Lockstep Arena Protocol",
  "description": "Offline Protobuf contracts and explicit Simulation Domain mapping.",
  "unity": "6000.3",
  "dependencies": {
    "com.locksteparena.simulation": "0.1.0"
  }
}
~~~

Create `Runtime/Directory.Build.props` with the same repository-root `.artifacts/` redirection pattern as the Simulation package. Create a netstandard2.1 / C# 9 / nullable / warnings-as-errors csproj that disables default compile items, references Simulation by `ProjectReference`, references `Google.Protobuf` version `3.36.0` for .NET build resolution, and explicitly compiles only future `Generated/**/*.cs` and `Mapping/**/*.cs`. At this preflight checkpoint those globs may match no source; do not include Tests, Editor, Schema, package metadata, or plugin assets as `Compile` items.

- [ ] **Step 4: Acquire only the official candidate DLL and record provenance**

Restore `Google.Protobuf 3.36.0` through the SDK project, copy only `lib/netstandard2.0/Google.Protobuf.dll` into `Runtime/Plugins`, and record in `Third Party Notices.md`:

- package id and version;
- official NuGet package source URL;
- selected target `lib/netstandard2.0`;
- SHA-256 of the tracked DLL;
- assembly name/version/public key token obtained from the file;
- license name and license URL;
- NuGet-declared `System.Memory >= 4.5.3` and `System.Runtime.CompilerServices.Unsafe >= 4.5.2` dependencies;
- statement that Unity closure is accepted only by the next isolated test.

Do not vendor the NuGet package, XML docs, PDB, `System.Memory.dll`, or `System.Runtime.CompilerServices.Unsafe.dll`.

- [ ] **Step 5: Configure the tracked Unity plugin importer and preflight test**

Author `Google.Protobuf.dll.meta` with Any Platform enabled, no platform exclusions, CPU/OS Any, Auto Reference disabled, Validate References enabled, no define constraints, and no project-wide assembly-validation setting changes. The preflight Editor asmdef explicitly references the DLL as a precompiled reference and the Simulation test dependency needed by the later assembly; it does not require Protocol generated or mapper source yet.

Create exactly this test name:

~~~text
GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads
~~~

The test must instantiate and round-trip a small Google.Protobuf runtime type, exercise an API path that loads the runtime dependencies, and assert the loaded `Google.Protobuf` assembly identity is the tracked 3.36.0 candidate. It must not test Domain mapping yet.

- [ ] **Step 6: Run the isolated Unity dependency gate and parse fresh NUnit XML**

Close all Unity instances, then run Unity 6000.3.10f1 in the Gate 5 worktree with EditMode, assembly filter `LockstepArena.Protocol.Editor.Tests`, and test filter `GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads`. Write XML and Editor log below `.artifacts/gate5/preflight/`.

Parse the XML, not just the exit code. Require total=1, passed=1, failed=0, and the named test `Passed`. Search the Editor log for missing assembly, version conflict, type-load, and resolution warnings.

**Stop condition:** if Unity cannot satisfy `System.Memory` or `System.Runtime.CompilerServices.Unsafe`, reports a version conflict, or cannot load Google.Protobuf, make no schema/generated/mapper changes. Preserve diagnostic evidence and request an exact dependency amendment.

- [ ] **Step 7: Audit package-lock and Unity serialization changes**

Permit only this new lock entry, with the actual lockfile schema/indentation preserved:

~~~json
"com.locksteparena.protocol": {
  "version": "file:com.locksteparena.protocol",
  "depth": 0,
  "source": "embedded",
  "dependencies": {
    "com.locksteparena.simulation": "0.1.0"
  }
}
~~~

Confirm `Packages/manifest.json` is unchanged. Inspect Unity-generated worktree-local changes; restore only unintended `Assets/` or `ProjectSettings/` serialization files by exact path. Do not use broad reset or clean.

- [ ] **Step 8: Commit the successful preflight slice**

Before commit, confirm the new package contains no `bin`, `obj`, LockstepArena DLL, transitive vendored DLL, schema, generated source, mapper, or Golden vector. Run `git diff --check`, then commit with the exact message above.

---

## Task 2: Pin Schema Code Generation and the Shared Protocol Assembly

**Commit:** `build: generate shared protobuf wire contracts`

**Files:**

- Create: `Packages/com.locksteparena.protocol/Schema/lockstep_arena_protocol.proto`
- Create: `Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.asmdef`
- Create: `Packages/com.locksteparena.protocol/Runtime/Generated/LockstepArenaProtocol.g.cs`
- Create: `Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj`
- Modify: `Packages/com.locksteparena.protocol/Tests/Editor/LockstepArena.Protocol.Editor.Tests.asmdef`
- Create: required `.meta` files

- [ ] **Step 1: Add the one approved schema**

Create exactly one `.proto` with this contract:

~~~proto
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
~~~

Do not add envelopes, opcode, BattleId, sequence, timestamps, version fields, or state messages.

- [ ] **Step 2: Add the pinned build-only CodeGen project**

The SDK project targets net8.0, routes all outputs through `.artifacts/`, and contains:

~~~xml
<PackageReference Include="Grpc.Tools" Version="2.83.0" PrivateAssets="all" />
<Protobuf Include="..\..\Packages\com.locksteparena.protocol\Schema\lockstep_arena_protocol.proto"
          ProtoRoot="..\..\Packages\com.locksteparena.protocol\Schema"
          OutputDir="..\..\Packages\com.locksteparena.protocol\Runtime\Generated"
          GrpcServices="None"
          CompileOutputs="false"
          OutputOptions="file_extension=.g.cs" />
~~~

Use no wildcard Include and no executable lookup from PATH. An empty CodeGen tool assembly is allowed only under `.artifacts/`.

- [ ] **Step 3: Prove generator provenance before regeneration**

Record command output that identifies:

- resolved NuGet package version `Grpc.Tools 2.83.0`;
- exact resolved bundled `protoc.exe` path inside that package;
- bundled `protoc --version`;
- environment and MSBuild property checks proving `PROTOBUF_PROTOC` and `Protobuf_ProtocFullPath` are unset/not overridden.

If the resolved generator is not from pinned Grpc.Tools 2.83.0, stop and correct the build-only project before generating.

- [ ] **Step 4: Generate exactly one tracked C# file**

Delete no unrelated file. Run the CodeGen target, then require:

~~~text
Generated file count: 1
Generated path: Packages/com.locksteparena.protocol/Runtime/Generated/LockstepArenaProtocol.g.cs
~~~

The generated namespace must be `LockstepArena.Protocol.Wire`; no gRPC client/server stubs may exist.

- [ ] **Step 5: Configure the production asmdef and test dependency**

Create `LockstepArena.Protocol.asmdef` with `autoReferenced: false`, `noEngineReferences: true`, no unsafe code, no define constraints, a reference to `LockstepArena.Simulation`, and an explicit precompiled reference to `Google.Protobuf.dll`. Update the Editor test asmdef to reference `LockstepArena.Protocol` plus the existing test dependencies. Do not add UnityEngine/UnityEditor usage to Protocol runtime source.

- [ ] **Step 6: Verify deterministic regeneration and assembly boundaries**

Regenerate a second time, then require:

~~~powershell
git diff --exit-code -- Packages/com.locksteparena.protocol/Schema Packages/com.locksteparena.protocol/Runtime/Generated
~~~

Build CodeGen and Protocol Release with 0 warnings / 0 errors. Confirm Protocol csproj compiles only `Generated/**/*.cs` and future `Mapping/**/*.cs`; package Schema, Editor tests, metadata, and plugin assets are not production compile items. Confirm package contains no `bin`, `obj`, or LockstepArena DLL.

- [ ] **Step 7: Commit the code-generation slice**

Require one proto, one generated `.g.cs`, no gRPC services, no PATH protoc usage, and no changes to Simulation, Server FrameSync, manifest, Assets, or ProjectSettings. Run `git diff --check` and commit.

---

## Task 3: Map Roster and Player Input Values with Explicit Boundary Validation

**Commit:** `feat: map protobuf roster and input domain values`

**Files:**

- Create: `Packages/com.locksteparena.protocol/Runtime/Mapping/ProtocolMappingException.cs`
- Create: `Packages/com.locksteparena.protocol/Runtime/Mapping/ProtocolMapper.cs`
- Create: `Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj`
- Create: `Tests/LockstepArena.Server.Protocol.Tests/Program.cs`
- Create: `Tests/LockstepArena.Server.Protocol.Tests/ProtocolParserContractTests.cs`
- Create: `Tests/LockstepArena.Server.Protocol.Tests/ActiveRosterProtocolTests.cs`
- Create: `Tests/LockstepArena.Server.Protocol.Tests/InputFrameProtocolTests.cs`
- Create: required package `.meta` files

**Public mapper surface:**

~~~csharp
public static ActiveRosterMessage ToWire(ActiveRoster roster);
public static ActiveRoster ToDomain(ActiveRosterMessage wire);
public static PlayerInputSubmissionMessage ToWire(PlayerId submittedPlayerId, InputFrame input);
public static (PlayerId SubmittedPlayerId, InputFrame Input) ToDomain(PlayerInputSubmissionMessage wire);
public static AuthoritativeFrameMessage ToWire(FrameData frame);
public static FrameData ToDomain(AuthoritativeFrameMessage wire, ActiveRoster expectedRoster);
~~~

The final two methods are completed in Task 4. `ProtocolMappingException` exposes only normal message and optional inner-exception constructors; it is not a hierarchy or error-code framework.

- [ ] **Step 1: Create the dependency-free runner and register the first 20 mapper tests**

The net8.0 test executable ProjectReferences Protocol and Simulation, directly compiles no package source, and follows the existing `PASS ...` / `RESULT N/N passed` runner. Register exactly:

- parser/API contract: 3;
- ActiveRoster mapping: 8;
- player input mapping: 9.

The parser tests distinguish malformed bytes (`InvalidProtocolBufferException`), null mapper arguments (`ArgumentNullException`), and parsed semantic invalidity (`ProtocolMappingException`). Roster tests cover shuffled entries, duplicate Slot, missing Slot/non-contiguous Slot, `uint32` Slot above `int.MaxValue`, empty roster, zero PlayerId delegated to Domain, duplicate PlayerId delegated to Domain, and stable round trip. Input tests cover nested-message presence, Slot overflow, MoveX/MoveZ outside `-1..1`, Aim above `ushort.MaxValue`, legal proto3 scalar zeros, submitted PlayerId, Tick, and stable round trip.

- [ ] **Step 2: Run the RED suite**

Run the test executable before adding mapper production code. Expected: compile failure because `ProtocolMapper` / `ProtocolMappingException` and the required methods do not exist. Do not weaken test registration to obtain green.

- [ ] **Step 3: Implement exception classification and roster mapping minimally**

`ToDomain(ActiveRosterMessage)` must:

1. reject null with `ArgumentNullException`;
2. reject zero entries with `ProtocolMappingException`;
3. allocate `PlayerId[]` and `bool[]` sized to wire entry count;
4. for each wire entry, reject missing entry, checked-convert `player_slot` to `int`, range-check against count, and reject duplicate Slot;
5. fill arrays by Slot without sorting and without Dictionary/HashSet iteration;
6. reject any missing Slot;
7. construct `ActiveRoster` once in Slot order;
8. catch only the expected Domain `ArgumentException` and wrap it as `ProtocolMappingException`.

`ToWire(ActiveRoster)` emits entries strictly by Slot 0..Count-1. Do not expose or retain mutable arrays.

- [ ] **Step 4: Implement player-input mapping minimally**

Wire-to-Domain validation order is top-level null, nested `input` presence, PlayerSlot checked conversion/range representation, MoveX range, MoveZ range, Aim range, then Domain construction. `Tick` and `PlayerId` use their same-width scalar values. Catch only expected Domain `ArgumentException`; never catch `Exception`.

Domain-to-wire writes all values explicitly, including legal zeros, and checked-converts non-negative `PlayerSlot` to `uint32`.

- [ ] **Step 5: Run and audit the 20-test slice**

Require `RESULT 20/20 passed`, Protocol Release build 0 warnings / 0 errors, and no UnityEngine/UnityEditor references in Runtime. Search mapper source to confirm no `Dictionary`, `HashSet`, reflection, JSON, runtime hash, generic mapper, or generated-code edits.

- [ ] **Step 6: Commit the roster/input mapping slice**

Run `git diff --check`; verify the commit does not change the schema/generated output except `.meta` bookkeeping and does not touch Shared or Server production; commit.

---

## Task 4: Map Complete Authoritative Frames into the Existing Domain

**Commit:** `feat: map authoritative protobuf frames`

**Files:**

- Modify: `Packages/com.locksteparena.protocol/Runtime/Mapping/ProtocolMapper.cs`
- Create: `Tests/LockstepArena.Server.Protocol.Tests/AuthoritativeFrameProtocolTests.cs`
- Modify: `Tests/LockstepArena.Server.Protocol.Tests/Program.cs`

- [ ] **Step 1: Register the 12 authoritative-frame contracts**

Cover: null wire, null expectedRoster, missing roster message, wire roster shuffled but structurally equal, wire roster count mismatch, Slot/PlayerId mismatch, missing input, duplicate input Slot, unknown input Slot, input Tick differing from frame Tick, repeated inputs shuffled into canonical Slot order, and successful stable round trip preserving the caller's structurally validated expected roster.

- [ ] **Step 2: Run RED before frame implementation**

Expected: the new frame tests fail because the two authoritative-frame mapper methods are absent/incomplete. Existing 20 tests must remain green.

- [ ] **Step 3: Implement Domain-to-wire frame mapping**

Reject null. Write frame Tick, write the full roster using `ToWire`, and emit one input for each Slot in canonical Slot order using `FrameData.GetInput`. Do not add an authoritative wrapper, coordinator reference, serialized digest, or Server dependency.

- [ ] **Step 4: Implement wire-to-Domain validation in the frozen order**

Perform exactly:

1. top-level null check;
2. `expectedRoster` null check;
3. nested roster presence check;
4. map the wire roster with the roster mapper;
5. compare structurally: same Count and same PlayerId at every Slot;
6. allocate an input array sized only from the wire repeated input count;
7. for each input, require nested message presence, require input Tick equals authoritative Tick, and map with explicit narrowing checks;
8. call `FrameData.Create(expectedRoster, wire.Tick, mappedInputs)` exactly once;
9. return that Domain frame, whose roster is the caller-provided expected roster.

Catch and wrap only the expected Domain `ArgumentException` from final construction. Do not preimplement FrameData's duplicate/missing/unknown rules and do not use the decoded wire roster as the battle roster.

- [ ] **Step 5: Run the cumulative 32-test mapper suite**

Require `RESULT 32/32 passed`. Confirm shuffled repeated inputs produce Slot-increasing Domain access and re-encoding. Confirm invalid frame mapping leaves `expectedRoster` unchanged and no mutable wire collection is retained.

- [ ] **Step 6: Commit the authoritative-frame slice**

Build Protocol and Protocol Tests Release with 0 warnings / 0 errors, run `git diff --check`, audit dependency direction, and commit.

---

## Task 5: Prove Dual-Runtime Protobuf Round Trip and Deterministic Golden

**Commit:** `test: prove dual-runtime protobuf round trip`

**Files:**

- Create: `Packages/com.locksteparena.protocol/Tests/Editor/Gate5ProtocolGoldenVector.cs`
- Create: `Packages/com.locksteparena.protocol/Tests/Editor/UnityProtocolGoldenVectorTests.cs`
- Create: `Tests/LockstepArena.Server.Protocol.Tests/ProtocolDeterminismTests.cs`
- Modify: `Tests/LockstepArena.Server.Protocol.Tests/Program.cs`
- Modify: `Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj`
- Create: required `.meta` files

- [ ] **Step 1: Add the sole physical pure-C# actual vector**

`Gate5ProtocolGoldenVector.cs` must contain no NUnit, UnityEngine, UnityEditor, file I/O, time, environment-variable access, randomness, or expected state/digest/batch/history literals. It creates the approved four-player roster and 12 logical complete frames, then runs actual:

~~~text
Domain FrameData
-> ProtocolMapper.ToWire
-> ToByteArray
-> Parser.ParseFrom
-> ProtocolMapper.ToDomain(parsed, expectedRoster)
-> BattleSimulation.Step
~~~

For every Tick, create two wire messages from the same logical inputs with different repeated-input arrival order:

~~~text
Order A: Slot 2, 0, 3, 1
Order B: Slot 1, 3, 0, 2
~~~

Return actual parsed/mapped frames, per-Tick digests, final states, and serialized bytes needed by consumers. Expected literals remain outside this file.

- [ ] **Step 2: Register the final three .NET contracts and run RED**

Add two wire-bytes / Domain-Digest separation tests and one 12-Tick Golden test, reaching exactly 35 registered tests. The tests must fail before the shared vector is complete or expected assertions are wired.

The two byte/digest tests prove: protobuf field/repeated ordering may change serialized bytes while canonical mapped Domain state remains equal; and `StateDigest` is computed only from Domain state, never serialized bytes.

- [ ] **Step 3: Add independent .NET expected assertions**

The .NET consumer independently asserts:

~~~text
Final Tick: 12
Slot0: X=200,  Z=0,    Aim=11001
Slot1: X=-200, Z=0,    Aim=22002
Slot2: X=0,    Z=200,  Aim=33003
Slot3: X=0,    Z=-200, Aim=44004
Digest: 0x5CFABE84CC00E1C3
~~~

It also asserts each mapped frame is canonical Slot 0..3 and both arrival orders yield equal per-Tick digests.

- [ ] **Step 4: Add the independent Unity Golden consumer**

Create exactly this second Unity test:

~~~text
UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector
~~~

The Unity test calls the same physical `Gate5ProtocolGoldenVector.cs`, but owns its own expected Tick, four Slot states, and `0x5CFABE84CC00E1C3` literals. It must not call a shared expected-value helper.

- [ ] **Step 5: Run Gate 5 .NET and Unity proof independently**

Require:

~~~text
Gate 5 .NET: RESULT 35/35 passed
Unity assembly filter: LockstepArena.Protocol.Editor.Tests
Unity XML: total=2, passed=2, failed=0
Named dependency preflight test: Passed
Named protocol Golden test: Passed
~~~

The Unity command must use only the Gate 5 assembly filter. Do not infer 2/2 from the whole project's EditMode total. Parse NUnit XML and inspect Editor logs.

- [ ] **Step 6: Re-run Gate 3 Unity Golden separately**

Run the existing Gate 3 named test through `LockstepArena.Simulation.Editor.Tests`. Require `UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector` = Passed and 0 failed. Keep its XML separate from Gate 5 results.

- [ ] **Step 7: Inspect Unity serialization and package artifacts**

Restore only exact worktree-local Unity serialization changes under Assets/ProjectSettings if Unity generated them. Do not broad reset/clean. Confirm the package has no `bin`, `obj`, generated LockstepArena DLL, copied source, or extra managed dependency; the intentional Google.Protobuf DLL and `.meta` remain.

- [ ] **Step 8: Commit the dual-runtime proof slice**

Run the 35/35 suite again, verify the Golden vector contains no expected literals or framework/runtime dependencies, run `git diff --check`, and commit.

---

## Task 6: Execute Fresh Final Verification, Record Evidence, Push, and Stop

**Commit:** `docs: record Gate 5 implementation evidence`

**Files:**

- Modify: `Docs/Architecture/GATE5_OFFLINE_PROTOBUF_DOMAIN_BOUNDARY.md`

- [ ] **Step 1: Reconfirm clean implementation HEAD and regenerate deterministically**

Record branch and HEAD. Run the pinned generator provenance checks again, regenerate, require exactly one generated file, and require:

~~~powershell
git diff --exit-code -- Packages/com.locksteparena.protocol/Schema Packages/com.locksteparena.protocol/Runtime/Generated
~~~

Evidence must state Grpc.Tools package version, bundled protoc version, resolved protoc executable path, and absence of `PROTOBUF_PROTOC` / `Protobuf_ProtocFullPath` overrides.

- [ ] **Step 2: Run all eight fresh Release builds**

Build these projects individually with 0 warnings / 0 errors:

~~~text
Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj
Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj
Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj
Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj
Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj
Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj
Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj
Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj
~~~

Any empty CodeGen assembly must exist only under `.artifacts/` and is not a production Protocol DLL.

- [ ] **Step 3: Run all fresh .NET regressions and Goldens**

Require:

~~~text
Gate 3 Simulation suite: RESULT 38/38 passed
Gate 4 FrameSync suite: RESULT 32/32 passed
Gate 3 Server Golden: Tick=1000 Players=4 Digest=89A7DD66F8D9E871
Gate 5 Protocol suite: RESULT 35/35 passed
Gate 5 final Tick: 12
Gate 5 final Digest: 5CFABE84CC00E1C3
~~~

- [ ] **Step 4: Run fresh Unity regressions as two independent jobs**

With Unity 6000.3.10f1 closed before each command:

1. run Gate 5 EditMode with assembly filter `LockstepArena.Protocol.Editor.Tests`; require fresh XML total=2, passed=2, failed=0, with both named tests Passed;
2. run Gate 3 EditMode with assembly filter `LockstepArena.Simulation.Editor.Tests` and the named Golden test; require passed and 0 failed.

Record Unity version, command exit code, XML totals, named results, and result/log paths. Exit code alone is not evidence.

- [ ] **Step 5: Run committed-diff and dependency-direction audits**

Relative to `cd09b89739284d5fe36e1d5c825a3fd1578e6768`, require:

- `Packages/com.locksteparena.simulation/` diff empty;
- `Server/LockstepArena.Server.FrameSync/` production diff empty;
- `Assets/`, `ProjectSettings/`, and `Packages/manifest.json` diff empty;
- `Packages/packages-lock.json` contains only the new embedded Protocol entry and matching Simulation dependency;
- Simulation csproj/asmdef/source contain no Protocol or Google.Protobuf dependency;
- Server FrameSync production contains no Protocol or Google.Protobuf dependency;
- Protocol depends on Simulation and not vice versa.

- [ ] **Step 6: Run source, dependency, script, link, and artifact audits**

Require exactly one tracked `.proto`, exactly one tracked generated `.g.cs`, and one physical `Gate5ProtocolGoldenVector.cs`. Confirm no copy/sync/codegen wrapper scripts, symlinks, junctions, precompiled LockstepArena DLL, unapproved third-party DLL, package `bin`/`obj`, network terms/types, or out-of-scope production types. Recompute and compare the tracked Google.Protobuf DLL SHA-256 and assembly identity with Third Party Notices.

- [ ] **Step 7: Confirm ordinary-checkout preservation**

From the ordinary checkout, require only:

~~~text
 M Assets/Settings/Mobile_RPAsset.asset
 M ProjectSettings/ShaderGraphSettings.asset
~~~

Investigate any additional path; never clean, restore, stage, or commit the user's two files.

- [ ] **Step 8: Write complete implementation evidence**

Append an Implementation Evidence section to the architecture document containing exact commit range, versions/provenance, eight build results, all .NET and Unity results, Golden state/digest, regeneration proof, dependency closure result, lockfile result, audits, ordinary checkout preservation, and explicit scope exclusion result. Do not state success for any command not freshly executed.

- [ ] **Step 9: Commit evidence and verify the final committed scope**

Run `git diff --check`; commit only the evidence document with the approved message. Then inspect:

~~~powershell
git log --oneline cd09b89739284d5fe36e1d5c825a3fd1578e6768..HEAD
git diff --name-status cd09b89739284d5fe36e1d5c825a3fd1578e6768..HEAD
git status --short
~~~

Expected: the planning commit plus six approved implementation commits; worktree clean; no unapproved path.

- [ ] **Step 10: Push, prove remote equality, hand off, and stop**

Push `codex/gate5-protobuf-domain-boundary`, then compare local HEAD with:

~~~powershell
git ls-remote --heads origin refs/heads/codex/gate5-protobuf-domain-boundary
~~~

Require remote SHA == local HEAD and a clean Gate 5 worktree. Submit the Gate 5 Final Handoff with exact SHAs and fresh evidence, then stop. Do not start transport, Gate 6 planning, or Gate 6 implementation before independent approval.
