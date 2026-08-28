# Gate 1 Deterministic Simulation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan.

**Goal:** Prove that two independent Unity-free Shared simulations starting from the same initial state and consuming the same ordered frame history produce identical canonical state digests at every tick.

**Architecture:** Add one dependency-free .NET Standard class library containing immutable integer-domain contracts, a two-player deterministic stepper, and an explicitly encoded FNV-1a 64-bit state digest. Add one dependency-free .NET 8 executable test project with a tiny in-process runner. Formal replay, networking, serialization, Unity integration, prediction, snapshot, rollback, combat, and speculative abstractions remain absent.

**Tech Stack:** C# with the .NET Standard 2.1 API surface for production code; .NET 8 executable for tests; SDK-style projects; no NuGet packages.

**Spec:** Docs/Architecture/LOCKSTEP_REFERENCE_STUDY.md section 16 plus the approved Gate 1 constraints.

## Global Constraints

- Make every implementation edit inside .worktrees/gate1-deterministic-simulation.
- Keep Source free of UnityEngine and all package references.
- Use scaled integers directly; do not create a general fixed-point framework.
- Keep Simulation domain types independent of future Protobuf-generated types.
- Do not add sockets, TCP, KCP, Protobuf, combat, prediction, snapshot, rollback, reconnect, or a production replay system.
- Do not use Dictionary, HashSet, LINQ ordering, reflection, runtime hash codes, or unordered iteration in Simulation.Step or state digest generation.
- A test may reconstruct state by looping over initial state plus a List<FrameData>; no production Replay type is allowed.
- Observe a failing test command before adding each corresponding production capability.

## Fixed Gate 1 Decisions

- Tick rate: 30 Hz.
- Position scale: 1,000 integer units per meter.
- Movement: 100 units per tick on each commanded axis. Diagonal input intentionally moves 100 on both axes; normalization is deferred because it would require a broader numeric policy.
- Arena: X from -5,000 through 5,000; Z from -3,000 through 3,000, inclusive.
- Initial positions: player slot 0 at X -1,000, Z 0; player slot 1 at X 1,000, Z 0.
- Input movement components: signed integers restricted to -1, 0, or 1.
- Aim: an unsigned 16-bit full-turn value copied to state; trigonometry and aiming gameplay are out of scope.
- Frame order: exactly slots 0 then 1, regardless of constructor argument order.
- Digest: FNV-1a 64-bit over explicitly emitted little-endian bytes in this order: state tick, player 0 X, Z, aim, player 1 X, Z, aim. Signed positions are emitted as their unchecked 32-bit two's-complement bit patterns.

## Task 1: Create Dependency-Free Project Shells and Contract Tests

**Files:**

- Create: Source/LockstepArena.Simulation/LockstepArena.Simulation.csproj
- Create: Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj
- Create: Tests/LockstepArena.Simulation.Tests/Program.cs
- Create: Tests/LockstepArena.Simulation.Tests/TestAssert.cs
- Create: Tests/LockstepArena.Simulation.Tests/ContractTests.cs

### Step 1: Create the project shells

The production project targets netstandard2.1, enables nullable reference analysis, disables implicit usings, and contains no package references. The test executable targets net8.0, references only the production project, and contains no test framework package.

### Step 2: Write the first failing contract tests

Register focused tests proving:

- swapped constructor arguments are exposed as slot 0 then slot 1;
- FrameData rejects duplicate slots and different ticks;
- InputFrame rejects any slot other than 0/1 and movement outside -1..1;
- BattleState.CreateInitial produces tick zero and the documented spawn positions.

The desired production API is:

~~~csharp
public readonly struct InputFrame
{
    public InputFrame(uint tick, byte playerSlot, sbyte moveX, sbyte moveZ, ushort aim);
    public uint Tick { get; }
    public byte PlayerSlot { get; }
    public sbyte MoveX { get; }
    public sbyte MoveZ { get; }
    public ushort Aim { get; }
}

public readonly struct FrameData
{
    public FrameData(InputFrame first, InputFrame second);
    public uint Tick { get; }
    public InputFrame Player0Input { get; }
    public InputFrame Player1Input { get; }
}

public readonly struct PlayerState
{
    public PlayerState(int positionX, int positionZ, ushort aim);
    public int PositionX { get; }
    public int PositionZ { get; }
    public ushort Aim { get; }
}

public readonly struct BattleState
{
    public BattleState(uint tick, PlayerState player0, PlayerState player1);
    public uint Tick { get; }
    public PlayerState Player0 { get; }
    public PlayerState Player1 { get; }
    public static BattleState CreateInitial();
}
~~~

### Step 3: Run the tests and confirm RED

Run:

~~~powershell
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release
~~~

Expected: compilation fails because the contract types do not exist.

### Step 4: Implement only the contract types and constants

**Files:**

- Create: Source/LockstepArena.Simulation/SimulationConfig.cs
- Create: Source/LockstepArena.Simulation/InputFrame.cs
- Create: Source/LockstepArena.Simulation/FrameData.cs
- Create: Source/LockstepArena.Simulation/PlayerState.cs
- Create: Source/LockstepArena.Simulation/BattleState.cs

Use ArgumentOutOfRangeException for invalid input ranges and ArgumentException for invalid frame composition. FrameData must store two explicit fields; do not introduce a collection.

### Step 5: Run the tests and confirm GREEN

Run the same test command. Expected: all contract tests pass.

## Task 2: Add the Integer Step Pipeline

**Files:**

- Modify: Tests/LockstepArena.Simulation.Tests/Program.cs
- Create: Tests/LockstepArena.Simulation.Tests/BattleSimulationTests.cs
- Create: Source/LockstepArena.Simulation/BattleSimulation.cs

### Step 1: Write failing step tests

Register tests proving:

- neutral input advances exactly one tick without moving either player;
- opposing movement updates the two explicit player values independently;
- aim is copied from each input to the corresponding player state;
- movement clamps to all arena bounds;
- a frame whose tick differs from the current state tick is rejected.

Desired API:

~~~csharp
public sealed class BattleSimulation
{
    public BattleSimulation(BattleState initialState);
    public BattleState State { get; }
    public void Step(FrameData frame);
}
~~~

### Step 2: Run and confirm RED

Expected: compilation fails because BattleSimulation does not exist.

### Step 3: Implement the smallest stepper

Step validates the exact expected tick, updates player 0 then player 1 with integer addition and explicit min/max clamp, and replaces State with tick + 1. It must not contain collection iteration, delta time, floating point, collision, gameplay events, or extension hooks.

### Step 4: Run and confirm GREEN

Expected: contract and step tests all pass.

## Task 3: Add Canonical Digest and Determinism Evidence

**Files:**

- Modify: Tests/LockstepArena.Simulation.Tests/Program.cs
- Create: Tests/LockstepArena.Simulation.Tests/DeterminismTests.cs
- Create: Source/LockstepArena.Simulation/StateDigest.cs

### Step 1: Write failing digest, twin, and history tests

Register tests proving:

- equal states have equal digests;
- changing aim or position changes the digest;
- a fixed documented state has a fixed golden digest value;
- two separately constructed simulations receive the same logical deterministic inputs for 10,000 ticks, but construct FrameData arguments in opposite orders, and match digest after every tick;
- rebuilding from the original initial state plus the recorded List<FrameData> produces the original final digest;
- the scripted run includes neutral input, opposing movement, direction changes, and boundary clamps.

Desired API:

~~~csharp
public static class StateDigest
{
    public static ulong Compute(BattleState state);
}
~~~

The history test performs an ordinary loop in test code. Do not create Replay, FrameHistory, Snapshot, or serializer production types.

### Step 2: Run and confirm RED

Expected: compilation fails because StateDigest does not exist.

### Step 3: Implement explicit FNV-1a 64

Emit every field byte explicitly in little-endian order using fixed constants. Do not hash object representations, use GetHashCode, allocate a serializer buffer, or depend on platform endianness.

### Step 4: Establish and lock the golden digest

Run the algorithm independently against the fixed test vector, replace the test's temporary expected value with the computed hexadecimal constant, and rerun. The golden value must be a literal in the test so later digest-order changes are visible.

### Step 5: Run and confirm GREEN repeatedly

Run the full executable test suite at least three times. Expected: every run reports the same count and all tests pass.

## Task 4: Document the Slice and Audit the Boundary

**Files:**

- Create: Docs/Architecture/GATE1_DETERMINISTIC_SIMULATION.md

### Step 1: Document decisions and evidence

Record the 30 Hz/unit/bounds/input/aim/digest decisions, source/test layout, exact test command, acceptance evidence, and explicit deferred list. State clearly that the replay-style test is only a determinism oracle and no formal replay feature exists.

### Step 2: Build production and tests

Run:

~~~powershell
dotnet build Source/LockstepArena.Simulation/LockstepArena.Simulation.csproj -c Release
dotnet build Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release
~~~

Expected: zero build errors, zero warnings, all tests pass.

### Step 3: Run scope and dependency audits

Run searches over Source and Tests for UnityEngine, package references, networking terms, Protobuf, combat, prediction, snapshot, rollback, formal replay types, Dictionary, and HashSet. Inspect every changed path relative to the approved Gate 0 commit. Expected: only the planned ignore rule, plan, documentation, Source, and Tests appear; no forbidden implementation exists.

### Step 4: Review Git status and diff

Confirm the normal checkout remains clean, the Gate 1 worktree contains only intentional changes, and the branch ancestry still descends directly from 2dc086859ddebab20a6861b6b0ad3d94f8e83d8f.

## Task 5: Commit, Push, and Stop at the Gate

### Step 1: Commit the verified Gate 1 slice

Use narrowly described commits and inspect each resulting commit. Do not amend unrelated history.

### Step 2: Push the exact branch

Push codex/gate1-deterministic-simulation to origin and verify the remote ref resolves to the local HEAD.

### Step 3: Produce the Gate Handoff

Report branch, commit, approved base, changed files, design decisions, test commands and results, twin per-tick digest evidence, history reconstruction evidence, dependency/scope audit, known limitations, and the proposed Gate 2 boundary. Stop all implementation and wait for independent ChatGPT approval.
