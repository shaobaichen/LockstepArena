# Gameplay Sample Step 1 Implementation Plan

> This file is an internal execution plan for the already-approved **official Step 1**. Its internal tasks are not extra project Steps, Gates, checkpoints, or review stops. Codex executes them continuously and stops only once the entire official Step 1 is complete.

**Goal:** Add the deterministic 1v1 projectile-arena gameplay core to the existing Lockstep Arena simulation and make the already-existing prediction / Dirty / rollback / re-simulation / replay stack operate on that gameplay state without creating a second synchronization stack.

**Approved design:** `Docs/Architecture/LOCKSTEP_ARENA_GAMEPLAY_SAMPLE_V1_DESIGN.md`

**Branch:** `gameplay-sample-v1`

**Starting design commit:** `09e1eaa09c41d2041331538f34614a7a635d64c6`

## Execution rules

- Work in the normal LockstepArena workspace on `gameplay-sample-v1`.
- Do **not** create a worktree, sandbox, copied Unity project, evidence directory, progress report, validation dump, or checkpoint document for ordinary Step 1 work.
- Do not add a new Gate or additional Step. There are only Step 1, Step 2, Step 3.
- Do not stop for mini-reviews between the internal tasks below. Make cohesive commits as useful, keep going, then push once the whole Step 1 target is complete and submit one handoff.
- Do not start Step 2 work: no LAN bind changes, BattleReady, disconnect-forfeit host flow, Unity LobbyScene/BattleScene, AppRoot, HUD, F1 UI, or final art.
- Do not add KCP/UDP, ECS, DI/EventBus, hero/weapon frameworks, generic asset systems, or unrelated refactors.
- Keep `LockstepArena.Simulation` Unity-free and deterministic. No UnityEngine, PhysX authority, wall-clock delta time, runtime random gameplay, or float Transform authority.
- Preserve the Gate 1-14 ownership model. Extend the existing `BattleSimulation`, prediction timeline, replay, protocol, and TCP runtime; do not create a second gameplay-specific simulation/prediction/rollback/replay/network stack.
- Preserve known user-authored local files. Never reset or overwrite `Assets/Settings/Mobile_RPAsset.asset` or `ProjectSettings/ShaderGraphSettings.asset` unless the user explicitly authorizes it.
- During implementation run only relevant existing test projects. The one full historical old+new regression belongs to Step 3, not Step 1.
- Reuse current test projects rather than creating new test projects.
- Use focused test-first development pragmatically: add the smallest failing contract/behavior test for the risky rule, implement it, run the relevant project, continue.

## Compatibility strategy

Gate 1-14 are frozen historical work. Avoid spending Step 1 rewriting their old golden vectors merely because gameplay was added.

Prefer a compatibility-preserving extension of the existing state model:

- Existing constructors/call sites that create the old movement-only state remain usable and retain the old deterministic behavior and old digest semantics where practical.
- Add an explicit gameplay-enabled initial-state/definition path for the new arena rules.
- Both paths still use the same `BattleSimulation` type and existing frame-sync stack; compatibility is not permission to build a second simulation engine.
- Existing `InputFrame` call sites remain source-compatible by defaulting new `Fire` input to `false` through an overload/compatible constructor.
- Existing `LocalInputSample` call sites remain source-compatible the same way.
- Gameplay-enabled states must use the complete new gameplay state, digest, prediction, rollback, and replay rules.

This strategy is specifically intended to keep Step 1 fast and protect the frozen Gate goldens while adding the new approved gameplay path.

---

## Internal Task 1 — Deterministic gameplay contracts and configuration

**Primary files**

Create under `Packages/com.locksteparena.simulation/Runtime/` only as needed:

- `GameplayConfig.cs`
- `ArenaConfig.cs`
- `BattleDefinition.cs`
- `BattlePhase.cs`
- `RoundResult.cs` (or an equally small value type following repository conventions)
- `ProjectileState.cs`

Modify:

- `InputFrame.cs`
- `PlayerState.cs`
- `BattleState.cs`
- `SimulationConfig.cs` only where legacy constants must remain for compatibility; do not grow it into a second config source.

Tests: reuse `Tests/LockstepArena.Simulation.Tests/`; add focused source files to that same project if clearer.

### Required contracts

- `GameplayConfig` is immutable and centralizes the canonical integer/Tick values required by the approved design: HP, projectile damage, move units/Tick, fire interval, projectile units/Tick, projectile lifetime, player/projectile radii, muzzle offset/safety margin, round duration, countdown, round-end delay, rounds-to-win.
- Give Codex one reasonable playable default set; exact feel values are intentionally tunable later.
- Validate invariants at construction, especially positive durations/speeds/radii where required and `MuzzleOffset <= PlayerRadius - ProjectileRadius - SafetyMargin`.
- `ArenaConfig` is immutable and contains arena bounds, spawn points and fixed axis-aligned rectangular obstacles. The first default arena is symmetric with two spawns, one central larger cover and a small number of side covers.
- `BattleDefinition` owns the deterministic `GameplayConfig` + `ArenaConfig` for one gameplay-enabled battle.
- Extend `InputFrame` with `bool Fire`; old constructor usage must mean `Fire=false`.
- Extend `PlayerState` with HP, round wins and fire cooldown while retaining old construction compatibility.
- Extend `BattleState` for gameplay-enabled battles with the approved gameplay concepts: definition, phase, countdown/phase delay, round timer, round result/winner, projectiles, deterministic next projectile id. Copy incoming arrays/collections so state remains immutable from callers.
- Add an explicit gameplay-enabled initial factory/path. It must initialize players from the arena spawns, full HP, zero wins/cooldown, no projectiles, deterministic initial facing, and `RoundCountdown`.
- Keep roster/player storage generic (`Players[]` by `PlayerSlot`), not `Player1` / `Player2` gameplay fields.

### Focused tests before/with implementation

At minimum prove:

- invalid config invariants are rejected;
- config and arena inputs are copied/immutable from caller mutation;
- gameplay initial state has correct HP, spawn, wins, cooldown, countdown, timer and empty projectiles;
- old `InputFrame` and `LocalInputSample`-style semantics remain `Fire=false` once their respective layers are changed;
- 2-player gameplay is the supported first battle, without embedding `if P1 then damage P2` special-cases in state structures.

Run while working:

```powershell
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release
```

Do not run the whole repository regression here.

---

## Internal Task 2 — Movement, aim, projectile, damage, round and BO3 simulation

**Primary files**

Modify:

- `Packages/com.locksteparena.simulation/Runtime/BattleSimulation.cs`

Create small focused helpers under the same Simulation Runtime only where they genuinely simplify deterministic code, for example:

- `DeterministicAim.cs`
- `DeterministicCollision.cs`

Do not create a generic physics engine.

Tests: add focused gameplay behavior cases inside the existing `LockstepArena.Simulation.Tests` project.

### Required gameplay behavior

For gameplay-enabled state, implement the approved per-Tick order:

```text
phase handling
-> input / logical aim
-> player movement
-> fire cooldown
-> fire / projectile spawn
-> same-Tick projectile movement + sweep
-> first effective collision / damage / cleanup
-> death resolution
-> round timer / timeout resolution
-> round lock / score
-> next round or MatchEnded
```

Keep the existing legacy movement behavior available for legacy states so frozen historical tests do not require a mass migration.

### Movement and aim

- Fixed deterministic speed, no acceleration/inertia.
- Movement is X/Z only.
- If diagonal input would otherwise be faster, normalize with deterministic integer/fixed arithmetic; do not use gameplay floats.
- Resolve fixed arena bounds and AABB obstacles with deterministic axis-separated movement and wall sliding.
- Axis order is fixed across all runtimes.
- Collision result must not depend on arbitrary obstacle enumeration order; resolve the nearest allowed position for the axis.
- Players do not block one another.
- `ushort Aim` remains the canonical logical aim representation. Convert it to projectile direction with deterministic integer/fixed math. A compact fixed lookup/quantized direction scheme is acceptable; `Math.Sin/Cos` floating gameplay authority is not.

### Fire and projectile

- Hold-style `Fire=true` means “attempt to fire this Tick”.
- Spawn only when phase is `Playing` and cooldown is zero, then reset cooldown to configured `FireIntervalTicks`.
- Projectile id allocation comes only from deterministic `NextProjectileId` state.
- Spawn from player position plus logical Aim * `MuzzleOffset`; enforce the config safety invariant instead of adding a second muzzle-to-wall query.
- Newly spawned projectile moves/sweeps during the same Tick.
- Straight fixed-speed projectile, fixed Tick lifetime.
- Projectile owner cannot be damaged by its own ordinary projectile.
- Sweep previous-to-current position, so high-speed tunneling is prevented.
- Collision candidates are fixed obstacles + eligible players.
- Earliest collision wins; exact obstacle/player distance tie => obstacle wins; exact eligible-player tie => lower `PlayerSlot` wins.
- One ordinary projectile causes at most one damage event and is then consumed.
- Obstacle hit consumes it; lifetime expiration consumes it.
- No bounce, penetration, gravity, knockback, recoil, spread, hit i-frames or reload.

Use bounded 64-bit/integer arithmetic and explicit checked/validated ranges where geometry products could overflow. Do not make determinism depend on dictionary/hash iteration order.

### Round / BO3

- Damage clamps HP to zero.
- Single survivor after all same-Tick projectile damage => survivor wins round.
- Both dead in same Tick => Draw; no score.
- If nobody died and round timer reaches zero: higher HP wins; equal HP => Draw.
- Lethal damage on the same Tick as timeout takes precedence over HP timeout comparison.
- On round end, lock the result and clear all projectiles.
- `RoundEnded` waits configured deterministic delay, then resets HP/position/aim/cooldown/projectiles/timer/countdown while preserving identity and accumulated round wins.
- Draw adds no win and therefore replays the same logical round number.
- First participant reaching configured `RoundsToWin` (default 2) transitions to `MatchEnded`.
- `MatchEnded` ignores gameplay effects while Tick progression remains safe for the existing frame machinery until Step 2 wires business settlement to match completion.

### High-value focused tests

Do not test every trivial line. Cover the failure-prone rules:

- no movement during countdown; movement begins in Playing;
- arena bound and obstacle block;
- diagonal wall sliding;
- held fire respects cooldown;
- new projectile moves in spawn Tick;
- projectile lifetime expires;
- high-speed sweep hits a player even when end position passed beyond the target;
- wall-before-player blocks damage;
- player-before-wall damages exactly once;
- exact collision tie follows deterministic rule;
- owner is ignored;
- same-Tick double KO is Draw;
- timeout HP winner and equal-HP Draw;
- lethal hit on timeout Tick wins by death result;
- round reset fields are reset/preserved correctly;
- BO3 reaches MatchEnded at 2 wins;
- twin gameplay simulations fed identical frames produce identical state/digest across a meaningful scripted run.

Run:

```powershell
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release
```

---

## Internal Task 3 — Extend State Digest and exact state comparison

**Primary files**

Modify:

- `Packages/com.locksteparena.simulation/Runtime/StateDigest.cs`

Create if it removes duplicated comparison logic:

- `Packages/com.locksteparena.simulation/Runtime/BattleStateValueComparer.cs`

Modify later consumers to use the shared exact comparer rather than maintaining incomplete copies.

### Required semantics

- Preserve the old digest output for legacy movement-only states if practical under the compatibility strategy; this protects frozen Gate golden vectors and avoids needless historical rewrites.
- Gameplay-enabled digest must include every deterministic value that can affect future simulation, including phase/timers/results, roster identity/order, complete player gameplay state, projectile list in canonical deterministic order, next projectile id, and deterministic gameplay/arena definition values (or one canonically computed definition fingerprint).
- Never include Unity/View state, object identity, allocation address, protobuf bytes, wall-clock values or hash-container iteration order.
- Exact state equality used by prediction/replay must compare the complete gameplay-enabled state. Do not treat equal 64-bit digest alone as proof of structural equality.

Focused tests:

- equal gameplay states => equal digest and exact comparer true;
- changing HP/cooldown/phase/timer/projectile/next id/critical config or obstacle data changes gameplay digest;
- copied states with identical projectile data remain equal;
- legacy golden digest tests continue unchanged if the compatibility path can preserve them.

Run the Simulation tests again only.

---

## Internal Task 4 — Carry Fire through Protocol and existing prediction / rollback / replay

This task wires the new player intent and state through the existing stack. It must not redesign the stack.

### Protocol files

Modify:

- `Packages/com.locksteparena.protocol/Schema/lockstep_arena_protocol.proto`
- `Packages/com.locksteparena.protocol/Runtime/Mapping/ProtocolMapper.cs`
- regenerate `Packages/com.locksteparena.protocol/Runtime/Generated/LockstepArenaProtocol.g.cs` using the existing pinned codegen project; do not hand-edit generated code.

Tests: reuse `Tests/LockstepArena.Server.Protocol.Tests/`.

Required wire change:

```proto
message InputFrameMessage {
  uint32 tick = 1;
  uint32 player_slot = 2;
  sint32 move_x = 3;
  sint32 move_z = 4;
  uint32 aim = 5;
  bool fire = 6;
}
```

Map `Fire` both ways. Proto3 default `false` preserves the old no-fire semantic.

Generate with the existing project, using repository-supported invocation discovered from the project if necessary; the source of truth remains the single `.proto` and the tracked generated `.g.cs`.

Run:

```powershell
dotnet run --project Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj -c Release
```

### Prediction files

Modify:

- `Packages/com.locksteparena.client-prediction/Runtime/ClientPredictionTimeline.cs`
- `Packages/com.locksteparena.client-live-tcp/Runtime/PredictedTcpClientBattleRuntime.cs`
- any existing small verification/helper files that duplicate full-state equality and therefore must understand gameplay state.

Tests: reuse:

- `Tests/LockstepArena.Client.Prediction.Tests/`
- `Tests/LockstepArena.LivePrediction.Tests/`

Required behavior:

- `LocalInputSample` gains `Fire`; existing constructor usage defaults false.
- Local predicted frame includes Fire.
- Remote repeat-last prediction preserves the most recently authoritative remote Fire value, consistent with the existing repeat-last policy; neutral/no-cache remote input uses `Fire=false`.
- `ClientPredictionTimeline.FramesHaveSameValue` includes Fire so a wrong fire prediction is Dirty.
- Replace incomplete private state equality helpers with the complete shared structural comparer (or otherwise update every equality site to compare all gameplay fields).
- Dirty rollback continues to rebuild by running the same `BattleSimulation` over retained predicted frames.
- Authoritative replay continues to reconstruct from the same initial state and authoritative frames; gameplay projectile/HP/round state must converge without a separate replay implementation.

Add only a few high-value integration cases:

- Fire mismatch alone marks an otherwise equal predicted frame Dirty.
- Wrong remote Fire prediction causes rollback/re-simulation and converges to the authoritative gameplay state.
- A gameplay case where authority changes projectile existence and/or HP converges after replay.
- Reconstructed authoritative gameplay state structurally equals live authoritative state and has the same digest.

Run:

```powershell
dotnet run --project Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
```

Do not run DemoFlow/Unity/LAN/full repository regression in Step 1 unless a concrete Step 1 change directly requires one of them for diagnosis.

### Explicit Step 1 deferrals

Do **not** implement these yet; they belong to Step 2:

- `BattleConfigHash` network handshake / mismatch rejection;
- shipping GameplayConfig/ArenaConfig over the control protocol if later needed;
- BattleReady / scene readiness;
- server LAN bind/client endpoint UX;
- match settlement on gameplay `MatchEnded` / disconnect-forfeit host business behavior;
- Unity input/view/scenes/HUD/debug UI.

Step 1 only has to prove the deterministic gameplay core and the existing frame-sync machinery can carry it.

---

## Internal Task 5 — Step 1 focused verification, cleanup, push, single handoff

This is still part of official Step 1, not a new verification Step.

Run the focused Step 1 suite once after implementation stabilizes:

```powershell
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
git diff --check
git status --short
```

Requirements before handoff:

- all four focused existing test projects pass;
- no Unity/art/LAN Step 2 work has leaked in;
- no new test project was created merely for Gameplay;
- no worktree/sandbox/evidence/progress/temp output is committed;
- no generated `bin/`, `obj/`, copied DLL/project or validation debris is tracked;
- protected user files are untouched;
- implementation remains on `gameplay-sample-v1`;
- cohesive implementation commits are pushed to GitHub.

Then STOP and provide one chat handoff only:

```text
Gameplay Step: 1
Branch:
HEAD:

Goal achieved:
Key architecture choices:
Important changed areas:
Tests actually run + exact results:
Known issues:
Intentional Step 2 deferrals:
Review requests:
```

Do not create a permanent handoff/evidence markdown file for this.

## Step 1 acceptance target

Step 1 is ready for ChatGPT review only when the repository demonstrates all of the following:

- a gameplay-enabled deterministic battle state/config/arena exists;
- movement, aim, fixed obstacles, fire cooldown, projectile sweep/lifetime, damage, Draw/timeout/round reset/BO3 operate in shared Simulation;
- gameplay does not rely on Unity/PhysX/float Transform authority;
- Fire travels through the existing Protobuf/domain input path;
- existing prediction can predict real gameplay state;
- Fire/projectile/HP mismatches can become Dirty and use the existing rollback/re-simulation path;
- existing authoritative Replay reconstructs gameplay state;
- gameplay-enabled State Digest covers gameplay-critical state;
- no second simulation/prediction/rollback/replay/network stack exists;
- the focused Step 1 test suite passes;
- the repository remains a single clean project ready to continue directly into Step 2 after review.