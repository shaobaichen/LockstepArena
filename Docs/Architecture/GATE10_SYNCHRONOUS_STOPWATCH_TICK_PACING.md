# Gate 10: Synchronous Stopwatch-Driven Tick Pacing

## 1. Status and Frozen Base

Gate 10 adds the smallest Server-side wall-clock pacing layer above the frozen Gate 9 logical scheduler. It converts monotonic elapsed `Stopwatch` ticks into zero, one, or many calls to the existing `TickDrivenFramePublisher.AdvanceOneTick()`.

Frozen Gate 9 base:

```text
e041efb290e1df609fc5003d619e4e19f70ded78
```

The proof chain is:

```text
Stopwatch monotonic elapsed time
-> StopwatchTickDriver
-> elapsed Stopwatch tick delta
-> ElapsedTickPacer
-> exact UInt128 accumulation at SimulationConfig.TickRate
-> 0 / 1 / N TickDrivenFramePublisher.AdvanceOneTick()
-> flattened authoritative FrameData[] in Advance order
-> caller-owned BattleSimulation
```

Gate 10 does not redefine InputFrame Tick, Collection Tick, Eligibility Ceiling, NextPublishTick, Simulation Tick, or Gate 9 fixed InputDelay semantics.

## 2. Production Ownership and Files

Gate 10 creates no production assembly. Both new types belong to the existing Server FrameSync assembly:

```text
Assembly:  LockstepArena.Server.FrameSync
Namespace: LockstepArena.Server.FrameSync

Server/LockstepArena.Server.FrameSync/ElapsedTickPacer.cs
Server/LockstepArena.Server.FrameSync/StopwatchTickDriver.cs
```

The existing `AuthoritativeFrameCoordinator.cs` and `TickDrivenFramePublisher.cs` remain unchanged.

The composition contract is single-threaded. After a Publisher is given to a pacer or driver, that pacer is the only component permitted to call its `AdvanceOneTick()`. The caller may retain the Publisher reference for `Submit`, history, and scheduling-state reads.

## 3. Exact Public API

```csharp
public sealed class ElapsedTickPacer
{
    public ElapsedTickPacer(
        TickDrivenFramePublisher publisher,
        long stopwatchFrequency);

    public FrameData[] ProcessElapsedStopwatchTicks(
        long elapsedStopwatchTicks);
}
```

```csharp
public sealed class StopwatchTickDriver
{
    public StopwatchTickDriver(
        TickDrivenFramePublisher publisher);

    public FrameData[] Poll();
}
```

No internal test API, public remainder/fault property, clock interface, callback, event, reset, recovery, or BattleSimulation dependency is added.

## 4. Exact State Ownership

`ElapsedTickPacer` contains only:

```csharp
private readonly TickDrivenFramePublisher _publisher;
private readonly ulong _stopwatchFrequency;
private ulong _fractionalTickNumerator;
private bool _faulted;
```

`StopwatchTickDriver` contains only:

```csharp
private readonly Stopwatch _stopwatch;
private readonly ElapsedTickPacer _pacer;
private long _lastElapsedStopwatchTicks;
```

| State | Owner |
|---|---|
| Real monotonic timestamp and Poll baseline | `StopwatchTickDriver` |
| Stopwatch frequency, fractional remainder, sticky fault | `ElapsedTickPacer` |
| Collection Tick and Eligibility Ceiling | `TickDrivenFramePublisher` |
| Pending collectors, authoritative history, NextPublishTick | `AuthoritativeFrameCoordinator` |
| BattleState and deterministic Simulation Tick | caller-owned `BattleSimulation` |

Shared Simulation remains entirely unaware of wall-clock time and pacing.

## 5. Construction and Numeric Types

Public elapsed-time values use `long` because BCL `Stopwatch.Frequency` and `Stopwatch.ElapsedTicks` are `long`:

```text
stopwatchFrequency     long
elapsedStopwatchTicks  long
Stopwatch.ElapsedTicks long
```

Internal pacing uses:

```text
stored frequency       ulong
fractional numerator   ulong
scaled numerator       UInt128
due Advances           UInt128
```

Pacer constructor validation order:

```text
publisher == null
-> ArgumentNullException

stopwatchFrequency <= 0
-> ArgumentOutOfRangeException
```

Successful construction initializes the remainder to zero and the fault flag to false. The Driver rejects a null Publisher and constructs its pacer with the exact runtime `Stopwatch.Frequency`.

`SimulationConfig.TickRate` remains the sole cadence source and remains exactly `30`.

## 6. Stopwatch Start and Poll Semantics

Driver construction performs:

```text
validate Publisher
-> construct ElapsedTickPacer(Publisher, Stopwatch.Frequency)
-> start one Stopwatch
-> capture its current ElapsedTicks as the baseline
-> perform no logical Advance
```

`Poll()` performs:

```text
current = Stopwatch.ElapsedTicks
delta = current - previous baseline
result = pacer.ProcessElapsedStopwatchTicks(delta)

only after successful pacer return:
    previous baseline = current

return result
```

If pacing throws, the baseline is not committed. The Driver propagates the exception and adds no retry, replacement Publisher, elapsed-time reconstruction, or independent fault state. A later Poll reaches the pacer's sticky fault and fails.

The finite Server process relies on `Stopwatch` monotonic, non-negative elapsed ticks. No clock-wrap protocol is introduced.

## 7. Exact Integer Pacing Formula

Define:

```text
R = SimulationConfig.TickRate = 30
F = stopwatchFrequency
Delta = elapsedStopwatchTicks
r = previous fractional numerator
```

Each healthy, non-terminal call computes:

```text
scaled =
    UInt128(r)
    + UInt128(Delta) * UInt128(R)

dueAdvances =
    scaled / UInt128(F)

nextRemainder =
    scaled % UInt128(F)
```

The invariant is:

```text
0 <= nextRemainder < F
```

The candidate remainder commits only after the full processing call completes without fault. No floating point, `TimeSpan.TotalSeconds`, rounded duration, or truncated `Stopwatch.Frequency / 30` Tick period is allowed.

## 8. Processing and Catch-Up Contract

Validation and operation order is frozen:

```text
1. sticky fault
2. elapsedStopwatchTicks < 0
3. Publisher.EligibilityCeiling == uint.MaxValue - 1
4. UInt128 pacing calculation
5. execute 0 / 1 / N AdvanceOneTick calls
6. commit next remainder
7. return flattened publication
```

Results:

```text
faulted
-> InvalidOperationException before argument processing

negative elapsed delta
-> ArgumentOutOfRangeException
-> no remainder, Publisher, publication, or fault mutation

terminal at entry
-> Array.Empty<FrameData>()
-> no additional remainder accumulation

zero/sub-Tick elapsed
-> zero Advances
-> exact remainder accumulation
-> empty publication
```

For `dueAdvances = N`, the pacer synchronously calls `AdvanceOneTick()` up to N times. Gate 10 has no per-call catch-up cap. Terminal is checked before each Advance.

## 9. Flattening and Container Ownership

Per-Advance batches are flattened in Advance order:

```text
Advance #1 -> []
Advance #2 -> [100, 101]
Advance #3 -> [102]

Process result -> [100, 101, 102]
```

An empty result uses `Array.Empty<FrameData>()`. A non-empty result uses a new array container. Immutable `FrameData` references may be shared with Publisher history, but neither Frames nor history are deep-copied. Mutating the returned array cannot modify retained history.

The pacer and driver never own or step `BattleSimulation`; the caller consumes returned Frames in array order.

## 10. Sticky Partial-Failure Semantics

For:

```text
Advance #1 succeeds
Advance #2 succeeds
Advance #3 throws unexpectedly
```

the contract is:

- the first two scheduling mutations and authoritative publications remain committed;
- no rollback, compensation, retry, or remaining Advance occurs;
- the local publication list is abandoned and no partial batch is returned;
- the candidate remainder is not committed;
- `_faulted` becomes true;
- the original exception is rethrown with `throw;` semantics.

The already-created authoritative publications are intentionally unavailable through the failed pacer call because the pacer has entered unrecoverable fail-stop state. They remain observable only through the existing Publisher authority/history state. No partial-result or recovery API is introduced.

After fault, every later elapsed-processing call throws `InvalidOperationException` before examining its argument or changing any state.

## 11. Terminal and Arithmetic Boundaries

Terminal is derived exclusively from:

```text
Publisher.EligibilityCeiling == uint.MaxValue - 1
```

Terminal at entry returns empty without changing remainder or fault state. If terminal is reached during catch-up, preceding successful Advances remain committed, remaining Advances stop normally, accumulated publications are returned, and the successfully processed delta's remainder commits. Later calls return empty without accumulating more remainder.

Terminal is not a fault. A mature but incomplete final Frame can still be completed through the existing Gate 9 `Submit` path. Gate 10 creates no missing input.

Accepted numeric bounds are:

```text
0 <= elapsedStopwatchTicks <= long.MaxValue
1 <= stopwatchFrequency <= long.MaxValue
TickRate = 30
```

`previous remainder + long.MaxValue * 30` fits safely within `UInt128`. `dueAdvances` remains `UInt128` and is not narrowed before execution. Actual Advances are bounded by the Gate 9 final consumable Tick frontier, with no Tick wraparound.

## 12. Deterministic Golden Vectors

All fake-time Goldens use:

```text
stopwatchFrequency = 300
TickRate = 30
10 Stopwatch ticks = 1 logical Advance
```

The Gate 10 Golden vector owns its logical inputs independently and returns actual results only. Expected state, batch, and Digest literals live exclusively in the consumer tests.

### 12.1 Two players: positive-delay warm-up

```text
Roster: [PlayerId 900, PlayerId 7]
Initial Tick=10, InputDelay=2, futureOffset=4, history=2

Initial:
S0 X=0 Z=0 Aim=100
S1 X=0 Z=0 Aim=200

Tick10 arrival:
S1 Move=(-1,0) Aim=201
S0 Move=( 1,0) Aim=101

Elapsed deltas: 9, 1, 10
Publications: [], [], [10]
```

Expected final:

```text
State Tick=11
S0 X=100  Z=0 Aim=101
S1 X=-100 Z=0 Aim=201
Digest=AE353BEBCCF29139
CollectionTick=12, EligibilityCeiling=10, NextPublishTick=11
```

### 12.2 Three players: mature but incomplete

```text
Roster: [PlayerId 500, PlayerId 1, PlayerId 300]
Initial Tick=20, InputDelay=1, futureOffset=4, history=2

Initial:
S0 X=0 Z=0 Aim=1000
S1 X=0 Z=0 Aim=2000
S2 X=0 Z=0 Aim=3000

Initial arrival:
S2 Move=(-1,0) Aim=3001
S0 Move=( 1,0) Aim=1001

Elapsed deltas: 4, 6
Late arrival:
S1 Move=(0,1) Aim=2001
```

Elapsed processing matures Tick20 but publishes nothing while it is incomplete. The late Submit publishes Tick20.

Expected final:

```text
State Tick=21
S0 X=100  Z=0   Aim=1001
S1 X=0    Z=100 Aim=2001
S2 X=-100 Z=0   Aim=3001
Digest=38CCC825F57B7655
CollectionTick=21, EligibilityCeiling=20, NextPublishTick=21
```

### 12.3 Four players: catch-up and elapsed segmentation

```text
Roster by Slot:
S0=0x0102030405060708
S1=0x000000000000002A
S2=0xFFEEDDCCBBAA0099
S3=0x00000000000F4243

Initial Tick=100, InputDelay=2, futureOffset=8, history=3

Initial:
S0 X=-300 Z=0    Aim=1000
S1 X=300  Z=0    Aim=2000
S2 X=0    Z=-300 Aim=3000
S3 X=0    Z=300  Aim=4000
```

Inputs:

```text
Tick100: S0( 1, 0,10100) S1(-1, 0,20100) S2( 0, 1,30100) S3( 0,-1,40100)
Tick101: S0( 0, 1,10101) S1( 0,-1,20101) S2( 1, 0,30101) S3(-1, 0,40101)
Tick102: S0(-1, 0,10102) S1( 1, 0,20102) S2( 0,-1,30102) S3( 0, 1,40102)
Tick103: S0( 0,-1,10103) S1( 0, 1,20103) S2(-1, 0,30103) S3( 1, 0,40103)
```

Primary arrival and elapsed segmentation:

```text
Tick101 order 3,1,0,2
Tick102 order 2,0,3,1
Tick103 order 1,3,0,2
Tick100 partial order 0,2,1
elapsed before gap fill: 7,3,20
submit Tick100 Slot3
elapsed after gap fill: 4,6,1,9
```

Alternative uses the same logical arrival and gap-fill positions, changing elapsed segmentation only:

```text
elapsed before gap fill: 30
elapsed after gap fill: 10,10
```

Both runs produce:

```text
authoritative Frames: 100,101,102,103
retained history: 101,102,103

State101 Digest D95809E1EB5CDDAA
State102 Digest A96B83267DD72A7D
State103 Digest 386C4BB11A7EB7E0
State104 Digest 9F41F69F63A24BCB
```

Final State104:

```text
S0 X=-300 Z=0    Aim=10103
S1 X=300  Z=0    Aim=20103
S2 X=0    Z=-300 Aim=30103
S3 X=0    Z=300  Aim=40103
```

## 13. Exact Gate 10 Test Matrix

The dependency-free Gate 10 executable suite must report:

```text
RESULT 27/27 passed
```

Exact test names and order:

```text
1.  ConstructorRejectsNullPublisher
2.  ConstructorRejectsZeroStopwatchFrequency
3.  ConstructorRejectsNegativeStopwatchFrequency
4.  NegativeElapsedTicksRejectBeforeAnyMutation
5.  NegativeElapsedRejectionDoesNotFaultPacer
6.  ZeroElapsedTicksReturnsEmptyWithoutMutation
7.  PacingUsesSimulationConfigTickRateThirty
8.  SubTickElapsedCarriesFractionalRemainder
9.  RationalBoundaryDoesNotUseTruncatedStopwatchPeriod
10. SplitElapsedDeltasMatchSingleDelta
11. MultipleDueIntervalsCatchUpWithoutCap
12. UInt128ArithmeticHandlesLongMaxElapsedDelta
13. InputDelayMaturityRemainsOwnedByTickDrivenPublisher
14. CatchUpFlattensPublicationsInAdvanceOrder
15. ReturnedPublicationArrayDoesNotBackAuthoritativeHistory
16. PartialCatchUpFailureKeepsEarlierAdvancesAndRethrowsOriginal
17. PartialCatchUpFailureReturnsNoPartialBatchAndFaultsPacer
18. StickyFaultPrecedesNegativeElapsedValidation
19. FaultedPacerCannotMutatePublisherAgain
20. TerminalAtEntryReturnsEmptyWithoutRemainderAccumulation
21. CatchUpStopsNormallyWhenTerminalReachedMidCall
22. FinalMatureIncompleteFrameCanCompleteThroughSubmitAfterTerminal
23. StopwatchDriverConstructionStartsBaselineWithoutAdvancing
24. StopwatchDriverTerminalPollReturnsEmptyRepeatedly
25. TwoPlayerWarmupGoldenMatchesExactPublicationAndDigest
26. ThreePlayerLateCompletionGoldenMatchesExactPublicationAndDigest
27. FourPlayerElapsedSegmentationsProduceSameAuthorityAndDigests
```

`UInt128ArithmeticHandlesLongMaxElapsedDelta` uses `elapsedStopwatchTicks = long.MaxValue`, `stopwatchFrequency = long.MaxValue`, and a Publisher beginning at `uint.MaxValue - 3`. The math produces 30 due Advances, but terminal is reached after exactly two successful Advances, proving widened arithmetic without a giant loop or product catch-up cap.

Partial-failure tests may use reflection only to corrupt a future pending completed Frame after valid construction, causing a later Advance invariant failure. They add no production hook, interface, delegate injection, `InternalsVisibleTo`, or recovery behavior.

## 14. Test Project Layout

```text
Tests/LockstepArena.Server.TickPacing.Tests/
  LockstepArena.Server.TickPacing.Tests.csproj
  Program.cs
  ElapsedTickPacerTests.cs
  StopwatchTickDriverTests.cs
  Gate10TickPacingGoldenVector.cs
```

Direct ProjectReferences are exactly:

```text
Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj
Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj
```

No test framework or Gate 9 test-project reference is added. If required by the existing `*.csproj` ignore rule, `.gitignore` adds exactly:

```text
!Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj
```

## 15. Acceptance and Protected Boundaries

Final acceptance requires 15 Release builds with zero warnings/errors and:

```text
Gate 3  38/38
Gate 4  32/32
Gate 5  35/35
Gate 6  24/24
Gate 7  32/32
Gate 8   8/8
Gate 9  27/27
Gate 10 27/27
```

Gate 3 Server Golden, pinned Protocol regeneration, and fresh Unity Gate 7/5/3 regressions remain required.

Relative to the frozen Gate 9 base, committed diff must be zero for:

- `Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs`;
- `Server/LockstepArena.Server.FrameSync/TickDrivenFramePublisher.cs`;
- Shared Simulation;
- Protocol;
- StreamFraming;
- ProtocolAuthority;
- Gate 8 TCP project;
- every existing Gate 3-9 test source/project;
- Unity `Assets/` and `ProjectSettings/`;
- `Packages/manifest.json` and `Packages/packages-lock.json`.

## 16. Explicit Exclusions

Gate 10 adds no Timer, background thread, `Task`, async loop, Unity `Update`/`FixedUpdate`, production TCP lifecycle, UDP/KCP, client clock synchronization, RTT compensation, adaptive InputDelay, timeout, neutral/repeat-last input, prediction, snapshot, rollback, replay, reconnect, heartbeat, Room/Login/Session, generic clock/scheduler abstraction, DI, EventBus, or pacing recovery framework.

Gate 10 stops after implementation evidence and independent review. It does not begin Gate 11.

## 17. Implementation Evidence

Fresh verification was completed on 2026-09-07 from frozen Gate 9 base
`e041efb290e1df609fc5003d619e4e19f70ded78` and approved Planning HEAD
`a9fb4608450d200ee609a810da4feac3e43d25db`.

Implementation commits before this evidence update were:

```text
a55c94b036960369627fe5b091f3b083d2c560ae  feat: pace authority from elapsed stopwatch ticks
74c1af7f9971a1b888ef762d3922502f9ca099b1  feat: contain tick pacing failures
17f82b0e76d027766f289bb05a8f4cebb8dc4871  feat: drive authority pacing from stopwatch polls
3b2408387d2adbb86901c85cde30801d1ff66cd9  test: prove stopwatch-paced authority determinism
```

The evidence parent was
`3b2408387d2adbb86901c85cde30801d1ff66cd9`.

### Restore and build evidence

The restore-assets preflight found missing assets for ten existing projects in
the new worktree. They were restored using their frozen project contracts; no
dependency, version, or project file changed. The complete matrix was then
restarted from build 1. All 15 independent Release builds completed with zero
warnings and zero errors.

### .NET execution evidence

```text
Gate 3 Simulation         RESULT 38/38 passed
Gate 4 FrameSync          RESULT 32/32 passed
Gate 5 Protocol           RESULT 35/35 passed
Gate 6 ProtocolAuthority  RESULT 24/24 passed
Gate 7 StreamFraming      RESULT 32/32 passed
Gate 8 TCP watchdog       RESULT 8/8 passed within 30 seconds
Gate 9 TickAuthority      RESULT 27/27 passed
Gate 10 TickPacing        RESULT 27/27 passed
Gate 3 Server Golden      Tick=1000 Players=4 Digest=89A7DD66F8D9E871
```

The frozen Gate 8 test `RealTcpRoundTripMatchesApprovedAuthoritySequenceStatesAndDigests`
passed and therefore retained its Tick 103 and `386C4BB11A7EB7E0` Digest proof.

The Gate 10 `long.MaxValue` fixture passed with a Publisher starting at
`uint.MaxValue - 3`, a frequency and elapsed delta both equal to
`long.MaxValue`, and exactly two successful logical Advances before terminal
eligibility stopped the mathematically due 30-Advance catch-up.

The reflection-only partial-failure fixture preserved the two already
authoritative Frames 100 and 101, `CollectionTick = 101`,
`EligibilityCeiling = 101`, and `NextPublishTick = 102`. It rethrew the original
`InvalidOperationException`, returned no partial batch, entered sticky fault,
and rejected every later elapsed-processing call before further mutation.

The actual-only Golden vector produced:

```text
2 players: publication [10], final Tick 11, Digest AE353BEBCCF29139
3 players: late-completion publication [20], final Tick 21, Digest 38CCC825F57B7655
4 players: batches [100,101], [102], [103]; retained history 101,102,103
4-player Digests: D95809E1EB5CDDAA, A96B83267DD72A7D,
                  386C4BB11A7EB7E0, 9F41F69F63A24BCB
4-player final Tick 104; primary and alternate elapsed segmentations matched
```

The real driver tests confirmed construction starts a `Stopwatch` baseline
without advancing authority and that synchronous `Poll()` delegates elapsed
ticks to the pacer without adding timing-sensitive duration assertions or a
second recovery state.

### Code generation and Unity evidence

Pinned Protocol regeneration used the existing CodeGen project, produced
exactly `LockstepArenaProtocol.g.cs`, and left Schema and Generated paths
diff-clean.

Three independent Unity 6000.3.10f1 EditMode runs generated fresh NUnit XML:

```text
.artifacts/gate10-unity/gate7-results.xml
  total=1 passed=1 failed=0
  UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden = Passed

.artifacts/gate10-unity/gate5-results.xml
  total=2 passed=2 failed=0
  GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads = Passed
  UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector = Passed

.artifacts/gate10-unity/gate3-results.xml
  total=1 passed=1 failed=0
  UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector = Passed
```

After each run, only individually inspected Unity-generated worktree-local
serialization changes were restored. No broad reset or clean was used.

### Boundary and repository evidence

- Frozen `AuthoritativeFrameCoordinator.cs` and `TickDrivenFramePublisher.cs`
  have zero committed diff from Gate 9.
- Simulation, Protocol, StreamFraming, ProtocolAuthority, Gate 8 TCP, existing
  tests, Assets, ProjectSettings, manifest, and packages-lock are unchanged.
- FrameSync production adds exactly `ElapsedTickPacer.cs` and
  `StopwatchTickDriver.cs`; the Gate 10 test project contains exactly five files
  and two direct ProjectReferences.
- `.gitignore` adds exactly the one approved authored-project exception.
- Expected Golden Digests occur only in consumer tests, never production or the
  actual-only Golden vector. Reflection remains test-only.
- Production scope, dependency, symlink, script, generated-source, tracked
  artifact, embedded-package artifact, and whitespace audits passed.
- The ordinary checkout remained exactly at its two protected user-owned
  modifications: `Assets/Settings/Mobile_RPAsset.asset` and
  `ProjectSettings/ShaderGraphSettings.asset`.
