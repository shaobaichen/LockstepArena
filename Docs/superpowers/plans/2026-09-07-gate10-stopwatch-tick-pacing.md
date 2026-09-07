# Gate 10 Synchronous Stopwatch-Driven Tick Pacing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert real monotonic Stopwatch elapsed ticks into exact, drift-free, synchronous 0/1/N Gate 9 logical Advances while preserving all deterministic Simulation and authority boundaries.

**Architecture:** Add a pure integer `ElapsedTickPacer` and a thin real `StopwatchTickDriver` to the existing Server FrameSync assembly. The pacer alone owns fractional remainder and sticky fail-stop state, while the frozen Gate 9 Publisher remains the only owner of fixed InputDelay and authority eligibility.

**Tech Stack:** .NET 8, C# 12, BCL `System.Diagnostics.Stopwatch`, `UInt128`, the existing dependency-free executable test style, PowerShell verification, and existing Unity 6000.3.10f1 regressions.

**Spec:** `Docs/Architecture/GATE10_SYNCHRONOUS_STOPWATCH_TICK_PACING.md`

## Global Constraints

- Frozen comparison base is `e041efb290e1df609fc5003d619e4e19f70ded78`.
- Work only on branch `codex/gate10-stopwatch-tick-pacing` in `.worktrees/gate10-stopwatch-tick-pacing`.
- Implementation must begin from the independently approved Planning HEAD on that branch; do not reset to the frozen base.
- Production changes are limited to creating `ElapsedTickPacer.cs` and `StopwatchTickDriver.cs` in the existing FrameSync project.
- Do not modify `AuthoritativeFrameCoordinator.cs`, `TickDrivenFramePublisher.cs`, or their public behavior.
- Use `SimulationConfig.TickRate == 30`; do not duplicate a configurable TickRate.
- Use `UInt128` integer accumulation; no floating point, rounded duration, or truncated `Stopwatch.Frequency / 30` interval.
- Partial catch-up failure is sticky and returns no partial batch; already-successful Advances remain committed.
- Terminal exhaustion is not a fault and never wraps Tick.
- Do not add production test hooks, injectable delegates, interfaces, `InternalsVisibleTo`, reset/recovery APIs, or a scheduler framework.
- Preserve all ordinary-checkout user-owned modifications without staging, restoring, cleaning, or copying them.

---

## Required Implementation Start State

- [ ] From the Gate 10 worktree, verify branch, remote equality, ancestry, clean status, and documentation-only Planning scope:

```powershell
$base = 'e041efb290e1df609fc5003d619e4e19f70ded78'
$branch = 'codex/gate10-stopwatch-tick-pacing'

if ((git branch --show-current) -ne $branch) {
    throw 'Wrong Gate 10 branch.'
}

$localPlanning = git rev-parse HEAD
$remotePlanning = ((git ls-remote --heads origin "refs/heads/$branch") -split '\s+')[0]
if ($localPlanning -ne $remotePlanning) {
    throw 'Gate 10 implementation must start from the independently approved remote Planning HEAD.'
}

git merge-base --is-ancestor $base HEAD
if ($LASTEXITCODE -ne 0) {
    throw 'Frozen Gate 9 baseline is not an ancestor of Gate 10 Planning HEAD.'
}

$planningFiles = @(git diff --name-only $base HEAD)
$expectedPlanningFiles = @(
    'Docs/Architecture/GATE10_SYNCHRONOUS_STOPWATCH_TICK_PACING.md',
    'Docs/superpowers/plans/2026-09-07-gate10-stopwatch-tick-pacing.md'
)
if ((Compare-Object $expectedPlanningFiles $planningFiles).Length -ne 0) {
    throw 'Planning scope is not exactly the two approved Gate 10 documents.'
}

if ((git status --porcelain).Length -ne 0) {
    throw 'Gate 10 worktree must be clean before implementation.'
}
```

- [ ] From the ordinary checkout, require exactly the two user-owned changes:

```powershell
$ordinary = 'E:\unityproject\LockstepArena'
$expected = @(
    ' M Assets/Settings/Mobile_RPAsset.asset',
    ' M ProjectSettings/ShaderGraphSettings.asset'
)
$actual = @(git -C $ordinary status --porcelain)
if ((Compare-Object $expected $actual).Length -ne 0) {
    throw 'Ordinary checkout no longer contains exactly the two protected user changes.'
}
```

## Exact File Map

Production:

```text
Create Server/LockstepArena.Server.FrameSync/ElapsedTickPacer.cs
Create Server/LockstepArena.Server.FrameSync/StopwatchTickDriver.cs
```

Tests:

```text
Create Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj
Create Tests/LockstepArena.Server.TickPacing.Tests/Program.cs
Create Tests/LockstepArena.Server.TickPacing.Tests/ElapsedTickPacerTests.cs
Create Tests/LockstepArena.Server.TickPacing.Tests/StopwatchTickDriverTests.cs
Create Tests/LockstepArena.Server.TickPacing.Tests/Gate10TickPacingGoldenVector.cs
```

Configuration and final evidence:

```text
Modify .gitignore
Modify Docs/Architecture/GATE10_SYNCHRONOUS_STOPWATCH_TICK_PACING.md only in final evidence task
```

No other authored file may change.

## Exact Test Project Contract

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

The exact suite order is:

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

## Task 1: Add Exact Elapsed-Time Arithmetic and Basic Pacing

**Commit:** `feat: pace authority from elapsed stopwatch ticks`

**Files:**

- Modify: `.gitignore`
- Create: `Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj`
- Create: `Tests/LockstepArena.Server.TickPacing.Tests/Program.cs`
- Create: `Tests/LockstepArena.Server.TickPacing.Tests/ElapsedTickPacerTests.cs`
- Create: `Server/LockstepArena.Server.FrameSync/ElapsedTickPacer.cs`

**Interfaces:**

- Consumes: frozen `TickDrivenFramePublisher.AdvanceOneTick()` and `SimulationConfig.TickRate`.
- Produces: the exact `ElapsedTickPacer` public API from the Spec.

- [ ] **Step 1: Create the dependency-free runner and project**

Add the exact project XML above. Add exactly this `.gitignore` line and no other ignore change:

```text
!Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj
```

`Program.cs` defines the existing repository-style `TestCase`, `TestAssert.Equal`, `Same`, `SequenceEqual`, `Throws`, and `ThrowsAndReturn` helpers, runs the registered tests in order, prints `PASS <name>`, then prints `RESULT <passed>/<total> passed`, returning nonzero on any failure.

- [ ] **Step 2: Register and write RED tests 1-13**

Register the first thirteen exact names. Freeze these arithmetic fixtures:

```text
frequency=300, TickRate=30
elapsed 9 -> 0 Advances
then elapsed 1 -> 1 Advance

frequency=1000
elapsed 33 -> 0 Advances with numerator 990
then elapsed 1 -> 1 Advance with remainder 20

single elapsed 100 at frequency 1000
equals segmented elapsed 7,11,15,1,33,33
both -> exactly 3 Advances and zero final fractional remainder
```

Test 12 must use the bounded fixture:

```text
initialTick = uint.MaxValue - 3
InputDelayTicks = 0
maxFutureTickOffset = 2
historyCapacity = 2
stopwatchFrequency = long.MaxValue
elapsedStopwatchTicks = long.MaxValue

mathematical dueAdvances = 30
actual successful Advances = 2
final EligibilityCeiling = uint.MaxValue - 1
```

This catches an overflowing `ulong` multiplication while reaching terminal after two real Advances. It must not loop billions of times.

Test 13 completes Tick100 under `InputDelayTicks=2`, processes one interval and observes no publication, then processes the second interval and requires `[100]`, proving Gate 9 owns maturity.

- [ ] **Step 3: Run RED**

```powershell
dotnet build Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj --configuration Release --nologo
```

Expected: compilation fails because `ElapsedTickPacer` does not exist. The failure must not be a missing dependency or malformed project reference.

- [ ] **Step 4: Implement the minimal arithmetic pacer**

Create the exact fields and constructor from the Spec. Implement the calculation with this shape:

```csharp
UInt128 scaled =
    (UInt128)_fractionalTickNumerator
    + (UInt128)(ulong)elapsedStopwatchTicks
    * (UInt128)(uint)SimulationConfig.TickRate;

UInt128 dueAdvances = scaled / _stopwatchFrequency;
ulong nextRemainder = checked((ulong)(scaled % _stopwatchFrequency));
```

Use a local `List<FrameData>`. Before every Advance, stop normally if `EligibilityCeiling == uint.MaxValue - 1U`. Decrement the `UInt128` due counter only after a successful Advance. Add each returned immutable Frame in order. Commit `_fractionalTickNumerator = nextRemainder` only after the loop finishes. Return `Array.Empty<FrameData>()` or `frames.ToArray()`.

At this Task, do not add a retry/recovery abstraction or expose state. Task 2 adds the frozen sticky-fault catch around the Advance call.

- [ ] **Step 5: Run GREEN and Gate 9 regression**

```powershell
dotnet run --project Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj --configuration Release
dotnet run --project Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj --configuration Release
```

Require tests 1-13 passed and Gate 9 `RESULT 27/27 passed`.

- [ ] **Step 6: Focused audit and commit**

Require no floating point, `TimeSpan`, `Frequency / TickRate`, Timer, Task, Thread, async, sleep, or new dependency. Confirm Test 12 executed only two Advances.

```powershell
git diff --check
git add .gitignore `
    Server/LockstepArena.Server.FrameSync/ElapsedTickPacer.cs `
    Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj `
    Tests/LockstepArena.Server.TickPacing.Tests/Program.cs `
    Tests/LockstepArena.Server.TickPacing.Tests/ElapsedTickPacerTests.cs
git commit -m "feat: pace authority from elapsed stopwatch ticks"
```

## Task 2: Freeze Flattening, Sticky Fault, and Terminal Behavior

**Commit:** `feat: contain tick pacing failures`

**Files:**

- Modify: `Tests/LockstepArena.Server.TickPacing.Tests/ElapsedTickPacerTests.cs`
- Modify: `Server/LockstepArena.Server.FrameSync/ElapsedTickPacer.cs`

**Interfaces:**

- Consumes: Task 1 `ElapsedTickPacer`.
- Produces: final publication ownership, sticky-fault, and terminal semantics.

- [ ] **Step 1: Write RED tests 14-22**

Test 14 completes Tick100-102 under `InputDelayTicks=1`, processes three due intervals in one call, and requires the single flattened array `[100,101,102]` in exact order.

Test 15 mutates its returned array element and independently requires Publisher history still contains its original immutable Frame references in Tick order.

For tests 16-19, build this exact test-only fault fixture:

```text
one-player roster
initialTick=100, InputDelayTicks=0, futureOffset=4, history=4
complete Tick100 -> publishes immediately
complete Tick101 -> pending
complete Tick102 -> pending
frequency=300, elapsed=20 -> two due Advances
```

Use reflection only in this fixture:

1. Locate the Publisher's unique private `AuthoritativeFrameCoordinator` field by field type.
2. Locate the Coordinator's unique private `Dictionary<uint, StrictFrameCollector>` field by field type.
3. Obtain Tick102's completed collector.
4. Locate its private completed `FrameData` field by field type.
5. Replace that completed Frame with a valid immutable Frame whose Tick is 101.

Advance #1 then publishes valid Tick101. Advance #2 plans Tick102 but encounters the corrupted Frame Tick and throws the existing Coordinator invariant `InvalidOperationException` before Coordinator field replacement.

Require:

```text
CollectionTick=101
EligibilityCeiling=101
NextPublishTick=102
history contains Tick100,Tick101
no returned partial array assignment
original InvalidOperationException is not wrapped
later negative and positive elapsed calls both throw InvalidOperationException
later calls leave Publisher/history unchanged
```

Tests 20-22 use near-terminal Publishers:

```text
terminal-at-entry: initialTick=uint.MaxValue-1, delay=0
mid-catch-up: initialTick=uint.MaxValue-2, delay=0, elapsed for 3 due Advances
```

Require mid-catch-up performs only one Advance, can return the final Frame if complete, does not fault, and subsequent pacing is empty. For the incomplete variant, final input arrives through `Submit` after pacing stops and publishes `uint.MaxValue-1`.

- [ ] **Step 2: Run RED**

```powershell
dotnet run --project Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj --configuration Release
```

Expected: at least the sticky-fault tests fail because Task 1 does not yet transition `_faulted` after an unexpected Advance failure.

- [ ] **Step 3: Add the minimal fail-stop catch**

Wrap only each non-terminal `AdvanceOneTick()` call:

```csharp
FrameData[] publication;
try
{
    publication = _publisher.AdvanceOneTick();
}
catch
{
    _faulted = true;
    throw;
}
```

Do not catch constructor validation, negative elapsed rejection, terminal checks, or arithmetic that occurs before catch-up. Do not commit the candidate remainder inside the loop. Abandon the local publication list when an exception escapes.

- [ ] **Step 4: Run GREEN and focused regressions**

```powershell
dotnet run --project Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj --configuration Release
dotnet run --project Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj --configuration Release
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj --configuration Release
```

Require tests 1-22 passed, Gate 9 `27/27`, and Gate 4 `32/32`.

- [ ] **Step 5: Audit and commit**

Confirm reflection appears only in the test fault fixture. Confirm no production hook, delegate, interface, `InternalsVisibleTo`, partial-result wrapper, reset, retry, or recovery exists.

```powershell
git diff --check
git add Server/LockstepArena.Server.FrameSync/ElapsedTickPacer.cs `
    Tests/LockstepArena.Server.TickPacing.Tests/ElapsedTickPacerTests.cs
git commit -m "feat: contain tick pacing failures"
```

## Task 3: Add the Thin Real Stopwatch Driver

**Commit:** `feat: drive authority pacing from stopwatch polls`

**Files:**

- Create: `Tests/LockstepArena.Server.TickPacing.Tests/StopwatchTickDriverTests.cs`
- Modify: `Tests/LockstepArena.Server.TickPacing.Tests/Program.cs`
- Create: `Server/LockstepArena.Server.FrameSync/StopwatchTickDriver.cs`

**Interfaces:**

- Consumes: final Task 2 `ElapsedTickPacer`.
- Produces: exact `StopwatchTickDriver(TickDrivenFramePublisher)` and `Poll()` API.

- [ ] **Step 1: Write RED tests 23-24**

Test 23 captures all Publisher schedule properties, constructs the Driver, and requires construction alone does not change CollectionTick, EligibilityCeiling, NextPublishTick, or history.

Test 24 uses a Publisher initialized at `uint.MaxValue - 1` with zero delay. Call `Poll()` twice and require both results empty and all Publisher state unchanged. Do not assert actual elapsed duration, exact delta, or exact Stopwatch timing.

- [ ] **Step 2: Run RED**

```powershell
dotnet build Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj --configuration Release --nologo
```

Expected: compilation fails only because `StopwatchTickDriver` is absent.

- [ ] **Step 3: Implement the exact Driver**

Use `Stopwatch.StartNew()`, capture its initial `ElapsedTicks`, and do no work during construction. Implement Poll in this exact order:

```csharp
long currentElapsedStopwatchTicks = _stopwatch.ElapsedTicks;
long elapsedStopwatchTicks = checked(
    currentElapsedStopwatchTicks - _lastElapsedStopwatchTicks);

FrameData[] publication =
    _pacer.ProcessElapsedStopwatchTicks(elapsedStopwatchTicks);

_lastElapsedStopwatchTicks = currentElapsedStopwatchTicks;
return publication;
```

Do not add a second fault flag, clock injection, interface, Timer, wait, sleep, thread, Task, async method, callback, or background loop.

- [ ] **Step 4: Run GREEN**

```powershell
dotnet run --project Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj --configuration Release
```

Require tests 1-24 passed.

- [ ] **Step 5: Audit and commit**

```powershell
git diff --check
git add Server/LockstepArena.Server.FrameSync/StopwatchTickDriver.cs `
    Tests/LockstepArena.Server.TickPacing.Tests/Program.cs `
    Tests/LockstepArena.Server.TickPacing.Tests/StopwatchTickDriverTests.cs
git commit -m "feat: drive authority pacing from stopwatch polls"
```

## Task 4: Add Actual-Only 2/3/4-Player Pacing Goldens

**Commit:** `test: prove stopwatch-paced authority determinism`

**Files:**

- Create: `Tests/LockstepArena.Server.TickPacing.Tests/Gate10TickPacingGoldenVector.cs`
- Modify: `Tests/LockstepArena.Server.TickPacing.Tests/ElapsedTickPacerTests.cs`
- Modify: `Tests/LockstepArena.Server.TickPacing.Tests/Program.cs`

**Interfaces:**

- Consumes: final pacer, existing Publisher, Domain, Simulation, and StateDigest.
- Produces: exact actual-only Golden vector and `RESULT 27/27 passed`.

- [ ] **Step 1: Register RED tests 25-27**

Register the exact final three test names. Reference missing methods:

```csharp
Gate10TickPacingGoldenVector.RunTwoPlayer()
Gate10TickPacingGoldenVector.RunThreePlayer()
Gate10TickPacingGoldenVector.RunFourPlayerPrimary()
Gate10TickPacingGoldenVector.RunFourPlayerAlternative()
```

The actual result type exposes only:

```text
FrameData[][] PublicationBatches
FrameData[] AuthoritativeFrames
BattleState[] SimulationStates
ulong[] Digests
FrameData[] History
BattleState FinalState
ulong CollectionTick
uint? EligibilityCeiling
uint NextPublishTick
```

- [ ] **Step 2: Run RED**

```powershell
dotnet build Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj --configuration Release --nologo
```

Expected: compilation fails because the Golden vector/result type does not exist.

- [ ] **Step 3: Implement exact actual-only vectors**

Implement the complete Spec Section 12 data literally. Use one local caller-owned `BattleSimulation` per run. Record every non-empty Submit or pacing publication; step each Frame in returned order; record each resulting State and Digest.

The vector source must not contain any expected Digest, expected final state, expected history Tick, or expected publication Tick array.

- [ ] **Step 4: Add exact consumer assertions**

Keep these expected literals only in `ElapsedTickPacerTests.cs`:

```text
Two player:   Tick11, Digest AE353BEBCCF29139
Three player: Tick21, Digest 38CCC825F57B7655
Four player:
  State101 D95809E1EB5CDDAA
  State102 A96B83267DD72A7D
  State103 386C4BB11A7EB7E0
  State104 9F41F69F63A24BCB
```

For both four-player runs require batches `[100,101]`, `[102]`, `[103]`, flattened Frames `100..103`, history `101..103`, CollectionTick `105`, EligibilityCeiling `103`, NextPublishTick `104`, every Frame roster/Input field, every full Simulation state, and final State104 values from the Spec.

- [ ] **Step 5: Run GREEN and focused regression**

```powershell
dotnet build Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj --configuration Release --nologo
dotnet run --project Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj --configuration Release --no-build
dotnet run --project Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj --configuration Release
```

Require Gate 10 `RESULT 27/27 passed` and Gate 9 `RESULT 27/27 passed`.

- [ ] **Step 6: Golden/source audit and commit**

Require exactly five authored Gate 10 test files and two direct ProjectReferences. Require all six expected Golden Digests absent from `Gate10TickPacingGoldenVector.cs` and production.

```powershell
git diff --check
git add Tests/LockstepArena.Server.TickPacing.Tests/Program.cs `
    Tests/LockstepArena.Server.TickPacing.Tests/ElapsedTickPacerTests.cs `
    Tests/LockstepArena.Server.TickPacing.Tests/Gate10TickPacingGoldenVector.cs
git commit -m "test: prove stopwatch-paced authority determinism"
```

## Task 5: Fresh Final Verification, Evidence, Push, and STOP

**Commit:** `docs: record Gate 10 implementation evidence`

**Files:**

- Modify: `Docs/Architecture/GATE10_SYNCHRONOUS_STOPWATCH_TICK_PACING.md`

### 5.1 Restore-assets preflight

- [ ] Resolve each effective `ProjectAssetsFile`. Restore only a project whose asset is missing, without changing any dependency/version/project contract. If any restore occurs, restart the complete build matrix from build 1.

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
    'Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj',
    'Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj'
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

### 5.2 Exact 15 Release builds

- [ ] Run independently in this order, all with zero warnings and zero errors:

```powershell
dotnet build Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj -c Release --no-restore --nologo
dotnet build Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj -c Release --no-restore --nologo
dotnet build Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release --no-restore --nologo
dotnet build Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj -c Release --no-restore --nologo
dotnet build Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj -c Release --no-restore --nologo
dotnet build Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj -c Release --no-restore --nologo
dotnet build Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj -c Release --no-restore --nologo
```

### 5.3 Exact .NET execution matrix

- [ ] Run:

```powershell
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj -c Release --no-build
dotnet run --project Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj -c Release --no-build
dotnet run --project Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj -c Release --no-build
```

Require:

```text
Gate 3 Simulation         RESULT 38/38 passed
Gate 4 FrameSync          RESULT 32/32 passed
Gate 5 Protocol           RESULT 35/35 passed
Gate 6 ProtocolAuthority  RESULT 24/24 passed
Gate 7 StreamFraming      RESULT 32/32 passed
Gate 3 Server Golden      Tick=1000 Players=4 Digest=89A7DD66F8D9E871
Gate 9 TickAuthority      RESULT 27/27 passed
Gate 10 TickPacing        RESULT 27/27 passed
```

- [ ] Run frozen Gate 8 under its external 30-second verification watchdog only:

```powershell
$stdoutPath = Join-Path (Get-Location) '.artifacts/gate10-gate8-tcp.stdout.txt'
$stderrPath = Join-Path (Get-Location) '.artifacts/gate10-gate8-tcp.stderr.txt'
$arguments = @('run','--project','Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj','-c','Release','--no-build')
$process = Start-Process dotnet -ArgumentList $arguments -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
if (-not $process.WaitForExit(30000)) {
    & taskkill.exe /PID $process.Id /T /F | Out-Null
    throw 'Frozen Gate 8 TCP tests exceeded the 30-second verification bound.'
}
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Gate 8 exited with code $($process.ExitCode)." }
Get-Content -Raw $stdoutPath
```

Require Gate 8 `RESULT 8/8 passed`, final Tick103, and Digest `386C4BB11A7EB7E0`.

### 5.4 Pinned Protocol regeneration

- [ ] Preserve the current environment with no protoc override, rebuild the pinned CodeGen project, require one generated source, then require diff-clean Schema and Generated paths:

```powershell
if ($env:PROTOBUF_PROTOC) { throw 'PROTOBUF_PROTOC override is not allowed.' }
if ($env:Protobuf_ProtocFullPath) { throw 'Protobuf_ProtocFullPath override is not allowed.' }

dotnet build Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj -c Release --no-restore --nologo

$generated = @(git ls-files 'Packages/com.locksteparena.protocol/Runtime/Generated/*.g.cs')
if ($generated.Length -ne 1 -or $generated[0] -ne 'Packages/com.locksteparena.protocol/Runtime/Generated/LockstepArenaProtocol.g.cs') {
    throw 'Pinned Protocol generation must produce exactly the one approved tracked source.'
}

git diff --exit-code -- Packages/com.locksteparena.protocol/Schema Packages/com.locksteparena.protocol/Runtime/Generated
```

### 5.5 Fresh Unity regressions

- [ ] Run Unity 6000.3.10f1 in three independent hidden `Start-Process -Wait` jobs without `-quit`. Delete each old XML first, require a fresh XML, parse totals, and locate the required named test. Exit code alone is not evidence.

Define this exact XML assertion helper before the three runs:

```powershell
function Assert-UnityNUnitXml {
    param(
        [string]$Path,
        [int]$MinimumTotal,
        [Nullable[int]]$ExactTotal,
        [string[]]$RequiredTestNameFragments
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Fresh Unity NUnit XML was not created: $Path"
    }

    [xml]$document = Get-Content -Raw -LiteralPath $Path
    $run = $document.SelectSingleNode('/test-run')
    if ($null -eq $run) { throw "Missing test-run node: $Path" }

    $total = [int]$run.GetAttribute('total')
    $passed = [int]$run.GetAttribute('passed')
    $failed = [int]$run.GetAttribute('failed')
    if ($total -lt $MinimumTotal) { throw "Unity total below required minimum in $Path" }
    if ($null -ne $ExactTotal -and $total -ne $ExactTotal.Value) {
        throw "Unity total did not match the exact requirement in $Path"
    }
    if ($failed -ne 0 -or $passed -ne $total) {
        throw "Unity test totals did not pass cleanly in $Path"
    }

    foreach ($fragment in $RequiredTestNameFragments) {
        $testCase = $document.SelectSingleNode(
            "//test-case[contains(@fullname,'$fragment') or @name='$fragment']")
        if ($null -eq $testCase -or $testCase.GetAttribute('result') -ne 'Passed') {
            throw "Required Unity test was not Passed: $fragment"
        }
    }
}
```

Gate 7:

```powershell
$xml = '.artifacts/gate10-unity/gate7-results.xml'
Remove-Item -LiteralPath $xml -Force -ErrorAction SilentlyContinue
$arguments = @('-batchmode','-nographics','-projectPath',(Get-Location).Path,'-runTests','-testPlatform','EditMode','-assemblyNames','LockstepArena.StreamFraming.Editor.Tests','-testResults',$xml,'-logFile','.artifacts/gate10-unity/gate7-unity.log')
$process = Start-Process -FilePath 'E:\unityhub\unity6.3\Editor\Unity.exe' -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
Write-Output "Unity Gate 7 diagnostic exit code: $($process.ExitCode)"
Assert-UnityNUnitXml -Path $xml -MinimumTotal 1 -ExactTotal 1 -RequiredTestNameFragments @('UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden')
```

Require `total=1 passed=1 failed=0` and `UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden = Passed`.

Gate 5:

```powershell
$xml = '.artifacts/gate10-unity/gate5-results.xml'
Remove-Item -LiteralPath $xml -Force -ErrorAction SilentlyContinue
$arguments = @('-batchmode','-nographics','-projectPath',(Get-Location).Path,'-runTests','-testPlatform','EditMode','-assemblyNames','LockstepArena.Protocol.Editor.Tests','-testResults',$xml,'-logFile','.artifacts/gate10-unity/gate5-unity.log')
$process = Start-Process -FilePath 'E:\unityhub\unity6.3\Editor\Unity.exe' -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
Write-Output "Unity Gate 5 diagnostic exit code: $($process.ExitCode)"
Assert-UnityNUnitXml -Path $xml -MinimumTotal 2 -ExactTotal 2 -RequiredTestNameFragments @('GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads','UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector')
```

Require `total=2 passed=2 failed=0` and both named tests:

```text
GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads
UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector
```

Gate 3:

```powershell
$xml = '.artifacts/gate10-unity/gate3-results.xml'
Remove-Item -LiteralPath $xml -Force -ErrorAction SilentlyContinue
$arguments = @('-batchmode','-nographics','-projectPath',(Get-Location).Path,'-runTests','-testPlatform','EditMode','-assemblyNames','LockstepArena.Simulation.Editor.Tests','-testFilter','UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector','-testResults',$xml,'-logFile','.artifacts/gate10-unity/gate3-unity.log')
$process = Start-Process -FilePath 'E:\unityhub\unity6.3\Editor\Unity.exe' -ArgumentList $arguments -PassThru -Wait -WindowStyle Hidden
Write-Output "Unity Gate 3 diagnostic exit code: $($process.ExitCode)"
Assert-UnityNUnitXml -Path $xml -MinimumTotal 1 -ExactTotal $null -RequiredTestNameFragments @('UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector')
```

Require `total>=1 failed=0` and `UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector = Passed`.

After each Unity run, inspect exact worktree-local `Assets/` and `ProjectSettings/` diff. Restore only an individually inspected Unity-generated path. Never run broad reset/clean and never touch the ordinary checkout.

### 5.6 Protected-boundary, dependency, source, and artifact audits

- [ ] Require zero frozen-base diff:

```powershell
$base = 'e041efb290e1df609fc5003d619e4e19f70ded78'
git diff --exit-code $base -- Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs
git diff --exit-code $base -- Server/LockstepArena.Server.FrameSync/TickDrivenFramePublisher.cs
git diff --exit-code $base -- Packages/com.locksteparena.simulation
git diff --exit-code $base -- Packages/com.locksteparena.protocol
git diff --exit-code $base -- Packages/com.locksteparena.stream-framing
git diff --exit-code $base -- Server/LockstepArena.Server.ProtocolAuthority
git diff --exit-code $base -- Tests/LockstepArena.TcpEndToEnd.Tests
git diff --exit-code $base -- Assets ProjectSettings Packages/manifest.json Packages/packages-lock.json
git diff --exit-code $base -- Tests ':(exclude)Tests/LockstepArena.Server.TickPacing.Tests/**'
```

- [ ] Require FrameSync production diff exactly the two new sources, Gate 10 tests exactly five files and two ProjectReferences, and `.gitignore` exactly one added exception.
- [ ] Require no expected Digest in either production file or `Gate10TickPacingGoldenVector.cs`.
- [ ] Require reflection only in the test-only fault fixture.
- [ ] Search production diff for Timer, Task, Thread, async, sleep, Unity, TCP, UDP, KCP, Socket, NetworkStream, timeout, retry, recovery, neutral input, prediction, snapshot, rollback, replay, DI, EventBus, interface, delegate injection, and `InternalsVisibleTo`; require no implementation match.
- [ ] Require no symlink/junction, copy/sync/cleanup script, generated source, new package, tracked `bin/obj`, or tracked LockstepArena build DLL. Embedded packages must contain no generated `bin`, `obj`, or LockstepArena DLL.
- [ ] Run `git diff --check`, inspect the complete base diff, and scan both Gate 10 documents for unresolved markers, obsolete test counts, floating-point pacing, or conflicting terminal/fault language.
- [ ] From the ordinary checkout, require exactly its two protected user-owned status lines and no Gate 10 path.

### 5.7 Evidence-only commit, push, and STOP

- [ ] Append an `Implementation Evidence` section to the Architecture document containing only fresh results:

```text
frozen base, approved Planning HEAD, implementation SHAs, evidence parent
restore-assets result
15 independent Release build results
Gate 3-10 exact suite totals and Gate 3 Server Golden
Gate 8 external-watchdog result
UInt128 long.MaxValue fixture's bounded two-Advance execution
partial-failure committed state, original exception, no partial return, sticky rejection
2/3/4-player publications, states, history, and Digests
real Stopwatch constructor/Poll proof
pinned Protocol regeneration result
three fresh Unity XML paths, totals, and named tests
protected-path, dependency, source-scope, artifact, and ordinary-checkout audits
```

- [ ] Commit only that evidence update:

```powershell
git add Docs/Architecture/GATE10_SYNCHRONOUS_STOPWATCH_TICK_PACING.md
git commit -m "docs: record Gate 10 implementation evidence"
```

- [ ] Push only the Gate 10 branch and prove equality and cleanliness:

```powershell
$branch = 'codex/gate10-stopwatch-tick-pacing'
git push origin $branch
$localFinal = git rev-parse HEAD
$remoteFinal = ((git ls-remote --heads origin "refs/heads/$branch") -split '\s+')[0]
if ($localFinal -ne $remoteFinal) { throw 'Remote Gate 10 SHA does not match local HEAD.' }
if ((git status --porcelain).Length -ne 0) { throw 'Gate 10 worktree is not clean.' }
```

- [ ] Confirm the ordinary checkout one final time, submit the Gate 10 Final Implementation Handoff, and **STOP**.

Do not begin Gate 11, production TCP lifecycle, KCP, adaptive InputDelay, client clock synchronization, prediction, rollback, replay, or any scheduler framework.

## Final Acceptance Invariants

Gate 10 is eligible for Final Handoff only when:

- integer pacing exactly follows the Spec formula and carries remainder without drift;
- negative input and sticky-fault priority match the frozen order;
- partial catch-up keeps successful Advances, returns no partial batch, and permanently faults;
- terminal exhaustion never faults or wraps and final incomplete input remains Submit-only;
- the Driver uses real `Stopwatch`, remains synchronous, and adds no independent recovery state;
- Gate 10 reports exactly `RESULT 27/27 passed`, including the bounded long.MaxValue proof;
- the exact 2/3/4-player Goldens and final `9F41F69F63A24BCB` Digest match;
- all 15 builds, Gate 3-9 regressions, Gate 8 watchdog, pinned regeneration, and Unity Gate 7/5/3 XML checks pass;
- protected paths, dependencies, source scope, artifacts, and ordinary checkout match their frozen contracts;
- remote SHA equals local final HEAD and work stops before the next Gate.
