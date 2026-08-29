# Gate 3 Variable-Roster Offline FrameSync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the temporary fixed P0/P1 model with an immutable variable-player roster and strict one-tick frame collection, then prove Unity and .NET independently reach the approved four-player state and digest.

**Architecture:** Keep every production type in the existing Unity-free `LockstepArena.Simulation` Runtime assembly. An immutable `ActiveRoster` maps continuous battle-local slots to opaque player identities; `StrictFrameCollector` verifies one submitted identity/input pair per slot and emits immutable canonical `FrameData`; `BattleSimulation` advances immutable variable-player state in explicit slot order and hashes the approved canonical byte stream.

**Tech Stack:** Unity 6000.3.10f1, Unity Test Framework 1.6.0, NUnit supplied only to the Unity Editor test assembly, C# 9 production source, .NET Standard 2.1 Shared assembly, .NET 8 dependency-free test and Server Verification executables, SDK-style MSBuild, PowerShell verification commands.

**Spec:** `Docs/Architecture/GATE3_VARIABLE_ROSTER_FRAME_SYNC.md`

## Global Constraints

- Begin implementation only after independent Planning PASS.
- Work only in `.worktrees/gate3-variable-roster-frame-sync` on branch `codex/gate3-variable-roster-frame-sync`.
- Preserve exact ancestry from `af86372ed598bd17dc0e42c9fc3571225ed050d0`.
- Keep one physical production source tree under `Packages/com.locksteparena.simulation/Runtime/` and one physical `Gate3GoldenVector.cs`.
- Keep Runtime `netstandard2.1`, C# 9, nullable enabled, warnings-as-errors, `noEngineReferences: true`, `autoReferenced: false`, and free of unsafe code and package/project dependencies.
- Preserve `Runtime/Directory.Build.props` and repository-root `.artifacts/` output isolation; do not create package `Runtime/bin`, `Runtime/obj`, or a Shared DLL artifact.
- `PlayerId` is opaque `ulong`; `PlayerSlot` is a non-negative `int`; roster validity supplies the upper slot bound.
- ActiveRoster, FrameData, and BattleState defensively copy caller collections and expose no mutable backing storage.
- Use explicit ascending slot loops for FrameData, Step, BattleState, and StateDigest; never rely on Dictionary/HashSet iteration.
- `StrictFrameCollector.Submit(PlayerId, InputFrame)` requires PlayerId, distinguishes accepted-incomplete `false` from accepted-complete `true`, and throws on rejection without mutation.
- Do not set a product maximum player count and do not introduce pooling, Span infrastructure, ECS, generic collection frameworks, interfaces, factories, DI, or events.
- Remove all production `Player0`, `Player1`, exactly-two-player, and slot-0/1 APIs without compatibility shims.
- Preserve `PlayerState.cs` byte-for-byte unless a compilation requirement proves a change necessary.
- Preserve all fifteen Gate 1 test intentions and add the approved 2/3/4-player, collector, twin, history, digest, and dual-runtime evidence.
- `Gate3GoldenVector.cs` contains actual execution only; expected final fields and `0x89A7DD66F8D9E871` exist separately in Unity and Server consumers.
- Do not modify package version, `Packages/manifest.json`, `Assets/`, or `ProjectSettings/`.
- Do not touch, clean, stage, copy, or commit the normal checkout's user-owned `Assets/Settings/Mobile_RPAsset.asset` and `ProjectSettings/ShaderGraphSettings.asset` changes.
- If Unity is locked, unlicensed, or otherwise unable to test this worktree, stop and report; never substitute the normal checkout.
- Do not add room, account, TCP/UDP/KCP, Protobuf, TickClock, input delay, missing-input policy, prediction, snapshot, rollback, replay, reconnect, view, or combat behavior.

---

### Task 1: Add Player Identity, Slot, and Immutable Active Roster

**Files:**

- Create: `Packages/com.locksteparena.simulation/Runtime/PlayerId.cs`
- Create: `Packages/com.locksteparena.simulation/Runtime/PlayerSlot.cs`
- Create: `Packages/com.locksteparena.simulation/Runtime/ActiveRoster.cs`
- Create: `Tests/LockstepArena.Simulation.Tests/ActiveRosterTests.cs`
- Modify: `Tests/LockstepArena.Simulation.Tests/Program.cs`

**Interfaces:**

- Consumes: only `System` and `System.Collections.Generic` from netstandard2.1.
- Produces: `PlayerId(ulong)`, `PlayerSlot(int)`, `ActiveRoster(IReadOnlyList<PlayerId>)`, `Count`, `GetPlayerId`, `TryGetSlot`, and `HasSameStructure` for all later tasks.

- [ ] **Step 1: Record the approved implementation baseline**

Run:

~~~powershell
git status --short --branch
git rev-parse HEAD
git merge-base --is-ancestor af86372ed598bd17dc0e42c9fc3571225ed050d0 HEAD
dotnet run --project Tests\LockstepArena.Simulation.Tests\LockstepArena.Simulation.Tests.csproj -c Release
dotnet run --project Server\LockstepArena.Server.Verification\LockstepArena.Server.Verification.csproj -c Release
~~~

Expected: clean branch at the approved planning commit descended from `af86372`, `RESULT 15/15 passed`, and `PASS Gate2GoldenVector Tick=1000 Digest=04633D1F8699DE68`.

- [ ] **Step 2: Write six ActiveRoster tests and register the group**

Create `ActiveRosterTests.All` with exactly these cases:

~~~csharp
new TestCase(nameof(ConstructorCopiesPlayerIds), ConstructorCopiesPlayerIds),
new TestCase(nameof(ConstructorRejectsEmptyRoster), ConstructorRejectsEmptyRoster),
new TestCase(nameof(ConstructorRejectsDuplicatePlayerIds), ConstructorRejectsDuplicatePlayerIds),
new TestCase(nameof(GetAndTryGetUseStableSlotMapping), GetAndTryGetUseStableSlotMapping),
new TestCase(nameof(TryGetSlotReportsMissingExplicitly), TryGetSlotReportsMissingExplicitly),
new TestCase(nameof(StructuralComparisonUsesOrderedPlayerIds), StructuralComparisonUsesOrderedPlayerIds),
~~~

Use non-sorted IDs `9003`, `42`, and `7000001`. Mutate the caller array after construction and verify Slot 0 still returns `9003`. Verify `GetPlayerId(new PlayerSlot(Count))` throws `ArgumentOutOfRangeException`. Verify `TryGetSlot` returns `false` for a missing ID; do not assert an out-value sentinel. Verify separately constructed equal-order rosters match and a swapped-order roster does not.

Register the group first in `Program.Main`:

~~~csharp
TestCase[] tests = Combine(
    ActiveRosterTests.All,
    ContractTests.All,
    BattleSimulationTests.All,
    DeterminismTests.All);
~~~

- [ ] **Step 3: Run the suite and confirm RED for missing roster types**

Run:

~~~powershell
dotnet run --project Tests\LockstepArena.Simulation.Tests\LockstepArena.Simulation.Tests.csproj -c Release
~~~

Expected: C# compilation fails because `PlayerId`, `PlayerSlot`, and `ActiveRoster` do not exist. An SDK, restore, or permission failure is not an acceptable RED.

- [ ] **Step 4: Add the two exact value-type contracts**

Implement both as readonly structs with direct value equality. The public shape must be:

~~~csharp
public readonly struct PlayerId : IEquatable<PlayerId>
{
    public PlayerId(ulong value);
    public ulong Value { get; }
    public bool Equals(PlayerId other);
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public static bool operator ==(PlayerId left, PlayerId right);
    public static bool operator !=(PlayerId left, PlayerId right);
}

public readonly struct PlayerSlot : IEquatable<PlayerSlot>
{
    public PlayerSlot(int value);
    public int Value { get; }
    public bool Equals(PlayerSlot other);
    public override bool Equals(object? obj);
    public override int GetHashCode();
    public static bool operator ==(PlayerSlot left, PlayerSlot right);
    public static bool operator !=(PlayerSlot left, PlayerSlot right);
}
~~~

`PlayerSlot(int value)` throws `ArgumentOutOfRangeException` when `value < 0`. Equality compares only the wrapped value. No conversion operators, parsing, formatting, sentinel, or ordering operators are added.

- [ ] **Step 5: Implement ActiveRoster with copies and linear scans**

Use one private `PlayerId[]`. Copy and detect duplicates with explicit loops:

~~~csharp
for (int index = 0; index < playerIdsInSlotOrder.Count; index++)
{
    PlayerId candidate = playerIdsInSlotOrder[index];
    for (int previous = 0; previous < index; previous++)
    {
        if (_playerIds[previous] == candidate)
        {
            throw new ArgumentException("Active roster cannot contain duplicate PlayerIds.", nameof(playerIdsInSlotOrder));
        }
    }

    _playerIds[index] = candidate;
}
~~~

Implement slot access through one private range check. Implement `TryGetSlot` with a forward loop and `slot = default` only on the `false` path; callers use the boolean rather than an invalid slot value. Implement `HasSameStructure` with Count plus ascending-slot ID comparison. Do not expose the array or implement a collection interface.

- [ ] **Step 6: Run Runtime build and all 21 tests**

Run:

~~~powershell
dotnet build Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj -c Release
dotnet run --project Tests\LockstepArena.Simulation.Tests\LockstepArena.Simulation.Tests.csproj -c Release
~~~

Expected: build has 0 warnings/0 errors and the runner ends `RESULT 21/21 passed`.

- [ ] **Step 7: Audit the first boundary and commit**

Run:

~~~powershell
rg -n "Dictionary|HashSet|IEnumerable|Player0|Player1" Packages\com.locksteparena.simulation\Runtime\ActiveRoster.cs Packages\com.locksteparena.simulation\Runtime\PlayerId.cs Packages\com.locksteparena.simulation\Runtime\PlayerSlot.cs
git diff --check
git status --short
~~~

Expected: no collection/cache or fixed-player match, no whitespace error, and only the five Task 1 files changed.

Commit:

~~~powershell
git add -- Packages/com.locksteparena.simulation/Runtime/PlayerId.cs Packages/com.locksteparena.simulation/Runtime/PlayerSlot.cs Packages/com.locksteparena.simulation/Runtime/ActiveRoster.cs Tests/LockstepArena.Simulation.Tests/ActiveRosterTests.cs Tests/LockstepArena.Simulation.Tests/Program.cs
git commit -m "feat: add immutable active roster"
~~~

### Task 2: Perform the Atomic Variable-Roster Runtime and Consumer Migration

This is one coherent breaking checkpoint. Runtime, .NET tests, shared Golden Vector, Server assertions, and Unity assertion source change together so no compatibility shim or committed stale consumer remains.

**Files:**

- Create: `Packages/com.locksteparena.simulation/Runtime/StrictFrameCollector.cs`
- Create: `Tests/LockstepArena.Simulation.Tests/FrameCollectionTests.cs`
- Modify: `Packages/com.locksteparena.simulation/Runtime/InputFrame.cs`
- Replace: `Packages/com.locksteparena.simulation/Runtime/FrameData.cs`
- Replace: `Packages/com.locksteparena.simulation/Runtime/BattleState.cs`
- Modify: `Packages/com.locksteparena.simulation/Runtime/BattleSimulation.cs`
- Modify: `Packages/com.locksteparena.simulation/Runtime/StateDigest.cs`
- Modify: `Packages/com.locksteparena.simulation/Runtime/SimulationConfig.cs`
- Preserve: `Packages/com.locksteparena.simulation/Runtime/PlayerState.cs`
- Modify: `Tests/LockstepArena.Simulation.Tests/ContractTests.cs`
- Modify: `Tests/LockstepArena.Simulation.Tests/BattleSimulationTests.cs`
- Modify: `Tests/LockstepArena.Simulation.Tests/DeterminismTests.cs`
- Modify: `Tests/LockstepArena.Simulation.Tests/Program.cs`
- Rename: `Packages/com.locksteparena.simulation/Tests/Editor/Gate2GoldenVector.cs` -> `Packages/com.locksteparena.simulation/Tests/Editor/Gate3GoldenVector.cs`
- Rename: `Packages/com.locksteparena.simulation/Tests/Editor/Gate2GoldenVector.cs.meta` -> `Packages/com.locksteparena.simulation/Tests/Editor/Gate3GoldenVector.cs.meta`
- Modify: `Packages/com.locksteparena.simulation/Tests/Editor/UnityGoldenVectorTests.cs`
- Modify: `Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj`
- Modify: `Server/LockstepArena.Server.Verification/Program.cs`

**Interfaces:**

- Consumes: Task 1 PlayerId, PlayerSlot, and ActiveRoster contracts.
- Produces: immutable variable-player FrameData/BattleState, atomic Step, canonical roster-aware digest, mandatory-identity StrictFrameCollector, `Gate3GoldenVector.Run()`, and independently asserting Unity/Server consumers.

- [ ] **Step 1: Record the unchanged PlayerState blob before edits**

Run:

~~~powershell
$approvedPlayerStateBlob = git rev-parse 'af86372ed598bd17dc0e42c9fc3571225ed050d0:Packages/com.locksteparena.simulation/Runtime/PlayerState.cs'
Write-Output "Approved PlayerState blob=$approvedPlayerStateBlob"
~~~

Keep the printed value in the task evidence; the pre-commit check below recomputes it directly from the approved base so it does not depend on shell-variable persistence.

- [ ] **Step 2: Replace the test contracts first**

Register `FrameCollectionTests.All` between Contract and BattleSimulation groups. The final runner contains 38 cases in these groups:

~~~text
ActiveRosterTests       6
ContractTests          10
FrameCollectionTests    8
BattleSimulationTests   8
DeterminismTests        6
Total                  38
~~~

Use exactly these ContractTests responsibilities:

~~~text
TwoPlayerFrameCanonicalizesArrivalOrder
ThreePlayerFrameCanonicalizesArrivalOrder
FourPlayerFrameCanonicalizesArrivalOrder
FrameDataRejectsMissingSlot
FrameDataRejectsDuplicateSlot
FrameDataRejectsUnknownSlot
FrameDataRejectsWrongInputTick
FrameDataCopiesReceivedInputs
InputFrameRejectsInvalidMovementAndNegativeSlot
InitialStateCopiesStatesAndChecksSlotRange
~~~

Use exactly these FrameCollectionTests responsibilities:

~~~text
SubmitReturnsFalseUntilLastAcceptedInput
UnknownPlayerIdIsRejectedWithoutPollution
PlayerIdSlotMismatchIsRejectedWithoutPollution
WrongTickIsRejectedWithoutPollution
UnknownSlotIsRejectedWithoutPollution
DuplicateSlotIsRejectedWithoutPollution
IncompleteCollectorCannotReturnFrame
CompleteCollectorRejectsFurtherSubmit
~~~

Use exactly these BattleSimulationTests responsibilities:

~~~text
NeutralInputsAdvanceOneTickWithoutMovement
FourPlayerMovementUpdatesInSlotOrder
InputAimReplacesEveryPlayerAim
MovementClampsAtEveryArenaBoundary
UnexpectedFrameTickIsRejectedWithoutMutation
StructurallyEqualRosterInstanceIsAccepted
DifferentRosterIsRejectedWithoutMutation
StepUsesCopiedInitialPlayerStates
~~~

Use exactly these DeterminismTests responsibilities:

~~~text
EqualVariablePlayerStatesHaveEqualDigests
CanonicalStateChangesAlterDigest
RosterIdentityAndOrderChangesAlterDigest
GoldenDigestLocksVariableRosterFieldAndByteOrder
FourPlayerTwinSimulationsMatchDigestAtEveryTick
InitialStateAndThreePlayerFrameHistoryRebuildFinalDigest
~~~

Keep the original 10,000 twin ticks and 2,000 history ticks. Build test rosters from explicit arrays and use different input permutations for the twin instances.

- [ ] **Step 3: Change both consumer assertions to the approved Gate 3 API before Runtime implementation**

Use `git mv` for the Golden Vector source and its meta file. Change the Server csproj linked item to:

~~~xml
<Compile Include="..\..\Packages\com.locksteparena.simulation\Tests\Editor\Gate3GoldenVector.cs"
         Link="Gate3GoldenVector.cs" />
~~~

Change Unity and Server to reference `Gate3GoldenVectorResult` and `Gate3GoldenVector`. Each consumer independently declares and checks these literals:

~~~text
Tick 1000
Count 4
Slot 0: PlayerId 0x0102030405060708, X 0,     Z -3000, Aim 13086
Slot 1: PlayerId 0x000000000000002A, X 0,     Z  3000, Aim  8699
Slot 2: PlayerId 0xFFEEDDCCBBAA0099, X -2500, Z -2000, Aim 51320
Slot 3: PlayerId 0x00000000000F4243, X 2500,  Z  2000, Aim 62539
Digest 0x89A7DD66F8D9E871
~~~

The Server keeps returning nonzero when any comparison fails. Unity keeps asserting `typeof(BattleSimulation).Assembly.GetName().Name == "LockstepArena.Simulation"`.

- [ ] **Step 4: Run the .NET test and Server projects and confirm RED**

Run:

~~~powershell
dotnet run --project Tests\LockstepArena.Simulation.Tests\LockstepArena.Simulation.Tests.csproj -c Release
dotnet run --project Server\LockstepArena.Server.Verification\LockstepArena.Server.Verification.csproj -c Release
~~~

Expected: compile failures identify the not-yet-implemented variable-roster APIs and Gate3 vector. Restore, SDK, or permission failures are not acceptable RED evidence.

- [ ] **Step 5: Migrate InputFrame and implement immutable FrameData**

Change the InputFrame constructor and property to strong slots:

~~~csharp
public InputFrame(uint tick, PlayerSlot playerSlot, sbyte moveX, sbyte moveZ, ushort aim)
public PlayerSlot PlayerSlot { get; }
~~~

Keep only the existing movement validation. Remove the `playerSlot > 1` check.

Implement FrameData as a sealed class with one private InputFrame array. Its factory signature is:

~~~csharp
public static FrameData Create(
    ActiveRoster roster,
    uint tick,
    IReadOnlyList<InputFrame> receivedInputs)
~~~

Inside the factory, reject nulls and count mismatch, allocate `InputFrame[roster.Count]` and `bool[roster.Count]`, validate tick/range/duplicate for every received value, verify every presence flag, then construct the frame from only those fresh local arrays. `GetInput` checks `slot.Value >= Roster.Count` and throws `ArgumentOutOfRangeException` before indexing.

- [ ] **Step 6: Implement immutable BattleState and atomic BattleSimulation**

BattleState stores one private copied PlayerState array and exposes:

~~~csharp
public BattleState(
    uint tick,
    ActiveRoster roster,
    IReadOnlyList<PlayerState> statesInSlotOrder)
public static BattleState CreateInitial(
    ActiveRoster roster,
    IReadOnlyList<PlayerState> statesInSlotOrder)
public uint Tick { get; }
public ActiveRoster Roster { get; }
public int PlayerCount { get; }
public PlayerState GetPlayerState(PlayerSlot slot)
~~~

Require state count equal to roster count and range-check every public slot read.

Implement Step with assignment last:

~~~csharp
BattleState current = State;
if (frame.Tick != current.Tick)
{
    throw new ArgumentException("Frame tick must match the current simulation tick.", nameof(frame));
}

if (!current.Roster.HasSameStructure(frame.Roster))
{
    throw new ArgumentException("Frame roster must match the simulation roster.", nameof(frame));
}

PlayerState[] nextPlayers = new PlayerState[current.PlayerCount];
for (int index = 0; index < nextPlayers.Length; index++)
{
    PlayerSlot slot = new PlayerSlot(index);
    nextPlayers[index] = Move(current.GetPlayerState(slot), frame.GetInput(slot));
}

uint nextTick = checked(current.Tick + 1);
BattleState nextState = new BattleState(nextTick, current.Roster, nextPlayers);
State = nextState;
~~~

Validate a null frame before this block. Remove fixed spawn constants from SimulationConfig; retain tick rate, integer scale, movement, and arena bounds unchanged.

- [ ] **Step 7: Implement the approved canonical StateDigest**

Keep the current FNV constants and little-endian byte helpers. The Compute body is exactly this field sequence:

~~~csharp
ulong hash = OffsetBasis;
AddUInt32(ref hash, state.Tick);
AddUInt32(ref hash, checked((uint)state.PlayerCount));

for (int index = 0; index < state.PlayerCount; index++)
{
    PlayerSlot slot = new PlayerSlot(index);
    AddUInt64(ref hash, state.Roster.GetPlayerId(slot).Value);
    PlayerState player = state.GetPlayerState(slot);
    AddInt32(ref hash, player.PositionX);
    AddInt32(ref hash, player.PositionZ);
    AddUInt16(ref hash, player.Aim);
}

return hash;
~~~

Add `AddUInt64` by emitting eight low-to-high bytes with shifts `0, 8, 16, 24, 32, 40, 48, 56`. Do not use BitConverter, serialization, reflection, memory layout, or `GetHashCode`.

- [ ] **Step 8: Implement transactional StrictFrameCollector**

Use these fields only:

~~~csharp
private readonly ActiveRoster _roster;
private readonly uint _targetTick;
private readonly InputFrame[] _inputs;
private readonly bool[] _present;
private int _acceptedCount;
private FrameData? _completedFrame;
~~~

Validate Complete state, tick, roster slot, PlayerId existence, identity/slot equality, and duplicate presence in the approved order. For a non-final valid input, commit it and return `false`.

For the final valid input, preserve transactionality:

~~~csharp
InputFrame[] candidate = (InputFrame[])_inputs.Clone();
candidate[slot.Value] = input;
FrameData completed = FrameData.Create(_roster, _targetTick, candidate);

_inputs[slot.Value] = input;
_present[slot.Value] = true;
_acceptedCount++;
_completedFrame = completed;
return true;
~~~

`GetCompletedFrame` throws `InvalidOperationException` while `_completedFrame` is null and otherwise returns the cached immutable frame. Add no reset, scheduling, timeout, history, or pool API.

- [ ] **Step 9: Implement the pure Gate3GoldenVector actual execution**

The file remains free of NUnit, UnityEngine, UnityEditor, I/O, environment, time, random, serialization, and expected literals. It exposes:

~~~csharp
public readonly struct Gate3GoldenVectorResult
{
    public Gate3GoldenVectorResult(BattleState state, ulong digest);
    public BattleState State { get; }
    public ulong Digest { get; }
}

public static class Gate3GoldenVector
{
    public const uint TickCount = 1_000;
    public static Gate3GoldenVectorResult Run();
}
~~~

`Run` creates two separate rosters with ordered IDs:

~~~csharp
new PlayerId(0x0102030405060708UL),
new PlayerId(0x000000000000002AUL),
new PlayerId(0xFFEEDDCCBBAA0099UL),
new PlayerId(0x00000000000F4243UL)
~~~

Create initial states `(-1000,0,0)`, `(1000,0,0)`, `(0,-1000,0)`, `(0,1000,0)`. For every tick, generate the approved five-phase movements and four aim formulas, create one collector using the second structurally equal roster, and Submit in these rotating slot orders:

~~~csharp
private static readonly int[][] SubmissionOrders =
{
    new[] { 2, 0, 3, 1 },
    new[] { 1, 3, 0, 2 },
    new[] { 3, 2, 1, 0 },
    new[] { 0, 2, 1, 3 },
};
~~~

Use the roster PlayerId for each submitted slot. Require the fourth accepted input to return `true`, retrieve the completed frame, Step, and finally return actual State plus `StateDigest.Compute`. Do not place `13086`, `8699`, `51320`, `62539`, or `0x89A7DD66F8D9E871` in this file.

- [ ] **Step 10: Run the complete .NET GREEN checks**

Run:

~~~powershell
dotnet build Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj -c Release
dotnet run --project Tests\LockstepArena.Simulation.Tests\LockstepArena.Simulation.Tests.csproj -c Release
dotnet run --project Server\LockstepArena.Server.Verification\LockstepArena.Server.Verification.csproj -c Release
~~~

Expected: Runtime build has 0 warnings/0 errors, test runner ends `RESULT 38/38 passed`, and Server prints:

~~~text
PASS Gate3GoldenVector Tick=1000 Players=4 Digest=89A7DD66F8D9E871
~~~

- [ ] **Step 11: Run focused immutability, fixed-coupling, and purity audits**

Run:

~~~powershell
$approvedPlayerStateBlob = git rev-parse 'af86372ed598bd17dc0e42c9fc3571225ed050d0:Packages/com.locksteparena.simulation/Runtime/PlayerState.cs'
if ((git hash-object Packages\com.locksteparena.simulation\Runtime\PlayerState.cs) -ne $approvedPlayerStateBlob) { throw 'PlayerState.cs changed without a compilation requirement.' }
$fixed = rg -n "Player0|Player1|exactly two|exactly 2|slot must be 0 or 1|Player slot must be 0 or 1" Packages\com.locksteparena.simulation\Runtime
if ($LASTEXITCODE -eq 0) { $fixed; throw 'Fixed-two-player production coupling remains.' }
if ($LASTEXITCODE -gt 1) { throw 'Fixed-coupling audit failed to run.' }
$vectorForbidden = rg -n "NUnit|UnityEngine|UnityEditor|System\.IO|Environment|DateTime|Random|89A7DD66F8D9E871|13_086|8_699|51_320|62_539" Packages\com.locksteparena.simulation\Tests\Editor\Gate3GoldenVector.cs
if ($LASTEXITCODE -eq 0) { $vectorForbidden; throw 'Golden Vector contains a forbidden dependency or expected literal.' }
if ($LASTEXITCODE -gt 1) { throw 'Golden Vector purity audit failed to run.' }
git diff --check
~~~

Expected: PlayerState blob unchanged, both scans have no match, and no whitespace error.

- [ ] **Step 12: Commit the coherent breaking migration**

Review `git status --short` and stage only the listed Runtime, Tests, package test, and Server paths. Commit:

~~~powershell
git add -- Packages/com.locksteparena.simulation/Runtime Packages/com.locksteparena.simulation/Tests/Editor Tests/LockstepArena.Simulation.Tests Server/LockstepArena.Server.Verification
git commit -m "feat: add variable-roster frame synchronization"
~~~

### Task 3: Execute the Four-Player Vector in Unity EditMode

**Files:**

- Verify: `Packages/com.locksteparena.simulation/Tests/Editor/UnityGoldenVectorTests.cs`
- Inspect after import: package `.meta` files and `Packages/packages-lock.json`
- Reject: every diff under `Assets/`, `ProjectSettings/`, and `Packages/manifest.json`

**Interfaces:**

- Consumes: Task 2 Runtime assembly and the one physical `Gate3GoldenVector.cs`.
- Produces: fresh NUnit XML containing a passed `UnityExecutesApprovedGoldenVector` test with independent four-player assertions.

- [ ] **Step 1: Confirm Unity preconditions without changing processes**

Run:

~~~powershell
$unity = 'E:\unityhub\unity6.3\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $unity)) { throw "Unity executable not found: $unity" }
if (Get-Process -Name Unity -ErrorAction SilentlyContinue) { throw 'A Unity instance is active; stop and report the worktree lock risk.' }
~~~

Do not terminate a Unity process automatically.

- [ ] **Step 2: Run only the Shared Simulation Editor test assembly**

Run from the Gate 3 worktree:

~~~powershell
$projectPath = (Get-Location).ProviderPath
$results = Join-Path $env:TEMP 'locksteparena-gate3-editmode-results.xml'
$editorLog = Join-Path $env:TEMP 'locksteparena-gate3-editor.log'
Remove-Item -LiteralPath $results -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $editorLog -Force -ErrorAction SilentlyContinue
& $unity -batchmode -runTests -projectPath $projectPath -testPlatform EditMode -assemblyNames LockstepArena.Simulation.Editor.Tests -testResults $results -logFile $editorLog
$unityExit = $LASTEXITCODE
if ($unityExit -ne 0) {
    Get-Content -LiteralPath $editorLog -Tail 200
    throw "Unity EditMode run failed with exit code $unityExit"
}
if (-not (Test-Path -LiteralPath $results)) {
    Get-Content -LiteralPath $editorLog -Tail 200
    throw 'Unity did not create NUnit XML.'
}
~~~

Do not add `-quit`; the Unity test runner exits after completion.

- [ ] **Step 3: Parse XML and prove the named Gate 3 test ran**

Run:

~~~powershell
[xml]$testXml = Get-Content -LiteralPath $results -Raw
$run = $testXml.'test-run'
if ($null -eq $run) { throw 'NUnit XML has no test-run root.' }
if ([int]$run.total -lt 1) { throw 'Unity discovered zero tests.' }
if ([int]$run.failed -ne 0) { throw "Unity reported $($run.failed) failed tests." }
$case = $testXml.SelectSingleNode("//test-case[contains(@fullname, 'UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector')]")
if ($null -eq $case) { throw 'Expected Gate 3 Unity test is absent.' }
if ($case.result -ne 'Passed') { throw "Expected Gate 3 Unity test result was $($case.result)." }
Write-Output "Unity Gate 3 XML: total=$($run.total) passed=$($run.passed) failed=$($run.failed) test=$($case.fullname)"
~~~

Expected: total at least 1, failed 0, and the named test Passed.

- [ ] **Step 4: Reject unrelated Unity serialization changes**

Run:

~~~powershell
git status --short
git diff -- Packages/manifest.json Assets ProjectSettings
~~~

Expected: no intentional Gate 3 diff in those paths. If Unity rewrites only known worktree-local render settings, inspect the exact diff and restore those exact files to the worktree HEAD; do not use broad reset/clean commands. Accept `Packages/packages-lock.json` only if its existing embedded-package record changes solely as a Unity serialization no-op; otherwise stop and investigate.

- [ ] **Step 5: Repeat .NET consumers after Unity import**

Run:

~~~powershell
dotnet run --project Tests\LockstepArena.Simulation.Tests\LockstepArena.Simulation.Tests.csproj -c Release
dotnet run --project Server\LockstepArena.Server.Verification\LockstepArena.Server.Verification.csproj -c Release
~~~

Expected: `RESULT 38/38 passed` and the exact Gate 3 Server PASS line.

- [ ] **Step 6: Commit only necessary Unity package metadata if Unity created it**

If no tracked package metadata changed, make no commit for this task. If Unity created required `.meta` changes caused by the Gate3 source rename, stage only those package meta paths and commit:

~~~powershell
git add -- Packages/com.locksteparena.simulation
git commit -m "test: verify variable roster in Unity EditMode"
~~~

Never stage Assets, ProjectSettings, manifest, Library, logs, XML results, or temporary files.

### Task 4: Run the Full Gate 3 Audit and Record Fresh Evidence

**Files:**

- Modify: `Docs/Architecture/GATE3_VARIABLE_ROSTER_FRAME_SYNC.md`

**Interfaces:**

- Consumes: complete Gate 3 Runtime, .NET tests, Server Verification, Unity XML, and Git tree.
- Produces: a committed evidence section and no new product capability.

- [ ] **Step 1: Build every .NET deliverable in Release**

Run:

~~~powershell
dotnet build Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj -c Release
dotnet build Tests\LockstepArena.Simulation.Tests\LockstepArena.Simulation.Tests.csproj -c Release
dotnet build Server\LockstepArena.Server.Verification\LockstepArena.Server.Verification.csproj -c Release
~~~

Expected: each build reports 0 warnings and 0 errors.

- [ ] **Step 2: Execute both .NET verification paths fresh**

Run:

~~~powershell
dotnet run --project Tests\LockstepArena.Simulation.Tests\LockstepArena.Simulation.Tests.csproj -c Release --no-restore
dotnet run --project Server\LockstepArena.Server.Verification\LockstepArena.Server.Verification.csproj -c Release --no-restore
~~~

Expected: `RESULT 38/38 passed` and `PASS Gate3GoldenVector Tick=1000 Players=4 Digest=89A7DD66F8D9E871`.

- [ ] **Step 3: Repeat Unity EditMode with fresh XML**

Repeat Task 3 Steps 1–3 after deleting the old XML and Editor log. Expected: target test present and Passed, failed 0. Reinspect and remove only Unity's unrelated worktree-local serialization changes before continuing.

- [ ] **Step 4: Prove source uniqueness and the intended Runtime file set**

Run:

~~~powershell
$runtimeRoot = 'Packages/com.locksteparena.simulation/Runtime/'
$runtimeNames = @(
    'ActiveRoster.cs',
    'BattleSimulation.cs',
    'BattleState.cs',
    'FrameData.cs',
    'InputFrame.cs',
    'PlayerId.cs',
    'PlayerSlot.cs',
    'PlayerState.cs',
    'SimulationConfig.cs',
    'StateDigest.cs',
    'StrictFrameCollector.cs'
)
foreach ($name in $runtimeNames) {
    $paths = @(git ls-files | Where-Object { $_ -like "*$name" })
    if ($paths.Count -ne 1) { throw "$name has $($paths.Count) tracked paths: $paths" }
    if (-not $paths[0].StartsWith($runtimeRoot, [StringComparison]::Ordinal)) { throw "$name is outside Runtime: $($paths[0])" }
    Write-Output "$name -> $($paths[0])"
}
$vectors = @(git ls-files | Where-Object { $_ -like '*Gate3GoldenVector.cs' })
if ($vectors.Count -ne 1) { throw "Gate3GoldenVector.cs has $($vectors.Count) tracked paths." }
if (git ls-files | Select-String 'Gate2GoldenVector.cs$') { throw 'Old Gate2GoldenVector.cs is still tracked.' }
~~~

- [ ] **Step 5: Prove Runtime remains Unity-free and scope-clean**

Run:

~~~powershell
$forbidden = rg -n -i "UnityEngine|UnityEditor|NUnit|TestFramework|PackageReference|ProjectReference|System\.Net|Socket|\bTcp\b|\bUdp\b|\bKcp\b|Protobuf|Room|TickClock|InputDelay|MissingInput|Prediction|Snapshot|Rollback|\bReplay\b|GameObject|Transform|Projectile|Combat|Damage|Health" Packages\com.locksteparena.simulation\Runtime
if ($LASTEXITCODE -eq 0) { $forbidden; throw 'Forbidden Runtime dependency or capability found.' }
if ($LASTEXITCODE -gt 1) { throw 'Runtime scope audit failed to run.' }
$fixed = rg -n "Player0|Player1|exactly two|exactly 2|must be 0 or 1|must contain two" Packages\com.locksteparena.simulation\Runtime
if ($LASTEXITCODE -eq 0) { $fixed; throw 'Fixed-two-player Runtime coupling found.' }
if ($LASTEXITCODE -gt 1) { throw 'Fixed-player audit failed to run.' }
Write-Output 'Runtime dependency/scope/fixed-player audit: PASS'
~~~

- [ ] **Step 6: Prove no alternate source, copy mechanism, link, or package artifact exists**

Run:

~~~powershell
$trackedLinks = git ls-files --stage Packages/com.locksteparena.simulation Server | Select-String '^120000 '
if ($trackedLinks) { $trackedLinks; throw 'Tracked symlink found.' }
$filesystemLinks = Get-ChildItem -LiteralPath 'Packages\com.locksteparena.simulation','Server\LockstepArena.Server.Verification' -Recurse -Force | Where-Object { $null -ne $_.LinkType }
if ($filesystemLinks) { $filesystemLinks; throw 'Filesystem link or junction found.' }
$dlls = Get-ChildItem -LiteralPath 'Packages\com.locksteparena.simulation' -Recurse -File -Filter '*.dll'
if ($dlls) { $dlls; throw 'DLL found inside embedded package.' }
if (Test-Path -LiteralPath 'Packages\com.locksteparena.simulation\Runtime\bin') { throw 'Runtime/bin exists.' }
if (Test-Path -LiteralPath 'Packages\com.locksteparena.simulation\Runtime\obj') { throw 'Runtime/obj exists.' }
$scripts = git diff --name-only af86372ed598bd17dc0e42c9fc3571225ed050d0..HEAD | Where-Object { $_ -match '\.(ps1|sh|cmd|bat|py)$' }
if ($scripts) { $scripts; throw 'Gate 3 added a script.' }
Write-Output 'source/link/artifact audit: PASS'
~~~

- [ ] **Step 7: Prove Golden Vector purity and separate expected assertions**

Run:

~~~powershell
$vector = 'Packages\com.locksteparena.simulation\Tests\Editor\Gate3GoldenVector.cs'
$forbiddenVector = rg -n "NUnit|UnityEngine|UnityEditor|System\.IO|Environment|DateTime|Random|89A7DD66F8D9E871|13_086|8_699|51_320|62_539" $vector
if ($LASTEXITCODE -eq 0) { $forbiddenVector; throw 'Golden Vector contains forbidden code or expected literals.' }
if ($LASTEXITCODE -gt 1) { throw 'Golden Vector audit failed to run.' }
rg -n "89A7DD66F8D9E871|13_086|8_699|51_320|62_539" Packages\com.locksteparena.simulation\Tests\Editor\UnityGoldenVectorTests.cs Server\LockstepArena.Server.Verification\Program.cs
~~~

Expected: no forbidden vector match; both consumer files contain their own expected literals.

- [ ] **Step 8: Prove project-scope isolation and clean diffs**

Run:

~~~powershell
$base = 'af86372ed598bd17dc0e42c9fc3571225ed050d0'
$prohibited = git diff --name-only "$base..HEAD" -- Assets ProjectSettings Packages/manifest.json
if ($prohibited) { $prohibited; throw 'Prohibited Unity project content changed.' }
git diff --check "$base..HEAD"
git status --short
~~~

Expected: no prohibited path, no whitespace error, and only the pending evidence document is modified.

- [ ] **Step 9: Record exact implementation evidence in the Gate 3 design**

Change the status to implementation complete pending independent Gate 3 approval and append an Implementation Evidence section containing:

- exact implementation commit IDs and approved base;
- three Release build warning/error counts;
- exact `38/38` test output;
- exact Server PASS line;
- Unity executable, arguments, XML total/passed/failed, and named test result;
- four-player final state and digest;
- source mappings and PlayerState unchanged-blob result;
- immutable/collector/fixed-player/dependency/purity/link/artifact/scope audit results;
- any Unity worktree-only serialization files restored after inspection;
- confirmation that the normal checkout was not used or changed.

- [ ] **Step 10: Commit evidence only**

Run:

~~~powershell
git add -- Docs/Architecture/GATE3_VARIABLE_ROSTER_FRAME_SYNC.md
git commit -m "docs: record Gate 3 deterministic roster evidence"
~~~

### Task 5: Push and Stop at the Gate

**Files:** None.

**Interfaces:**

- Consumes: a clean, fully verified Gate 3 branch.
- Produces: a remote branch and Gate 3 Handoff; no merge and no Gate 4 work.

- [ ] **Step 1: Verify final ancestry, status, and commit range**

Run:

~~~powershell
git status --short --branch
git log --oneline --decorate af86372ed598bd17dc0e42c9fc3571225ed050d0..HEAD
git merge-base --is-ancestor af86372ed598bd17dc0e42c9fc3571225ed050d0 HEAD
~~~

Expected: clean Gate 3 worktree and approved base is an ancestor.

- [ ] **Step 2: Inspect the normal checkout without changing it**

Run:

~~~powershell
git -C 'E:\unityproject\LockstepArena' status --short --branch
~~~

Report the two user-owned modifications as found. Do not restore, stage, clean, copy, or commit them.

- [ ] **Step 3: Push the exact branch and verify its remote SHA**

Run:

~~~powershell
git push --set-upstream origin codex/gate3-variable-roster-frame-sync
$local = git rev-parse HEAD
$remoteLine = git ls-remote origin refs/heads/codex/gate3-variable-roster-frame-sync
$remote = ($remoteLine -split '\s+')[0]
if ($local -ne $remote) { throw "Remote SHA $remote does not equal local SHA $local." }
Write-Output "Gate 3 remote verified: $remote"
~~~

- [ ] **Step 4: Submit the Gate 3 implementation Handoff and stop**

The Handoff contains:

- branch, final commit, planning commit, and approved base;
- Runtime types and breaking fixed-player API removal;
- complete test count and Gate 1 intent migration mapping;
- exact Server output and Unity NUnit XML evidence;
- approved roster, full final state, and digest;
- structural roster, atomic rejection, twin, and history evidence;
- source uniqueness, PlayerState blob, dependency, fixed-coupling, Golden purity, link/artifact, and scope audits;
- explicit untouched Assets, ProjectSettings, manifest, package version, output-isolation configuration, and normal-checkout user files;
- explicit confirmation that rooms, networking, Protobuf, timing, missing-input policy, prediction, snapshot, rollback, replay, view, combat, and Gate 4 work remain absent.

Do not merge and do not begin the next Gate before independent approval.
