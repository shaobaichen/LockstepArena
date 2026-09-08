# Gate 12: Offline Bounded Client Prediction & Authoritative Reconciliation

## Status and baseline

Architecture Direction and Section 1 are approved. This document is the
authoritative Gate 12 Planning specification. Implementation remains
unauthorized until the Planning commit receives independent PASS.

Frozen Gate 11 baseline:

```text
03635526ffe53fcb384bf4b24887d34a942502bf
```

Gate 12 is an offline client-domain Gate. It does not connect prediction to
the Gate 11 TCP pumps. That integration belongs to Gate 13.

## 1. Learning objective

Gate 12 proves that a client can:

```text
complete canonical predicted FrameData
-> advance a bounded predicted timeline
-> receive contiguous authoritative FrameData
-> classify clean versus Dirty by canonical input content
-> roll back to StateBefore(Dirty Tick)
-> replay later retained predictions with BattleSimulation.Step
-> converge to the authoritative deterministic state
```

The component never invents remote-player input. Its caller supplies one
complete `FrameData` for every predicted Tick.

## 2. Production ownership and dependency

Embedded package:

```text
Packages/com.locksteparena.client-prediction/
```

Assembly and namespace:

```text
LockstepArena.Client.Prediction
```

Dependency direction:

```text
LockstepArena.Client.Prediction
└── LockstepArena.Simulation
```

The production assembly must not depend on Protocol, Google.Protobuf,
StreamFraming, either LiveTcp assembly, FrameSync, ProtocolAuthority,
UnityEngine, or UnityEditor.

Exactly one production class is introduced:

```text
ClientPredictionTimeline
```

There is no interface, factory, DI container, event bus, generic rollback or
snapshot framework, network adapter, or alternate Simulation algorithm.

## 3. Exact authored layout

```text
Packages/com.locksteparena.client-prediction/
├── package.json
├── Runtime/
│   ├── Directory.Build.props
│   ├── LockstepArena.Client.Prediction.asmdef
│   ├── LockstepArena.Client.Prediction.csproj
│   └── ClientPredictionTimeline.cs
└── Tests/Editor/
    ├── Gate12PredictionGoldenVector.cs
    ├── UnityClientPredictionGoldenTests.cs
    └── LockstepArena.Client.Prediction.Editor.Tests.asmdef

Tests/LockstepArena.Client.Prediction.Tests/
├── LockstepArena.Client.Prediction.Tests.csproj
├── Program.cs
├── TestAssert.cs
└── ClientPredictionTimelineTests.cs
```

Unity `.meta` files for every new package directory and authored package
asset are tracked. No generated build output is tracked.

Package manifest:

```json
{
  "name": "com.locksteparena.client-prediction",
  "version": "0.1.0",
  "displayName": "Lockstep Arena Client Prediction",
  "description": "Offline bounded client prediction and authoritative reconciliation.",
  "unity": "6000.3",
  "dependencies": {
    "com.locksteparena.simulation": "0.1.0"
  }
}
```

Runtime `.csproj`:

```text
TargetFramework       netstandard2.1
LangVersion           9.0
Nullable              enable
ImplicitUsings        disable
TreatWarningsAsErrors true
```

It compiles only Runtime production source. Package-local
`Directory.Build.props` routes final and intermediate .NET output to the
repository `.artifacts/` directory.

Runtime asmdef:

```text
name               LockstepArena.Client.Prediction
references         LockstepArena.Simulation
autoReferenced     false
allowUnsafeCode    false
noEngineReferences true
```

## 4. Exact public API

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

        // false = clean/no rollback, including A == P
        // true  = Dirty reconciliation completed rollback/replay
        public bool ReconcileAuthoritative(
            FrameData authoritativeFrame);
    }
}
```

No Snapshot, PredictionRecord, history collection, mutable collection,
reset, retry, recovery, fault-control, protocol, or network type is public.

The class has a single-threaded usage contract and adds no locking.

## 5. Constructor contract

Validation order:

```text
1. initialState == null
2. maxPredictionTicks < 1
3. initialize state
```

Failures:

```text
null initialState       -> ArgumentNullException
maxPredictionTicks < 1  -> ArgumentOutOfRangeException
```

Initial live state:

```text
AuthoritativeState     = initialState
PredictedState         = initialState
PendingPredictionCount = 0
MaxPredictionTicks     = supplied value
faulted                = false
```

An initial state at `uint.MaxValue` is legal and already terminal.

## 6. Timeline and MaxPredictionTicks

Define:

```text
A = AuthoritativeState.Tick
P = PredictedState.Tick
```

The live invariant is:

```text
A <= P
history.Length == P - A
history.Length <= MaxPredictionTicks
history covers exactly [A, P)

if history is empty:
    A == P

if history is non-empty:
    history[0].StateBefore structurally equals AuthoritativeState

for logical record i:
    PredictedFrame.Tick == A + i
    StateBefore.Tick == PredictedFrame.Tick
    StateBefore roster structurally equals the battle roster
    PredictedFrame roster structurally equals the battle roster

PredictedState.Tick == P
```

Candidate histories must satisfy the same contract before commit. These
checks do not run a second Simulation algorithm.

`MaxPredictionTicks` is an explicit constructor parameter and is independent
of server `InputDelayTicks`. When `P - A == MaxPredictionTicks`, another
prediction is rejected before mutation. No record is evicted, capacity is not
grown, and no Snapshot request is generated.

## 7. Private ownership and prediction records

The only live fields are:

```text
BattleState _authoritativeState
BattleState _predictedState
PredictionRecord[] _history
readonly int _maxPredictionTicks
bool _faulted
```

There is no persistent `BattleSimulation`; candidate simulations are local
to one API call.

The private nested record is exactly:

```csharp
private sealed class PredictionRecord
{
    public PredictionRecord(
        BattleState stateBefore,
        FrameData predictedFrame);

    public BattleState StateBefore { get; }
    public FrameData PredictedFrame { get; }
}
```

`BattleState` and `FrameData` are existing immutable Domain objects. Records
share those immutable references. They are not deep-copied or serialized.
The private history array is replaced as a container and is never exposed.

## 8. Predict transaction

Validation and operation order:

```text
1. sticky fault
2. predictedFrame null
3. predictedFrame.Tick == PredictedState.Tick
4. predictedFrame roster structurally equals PredictedState roster
5. PredictedState is not terminal
6. live internal invariant
7. prediction lead is below MaxPredictionTicks
8. candidate Step and history construction
9. candidate invariant validation
10. final commit
```

Exceptions:

```text
faulted                    -> InvalidOperationException
null frame                 -> ArgumentNullException
wrong Tick or roster       -> ArgumentException
terminal or at capacity    -> InvalidOperationException
```

Public boundary rejection changes no state/history and does not fault.

Successful prediction:

1. Retain current `PredictedState` as `StateBefore`.
2. Create local `BattleSimulation(PredictedState)`.
3. Call `Step(predictedFrame)`.
4. Build a new history array with one appended record.
5. Validate the full candidate invariant.
6. Replace `_predictedState` and `_history`.
7. Leave `_authoritativeState` unchanged.

The frame must already be complete and canonical. Gate 12 does not create
neutral, repeat-last, or incomplete remote inputs.

## 9. Authoritative validation and Dirty comparison

`ReconcileAuthoritative` order:

```text
1. sticky fault
2. authoritativeFrame null
3. exact AuthoritativeState.Tick
4. structural roster equality
5. legal terminal Tick
6. live internal invariant
7. A == P / clean / Dirty classification
8. complete candidate construction
9. candidate invariant validation
10. final commit
```

Stale, duplicate, future-gap, wrong-roster, and terminal authority are
boundary rejection. They mutate nothing and do not fault.

Roster mismatch is never Dirty. Structural roster equality is `Count` plus
the same `PlayerId` at every `PlayerSlot`; object identity is irrelevant.

After boundary validation, a private helper compares predicted and
authoritative input in increasing Slot order:

```text
FrameData.Tick

for Slot 0..Roster.Count-1:
    InputFrame.PlayerSlot
    InputFrame.MoveX
    InputFrame.MoveZ
    InputFrame.Aim
```

It never compares protobuf bytes, repeated-field arrival order, object
identity, `GetHashCode`, `StateDigest`, or resulting state. Different input
is Dirty even if clamping makes the resulting state coincide.

## 10. Authority without prediction (`A == P`)

There is no corresponding prediction record.

1. Stage one local Simulation from `AuthoritativeState`.
2. Step the authoritative frame.
3. Use the result for both candidate states.
4. Keep candidate history empty.
5. Validate candidates.
6. Commit both state references.
7. Return `false`.

This is clean authority progress, not rollback.

## 11. Clean reconciliation

For equal predicted and authoritative input:

1. Stage and Step authority from current `AuthoritativeState`.
2. Build candidate history without the confirmed oldest record.
3. Keep the existing `PredictedState` reference unchanged.
4. If history remains, require its new oldest `StateBefore` to be
   structurally equal to the candidate authoritative frontier.
5. If history is empty, require candidate authoritative and predicted states
   to be structurally equal.
6. Validate the complete candidate invariant.
7. Commit authoritative state and history.
8. Return `false`.

Clean reconciliation must not recompute or replace `PredictedState`.

State structural comparison uses Tick, roster structure, PlayerCount, and
every PlayerState PositionX/PositionZ/Aim field. It does not use object
identity or `StateDigest`.

## 12. Dirty reconciliation

For Dirty authoritative Tick `T`:

```text
rollback start = StateBefore(T)
```

1. Read `StateBefore(T)` from the oldest pending record.
2. Require it to structurally equal the authoritative frontier.
3. Step authoritative Frame `T` from current `AuthoritativeState`.
4. Start candidate predicted Simulation from the corrected State `T+1`.
5. Discard the incorrect predicted Frame `T`.
6. Replay retained predicted Frames `T+1 .. P-1` in ascending Tick order.
7. Before each replayed frame, create its replacement `StateBefore`.
8. Build the full replacement history array.
9. Require reconstructed predicted Tick to remain the pre-call `P`.
10. Validate candidate history over `[T+1, P)`.
11. Commit authoritative state, reconstructed predicted state, and history.
12. Return `true`.

Only existing `BattleSimulation.Step` performs state transition. There is no
alternate movement, state patching, or replay simulation implementation.

## 13. Candidate-before-commit atomicity

Every mutation follows:

```text
live states/history
-> complete local candidate construction
-> complete candidate validation
-> final field replacement
```

No candidate is externally visible. Candidate arrays are independent
containers; immutable Domain objects may be shared. All potentially throwing
work finishes before live fields are assigned. This is atomic under the
single-threaded contract, not a concurrency transaction framework.

## 14. Sticky fail-stop and failure priority

An internal invariant violation:

```text
set _faulted = true
throw InvalidOperationException
commit no candidate state/history
```

If valid candidate `BattleSimulation.Step` or deterministic replay itself
unexpectedly throws:

```text
set _faulted = true
rethrow the original exception unchanged
commit no candidate state/history
```

After sticky fault, only mutation APIs are blocked first. Every later
`Predict` or `ReconcileAuthoritative` immediately throws
`InvalidOperationException` before argument validation.

Read-only getters remain observable after fault and return the last
successfully committed values:

```text
AuthoritativeState
PredictedState
MaxPredictionTicks
PendingPredictionCount
```

There is no Reset, Recover, Retry, compensation, replacement Publisher, or
public fault API. A test-only reflection fixture may create an impossible
private invariant; production receives no hook, delegate, interface, or
`InternalsVisibleTo`.

## 15. Terminal Tick

```text
uint.MaxValue - 1 = final consumable FrameData.Tick
uint.MaxValue     = terminal BattleState.Tick
```

Prediction may consume the final frame and reach predicted Tick
`uint.MaxValue`. Authority may later reconcile that pending final frame and
reach authoritative Tick `uint.MaxValue`. No Frame Tick `uint.MaxValue` may
be consumed. No Tick wraps. Normal terminal rejection is mutation-free and
does not fault.

## 16. Correct-prediction Golden

Roster:

| Slot | PlayerId |
|---:|---:|
| 0 | `0x0102030405060708` |
| 1 | `0x000000000000002A` |
| 2 | `0xFFEEDDCCBBAA0099` |
| 3 | `0x00000000000F4243` |

Initial State:

```text
Tick = 100
Slot0 X=-300 Z=0    Aim=1000
Slot1 X=300  Z=0    Aim=2000
Slot2 X=0    Z=-300 Aim=3000
Slot3 X=0    Z=300  Aim=4000
```

Inputs:

| Tick | Slot | MoveX | MoveZ | Aim |
|---:|---:|---:|---:|---:|
| 100 | 0 | 1 | 0 | 10100 |
| 100 | 1 | -1 | 0 | 20100 |
| 100 | 2 | 0 | 1 | 30100 |
| 100 | 3 | 0 | -1 | 40100 |
| 101 | 0 | 0 | 1 | 10101 |
| 101 | 1 | 0 | -1 | 20101 |
| 101 | 2 | 1 | 0 | 30101 |
| 101 | 3 | -1 | 0 | 40101 |
| 102 | 0 | -1 | 0 | 10102 |
| 102 | 1 | 1 | 0 | 20102 |
| 102 | 2 | 0 | -1 | 30102 |
| 102 | 3 | 0 | 1 | 40102 |

Predict 100, 101, 102, then reconcile authority 100, 101, 102.

```text
Dirty results: false, false, false
State101 Digest: D95809E1EB5CDDAA
State102 Digest: A96B83267DD72A7D
State103 Digest: 386C4BB11A7EB7E0

final A = 103
final P = 103
pending = 0
```

Final state:

```text
Slot0 X=-300 Z=100  Aim=10102
Slot1 X=300  Z=-100 Aim=20102
Slot2 X=100  Z=-300 Aim=30102
Slot3 X=-100 Z=300  Aim=40102
```

## 17. Wrong-prediction Golden

Use the same roster, initial state, and authority. Exactly one predicted
input differs:

```text
Tick101 Slot2

authority: MoveX= 1 MoveZ=0 Aim=30101
prediction: MoveX=-1 MoveZ=0 Aim=30101
```

All other predicted inputs equal authority.

Wrong predicted State102:

```text
Slot0 X=-200 Z=100  Aim=10101
Slot1 X=200  Z=-100 Aim=20101
Slot2 X=-100 Z=-200 Aim=30101
Slot3 X=-100 Z=200  Aim=40101
Digest = 8506E4507001B972
```

Wrong predicted State103 before authority:

```text
Slot0 X=-300 Z=100  Aim=10102
Slot1 X=300  Z=-100 Aim=20102
Slot2 X=-100 Z=-300 Aim=30102
Slot3 X=-100 Z=300  Aim=40102
Digest = 7D0D3A230618500F
```

Reconciliation sequence:

```text
Tick100 -> false / clean
Tick101 -> true  / Dirty
Tick102 -> false / clean
```

After Dirty Tick101:

```text
Authoritative State102 Digest = A96B83267DD72A7D

Reconstructed Predicted State103:
Slot0 X=-300 Z=100  Aim=10102
Slot1 X=300  Z=-100 Aim=20102
Slot2 X=100  Z=-300 Aim=30102
Slot3 X=-100 Z=300  Aim=40102
Digest = 386C4BB11A7EB7E0
```

The rebuilt retained Tick102 record starts from corrected authoritative
State102. Final Tick102 confirmation is clean and drains history:

```text
A = 103
P = 103
pending = 0
authoritative Digest = 386C4BB11A7EB7E0
predicted Digest     = 386C4BB11A7EB7E0
```

The wrong-path Digests were independently computed from the frozen 80-byte
little-endian state schema with FNV-1a64, without invoking production
Simulation.

`Gate12PredictionGoldenVector.cs` owns actual setup, execution, Dirty flags,
counts, and resulting states only. Expected state, flags, counts, and Digest
literals live only in .NET and Unity consumer tests.

## 18. Exact test contract

Dependency-free .NET test project:

```text
OutputType            Exe
TargetFramework       net8.0
LangVersion           12.0
Nullable              enable
ImplicitUsings        disable
TreatWarningsAsErrors true
BuildInParallel       false
```

Direct ProjectReferences exactly:

```text
LockstepArena.Client.Prediction
LockstepArena.Simulation
```

It links the single physical package Golden vector explicitly and uses no
NUnit/xUnit/MSTest or earlier Gate test project/helper.

Exact result:

```text
RESULT 36/36 passed
```

Exact test names:

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

Unity Editor test asmdef:

```text
name               LockstepArena.Client.Prediction.Editor.Tests
references         LockstepArena.Client.Prediction, LockstepArena.Simulation
includePlatforms   Editor
allowUnsafeCode    false
autoReferenced     false
optionalReferences TestAssemblies
```

Exact Unity result:

```text
total=2 passed=2 failed=0
```

Named tests:

```text
UnityClientPredictionGoldenTests.UnityExecutesCorrectPredictionGolden
UnityClientPredictionGoldenTests.UnityExecutesDirtyRollbackGolden
```

Fresh NUnit XML and named `Passed` results are mandatory; process exit code
alone is not evidence.

## 19. Repository integration contract

`.gitignore` adds exactly:

```text
!Packages/com.locksteparena.client-prediction/Runtime/LockstepArena.Client.Prediction.csproj
!Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj
```

`packages-lock.json` adds exactly the equivalent embedded entry:

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

Every existing lock entry remains unchanged. `Packages/manifest.json` has
zero committed diff.

## 20. Verification contract

Release build matrix is the frozen Gate 11 set of 18 projects plus the new
Prediction Runtime and test executable:

```text
20 Release builds
0 warnings
0 errors
```

.NET executions:

```text
Gate 3  38/38
Gate 4  32/32
Gate 5  35/35
Gate 6  24/24
Gate 7  32/32
Gate 8   8/8
Gate 9  27/27
Gate 10 27/27
Gate 11 32/32
Gate 12 36/36

Gate 3 Server Golden = 89A7DD66F8D9E871
Gate 8 TCP watchdog passes
Gate 11 live Tick103 Digest = 386C4BB11A7EB7E0
pinned Protocol regeneration = diff clean
```

Unity executions:

```text
Gate 12 2/2
Gate 7  1/1
Gate 5  2/2
Gate 3  required named Golden Passed, failed=0
```

## 21. Protected paths

Relative to the frozen baseline, committed diff must remain zero for:

```text
Packages/com.locksteparena.simulation/
Packages/com.locksteparena.protocol/
Packages/com.locksteparena.stream-framing/
Server/LockstepArena.Server.FrameSync/
Server/LockstepArena.Server.ProtocolAuthority/
Server/LockstepArena.Server.LiveTcp/
Client/LockstepArena.Client.LiveTcp/
Tests containing Gate 3-11 Goldens
Assets/
ProjectSettings/
Packages/manifest.json
```

In particular, Gate 12 does not modify `TcpClientBattlePump` or
`TcpServerBattlePump`.

The new package must contain no `bin`, `obj`, generated LockstepArena DLL,
symlink, junction, copy/sync script, or duplicated Simulation source.

The ordinary checkout must retain only its two user-owned modifications:

```text
Assets/Settings/Mobile_RPAsset.asset
ProjectSettings/ShaderGraphSettings.asset
```

## 22. Explicit exclusions and roadmap boundary

Gate 12 does not implement:

```text
Protocol/protobuf mapping
TCP/UDP/KCP integration
changes to either Gate 11 TCP pump
connection lifecycle or routing
incomplete prediction Frames
neutral or repeat-last remote input
missing-input or timeout policy
adaptive InputDelay
server Eligibility or Tick generation
wall-clock ownership
Snapshot wire DTO or transport
server Snapshot request
Replay product system
network catch-up or weak-network simulation
interpolation/render smoothing
Unity Transform/View
combat
room/login/session/matchmaking
reconnect/heartbeat
ECS
generic prediction/rollback/snapshot/netcode framework
DI/EventBus
Gate 13 or Gate 14 implementation
```

Gate 13 owns integration of this approved component with the live network
path, then Replay/catch-up/weak-network verification and the frozen KCP
reconsideration. Gate 14 remains v1 closure. No Gate 15 is introduced.

## 23. Planning-state evidence

This Architecture document is part of the Planning-only change. At Planning
time no package, implementation source, tests, `.gitignore`, lockfile, Unity
configuration, or protected baseline file has changed. Final implementation
evidence is appended only after a separately approved implementation and a
fresh complete verification matrix.
