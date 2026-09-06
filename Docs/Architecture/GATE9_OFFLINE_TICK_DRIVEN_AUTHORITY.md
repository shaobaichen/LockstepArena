# Gate 9: Offline Tick-Driven Authority Scheduling & Fixed Input Delay

## 1. Status and Frozen Base

Gate 9 is an offline Server-authority scheduling gate. It freezes when a complete input frame becomes eligible for authoritative publication. It does not add a real clock, networking, missing-input policy, or Client runtime loop.

Frozen Gate 8 base:

```text
a91641f5a6a973833c62b13e950a234fbef9552b
```

Gate 9 proves:

```text
explicit logical AdvanceOneTick calls
-> fixed publication delay measured in logical ticks
-> Gate 4 pending collectors remain the single collection owner
-> complete and eligible continuous frames become authoritative
-> BattleSimulation consumes the authoritative sequence
-> different input arrival orders produce identical states and Digests
```

## 2. Production Ownership

Gate 9 creates no production assembly. The new type belongs to the existing Server FrameSync assembly:

```text
Assembly:  LockstepArena.Server.FrameSync
Namespace: LockstepArena.Server.FrameSync
Source:    Server/LockstepArena.Server.FrameSync/TickDrivenFramePublisher.cs
Type:      TickDrivenFramePublisher
```

`TickDrivenFramePublisher` owns only fixed-delay logical time and one existing `AuthoritativeFrameCoordinator`. It does not own a `BattleSimulation`, Protocol mapper, protobuf message, transport, wall-clock source, Timer, thread, or background loop.

Gate 4 `AuthoritativeFrameCoordinator` remains the unique owner of:

- the pending Tick dictionary;
- every `StrictFrameCollector`;
- PlayerId/PlayerSlot ownership validation;
- duplicate, Tick, completeness, and canonical-order validation;
- continuous authoritative publication;
- bounded authoritative history.

Shared Simulation remains unaware of `AdvanceOneTick`, InputDelay, eligibility, collection time, and pending timelines.

## 3. Exact Public API

```csharp
public sealed class TickDrivenFramePublisher
{
    public TickDrivenFramePublisher(
        ActiveRoster roster,
        uint initialTick,
        uint inputDelayTicks,
        uint maxFutureTickOffset,
        int authoritativeHistoryCapacity);

    public ActiveRoster Roster { get; }

    public uint InputDelayTicks { get; }

    public ulong CollectionTick { get; }

    public uint? EligibilityCeiling { get; }

    public uint NextPublishTick { get; }

    public FrameData[] Submit(
        PlayerId submittedPlayerId,
        InputFrame input);

    public FrameData[] AdvanceOneTick();

    public FrameData[] GetAuthoritativeHistorySnapshot();
}
```

Construction requires:

```text
roster != null
initialTick != uint.MaxValue
authoritativeHistoryCapacity > 0
```

The history-capacity validation and roster storage are delegated to Gate 4 where possible. `maxFutureTickOffset` and `inputDelayTicks` are independent. No size relationship between them is required.

For example, this is valid:

```text
initialTick = 100
inputDelayTicks = 2
maxFutureTickOffset = 1
```

The publisher can accept Tick100/Tick101, mature and publish Tick100, move `NextPublishTick`, and then accept Tick102 through the moved Gate 4 future window.

## 4. Tick Ownership

| Concept | Type | Owner | Meaning |
|---|---:|---|---|
| Collection Tick | `ulong` | `TickDrivenFramePublisher` | Current logical input-collection time coordinate |
| Eligibility Ceiling | `uint?` | `TickDrivenFramePublisher` | Greatest Frame Tick currently permitted to publish; absent during positive-delay warm-up |
| NextPublishTick | `uint` | Gate 4 Coordinator | Earliest continuous Tick not yet published |
| Simulation State Tick | `uint` | Caller-owned `BattleSimulation` | Frame Tick the Simulation must consume next |

Collection Tick is a publication-time coordinate, not a second future-input admission policy. Input admission remains governed exclusively by the Gate 4 future window relative to `NextPublishTick`.

`CollectionTick` is `ulong` so the last consumable `uint` Frame Tick can mature under a non-zero delay without arithmetic wraparound.

## 5. Exact InputDelay Mathematics

Define:

```text
S = initialTick
D = InputDelayTicks
A = number of successfully completed AdvanceOneTick calls
M = uint.MaxValue
```

After exactly `A` successful Advances:

```text
CollectionTick C(A) = (ulong)S + A
```

Eligibility is:

```text
A < D
-> E(A) = null

A >= D
-> E(A) = min(
     (ulong)S + (A - D),
     (ulong)M - 1)
```

For every consumable Frame Tick `T` where `S <= T <= M - 1`, its first eligible Advance count is:

```text
Afirst(T) = D + (T - S)
```

Eligibility is necessary but not sufficient for publication. Frame `T` publishes only when:

```text
T <= EligibilityCeiling
AND Frame T is complete
AND T == NextPublishTick
```

`InputDelayTicks` is immutable for the publisher lifetime. Gate 9 adds no dynamic or adaptive delay.

## 6. Frozen Off-by-One Example

For:

```text
initialTick = 100
InputDelayTicks = 2
```

the exact schedule is:

| Successful Advances | CollectionTick | EligibilityCeiling | Tick100 |
|---:|---:|---:|---|
| 0 | 100 | null | not eligible |
| 1 | 101 | null | not eligible |
| 2 | 102 | 100 | first eligible |
| 3 | 103 | 101 | already eligible |
| 4 | 104 | 102 | already eligible |

Tick100 first becomes eligible on the second successful `AdvanceOneTick`, never the first or third.

For zero delay:

```text
A = 0
CollectionTick = S
EligibilityCeiling = S
```

A complete initial Frame publishes from the final `Submit` without any Advance. Afterwards:

```text
NextPublishTick = S + 1
CollectionTick = S
EligibilityCeiling = S
```

## 7. Submit Contract

`Submit` performs no independent frame validation. It passes PlayerId and InputFrame to the same Gate 4 collector storage and then attempts publication only through the current eligibility ceiling.

The result means:

```text
[]
= input accepted, but no new authoritative frame

[Frame T]
= this call produced one authoritative frame

[Frame T ... Frame N]
= this call filled a mature gap and produced a continuous eligible batch
```

The returned array is an independent container. Immutable `FrameData` objects are not deep-copied.

A rejected input must not change Collection Tick, Eligibility Ceiling, pending membership, previously accepted collector contents, history, or `NextPublishTick`.

## 8. AdvanceOneTick Contract

```csharp
public FrameData[] AdvanceOneTick();
```

Each successful call:

1. calculates the next successful Advance count locally;
2. calculates the next `CollectionTick` and `EligibilityCeiling` with widened arithmetic;
3. asks Gate 4 to publish only the complete continuous prefix not exceeding that ceiling;
4. commits the successful Advance count only after Gate 4 publication succeeds;
5. returns only the authoritative publication created by this call.

An empty array means logical time advanced but no frame simultaneously satisfied maturity, completeness, and continuity.

`AdvanceOneTick` does not collect input, manufacture a neutral Frame, invoke Simulation, read time, sleep, retry, or compensate an external component.

## 9. Gate 4 Internal Extraction

`AuthoritativeFrameCoordinator` receives a behavior-preserving internal extraction:

```csharp
internal void Collect(
    PlayerId submittedPlayerId,
    InputFrame input);

internal FrameData[] PublishThrough(
    uint inclusiveEligibilityCeiling);
```

The existing public API remains unchanged:

```csharp
public FrameData[] Submit(
    PlayerId submittedPlayerId,
    InputFrame input);
```

Its effective implementation remains:

```text
Collect(submittedPlayerId, input)
-> PublishThrough(uint.MaxValue - 1)
```

### 9.1 Collect validation order

The extraction must not weaken or reorder the existing Gate 4 pre-publication contract. `Collect` preserves this effective order:

1. reject `input.Tick == uint.MaxValue`;
2. reject an exhausted Coordinator where `NextPublishTick == uint.MaxValue`;
3. reject `input.Tick < NextPublishTick`;
4. compute the future-window upper bound in `ulong` and reject beyond it;
5. locate or transactionally create the Tick's `StrictFrameCollector`;
6. delegate PlayerId/Slot ownership and duplicate rejection to that collector;
7. add a new pending dictionary entry only after its first Submit succeeds.

`StrictFrameCollector` continues to own completeness. No second canonicalization or roster check is added.

### 9.2 PublishThrough behavior

`PublishThrough` scans only from `NextPublishTick`; it never enumerates the pending dictionary to choose order. It stops on the first missing/incomplete Tick, the inclusive ceiling, or the final consumable Tick.

Publication keeps the Gate 4 atomic algorithm:

```text
complete local batch planning
-> copied pending dictionary
-> copied history queue
-> final field replacement
```

When:

```text
inclusiveEligibilityCeiling < NextPublishTick
```

it must return `Array.Empty<FrameData>()` and leave pending storage, collector contents, history, and `NextPublishTick` unchanged. This is a normal operation, not an error.

## 10. Warm-up, Maturity, and Gap Rules

### 10.1 Complete but not mature

A complete Frame remains in Gate 4 pending storage when eligibility is absent or its Tick exceeds the ceiling. It is not authoritative, does not enter history, and does not move `NextPublishTick`.

### 10.2 Mature but incomplete

A mature but incomplete Frame blocks publication. Advancing logical time never supplies missing input.

### 10.3 Mature future Frames behind a gap

If Tick101 and Tick102 are mature and complete while Tick100 is incomplete and `NextPublishTick == 100`, neither future Frame is authoritative.

### 10.4 Eligible-prefix gap-fill

If the ceiling is 101, completing Tick100 may publish `[100, 101]`, but a complete Tick102 remains pending until a later Advance makes it eligible.

### 10.5 Frontier ahead of ceiling

After zero-delay Tick `S` publishes, `NextPublishTick == S + 1` while the ceiling remains `S`. Collecting a complete Tick `S + 1` calls `PublishThrough(S)`, returns empty, and preserves that pending Frame. The next Advance raises the ceiling and publishes it.

## 11. uint.MaxValue Boundary

- `uint.MaxValue` is never a consumable Frame Tick.
- `uint.MaxValue - 1` is the final consumable Tick.
- Collection and eligibility calculations use `ulong` before a validated/clamped conversion to `uint`.
- Eligibility saturates at `uint.MaxValue - 1` and never wraps.
- Publishing the final Frame leaves Gate 4 `NextPublishTick == uint.MaxValue`.
- No cyclic Tick meaning exists.

Once Eligibility Ceiling is `uint.MaxValue - 1`, `AdvanceOneTick` throws `InvalidOperationException` before mutation because no later consumable Tick can become eligible. A late Submit may still complete an already-mature final pending Frame. Gate 4 then publishes it and becomes exhausted.

## 12. Two-Player Golden

```text
initialTick = 10
InputDelayTicks = 2
maxFutureTickOffset = 4
historyCapacity = 2

Slot0 PlayerId = 900
Slot1 PlayerId = 7

Initial:
Slot0 X=0 Z=0 Aim=100
Slot1 X=0 Z=0 Aim=200

Tick10:
Slot0 Move=( 1,0) Aim=101
Slot1 Move=(-1,0) Aim=201
Arrival: Slot1, Slot0
```

Expected publication:

```text
Submit Slot1 -> []
Submit Slot0 -> []
Advance A=1 -> []
Advance A=2 -> [10]
```

Expected final state:

```text
Tick=11
Slot0 X=100  Z=0 Aim=101
Slot1 X=-100 Z=0 Aim=201
Digest=AE353BEBCCF29139
```

## 13. Three-Player Golden

```text
initialTick = 20
InputDelayTicks = 1
maxFutureTickOffset = 4
historyCapacity = 2

Slot0 PlayerId = 500
Slot1 PlayerId = 1
Slot2 PlayerId = 300

Initial:
Slot0 X=0 Z=0 Aim=1000
Slot1 X=0 Z=0 Aim=2000
Slot2 X=0 Z=0 Aim=3000

Tick20:
Slot0 Move=( 1,0) Aim=1001
Slot1 Move=( 0,1) Aim=2001
Slot2 Move=(-1,0) Aim=3001
```

Expected execution:

```text
Submit Slot2, Slot0
Advance A=1 -> []
Submit Slot1 -> [20]
```

Expected final state:

```text
Tick=21
Slot0 X=100  Z=0   Aim=1001
Slot1 X=0    Z=100 Aim=2001
Slot2 X=-100 Z=0   Aim=3001
Digest=38CCC825F57B7655
```

## 14. Four-Player Golden

Configuration:

```text
initialTick = 100
InputDelayTicks = 2
maxFutureTickOffset = 8
historyCapacity = 3

Slot0 PlayerId = 0x0102030405060708
Slot1 PlayerId = 0x000000000000002A
Slot2 PlayerId = 0xFFEEDDCCBBAA0099
Slot3 PlayerId = 0x00000000000F4243
```

Initial state:

```text
Slot0 X=-300 Z=0    Aim=1000
Slot1 X=300  Z=0    Aim=2000
Slot2 X=0    Z=-300 Aim=3000
Slot3 X=0    Z=300  Aim=4000
```

Inputs:

```text
Tick100
Slot0 ( 1, 0, 10100)  Slot1 (-1, 0, 20100)
Slot2 ( 0, 1, 30100)  Slot3 ( 0,-1, 40100)

Tick101
Slot0 ( 0, 1, 10101)  Slot1 ( 0,-1, 20101)
Slot2 ( 1, 0, 30101)  Slot3 (-1, 0, 40101)

Tick102
Slot0 (-1, 0, 10102)  Slot1 ( 1, 0, 20102)
Slot2 ( 0,-1, 30102)  Slot3 ( 0, 1, 40102)

Tick103
Slot0 ( 0,-1, 10103)  Slot1 ( 0, 1, 20103)
Slot2 (-1, 0, 30103)  Slot3 ( 1, 0, 40103)
```

Primary arrival and Advances:

```text
Tick101: 3,1,0,2
Tick102: 2,0,3,1
Tick103: 1,3,0,2
Tick100 partial: 0,2,1

Advance A=1 -> []       ceiling=null
Advance A=2 -> []       ceiling=100, gap remains
Advance A=3 -> []       ceiling=101, gap remains
Submit Tick100 Slot3 -> [100,101]
Advance A=4 -> [102]
Advance A=5 -> [103]
```

Alternative arrival:

```text
Tick103: 2,0,3,1
Tick102: 1,3,0,2
Tick101: 0,2,1,3
Tick100 partial: 1,0,2
same Advances
final Tick100 Slot3
```

Both produce:

```text
publication batches: [100,101], [102], [103]
flattened sequence:   100,101,102,103
retained history:     101,102,103
```

Frozen state Digests:

```text
after Frame100 / State Tick101: D95809E1EB5CDDAA
after Frame101 / State Tick102: A96B83267DD72A7D
after Frame102 / State Tick103: 386C4BB11A7EB7E0

after Frame103 / State Tick104:
Slot0 X=-300 Z=0    Aim=10103
Slot1 X=300  Z=0    Aim=20103
Slot2 X=0    Z=-300 Aim=30103
Slot3 X=0    Z=300  Aim=40103
Digest=9F41F69F63A24BCB
```

The six Gate 9 Golden Digests were independently computed from the existing canonical little-endian FNV-1a64 state schema. Future implementation output may not update the expected values.

## 15. Exact Gate 9 Test Matrix

The new dependency-free .NET executable suite is:

```text
Tests/LockstepArena.Server.TickAuthority.Tests
RESULT 27/27 passed
```

Exact test names and order:

```text
1.  ConstructorRejectsNullRoster
2.  ConstructorAllowsFutureWindowSmallerThanInputDelay
3.  ZeroDelayStartsWithInitialTickEligible
4.  ZeroDelayCompleteInitialFramePublishesImmediatelyOnSubmit
5.  PositiveDelayStartsWithoutEligibilityCeiling
6.  AdvanceOneTickIncrementsCollectionTickExactlyOnce
7.  TickFirstBecomesEligibleAtDelayPlusTickOffset
8.  EligibilityCeilingAdvancesAtMostOneFrameTickPerAdvance
9.  CompleteFrameBeforeMaturityReturnsEmpty
10. AdvancePublishesCompleteFrameAtExactMaturity
11. MatureIncompleteFrameRemainsUnpublished
12. LateCompletionOfMatureFramePublishesImmediately
13. MatureFutureFramesRemainBlockedByNextPublishGap
14. GapFillPublishesOnlyEligibleContinuousPrefix
15. CompleteButIneligibleFramesRemainPendingAfterGapFill
16. LaterAdvancesPublishNewlyEligibleFramesInOrder
17. AuthoritativeHistoryContainsOnlyPublishedFrames
18. AuthoritativeHistoryCapacityRemainsBounded
19. RejectedSubmitPreservesScheduleAndExistingPendingInputs
20. PublicationBatchContainerIsIndependentFromHistorySnapshot
21. FinalConsumableTickBecomesEligibleWithoutWrap
22. EligibilityCeilingSaturatesAtFinalConsumableTick
23. AdvanceAfterEligibilitySaturationThrowsWithoutMutation
24. UintMaxInputIsRejectedWithoutScheduleMutation
25. TwoPlayerWarmupGoldenMatchesStateAndDigest
26. ThreePlayerLateCompletionGoldenMatchesStateAndDigest
27. FourPlayerArrivalOrdersProduceSameAuthorityAndPerTickDigests
```

Test 15 explicitly covers `PublishThrough(ceiling < NextPublishTick)` through the public Publisher path. Test 27 uses two independent Publishers, different arrival orders, identical logical Advances, and two independent `BattleSimulation` instances. It compares every authoritative Frame field and every per-Tick full state/Digest through final Tick104 and `9F41F69F63A24BCB`.

## 16. Source and Test Layout

Permitted production changes:

```text
Modify: Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs
Create: Server/LockstepArena.Server.FrameSync/TickDrivenFramePublisher.cs
```

The new test project contains exactly four authored files:

```text
Tests/LockstepArena.Server.TickAuthority.Tests/
  LockstepArena.Server.TickAuthority.Tests.csproj
  Program.cs
  TickDrivenFramePublisherTests.cs
  Gate9TickAuthorityGoldenVector.cs
```

Its only direct ProjectReferences are:

```text
Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj
Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj
```

It adds no test framework or package dependency. Expected state and Digest literals live only in `TickDrivenFramePublisherTests.cs`; the Golden vector returns actual results only.

The only permitted `.gitignore` change is:

```text
!Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj
```

## 17. Protected Boundaries

Relative to the frozen Gate 8 base:

- Shared Simulation committed diff must be zero;
- Protocol committed diff must be zero;
- StreamFraming committed diff must be zero;
- ProtocolAuthority committed diff must be zero;
- Gate 8 TCP project committed diff must be zero;
- Assets, ProjectSettings, manifest, and packages-lock committed diff must be zero;
- every pre-existing test source/project committed diff must be zero;
- `AuthoritativeFrameCoordinator` public constructor, `Submit`, history API, validation order, immediate-publication behavior, and atomicity remain behaviorally unchanged;
- Gate 3 through Gate 8 tests and Goldens remain green.

No production or test source may add:

- Timer, Stopwatch pacing, Task, Thread, async, sleep, Unity Update, or FixedUpdate;
- TCP, UDP, KCP, Socket, NetworkStream, connection lifecycle, retry, reconnect, or heartbeat;
- timeout, neutral input, repeat-last input, missing-input replacement, or adaptive delay;
- Prediction, Dirty Frame, Snapshot, Rollback, Replay, View, or Combat;
- scheduler framework, interface, factory, DI, middleware, EventBus, generic timeline, or transport abstraction.

## 18. Acceptance Boundary

Gate 9 ends after:

- the Gate 4 refactor is proven behavior-preserving;
- the exact 27 Gate 9 tests pass;
- the 2/3/4-player Golden results and per-Tick Digests match;
- all Gate 3-8 regressions pass;
- Unity's existing Gate 7/5/3 regressions pass from fresh NUnit XML;
- protected-source, dependency, scope, artifact, and ordinary-checkout audits pass;
- the implementation evidence is committed and the remote Gate 9 branch matches local HEAD.

Gate 9 then stops. It does not begin a real clock driver, production TCP, KCP, weak-network behavior, or any next Gate.

## 19. Implementation Evidence

Frozen comparison base:

```text
a91641f5a6a973833c62b13e950a234fbef9552b
```

Approved Planning HEAD and implementation commits:

```text
75a1340a5c7677a5754be23591708063a7206f93  docs: plan Gate 9 tick-driven authority
f657328c729914e44d5677c08ec349ae804fbda1  feat: gate authoritative frames by logical time
2481bf121985280722bbc6f43b8eaa4988d68a64  feat: preserve delayed authority tick limits
38de260ea19e676fd3a2e17602945f065290cda3  test: prove fixed-delay authority determinism
```

The evidence commit's parent is `38de260ea19e676fd3a2e17602945f065290cda3`.

### 19.1 Restore and Release builds

The restore-assets preflight resolved all 14 effective `ProjectAssetsFile` paths. Ten missing or stale project assets were restored using only their existing frozen project contracts. A transient NuGet vulnerability-index failure left an error-bearing CodeGen asset; after the same endpoint returned HTTP 200, the pinned CodeGen project was restored without dependency, source, version, generated-source, or project-XML changes.

The complete 14-project Release matrix was then restarted from build 1. Every build completed with:

```text
0 warnings
0 errors
```

### 19.2 Fresh .NET execution

```text
Gate 3 Simulation:         RESULT 38/38 passed
Gate 4 FrameSync:          RESULT 32/32 passed
Gate 5 Protocol:           RESULT 35/35 passed
Gate 6 ProtocolAuthority:  RESULT 24/24 passed
Gate 7 StreamFraming:      RESULT 32/32 passed
Gate 8 TCP watchdog:       RESULT 8/8 passed
Gate 9 TickAuthority:      RESULT 27/27 passed
Gate 3 Server Golden:      Tick=1000 Players=4 Digest=89A7DD66F8D9E871
```

The Gate 8 synchronous TCP suite completed inside the approved external 30-second process watchdog. The watchdog added no socket, gameplay timeout, or retry behavior.

### 19.3 Gate 9 deterministic results

Two-player warm-up:

```text
publication: [10]
final Tick:  11
Slot0: X=100  Z=0 Aim=101
Slot1: X=-100 Z=0 Aim=201
Digest: AE353BEBCCF29139
```

Three-player late completion:

```text
publication: [20]
final Tick:  21
Slot0: X=100  Z=0   Aim=1001
Slot1: X=0    Z=100 Aim=2001
Slot2: X=-100 Z=0   Aim=3001
Digest: 38CCC825F57B7655
```

Both independent four-player arrival orders produced:

```text
publication batches: [100,101], [102], [103]
flattened frames:     100,101,102,103
retained history:     101,102,103
CollectionTick:       105
EligibilityCeiling:   103
NextPublishTick:      104

State Tick101 Digest: D95809E1EB5CDDAA
State Tick102 Digest: A96B83267DD72A7D
State Tick103 Digest: 386C4BB11A7EB7E0
State Tick104 Digest: 9F41F69F63A24BCB
```

The final Tick104 state was:

```text
Slot0: X=-300 Z=0    Aim=10103
Slot1: X=300  Z=0    Aim=20103
Slot2: X=0    Z=-300 Aim=30103
Slot3: X=0    Z=300  Aim=40103
```

The two runs matched field-for-field for batch boundaries, roster, authoritative Frame data, every per-Frame Simulation state, and every Digest.

### 19.4 Unity 6000.3.10f1 regressions

Each regression ran separately from the Gate 9 worktree and produced fresh NUnit XML:

```text
.artifacts/gate9-unity/gate7-results.xml
total=1 passed=1 failed=0
UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden = Passed

.artifacts/gate9-unity/gate5-results.xml
total=2 passed=2 failed=0
GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads = Passed
UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector = Passed

.artifacts/gate9-unity/gate3-results.xml
total=1 passed=1 failed=0
UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector = Passed
```

After every Unity run, exact `Assets` / `ProjectSettings` diffs were inspected. Only confirmed Unity-generated serialization changes were restored by exact path inside the Gate 9 worktree.

### 19.5 Boundary and artifact audits

- Gate 4's public Coordinator API remained unchanged and its full 32-test behavior passed after the refactor.
- `Collect` preserved the approved validation order, including transactional candidate submission before pending-dictionary insertion.
- `PublishThrough` retained local planning, copied pending/history containers, and final field replacement; a ceiling below `NextPublishTick` is empty and state-preserving.
- `TickDrivenFramePublisher` contains only scalar scheduling state plus one Coordinator and owns no pending collection, history, Simulation, Protocol, transport, clock, task, thread, or recovery mechanism.
- Shared Simulation, Protocol, StreamFraming, ProtocolAuthority, Gate 8 TCP, pre-existing tests, Unity Assets/ProjectSettings, manifest, and packages-lock had zero committed diff from the frozen base.
- The FrameSync production diff contained exactly `AuthoritativeFrameCoordinator.cs` and `TickDrivenFramePublisher.cs`.
- `.gitignore` added exactly the approved TickAuthority test-project exception.
- The Gate 9 test project contained exactly four tracked files and two direct ProjectReferences.
- All six expected Gate 9 Digest literals existed only in the consumer test source, never in production or the actual-only Golden vector.
- No symlink/junction, copy/sync/cleanup script, new package, generated Gate 9 source, tracked `bin`/`obj`, or tracked/package-local LockstepArena build DLL was introduced.
- The ordinary checkout remained untouched with only its two pre-existing user-owned modifications.
