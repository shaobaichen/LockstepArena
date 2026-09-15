# Lockstep Arena Gameplay Sample v1 Design

## Status

Approved design for the post-v1 Gameplay Sample. This document is the single formal design source for the three-step implementation. It extends the merged Lockstep Arena v1 baseline without reopening Gate 1–14.

- Base `main`: `2308309b8f8ba78424e1551e6403c50b0e6a7dcf`
- Development branch: `gameplay-sample-v1`
- Unity baseline: `6000.3.10f1`
- Gate 1–14 remain frozen and complete. There is no Gate 15.
- Gameplay Sample work has exactly three Steps. No additional Step, Gate, or checkpoint may be added.

## 1. Product goal

Build the first genuinely playable game on top of the existing reusable deterministic lockstep stack: a stylized 3D oblique top-down 1v1 projectile arena that can be played by two real PCs on the same LAN.

Core controls:

- WASD movement.
- Mouse world-position aim on a fixed horizontal X/Z gameplay plane.
- Hold left mouse button to fire at a fixed Tick cooldown.
- Movement direction and aim direction are independent.

Match flow:

```text
Server
-> nickname / lobby
-> room create or join
-> Ready
-> host Start
-> BattleScene load
-> BattleReady
-> deterministic countdown
-> 1v1 round gameplay
-> best-of-three / first to two round wins
-> settlement
-> return to lobby
```

A successful v1 Gameplay Sample means two LAN PCs can complete this full flow, play a complete BO3 match, settle, return to lobby, and start another match without stale battle state.

## 2. Scope boundaries

Included:

- Fixed-speed movement with no acceleration or inertia.
- Fixed oblique top-down camera.
- Symmetric arena with one large central cover and a small number of side covers.
- Deterministic 2D X/Z gameplay under a 3D presentation.
- Player circle hitboxes, projectile point/small-circle hitboxes, axis-aligned rectangular obstacles.
- Player-vs-map collision with axis-separated sliding.
- Players do not physically block one another.
- Straight physical projectiles with flight time.
- Projectile sweep collision from previous to current position to prevent tunneling.
- One effective collision per ordinary projectile.
- Projectile hits obstacle: destroy.
- Projectile hits non-owner player: damage once and destroy.
- Projectile has fixed Tick lifetime.
- Infinite ammunition; fire cooldown only.
- No spread, recoil, knockback, hit invulnerability, reload, dash, jump, vertical aim, destructible obstacles, moving platforms, hero differences, spectator, reconnect, public Internet, NAT traversal, KCP/UDP, TLS, FFA, or teams in this version.

Future multiplayer must not be blocked by first-version code. Gameplay state and logic use `Players[]`, `PlayerId`, `PlayerSlot`, projectile owner identity, and generic iteration rather than hard-coded `Player1` / `Player2` branches.

## 3. Architecture rule: deterministic Logic, replaceable View

The existing ownership model remains authoritative:

```text
Unity Input / View
       |
       | InputFrame
       v
Deterministic Battle Simulation
       |
       | BattleState
       v
Existing prediction / Dirty / rollback / replay
       |
       v
Unity presentation
```

All match-result-affecting state belongs to the deterministic simulation. Unity `Transform`, `Rigidbody`, `Physics`, Animator, particles, camera, UI, audio, and other presentation state must never decide gameplay outcomes.

Gameplay extends the existing simulation, prediction, rollback, replay, authority, framing, and networking stack. It must not introduce a second gameplay-specific simulation, prediction stack, rollback stack, replay stack, or socket stack.

Authority logic must avoid `UnityEngine`, wall-clock delta time, Unity physics, runtime random values, and float Transform state. Canonical gameplay values use integer/fixed deterministic units and Ticks.

## 4. Deterministic gameplay state

The concrete code shape may follow current repository conventions, but the authoritative information must cover the following concepts:

```text
BattleState
- Tick
- Phase
- countdown ticks remaining
- round timer ticks remaining
- round result
- match winner
- Players[]
- Projectiles[]
- next deterministic ProjectileId

Player state
- PlayerId
- PlayerSlot
- position X/Z
- aim direction
- HP
- round wins
- fire cooldown ticks

Projectile state
- ProjectileId
- OwnerPlayerId
- position X/Z
- direction
- lifetime ticks remaining
```

Any state that can change a future simulation result must participate in the existing State Digest. Presentation-only state must not.

`ProjectileId` is deterministic and monotonically allocated from simulation state. Do not use GUIDs, Unity instance IDs, runtime random values, or allocation order outside the deterministic simulation.

## 5. Input semantics

Clients send player intent, not gameplay results.

The effective input includes:

```text
Tick
PlayerSlot / participant identity according to existing contracts
MoveX
MoveZ
Aim
Fire
```

`Fire` means the player wants to fire. Projectile creation, collision, damage, death, round result, and match result are derived by deterministic simulation and are not sent as client-authored truth.

## 6. Per-Tick gameplay order

The exact implementation must be shared by server authority, prediction, rollback re-simulation, and replay. The first-version semantic order is:

1. Read current gameplay phase.
2. During `RoundCountdown`, advance countdown only; Move / Fire are ignored and round timer does not run.
3. During `Playing`, consume the frame inputs.
4. Update logical aim.
5. Resolve player movement against arena bounds and obstacles with deterministic axis-separated sliding.
6. Advance fire cooldowns.
7. Resolve Fire requests and create eligible projectiles.
8. Newly created projectiles participate in projectile movement/sweep during the same Tick.
9. Move/sweep all projectiles and select their first effective collision.
10. Apply at most one damage event per ordinary projectile and remove consumed/expired projectiles.
11. Resolve deaths after all same-Tick projectile damage is applied.
12. Advance/check the round timer if no death result already ended the round.
13. Lock round result, clear remaining projectiles, update round score where applicable.
14. Transition to the next round or `MatchEnded`.
15. Produce the resulting deterministic state and State Digest.

If a lethal hit happens on the same Tick that the timer reaches zero, death resolution wins over timeout HP comparison.

## 7. Movement and collision

Movement has fixed speed and no acceleration/deceleration. Arena bounds and fixed obstacles block movement; other players do not.

Axis handling order is fixed and identical on all runtimes. The implementation should calculate the nearest allowed movement for an axis rather than depending on arbitrary obstacle iteration order. Diagonal movement into a wall therefore continues along the unblocked axis and produces wall sliding.

The first map uses only simple axis-aligned rectangular logical obstacles. Rotated obstacle collision, complex meshes, PhysX authority, moving obstacles, and destructible obstacles are out of scope.

## 8. Projectile rules

Ordinary projectile behavior is intentionally minimal:

- Straight line, fixed deterministic speed.
- Spawn from a deterministic logical offset from the player in Aim direction.
- Spawn offset obeys the invariant `MuzzleOffset <= PlayerRadius - ProjectileRadius - SafetyMargin` so the authority projectile cannot appear beyond a wall merely because a visual weapon muzzle is outside the player body.
- Visual weapon muzzle transforms are presentation only.
- Sweep from previous to current position each Tick.
- Ignore projectile owner for damage.
- Consider obstacles and eligible players as collision candidates.
- Choose the earliest hit along the sweep.
- Exact-distance tie: obstacle wins over player.
- If eligible players are exactly tied in a future overlapping-player case, lower `PlayerSlot` is the deterministic tie-break.
- First valid player hit: apply damage once, consume projectile.
- First obstacle hit: consume projectile.
- Lifetime expiration: consume projectile.

No bounce, penetration, gravity, acceleration, random spread, recoil, or knockback is present in the base projectile.

## 9. Damage, rounds, BO3

Default initial balance is approximately four hits to kill, for example `MaxHP = 100`, `ProjectileDamage = 25`. These are starter values only and must remain configurable.

Damage clamps HP at zero. A single ordinary projectile cannot repeatedly damage a player across Ticks, so the first version does not need hit invulnerability frames.

Round rules:

- One player reaches 0 HP: surviving player wins the round.
- Both reach 0 HP in the same deterministic Tick: Draw; no score awarded.
- Default round time is about 60 seconds, represented as configurable Ticks.
- Timeout with unequal HP: higher HP wins the round.
- Timeout with equal HP: Draw; no score awarded.
- Draw restarts the same logical round number because no round win is added.
- When a round ends, all remaining projectiles are cleared and no later projectile can change the locked result.
- Round reset restores HP, position, initial facing, cooldown, projectiles, timer, and countdown while preserving participant identity and accumulated round wins.
- Spawn positions are fixed and symmetric; players initially face one another.
- First player to two round wins ends the match.

Deterministic gameplay phases are intentionally small:

```text
RoundCountdown -> Playing -> RoundEnded -> RoundCountdown
                                  |
                                  -> MatchEnded
```

Server business lifecycle states such as battle preparation, BattleReady timeout, settlement, and ReturnToLobby are not incorrectly forced into rollback gameplay state.

## 10. GameplayConfig and ArenaConfig

All tunable gameplay values are centralized, not scattered as constants. Canonical deterministic configuration includes the concepts below, using repository-appropriate naming:

```text
MaxHP
ProjectileDamage
MoveUnitsPerTick
FireIntervalTicks
ProjectileUnitsPerTick
ProjectileLifetimeTicks
PlayerRadiusUnits
ProjectileRadiusUnits
MuzzleOffsetUnits
MuzzleSafetyMarginUnits
RoundDurationTicks
RoundCountdownTicks
RoundEndDelayTicks
RoundsToWin
```

`BattleReadyTimeout` is server/business real-time configuration rather than deterministic gameplay-Tick configuration.

Arena configuration is lightweight:

```text
ArenaId
Bounds
SpawnPoints[]
Obstacles[]
```

Each logical obstacle is a fixed axis-aligned rectangle. The first version contains one formal symmetric arena: central large cover plus a small number of side covers and symmetric spawns. Exact dimensions, movement speed, projectile speed, and similar feel values are not permanently frozen before playtesting; Codex supplies reasonable defaults and they remain centralized for tuning.

## 11. Configuration compatibility and State Digest

`BattleConfigHash` is a small pre-battle compatibility check covering the deterministic GameplayConfig and ArenaConfig. Its purpose is only to prevent machines with different gameplay/map rules from starting the same battle. Keep it lightweight: canonical deterministic serialization, identical config -> identical hash, a changed gameplay-critical value -> changed hash.

State Digest is different: it is the existing runtime fingerprint of authoritative gameplay state. It is retained and extended to new gameplay-critical state so server, clients, rollback/re-simulation, and replay can verify convergence.

Do not build a large new hashing subsystem or a separate test project solely for these features.

## 12. LobbyScene, BattleScene, and persistent app/network lifetime

Unity presentation uses two principal scenes:

```text
LobbyScene
- nickname/session entry
- server address
- room list
- create/join/leave room
- Ready/Unready
- host Start

BattleScene
- arena visuals
- player visuals
- projectile visuals
- fixed camera
- gameplay input
- battle HUD
- F1 debug overlay
```

A small persistent App/Network root survives scene changes and owns application-level references/context such as the control client, endpoint, session, battle bootstrap, and scene transition state. It must not become a giant gameplay manager.

The CONTROL TCP connection persists across LobbyScene -> BattleScene -> LobbyScene. BATTLE TCP remains temporary for one active battle, preserving the existing Gate 14 separation and ticketed attachment model.

Scene objects consume the persistent runtime; scenes do not own the core TCP session.

## 13. BattleReady and start synchronization

Room Ready and BattleReady are separate concepts.

Flow:

```text
all room participants Ready
-> host Start
-> server freezes roster and prepares battle/bootstrap/tickets
-> clients load BattleScene and attach battle connection
-> local BattleScene presentation becomes ready
-> client sends BattleReady
-> server waits for all battle participants
-> deterministic RoundCountdown starts
-> Playing starts on deterministic Tick progression
```

A client does not start the match merely because its Unity scene loaded first, and countdown is not implemented as independent local wall-clock timers.

A configurable real-time `BattleReadyTimeout` (initially around 15 seconds) prevents infinite preparation. Failure before gameplay starts aborts battle preparation and safely returns connected participants to lobby; it is not counted as an in-match loss.

After gameplay has started, a participant CONTROL/BATTLE disconnect is a match forfeit in the first version. The surviving participant receives the match result with a clear disconnect/forfeit settlement reason. Reconnect is out of scope.

## 14. LAN target

Gameplay Sample supports both local loopback testing and real same-LAN two-PC play.

Do not rewrite the transport. The current CONTROL TCP + BATTLE TCP architecture remains. Replace loopback-only assumptions with configurable server bind/client endpoint settings so the server can listen on the LAN and the second PC can connect to the host private-network IP.

First-version development UX uses a separately launched server process. Automatic local server launch / polished "Create LAN Game" hosting can be considered later and is not part of these three Steps.

No public Internet hosting, cloud deployment, NAT traversal, KCP/UDP migration, or TLS is introduced.

## 15. Unity presentation and future art replacement

The three Gameplay Steps do not depend on final art assets. Placeholder presentation is explicitly allowed and expected:

- Capsule/simple primitive player.
- Sphere/simple primitive projectile.
- Cube/primitive covers.
- Plane/simple floor.
- Functional Unity UI for login/lobby/room/HUD/result.

The architecture must make the later presentation/art version a skinning pass rather than a gameplay rewrite.

Use a lightweight presentation binding boundary so visual resources can be swapped without changing deterministic simulation, protocol, room flow, damage, projectile, round, or networking rules. The exact implementation should remain small; do not create a generic asset framework.

Conceptual replaceable bindings include player prefab, projectile prefab, arena floor/boundary/cover visuals, optional VFX prefabs, and UI visual assets. `PlayerView` should expose gameplay-neutral presentation actions/state such as movement/aim/fire/hit/death without hard-coding one third-party character's bone names into gameplay code.

Logical muzzle position and logical collision sizes remain stable when art changes. Visual weapon muzzles, meshes, Animator, VFX, audio, materials, lighting, fonts, sprites, and layout styling are presentation only.

A later separate `v3 Presentation / Art Pass` will replace placeholders with final Low Poly characters, environment, UI, animation, VFX, audio, lighting, and decoration. It is not Step 4; the Gameplay Sample development ends after Step 3.

## 16. HUD and Debug presentation

Functional battle HUD shows only player-facing information:

- P1/P2 HP.
- Round number.
- Round score.
- Remaining time.
- `3 / 2 / 1 / FIGHT` countdown.
- Round winner / Draw presentation.
- Match winner / final score / Return To Lobby.

Engineering metrics stay out of the formal HUD. F1 toggles a default-hidden debug overlay using only metrics that the current runtime can truthfully expose, such as server/authority/predicted Tick, Dirty/rollback/re-simulation/replay information, digest values, battle/session identity, and connection state where available.

Local predicted gameplay may immediately drive local visual firing/projectile presentation. If later authority makes that prediction Dirty, existing rollback/re-simulation corrects the state and View follows the corrected state. View objects may smooth or interpolate visual transforms but cannot become a second source of gameplay truth.

## 17. Testing philosophy: minimum necessary for speed

The project prioritizes rapid delivery while preserving the deterministic guarantees that make the sample meaningful.

Rules:

- During normal development, run only tests relevant to the code being changed.
- Reuse existing test projects instead of creating many new narrowly named projects.
- Add focused tests for the highest-risk deterministic rules: movement/obstacle behavior, fire cooldown, projectile lifetime/sweep, first-hit/tie behavior, one-hit damage, same-Tick double KO, timeout, round reset/BO3, configuration compatibility, gameplay prediction/rollback/replay convergence.
- Do not rerun all historical tests after every small change.
- Run one complete old+new regression at the end of Step 3 before final acceptance.
- Unity tests focus on wiring/lifetime/presentation boundaries rather than duplicating all pure simulation mathematics.
- Real two-PC LAN play is a required final manual acceptance because automated loopback cannot prove the actual LAN path.

Do not create persistent evidence directories, progress reports, checkpoint report files, validation dumps, copied Unity projects, or redundant sandboxes by default. Temporary logs/output are disposable unless a concrete final acceptance artifact is genuinely useful.

## 18. Repository/workspace cleanliness

Use one formal feature branch and the normal project workspace for the Gameplay Sample. Do not create a new worktree/sandbox/project copy for ordinary small changes. Isolation is reserved for genuinely high-risk mutually exclusive experiments.

The final repository should remain one coherent LockstepArena project containing only necessary source, tests, project configuration, and formal documentation. Development-process debris must not become part of the product.

Existing user-authored local project changes must not be reset or overwritten, including known protected Unity settings files unless the user explicitly authorizes it.

## 19. Exactly three implementation Steps

The full Gameplay Sample implementation, Codex work, ChatGPT review, bug correction, and acceptance are contained in exactly these three Steps. No Step 4, Step 2.5, new Gate, hidden checkpoint, separate cleanup phase, or separate art phase may be inserted into this Gameplay Sample flow. Problems discovered inside a Step are fixed inside that Step.

### Step 1 — Gameplay Core + FrameSync

Codex continuously completes the deterministic gameplay core and integrates it with the existing frame-sync stack:

```text
GameplayConfig / ArenaConfig
movement / aim
obstacle collision and wall sliding
fire cooldown
projectile spawn / movement / sweep / lifetime
first-hit collision rules
damage / HP
round / draw / timeout / BO3
gameplay State Digest
existing prediction / Dirty / rollback / re-simulation / replay integration
focused relevant tests
```

Codex may make multiple commits and work on independent subproblems in parallel where safe, but does not stop for mini-reviews. At the end it pushes and submits one Step 1 handoff. ChatGPT performs one unified architecture/code review and gives PASS / CONDITIONAL PASS / FAIL. Any correction remains part of Step 1.

### Step 2 — LAN + Unity Playable Slice

After Step 1 passes, Codex continuously completes:

```text
LAN-capable endpoint/bind configuration
BattleReady and timeout
BattleConfig compatibility check
disconnect/forfeit behavior
persistent App/Network root
LobbyScene
BattleScene
WASD + mouse aim + fire
placeholder arena/player/projectile View
functional HUD
F1 debug overlay
full local two-client lobby -> BO3 -> settlement -> lobby flow
```

No formal art assets are required. At the end Codex pushes and submits one Step 2 handoff. ChatGPT performs one unified code/flow review. Corrections stay inside Step 2.

### Step 3 — Real LAN + Final Acceptance

After Step 2 passes:

```text
PC A: server + client A
PC B: client B
same LAN full connection
complete BO3
settlement
return to lobby
second match to detect stale state/leaks
one final full old+new regression
necessary bug fixes
final README/architecture usage notes if needed
```

ChatGPT performs the final review. If accepted, the result is:

`Lockstep Arena Gameplay Sample v1 — FINAL PASS`

There is no additional Gameplay implementation Step after this.

## 20. Handoff discipline

Codex handoffs exist in conversation; they do not require permanent checkpoint/evidence documents in the repository. Each end-of-Step handoff should provide the branch, head commit, goal achieved, key architectural choices, important changed areas, tests actually run, known issues, intentional exclusions, and items requiring review, then stop.

## 21. Final acceptance

Gameplay Sample v1 is complete only when all of the following are true:

- Two real PCs on the same LAN can connect through the intended server/client path.
- The user can complete nickname/lobby/room/Ready/Start/BattleReady gameplay flow without console hacks.
- Players can move, aim, fire, use cover, take damage, complete rounds, handle Draw/timeout rules, and finish BO3.
- Match settlement and ReturnToLobby work.
- A second match can start without stale battle state.
- Existing prediction, Dirty detection, rollback, re-simulation, replay, and State Digest remain functional with real gameplay state.
- Server/client/replay deterministic state converges under the supported test scenarios.
- One final full regression passes before acceptance.
- The repository remains one clean coherent project rather than a collection of copied stages/sandboxes/evidence output.
- Final art is not required for Gameplay Sample v1; placeholders are intentionally replaceable by the later v3 Presentation / Art Pass without gameplay/network rewrite.
