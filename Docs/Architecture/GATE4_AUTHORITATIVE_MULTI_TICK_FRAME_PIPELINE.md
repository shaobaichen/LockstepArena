# Lockstep Arena — Gate 4 Offline Authoritative Multi-Tick Frame Pipeline

> Status: implementation complete; pending independent Gate 4 approval
>
> Approved base: `0137342be01d15ae52f437ef53a9fdd0f3437c85`
>
> Scope: given an already-fixed ActiveRoster and no network or wall clock, accept multiplayer inputs for several ticks in arbitrary order, reuse one StrictFrameCollector per tick, and publish only the continuous complete prefix as authoritative FrameData.

## 1. Goal

Gate 3 proved the complete-frame rule for one specified tick. Gate 4 adds only the Server-owned authority that organizes several such ticks. It must prove this chain:

~~~text
arbitrary arrival order
  -> exact Tick buckets
  -> Gate 3 StrictFrameCollector validation per Tick
  -> completed future frames wait behind gaps
  -> only the continuous prefix is published
  -> one authoritative FrameData sequence
  -> Server Simulation consumes it in Tick order
  -> different arrival orders produce the same per-Tick Digest
~~~

Gate 4 remains offline. It does not decide what happens when an input never arrives.

## 2. Ownership Boundary

`AuthoritativeFrameCoordinator` belongs to the Server/application authority layer. One already-started battle owns one coordinator. It is not added to the embedded Unity package and does not alter Shared Simulation.

Gate 3 responsibilities remain unchanged:

- `ActiveRoster` defines the immutable Slot-to-PlayerId mapping;
- `StrictFrameCollector` validates one Tick's identity, Slot, duplicate, completeness, and canonical order rules;
- `FrameData` is the immutable complete frame;
- `BattleSimulation` only evaluates `BattleState + complete FrameData -> next BattleState`;
- `StateDigest` continues to hash Shared deterministic state.

The coordinator owns only Tick routing, the acceptance window, continuous publication, and bounded authoritative history. It does not hold `BattleSimulation`.

## 3. Tick Semantics

| Concept | Meaning |
|---|---|
| Simulation current Tick | `BattleSimulation.State.Tick`; the Tick required by the next `Step` |
| Server publication Tick | `AuthoritativeFrameCoordinator.NextPublishTick`; the earliest Tick not yet formally published |
| Pending future Tick | An accepted Tick greater than `NextPublishTick`; it may be incomplete or complete but blocked by an earlier gap |

Server bootstrap supplies the same initial Tick and structurally equal roster to Simulation and Coordinator. After `Submit` returns several published frames, Coordinator may temporarily be ahead of Simulation. The composition code consumes the returned array in ascending Tick order until both Tick values match again.

The difference during batch delivery is valid. If a later `Simulation.Step` fails, the battle exposes a composition/deterministic invariant failure. Coordinator publication is not rolled back.

## 4. Minimal Public API

~~~csharp
public sealed class AuthoritativeFrameCoordinator
{
    public AuthoritativeFrameCoordinator(
        ActiveRoster roster,
        uint initialPublishTick,
        uint maxFutureTickOffset,
        int authoritativeHistoryCapacity);

    public ActiveRoster Roster { get; }

    public uint NextPublishTick { get; }

    public FrameData[] Submit(
        PlayerId submittedPlayerId,
        InputFrame input);

    public FrameData[] GetAuthoritativeHistorySnapshot();
}
~~~

`Submit` returns only publications caused by that invocation:

- empty array: the input was accepted but a gap still prevents publication;
- one element: the current Tick became publishable;
- several elements: closing a gap released a continuous completed prefix;
- exception: the input was rejected.

No `AuthoritativeFrameData` wrapper, Result framework, custom exception hierarchy, Publish/Acknowledge API, or mutable collection is added.

## 5. Production Data

The coordinator contains only:

~~~text
ActiveRoster _roster
uint _nextPublishTick
uint _maxFutureTickOffset
int _authoritativeHistoryCapacity
Dictionary<uint, StrictFrameCollector> _pendingByTick
Queue<FrameData> _authoritativeHistory
~~~

The Dictionary is used exclusively for exact Tick lookup. Publication never depends on Dictionary enumeration. The Queue contains only formally published frames and preserves their Tick order.

The coordinator is designed for serialized, single-threaded calls for one battle. It contains no locks, concurrent collection, async method, scheduler, or clock abstraction.

## 6. Constructor and Window Contract

The constructor rejects a null roster and a non-positive history capacity. `maxFutureTickOffset` is an explicit unsigned test/application parameter; zero is legal and accepts only `NextPublishTick`.

For `NextPublishTick = P`, where `P < uint.MaxValue`, the allowed range is:

~~~text
P <= input.Tick <= min((ulong)P + maxFutureTickOffset, uint.MaxValue - 1)
~~~

The calculation uses `ulong`. An input for `uint.MaxValue` is always rejected. An input below `NextPublishTick` is old; an input above the widened upper bound is too far in the future.

`uint.MaxValue - 1` is the last publishable and consumable Frame Tick. Publishing it sets `NextPublishTick` to `uint.MaxValue`. That value is the only exhausted representation. No Tick wraps to zero, and no extra `IsExhausted` field or property exists.

## 7. Submit Validation and Collector Transactions

The coordinator validates in this order:

1. capture the pre-submit `NextPublishTick`;
2. reject `input.Tick == uint.MaxValue`;
3. reject all submissions when `NextPublishTick == uint.MaxValue`;
4. reject an old Tick;
5. compute the widened future bound and reject a Tick beyond it;
6. exact-lookup the Tick's pending collector;
7. delegate identity, Slot, duplicate, Tick, and completeness validation to `StrictFrameCollector`;
8. only after a new collector accepts its first input, insert it into pending storage;
9. plan and, if possible, commit the continuous authoritative publication.

For a new Tick, the collector is constructed locally and submitted locally before Dictionary insertion. A rejected first input therefore leaves no empty pending entry. For an existing Tick, Gate 3's collector transaction guarantees that rejection does not change its accepted inputs.

Any rejection leaves `NextPublishTick`, authoritative history, other collectors, pending membership, and the target collector's previously accepted inputs unchanged.

## 8. Atomic Publication Planning

Publication is computed completely in locals before coordinator fields change.

Starting at the pre-submit `NextPublishTick`, a `ulong scanTick` performs exact lookups. Scanning stops at the first absent or incomplete collector. When the scan reaches `uint.MaxValue - 1`, it can include that frame and sets the planned next Tick directly to `uint.MaxValue`; it never increments or probes beyond it.

The planning phase creates:

- a fresh `FrameData[]` publication container;
- `nextPublishTickAfterBatch`;
- a copied pending Dictionary with the publication Tick keys removed;
- a new history Queue containing the retained old suffix plus the new batch, evicted to capacity.

The copied Dictionary may share unmodified `StrictFrameCollector` references. The publication array and history Queue may share immutable `FrameData` references. No collector or FrameData is deep-cloned.

After every check and allocation required by the domain algorithm succeeds, publication commits through final field assignments:

~~~text
_pendingByTick = pendingAfter
_authoritativeHistory = historyAfter
_nextPublishTick = nextPublishTickAfterBatch
~~~

There are no callbacks between these assignments. Under the approved serialized-call model, external code cannot observe a partially committed publication. The implementation must not remove pending entries, enqueue the live history Queue, or advance the live Tick while it is still scanning.

## 9. Authoritative Meaning

A completed future collector is not authoritative while a lower Tick is missing. A FrameData becomes authoritative when it is included in a successfully committed publication batch.

This fact does not wait for Simulation consumption. If Submit publishes `[100, 101, 102]`, then all three are authoritative before Submit returns, even if Simulation still needs Tick 100. Server composition must call `Step` in returned-array order. A Step failure stops and exposes the battle failure; Gate 4 has no cross-component transaction, compensation, retry, snapshot, or rollback.

## 10. Authoritative History

History stores only published FrameData. Completed future frames do not enter it. Every publication appends frames in ascending Tick order, then evicts from the oldest end until the configured capacity is met.

`GetAuthoritativeHistorySnapshot()` returns a new array in oldest-to-newest order. Replacing elements in either a publication array or a history snapshot cannot change the retained Queue. Immutable FrameData objects themselves are shared rather than reconstructed.

History is not a Replay, patch store, persistence layer, recovery source, or network message cache.

## 11. Server Project Layout

~~~text
Server/LockstepArena.Server.FrameSync/
  LockstepArena.Server.FrameSync.csproj
  AuthoritativeFrameCoordinator.cs
~~~

The class library targets .NET 8 with C# 12, nullable enabled, implicit usings disabled, and warnings treated as errors. It has one ProjectReference to the existing Simulation project and no PackageReference.

The current Server Verification executable remains an offline Gate 3 verifier and is not converted into a production host.

## 12. Test Project and Count

Gate 4 adds one dependency-free .NET 8 executable:

~~~text
Tests/LockstepArena.Server.FrameSync.Tests/
  LockstepArena.Server.FrameSync.Tests.csproj
  Program.cs
  CoordinatorContractTests.cs
  CoordinatorRosterTests.cs
  CoordinatorWindowTests.cs
  CoordinatorRejectTests.cs
  CoordinatorPublicationTests.cs
  CoordinatorHistoryTests.cs
  CoordinatorTickLimitTests.cs
  CoordinatorDeterminismTests.cs
  Gate4MultiTickGoldenVector.cs
~~~

The target is exactly `RESULT 32/32 passed`: 4 contract, 3 roster, 3 window, 6 rejection, 5 publication, 4 history, 3 Tick-limit, and 4 determinism/composition tests.

The existing Gate 3 suite remains a separate `38/38` regression gate.

## 13. Multi-Tick Golden Vector

The vector uses four players, Tick 0 through 11, `maxFutureTickOffset = 3`, and `authoritativeHistoryCapacity = 5`. These numbers are test parameters, not product defaults.

Roster by Slot:

| Slot | PlayerId |
|---:|---:|
| 0 | `0x0102030405060708` |
| 1 | `0x000000000000002A` |
| 2 | `0xFFEEDDCCBBAA0099` |
| 3 | `0x00000000000F4243` |

Initial players:

| Slot | X | Z | Aim |
|---:|---:|---:|---:|
| 0 | -1000 | 0 | 0 |
| 1 | 1000 | 0 | 0 |
| 2 | 0 | -1000 | 0 |
| 3 | 0 | 1000 | 0 |

For Tick `t`:

| Slot | MoveX | MoveZ | Aim |
|---:|---:|---:|---:|
| 0 | 1 | 0 | `t * 1000 + 1` |
| 1 | -1 | 0 | `t * 2000 + 2` |
| 2 | 0 | 1 | `t * 3000 + 3` |
| 3 | 0 | -1 | `t * 4000 + 4` |

Each four-Tick block begins at Tick 0, 4, or 8.

Coordinator A completes Tick offsets `3,2,1,0`, with Slot order `2,0,3,1` inside each Tick. Its non-empty batch sizes are `4,4,4`.

Coordinator B visits Slot passes `1,3,0,2`, using Tick offsets `2,0,3,1` inside each pass. Its non-empty batch sizes are `1,3,1,3,1,3`.

Both flattened publications must equal Tick `0..11` and have identical inputs at every Slot. Both retained histories must equal Tick `7..11`.

`Gate4MultiTickGoldenVector.cs` contains only actual vector construction and execution. Expected batch sizes, final state, retained history, and digest remain in the test consumer.

Approved final state:

| Slot | X | Z | Aim |
|---:|---:|---:|---:|
| 0 | 200 | 0 | 11001 |
| 1 | -200 | 0 | 22002 |
| 2 | 0 | 200 | 33003 |
| 3 | 0 | -200 | 44004 |

Final Tick is 12. Using the Gate 3 80-byte canonical state schema, the independently approved FNV-1a 64 digest is:

~~~text
0x5CFABE84CC00E1C3
~~~

## 14. Regression and Acceptance Matrix

| Evidence | Required result |
|---|---|
| Simulation Release build | 0 warnings, 0 errors |
| Gate 3 Simulation suite | 38/38 |
| Gate 3 Server Verification | `Digest=89A7DD66F8D9E871` |
| Gate 3 Unity EditMode | named test Passed, XML failed 0 |
| Server FrameSync Release build | 0 warnings, 0 errors |
| Gate 4 test executable | 32/32 |
| Flattened publications | both exactly Tick 0..11 |
| Batch sizes | A `4,4,4`; B `1,3,1,3,1,3` |
| History | both exactly Tick 7..11 |
| Dual Simulation | per-consumed-Tick digest equality |
| Final digest | `5CFABE84CC00E1C3` |
| Publication audit | local planning followed by final field replacement |
| Dictionary audit | no enumeration controls publication |
| Shared package audit | no diff from approved Gate 3 commit |
| Scope audit | no network, protocol, time policy, prediction, recovery, room, view, or combat |
| Artifact audit | no package bin, obj, DLL, link, copy, or sync mechanism |
| Project scope | no committed Assets, ProjectSettings, manifest, or package change |

## 15. Additive and Non-Breaking Impact

Gate 4 adds one Server class library and one test executable. Shared Simulation APIs, the Gate 3 schema/digest, the Gate 3 Golden Vector, Unity package, and existing Server Verification remain unchanged.

The repository-wide `*.csproj` ignore rule currently matches both planned authored project files. Implementation therefore requires exactly two negated `.gitignore` entries for those paths. No other build-system change is authorized.

## 16. Explicitly Out of Scope

- Protobuf or any serialization DTO;
- TCP, UDP, KCP, sockets, packets, or Unity networking;
- Room, Login, Session, lobby, or account behavior;
- TickClock, wall clock, timer, timeout, fixed input delay, or future-tick scheduling by time;
- neutral missing input, repeat-last input, disconnect policy, or reconnect;
- Prediction, dirty frame, Snapshot, Rollback, catch-up recovery, or formal Replay;
- Unity View, Transform, interpolation, or presentation;
- Combat, projectile, collision, damage, health, score, or result;
- interface/factory/DI, scheduler framework, generic timeline, middleware, event bus, transport abstraction, pool, or ring-buffer framework.

## 17. Planning Isolation

- Worktree: `.worktrees/gate4-authoritative-frame-pipeline`
- Branch: `codex/gate4-authoritative-frame-pipeline`
- Exact base: `0137342be01d15ae52f437ef53a9fdd0f3437c85`
- Planning commit contains only this document and the approved Implementation Plan.
- Server, Tests, Shared, Unity, `.gitignore`, and configuration implementation wait for independent Planning PASS.
- The normal checkout's two user-owned modifications remain untouched.

## 18. Exit Contract

Gate 4 may be submitted for implementation approval only after the planning commit is pushed and independently approved. A later implementation may be submitted for final Gate approval only after every acceptance row passes, the implementation branch is pushed, the worktree is clean, and work stops before any Protobuf, networking, timing policy, room, prediction, recovery, view, combat, or Gate 5 work begins.

## 19. Implementation Evidence

Fresh final verification was completed on 2026-08-30 from the isolated Gate 4 worktree.

### 19.1 Git Boundary

- Approved Gate 3 base: `0137342be01d15ae52f437ef53a9fdd0f3437c85`.
- Approved Gate 4 planning commit: `d86450446c5413bf1cb0cb835048bb47ef395d37`.
- Server project and constructor contract: `1c8a5fb1c21d54467b8371dea96b039203c7e766`.
- Strict per-Tick input routing: `b1049dc52cc557de2ae1ad8ba676a21badf0a103`.
- Continuous atomic publication: `089e99834b4d9286ccd517adfd9e673b2600a8e5`.
- Multi-Tick Golden evidence: `f26e2cb8e5a4d7d474a2db21c602189a2b078981`.
- The implementation contains no committed change under `Packages/com.locksteparena.simulation/`, `Assets/`, `ProjectSettings/`, or `Packages/manifest.json` relative to the approved Gate 3 base.

### 19.2 Fresh Release Builds

All five approved deliverables were built separately with `-c Release` and reported zero warnings and zero errors:

| Project | Warnings | Errors |
|---|---:|---:|
| `Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj` | 0 | 0 |
| `Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj` | 0 | 0 |
| `Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj` | 0 | 0 |
| `Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj` | 0 | 0 |
| `Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj` | 0 | 0 |

### 19.3 Fresh Runtime Evidence

- Gate 3 Simulation regression: `RESULT 38/38 passed`.
- Gate 4 Server FrameSync suite: `RESULT 32/32 passed`.
- Gate 3 Server verifier: `PASS Gate3GoldenVector Tick=1000 Players=4 Digest=89A7DD66F8D9E871`.
- Coordinator A non-empty publication batches: `4,4,4`.
- Coordinator B non-empty publication batches: `1,3,1,3,1,3`.
- Both flattened authoritative sequences: Tick `0..11`.
- Both retained authoritative histories at capacity five: Tick `7..11`.
- Both Simulations produced equal digests after every consumed authoritative Tick.
- Final Tick: `12`.
- Final Slot states: `(200,0,11001)`, `(-200,0,22002)`, `(0,200,33003)`, `(0,-200,44004)`.
- Final Gate 4 digest: `0x5CFABE84CC00E1C3` for both runs.
- A composition-failure test proves that a later `BattleSimulation.Step` exception does not roll back already committed Coordinator publication.

### 19.4 Fresh Unity Evidence

The unchanged Gate 3 Unity test was run from the Gate 4 worktree with:

~~~text
E:\unityhub\unity6.3\Editor\Unity.exe
-batchmode -runTests
-projectPath E:\unityproject\LockstepArena\.worktrees\gate4-authoritative-frame-pipeline
-testPlatform EditMode
-assemblyNames LockstepArena.Simulation.Editor.Tests
~~~

Final NUnit XML: `C:\Users\张晨旭\AppData\Local\Temp\locksteparena-gate4-final-verification-results.xml`.

- Unity version: `6000.3.10f1`.
- Unity exit code: `0`.
- XML counts: `total=1`, `passed=1`, `failed=0`.
- Named test: `LockstepArena.Simulation.Editor.Tests.UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector`.
- Named result: `Passed`.

The first successful post-license run automatically touched these worktree-local serialization paths, which were inspected and restored individually:

- `Assets/Settings/Mobile_RPAsset.asset`;
- `ProjectSettings/EditorBuildSettings.asset`;
- `ProjectSettings/ShaderGraphSettings.asset`.

The final verification run automatically upgraded only `Assets/Settings/Mobile_RPAsset.asset`; its diff was inspected and that exact path was restored. No broad reset or clean command was used. The Gate 4 worktree was clean afterward.

### 19.5 Final Audits

- Atomic publication: continuous frames are planned locally, copied pending/history containers are constructed, then the three live fields are replaced at the end.
- No incremental publication mutation: the live pending Dictionary and history Queue are not removed/enqueued/dequeued while scanning, and the live Tick is not incremented during planning.
- Dictionary ordering: pending storage is used only for exact Tick lookup; no enumeration, sorting, Keys, or Values operation controls publication.
- Shared ownership: the Coordinator does not call `FrameData.Create`, `TryGetSlot`, or `GetPlayerId`, and does not own `BattleSimulation` or `StateDigest`.
- Dependencies and scope: no Unity, NUnit, network, protocol, room/session, clock/timeout, missing-input policy, prediction, rollback, Replay, View, or Combat dependency exists in the production Server assembly. The approved `GetAuthoritativeHistorySnapshot` container-copy API remains distinct from out-of-scope Simulation Snapshot functionality.
- Copy boundary: publication uses a new array, copied Dictionary, and copied Queue while sharing immutable FrameData and existing pending collectors; no deep clone exists.
- Source boundary: exactly one tracked `AuthoritativeFrameCoordinator.cs` exists, and both authored project files are tracked.
- Link/script/artifact boundary: no tracked or filesystem symlink/junction, Gate 4 script, package `bin`, package `obj`, or package DLL exists.
- Golden separation: the actual vector contains no expected batch/history/final-state/digest literals; consumer tests independently own all frozen expectations.
- Normal checkout preservation: it still contains only the user-owned modifications to `Assets/Settings/Mobile_RPAsset.asset` and `ProjectSettings/ShaderGraphSettings.asset`.

Gate 4 remains strictly offline. Protobuf, networking, time and missing-input policies, rooms, prediction, recovery, View, Combat, and Gate 5 work remain absent.
