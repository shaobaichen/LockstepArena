# Lockstep Arena — Gate 1 Deterministic Simulation

> Status: implementation complete pending independent Gate approval
>
> Scope: offline, Unity-free Shared Simulation learning slice only

## 1. Outcome

Gate 1 proves the approved invariant: two independent simulations starting from the same initial BattleState and consuming the same logical inputs produce the same canonical digest after every tested tick.

The slice is deliberately small. Production code contains domain contracts, an integer movement Step pipeline, arena clamp, aim state, and a canonical digest. It contains no Unity integration, networking, serialization, combat, prediction, snapshot, rollback, or replay subsystem.

## 2. Deterministic Domain Decisions

| Decision | Gate 1 value | Reason |
|---|---:|---|
| Tick rate | 30 Hz | Matches the approved study and is sufficient for a small 1v1 learning slice. Timing orchestration is deferred; Step itself accepts one exact tick. |
| Position scale | 1,000 units per meter | Provides millimetre-scale integer coordinates without a fixed-point framework. |
| Movement | 100 units per tick per commanded axis | Keeps the first deterministic rule explicit. Diagonal normalization is deferred until a broader numeric policy is justified. |
| Arena X | -5,000 through 5,000 inclusive | Small bounded test arena. |
| Arena Z | -3,000 through 3,000 inclusive | Small bounded test arena. |
| Player 0 spawn | X -1,000, Z 0 | Explicit canonical initial state. |
| Player 1 spawn | X 1,000, Z 0 | Explicit canonical initial state. |
| Movement input | sbyte restricted to -1, 0, or 1 on X/Z | Rejects ambiguous magnitudes before Step. |
| Aim | ushort full-turn value | Stores a deterministic orientation input without floats or trigonometry. |
| Input order | explicit slot 0 then slot 1 | FrameData canonicalizes constructor arguments and Step never iterates an unordered collection. |

The core model is ordinary C# and has no dependency on generated protocol types. A future protocol layer must translate into these domain values at its boundary.

## 3. Production Layout

~~~text
Source/LockstepArena.Simulation/
  SimulationConfig.cs       fixed Gate 1 constants
  InputFrame.cs             validated input for one player and tick
  FrameData.cs              exactly two inputs in canonical slot order
  PlayerState.cs            integer X/Z and ushort aim
  BattleState.cs            tick plus two explicit player states
  BattleSimulation.cs       deterministic Step pipeline
  StateDigest.cs            canonical FNV-1a 64 digest
~~~

LockstepArena.Simulation targets .NET Standard 2.1, has no package references, and uses a C# 9-compatible language surface. It does not reference UnityEngine.

## 4. Step Contract

BattleSimulation.Step performs the following fixed order:

1. Reject a FrameData whose tick is not exactly the current BattleState tick.
2. Apply player slot 0 movement and aim.
3. Apply player slot 1 movement and aim.
4. Clamp each integer X/Z coordinate to the documented arena bounds.
5. Replace state with a new immutable BattleState at tick + 1.

FrameData owns two explicit InputFrame values. It rejects different ticks and duplicate player slots, then exposes Player0Input and Player1Input in canonical order regardless of constructor argument order. No collection participates in Step.

## 5. Canonical Digest

StateDigest.Compute uses FNV-1a 64 with offset basis 14695981039346656037 and prime 1099511628211. It emits values byte by byte in explicit little-endian order:

~~~text
BattleState.Tick          uint32
Player0.PositionX         int32 two's-complement bits
Player0.PositionZ         int32 two's-complement bits
Player0.Aim               uint16
Player1.PositionX         int32 two's-complement bits
Player1.PositionZ         int32 two's-complement bits
Player1.Aim               uint16
~~~

The implementation does not use object memory layout, runtime GetHashCode, platform endianness, reflection, serialization, or an allocation buffer. A golden vector locks the field and byte order:

~~~text
Tick 0x01020304
P0 (-1, 0x01020304, aim 0xABCD)
P1 (5000, -3000, aim 0xFFFF)
Digest 0x6123AD83F7831D54
~~~

## 6. Test Evidence

The test project is a dependency-free .NET 8 executable. Its runner returns a non-zero process exit code on any failure.

| Evidence | Executed behavior |
|---|---|
| Contract guards | Canonical slot order, duplicate-slot rejection, tick mismatch rejection, bounded movement input, documented initial state. |
| Step behavior | Neutral input, opposing movement, aim replacement, all four arena clamps, wrong-tick rejection without state mutation. |
| Digest guards | Equal state equality, position/aim sensitivity, hand-derived golden digest. |
| Twin simulation | Two independent instances run 10,000 ticks. Every tick uses the same logical inputs but opposite FrameData constructor argument order; digests are compared after every Step. |
| History reconstruction | An original run records 2,000 FrameData values. A new simulation starts from the same initial state, consumes that list, and reaches the same final digest. |

The scripted input cycles include no-input frames, opposing movement, direction changes, all arena clamps, and changing aim values.

Run the suite with:

~~~powershell
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release
~~~

Expected result at this Gate: RESULT 15/15 passed.

## 7. Scope Boundary

Explicitly deferred:

- Unity scene, GameObjects, Transform view, rendering, and interpolation;
- TCP, UDP, KCP, sockets, sessions, rooms, server frame collection, input delay, and network simulation;
- Protobuf schemas, generated types, or serialization adapters;
- projectile, collision, hit, damage, health, death, respawn, score, or battle result;
- prediction, dirty-frame detection, snapshot, rollback, catch-up, reconnect, and desync recovery;
- formal FrameHistory storage and user-facing replay.

The history reconstruction test is only a determinism oracle. It deliberately loops over List<FrameData> in test code and does not introduce a second gameplay path or a production Replay type.

## 8. Gate Decision Requested

Independent review should verify:

1. the domain model remains small and protocol-independent;
2. canonical ordering and digest encoding are sufficiently explicit;
3. twin per-tick and initial-state-plus-history evidence satisfy Gate 1;
4. no later-gate capability entered Source;
5. Gate 2 receives a separately approved narrow scope before any further implementation.

No Gate 2 work begins until the Owner receives independent ChatGPT approval.
