# Gate 4 Offline Authoritative Multi-Tick Frame Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add one offline Server-owned coordinator that accepts variable-roster inputs across a bounded Tick window and publishes only the continuous complete prefix as authoritative FrameData.

**Architecture:** A .NET 8 Server class library references the unchanged Shared Simulation project and owns one `AuthoritativeFrameCoordinator`. It routes each exact Tick to Gate 3's `StrictFrameCollector`, plans a complete publication batch in local copied containers, then replaces pending/history/Tick state without exposing partial publication. A separate dependency-free .NET 8 executable proves 32 contracts, including two different multi-Tick arrival orders driving identical Simulation digests.

**Tech Stack:** C# 12, .NET 8, existing netstandard2.1 `LockstepArena.Simulation`, SDK-style projects, `Dictionary<uint, StrictFrameCollector>`, `Queue<FrameData>`, existing dependency-free test-runner pattern, Unity 6000.3.10f1 regression only.

**Spec:** `Docs/Architecture/GATE4_AUTHORITATIVE_MULTI_TICK_FRAME_PIPELINE.md`

## Global Constraints

- Exact approved base: `0137342be01d15ae52f437ef53a9fdd0f3437c85`.
- Work only in `.worktrees/gate4-authoritative-frame-pipeline` on `codex/gate4-authoritative-frame-pipeline`.
- Gate 4 is additive: make no committed change anywhere under `Packages/com.locksteparena.simulation/`.
- `AuthoritativeFrameCoordinator` belongs only to `Server/LockstepArena.Server.FrameSync` and does not hold `BattleSimulation`.
- Reuse `StrictFrameCollector` for all identity, Slot, duplicate, completeness, and canonical-frame rules.
- Use pending Dictionary only for exact Tick lookup; never enumerate it to choose publication order.
- Compute publication batch, next Tick, copied pending Dictionary, and copied bounded history Queue completely before final field replacement.
- Share immutable `FrameData` and unmodified `StrictFrameCollector` references; do not add deep-copy infrastructure.
- `InputFrame.Tick == uint.MaxValue` is always rejected; `uint.MaxValue - 1` is the last publishable Tick; no wraparound.
- One battle serializes Submit calls. Add no locks, concurrent collections, async flow, scheduler, timeline, clock, or transport abstraction.
- Add no Protobuf, TCP, UDP, KCP, Socket, Room, Login, Session, TickClock, wall-clock timeout, missing-input substitution, Prediction, Snapshot, Rollback, Replay, reconnect, Unity View, or Combat behavior.
- The two planned authored csproj paths are currently ignored by `*.csproj`; implementation adds only their exact negated `.gitignore` entries.
- Keep the ordinary checkout's `Assets/Settings/Mobile_RPAsset.asset` and `ProjectSettings/ShaderGraphSettings.asset` changes untouched.
- Do not start Gate 5 work.

---

## Planned File Structure

~~~text
.gitignore
Docs/Architecture/GATE4_AUTHORITATIVE_MULTI_TICK_FRAME_PIPELINE.md
Server/LockstepArena.Server.FrameSync/
  LockstepArena.Server.FrameSync.csproj
  AuthoritativeFrameCoordinator.cs
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

No solution, Server Host executable, package, interface assembly, generated code, script, or Unity metadata is added.

### Task 1: Establish the Server Assembly and Constructor Contract

**Files:**

- Modify: `.gitignore`
- Create: `Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj`
- Create: `Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs`
- Create: `Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj`
- Create: `Tests/LockstepArena.Server.FrameSync.Tests/Program.cs`
- Create: `Tests/LockstepArena.Server.FrameSync.Tests/CoordinatorContractTests.cs`

**Interfaces:**

- Consumes: Gate 3 `ActiveRoster`, `PlayerId`, `InputFrame`, `StrictFrameCollector`, and `FrameData` from the existing Simulation ProjectReference.
- Produces: constructor, `Roster`, `NextPublishTick`, and an initially empty defensive history snapshot. Submit is added under RED tests in Task 2.

- [ ] **Step 1: Confirm the exact base, clean tree, and real csproj ignore behavior**

Run:

~~~powershell
if ((git rev-parse HEAD) -ne '0137342be01d15ae52f437ef53a9fdd0f3437c85') { throw 'Gate 4 is not based on the approved Gate 3 commit.' }
if (git status --porcelain) { git status --short; throw 'Gate 4 worktree is not clean.' }
git check-ignore -v Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj
git check-ignore -v Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj
~~~

Expected: both csproj candidates match the repository's `*.csproj` ignore rule.

- [ ] **Step 2: Add only the required authored-project exceptions**

Append beside the current authored-project exceptions in `.gitignore`:

~~~gitignore
!Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj
!Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj
~~~

Verify:

~~~powershell
git check-ignore Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj
if ($LASTEXITCODE -eq 0) { throw 'Production csproj is still ignored.' }
git check-ignore Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj
if ($LASTEXITCODE -eq 0) { throw 'Test csproj is still ignored.' }
~~~

- [ ] **Step 3: Create the two SDK projects**

Production csproj:

~~~xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj" />
  </ItemGroup>
</Project>
~~~

Test csproj:

~~~xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Server\LockstepArena.Server.FrameSync\LockstepArena.Server.FrameSync.csproj" />
    <ProjectReference Include="..\..\Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj" />
  </ItemGroup>
</Project>
~~~

- [ ] **Step 4: Add the dependency-free runner and four constructor tests**

`Program.cs` contains the complete small runner and assertions:

~~~csharp
using System;
using System.Collections.Generic;

namespace LockstepArena.Server.FrameSync.Tests
{
    internal sealed class TestCase
    {
        public TestCase(string name, Action body)
        {
            Name = name;
            Body = body;
        }

        public string Name { get; }
        public Action Body { get; }
    }

    internal static class TestAssert
    {
        public static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"Expected <{expected}> but found <{actual}>.");
            }
        }

        public static void Same(object expected, object actual)
        {
            if (!ReferenceEquals(expected, actual))
            {
                throw new InvalidOperationException("Expected both values to reference the same object.");
            }
        }

        public static void Throws<TException>(Action body)
            where TException : Exception
        {
            try
            {
                body();
            }
            catch (TException)
            {
                return;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Expected {typeof(TException).Name} but found {exception.GetType().Name}.",
                    exception);
            }

            throw new InvalidOperationException($"Expected {typeof(TException).Name} but no exception was thrown.");
        }
    }

    internal static class Program
    {
        private static int Main()
        {
            TestCase[] tests = Combine(CoordinatorContractTests.All);
            int failures = 0;
            foreach (TestCase test in tests)
            {
                try
                {
                    test.Body();
                    Console.WriteLine($"PASS {test.Name}");
                }
                catch (Exception exception)
                {
                    failures++;
                    Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
                }
            }

            Console.WriteLine($"RESULT {tests.Length - failures}/{tests.Length} passed");
            return failures == 0 ? 0 : 1;
        }

        private static TestCase[] Combine(params TestCase[][] groups)
        {
            int count = 0;
            foreach (TestCase[] group in groups)
            {
                count += group.Length;
            }

            TestCase[] combined = new TestCase[count];
            int offset = 0;
            foreach (TestCase[] group in groups)
            {
                Array.Copy(group, 0, combined, offset, group.Length);
                offset += group.Length;
            }

            return combined;
        }
    }
}
~~~

Register exactly these tests in `CoordinatorContractTests.All`:

~~~csharp
new TestCase("ConstructorRejectsNullRoster", ConstructorRejectsNullRoster),
new TestCase("ConstructorRejectsNonPositiveHistoryCapacity", ConstructorRejectsNonPositiveHistoryCapacity),
new TestCase("ConstructorExposesRosterAndInitialTick", ConstructorExposesRosterAndInitialTick),
new TestCase("InitialMaxTickHasEmptyHistory", InitialMaxTickHasEmptyHistory),
~~~

Use a two-player roster with IDs `91` and `17`. Assert null throws `ArgumentNullException`, capacities `0` and `-1` throw `ArgumentOutOfRangeException`, the exact immutable roster reference is exposed, Tick `73` is preserved, and a coordinator initialized at `uint.MaxValue` returns an empty history array.

- [ ] **Step 5: Run RED before the production type exists**

Run:

~~~powershell
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release
~~~

Expected: compilation fails with `CS0246` for `AuthoritativeFrameCoordinator`. Restore or SDK failures are not acceptable RED evidence.

- [ ] **Step 6: Implement only the constructor contract**

Create the production type with these fields and members:

~~~csharp
private readonly uint _maxFutureTickOffset;
private readonly int _authoritativeHistoryCapacity;
private Dictionary<uint, StrictFrameCollector> _pendingByTick;
private Queue<FrameData> _authoritativeHistory;

public AuthoritativeFrameCoordinator(
    ActiveRoster roster,
    uint initialPublishTick,
    uint maxFutureTickOffset,
    int authoritativeHistoryCapacity)
{
    Roster = roster ?? throw new ArgumentNullException(nameof(roster));
    if (authoritativeHistoryCapacity <= 0)
    {
        throw new ArgumentOutOfRangeException(nameof(authoritativeHistoryCapacity));
    }

    NextPublishTick = initialPublishTick;
    _maxFutureTickOffset = maxFutureTickOffset;
    _authoritativeHistoryCapacity = authoritativeHistoryCapacity;
    _pendingByTick = new Dictionary<uint, StrictFrameCollector>();
    _authoritativeHistory = new Queue<FrameData>();
}

public ActiveRoster Roster { get; }

public uint NextPublishTick { get; private set; }

public FrameData[] GetAuthoritativeHistorySnapshot()
{
    return _authoritativeHistory.ToArray();
}
~~~

Do not add Submit, an exhausted flag, or a Simulation field in this task.

- [ ] **Step 7: Run GREEN and build with warnings as errors**

Run:

~~~powershell
dotnet build Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj -c Release
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release
~~~

Expected: production build `0 warnings / 0 errors`, then `RESULT 4/4 passed`.

- [ ] **Step 8: Commit the assembly and constructor contract**

~~~powershell
git add -- .gitignore Server/LockstepArena.Server.FrameSync Tests/LockstepArena.Server.FrameSync.Tests
git diff --cached --check
git commit -m "build: add server frame sync project"
~~~

### Task 2: Route Valid Inputs Through Tick Windows and Gate 3 Collectors

**Files:**

- Modify: `Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs`
- Create: `Tests/LockstepArena.Server.FrameSync.Tests/CoordinatorRosterTests.cs`
- Create: `Tests/LockstepArena.Server.FrameSync.Tests/CoordinatorWindowTests.cs`
- Create: `Tests/LockstepArena.Server.FrameSync.Tests/CoordinatorRejectTests.cs`
- Modify: `Tests/LockstepArena.Server.FrameSync.Tests/Program.cs`

**Interfaces:**

- Consumes: Task 1 constructor/state and Gate 3 `StrictFrameCollector.Submit(PlayerId, InputFrame)`.
- Produces: `FrameData[] Submit(PlayerId submittedPlayerId, InputFrame input)` with range validation, lazy collector insertion, one-current-frame publication, and transactional rejection. Continuous multi-frame release replaces the one-frame publication in Task 3.

- [ ] **Step 1: Add three roster tests**

Register:

~~~csharp
new TestCase("TwoPlayerSameTickArrivalOrderIsCanonical", TwoPlayerSameTickArrivalOrderIsCanonical),
new TestCase("ThreePlayerSameTickArrivalOrderIsCanonical", ThreePlayerSameTickArrivalOrderIsCanonical),
new TestCase("FourPlayerSameTickArrivalOrderIsCanonical", FourPlayerSameTickArrivalOrderIsCanonical),
~~~

For each roster size, submit the current Tick's Slots in a non-ascending order. Assert every non-final submission returns an empty array, the final submission returns one FrameData, its Tick is the initial Tick, and `GetInput(new PlayerSlot(index))` returns the logical input for that Slot.

- [ ] **Step 2: Add three future-window tests**

Register:

~~~csharp
new TestCase("ZeroFutureOffsetAcceptsOnlyNextPublishTick", ZeroFutureOffsetAcceptsOnlyNextPublishTick),
new TestCase("FutureWindowIncludesUpperBoundAndRejectsBeyondIt", FutureWindowIncludesUpperBoundAndRejectsBeyondIt),
new TestCase("FutureWindowMovesAndPublishedTicksBecomeOld", FutureWindowMovesAndPublishedTicksBecomeOld),
~~~

Use initial Tick 100. Prove offset zero rejects Tick 101; offset two accepts a valid first input at Tick 102 but rejects Tick 103; after publishing Tick 100, prove Tick 103 becomes the new inclusive upper bound and a late Tick 100 input is rejected.

- [ ] **Step 3: Add six rejection tests**

Register:

~~~csharp
new TestCase("DuplicateSlotIsRejectedWithoutPublication", DuplicateSlotIsRejectedWithoutPublication),
new TestCase("UnknownPlayerIdIsRejectedWithoutPublication", UnknownPlayerIdIsRejectedWithoutPublication),
new TestCase("PlayerIdSlotMismatchIsRejectedWithoutPublication", PlayerIdSlotMismatchIsRejectedWithoutPublication),
new TestCase("RosterOutOfRangeSlotIsRejectedWithoutPublication", RosterOutOfRangeSlotIsRejectedWithoutPublication),
new TestCase("FirstInvalidSubmissionDoesNotPolluteNewTick", FirstInvalidSubmissionDoesNotPolluteNewTick),
new TestCase("RejectPreservesAcceptedInputsAndOtherPendingTicks", RejectPreservesAcceptedInputsAndOtherPendingTicks),
~~~

Each test snapshots `NextPublishTick` and history before rejection and checks both afterward. The first-invalid test rejects an unknown identity for a new future Tick and then completes that Tick legally. In this intermediate task, the final isolation test accepts Slot 0 at Tick 100, completes Tick 101, rejects a duplicate at Tick 100, submits Slot 1 at Tick 100, and asserts the one-frame result is Tick 100, `NextPublishTick` becomes 101, and history contains only Tick 100. Task 3 strengthens this same test to require `[100,101]`, which proves the completed future collector survived the rejection.

- [ ] **Step 4: Register the new groups and run RED**

Update `Program.cs`:

~~~csharp
TestCase[] tests = Combine(
    CoordinatorContractTests.All,
    CoordinatorRosterTests.All,
    CoordinatorWindowTests.All,
    CoordinatorRejectTests.All);
~~~

Run:

~~~powershell
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release
~~~

Expected: compilation fails because `Submit` does not exist.

- [ ] **Step 5: Implement validation and lazy per-Tick collectors**

Add the exact public method:

~~~csharp
public FrameData[] Submit(PlayerId submittedPlayerId, InputFrame input)
~~~

Validation begins with:

~~~csharp
if (input.Tick == uint.MaxValue)
{
    throw new ArgumentOutOfRangeException(nameof(input), "uint.MaxValue cannot be a consumable frame Tick.");
}

if (NextPublishTick == uint.MaxValue)
{
    throw new InvalidOperationException("The coordinator has no publishable Tick remaining.");
}

if (input.Tick < NextPublishTick)
{
    throw new ArgumentOutOfRangeException(nameof(input), "Input Tick is older than NextPublishTick.");
}

ulong upperBound = Math.Min(
    (ulong)NextPublishTick + _maxFutureTickOffset,
    (ulong)uint.MaxValue - 1UL);
if ((ulong)input.Tick > upperBound)
{
    throw new ArgumentOutOfRangeException(nameof(input), "Input Tick exceeds the accepted future window.");
}
~~~

For a missing Tick, create and submit a local collector before adding it:

~~~csharp
if (!_pendingByTick.TryGetValue(input.Tick, out StrictFrameCollector? collector))
{
    StrictFrameCollector candidate = new StrictFrameCollector(Roster, input.Tick);
    candidate.Submit(submittedPlayerId, input);
    _pendingByTick.Add(input.Tick, candidate);
    collector = candidate;
}
else
{
    collector.Submit(submittedPlayerId, input);
}
~~~

At this intermediate task, publish at most the current Tick when its collector is complete. Return a new one-element array, remove only that exact Tick, append it to bounded history, and advance `NextPublishTick` by one. A complete future collector returns `Array.Empty<FrameData>()`. Task 3 replaces this intermediate publication block with the approved multi-frame atomic planner.

- [ ] **Step 6: Run the focused suite and inspect rejection state**

Run:

~~~powershell
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release
~~~

Expected: `RESULT 16/16 passed`. If the final isolation test requires continuous release, assert the pre-Task-3 observable state in this commit and strengthen it to the final two-frame batch in Task 3 before its commit.

- [ ] **Step 7: Audit that Shared validation was not duplicated**

Run:

~~~powershell
rg -n "TryGetSlot|GetPlayerId|GetInput|InputCount|MoveX|MoveZ|Aim" Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs
~~~

Expected: no coordinator reimplementation of PlayerId ownership, Slot canonicalization, or input-value validation. `InputFrame.Tick` use is allowed only for Tick routing/window checks.

- [ ] **Step 8: Commit input routing**

~~~powershell
git add -- Server/LockstepArena.Server.FrameSync Tests/LockstepArena.Server.FrameSync.Tests
git diff --cached --check
git commit -m "feat: collect validated server frame inputs"
~~~

### Task 3: Publish Continuous Batches with Copied State Containers

**Files:**

- Modify: `Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs`
- Create: `Tests/LockstepArena.Server.FrameSync.Tests/CoordinatorPublicationTests.cs`
- Create: `Tests/LockstepArena.Server.FrameSync.Tests/CoordinatorHistoryTests.cs`
- Create: `Tests/LockstepArena.Server.FrameSync.Tests/CoordinatorTickLimitTests.cs`
- Modify: `Tests/LockstepArena.Server.FrameSync.Tests/CoordinatorRejectTests.cs`
- Modify: `Tests/LockstepArena.Server.FrameSync.Tests/Program.cs`

**Interfaces:**

- Consumes: Task 2 accepted per-Tick collectors.
- Produces: the final local-plan/final-replacement publication algorithm, bounded history snapshots, and exact exhausted semantics.

- [ ] **Step 1: Add five publication tests**

Register:

~~~csharp
new TestCase("CompleteFutureFrameWaitsForGap", CompleteFutureFrameWaitsForGap),
new TestCase("GapFillPublishesCurrentAndNextTick", GapFillPublishesCurrentAndNextTick),
new TestCase("GapFillPublishesSeveralCompletedFutureTicks", GapFillPublishesSeveralCompletedFutureTicks),
new TestCase("IncompleteMiddleTickStopsContinuousBatch", IncompleteMiddleTickStopsContinuousBatch),
new TestCase("PublicationBatchStartsAtPriorNextTickAndOwnsItsContainer", PublicationBatchStartsAtPriorNextTickAndOwnsItsContainer),
~~~

Use initial Tick 100 and future offset at least three. Complete Tick 101 before 100 and assert no publication. Complete 100 and assert `[100,101]`. Complete 103, 102, 101 before 100 and assert closing Tick 100 returns `[100,101,102,103]`. Leave 102 incomplete while 103 is complete and prove release stops after 101. Replace an element in the returned array and prove a history snapshot still contains the originally published frame reference at that position.

- [ ] **Step 2: Add four history tests**

Register:

~~~csharp
new TestCase("HistoryStartsEmptyAndContainsOnlyPublishedFrames", HistoryStartsEmptyAndContainsOnlyPublishedFrames),
new TestCase("BlockedCompleteFutureFrameIsAbsentFromHistory", BlockedCompleteFutureFrameIsAbsentFromHistory),
new TestCase("HistoryEvictsOldestFramesAtCapacity", HistoryEvictsOldestFramesAtCapacity),
new TestCase("HistorySnapshotOwnsItsContainer", HistorySnapshotOwnsItsContainer),
~~~

Use capacity two to publish three consecutive frames and assert retained Ticks are the latter two. Mutate the first snapshot array, obtain a second snapshot, and assert the Queue retained the original immutable FrameData references.

- [ ] **Step 3: Add three Tick-limit tests**

Register:

~~~csharp
new TestCase("UintMaxValueInputIsAlwaysRejected", UintMaxValueInputIsAlwaysRejected),
new TestCase("LastConsumableTickPublishesThenCoordinatorIsExhausted", LastConsumableTickPublishesThenCoordinatorIsExhausted),
new TestCase("LargeFutureOffsetNeverWrapsToLowTicks", LargeFutureOffsetNeverWrapsToLowTicks),
~~~

Construct at `uint.MaxValue - 1`, publish a complete roster frame, assert one result at that Tick and `NextPublishTick == uint.MaxValue`, then assert every further Submit fails. With a large future offset near the limit, prove Tick zero is old rather than wrapped into the future window.

- [ ] **Step 4: Strengthen the cross-Tick rejection test and run RED**

Make `RejectPreservesAcceptedInputsAndOtherPendingTicks` assert that completing Tick 100 after the duplicate rejection returns `[100,101]`.

Register all seven new groups in this exact order:

~~~csharp
TestCase[] tests = Combine(
    CoordinatorContractTests.All,
    CoordinatorRosterTests.All,
    CoordinatorWindowTests.All,
    CoordinatorRejectTests.All,
    CoordinatorPublicationTests.All,
    CoordinatorHistoryTests.All,
    CoordinatorTickLimitTests.All);
~~~

Run the suite. Expected: at least the two-frame and multi-frame publication tests fail because Task 2 publishes at most one Tick per Submit. Total registered tests must be 28.

- [ ] **Step 5: Replace the intermediate block with a local publication planner**

After the target collector successfully accepts the input, scan without mutating live state:

~~~csharp
List<FrameData> frames = new List<FrameData>();
ulong scanTick = NextPublishTick;
while (scanTick < uint.MaxValue)
{
    uint tick = (uint)scanTick;
    if (!_pendingByTick.TryGetValue(tick, out StrictFrameCollector? pending) ||
        !pending.IsComplete)
    {
        break;
    }

    frames.Add(pending.GetCompletedFrame());
    if (tick == uint.MaxValue - 1U)
    {
        scanTick = uint.MaxValue;
        break;
    }

    scanTick++;
}

if (frames.Count == 0)
{
    return Array.Empty<FrameData>();
}
~~~

Create every post-publication container before touching the live fields:

~~~csharp
FrameData[] publication = frames.ToArray();
uint nextPublishTickAfterBatch = checked((uint)scanTick);

Dictionary<uint, StrictFrameCollector> pendingAfter =
    new Dictionary<uint, StrictFrameCollector>(_pendingByTick);
for (int index = 0; index < publication.Length; index++)
{
    if (!pendingAfter.Remove(publication[index].Tick))
    {
        throw new InvalidOperationException("A planned publication Tick was absent from pending storage.");
    }
}

Queue<FrameData> historyAfter = new Queue<FrameData>(_authoritativeHistory);
for (int index = 0; index < publication.Length; index++)
{
    historyAfter.Enqueue(publication[index]);
    while (historyAfter.Count > _authoritativeHistoryCapacity)
    {
        historyAfter.Dequeue();
    }
}
~~~

Only then commit:

~~~csharp
_pendingByTick = pendingAfter;
_authoritativeHistory = historyAfter;
NextPublishTick = nextPublishTickAfterBatch;
return publication;
~~~

Do not call `FrameData.Create` in this Server class. Do not clone FrameData or pending collectors.

- [ ] **Step 6: Run all 28 tests and the Gate 3 regression suite**

Run:

~~~powershell
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release
~~~

Expected: `RESULT 28/28 passed`, then `RESULT 38/38 passed`.

- [ ] **Step 7: Inspect the production algorithm for forbidden incremental mutation**

Run:

~~~powershell
rg -n "_pendingByTick\.Remove|_authoritativeHistory\.(Enqueue|Dequeue|Clear)|NextPublishTick\+\+|NextPublishTick \+=" Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs
~~~

Expected: no mutation of the live pending/history containers and no live Tick increment during scanning. Assignments to the three planned replacement values occur together at the end of the method.

- [ ] **Step 8: Commit continuous publication**

~~~powershell
git add -- Server/LockstepArena.Server.FrameSync Tests/LockstepArena.Server.FrameSync.Tests
git diff --cached --check
git commit -m "feat: publish continuous authoritative frame batches"
~~~

### Task 4: Prove Arrival-Order Independence with the Frozen Golden Vector

**Files:**

- Create: `Tests/LockstepArena.Server.FrameSync.Tests/Gate4MultiTickGoldenVector.cs`
- Create: `Tests/LockstepArena.Server.FrameSync.Tests/CoordinatorDeterminismTests.cs`
- Modify: `Tests/LockstepArena.Server.FrameSync.Tests/Program.cs`

**Interfaces:**

- Consumes: final Coordinator API, Gate 3 BattleState/BattleSimulation/StateDigest, and the approved 12-Tick logical inputs.
- Produces: exact 32/32 suite, batch-pattern evidence, flattened authoritative equality, per-Tick dual-Simulation digest equality, history evidence, and final digest `0x5CFABE84CC00E1C3`.

- [ ] **Step 1: Add four determinism/composition tests against the approved vector API**

Register:

~~~csharp
new TestCase("DifferentArrivalOrdersPublishIdenticalAuthoritativeFrames", DifferentArrivalOrdersPublishIdenticalAuthoritativeFrames),
new TestCase("PublishedBatchCanLeadSimulationThenCatchUp", PublishedBatchCanLeadSimulationThenCatchUp),
new TestCase("DualSimulationsMatchEveryDigestAndApprovedGolden", DualSimulationsMatchEveryDigestAndApprovedGolden),
new TestCase("SimulationFailureDoesNotRollbackCoordinatorPublication", SimulationFailureDoesNotRollbackCoordinatorPublication),
~~~

Write these tests against these exact internal methods:

~~~csharp
internal static CoordinatorRunResult RunCoordinatorA();
internal static CoordinatorRunResult RunCoordinatorB();
internal static (
    AuthoritativeFrameCoordinator Coordinator,
    BattleSimulation Simulation,
    FrameData[] Publication) CreateCoordinatorAFirstBlock();
~~~

The first test independently asserts A batch sizes `{4,4,4}`, B batch sizes `{1,3,1,3,1,3}`, 12 flattened frames, Tick `0..11`, equal roster structure, and equal per-Slot InputFrames.

The second test uses `Gate4MultiTickGoldenVector.CreateCoordinatorAFirstBlock()` to obtain a coordinator, its four-frame returned batch, and an unadvanced Simulation. Assert Coordinator Tick 4 while Simulation Tick remains 0, then step the returned frames and assert both reach Tick 4.

The third test asserts all 12 recorded digest pairs are equal, histories are Tick `{7,8,9,10,11}`, final Tick 12, the approved final states, and:

~~~csharp
TestAssert.Equal(0x5CFABE84CC00E1C3UL, StateDigest.Compute(runA.FinalState));
TestAssert.Equal(0x5CFABE84CC00E1C3UL, StateDigest.Compute(runB.FinalState));
~~~

The fourth test publishes one valid frame, sends it to a Simulation with a different current Tick or structurally different roster, asserts `Simulation.Step` throws, then proves Coordinator `NextPublishTick`, history, and the already returned authoritative frame remain committed.

- [ ] **Step 2: Register the final group and run RED**

Add `CoordinatorDeterminismTests.All` last in `Program.Combine`. Run:

~~~powershell
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release
~~~

Expected: compilation fails because `Gate4MultiTickGoldenVector`, `CoordinatorRunResult`, and the approved vector methods do not exist. The registered final count is 32.

- [ ] **Step 3: Implement the pure actual-result vector with no expected literals**

`Gate4MultiTickGoldenVector.cs` defines actual-result containers for each run:

~~~csharp
internal sealed class CoordinatorRunResult
{
    public CoordinatorRunResult(
        int[] publicationBatchSizes,
        FrameData[] publishedFrames,
        FrameData[] history,
        BattleState finalState,
        ulong[] digestsAfterEachFrame)
    {
        PublicationBatchSizes = publicationBatchSizes;
        PublishedFrames = publishedFrames;
        History = history;
        FinalState = finalState;
        DigestsAfterEachFrame = digestsAfterEachFrame;
    }

    public int[] PublicationBatchSizes { get; }
    public FrameData[] PublishedFrames { get; }
    public FrameData[] History { get; }
    public BattleState FinalState { get; }
    public ulong[] DigestsAfterEachFrame { get; }
}
~~~

Create separate structurally equal rosters for each Coordinator and Simulation using the four approved PlayerIds. Create the four approved initial states. For Tick `0..11`, generate Inputs with constant movement and these aim formulas:

~~~csharp
unchecked((ushort)((tick * 1_000U) + 1U))
unchecked((ushort)((tick * 2_000U) + 2U))
unchecked((ushort)((tick * 3_000U) + 3U))
unchecked((ushort)((tick * 4_000U) + 4U))
~~~

Run Coordinator A in four-Tick blocks with Tick offsets `{3,2,1,0}` and Slot order `{2,0,3,1}`. Run Coordinator B with Slot passes `{1,3,0,2}` and Tick offsets `{2,0,3,1}`. Append only non-empty batch lengths, flatten every publication array, immediately feed each returned FrameData to its matching Simulation, and record StateDigest after every Step.

Implement `RunCoordinatorA`, `RunCoordinatorB`, and `CreateCoordinatorAFirstBlock` with the exact signatures from Step 1. The first-block method returns after A has published Tick 0 through 3 but before its Simulation consumes them. The vector file must not contain final X/Z/Aim literals, expected batch arrays, expected history Tick arrays, or `5CFABE84CC00E1C3`.

- [ ] **Step 4: Run all deterministic evidence**

Run:

~~~powershell
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release
dotnet run --project Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj -c Release
~~~

Expected:

~~~text
RESULT 32/32 passed
RESULT 38/38 passed
PASS Gate3GoldenVector Tick=1000 Players=4 Digest=89A7DD66F8D9E871
~~~

- [ ] **Step 5: Prove expected values are absent from the actual vector**

Run:

~~~powershell
$vector = 'Tests/LockstepArena.Server.FrameSync.Tests/Gate4MultiTickGoldenVector.cs'
$forbidden = rg -n "5CFABE84CC00E1C3|11_001|22_002|33_003|44_004|new\[\] \{ 4, 4, 4 \}|new\[\] \{ 1, 3, 1, 3, 1, 3 \}|7U, 8U, 9U, 10U, 11U" $vector
if ($LASTEXITCODE -eq 0) { $forbidden; throw 'Golden vector contains expected-result literals.' }
if ($LASTEXITCODE -gt 1) { throw 'Golden purity scan failed to run.' }
rg -n "5CFABE84CC00E1C3|11_001|22_002|33_003|44_004" Tests/LockstepArena.Server.FrameSync.Tests/CoordinatorDeterminismTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Golden consumer expected literals are missing.' }
~~~

- [ ] **Step 6: Commit the Golden evidence**

~~~powershell
git add -- Tests/LockstepArena.Server.FrameSync.Tests
git diff --cached --check
git commit -m "test: add authoritative multi-tick golden vector"
~~~

### Task 5: Run the Full Gate 4 Regression and Scope Audit

**Files:**

- Modify: `Docs/Architecture/GATE4_AUTHORITATIVE_MULTI_TICK_FRAME_PIPELINE.md`

**Interfaces:**

- Consumes: completed Server coordinator, 32-test suite, unchanged Gate 3 code and tests, and Git history.
- Produces: fresh build/runtime/XML/audit evidence and an evidence-only documentation commit.

- [ ] **Step 1: Build every .NET deliverable in Release**

Run:

~~~powershell
dotnet build Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj -c Release
dotnet build Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj -c Release
dotnet build Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release
dotnet build Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release
dotnet build Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj -c Release
~~~

Expected: every build reports `0 warnings / 0 errors`.

- [ ] **Step 2: Execute both suites and the Gate 3 Server verifier fresh**

~~~powershell
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release --no-restore
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj -c Release --no-restore
dotnet run --project Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj -c Release --no-restore
~~~

Expected: `38/38`, `32/32`, and the unchanged Gate 3 PASS line.

- [ ] **Step 3: Run the unchanged Gate 3 Unity test and parse NUnit XML**

First ensure no Unity instance is active. Do not terminate one automatically.

~~~powershell
$unity = 'E:\unityhub\unity6.3\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $unity)) { throw "Unity executable not found: $unity" }
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'A Unity instance is active; stop and report.' }
$projectPath = (Get-Location).ProviderPath
$results = Join-Path $env:TEMP 'locksteparena-gate4-editmode-results.xml'
$editorLog = Join-Path $env:TEMP 'locksteparena-gate4-editor.log'
Remove-Item -LiteralPath $results -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $editorLog -Force -ErrorAction SilentlyContinue
$arguments = @(
    '-batchmode', '-runTests',
    '-projectPath', $projectPath,
    '-testPlatform', 'EditMode',
    '-assemblyNames', 'LockstepArena.Simulation.Editor.Tests',
    '-testResults', $results,
    '-logFile', $editorLog)
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $editorLog -Tail 200; throw "Unity failed: $($process.ExitCode)" }
[xml]$testXml = Get-Content -LiteralPath $results -Raw
$run = $testXml.'test-run'
if ([int]$run.total -lt 1 -or [int]$run.failed -ne 0) { throw 'Unity XML counts are not passing.' }
$case = $testXml.SelectSingleNode("//test-case[contains(@fullname, 'UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector')]")
if ($null -eq $case -or $case.result -ne 'Passed') { throw 'The Gate 3 named Unity test did not pass.' }
Write-Output "Unity Gate 4 regression: total=$($run.total) passed=$($run.passed) failed=$($run.failed) test=$($case.fullname)"
~~~

Inspect `git status --short` and exact diffs. Restore only Unity-generated worktree-local changes under Assets or ProjectSettings after inspection. Do not use broad reset/clean commands and never run Unity from the ordinary checkout.

- [ ] **Step 4: Prove Shared package immutability and project-scope isolation**

~~~powershell
$base = '0137342be01d15ae52f437ef53a9fdd0f3437c85'
$packageDiff = git diff --name-only "$base..HEAD" -- Packages/com.locksteparena.simulation
if ($packageDiff) { $packageDiff; throw 'Gate 4 changed the Shared package.' }
$prohibited = git diff --name-only "$base..HEAD" -- Assets ProjectSettings Packages/manifest.json
if ($prohibited) { $prohibited; throw 'Gate 4 changed prohibited Unity project content.' }
git diff --check "$base..HEAD"
if ($LASTEXITCODE -ne 0) { throw 'Committed diff contains whitespace errors.' }
~~~

- [ ] **Step 5: Prove production dependency and scope boundaries**

~~~powershell
$production = 'Server/LockstepArena.Server.FrameSync'
$forbidden = rg -n -i "UnityEngine|UnityEditor|\bNUnit\b|PackageReference|System\.Net|Socket|\bTcp\b|\bUdp\b|\bKcp\b|Protobuf|Room|Login|Session|TickClock|DateTime|Stopwatch|Timer|Timeout|Task|Thread|lock\s*\(|Concurrent|Channel|InputDelay|MissingInput|Prediction|Snapshot|Rollback|\bReplay\b|GameObject|Transform|Projectile|Combat|Damage|Health" $production
if ($LASTEXITCODE -eq 0) { $forbidden; throw 'Forbidden dependency or later-gate capability found.' }
if ($LASTEXITCODE -gt 1) { throw 'Production scope scan failed to run.' }
$simulationOwnership = rg -n "BattleSimulation|StateDigest|FrameData\.Create|TryGetSlot|GetPlayerId" $production
if ($LASTEXITCODE -eq 0) { $simulationOwnership; throw 'Coordinator duplicated or assumed Shared/Simulation ownership.' }
if ($LASTEXITCODE -gt 1) { throw 'Ownership scan failed to run.' }
$pendingEnumeration = rg -n "_pendingByTick\.(Keys|Values|GetEnumerator)|foreach\s*\([^\)]*_pendingByTick|OrderBy|SortedDictionary" $production
if ($LASTEXITCODE -eq 0) { $pendingEnumeration; throw 'Pending publication depends on collection enumeration.' }
if ($LASTEXITCODE -gt 1) { throw 'Pending enumeration scan failed to run.' }
Write-Output 'Server authority dependency/ownership/order audit: PASS'
~~~

- [ ] **Step 6: Prove copied-container publication and no deep clone**

~~~powershell
rg -n "new Dictionary<uint, StrictFrameCollector>\(_pendingByTick\)|new Queue<FrameData>\(_authoritativeHistory\)|frames\.ToArray\(\)" Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs
if ($LASTEXITCODE -ne 0) { throw 'Required copied publication containers are absent.' }
$deepCopy = rg -n "FrameData\.Create|new StrictFrameCollector\([^\)]*pending|Clone\(" Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs
if ($LASTEXITCODE -eq 0) { $deepCopy; throw 'Unexpected deep-copy implementation found.' }
if ($LASTEXITCODE -gt 1) { throw 'Deep-copy scan failed to run.' }
~~~

The allowed `new StrictFrameCollector(Roster, input.Tick)` for a newly observed Tick must be inspected separately; the scan must not mistake that required creation for cloning an existing pending collector.

- [ ] **Step 7: Prove tracked source, link, script, and artifact boundaries**

~~~powershell
$coordinators = @(git ls-files | Where-Object { $_ -like '*AuthoritativeFrameCoordinator.cs' })
if ($coordinators.Count -ne 1 -or $coordinators[0] -ne 'Server/LockstepArena.Server.FrameSync/AuthoritativeFrameCoordinator.cs') { throw "Unexpected coordinator paths: $coordinators" }
$projects = @(
    'Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj',
    'Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj')
foreach ($project in $projects) { if (-not (git ls-files --error-unmatch $project 2>$null)) { throw "Authored project is not tracked: $project" } }
$links = git ls-files --stage Server/LockstepArena.Server.FrameSync Tests/LockstepArena.Server.FrameSync.Tests | Select-String '^120000 '
if ($links) { $links; throw 'Tracked symlink found.' }
$filesystemLinks = Get-ChildItem -LiteralPath 'Server/LockstepArena.Server.FrameSync','Tests/LockstepArena.Server.FrameSync.Tests' -Recurse -Force | Where-Object { $null -ne $_.LinkType }
if ($filesystemLinks) { $filesystemLinks; throw 'Filesystem link or junction found.' }
$scripts = git diff --name-only 0137342be01d15ae52f437ef53a9fdd0f3437c85..HEAD | Where-Object { $_ -match '\.(ps1|sh|cmd|bat|py)$' }
if ($scripts) { $scripts; throw 'Gate 4 added a script.' }
$packageArtifacts = Get-ChildItem -LiteralPath 'Packages/com.locksteparena.simulation' -Recurse -Force | Where-Object { $_.Name -in @('bin','obj') -or $_.Extension -eq '.dll' }
if ($packageArtifacts) { $packageArtifacts.FullName; throw 'Artifact exists inside the Unity package.' }
Write-Output 'source/link/script/artifact audit: PASS'
~~~

- [ ] **Step 8: Confirm the normal checkout still contains only its two user changes**

Run from the Gate 4 worktree without modifying the ordinary checkout:

~~~powershell
$normal = git -C E:/unityproject/LockstepArena status --short
$expected = @(
    ' M Assets/Settings/Mobile_RPAsset.asset',
    ' M ProjectSettings/ShaderGraphSettings.asset')
if (@($normal).Count -ne 2 -or $normal[0] -ne $expected[0] -or $normal[1] -ne $expected[1]) { $normal; throw 'Normal checkout state changed.' }
~~~

- [ ] **Step 9: Record fresh evidence in the Gate 4 architecture document**

Change status to implementation complete pending independent approval and append an Implementation Evidence section containing:

- approved base and every implementation commit ID;
- all five Release build warning/error counts;
- exact `38/38`, `32/32`, and Gate 3 Server output;
- Unity executable/arguments/XML counts/named test;
- A/B batch sizes and flattened Tick range;
- final history, final state, and `0x5CFABE84CC00E1C3`;
- per-Tick dual-Simulation digest result;
- atomic planner, no-enumeration, Shared immutability, dependency, deep-copy, source/link/script/artifact, project-scope, and normal-checkout audit results;
- exact Unity serialization paths inspected and restored, if any.

- [ ] **Step 10: Commit evidence only**

~~~powershell
git add -- Docs/Architecture/GATE4_AUTHORITATIVE_MULTI_TICK_FRAME_PIPELINE.md
git diff --cached --check
git commit -m "docs: record Gate 4 authority pipeline evidence"
~~~

### Task 6: Push and Stop at the Gate

**Files:** None.

**Interfaces:**

- Consumes: clean, fully verified Gate 4 branch.
- Produces: remote branch matching local HEAD and a Planning-approved implementation Handoff; no merge, PR, cleanup, or Gate 5 work.

- [ ] **Step 1: Verify final ancestry and clean status**

~~~powershell
if (-not (git merge-base --is-ancestor 0137342be01d15ae52f437ef53a9fdd0f3437c85 HEAD)) { throw 'Approved Gate 3 base is not an ancestor.' }
if (git status --porcelain) { git status --short; throw 'Gate 4 worktree is not clean.' }
git log --oneline 0137342be01d15ae52f437ef53a9fdd0f3437c85..HEAD
~~~

- [ ] **Step 2: Push without force**

~~~powershell
git push -u origin codex/gate4-authoritative-frame-pipeline
~~~

- [ ] **Step 3: Verify the exact remote SHA**

~~~powershell
$local = git rev-parse HEAD
$remoteLine = git ls-remote --heads origin refs/heads/codex/gate4-authoritative-frame-pipeline
$remote = ($remoteLine -split '\s+')[0]
if ($local -ne $remote) { throw "Remote mismatch: local=$local remote=$remote" }
Write-Output "Gate 4 remote verified: $remote"
~~~

- [ ] **Step 4: Submit the implementation Handoff and stop**

Report branch, approved base, final commit, commit list, build/test/XML evidence, Golden batches/state/history/digest, audits, clean worktree, unchanged normal checkout, and remote equality. Explicitly state that Protobuf, networking, time policy, missing-input policy, rooms, prediction, recovery, view, combat, and Gate 5 remain absent.

Do not merge, create a PR, remove the worktree, or begin the next Gate before independent approval.
