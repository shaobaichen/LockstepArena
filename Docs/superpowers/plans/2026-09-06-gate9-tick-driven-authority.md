# Gate 9 Offline Tick-Driven Authority Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an offline, manually advanced fixed-InputDelay publication gate that reuses Gate 4 pending collectors and proves deterministic 2/3/4-player authoritative Frame publication without adding a real clock or transport behavior.

**Architecture:** Add `TickDrivenFramePublisher` to the existing `LockstepArena.Server.FrameSync` assembly. Refactor `AuthoritativeFrameCoordinator` internally into collection and ceiling-bounded publication while preserving its public Gate 4 behavior exactly. The Publisher owns only logical Advance count and eligibility mathematics; Gate 4 retains all pending collectors, validation, canonical publication, and history.

**Tech Stack:** .NET 8, C# 12, the existing dependency-free executable test style, Gate 4 FrameSync, Gate 3 Simulation, PowerShell verification, and existing Unity 6000.3.10f1 regressions.

**Spec:** `Docs/Architecture/GATE9_OFFLINE_TICK_DRIVEN_AUTHORITY.md`

## 1. Global Constraints

- Frozen Gate 8 comparison base: `a91641f5a6a973833c62b13e950a234fbef9552b`.
- Branch: `codex/gate9-tick-driven-authority`.
- Worktree: `.worktrees/gate9-tick-driven-authority`.
- The Planning commit must have the frozen base as its direct parent and contain only this Plan plus the Gate 9 Architecture document.
- Implementation starts from the final Planning commit independently approved on this branch. Never reset or check out the implementation worktree back to the frozen base.
- Keep the worktree clean before Task 1 and preserve the ordinary checkout's exact user-owned modifications:

```text
 M Assets/Settings/Mobile_RPAsset.asset
 M ProjectSettings/ShaderGraphSettings.asset
```

- Never modify, restore, stage, clean, or commit those ordinary-checkout files.
- Create no production assembly. Add one production type to the existing FrameSync assembly.
- Gate 4 remains the single owner of pending collectors, `StrictFrameCollector`, complete-frame validation, canonical ordering, continuous publication, and authoritative history.
- `maxFutureTickOffset` and `inputDelayTicks` remain independent.
- Shared Simulation, Protocol, StreamFraming, ProtocolAuthority, Gate 8 TCP production proof, Assets, ProjectSettings, manifest, and packages-lock remain unchanged.
- Do not add Timer, Stopwatch pacing, Task, Thread, async, sleep, Unity Update/FixedUpdate, TCP, Socket, UDP, KCP, timeout, neutral input, repeat-last, Prediction, Snapshot, Rollback, Replay, reconnect, heartbeat, dynamic delay, scheduler framework, interface, factory, DI, middleware, EventBus, or generic timeline.
- Each feature task follows RED -> verify intended failure -> minimal implementation -> GREEN -> focused audit -> commit. The Gate 4 internal extraction is made only after its existing 32-test characterization suite passes.
- If the frozen API, mathematics, or boundary proves contradictory, stop and report it rather than changing Architecture.

## 2. Implementation-Start Verification

- [ ] Verify exact branch, approved Planning ancestry, remote equality, and clean worktree:

```powershell
$frozenBase = 'a91641f5a6a973833c62b13e950a234fbef9552b'
$branch = 'codex/gate9-tick-driven-authority'

if ((git branch --show-current) -ne $branch) {
    throw 'Wrong Gate 9 branch.'
}

$localPlanningHead = git rev-parse HEAD
$remoteLine = git ls-remote --heads origin "refs/heads/$branch"
$remotePlanningHead = ($remoteLine -split '\s+')[0]
if ([string]::IsNullOrWhiteSpace($remotePlanningHead) -or
    $localPlanningHead -ne $remotePlanningHead) {
    throw 'Local and remote Gate 9 Planning HEAD must match.'
}

if ((git rev-parse HEAD^) -ne $frozenBase) {
    throw 'Approved Gate 9 Planning commit must have the frozen Gate 8 base as direct parent.'
}
if ((git merge-base HEAD $frozenBase) -ne $frozenBase) {
    throw 'Frozen Gate 8 base must remain the merge-base.'
}
if ([int](git rev-list --count "$frozenBase..HEAD") -ne 1 -or
    [int](git rev-list --count "HEAD..$frozenBase") -ne 0) {
    throw 'Gate 9 must start exactly one Planning commit ahead of the frozen base.'
}
if ((git status --porcelain).Length -ne 0) {
    throw 'Gate 9 worktree must be clean.'
}
```

- [ ] Require the cumulative Planning diff to contain exactly:

```text
Docs/Architecture/GATE9_OFFLINE_TICK_DRIVEN_AUTHORITY.md
Docs/superpowers/plans/2026-09-06-gate9-tick-driven-authority.md
```

- [ ] Read the complete Spec and this Plan. Reject unresolved markers or alternatives, the removed future-window/InputDelay constructor coupling, and any stale pre-Amendment suite count.
- [ ] From the ordinary checkout, require exactly the two user-owned status lines above and no Gate 9 path. Do not mutate it.
- [ ] Run the Gate 4 characterization baseline before editing production:

```powershell
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj --configuration Release
```

Expected: `RESULT 32/32 passed`.

## 3. Exact File Map

Permitted implementation files:

```text
Modify: .gitignore
Modify: Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs
Create: Server/LockstepArena.Server.FrameSync/TickDrivenFramePublisher.cs

Create: Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj
Create: Tests/LockstepArena.Server.TickAuthority.Tests/Program.cs
Create: Tests/LockstepArena.Server.TickAuthority.Tests/TickDrivenFramePublisherTests.cs
Create: Tests/LockstepArena.Server.TickAuthority.Tests/Gate9TickAuthorityGoldenVector.cs

Modify for final evidence only:
Docs/Architecture/GATE9_OFFLINE_TICK_DRIVEN_AUTHORITY.md
```

No other production, test, package, Unity, or configuration file may change.

### 3.1 Frozen test project

`LockstepArena.Server.TickAuthority.Tests.csproj` must be exactly equivalent to:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <BuildInParallel>false</BuildInParallel>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Server\LockstepArena.Server.FrameSync\LockstepArena.Server.FrameSync.csproj" />
    <ProjectReference Include="..\..\Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj" />
  </ItemGroup>
</Project>
```

There is no Protocol, StreamFraming, ProtocolAuthority, TCP, test-framework, PackageReference, external Compile Include, or existing test-project reference.

The only permitted `.gitignore` change is one added line and no deletion:

```text
!Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj
```

### 3.2 Frozen production API

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

    public FrameData[] Submit(PlayerId submittedPlayerId, InputFrame input);
    public FrameData[] AdvanceOneTick();
    public FrameData[] GetAuthoritativeHistorySnapshot();
}
```

### 3.3 Frozen actual-only Golden API

`Gate9TickAuthorityGoldenVector.cs` contains no expected state or Digest literal and exposes:

```csharp
internal static class Gate9TickAuthorityGoldenVector
{
    internal static Gate9GoldenResult RunTwoPlayer();
    internal static Gate9GoldenResult RunThreePlayer();
    internal static Gate9GoldenResult RunFourPlayerPrimary();
    internal static Gate9GoldenResult RunFourPlayerAlternative();
}

internal sealed class Gate9GoldenResult
{
    public FrameData[][] PublicationBatches { get; }
    public FrameData[] AuthoritativeFrames { get; }
    public BattleState[] SimulationStates { get; }
    public ulong[] Digests { get; }
    public FrameData[] History { get; }
    public BattleState FinalState { get; }
    public ulong CollectionTick { get; }
    public uint? EligibilityCeiling { get; }
    public uint NextPublishTick { get; }
}
```

Every returned array is an actual result container owned by the vector result. `PublicationBatches` contains only non-empty batches in call order; `AuthoritativeFrames` is their flattened Frame sequence. `SimulationStates` and `Digests` contain one entry per flattened Frame, captured immediately after that Frame is stepped. Expected publication Ticks, states, batch lengths, history, and Digests remain only in `TickDrivenFramePublisherTests.cs`.

## 4. Exact Final Test Registration

`Program.cs` registers these exact names once and in order:

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

Final output is exactly `RESULT 27/27 passed`.

## Task 1: Extract Gate 4 Collection and Add the Core Publication Gate

**Commit:** `feat: gate authoritative frames by logical time`

**Files:**

- Modify: `.gitignore`
- Modify: `Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs`
- Create: `Server/LockstepArena.Server.FrameSync/TickDrivenFramePublisher.cs`
- Create: `Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj`
- Create: `Tests/LockstepArena.Server.TickAuthority.Tests/Program.cs`
- Create: `Tests/LockstepArena.Server.TickAuthority.Tests/TickDrivenFramePublisherTests.cs`

**Interfaces:**

- Consumes: Gate 3 `ActiveRoster`, `PlayerId`, `InputFrame`, `FrameData`; Gate 4 Coordinator behavior.
- Produces: the frozen `TickDrivenFramePublisher` public API and internal `Collect` / `PublishThrough` Coordinator split.

- [ ] **Step 1: Create the project and exact runner**

Add the one approved `.gitignore` exception, exact csproj, and a dependency-free runner with `TestCase`, `TestAssert.Equal`, `TestAssert.Same`, `TestAssert.SequenceEqual`, and `TestAssert.Throws<TException>`. Register tests 1 through 20 in final order; tests 21-27 are added by later tasks without changing the relative order.

- [ ] **Step 2: Write the first 20 failing tests**

Use direct construction and small helpers in `TickDrivenFramePublisherTests.cs`. The tests must encode these exact responsibilities:

```text
1  null roster -> ArgumentNullException
2  S=100, D=2, futureOffset=1 constructs; accepts 100/101, publishes 100 when mature, then accepts 102
3  D=0 -> CollectionTick=S and EligibilityCeiling=S
4  D=0 complete Frame S -> final Submit [S], no Advance, Next=S+1, Collection=S, ceiling=S
5  D>0 -> initial ceiling null
6  each successful Advance increments CollectionTick once
7  first eligibility Afirst(T)=D+(T-S)
8  non-null ceiling increases by no more than one Frame Tick per Advance
9  complete but immature -> empty and absent from history
10 exact maturity Advance publishes complete Frame
11 mature incomplete Frame remains pending and unpublished
12 late final input for mature Frame publishes immediately
13 mature complete future Frames cannot cross a NextPublish gap
14 gap-fill publishes only the eligible continuous prefix
15 ceiling < NextPublish returns empty, preserves complete pending, and later Advance publishes it
16 later Advances publish 102 then 103 in order
17 history contains published Frames only
18 history capacity evicts the oldest authoritative Frame only
19 rejected duplicate/mismatch preserves schedule, pending accepted input, history, and NextPublish
20 publication array mutation cannot mutate a separately obtained history snapshot
```

Build before adding production. Expected RED: missing `TickDrivenFramePublisher` and no other project/reference/syntax failure.

```powershell
dotnet build Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj --configuration Release
```

- [ ] **Step 3: Re-run the Gate 4 characterization immediately before refactor**

```powershell
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj --configuration Release
```

Require `RESULT 32/32 passed`.

- [ ] **Step 4: Extract `Collect` without changing validation order**

Move the existing pre-publication portion of public `Submit` into:

```csharp
internal void Collect(PlayerId submittedPlayerId, InputFrame input)
```

Preserve this exact effective order:

```text
input.Tick == uint.MaxValue
-> NextPublishTick == uint.MaxValue
-> stale Tick
-> widened future upper bound
-> exact pending lookup
-> transactional candidate collector Submit before dictionary Add
-> existing collector Submit
```

Do not add a new null-input rule, change exception types/messages, add a dictionary, or move validation into the Publisher.

- [ ] **Step 5: Extract atomic ceiling-bounded publication**

Move the current scan and copied-container commit into:

```csharp
internal FrameData[] PublishThrough(uint inclusiveEligibilityCeiling)
```

Use the frozen shape:

```csharp
if (inclusiveEligibilityCeiling < NextPublishTick)
{
    return Array.Empty<FrameData>();
}

var frames = new List<FrameData>();
ulong scanTick = NextPublishTick;
while (scanTick <= inclusiveEligibilityCeiling && scanTick < uint.MaxValue)
{
    uint tick = (uint)scanTick;
    if (!_pendingByTick.TryGetValue(tick, out StrictFrameCollector? pending) ||
        !pending.IsComplete)
    {
        break;
    }

    frames.Add(pending.GetCompletedFrame());
    scanTick = tick == uint.MaxValue - 1U ? uint.MaxValue : scanTick + 1UL;
}
```

If the local batch is empty, return `Array.Empty<FrameData>()`. Otherwise keep the existing full local plan, copied dictionary, copied queue, final field replacement, and independent returned array. Do not mutate live pending/history/NextPublish while scanning.

- [ ] **Step 6: Preserve public Gate 4 Submit**

```csharp
public FrameData[] Submit(PlayerId submittedPlayerId, InputFrame input)
{
    Collect(submittedPlayerId, input);
    return PublishThrough(uint.MaxValue - 1U);
}
```

Run Gate 4 immediately and require `RESULT 32/32 passed` before adding Publisher behavior.

- [ ] **Step 7: Implement the minimal Publisher for tests 1-20**

Use one Coordinator and scalar schedule state only:

```csharp
private readonly uint _initialTick;
private readonly AuthoritativeFrameCoordinator _coordinator;
private ulong _successfulAdvanceCount;
```

`InputDelayTicks` is an immutable property. `CollectionTick` and `EligibilityCeiling` are computed from `_initialTick`, `InputDelayTicks`, and `_successfulAdvanceCount` with `ulong` arithmetic. Do not store a second pending/history container.

Core publication paths:

```csharp
public FrameData[] Submit(PlayerId submittedPlayerId, InputFrame input)
{
    _coordinator.Collect(submittedPlayerId, input);
    uint? ceiling = EligibilityCeiling;
    return ceiling.HasValue
        ? _coordinator.PublishThrough(ceiling.Value)
        : Array.Empty<FrameData>();
}
```

For ordinary non-terminal values, `AdvanceOneTick` computes the next count and ceiling locally, calls `PublishThrough` only when the next ceiling exists, then commits `_successfulAdvanceCount`. Task 2 freezes terminal saturation behavior; do not add Timer, clock, Simulation, transport, or recovery logic.

- [ ] **Step 8: Run GREEN and focused audits**

```powershell
dotnet build Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj --configuration Release --nologo
dotnet run --project Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj --configuration Release --no-build
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj --configuration Release
```

Require `RESULT 20/20 passed` and Gate 4 `RESULT 32/32 passed`, all builds with zero warnings/errors.

Audit:

- `TickDrivenFramePublisher.cs` contains no `Dictionary`, `Queue`, `StrictFrameCollector`, `BattleSimulation`, Protocol, transport, Timer, Task, or Thread;
- Coordinator public signatures are unchanged;
- `Collect` validation order matches the pre-refactor source;
- `PublishThrough(ceiling < NextPublishTick)` has no mutation;
- `.gitignore` has exactly the one approved addition;
- protected paths have zero frozen-base diff.

- [ ] **Step 9: Commit Task 1 only**

```powershell
git diff --check
git add .gitignore `
    Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs `
    Server/LockstepArena.Server.FrameSync/TickDrivenFramePublisher.cs `
    Tests/LockstepArena.Server.TickAuthority.Tests
git commit -m "feat: gate authoritative frames by logical time"
```

## Task 2: Freeze the Terminal Tick Boundary

**Commit:** `feat: preserve delayed authority tick limits`

**Files:**

- Modify: `Server/LockstepArena.Server.FrameSync/TickDrivenFramePublisher.cs`
- Modify: `Tests/LockstepArena.Server.TickAuthority.Tests/Program.cs`
- Modify: `Tests/LockstepArena.Server.TickAuthority.Tests/TickDrivenFramePublisherTests.cs`

**Interfaces:**

- Consumes: Task 1 Publisher and Gate 4 final-Tick rules.
- Produces: saturated eligibility and no-wrap behavior through `uint.MaxValue - 1`.

- [ ] **Step 1: Add tests 21-24 in final order**

```text
21 FinalConsumableTickBecomesEligibleWithoutWrap
22 EligibilityCeilingSaturatesAtFinalConsumableTick
23 AdvanceAfterEligibilitySaturationThrowsWithoutMutation
24 UintMaxInputIsRejectedWithoutScheduleMutation
```

Use `initialTick = uint.MaxValue - 1U` with positive delay to prove Collection Tick may safely exceed the Frame Tick range while the ceiling clamps to the last consumable Tick. Capture Collection Tick, ceiling, NextPublish, history, and pending-observable behavior before every rejected action.

- [ ] **Step 2: Run RED**

```powershell
dotnet run --project Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj --configuration Release
```

Expected: the new boundary tests fail because ordinary-range Advance logic has not yet frozen saturation/rejection. The original 20 tests must still pass.

- [ ] **Step 3: Implement terminal arithmetic minimally**

Constructor rejects `initialTick == uint.MaxValue` before constructing the Coordinator.

Calculate ceiling through `ulong` and clamp only after comparison:

```csharp
private uint? GetEligibilityCeiling(ulong successfulAdvanceCount)
{
    if (successfulAdvanceCount < InputDelayTicks)
    {
        return null;
    }

    ulong candidate = (ulong)_initialTick +
        (successfulAdvanceCount - InputDelayTicks);
    return candidate >= uint.MaxValue
        ? uint.MaxValue - 1U
        : (uint)candidate;
}
```

At the start of `AdvanceOneTick`, reject when current eligibility is already `uint.MaxValue - 1U`. Compute the next count with `checked`, plan publication, then commit the count. Rejection or publication failure must leave the Advance count unchanged.

- [ ] **Step 4: Run GREEN and regressions**

```powershell
dotnet build Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj --configuration Release --nologo
dotnet run --project Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj --configuration Release --no-build
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj --configuration Release
```

Require `RESULT 24/24 passed`, Gate 4 `32/32`, and zero warnings/errors.

- [ ] **Step 5: Audit and commit**

Verify no uint increment/cast happens before widened comparison, no wrap semantics exists, no new exhausted flag duplicates Gate 4 state, and no protected path changed.

```powershell
git diff --check
git add Server/LockstepArena.Server.FrameSync/TickDrivenFramePublisher.cs `
    Tests/LockstepArena.Server.TickAuthority.Tests/Program.cs `
    Tests/LockstepArena.Server.TickAuthority.Tests/TickDrivenFramePublisherTests.cs
git commit -m "feat: preserve delayed authority tick limits"
```

## Task 3: Prove the 2/3/4-Player Goldens and Arrival-Order Determinism

**Commit:** `test: prove fixed-delay authority determinism`

**Files:**

- Modify: `Tests/LockstepArena.Server.TickAuthority.Tests/Program.cs`
- Modify: `Tests/LockstepArena.Server.TickAuthority.Tests/TickDrivenFramePublisherTests.cs`
- Create: `Tests/LockstepArena.Server.TickAuthority.Tests/Gate9TickAuthorityGoldenVector.cs`

**Interfaces:**

- Consumes: frozen Publisher API and Gate 3 `BattleSimulation` / `StateDigest`.
- Produces: the exact actual-only `Gate9TickAuthorityGoldenVector` and final `RESULT 27/27 passed` proof.

- [ ] **Step 1: Add tests 25-27 before the vector exists**

Register:

```text
25 TwoPlayerWarmupGoldenMatchesStateAndDigest
26 ThreePlayerLateCompletionGoldenMatchesStateAndDigest
27 FourPlayerArrivalOrdersProduceSameAuthorityAndPerTickDigests
```

Reference all four missing `Gate9TickAuthorityGoldenVector.Run...` methods and exact `Gate9GoldenResult` properties from Section 3.3.

- [ ] **Step 2: Run RED**

```powershell
dotnet build Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj --configuration Release
```

Expected: compilation fails only because `Gate9TickAuthorityGoldenVector` / `Gate9GoldenResult` are absent. Tests 1-24 and project references must remain otherwise valid.

- [ ] **Step 3: Implement the actual-only two-player vector**

Use exactly:

```text
S=10 D=2 futureOffset=4 history=2
Roster in Slot order: 900, 7
Initial: (0,0,100), (0,0,200)
Tick10: Slot0 (1,0,101), Slot1 (-1,0,201)
Arrival: Slot1, Slot0
Publications: [], [], Advance1 [], Advance2 [10]
```

Step a local Simulation only with actual publications. Return actual state/Digest. Keep these expected literals exclusively in the consumer test:

```text
State Tick11
Slot0 (100,0,101)
Slot1 (-100,0,201)
Digest AE353BEBCCF29139
```

- [ ] **Step 4: Implement the actual-only three-player vector**

Use exactly:

```text
S=20 D=1 futureOffset=4 history=2
Roster: 500, 1, 300
Initial: all positions zero; Aims 1000,2000,3000
Tick20: Slot0 (1,0,1001), Slot1 (0,1,2001), Slot2 (-1,0,3001)
Submit Slot2,Slot0 -> Advance1 [] -> Submit Slot1 [20]
```

Consumer-only expected result:

```text
State Tick21
Slot0 (100,0,1001)
Slot1 (0,100,2001)
Slot2 (-100,0,3001)
Digest 38CCC825F57B7655
```

- [ ] **Step 5: Implement both actual-only four-player runs**

Use the Spec's exact roster, initial State, Tick100-103 inputs, and arrival orders. The primary run uses:

```text
Tick101 3,1,0,2
Tick102 2,0,3,1
Tick103 1,3,0,2
Tick100 partial 0,2,1
Advance1, Advance2, Advance3
Tick100 final Slot3
Advance4, Advance5
```

The alternative run uses:

```text
Tick103 2,0,3,1
Tick102 1,3,0,2
Tick101 0,2,1,3
Tick100 partial 1,0,2
same Advance positions
Tick100 final Slot3
```

Both vectors return actual batch containers, flattened Frames, per-publication Simulation states and Digests, final history, and Publisher properties. They contain no expected Tick, batch, history, State, or Digest literal.

- [ ] **Step 6: Assert exact four-player consumer expectations**

In `TickDrivenFramePublisherTests.cs`, require for both runs:

```text
batches [100,101], [102], [103]
flattened 100,101,102,103
history 101,102,103
CollectionTick 105
EligibilityCeiling 103
NextPublishTick 104

State Tick101 Digest D95809E1EB5CDDAA
State Tick102 Digest A96B83267DD72A7D
State Tick103 Digest 386C4BB11A7EB7E0
State Tick104 Digest 9F41F69F63A24BCB
```

Assert the final full Tick104 state:

```text
Slot0 X=-300 Z=0    Aim=10103
Slot1 X=300  Z=0    Aim=20103
Slot2 X=0    Z=-300 Aim=30103
Slot3 X=0    Z=300  Aim=40103
```

Compare the two runs field-for-field: batch boundaries, Frame Tick, roster Count, every Slot PlayerId, InputCount, and every Input Tick/PlayerSlot/MoveX/MoveZ/Aim. Compare both simulations after every authoritative Frame.

- [ ] **Step 7: Run GREEN and focused audits**

```powershell
dotnet build Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj --configuration Release --nologo
dotnet run --project Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj --configuration Release --no-build
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj --configuration Release
```

Require `RESULT 27/27 passed`, Gate 4 `32/32`, and zero warnings/errors.

Audit that all six expected Digests occur only in `TickDrivenFramePublisherTests.cs`, never in the vector or production. Require exactly four authored test-project files and exactly two direct ProjectReferences.

- [ ] **Step 8: Commit Task 3 only**

```powershell
git diff --check
git add Tests/LockstepArena.Server.TickAuthority.Tests/Program.cs `
    Tests/LockstepArena.Server.TickAuthority.Tests/TickDrivenFramePublisherTests.cs `
    Tests/LockstepArena.Server.TickAuthority.Tests/Gate9TickAuthorityGoldenVector.cs
git commit -m "test: prove fixed-delay authority determinism"
```

## Task 4: Fresh Final Verification, Evidence, Push, and STOP

**Commit:** `docs: record Gate 9 implementation evidence`

**Files:**

- Modify: `Docs/Architecture/GATE9_OFFLINE_TICK_DRIVEN_AUTHORITY.md`

### 4.1 Restore-assets preflight

- [ ] Start clean and resolve each effective `ProjectAssetsFile` for the exact 14 projects below. Restore only a project whose asset is missing, using its frozen project contract. Any restore requires restarting the full build matrix at build 1.

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
    'Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj'
)

$restoreOccurred = $false
foreach ($project in $projects) {
    $propertyOutput = & dotnet msbuild $project -nologo -verbosity:quiet -getProperty:ProjectAssetsFile
    if ($LASTEXITCODE -ne 0) { throw "Could not resolve ProjectAssetsFile for $project" }
    $assetPath = ($propertyOutput | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Last 1).Trim()
    if (-not [IO.Path]::IsPathRooted($assetPath)) {
        $assetPath = Join-Path (Split-Path -Parent $project) $assetPath
    }
    if (-not (Test-Path -LiteralPath $assetPath)) {
        dotnet restore $project --nologo
        if ($LASTEXITCODE -ne 0) { throw "Restore failed for $project" }
        $restoreOccurred = $true
    }
}
```

No dependency, version, source, generated file, or project XML may change.

### 4.2 Exact 14 Release builds

- [ ] Run independently and in this exact order:

```powershell
dotnet build Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj --configuration Release --no-restore --nologo
dotnet build Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj --configuration Release --no-restore --nologo
dotnet build Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj --configuration Release --no-restore --nologo
dotnet build Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj --configuration Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj --configuration Release --no-restore --nologo
dotnet build Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj --configuration Release --no-restore --nologo
dotnet build Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj --configuration Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj --configuration Release --no-restore --nologo
dotnet build Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj --configuration Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj --configuration Release --no-restore --nologo
dotnet build Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj --configuration Release --no-restore --nologo
dotnet build Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj --configuration Release --no-restore --nologo
dotnet build Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj --configuration Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj --configuration Release --no-restore --nologo
```

Every build must report zero warnings and zero errors.

### 4.3 Exact .NET execution matrix

- [ ] Run:

```powershell
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj --configuration Release --no-build
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj --configuration Release --no-build
dotnet run --project Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj --configuration Release --no-build
dotnet run --project Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj --configuration Release --no-build
dotnet run --project Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj --configuration Release --no-build
dotnet run --project Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj --configuration Release --no-build
dotnet run --project Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj --configuration Release --no-build
```

Expected:

```text
Gate 3 Simulation:         RESULT 38/38 passed
Gate 4 FrameSync:          RESULT 32/32 passed
Gate 5 Protocol:           RESULT 35/35 passed
Gate 6 ProtocolAuthority:  RESULT 24/24 passed
Gate 7 StreamFraming:      RESULT 32/32 passed
Gate 3 Server Golden:      Tick=1000 Players=4 Digest=89A7DD66F8D9E871
Gate 9 TickAuthority:      RESULT 27/27 passed
```

- [ ] Run Gate 8 through this exact external 30-second watchdog only. It may terminate only the captured Gate 8 process tree and adds no socket/gameplay timeout or retry:

```powershell
$stdoutPath = Join-Path (Get-Location) '.artifacts/gate9-gate8-tcp.stdout.txt'
$stderrPath = Join-Path (Get-Location) '.artifacts/gate9-gate8-tcp.stderr.txt'
$arguments = @(
    'run',
    '--project', 'Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj',
    '--configuration', 'Release',
    '--no-build'
)
$process = Start-Process dotnet `
    -ArgumentList $arguments `
    -PassThru `
    -WindowStyle Hidden `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath
if (-not $process.WaitForExit(30000)) {
    & taskkill.exe /PID $process.Id /T /F | Out-Null
    throw 'Frozen Gate 8 synchronous TCP tests exceeded the 30-second verification bound.'
}
$process.WaitForExit()
if ($process.ExitCode -ne 0) {
    Get-Content -Raw $stderrPath | Write-Error
    throw "Gate 8 exited with code $($process.ExitCode)."
}
Get-Content -Raw $stdoutPath
```

Require `RESULT 8/8 passed`, final Tick103, and Digest `386C4BB11A7EB7E0` from its frozen assertions.

### 4.4 Fresh Unity regressions

- [ ] Run Unity 6000.3.10f1 from the Gate 9 worktree through hidden `Start-Process -Wait`, without `-quit`, in three separate jobs. Keep every other frozen argument exact.

Gate 7 arguments:

```powershell
$arguments = @(
    '-batchmode', '-nographics',
    '-projectPath', (Get-Location).Path,
    '-runTests', '-testPlatform', 'EditMode',
    '-assemblyNames', 'LockstepArena.StreamFraming.Editor.Tests',
    '-testResults', '.artifacts/gate9-unity/gate7-results.xml',
    '-logFile', '.artifacts/gate9-unity/gate7-unity.log'
)
Start-Process -FilePath 'E:\unityhub\unity6.3\Editor\Unity.exe' -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
```

Require fresh XML:

```text
total=1 passed=1 failed=0
UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden = Passed
```

Gate 5 exact arguments:

```powershell
$arguments = @(
    '-batchmode', '-nographics',
    '-projectPath', (Get-Location).Path,
    '-runTests', '-testPlatform', 'EditMode',
    '-assemblyNames', 'LockstepArena.Protocol.Editor.Tests',
    '-testResults', '.artifacts/gate9-unity/gate5-results.xml',
    '-logFile', '.artifacts/gate9-unity/gate5-unity.log'
)
Start-Process -FilePath 'E:\unityhub\unity6.3\Editor\Unity.exe' -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
```

Require:

```text
total=2 passed=2 failed=0
GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads = Passed
UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector = Passed
```

Gate 3 exact arguments:

```powershell
$arguments = @(
    '-batchmode', '-nographics',
    '-projectPath', (Get-Location).Path,
    '-runTests', '-testPlatform', 'EditMode',
    '-assemblyNames', 'LockstepArena.Simulation.Editor.Tests',
    '-testFilter', 'UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector',
    '-testResults', '.artifacts/gate9-unity/gate3-results.xml',
    '-logFile', '.artifacts/gate9-unity/gate3-unity.log'
)
Start-Process -FilePath 'E:\unityhub\unity6.3\Editor\Unity.exe' -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
```

Require:

```text
-testFilter UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector
total>=1 failed=0
UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector = Passed
```

For each job, delete any old target XML before launch, require a newly created XML, parse `test-run` totals and the unique named `test-case`, and treat process exit code only as diagnostic. After each run, inspect exact worktree Assets/ProjectSettings changes. Restore only an inspected Unity-generated exact path; never broad reset/clean or use the ordinary checkout.

### 4.5 Protected-boundary and scope audits

- [ ] Use the frozen base and require zero diff:

```powershell
$base = 'a91641f5a6a973833c62b13e950a234fbef9552b'
git diff --exit-code $base -- Packages/com.locksteparena.simulation
git diff --exit-code $base -- Packages/com.locksteparena.protocol
git diff --exit-code $base -- Packages/com.locksteparena.stream-framing
git diff --exit-code $base -- Server/LockstepArena.Server.ProtocolAuthority
git diff --exit-code $base -- Tests/LockstepArena.TcpEndToEnd.Tests
git diff --exit-code $base -- Assets ProjectSettings Packages/manifest.json Packages/packages-lock.json
git diff --exit-code $base -- Tests ':(exclude)Tests/LockstepArena.Server.TickAuthority.Tests/**'
```

- [ ] Require the FrameSync production diff to contain exactly:

```text
Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs
Server/LockstepArena.Server.FrameSync/TickDrivenFramePublisher.cs
```

- [ ] Require `.gitignore` to add exactly the one approved exception and no deletion.
- [ ] Require the Gate 9 test directory to have exactly four tracked authored files and exactly two ProjectReferences.
- [ ] Compare Gate 4 public API before/after and inspect `Collect` validation order. Run all 32 Gate 4 tests after the final diff.
- [ ] Require `TickDrivenFramePublisher.cs` to contain no `Dictionary`, `Queue`, `StrictFrameCollector`, `BattleSimulation`, Protocol, protobuf, Timer, Stopwatch, Task, Thread, async, sleep, Unity, TCP, Socket, UDP, KCP, timeout, retry, neutral input, Prediction, Rollback, Replay, DI, interface, factory, middleware, or EventBus.
- [ ] Search the complete Gate 9 source diff for the same forbidden scope. Documentation mentions of exclusions are not implementation matches.
- [ ] Require expected Digests only in the consumer test, not production or `Gate9TickAuthorityGoldenVector.cs`.
- [ ] Require no symlink/junction, copy/sync/cleanup script, external Golden, new package, generated source, tracked `bin/obj`, or tracked LockstepArena build DLL. Package directories must contain no generated `bin/obj` or LockstepArena build DLL.
- [ ] Require `git diff --check`, inspect the complete frozen-base diff, and scan both Gate 9 documents for unresolved markers or obsolete 26-test/coupled-window wording.
- [ ] From the ordinary checkout, require exactly its two user-owned status lines and no Gate 9 path.

### 4.6 Evidence commit and push

- [ ] Append `## 19. Implementation Evidence` to the Architecture document. Record only fresh facts:

```text
frozen base and approved Planning HEAD
implementation commit SHAs and evidence-parent SHA
restore-assets result
all 14 builds with zero warnings/errors
Gate 3-9 exact suite totals and Gate 3 Server Golden
Gate 8 watchdog result
2/3/4-player publications, states, history, and Digests
Gate 4 public behavior and validation-order regression
three fresh Unity XML paths/totals/named tests
protected-path, dependency, source-scope, artifact, and ordinary-checkout audits
```

- [ ] Commit only the evidence update:

```powershell
git add Docs/Architecture/GATE9_OFFLINE_TICK_DRIVEN_AUTHORITY.md
git commit -m "docs: record Gate 9 implementation evidence"
```

- [ ] Push only `codex/gate9-tick-driven-authority`, then prove remote equality and cleanliness:

```powershell
git push origin codex/gate9-tick-driven-authority
$localFinal = git rev-parse HEAD
$remoteFinal = ((git ls-remote --heads origin refs/heads/codex/gate9-tick-driven-authority) -split '\s+')[0]
if ($localFinal -ne $remoteFinal) { throw 'Remote Gate 9 SHA does not match local HEAD.' }
if ((git status --porcelain).Length -ne 0) { throw 'Gate 9 worktree is not clean.' }
```

- [ ] Confirm the ordinary checkout one final time, submit the Gate 9 Final Implementation Handoff, and **STOP**.

Do not begin a real TickClock, Timer, production TCP, KCP, weak-network behavior, Gate 10 planning, or any next-Gate implementation.

## 5. Final Acceptance Invariants

Gate 9 is eligible for Final Handoff only when:

- the existing Gate 4 public API and all 32 tests remain unchanged and green;
- `Collect` preserves the exact existing validation order and transactional candidate insertion;
- `PublishThrough` uses only the existing atomic continuous-publication machinery and treats ceiling below `NextPublishTick` as empty/no mutation;
- `TickDrivenFramePublisher` has no pending/history/Simulation/transport ownership;
- zero-delay initial publication works without Advance;
- positive delay follows `Afirst(T) = D + (T - S)` exactly;
- complete/immature, mature/incomplete, gap, and eligible-prefix behavior match the Spec;
- terminal Tick behavior does not wrap;
- Gate 9 reports exactly `RESULT 27/27 passed` and all frozen 2/3/4-player states/Digests match;
- all 14 builds, Gate 3-8 regressions, Gate 3 Server Golden, Gate 8 watchdog, and Unity Gate 7/5/3 XML checks pass;
- protected paths, `.gitignore`, dependencies, source scope, artifacts, and ordinary checkout match their frozen contracts;
- remote SHA equals local final HEAD and work stops before the next Gate.
