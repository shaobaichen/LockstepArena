# Lockstep Arena — Gate 3 Variable-Roster Offline FrameSync

> Status: implementation complete; pending independent Gate 3 approval
>
> Approved base: `af86372ed598bd17dc0e42c9fc3571225ed050d0`
>
> Scope: given an already-fixed battle roster, prove that Shared Runtime can collect one authenticated input per active slot, canonicalize an arbitrary arrival order, advance a variable-player deterministic simulation, and produce the same digest in Unity and .NET

## 1. Executive Summary

Gate 3 replaces the temporary Gate 1/2 `Player0` / `Player1` model with an immutable variable-player roster. `PlayerId` answers who a participant is; `PlayerSlot` answers where that participant resides in the current battle. Every simulation loop, state lookup, complete frame, and digest uses explicit ascending `PlayerSlot` order.

The slice remains offline. An external caller supplies the final roster and initial player states. A one-tick `StrictFrameCollector` accepts inputs in any order, verifies the submitted identity against the roster, and emits an immutable `FrameData` only after every active slot has exactly one input. Gate 3 adds no room, transport, timing, missing-input, prediction, snapshot, rollback, replay, or Unity view behavior.

## 2. Goal and Required Proof

Gate 3 succeeds only when all of the following are simultaneously true:

1. A battle can be initialized with 2, 3, or 4 players without production code containing an exact-player-count rule.
2. `PlayerId` never determines execution order; the immutable battle-local `PlayerSlot` range `0..Count-1` does.
3. An arbitrary input arrival order produces one canonical `FrameData` ordered by ascending slot.
4. Missing, duplicate, unknown-slot, wrong-tick, unknown-identity, and identity/slot-mismatch submissions are explicitly rejected without partial mutation.
5. Two simulations given structurally equal rosters, the same initial states, and the same logical frame history have equal digests after every tested tick.
6. Unity EditMode and the offline .NET Server Verification each execute the same physical four-player Golden Vector and independently assert digest `0x89A7DD66F8D9E871`.
7. Gate 1's fifteen test intentions and Gate 2's one-physical-source/two-runtime proof remain present after the breaking migration.

## 3. Ownership and Domain Boundaries

| Data or decision | Owner | Gate 3 contract |
|---|---|---|
| Player identity creation | Future upper layer | Supplies an opaque `PlayerId`; Shared Runtime does not generate or interpret it |
| Slot assignment | Future battle bootstrap | Supplies PlayerIds already ordered by battle-local slot |
| Active roster | Current battle | Immutable slot-to-identity mapping for the battle lifetime |
| Initial player states | Battle bootstrap/test vector | Supplied in slot order; Simulation does not choose multiplayer spawns |
| Pending inputs for one tick | `StrictFrameCollector` | Mutable only while collecting; never part of `BattleState` or its digest |
| Complete canonical frame | `FrameData` | Immutable, roster-bound, and exactly one input per slot |
| Current deterministic state | `BattleSimulation` | Replaced atomically only after a complete successful Step |
| Room size rules, disconnects, joins, reconnects, AI replacement | Future gates | Not represented in Gate 3 production types |

`PlayerState` stores only deterministic movement state. It does not duplicate `PlayerId` or `PlayerSlot`; its position in `BattleState` is the slot, and the state's `ActiveRoster` supplies the identity.

## 4. Player Identity and Battle-Local Slot

### 4.1 `PlayerId`

`PlayerId` is a readonly value type wrapping one `ulong`.

- It identifies a participant but does not define order.
- It is supplied by an upper layer and remains stable for the battle.
- Gate 3 assigns no account, database, connection, or serialization meaning to it.
- All `ulong` values, including zero, are ordinary values; Gate 3 adds no sentinel.
- One Active Roster cannot contain the same PlayerId twice.

### 4.2 `PlayerSlot`

`PlayerSlot` is a readonly value type wrapping one non-negative `int`.

- Its constructor rejects negative values.
- Membership is roster-relative; non-negative does not mean valid for every roster.
- Every public slot lookup checks `slot.Value < roster.Count`.
- Valid slots for a battle are continuous and stable from `0` through `Count - 1`.
- `int` is the native array index type, not a product-level maximum-player decision.

Production execution must not sort by PlayerId or infer a slot from input arrival order.

## 5. Immutable Active Roster

The public contract is deliberately small:

~~~text
sealed immutable ActiveRoster
  ActiveRoster(IReadOnlyList<PlayerId> playerIdsInSlotOrder)
  int Count
  PlayerId GetPlayerId(PlayerSlot slot)
  bool TryGetSlot(PlayerId playerId, out PlayerSlot slot)
  bool HasSameStructure(ActiveRoster other)
~~~

Construction requires a non-null, non-empty, duplicate-free sequence. The sequence index assigns the slot; ActiveRoster never sorts the IDs. It makes a defensive copy and never returns its backing array or a mutable collection view.

`TryGetSlot` uses a linear scan and an explicit boolean success result. It does not return `-1` or another invalid PlayerSlot sentinel, and Gate 3 adds no Dictionary cache or lookup framework.

Roster equivalence is structural:

~~~text
same Count
+ same PlayerId at every PlayerSlot
~~~

Object identity and runtime hash codes do not participate. A separately constructed roster with identical ordered IDs is valid for the same Simulation state.

## 6. Canonical Complete Frame

`InputFrame` retains tick, movement, and aim but changes its slot field from `byte` to the strong `PlayerSlot` value type. It continues to reject movement components outside `-1`, `0`, and `1`. It no longer contains a fixed `0/1` slot rule.

The immutable `FrameData` public surface is:

~~~text
static FrameData Create(
    ActiveRoster roster,
    uint tick,
    IReadOnlyList<InputFrame> receivedInputs)

uint Tick
ActiveRoster Roster
int InputCount
InputFrame GetInput(PlayerSlot slot)
~~~

Creation builds a local slot-indexed array and presence array. It rejects a null argument, count mismatch, wrong tick, slot outside the roster, duplicate slot, or missing slot. Only after every validation succeeds is the immutable FrameData published. The caller's collection is never stored, and later caller mutation cannot affect the frame.

The resulting internal layout is always:

~~~text
inputs[0] = Slot 0
inputs[1] = Slot 1
inputs[N-1] = Slot N-1
~~~

`GetInput` performs a roster range check and returns a value copy. No backing array or mutable enumerable is exposed.

## 7. StrictFrameCollector State Machine

The collector is a one-shot object for one exact tick:

~~~text
StrictFrameCollector(ActiveRoster roster, uint targetTick)
bool Submit(PlayerId submittedPlayerId, InputFrame input)
bool IsComplete
FrameData GetCompletedFrame()
~~~

Its only successful transition is:

~~~text
Collecting --last valid input--> Complete
~~~

`Submit` returns:

- `false`: this valid input was accepted, but at least one roster slot remains missing;
- `true`: this valid input was accepted and completed the frame for the first time;
- an exception for every rejection; rejection is never encoded as `false`.

Validation order is fixed:

1. collector is still Collecting;
2. input tick equals the target tick;
3. input slot belongs to the roster;
4. submitted PlayerId exists in the roster;
5. that PlayerId's assigned slot equals the input slot;
6. the slot has not already been accepted;
7. only then may pending state change.

| Rejection | Exception category |
|---|---|
| Wrong tick | `ArgumentException` |
| Slot outside the roster | `ArgumentOutOfRangeException` |
| PlayerId absent from roster | `ArgumentException` |
| PlayerId belongs to a different slot | `ArgumentException` |
| Duplicate slot | `InvalidOperationException` |
| Submit after completion | `InvalidOperationException` |
| Get frame before completion | `InvalidOperationException` |

Tests lock the exception category and unchanged-state result, not message text.

Pending storage is one InputFrame array plus one presence array indexed by slot. The final Submit creates a candidate complete input set and calls the same `FrameData.Create` validation path. The collector commits its final pending value, cached frame, and Complete state only after FrameData creation succeeds. There is no reset, next-tick, timeout, history, scheduler, pool, or reuse API.

The direct `FrameData.Create` entry represents inputs whose identity attribution has already been handled, such as deterministic tests or recorded in-memory history. Any flow needing identity/slot attribution uses the mandatory-identity collector API.

## 8. Variable-Player Battle State and Step

`BattleState` changes from explicit P0/P1 fields to:

~~~text
sealed immutable BattleState
  BattleState(uint tick, ActiveRoster roster, IReadOnlyList<PlayerState> statesInSlotOrder)
  static CreateInitial(ActiveRoster roster, IReadOnlyList<PlayerState> statesInSlotOrder)
  uint Tick
  ActiveRoster Roster
  int PlayerCount
  PlayerState GetPlayerState(PlayerSlot slot)
~~~

The state count must equal the roster count. Construction defensively copies the states, does not expose its backing array, and `GetPlayerState` checks the slot against the roster.

The no-argument initial-state factory and P0/P1 spawn constants are removed. Gate 3 callers supply explicit initial player states. The public explicit-tick constructor remains; the separate validation boundary for future Snapshot/Restore stays deferred.

`BattleSimulation.Step` is atomic:

1. capture the current state;
2. reject a null frame, wrong tick, or structurally different roster;
3. compute every next PlayerState in explicit ascending slot order into a fresh local array;
4. compute `checked(current.Tick + 1)`;
5. successfully construct the next immutable BattleState;
6. assign `State` exactly once.

Any exception leaves the prior State and digest unchanged. Gate 3 accepts straightforward arrays and defensive copies; it adds no pooling, Span framework, allocator, ECS, or mutable-state reuse.

## 9. Canonical Digest Schema

StateDigest continues to use FNV-1a 64 with offset basis `14695981039346656037` and prime `1099511628211`. Every integer is emitted byte by byte in explicit little-endian order:

~~~text
BattleState.Tick     uint32
Roster.Count         checked int32 -> uint32

for PlayerSlot = 0..Count-1:
    PlayerId.Value   uint64
    PositionX        int32 two's-complement bits
    PositionZ        int32 two's-complement bits
    Aim              uint16
~~~

The slot is represented by its position in the stream; writing the redundant sequence from `0` through `Count - 1` adds no information. Count, ordered IDs, and ordered states distinguish roster membership, roster order, and player state.

Digest code must not use object references, `GetHashCode`, reflection order, platform memory layout, serialization, or unordered collection iteration.

The Gate 2 digest `0x04633D1F8699DE68` remains historical evidence for the former schema. Gate 3 intentionally establishes a new schema and baseline.

## 10. Approved Four-Player Golden Vector

The vector runs input ticks `0..999` and finishes at BattleState tick 1000.

### 10.1 Roster and initial states

| Slot | PlayerId | Initial X | Initial Z | Initial Aim |
|---:|---:|---:|---:|---:|
| 0 | `0x0102030405060708` | -1000 | 0 | 0 |
| 1 | `0x000000000000002A` | 1000 | 0 | 0 |
| 2 | `0xFFEEDDCCBBAA0099` | 0 | -1000 | 0 |
| 3 | `0x00000000000F4243` | 0 | 1000 | 0 |

IDs are deliberately not sorted by slot. The vector constructs two distinct ActiveRoster instances from the same ordered IDs: the initial BattleState uses one, while every collector uses the other. This executes structural roster equivalence rather than object-reference equivalence.

### 10.2 Movement

For `phase = tick % 400`:

| Phase | Slot 0 | Slot 1 | Slot 2 | Slot 3 |
|---|---|---|---|---|
| 0–99 | X +1 | X -1 | Z +1 | Z -1 |
| 100–149 | neutral | neutral | neutral | neutral |
| 150–249 | X -1 | X +1 | Z -1 | Z +1 |
| 250–324 | Z +1 | Z -1 | X +1 | X -1 |
| 325–399 | Z -1 | Z +1 | X -1 | X +1 |

Existing movement remains 100 integer units per commanded axis and clamps X to `[-5000, 5000]` and Z to `[-3000, 3000]`.

### 10.3 Aim

~~~text
Slot 0 = unchecked ushort(tick *  997 +   123)
Slot 1 = unchecked ushort(tick *  619 + 45678)
Slot 2 = unchecked ushort(tick *  313 +   777)
Slot 3 = unchecked ushort(tick * 1597 + 40000)
~~~

### 10.4 Submission order

| `tick % 4` | Submitted slots |
|---:|---|
| 0 | 2, 0, 3, 1 |
| 1 | 1, 3, 0, 2 |
| 2 | 3, 2, 1, 0 |
| 3 | 0, 2, 1, 3 |

Each Submit includes the PlayerId assigned to that input's slot.

### 10.5 Frozen result

| Slot | PlayerId | Final X | Final Z | Final Aim |
|---:|---:|---:|---:|---:|
| 0 | `0x0102030405060708` | 0 | -3000 | 13086 |
| 1 | `0x000000000000002A` | 0 | 3000 | 8699 |
| 2 | `0xFFEEDDCCBBAA0099` | -2500 | -2000 | 51320 |
| 3 | `0x00000000000F4243` | 2500 | 2000 | 62539 |

The independently approved 80-byte digest stream is:

~~~text
e803000004000000
08070605040302010000000048f4ffff1e33
2a0000000000000000000000b80b0000fb21
9900aabbccddeeff3cf6ffff30f8ffff78c8
43420f0000000000c4090000d00700004bf4
~~~

The frozen Gate 3 Golden Digest is:

~~~text
0x89A7DD66F8D9E871
~~~

`Gate3GoldenVector.cs` owns only the roster, initial state, input generation, collector execution, Simulation execution, and returned actual state/digest. It contains no expected state or expected digest. Unity and Server each own separate literal assertions for the complete result.

## 11. Source Topology and Breaking Migration

The embedded package remains the only Runtime source home:

~~~text
Packages/com.locksteparena.simulation/Runtime/
  ActiveRoster.cs             new
  PlayerId.cs                 new
  PlayerSlot.cs               new
  StrictFrameCollector.cs     new
  InputFrame.cs               modify
  FrameData.cs                modify
  BattleState.cs              modify
  BattleSimulation.cs         modify
  StateDigest.cs              modify
  SimulationConfig.cs         remove fixed spawn constants
  PlayerState.cs              preserve unchanged when possible
  LockstepArena.Simulation.asmdef
  LockstepArena.Simulation.csproj
  Directory.Build.props
~~~

Gate 3 intentionally removes without compatibility shims:

- `BattleState.Player0` and `Player1`;
- `FrameData.Player0Input` and `Player1Input`;
- the two-input FrameData constructor;
- no-argument `BattleState.CreateInitial()`;
- P0/P1 spawn constants;
- the byte slot and its `0/1` restriction.

No `[Obsolete]` properties, two-player adapter, second assembly, package, interface layer, factory, DI, event bus, or generic player/entity collection is added.

The existing package version remains unchanged because the embedded package is not published. Gate 2's `noEngineReferences`, `autoReferenced: false`, netstandard2.1/C# 9, warnings-as-errors, and `.artifacts/` output isolation contracts remain unchanged.

## 12. Test and Runtime Evidence

The dependency-free .NET test executable retains all fifteen Gate 1 verification intentions while migrating their APIs. Gate 3 adds focused ActiveRoster and frame-collection tests. Required evidence includes:

- defensive copying and no mutable backing-store exposure for ActiveRoster, FrameData, and BattleState;
- duplicate roster identity rejection and explicit `TryGetSlot` failure;
- range checks on every public PlayerSlot lookup;
- structural roster equality and order sensitivity;
- canonical 2-, 3-, and 4-player FrameData;
- every FrameData and collector rejection, with unchanged pending or Simulation state;
- two independent four-player simulations over 10,000 ticks with per-tick digest equality;
- initial state plus a 3- or 4-player in-memory frame list rebuilding the same digest;
- digest sensitivity to tick, count, identity, identity order, and every player state;
- the approved 80-byte field-order Golden Digest;
- Unity and .NET Server each executing and independently asserting the four-player 1,000-tick vector.

History remains an ordinary `List<FrameData>` in test code. It does not create a production FrameHistory or Replay feature.

## 13. Verification Matrix

| Verification | Required result |
|---|---|
| Simulation Release build | 0 warnings, 0 errors |
| .NET regression executable | every migrated Gate 1 intent plus every Gate 3 test passes |
| 2/3/4-player evidence | all three roster sizes execute and validate canonical frames |
| Server Verification | exact four-player state and digest `89A7DD66F8D9E871` |
| Unity EditMode | named Gate 3 Golden test is discovered and passed |
| Unity NUnit XML | total at least 1, passed expected test, failed 0 |
| Source uniqueness | every production `.cs` and Golden Vector has one tracked path |
| Runtime dependency scan | no Unity, test framework, network, Protobuf, or later-gate dependency |
| Fixed-two-player scan | no production Player0, Player1, exactly-two, or slot-0/1 restriction |
| Artifact scan | no package Runtime bin, obj, DLL, symlink, junction, copy, or sync mechanism |
| Project-scope scan | no Gate 3 change under Assets, ProjectSettings, or Packages/manifest.json |

Unity verification must run in the Gate 3 worktree and parse fresh NUnit XML. An instance lock, license failure, or environmental failure stops the work; the normal checkout is never used as a substitute.

## 14. Explicitly Out of Scope

- room creation, room configuration, host-selected player-count UI, lobby, ready, or battle lifecycle;
- login, accounts, sessions, or PlayerId generation;
- TCP, UDP, KCP, sockets, transport, packets, or Protobuf;
- TickClock, fixed input delay, future-tick scheduling, timeout, or network simulation;
- missing-input substitution, prediction, dirty frames, snapshot, rollback, catch-up, reconnect, or desync recovery;
- production FrameHistory, Replay, serialization, persistence, or file I/O;
- join-in-progress, leave, disconnect removal, AI takeover, or roster mutation;
- Unity GameObject, Transform, view adapter, interpolation, or rendering;
- combat, projectile, collision, damage, health, death, score, or results;
- pool, Span framework, custom allocator, ECS, generic entity framework, or package publication.

## 15. Planning and Isolation Contract

- Planning branch: `codex/gate3-variable-roster-frame-sync`.
- Worktree: `.worktrees/gate3-variable-roster-frame-sync`.
- Exact base: `af86372ed598bd17dc0e42c9fc3571225ed050d0`.
- The first planning commit contains only this design and its Implementation Plan.
- Runtime, Tests, Unity, and Server implementation cannot begin before independent Planning PASS.
- The normal checkout's user-owned changes to `Assets/Settings/Mobile_RPAsset.asset` and `ProjectSettings/ShaderGraphSettings.asset` must remain untouched.

## 16. Gate 3 Exit Criteria

An implementation may later be submitted for Gate 3 approval only when:

1. all verification-matrix rows have fresh passing evidence;
2. the implementation branch is pushed and its remote ref equals local HEAD;
3. the Gate 3 worktree is clean and the normal checkout was only inspected;
4. the Handoff records exact commits, build/test outputs, Unity XML counts, named test, Golden state/digest, uniqueness/dependency/artifact/fixed-coupling/scope audits, and environmental observations;
5. work stops without beginning any room, protocol, transport, timing, prediction, snapshot, rollback, replay, view, or combat gate.

## 17. Implementation Evidence

Gate 3 was implemented in `.worktrees/gate3-variable-roster-frame-sync` from approved base `af86372ed598bd17dc0e42c9fc3571225ed050d0`. The implementation commits before this evidence update are:

- `f223f1aa3cc230f3549731097495362cfe33ca35` — immutable `PlayerId`, `PlayerSlot`, and `ActiveRoster`;
- `a0fadc88cc873a3fe276888c85cc9829af6639ae` — variable-roster frame collection, Simulation and Digest migration, 38-test suite, and shared Gate 3 consumers;
- `d841a054dedcc5bb5c7d534e83beccd6c594239a` — Unity-generated metadata for the four new Runtime source files.

Fresh Release verification reported `0 warnings` and `0 errors` for each of:

- `Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj`;
- `Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj`;
- `Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj`.

The dependency-free .NET suite printed `RESULT 38/38 passed`. This includes canonical 2-, 3-, and 4-player frames, every specified strict-collector rejection, 10,000-tick per-tick twin digests, and 2,000-frame initial-state/history reconstruction. The offline Server consumer printed exactly:

~~~text
PASS Gate3GoldenVector Tick=1000 Players=4 Digest=89A7DD66F8D9E871
~~~

Unity `6000.3.10f1` at `E:\unityhub\unity6.3\Editor\Unity.exe` ran the Gate 3 worktree with:

~~~text
-batchmode -runTests
-projectPath <Gate 3 worktree>
-testPlatform EditMode
-assemblyNames LockstepArena.Simulation.Editor.Tests
-testResults <temporary results.xml>
-logFile <temporary editor.log>
~~~

Fresh NUnit XML reported `total=1 passed=1 failed=0`. The named test `LockstepArena.Simulation.Editor.Tests.UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector` was present with result `Passed`. Both the Unity test and Server process compiled and executed the one physical `Gate3GoldenVector.cs`, while independently asserting this final state:

| Slot | PlayerId | X | Z | Aim |
|---:|---:|---:|---:|---:|
| 0 | `0x0102030405060708` | 0 | -3000 | 13086 |
| 1 | `0x000000000000002A` | 0 | 3000 | 8699 |
| 2 | `0xFFEEDDCCBBAA0099` | -2500 | -2000 | 51320 |
| 3 | `0x00000000000F4243` | 2500 | 2000 | 62539 |

Both consumers independently asserted digest `0x89A7DD66F8D9E871`.

The final audits established:

- each of the eleven production Runtime `.cs` files has exactly one tracked path under `Packages/com.locksteparena.simulation/Runtime/`, and `Gate3GoldenVector.cs` also has exactly one tracked path;
- `Gate2GoldenVector.cs` is no longer tracked, and no copy/sync script, symlink, junction, precompiled Shared DLL, package `Runtime/bin`, or package `Runtime/obj` exists;
- Runtime has no Unity, UnityEditor, test-framework, networking, Protobuf, room, timing, prediction, snapshot, rollback, replay, view, or combat dependency, and no production `Player0`, `Player1`, exactly-two-player rule, or slot-0/1 restriction;
- ActiveRoster, FrameData, and BattleState expose no public array surface; their defensive-copy, range-check, structural-roster, atomic collector-reject, and atomic Simulation-step contracts are covered by the passing suite;
- the Golden Vector contains no Unity/test/file/time/environment/random dependency and no expected final-state or expected-digest literal; those expected literals are present separately in both consumers;
- `PlayerState.cs` retained approved blob `e90bd93a1c96c0eb36a1b9c74d4d3d8a062e2e32`;
- Gate 3 changed nothing under `Assets/`, `ProjectSettings/`, or `Packages/manifest.json` relative to the approved base.

Unity import serialization changes were inspected and restored only inside the Gate 3 worktree: the first run touched `Assets/Settings/Mobile_RPAsset.asset`, `ProjectSettings/EditorBuildSettings.asset`, and `ProjectSettings/ShaderGraphSettings.asset`; the final fresh run touched only `Assets/Settings/Mobile_RPAsset.asset`. None was committed. The normal checkout was never used for implementation or verification and still contains only the two pre-existing user-owned modifications named in Section 15.
