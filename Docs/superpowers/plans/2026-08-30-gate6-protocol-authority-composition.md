# Gate 6 Offline Protocol-Aware Authority Composition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build one minimal offline Server composition class that accepts complete PlayerInputSubmission Protobuf payloads, drives continuous authoritative publication and Server Simulation, emits one authoritative payload per published Frame, and proves an independent Client Simulation reaches the same deterministic result.

**Architecture:** Add `LockstepArena.Server.ProtocolAuthority`, depending directly on the existing Protocol, Server.FrameSync, and Simulation projects. Its single `ProtocolAuthorityProcessor` owns the Coordinator and Server BattleSimulation, preserves all existing exception boundaries, converts Gate 4 publication batches into independent Gate 5 payloads, and becomes permanently faulted only when processing fails after non-empty authority publication. A dependency-free .NET suite performs all Client-side consumption and Golden verification; Unity keeps only its existing Gate 3 and Gate 5 regressions.

**Tech Stack:** .NET 8, C# 12, nullable reference types, Google.Protobuf 3.36.0 through the existing Protocol project, existing Gate 3 Simulation, Gate 4 FrameSync, Gate 5 generated contracts and ProtocolMapper, dependency-free console test runner, Unity 6000.3.10f1 regression tests.

**Spec:** `Docs/Architecture/GATE6_OFFLINE_PROTOCOL_AUTHORITY_COMPOSITION.md`

## Global Constraints

- Exact base is `72764ebcd2f0fbfa9f74ad95e4e61bf12c9709b2` on branch `codex/gate6-protocol-authority-composition` in `.worktrees/gate6-protocol-authority-composition`.
- Production is limited to `Server/LockstepArena.Server.ProtocolAuthority/ProtocolAuthorityProcessor.cs` plus its authored csproj.
- Existing Simulation, Server.FrameSync, Protocol, Assets, ProjectSettings, manifest, and packages-lock committed diff must remain zero.
- Add only the two exact `.gitignore` csproj exceptions approved by the architecture.
- Do not add a package, asmdef, solution, Directory.Build.props, NuGet dependency, Client production assembly, test hook, `InternalsVisibleTo`, injectable serializer, interface, factory, DI, callback, event, observer, lock, or async API.
- `SubmitPlayerInputPayload` starts with sticky-fault validation, then null validation, parse, mapping, and Coordinator submission in that exact order.
- Pre-publication failures retain their original exception types and do not fault the Processor.
- Any failure after a non-empty publication sets `_faulted = true`, rethrows the original exception, returns no partial payload array, and performs no rollback.
- One authoritative Frame always produces one independent authoritative Protobuf payload; do not add a wire batch or envelope.
- The Gate 6 suite must register exactly 24 tests and report `RESULT 24/24 passed`.
- Frozen per-Tick Digests are `D95809E1EB5CDDAA`, `A96B83267DD72A7D`, and `386C4BB11A7EB7E0`.
- The Golden vector contains actual values only; expected state and Digest literals remain in the test consumer.
- Do not implement TCP, UDP, KCP, Socket, framing, length prefix, envelope/opcode, connection, Session, Login, Room, clock, InputDelay, timeout, missing-input replacement, Prediction, Snapshot, Rollback, Replay, State Sync, reconnect, heartbeat, View, Combat, router, handler framework, middleware, transaction, retry, or recovery.
- Use TDD for every production behavior and commit each approved slice independently.

---

## File Map

### Production

- Create `Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj`: .NET 8 production project with direct references to Protocol, FrameSync, and Simulation.
- Create `Server/LockstepArena.Server.ProtocolAuthority/ProtocolAuthorityProcessor.cs`: sole production composition type and sticky-fault boundary.

### Tests

- Create `Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj`: dependency-free .NET 8 executable referencing Gate 6 production, Protocol, and Simulation.
- Create `Tests/LockstepArena.Server.ProtocolAuthority.Tests/Program.cs`: minimal test runner and `TestAssert` helpers.
- Create `Tests/LockstepArena.Server.ProtocolAuthority.Tests/ProtocolAuthorityProcessorTests.cs`: bootstrap, publication, ownership, and deterministic composition test groups.
- Create `Tests/LockstepArena.Server.ProtocolAuthority.Tests/ProtocolAuthorityErrorTests.cs`: parse/mapping/authority rejection and sticky-fault groups.
- Create `Tests/LockstepArena.Server.ProtocolAuthority.Tests/Gate6GapFillGoldenVector.cs`: pure actual 4-player Tick100-102 execution vector with no expected literals.

### Existing Files

- Modify `.gitignore`: add only the two exact Gate 6 authored csproj exceptions.
- Modify `Docs/Architecture/GATE6_OFFLINE_PROTOCOL_AUTHORITY_COMPOSITION.md` only in the final evidence task.

---

## Task 1: Add the Production and Test Project Baseline

**Commit:** `build: add server protocol authority projects`

**Files:**

- Modify: `.gitignore`
- Create: `Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj`
- Create: `Server/LockstepArena.Server.ProtocolAuthority/ProtocolAuthorityProcessor.cs`
- Create: `Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj`
- Create: `Tests/LockstepArena.Server.ProtocolAuthority.Tests/Program.cs`
- Create: `Tests/LockstepArena.Server.ProtocolAuthority.Tests/ProtocolAuthorityProcessorTests.cs`

**Interfaces:**

- Consumes: `BattleState`, `BattleSimulation`, and `AuthoritativeFrameCoordinator` constructors from the approved base.
- Produces: the final constructor, `ServerState`, and `NextPublishTick`; submission behavior is added in Task 2.

- [ ] **Step 1: Reconfirm the final approved Planning HEAD and lineage**

Required implementation start state:

```text
Branch:
codex/gate6-protocol-authority-composition

HEAD:
the final independently approved Planning amendment commit

HEAD direct parent / Planning foundation:
9ec1f2c63ae804be74b266a111cf060ddce6036b

Planning foundation direct parent / Approved Base:
72764ebcd2f0fbfa9f74ad95e4e61bf12c9709b2

Approved Base remains ancestor / merge-base:
72764ebcd2f0fbfa9f74ad95e4e61bf12c9709b2

Gate 6 worktree:
clean
```

Run these checks before making any implementation change:

```powershell
$planningFoundation = '9ec1f2c63ae804be74b266a111cf060ddce6036b'
$approvedBase = '72764ebcd2f0fbfa9f74ad95e4e61bf12c9709b2'

if ((git branch --show-current) -ne 'codex/gate6-protocol-authority-composition') {
    throw 'Wrong Gate 6 branch.'
}

if ((git rev-parse HEAD^) -ne $planningFoundation) {
    throw 'Gate 6 implementation must start from the final approved Planning amendment commit.'
}

if ((git rev-parse "$planningFoundation^") -ne $approvedBase) {
    throw 'Gate 6 Planning foundation direct parent does not match the approved Gate 5 baseline.'
}

if ((git merge-base HEAD $approvedBase) -ne $approvedBase) {
    throw 'Approved Gate 5 baseline must remain the Gate 6 merge-base.'
}

if ((git status --porcelain).Length -ne 0) {
    throw 'Gate 6 worktree must be clean.'
}

git -C E:\unityproject\LockstepArena status --short
```

Require the exact lineage above and only the two user-owned ordinary-checkout changes. Do not checkout or reset the implementation worktree to the Approved Base: the Approved Base is the regression/diff comparison baseline and ancestor, not the implementation start HEAD.

- [ ] **Step 2: Add exactly two authored-project ignore exceptions**

Append beside the existing authored csproj exceptions:

```gitignore
!Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj
!Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj
```

Run `git check-ignore -v --no-index` for both proposed csproj paths before and after the edit. Before the edit, both must match `*.csproj`; afterward, neither may remain ignored. Do not change any other ignore rule.

- [ ] **Step 3: Create the two project files**

Production csproj properties:

```xml
<TargetFramework>net8.0</TargetFramework>
<LangVersion>12.0</LangVersion>
<Nullable>enable</Nullable>
<ImplicitUsings>disable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

Add direct ProjectReferences to:

```text
../../Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj
../LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj
../../Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj
```

The test project uses the same compiler properties plus:

```xml
<OutputType>Exe</OutputType>
<BuildInParallel>false</BuildInParallel>
```

Its direct ProjectReferences are the Gate 6 production project, Protocol project, and Simulation project.

- [ ] **Step 4: Write four failing bootstrap tests**

Create the same dependency-free `TestCase`, `TestAssert.Equal`, `TestAssert.Same`, `TestAssert.True`, and `TestAssert.Throws<TException>` pattern used by the Gate 5 Protocol suite. Register exactly these first four cases in `ProcessorBootstrapTests.All`:

```text
ConstructorRejectsNullInitialState
ConstructorDelegatesInvalidHistoryCapacity
ConstructorBootstrapsExactServerState
ConstructorStartsAuthorityAtInitialStateTick
```

Use a two-player `BattleState` at Tick 100. The last two tests assert the Processor returns the exact initial state instance, preserves its roster, and exposes `NextPublishTick == 100`.

- [ ] **Step 5: Run RED**

Run:

```powershell
dotnet run --project Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj -c Release
```

Require compilation failure because `ProtocolAuthorityProcessor` does not exist. A restore/network failure is not valid RED evidence.

- [ ] **Step 6: Implement only construction and read-only state**

Create the sealed class with:

```csharp
public ProtocolAuthorityProcessor(
    BattleState initialState,
    uint maxFutureTickOffset,
    int authoritativeHistoryCapacity)
{
    if (initialState is null)
    {
        throw new ArgumentNullException(nameof(initialState));
    }

    _coordinator = new AuthoritativeFrameCoordinator(
        initialState.Roster,
        initialState.Tick,
        maxFutureTickOffset,
        authoritativeHistoryCapacity);
    _serverSimulation = new BattleSimulation(initialState);
}

public BattleState ServerState => _serverSimulation.State;

public uint NextPublishTick => _coordinator.NextPublishTick;
```

Declare only `_coordinator` and `_serverSimulation` in this task. Add `_faulted` with the behavior that first uses it in Task 3, so the warnings-as-errors Task 1 build cannot report an unused private field. Do not add a public fault API or implement submission in this task.

- [ ] **Step 7: Run GREEN and build**

Require:

```text
RESULT 4/4 passed
production Release build: 0 warnings / 0 errors
test Release build: 0 warnings / 0 errors
```

- [ ] **Step 8: Audit and commit**

Confirm the diff contains only the two exact exceptions and the five Task 1 project/source files. Run `git diff --check`, then commit:

```powershell
git add .gitignore Server/LockstepArena.Server.ProtocolAuthority Tests/LockstepArena.Server.ProtocolAuthority.Tests
git commit -m "build: add server protocol authority projects"
```

---

## Task 2: Compose Complete Submission Payloads into Authority Outputs

**Commit:** `feat: compose protobuf inputs into authority outputs`

**Files:**

- Modify: `Server/LockstepArena.Server.ProtocolAuthority/ProtocolAuthorityProcessor.cs`
- Modify: `Tests/LockstepArena.Server.ProtocolAuthority.Tests/Program.cs`
- Modify: `Tests/LockstepArena.Server.ProtocolAuthority.Tests/ProtocolAuthorityProcessorTests.cs`
- Create: `Tests/LockstepArena.Server.ProtocolAuthority.Tests/ProtocolAuthorityErrorTests.cs`

**Interfaces:**

- Consumes: `PlayerInputSubmissionMessage.Parser.ParseFrom`, `ProtocolMapper.ToDomain(PlayerInputSubmissionMessage)`, `AuthoritativeFrameCoordinator.Submit`, `ProtocolMapper.ToWire(FrameData)`, and `IMessage.ToByteArray()`.
- Produces: `public byte[][] SubmitPlayerInputPayload(byte[] completePayload)` with final successful 0/1/N behavior. Sticky containment is completed in Task 3.

- [ ] **Step 1: Add seven pre-publication rejection tests**

Register these in `ProtocolAuthorityRejectionTests.All`:

```text
NullPayloadThrowsArgumentNullWithoutFault
MalformedPayloadPreservesParserExceptionWithoutFault
InvalidMappingPreservesProtocolMappingExceptionWithoutFault
OldTickPreservesCoordinatorExceptionWithoutFault
FutureWindowPreservesCoordinatorExceptionWithoutFault
PlayerIdSlotMismatchPreservesCoordinatorExceptionWithoutFault
DuplicateSubmissionPreservesAcceptedInputWithoutFault
```

After each rejection, submit the remaining valid inputs for the current Tick and prove the Processor can still publish and advance. The malformed test must catch `InvalidProtocolBufferException`; the invalid-mapping test uses a parsed message with `MoveX = 2`; authority tests preserve the exact existing Coordinator exception category.

- [ ] **Step 2: Add five initial publication tests**

Register these in `ProtocolAuthorityPublicationTests.All`:

```text
TwoPlayerIncompleteFrameReturnsEmpty
TwoPlayerCompletionReturnsOnePayload
ThreePlayerCompleteFutureFrameWaitsForGap
GapFillReturnsContinuousPayloadTicks
SuccessfulBatchCatchesServerStateUpToAuthority
```

Parse every returned payload with `AuthoritativeFrameMessage.Parser`, map it with a structurally equal independent roster, and assert canonical Frame Tick and Slot order. For the gap test, complete future Ticks before the current gap and require output Tick order to begin at the prior `NextPublishTick`.

- [ ] **Step 3: Register the cumulative RED suite**

Update `Program` to combine Bootstrap, Rejection, and Publication groups. Run the suite and require compilation failure because `SubmitPlayerInputPayload` is absent. The expected future Green count is 16.

- [ ] **Step 4: Implement parse, authority, Server Step, and payload output**

Add the method with this operation order:

```csharp
public byte[][] SubmitPlayerInputPayload(byte[] completePayload)
{
    if (completePayload is null)
    {
        throw new ArgumentNullException(nameof(completePayload));
    }

    PlayerInputSubmissionMessage wire =
        PlayerInputSubmissionMessage.Parser.ParseFrom(completePayload);
    (PlayerId submittedPlayerId, InputFrame input) = ProtocolMapper.ToDomain(wire);
    FrameData[] publication = _coordinator.Submit(submittedPlayerId, input);
    if (publication.Length == 0)
    {
        return Array.Empty<byte[]>();
    }

    var payloads = new byte[publication.Length][];
    for (int index = 0; index < publication.Length; index++)
    {
        FrameData frame = publication[index];
        _serverSimulation.Step(frame);
        payloads[index] = ProtocolMapper.ToWire(frame).ToByteArray();
    }

    return payloads;
}
```

Use the existing generated wire type and Google.Protobuf extension. Do not catch or wrap pre-publication exceptions. Do not sort publication Frames or create a batch message.

- [ ] **Step 5: Run GREEN and focused audits**

Require:

```text
RESULT 16/16 passed
production and test builds: 0 warnings / 0 errors
```

Search production to confirm there is no interface, event, delegate, callback, Task, lock, router, handler, batch DTO, envelope, or transport term.

- [ ] **Step 6: Commit**

Run `git diff --check`, then commit only Task 2 files:

```powershell
git add Server/LockstepArena.Server.ProtocolAuthority/ProtocolAuthorityProcessor.cs Tests/LockstepArena.Server.ProtocolAuthority.Tests
git commit -m "feat: compose protobuf inputs into authority outputs"
```

---

## Task 3: Enforce Output Ownership and Sticky Invariant Containment

**Commit:** `feat: contain post-publication authority failures`

**Files:**

- Modify: `Server/LockstepArena.Server.ProtocolAuthority/ProtocolAuthorityProcessor.cs`
- Modify: `Tests/LockstepArena.Server.ProtocolAuthority.Tests/ProtocolAuthorityProcessorTests.cs`
- Modify: `Tests/LockstepArena.Server.ProtocolAuthority.Tests/ProtocolAuthorityErrorTests.cs`
- Modify: `Tests/LockstepArena.Server.ProtocolAuthority.Tests/Program.cs`

**Interfaces:**

- Consumes: the Task 2 submission API.
- Produces: final sticky-fault behavior and caller-owned 1/N payload containers without changing the public API.

- [ ] **Step 1: Add two ownership tests**

Extend `ProtocolAuthorityPublicationTests.All` with:

```text
NonEmptyOutputOwnsItsOuterContainer
PayloadBuffersAreDistinctAndCallerMutationCannotAffectServerState
```

Create a three-Frame publication. Save the final Server Digest and NextPublishTick, replace elements in the returned outer array, mutate bytes in one payload, and assert Server state/Digest and NextPublishTick are unchanged. Assert every non-empty payload is a distinct byte-array reference and mutation of one does not alter another.

- [ ] **Step 2: Add two sticky-fault tests**

Register in `ProtocolAuthorityFaultTests.All`:

```text
PostPublicationStepFailureRethrowsAndAdvancesAuthority
FaultedProcessorRejectsBeforePayloadValidation
```

Use reflection flags `Instance | NonPublic`, select the only field whose `FieldType == typeof(BattleSimulation)`, and fail the test unless exactly one exists. Advance that Simulation one Tick using a valid local Frame while leaving Processor authority unchanged. Complete the Processor's current authority Tick through public payload submissions.

The first test must catch the original `ArgumentException` from `BattleSimulation.Step` and assert public `NextPublishTick` advanced, proving authority publication preceded failure. The second creates the same fault, then passes null or malformed bytes and requires immediate `InvalidOperationException`, proving fault validation happens first.

- [ ] **Step 3: Run RED**

Run the cumulative suite. Require the sticky-fault tests to fail because Task 2 does not retain a fault and subsequent submission reaches payload validation. Do not accept a reflection setup failure as RED.

- [ ] **Step 4: Add the exact sticky-fault boundary**

Add the final approved private field:

```csharp
private bool _faulted;
```

Place this first in `SubmitPlayerInputPayload`:

```csharp
if (_faulted)
{
    throw new InvalidOperationException("The protocol authority processor is faulted.");
}
```

Leave parse, mapping, and Coordinator submission outside the protected block. After a non-empty publication, wrap output allocation, ordered Step, mapping, and serialization only:

```csharp
try
{
    var payloads = new byte[publication.Length][];
    for (int index = 0; index < publication.Length; index++)
    {
        FrameData frame = publication[index];
        _serverSimulation.Step(frame);
        payloads[index] = ProtocolMapper.ToWire(frame).ToByteArray();
    }

    return payloads;
}
catch
{
    _faulted = true;
    throw;
}
```

Do not add a public fault property, rollback, recovery, custom exception, or injection point.

- [ ] **Step 5: Run GREEN and verify exact count**

Require:

```text
RESULT 20/20 passed
production and test builds: 0 warnings / 0 errors
```

Re-run malformed, mapping, authority-rejection, and incomplete-frame cases to prove they do not set the sticky fault.

- [ ] **Step 6: Commit**

Run `git diff --check`, then commit:

```powershell
git add Server/LockstepArena.Server.ProtocolAuthority/ProtocolAuthorityProcessor.cs Tests/LockstepArena.Server.ProtocolAuthority.Tests
git commit -m "feat: contain post-publication authority failures"
```

---

## Task 4: Prove the Frozen Gap-Fill Golden and Client Determinism

**Commit:** `test: prove offline protocol authority composition`

**Files:**

- Create: `Tests/LockstepArena.Server.ProtocolAuthority.Tests/Gate6GapFillGoldenVector.cs`
- Modify: `Tests/LockstepArena.Server.ProtocolAuthority.Tests/ProtocolAuthorityProcessorTests.cs`
- Modify: `Tests/LockstepArena.Server.ProtocolAuthority.Tests/Program.cs`

**Interfaces:**

- Consumes: the final Processor API and existing ProtocolMapper/BattleSimulation APIs.
- Produces: actual Server/Client results for the approved Tick100-102 vector and four independent consumer tests with frozen expected literals.

- [ ] **Step 1: Add four failing deterministic tests**

Register in `ProtocolAuthorityDeterminismTests.All`:

```text
GapFillPublishesTicks100Through102AsIndependentPayloads
ClientDigestsMatchApprovedPerTickOracles
ServerAndClientMatchApprovedFinalStateAndDigest
DifferentSubmissionArrivalOrdersProduceSameAuthoritativeDomainSequence
```

The first asserts all pre-gap outputs are empty, the final output length is three, payload arrays are distinct, and parsed authoritative Ticks are exactly 100, 101, 102.

The second independently asserts Client Digests after each consumed payload:

```text
0xD95809E1EB5CDDAA
0xA96B83267DD72A7D
0x386C4BB11A7EB7E0
```

The third independently asserts Server and Client Tick 103, all four approved final PlayerState values, `NextPublishTick == 103`, structural roster equality, full state equality, and final Digest `0x386C4BB11A7EB7E0` on both sides.

The fourth runs a second Processor with this different valid inter-Tick and per-Slot arrival order:

```text
Tick100: 3,1,2
Tick102: 1,3,0,2
Tick101: 2,0,3,1
Tick100: 0
```

Parse/map both output sequences and compare Tick plus every canonical InputFrame field, then compare final Server state and Digest. Do not require submission payload byte equality and do not claim this Golden shuffles roster entries.

- [ ] **Step 2: Run RED**

Run the suite and require compilation failure because `Gate6GapFillGoldenVector` and its result type do not exist.

- [ ] **Step 3: Implement the pure actual vector**

Create a four-player roster in Slot order:

```text
0102030405060708
000000000000002A
FFEEDDCCBBAA0099
00000000000F4243
```

Create independent Server and Client BattleStates at Tick 100 with initial positions and aims from the architecture spec. Generate the twelve approved InputFrames, convert each to `PlayerInputSubmissionMessage` with `ProtocolMapper.ToWire`, serialize it, and call the Processor in approved order:

```text
Tick100: 0,2,1
Tick101: 3,1,0,2
Tick102: 2,0,3,1
Tick100: 3
```

For every authoritative payload returned by the final call, parse, map with the independent Client roster, Step the Client Simulation, and record the actual mapped Frame, Client state, and Client Digest. Return actual pre-gap output lengths, authoritative payloads, mapped Frames, Client states/Digests, final Processor ServerState, Client final state, and Processor NextPublishTick.

Provide a second run method using the exact alternate arrival order specified in Step 1 and the same logical inputs. Do not put any expected state, expected Digest, NUnit, Unity API, file I/O, time, environment access, or randomness in the vector.

- [ ] **Step 4: Run GREEN and audit expected separation**

Require:

```text
RESULT 24/24 passed
Gate 6 production/test Release builds: 0 warnings / 0 errors
```

Search `Gate6GapFillGoldenVector.cs` to prove it contains none of the three expected Digest literals and no expected final-state helper. Confirm the expected literals occur only in the consumer tests.

- [ ] **Step 5: Run Gate 3, Gate 4, and Gate 5 .NET regressions**

Require fresh:

```text
Gate 3: RESULT 38/38 passed
Gate 4: RESULT 32/32 passed
Gate 5: RESULT 35/35 passed
Gate 3 Server Golden: Tick=1000 Players=4 Digest=89A7DD66F8D9E871
```

- [ ] **Step 6: Audit protected production paths and commit**

Relative to the approved base, require zero committed and working-tree diff under Simulation, FrameSync, Protocol, Assets, ProjectSettings, manifest, and packages-lock. Run `git diff --check`, then commit:

```powershell
git add Tests/LockstepArena.Server.ProtocolAuthority.Tests
git commit -m "test: prove offline protocol authority composition"
```

---

## Task 5: Execute Fresh Final Verification, Record Evidence, Push, and Stop

**Commit:** `docs: record Gate 6 implementation evidence`

**Files:**

- Modify: `Docs/Architecture/GATE6_OFFLINE_PROTOCOL_AUTHORITY_COMPOSITION.md`

- [ ] **Step 1: Reconfirm implementation HEAD and regenerate Protocol deterministically**

Record branch, HEAD, and clean status. Confirm `PROTOBUF_PROTOC` and `Protobuf_ProtocFullPath` are absent, record Grpc.Tools 2.83.0, resolved bundled protoc path and `libprotoc 35.1`, regenerate, require exactly one `.proto` and one tracked `.g.cs`, and require Schema/Generated `git diff --exit-code`.

- [ ] **Step 2: Run ten individual Release builds**

Build with zero warnings and zero errors:

```text
Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj
Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj
Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj
Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj
Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj
Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj
Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj
Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj
Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj
Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj
```

- [ ] **Step 3: Run fresh .NET suites and Goldens**

Require exact results:

```text
Gate 3: RESULT 38/38 passed
Gate 4: RESULT 32/32 passed
Gate 5: RESULT 35/35 passed
Gate 6: RESULT 24/24 passed
Gate 3 Server Golden: Tick=1000 Players=4 Digest=89A7DD66F8D9E871
Gate 6 Server/Client final Tick: 103
Gate 6 final Digest: 386C4BB11A7EB7E0
```

- [ ] **Step 4: Run two fresh Unity regression jobs**

With Unity Editor closed before each job:

1. Run EditMode using only assembly filter `LockstepArena.Protocol.Editor.Tests`; require fresh XML `total=2`, `passed=2`, `failed=0`, with both Gate 5 named tests Passed.
2. Run EditMode using assembly filter `LockstepArena.Simulation.Editor.Tests` and test filter `UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector`; require fresh XML `total=1`, `passed=1`, `failed=0`, and the named test Passed.

Record Unity 6000.3.10f1 version, process exit, XML totals, names, log paths, and XML paths. Exit code alone is not acceptance evidence. If license, package-manager, or instance-lock failure prevents the worktree run, stop and report; do not use the ordinary checkout as a workaround.

- [ ] **Step 5: Inspect and restore only Unity-generated serialization changes**

Inspect every worktree-local Assets or ProjectSettings diff. Restore only exact Unity-generated paths after viewing their diffs. Never use broad reset or clean. Re-run `git status --short` afterward.

- [ ] **Step 6: Run dependency, scope, source, and artifact audits**

Relative to the approved base, require committed diff zero for Simulation, FrameSync, Protocol, Assets, ProjectSettings, manifest, and packages-lock. Confirm exactly two `.gitignore` additions; no new DLL, package, asmdef, solution, Directory.Build.props, script, symlink, junction, Client production assembly, transport/time type, test hook, `InternalsVisibleTo`, injection abstraction, router, handler, event, DI, middleware, transaction, retry, or recovery implementation.

Confirm `ProtocolAuthorityProcessor` has only the approved three fields and public API, authority publication order comes only from the Coordinator array, every Frame is serialized independently, and the reflection test finds BattleSimulation by unique field type. Search the full Gate 6 diff to ensure only the three corrected Digest constants occur.

- [ ] **Step 7: Confirm ordinary-checkout preservation**

Require exactly:

```text
 M Assets/Settings/Mobile_RPAsset.asset
 M ProjectSettings/ShaderGraphSettings.asset
```

Do not restore, stage, clean, or commit those files.

- [ ] **Step 8: Append complete implementation evidence**

Add an Implementation Evidence section to the architecture document. Record exact base and implementation commits, ten build results, four suite totals, both Goldens, three Gate 6 Digests, Server/Client final equality, sticky-fault evidence, Unity XML evidence, regeneration provenance/diff result, protected-path audits, dependency direction, source/artifact/scope audits, and ordinary-checkout preservation. Do not claim a command that was not freshly executed.

- [ ] **Step 9: Commit evidence and inspect final committed scope**

Run `git diff --check`, commit only the architecture evidence with:

```powershell
git add Docs/Architecture/GATE6_OFFLINE_PROTOCOL_AUTHORITY_COMPOSITION.md
git commit -m "docs: record Gate 6 implementation evidence"
```

Then inspect `git log`, `git diff --name-status` from the approved base, and clean status. Require the planning commit plus the approved implementation slices, with no unapproved path.

- [ ] **Step 10: Push, prove remote equality, hand off, and stop**

Push `codex/gate6-protocol-authority-composition`, compare local HEAD with `git ls-remote --heads origin refs/heads/codex/gate6-protocol-authority-composition`, require exact SHA equality and a clean Gate 6 worktree, submit the Gate 6 Final Implementation Handoff, and stop. Do not begin Gate 7 planning or implementation.
