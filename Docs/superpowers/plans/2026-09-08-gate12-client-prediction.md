# Gate 12 Offline Bounded Client Prediction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add one offline, bounded client prediction timeline that confirms clean authority without recomputation and corrects Dirty authority through deterministic rollback/replay.

**Architecture:** A dependency-free embedded client-prediction package depends only on the existing immutable Simulation Domain. One `ClientPredictionTimeline` owns authoritative/predicted state references plus a bounded private array of `StateBefore + PredictedFrame` records; every mutation builds and validates complete candidates before committing live fields.

**Tech Stack:** Unity 6000.3.10f1 embedded UPM package, .NET Standard 2.1/C# 9 production, .NET 8/C# 12 dependency-free test runner, existing `LockstepArena.Simulation` Domain.

**Spec:** `Docs/Architecture/GATE12_OFFLINE_BOUNDED_CLIENT_PREDICTION.md`

## Global Constraints

- Start implementation from the independently approved Gate 12 Planning HEAD, never by resetting to the frozen base.
- Frozen comparison base is `03635526ffe53fcb384bf4b24887d34a942502bf`.
- Production is exactly one class under `Packages/com.locksteparena.client-prediction/Runtime/` and depends only on Simulation.
- Preserve the exact public API, private record shape, validation priority, candidate-before-commit semantics, sticky-fault behavior, terminal boundary, and 36-test names from the Spec.
- Internal invariant failure sets sticky fault and throws `InvalidOperationException`; candidate Step/replay failure sets sticky fault and rethrows its original exception unchanged.
- Read-only getters remain usable after fault; only `Predict` and `ReconcileAuthoritative` reject first.
- Do not modify Gate 3-11 production/test sources, Gate 3-11 Goldens, `Assets`, `ProjectSettings`, or `Packages/manifest.json`.
- Do not modify `TcpClientBattlePump` or `TcpServerBattlePump`; Gate 12 is offline.
- Do not introduce Protocol, Protobuf, StreamFraming, FrameSync, LiveTcp, UnityEngine, UnityEditor, networking, InputDelay, wall-clock, neutral/repeat-last input, Replay, or a generic prediction/rollback/snapshot framework.
- Expected state/Digest/Dirty literals live only in .NET and Unity consumer tests, never production or the actual-only Golden vector.
- Keep the ordinary checkout user-owned changes untouched: `Assets/Settings/Mobile_RPAsset.asset` and `ProjectSettings/ShaderGraphSettings.asset`.

---

## Final file map

Planning already creates only:

```text
Docs/Architecture/GATE12_OFFLINE_BOUNDED_CLIENT_PREDICTION.md
Docs/superpowers/plans/2026-09-08-gate12-client-prediction.md
```

Implementation creates:

```text
Packages/com.locksteparena.client-prediction/package.json
Packages/com.locksteparena.client-prediction/Runtime/Directory.Build.props
Packages/com.locksteparena.client-prediction/Runtime/LockstepArena.Client.Prediction.asmdef
Packages/com.locksteparena.client-prediction/Runtime/LockstepArena.Client.Prediction.csproj
Packages/com.locksteparena.client-prediction/Runtime/ClientPredictionTimeline.cs
Packages/com.locksteparena.client-prediction/Tests/Editor/Gate12PredictionGoldenVector.cs
Packages/com.locksteparena.client-prediction/Tests/Editor/UnityClientPredictionGoldenTests.cs
Packages/com.locksteparena.client-prediction/Tests/Editor/LockstepArena.Client.Prediction.Editor.Tests.asmdef
Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj
Tests/LockstepArena.Client.Prediction.Tests/Program.cs
Tests/LockstepArena.Client.Prediction.Tests/TestAssert.cs
Tests/LockstepArena.Client.Prediction.Tests/ClientPredictionTimelineTests.cs
```

Implementation also creates/tracks matching Unity `.meta` files and modifies only:

```text
.gitignore
Packages/packages-lock.json
Docs/Architecture/GATE12_OFFLINE_BOUNDED_CLIENT_PREDICTION.md
```

The Architecture modification is the final evidence-only commit. `.gitignore`
adds exactly the two authored `.csproj` exceptions from the Spec. The lockfile
adds exactly the one embedded client-prediction entry from the Spec.

## Frozen production contract

The final production API is exactly:

```csharp
namespace LockstepArena.Client.Prediction
{
    public sealed class ClientPredictionTimeline
    {
        public ClientPredictionTimeline(
            BattleState initialState,
            int maxPredictionTicks);

        public BattleState AuthoritativeState { get; }
        public BattleState PredictedState { get; }
        public int MaxPredictionTicks { get; }
        public int PendingPredictionCount { get; }

        public void Predict(FrameData predictedFrame);
        public bool ReconcileAuthoritative(FrameData authoritativeFrame);
    }
}
```

`ReconcileAuthoritative` returns `false` for clean confirmation and for
authority when `A == P`; it returns `true` only after a Dirty rollback/replay
commits successfully.

Live invariant:

```text
A = AuthoritativeState.Tick
P = PredictedState.Tick
A <= P
history.Length == P - A
history.Length <= MaxPredictionTicks
history covers exactly [A,P)
empty history implies A == P
non-empty history[0].StateBefore structurally equals AuthoritativeState
record i PredictedFrame.Tick == A+i
record i StateBefore.Tick == record i PredictedFrame.Tick
record rosters structurally equal the battle roster
PredictedState.Tick == P
```

Constructor validation is `initialState null -> maxPredictionTicks < 1 ->
initialize`. `Predict` validation is `fault -> null -> Tick -> roster ->
terminal -> internal invariant -> capacity -> candidate -> commit`.
Reconciliation validation is `fault -> null -> authoritative Tick -> roster
-> terminal -> internal invariant -> classify -> candidate -> commit`.

Private record and fields are exactly the shapes frozen by the Spec:

```csharp
private BattleState _authoritativeState;
private BattleState _predictedState;
private PredictionRecord[] _history;
private readonly int _maxPredictionTicks;
private bool _faulted;

private sealed class PredictionRecord
{
    public PredictionRecord(BattleState stateBefore, FrameData predictedFrame);
    public BattleState StateBefore { get; }
    public FrameData PredictedFrame { get; }
}
```

No additional public type or persistent `BattleSimulation` is allowed.

## Required implementation-start verification

- [ ] Require the local Planning HEAD to equal the remote Planning branch,
remain exactly one commit above the frozen base, and preserve cleanliness and
the ordinary checkout. Independent implementation authorization must name
that same local/remote SHA before this check is run:

```powershell
$base = '03635526ffe53fcb384bf4b24887d34a942502bf'
if ((git branch --show-current) -ne 'codex/gate12-client-prediction') {
  throw 'Wrong Gate 12 branch.'
}
$localHead = git rev-parse HEAD
$remoteLine = git ls-remote origin refs/heads/codex/gate12-client-prediction
if ($LASTEXITCODE -ne 0 -or -not $remoteLine) {
  throw 'Unable to resolve the Gate 12 remote Planning branch.'
}
$remoteHead = ($remoteLine -split '\s+')[0]
if ($localHead -ne $remoteHead) {
  throw 'Local and remote Gate 12 Planning HEAD differ.'
}
git merge-base --is-ancestor $base HEAD
if ($LASTEXITCODE -ne 0) { throw 'Frozen Gate 11 base is not an ancestor.' }
if ((git rev-list --count "$base..HEAD") -ne '1') {
  throw 'Gate 12 implementation must start exactly one Planning commit above the frozen base.'
}
if ((git rev-list --count "HEAD..$base") -ne '0') {
  throw 'Gate 12 Planning branch is behind the frozen base.'
}
if ((git status --porcelain).Length -ne 0) {
  throw 'Gate 12 worktree must be clean.'
}
$ordinary = @(git -C 'E:\unityproject\LockstepArena' status --porcelain)
$expectedOrdinary = @(
  ' M Assets/Settings/Mobile_RPAsset.asset',
  ' M ProjectSettings/ShaderGraphSettings.asset'
)
if ((Compare-Object $ordinary $expectedOrdinary).Length -ne 0) {
  throw 'Ordinary checkout differs from its two protected user changes.'
}
```

## Exact test registry

The final runner registers these exact names once and prints
`RESULT 36/36 passed`:

1. `ConstructorRejectsNullInitialState`
2. `ConstructorRejectsNonPositiveMaxPredictionTicks`
3. `ConstructorStartsAlignedWithEmptyHistory`
4. `PredictRejectsNullFrameWithoutMutation`
5. `PredictRejectsWrongTickWithoutMutation`
6. `PredictRejectsRosterMismatchWithoutMutation`
7. `PredictRejectsWhenLeadEqualsCapacityWithoutMutation`
8. `PredictAdvancesOnlyPredictedTimeline`
9. `PredictionSupportsTwoThreeAndFourPlayerRosters`
10. `PredictionUsesCanonicalSlotOrder`
11. `FinalConsumablePredictionReachesUintMaxValue`
12. `PredictionBeyondTerminalIsRejectedWithoutMutation`
13. `ReconcileRejectsNullFrameWithoutMutation`
14. `ReconcileRejectsWrongTickWithoutMutation`
15. `ReconcileRejectsRosterMismatchWithoutMutation`
16. `ReconcileBoundaryFailureDoesNotFaultSubsequentValidWork`
17. `ReconcileAtAlignedFrontierAdvancesBothTimelinesCleanly`
18. `StructurallyEqualRosterInstancesAreAccepted`
19. `EqualFrameDataInDifferentInstancesIsClean`
20. `CanonicallyEqualFramesFromDifferentConstructionOrderAreClean`
21. `CleanReconcileRemovesOldestWithoutRecomputingPredictedState`
22. `CleanReconcileRetainsLaterPredictionSnapshots`
23. `MoveXDifferenceIsDirty`
24. `MoveZDifferenceIsDirty`
25. `AimDifferenceIsDirty`
26. `DifferentInputWithSameClampedStateIsDirty`
27. `EarliestPendingMismatchRollsBackAndReplaysAllLaterFrames`
28. `LatestPendingMismatchRollsBackWithoutLaterReplay`
29. `DirtyReplayRebuildsRetainedSnapshots`
30. `CorrectPredictionGoldenConvergesCleanly`
31. `WrongPredictionGoldenFindsTick101AndConvergesAfterReplay`
32. `TwinPredictorsMatchDigestsAfterEveryOperation`
33. `ImpossibleInternalInvariantEntersStickyFailStopWithoutPartialCommit`
34. `StickyFaultRejectsBeforeArgumentValidation`
35. `FinalAuthoritativeFrameDrainsTerminalPredictionWithoutWrap`
36. `AuthorityBeyondTerminalIsRejectedWithoutMutation`

---

## Task 1: Add bounded prediction and tests 1-12

**Files:**

- Create: `Packages/com.locksteparena.client-prediction/package.json`
- Create: `Packages/com.locksteparena.client-prediction/Runtime/Directory.Build.props`
- Create: `Packages/com.locksteparena.client-prediction/Runtime/LockstepArena.Client.Prediction.asmdef`
- Create: `Packages/com.locksteparena.client-prediction/Runtime/LockstepArena.Client.Prediction.csproj`
- Create: `Packages/com.locksteparena.client-prediction/Runtime/ClientPredictionTimeline.cs`
- Create: `Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj`
- Create: `Tests/LockstepArena.Client.Prediction.Tests/Program.cs`
- Create: `Tests/LockstepArena.Client.Prediction.Tests/TestAssert.cs`
- Create: `Tests/LockstepArena.Client.Prediction.Tests/ClientPredictionTimelineTests.cs`
- Modify: `.gitignore`

**Interfaces:**

- Consumes: `BattleState`, `FrameData`, `BattleSimulation`, `ActiveRoster`, `PlayerSlot`, and `PlayerState` from `LockstepArena.Simulation`.
- Produces initially: constructor/getters and `void Predict(FrameData)` from the frozen public API. `ReconcileAuthoritative` is added in Task 2, so Task 1 does not commit a temporary public stub.

### 1.1 RED project and test runner

- [ ] Add exactly the two `.gitignore` exceptions:

```text
!Packages/com.locksteparena.client-prediction/Runtime/LockstepArena.Client.Prediction.csproj
!Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj
```

- [ ] Create the test `.csproj` with exactly:

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
    <ProjectReference Include="..\..\Packages\com.locksteparena.client-prediction\Runtime\LockstepArena.Client.Prediction.csproj" />
    <ProjectReference Include="..\..\Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj" />
  </ItemGroup>
</Project>
```

- [ ] Add dependency-free `TestAssert` methods for equality, reference
equality/non-equality, and generic exception capture. Add a runner that
executes tests 1-12 by their exact frozen names and returns nonzero on any
failure.

- [ ] Write tests 1-12. Use immutable state/frame fixtures, explicit state
snapshots before every rejection, roster sizes 2/3/4 in Test 9, shuffled
construction passed through `FrameData.Create` in Test 10, and initial Tick
`uint.MaxValue - 1` in Tests 11-12.

- [ ] Run RED:

```powershell
dotnet build Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release --nologo
```

Expected RED: the referenced Prediction Runtime project/type does not exist.
No unrelated existing project may fail.

### 1.2 Minimal package and prediction implementation

- [ ] Create package metadata, Runtime asmdef, Runtime `.csproj`, and
package-local artifact routing exactly as frozen in the Spec.

- [ ] Implement the exact state and record ownership:

```csharp
private BattleState _authoritativeState;
private BattleState _predictedState;
private PredictionRecord[] _history;
private readonly int _maxPredictionTicks;
private bool _faulted;

private sealed class PredictionRecord
{
    public PredictionRecord(BattleState stateBefore, FrameData predictedFrame)
    {
        StateBefore = stateBefore;
        PredictedFrame = predictedFrame;
    }

    public BattleState StateBefore { get; }
    public FrameData PredictedFrame { get; }
}
```

- [ ] Implement constructor validation in frozen order, read-only getters,
structural roster/state comparison, and a live/candidate invariant validator.
The validator checks `A <= P`, exact history length, capacity, exact contiguous
record ticks, StateBefore ticks, structural rosters, empty-history alignment,
and oldest StateBefore equality with AuthoritativeState. It never re-simulates
history and never uses `StateDigest`.

- [ ] Implement `Predict` in frozen order:

```text
fault -> null -> Tick -> roster -> terminal -> live invariant -> capacity
-> local BattleSimulation.Step -> new array append -> candidate invariant
-> commit predicted state/history
```

Use widened arithmetic for `P - A`; never subtract unsigned ticks before
checking `A <= P`. On valid candidate failure, set sticky fault and rethrow the
original exception unchanged. Internal invariant failure throws
`InvalidOperationException` after setting fault.

### 1.3 GREEN, focused audit, commit

- [ ] Run:

```powershell
dotnet run --project Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release
```

Require interim `RESULT 12/12 passed`.

- [ ] Build the Runtime and require zero warnings/errors:

```powershell
dotnet build Packages/com.locksteparena.client-prediction/Runtime/LockstepArena.Client.Prediction.csproj -c Release --no-restore --nologo
```

- [ ] Audit that production imports only `System` and
`LockstepArena.Simulation`, contains no forbidden namespace/type, and that
`.gitignore` changed by exactly the two approved lines.

- [ ] Commit only Task 1 files:

```powershell
git add .gitignore Packages/com.locksteparena.client-prediction/package.json Packages/com.locksteparena.client-prediction/Runtime Tests/LockstepArena.Client.Prediction.Tests
git commit -m "feat: add bounded client prediction"
```

Before committing, do not stage any Unity `.meta` or lockfile change that was
not part of Task 1.

---

## Task 2: Add authoritative validation and clean reconciliation

**Files:**

- Modify: `Packages/com.locksteparena.client-prediction/Runtime/ClientPredictionTimeline.cs`
- Modify: `Tests/LockstepArena.Client.Prediction.Tests/Program.cs`
- Modify: `Tests/LockstepArena.Client.Prediction.Tests/ClientPredictionTimelineTests.cs`

**Interfaces:**

- Consumes: Task 1 timeline state, history, invariant and comparison helpers.
- Produces: exact `bool ReconcileAuthoritative(FrameData authoritativeFrame)` API with aligned and clean behavior. Dirty input has a temporary explicit rejection in this checkpoint and is replaced completely in Task 3.

### 2.1 RED tests 13-22

- [ ] Add and register tests 13-22 in the exact final names/order.

Required proof details:

```text
13: null authority, complete before/after state comparison
14: stale and future-gap Tick rejection
15: structurally different roster rejection and no Dirty classification
16: boundary rejection followed by valid successful reconciliation
17: A == P advances both state references and returns false
18: separately constructed structurally equal roster is accepted
19: equivalent separately allocated FrameData is clean
20: differently ordered received inputs become equal through FrameData.Create
21: clean confirmation removes one record and preserves PredictedState reference
22: later retained prediction remains usable as the next rollback frontier
```

- [ ] Run RED:

```powershell
dotnet build Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release --no-restore --nologo
```

Expected RED: `ReconcileAuthoritative` is missing. No existing behavior may
fail.

### 2.2 Minimal aligned and clean implementation

- [ ] Add `ReconcileAuthoritative` with frozen validation order:

```text
fault -> null -> exact authoritative Tick -> roster -> terminal
-> live invariant -> classification -> candidate -> candidate invariant -> commit
```

- [ ] Implement the `A == P` candidate path using one local Simulation and
the same resulting immutable state reference for both timelines.

- [ ] Implement canonical input equality over Tick and, in increasing Slot
order, PlayerSlot/MoveX/MoveZ/Aim. Do not use bytes, identity, hashes, Digest,
arrival order, or resulting state.

- [ ] Implement clean reconciliation by staging authoritative Step, slicing a
new history array, leaving the existing predicted state reference untouched,
checking the new frontier, then committing and returning `false`.

- [ ] For a Dirty frame only at this interim checkpoint, throw an explicit
`InvalidOperationException` before any candidate/live mutation. Do not create
a compatibility flag or abstraction around this temporary branch; Task 3
replaces it with final rollback/replay.

### 2.3 GREEN, regressions, audit, commit

- [ ] Run Gate 12 and require interim `RESULT 22/22 passed`:

```powershell
dotnet run --project Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release
```

- [ ] Run Gate 3 Simulation regression and require `38/38`:

```powershell
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release
```

- [ ] Audit that clean confirmation does not assign `_predictedState`, roster
mismatch exits before the Dirty helper, and no state/history field assignment
occurs before candidate validation.

- [ ] Commit:

```powershell
git add Packages/com.locksteparena.client-prediction/Runtime/ClientPredictionTimeline.cs Tests/LockstepArena.Client.Prediction.Tests/Program.cs Tests/LockstepArena.Client.Prediction.Tests/ClientPredictionTimelineTests.cs
git commit -m "feat: reconcile clean client predictions"
```

---

## Task 3: Add Dirty rollback/replay, fail-stop, and terminal authority

**Files:**

- Modify: `Packages/com.locksteparena.client-prediction/Runtime/ClientPredictionTimeline.cs`
- Modify: `Tests/LockstepArena.Client.Prediction.Tests/Program.cs`
- Modify: `Tests/LockstepArena.Client.Prediction.Tests/ClientPredictionTimelineTests.cs`

**Interfaces:**

- Consumes: exact public API and clean behavior from Tasks 1-2.
- Produces: final Dirty reconciliation, rebuilt snapshots, sticky fail-stop,
and terminal behavior. No temporary Dirty rejection remains afterward.

### 3.1 RED tests 23-29 and 33-36

- [ ] Add and register exact tests 23-29 and 33-36.

Fixtures must prove:

```text
23-25: each MoveX/MoveZ/Aim difference independently returns Dirty
26: at an arena clamp boundary, different Move input with equal resulting state is Dirty
27: oldest mismatch rolls back and replays every later retained frame
28: mismatch at latest remaining record performs no later replay
29: a subsequent reconciliation proves replay rebuilt the retained StateBefore
33: test-only reflection creates one impossible private history invariant;
    the call throws InvalidOperationException, faults, and commits no candidate
34: after Test 33-style fault, null/wrong calls still fail first with
    InvalidOperationException while all getters return last committed values
35: prediction of uint.MaxValue-1 followed by authority drains to uint.MaxValue
36: authority beyond terminal is rejected without mutation or wrap
```

Reflection locates the unique private prediction-history field by its array
element shape; it must not add a production hook or freeze a private field
name as public API.

- [ ] Run RED:

```powershell
dotnet run --project Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release
```

Expected RED: Dirty cases hit the interim rejection and fail their rollback,
replay, snapshot, and bool assertions. Sticky/terminal cases also fail until
their final handling exists.

### 3.2 Minimal final reconciliation implementation

- [ ] Replace the temporary Dirty rejection with the exact algorithm:

```text
StateBefore(T)
-> Step authoritative T
-> start candidate predicted state at corrected T+1
-> for each retained predicted frame T+1..P-1:
     append PredictionRecord(candidateStateBefore, frame)
     BattleSimulation.Step(frame)
-> require final predicted Tick P
-> validate complete candidate history
-> commit all three live references
-> return true
```

- [ ] Ensure clean and Dirty paths share only small private comparison,
candidate Step, array-copy, and invariant helpers. Do not introduce a generic
transaction/snapshot/replay abstraction.

- [ ] Finalize failure behavior:

```text
public boundary rejection -> no fault, no mutation
internal invariant failure -> _faulted=true; throw InvalidOperationException
candidate Step/replay throw -> _faulted=true; throw; (original exception)
faulted mutation call       -> InvalidOperationException before arguments
read-only getter after fault -> last successfully committed value
```

- [ ] Finalize terminal checks so `uint.MaxValue - 1` is consumable and
`uint.MaxValue` is not, with no unsigned arithmetic wrap.

### 3.3 GREEN, focused audit, commit

- [ ] Run Gate 12 and require interim `RESULT 33/33 passed` (tests 1-29 and
33-36; Golden/twin tests 30-32 arrive in Task 4):

```powershell
dotnet run --project Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release
```

- [ ] Run Gate 3 `38/38` again.

- [ ] Audit no temporary Dirty rejection remains, all replay uses only
`BattleSimulation.Step`, no live field is assigned during candidate loops,
original candidate exceptions use bare `throw;`, and getters do not reject
after fault.

- [ ] Commit:

```powershell
git add Packages/com.locksteparena.client-prediction/Runtime/ClientPredictionTimeline.cs Tests/LockstepArena.Client.Prediction.Tests/Program.cs Tests/LockstepArena.Client.Prediction.Tests/ClientPredictionTimelineTests.cs
git commit -m "feat: rollback dirty client predictions"
```

---

## Task 4: Add correct/wrong Goldens and Unity execution proof

**Files:**

- Create: `Packages/com.locksteparena.client-prediction/Tests/Editor/Gate12PredictionGoldenVector.cs`
- Create: `Packages/com.locksteparena.client-prediction/Tests/Editor/UnityClientPredictionGoldenTests.cs`
- Create: `Packages/com.locksteparena.client-prediction/Tests/Editor/LockstepArena.Client.Prediction.Editor.Tests.asmdef`
- Create: matching package `.meta` files
- Modify: `Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj`
- Modify: `Tests/LockstepArena.Client.Prediction.Tests/Program.cs`
- Modify: `Tests/LockstepArena.Client.Prediction.Tests/ClientPredictionTimelineTests.cs`
- Modify: `Packages/packages-lock.json`

**Interfaces:**

- Consumes: final production timeline from Task 3.
- Produces: one physical pure-C# actual-only Golden vector consumed directly
by both .NET and Unity; final tests 30-32; Unity named tests 2/2.

### 4.1 Frozen actual vectors

- [ ] Use this exact four-player roster and initial State:

```text
Slot0 PlayerId 0102030405060708  X=-300 Z=0    Aim=1000
Slot1 PlayerId 000000000000002A  X=300  Z=0    Aim=2000
Slot2 PlayerId FFEEDDCCBBAA0099  X=0    Z=-300 Aim=3000
Slot3 PlayerId 00000000000F4243  X=0    Z=300  Aim=4000
Initial Tick=100
```

- [ ] Use these exact authoritative/correct-predicted inputs:

```text
Tick100: S0( 1, 0,10100) S1(-1, 0,20100) S2( 0, 1,30100) S3( 0,-1,40100)
Tick101: S0( 0, 1,10101) S1( 0,-1,20101) S2( 1, 0,30101) S3(-1, 0,40101)
Tick102: S0(-1, 0,10102) S1( 1, 0,20102) S2( 0,-1,30102) S3( 0, 1,40102)
```

- [ ] Wrong prediction differs only at Tick101 Slot2:

```text
authority  MoveX=1  MoveZ=0 Aim=30101
prediction MoveX=-1 MoveZ=0 Aim=30101
```

- [ ] Freeze these consumer-owned correct-path oracles:

```text
State101
  S0(-200,   0,10100) S1( 200,   0,20100)
  S2(   0,-200,30100) S3(   0, 200,40100)
  Digest D95809E1EB5CDDAA

State102
  S0(-200, 100,10101) S1( 200,-100,20101)
  S2( 100,-200,30101) S3(-100, 200,40101)
  Digest A96B83267DD72A7D

State103
  S0(-300, 100,10102) S1( 300,-100,20102)
  S2( 100,-300,30102) S3(-100, 300,40102)
  Digest 386C4BB11A7EB7E0

Dirty sequence false,false,false
Final A=103 P=103 Pending=0
```

- [ ] Freeze these consumer-owned wrong-path oracles:

```text
wrong predicted State102
  S0(-200, 100,10101) S1( 200,-100,20101)
  S2(-100,-200,30101) S3(-100, 200,40101)
  Digest 8506E4507001B972

wrong predicted State103 before authority
  S0(-300, 100,10102) S1( 300,-100,20102)
  S2(-100,-300,30102) S3(-100, 300,40102)
  Digest 7D0D3A230618500F

reconciliation Dirty sequence false,true,false

after Dirty Tick101:
  authoritative State102 Digest A96B83267DD72A7D
  reconstructed predicted State103
    S0(-300, 100,10102) S1( 300,-100,20102)
    S2( 100,-300,30102) S3(-100, 300,40102)
    Digest 386C4BB11A7EB7E0
  retained Tick102 StateBefore equals corrected State102

after clean Tick102:
  A=103 P=103 Pending=0
  authoritative/predicted Digest 386C4BB11A7EB7E0
```

The vector returns actual states, actual Dirty bools, actual pending counts,
and actual checkpoints. It contains none of these consumer literals:

```text
D95809E1EB5CDDAA
8506E4507001B972
7D0D3A230618500F
A96B83267DD72A7D
386C4BB11A7EB7E0
```

### 4.2 RED tests 30-32

- [ ] Add tests 30-32 and register the final exact 1-36 order. Add an
explicit external compile link to the test `.csproj`:

```xml
<Compile Include="..\..\Packages\com.locksteparena.client-prediction\Tests\Editor\Gate12PredictionGoldenVector.cs"
         Link="Gate12PredictionGoldenVector.cs" />
```

- [ ] Tests 30 and 31 own all expected state/Dirty/count/Digest literals.
Test 32 runs two independent predictors through identical correct and wrong
operation sequences and compares both timeline states by field and Digest
after every Predict/Reconcile call.

- [ ] Run RED before creating the Golden vector:

```powershell
dotnet build Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release --no-restore --nologo
```

Expected RED: the explicitly linked `Gate12PredictionGoldenVector.cs` is
missing. Production source remains unchanged.

### 4.3 Minimal Golden and Unity consumer

- [ ] Create the pure-C# Golden vector without NUnit, UnityEngine,
UnityEditor, Protocol, network, file/time/environment/random input, or
expected constants.

- [ ] Create Unity consumer assertions for:

```text
UnityClientPredictionGoldenTests.UnityExecutesCorrectPredictionGolden
UnityClientPredictionGoldenTests.UnityExecutesDirtyRollbackGolden
```

The Unity consumer independently owns the same expected literals as the .NET
consumer; neither asks the actual-only vector to validate itself.

- [ ] Create the Editor asmdef exactly:

```json
{
  "name": "LockstepArena.Client.Prediction.Editor.Tests",
  "references": [
    "LockstepArena.Client.Prediction",
    "LockstepArena.Simulation"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false,
  "optionalUnityReferences": ["TestAssemblies"]
}
```

- [ ] Import with Unity only after all package source exists. Track only the
new package `.meta` files and this exact lock entry:

```json
"com.locksteparena.client-prediction": {
  "version": "file:com.locksteparena.client-prediction",
  "depth": 0,
  "source": "embedded",
  "dependencies": {
    "com.locksteparena.simulation": "0.1.0"
  }
}
```

Reject/restore every other lockfile change. `Packages/manifest.json` remains
unchanged.

### 4.4 GREEN, Unity XML, audit, commit

- [ ] Run .NET Gate 12 and require exactly:

```text
RESULT 36/36 passed
```

- [ ] Delete stale Gate 12 XML, run Unity 6000.3.10f1 EditMode with assembly
filter `LockstepArena.Client.Prediction.Editor.Tests`, using the established
`Start-Process -Wait` form without `-quit`. Parse fresh XML and require:

```text
total=2 passed=2 failed=0
UnityClientPredictionGoldenTests.UnityExecutesCorrectPredictionGolden Passed
UnityClientPredictionGoldenTests.UnityExecutesDirtyRollbackGolden Passed
```

Process exit code alone is not PASS. After Unity, inspect exact
Assets/ProjectSettings diffs and restore only individually confirmed
Unity-generated worktree-local paths; never broad reset/clean.

- [ ] Audit exactly one physical `Gate12PredictionGoldenVector.cs`, no
expected Digest inside it, no prohibited dependency, no duplicate Simulation
source, and only the approved lockfile entry.

- [ ] Commit:

```powershell
git add Packages/com.locksteparena.client-prediction Tests/LockstepArena.Client.Prediction.Tests Packages/packages-lock.json
git commit -m "test: add client prediction golden vectors"
```

---

## Task 5: Fresh final verification, evidence, push, and STOP

**Files:**

- Modify: `Docs/Architecture/GATE12_OFFLINE_BOUNDED_CLIENT_PREDICTION.md`

**Interfaces:**

- Consumes: committed Tasks 1-4.
- Produces: fresh reproducible evidence and the final Gate 12 handoff only.

### 5.1 Restore-assets preflight

- [ ] Resolve assets for these exact 20 projects. Restore only a missing
asset under the existing project contract. If any restore occurs, restart the
complete build matrix at build 1.

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
 'Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj',
 'Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj',
 'Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj',
 'Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj',
 'Packages/com.locksteparena.client-prediction/Runtime/LockstepArena.Client.Prediction.csproj',
 'Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj'
)
$restoreOccurred = $false
foreach ($project in $projects) {
  $output = & dotnet msbuild $project -nologo -verbosity:quiet -getProperty:ProjectAssetsFile
  if ($LASTEXITCODE -ne 0) { throw "Could not resolve assets: $project" }
  $asset = ($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -Last 1).Trim()
  if (-not [IO.Path]::IsPathRooted($asset)) {
    $asset = Join-Path (Split-Path -Parent $project) $asset
  }
  if (-not (Test-Path -LiteralPath $asset)) {
    dotnet restore $project --nologo
    if ($LASTEXITCODE -ne 0) { throw "Restore failed: $project" }
    $restoreOccurred = $true
  }
}
```

### 5.2 Exact 20 Release builds

- [ ] Run sequentially with `--no-restore`; each must report zero warnings and
zero errors:

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
dotnet build Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj -c Release --no-restore --nologo
dotnet build Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj -c Release --no-restore --nologo
dotnet build Packages/com.locksteparena.client-prediction/Runtime/LockstepArena.Client.Prediction.csproj -c Release --no-restore --nologo
dotnet build Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release --no-restore --nologo
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
dotnet run --project Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release --no-build
```

Require, in order:

```text
Gate 3  38/38
Gate 4  32/32
Gate 5  35/35
Gate 6  24/24
Gate 7  32/32
Server Golden 89A7DD66F8D9E871
Gate 9  27/27
Gate 10 27/27
Gate 12 36/36
```

- [ ] Run Gate 8 under its existing external 30-second watchdog:

```powershell
$gate8Out = '.artifacts/gate12-gate8.stdout.txt'
$gate8Err = '.artifacts/gate12-gate8.stderr.txt'
$gate8Args = @('run','--project','Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj','-c','Release','--no-build')
$gate8 = Start-Process dotnet -ArgumentList $gate8Args -PassThru -WindowStyle Hidden `
  -RedirectStandardOutput $gate8Out -RedirectStandardError $gate8Err
if (-not $gate8.WaitForExit(30000)) {
  & taskkill.exe /PID $gate8.Id /T /F | Out-Null
  throw 'Gate 8 exceeded the external 30-second test bound.'
}
$gate8.WaitForExit()
if ($gate8.ExitCode -ne 0) { throw "Gate 8 exited $($gate8.ExitCode)." }
Get-Content -Raw $gate8Out
```

Require `RESULT 8/8 passed`.

- [ ] Run Gate 11 under the existing external 60-second watchdog:

```powershell
$gate11Out = '.artifacts/gate12-gate11.stdout.txt'
$gate11Err = '.artifacts/gate12-gate11.stderr.txt'
$gate11Args = @('run','--project','Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj','-c','Release','--no-build')
$gate11 = Start-Process dotnet -ArgumentList $gate11Args -PassThru -WindowStyle Hidden `
  -RedirectStandardOutput $gate11Out -RedirectStandardError $gate11Err
if (-not $gate11.WaitForExit(60000)) {
  & taskkill.exe /PID $gate11.Id /T /F | Out-Null
  throw 'Gate 11 exceeded the external 60-second test bound.'
}
$gate11.WaitForExit()
if ($gate11.ExitCode -ne 0) { throw "Gate 11 exited $($gate11.ExitCode)." }
Get-Content -Raw $gate11Out
```

Require:

```text
RESULT 32/32 passed
authority Tick100,101,102
Server/Client State Tick103
Digest 386C4BB11A7EB7E0
```

The watchdog is verification tooling only and adds no product timeout.

### 5.4 Pinned Protocol regeneration

- [ ] Require no protoc override, build the pinned CodeGen project, require
exactly one tracked generated `.g.cs`, and require Schema/Generated diff clean:

```powershell
if ($env:PROTOBUF_PROTOC) { throw 'PROTOBUF_PROTOC override is not allowed.' }
if ($env:Protobuf_ProtocFullPath) { throw 'Protobuf_ProtocFullPath override is not allowed.' }
dotnet build Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj -c Release --no-restore --nologo
$generated = @(git ls-files 'Packages/com.locksteparena.protocol/Runtime/Generated/*.g.cs')
if ($generated.Length -ne 1 -or $generated[0] -ne 'Packages/com.locksteparena.protocol/Runtime/Generated/LockstepArenaProtocol.g.cs') {
  throw 'Unexpected generated Protocol source set.'
}
git diff --exit-code -- Packages/com.locksteparena.protocol/Schema Packages/com.locksteparena.protocol/Runtime/Generated
```

### 5.5 Fresh Unity 6000.3.10f1 regressions

- [ ] Run four independent EditMode commands using `Start-Process -Wait`
without `-quit`. Delete each prior result XML first. Keep frozen project path,
assembly filter, Gate 3 test filter, XML path, and log path. Parse fresh NUnit
XML; exit code alone is never PASS.

```text
LockstepArena.Client.Prediction.Editor.Tests
  exact total=2 passed=2 failed=0
  UnityClientPredictionGoldenTests.UnityExecutesCorrectPredictionGolden Passed
  UnityClientPredictionGoldenTests.UnityExecutesDirtyRollbackGolden Passed

LockstepArena.StreamFraming.Editor.Tests
  exact total=1 passed=1 failed=0
  UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden Passed

LockstepArena.Protocol.Editor.Tests
  exact total=2 passed=2 failed=0
  GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads Passed
  UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector Passed

LockstepArena.Simulation.Editor.Tests
  filter UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector
  total>=1 failed=0 required named test Passed
```

After every Unity run, inspect exact worktree-local Assets/ProjectSettings
diff. Restore only individually confirmed Unity-generated paths. Never broad
reset/clean and never operate on the ordinary checkout user files.

- [ ] Use these exact argument sets and fresh output paths:

```powershell
$unity = 'E:\unityhub\unity6.3\Editor\Unity.exe'
$project = 'E:\unityproject\LockstepArena\.worktrees\gate12-client-prediction'
$out = Join-Path $project '.artifacts\gate12-unity'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$runs = @(
  @{
    Name='gate12'; Assembly='LockstepArena.Client.Prediction.Editor.Tests'; Filter=$null;
    Xml=(Join-Path $out 'gate12-results.xml'); Log=(Join-Path $out 'gate12.log');
    Total=2; Required=@(
      'UnityClientPredictionGoldenTests.UnityExecutesCorrectPredictionGolden',
      'UnityClientPredictionGoldenTests.UnityExecutesDirtyRollbackGolden')
  },
  @{
    Name='gate7'; Assembly='LockstepArena.StreamFraming.Editor.Tests'; Filter=$null;
    Xml=(Join-Path $out 'gate7-results.xml'); Log=(Join-Path $out 'gate7.log');
    Total=1; Required=@(
      'UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden')
  },
  @{
    Name='gate5'; Assembly='LockstepArena.Protocol.Editor.Tests'; Filter=$null;
    Xml=(Join-Path $out 'gate5-results.xml'); Log=(Join-Path $out 'gate5.log');
    Total=2; Required=@(
      'GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads',
      'UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector')
  },
  @{
    Name='gate3'; Assembly='LockstepArena.Simulation.Editor.Tests';
    Filter='UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector';
    Xml=(Join-Path $out 'gate3-results.xml'); Log=(Join-Path $out 'gate3.log');
    Total=$null; Required=@(
      'UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector')
  }
)

foreach ($run in $runs) {
  Remove-Item -LiteralPath $run.Xml -Force -ErrorAction SilentlyContinue
  $arguments = @(
    '-batchmode','-nographics',
    '-projectPath',$project,
    '-runTests','-testPlatform','EditMode',
    '-assemblyNames',$run.Assembly
  )
  if ($run.Filter) { $arguments += @('-testFilter',$run.Filter) }
  $arguments += @('-testResults',$run.Xml,'-logFile',$run.Log)
  $process = Start-Process -FilePath $unity -ArgumentList $arguments `
    -PassThru -Wait -WindowStyle Hidden
  if (-not (Test-Path -LiteralPath $run.Xml)) {
    throw "$($run.Name) did not create fresh NUnit XML."
  }
  [xml]$xml = Get-Content -Raw -LiteralPath $run.Xml
  $testRun = $xml.'test-run'
  if ([int]$testRun.failed -ne 0) { throw "$($run.Name) has failed tests." }
  if ($run.Total -ne $null -and [int]$testRun.total -ne $run.Total) {
    throw "$($run.Name) discovered an unexpected test count."
  }
  if ($run.Total -eq $null -and [int]$testRun.total -lt 1) {
    throw "$($run.Name) discovered no tests."
  }
  $cases = @($xml.SelectNodes('//test-case'))
  foreach ($required in $run.Required) {
    $match = @($cases | Where-Object {
      $_.fullname -like "*$required" -or $_.name -eq ($required -split '\.')[-1]
    })
    if ($match.Count -ne 1 -or $match[0].result -ne 'Passed') {
      throw "$($run.Name) required test was not uniquely Passed: $required"
    }
  }
  git status --short -- Assets ProjectSettings
}
```

The command intentionally omits `-quit`, preserving the approved Unity
6000.3.10f1 execution correction. Any license, package, instance-lock, hang,
missing XML, or Test Runner failure stops verification; do not change filters
or project dependencies to work around it.

### 5.6 Final protected-path, dependency, artifact, and source audits

- [ ] Require protected committed diff zero from frozen Gate 11:

```powershell
$base = '03635526ffe53fcb384bf4b24887d34a942502bf'
git diff --exit-code $base -- Packages/com.locksteparena.simulation
git diff --exit-code $base -- Packages/com.locksteparena.protocol
git diff --exit-code $base -- Packages/com.locksteparena.stream-framing
git diff --exit-code $base -- Server/LockstepArena.Server.FrameSync
git diff --exit-code $base -- Server/LockstepArena.Server.ProtocolAuthority
git diff --exit-code $base -- Server/LockstepArena.Server.LiveTcp
git diff --exit-code $base -- Client/LockstepArena.Client.LiveTcp
git diff --exit-code $base -- Assets ProjectSettings Packages/manifest.json
git diff --exit-code $base -- Tests ':(exclude)Tests/LockstepArena.Client.Prediction.Tests/**'
```

- [ ] Require `.gitignore` diff exactly two approved lines and lockfile diff
exactly one approved embedded entry; investigate and restore any other change.

- [ ] Require production dependency/source scope:

```powershell
rg -n "Google\.Protobuf|LockstepArena\.Protocol|StreamFraming|LiveTcp|FrameSync|ProtocolAuthority|UnityEngine|UnityEditor|TcpClient|TcpListener|Socket|NetworkStream|InputDelay|Stopwatch|Timer|Task|Thread|async|Replay|KCP|UDP" Packages/com.locksteparena.client-prediction/Runtime
```

Expected: no matches except harmless words in XML/package metadata if any;
every match must be explained and production C# must contain none.

- [ ] Require one production `.cs`, one physical Golden vector, no copied
Simulation source, no sync/copy scripts, no symlink/junction, and no package
`bin`, `obj`, or DLL:

```powershell
$runtimeCs = @(git ls-files 'Packages/com.locksteparena.client-prediction/Runtime/*.cs')
if ($runtimeCs.Length -ne 1 -or $runtimeCs[0] -ne 'Packages/com.locksteparena.client-prediction/Runtime/ClientPredictionTimeline.cs') {
  throw 'Unexpected Prediction production source set.'
}
$goldens = @(git ls-files '*Gate12PredictionGoldenVector.cs')
if ($goldens.Length -ne 1 -or $goldens[0] -ne 'Packages/com.locksteparena.client-prediction/Tests/Editor/Gate12PredictionGoldenVector.cs') {
  throw 'Gate 12 Golden vector is not unique.'
}
if (Test-Path 'Packages/com.locksteparena.client-prediction/Runtime/bin') { throw 'Runtime/bin exists.' }
if (Test-Path 'Packages/com.locksteparena.client-prediction/Runtime/obj') { throw 'Runtime/obj exists.' }
$packageDll = @(Get-ChildItem 'Packages/com.locksteparena.client-prediction' -Recurse -File -Filter '*.dll')
if ($packageDll.Length -ne 0) { throw 'Prediction package contains a DLL.' }
```

- [ ] Scan for expected constants in production and actual-only Golden; require
no matches there:

```powershell
rg -n "D95809E1EB5CDDAA|8506E4507001B972|7D0D3A230618500F|A96B83267DD72A7D|386C4BB11A7EB7E0" Packages/com.locksteparena.client-prediction/Runtime Packages/com.locksteparena.client-prediction/Tests/Editor/Gate12PredictionGoldenVector.cs
```

- [ ] Confirm no forbidden framework/type, no modification to either Gate 11
pump, and ordinary checkout still has exactly the two protected modifications.

### 5.7 Evidence-only commit, push, handoff, STOP

- [ ] Append complete fresh evidence to the Architecture document: 20 builds,
all Gate 3-12 results, both Gate 12 Goldens/checkpoints, Gate 8/11 watchdogs,
Protocol regeneration, four Unity XML results, exact lock/ignore diffs,
dependency/source/artifact/protected-path audits, and ordinary checkout audit.
Do not alter architecture semantics.

- [ ] Require only the Architecture document changed, then commit:

```powershell
git add Docs/Architecture/GATE12_OFFLINE_BOUNDED_CLIENT_PREDICTION.md
git commit -m "docs: record Gate 12 implementation evidence"
```

- [ ] Push only the Gate 12 branch normally. If direct GitHub access fails,
use only the previously proven per-command option
`-c http.proxy=http://127.0.0.1:7897`; never change persistent Git config,
environment, remote, proxy, ancestry, or use force.

```powershell
git push origin codex/gate12-client-prediction
```

- [ ] Verify remote SHA equals local HEAD, worktree clean, base is ancestor,
and ordinary checkout preserved. Submit Gate 12 Final Implementation Handoff
with the complete commit chain and evidence, then STOP.

Do not begin Gate 13, live prediction integration, Replay, weak-network work,
or KCP reconsideration.
