# Lockstep Arena — Gate 0 Reference Study

> Research date: 2026-08-29
> Scope: research and architecture decisions only; no Gate 1 implementation
> Project baseline: Unity 6000.3.10f1, URP 17.3.0, empty SampleScene
> Evidence rule: observations below come from source code at the pinned commits. Recommendations for Lockstep Arena are labelled separately.

# 1. Executive Summary

**Recommendation in one sentence:** build a small three-part system in which a Unity Client samples and renders, a headless .NET Server owns sessions/rooms and orders both players' inputs, and a Unity-free Shared deterministic simulation advances the same canonical battle state from the same ordered FrameInput on client and server.

This is deliberately not a framework. For the 1–2 week demo, the deterministic core should be ordinary C# data plus an explicit Step(state, frame) pipeline. Positions and velocities use scaled integers, entity iteration is sorted by stable IDs, collision is a small XZ-plane circle/AABB implementation, and Unity Transform is only an interpolated view. The first networking version waits for both active players' inputs before publishing a frame; fixed input delay gives those inputs time to arrive. Prediction, snapshot, rollback and replay must reuse the same Step function, but their implementations are deferred until the basic lockstep path and digest tests are proven.

The strongest near-term reference is Unity-Lockstep because its Client, ServerHost and Shared Simulator boundaries are small and legible. Lockstep-Tutorial supplies the more mature mental model for frame comparison, backup, rollback, replay and desync dumps. netcode-lockstep is the scale and weak-network teaching reference, not a determinism reference. UnityLockstep is evidence for future rollback design and for the cost of over-engineering.

# 2. Reference Repositories

The commit is the default-branch HEAD observed on the research date, not a claim that the repository is production-ready.

| Repository | Pinned commit | Primary value | Primary caution |
|---|---|---|---|
| [JiepengTan/Lockstep-Tutorial](https://github.com/JiepengTan/Lockstep-Tutorial/tree/1a6b297707101a8ef1b29f256a33c78ce41488f6) | 1a6b297707101a8ef1b29f256a33c78ce41488f6 | Full prediction/rollback teaching path, frame comparison, snapshots, hashes, dumps, records | Large custom engine with ECS-like services, code generation, collision, behaviour tree and other V1-irrelevant systems |
| [inikin111/Unity-Lockstep](https://github.com/inikin111/Unity-Lockstep/tree/a2f43c3570f0f5fc32f63c79a7bb0c044a89819a) | a2f43c3570f0f5fc32f63c79a7bb0c044a89819a | Small Client/ServerHost/Shared Simulator split; input-only packets; integer simulation | Reliability and reconnection are incomplete; some iteration order is not made explicitly canonical |
| [Corrade/netcode-lockstep](https://github.com/Corrade/netcode-lockstep/tree/c93b68fa77ac36400abd8592ea18ce3224e3b9d9) | c93b68fa77ac36400abd8592ea18ce3224e3b9d9 | Compact 1v1 scale, input delay, ack/resend, artificial latency/loss, visible stalling | P2P topology and Unity Physics2D/float simulation do not meet our authoritative-server or cross-platform determinism target |
| [proepkes/UnityLockstep](https://github.com/proepkes/UnityLockstep/tree/993f82b5f6407353157c397265fad3717937e631) | 993f82b5f6407353157c397265fad3717937e631 | Client prediction, snapshot-on-prediction, late-input rollback, input-log replay tests | Archived/old stack; Entitas, .NET Core 2.2, BinaryFormatter and heavy dependencies; known rollback spikes and incomplete despawn recovery |

## 2.1 JiepengTan/Lockstep-Tutorial

**Relevant files**

- [FrameBuffer.cs](https://github.com/JiepengTan/Lockstep-Tutorial/blob/1a6b297707101a8ef1b29f256a33c78ce41488f6/Unity/Assets/Scripts/Logic/Framework/Simulator/FrameBuffer.cs)
- [SimulatorService.cs](https://github.com/JiepengTan/Lockstep-Tutorial/blob/1a6b297707101a8ef1b29f256a33c78ce41488f6/Unity/Assets/Scripts/Logic/Framework/SimulatorService.cs)
- [World.cs](https://github.com/JiepengTan/Lockstep-Tutorial/blob/1a6b297707101a8ef1b29f256a33c78ce41488f6/Unity/Assets/Scripts/Logic/Framework/Simulator/World.cs)
- [GameStateService.cs](https://github.com/JiepengTan/Lockstep-Tutorial/blob/1a6b297707101a8ef1b29f256a33c78ce41488f6/Unity/Assets/Scripts/Logic/Service/Services/GameStateService.cs)
- [HashHelper.cs](https://github.com/JiepengTan/Lockstep-Tutorial/blob/1a6b297707101a8ef1b29f256a33c78ce41488f6/Unity/Assets/Scripts/Logic/Framework/Simulator/HashHelper.cs)
- [DumpHelper.cs](https://github.com/JiepengTan/Lockstep-Tutorial/blob/1a6b297707101a8ef1b29f256a33c78ce41488f6/Unity/Assets/Scripts/Logic/Framework/Simulator/DumpHelper.cs)
- [Server Game.cs](https://github.com/JiepengTan/Lockstep-Tutorial/blob/1a6b297707101a8ef1b29f256a33c78ce41488f6/Server/Src/SimpleServer/Src/Server/Game.cs)
- [ServerFrame.cs](https://github.com/JiepengTan/Lockstep-Tutorial/blob/1a6b297707101a8ef1b29f256a33c78ce41488f6/Unity/Assets/LockstepEngine/NetMsg.Common/Src/Udp/ServerFrame.cs)
- [EntityView.cs](https://github.com/JiepengTan/Lockstep-Tutorial/blob/1a6b297707101a8ef1b29f256a33c78ce41488f6/Unity/Assets/Scripts/View/LogicView/Entity/EntityView.cs)

**Observed design**

- The server stores a ServerFrame for every tick. If every player input is present it publishes immediately; when wall-clock progress forces a tick, missing inputs become explicit IsMiss inputs. It broadcasts the current frame plus the previous two, providing simple redundancy.
- A ServerFrame has a tick and one Msg_PlayerInput per actor. Each player input contains compact command byte payloads. Equality is based on the serialized canonical input bytes.
- FrameBuffer keeps separate circular server and client/predicted frame arrays. It compares a predicted client frame with the later authoritative frame. The first mismatch sets IsNeedRollback; gaps trigger missing-frame requests.
- SimulatorService backs up state before each Step, restores at NextTickToCheck on a mismatch, re-simulates confirmed server frames, then predicts forward. Prediction fills absent player input from a deterministic prior-frame rule.
- GameStateService serializes all mutable Player, Enemy and Spawner fields and rebuilds entities/rebinds views on restore. RandomService separately backs up RNG state.
- HashHelper hashes registered deterministic services, sends only hashes for input-confirmed frames, and DumpHelper can write raw versus re-simulated per-frame state for diagnosis.
- The server dumps initial game data plus frame history as a record. Video mode drives the same simulator from that history.
- World.Step invokes systems in explicit registration order. EntityView reads fixed-point logic position and lerps Unity Transform in Update; the Transform is not fed back into simulation.

**Why it matters**

This repository demonstrates the whole causal chain: predicted input differs from authoritative input → earliest dirty frame → restore complete state → replay the same frames through the same Step → compare hashes and dumps. It also demonstrates that RNG state, entity creation IDs and view rebinding are part of rollback correctness.

**Adopt**

- Separate predicted and authoritative frame histories.
- Canonical frame comparison, earliest-dirty-frame rollback, RNG/state backup, replay through the same Step.
- Hash only confirmed frames and retain per-tick diagnostic dumps.
- Keep logic state separate from Unity presentation.

**Reject for V1**

- Its service/container/ECS-style framework, code-generated backups, behaviour tree, NavMesh, skill framework, large collision engine and broad serializer stack.
- Dynamic prediction-buffer tuning before a fixed delay works in our two-player network simulator.

## 2.2 inikin111/Unity-Lockstep

**Relevant files**

- [Client.cs](https://github.com/inikin111/Unity-Lockstep/blob/a2f43c3570f0f5fc32f63c79a7bb0c044a89819a/Assets/Scripts/Client/Client.cs)
- [GameRenderer.cs](https://github.com/inikin111/Unity-Lockstep/blob/a2f43c3570f0f5fc32f63c79a7bb0c044a89819a/Assets/Scripts/Client/GameRenderer.cs)
- [ServerHost/Program.cs](https://github.com/inikin111/Unity-Lockstep/blob/a2f43c3570f0f5fc32f63c79a7bb0c044a89819a/ServerHost/Program.cs)
- [Simulator.cs](https://github.com/inikin111/Unity-Lockstep/blob/a2f43c3570f0f5fc32f63c79a7bb0c044a89819a/Assets/Scripts/Shared/Simulator.cs)
- [Packets.cs](https://github.com/inikin111/Unity-Lockstep/blob/a2f43c3570f0f5fc32f63c79a7bb0c044a89819a/Assets/Scripts/Shared/Packets.cs)
- [EntityData.cs](https://github.com/inikin111/Unity-Lockstep/blob/a2f43c3570f0f5fc32f63c79a7bb0c044a89819a/Assets/Scripts/Shared/EntityData.cs)
- [RingBuffer.cs](https://github.com/inikin111/Unity-Lockstep/blob/a2f43c3570f0f5fc32f63c79a7bb0c044a89819a/Assets/Scripts/Shared/RingBuffer.cs)

**Observed design**

- Client samples local intent, labels it currentFrame + InputDelay, and sends InputPacket. It does not send position or final state.
- ServerHost caches inputs as tick → clientId → InputPacket. CanAdvanceTick returns true only when each active client has an input, except for warm-up ticks. Inputs are ordered by clientId into FramePacket before broadcast.
- Both Client and ServerHost call the same Simulator.SimulateFrame. The pipeline is ApplyFrameInputs → movement → entity motion → collision → SaveGameState.
- Simulation data uses Vector3i/scaled integers. Player and entity state are ordinary structs. Sphere and box/sphere collision are implemented inside Simulator rather than delegated to Unity physics.
- GameRenderer creates/updates Unity objects from GameState. It writes logic positions to view objects and does not make Transform authoritative.
- GameState history contains player state, entity state and entity motion velocity. LoadGameState restores those fields. The checksum is an FNV-style hash over fields sorted by IDs.
- StateSync and buffered FramePacket resend establish a reconnection/catch-up seam, but comments and code explicitly leave full reliability, resend requests and robust catch-up incomplete.

**Why it matters**

This is the closest structural fit for Lockstep Arena: small files, input-only client traffic, a server frame collector and one shared deterministic simulator. It proves that a clear learning architecture needs only a few concepts.

**Adopt**

- Client/Server/Shared separation and input-only battle messages.
- Ordered FramePacket with one slot per player.
- Ordinary data structures and a single explicit simulation pipeline.
- Server also runs Shared simulation, enabling authoritative result/digest validation.
- Integer collision and render-only Unity objects.

**Reject or correct**

- Do not copy the custom codec; our selected wire schema is Protobuf.
- Do not rely on Dictionary enumeration order. Simulation, collision pairs, snapshots and digest encoding must sort by stable ID or use fixed player/entity arrays.
- Do not treat its reconnection and reliability skeleton as complete.
- Do not store every large state forever; choose bounded frame and snapshot windows from rollback requirements.

## 2.3 Corrade/netcode-lockstep

**Relevant files**

- [GameController.cs](https://github.com/Corrade/netcode-lockstep/blob/c93b68fa77ac36400abd8592ea18ce3224e3b9d9/Assets/Scripts/Gameplay/GameController.cs)
- [Clock.cs](https://github.com/Corrade/netcode-lockstep/blob/c93b68fa77ac36400abd8592ea18ce3224e3b9d9/Assets/Scripts/Gameplay/Clock.cs)
- [TickService.cs](https://github.com/Corrade/netcode-lockstep/blob/c93b68fa77ac36400abd8592ea18ce3224e3b9d9/Assets/Scripts/Services/TickService.cs)
- [InputBuffer.cs](https://github.com/Corrade/netcode-lockstep/blob/c93b68fa77ac36400abd8592ea18ce3224e3b9d9/Assets/Scripts/Gameplay/Player/Input/InputBuffer.cs)
- [SelfInputManager.cs](https://github.com/Corrade/netcode-lockstep/blob/c93b68fa77ac36400abd8592ea18ce3224e3b9d9/Assets/Scripts/Gameplay/Player/Input/SelfInputManager.cs)
- [PeerInputManager.cs](https://github.com/Corrade/netcode-lockstep/blob/c93b68fa77ac36400abd8592ea18ce3224e3b9d9/Assets/Scripts/Gameplay/Player/Input/PeerInputManager.cs)
- [ConnectionManager.cs](https://github.com/Corrade/netcode-lockstep/blob/c93b68fa77ac36400abd8592ea18ce3224e3b9d9/Assets/Scripts/Gameplay/ConnectionManager.cs)
- [Settings.cs](https://github.com/Corrade/netcode-lockstep/blob/c93b68fa77ac36400abd8592ea18ce3224e3b9d9/Assets/Scripts/Gameplay/Settings.cs)

**Observed design**

- It is a symmetric P2P 1v1 demo. Each peer stores local and remote circular input buffers.
- Simulation tick is currentTick - InputDelayTicks. If remote input is absent, the clock pauses rather than guessing.
- Inputs are a ushort bit mask. The sender repeatedly sends every unacknowledged tick as one range; the receiver writes missing entries and acknowledges an exclusive end tick. This survives loss without a large reliability framework.
- Settings exposes fixed input delay, artificial latency and sender-side packet loss. Reliable metadata uses TCP; input/ack uses UDP.
- Unity Physics2D is switched to script simulation and advanced once per tick. Movement uses float, Rigidbody2D, BoxCastAll and Physics2D.Simulate.

**Why it matters**

The demo makes network pain visible with very little code. Input delay moves simulation behind input capture, repeated unacknowledged ranges make packet loss understandable, and the clock visibly stalls when the delay budget is insufficient.

**Adopt**

- A first-class network simulator with latency, jitter, loss, duplication and reordering controls.
- Metrics/overlay for current tick, simulation tick, missing input and stall duration.
- Fixed input delay and redundant recent-input delivery as concepts.
- Bit-packed action buttons and tick-indexed input history.

**Reject or correct**

- Dedicated authoritative server is required for our rooms/results; do not adopt P2P/NAT setup.
- Physics2D, Rigidbody2D, raycasts and floats are not acceptable as cross-platform battle truth.
- The project has no prediction, rollback, snapshot or state digest; it cannot validate those future requirements.
- Tick wrapping at about 18 minutes is needless for our protocol; use uint tick and an explicit match-duration limit.

## 2.4 proepkes/UnityLockstep

**Relevant files**

- [Simulation.cs](https://github.com/proepkes/UnityLockstep/blob/993f82b5f6407353157c397265fad3717937e631/Engine/Game/Simulation.cs)
- [World.cs](https://github.com/proepkes/UnityLockstep/blob/993f82b5f6407353157c397265fad3717937e631/Engine/Core.Logic/World.cs)
- [OnNewPredictionCreateSnapshot.cs](https://github.com/proepkes/UnityLockstep/blob/993f82b5f6407353157c397265fad3717937e631/Engine/Core.Logic/Systems/GameState/OnNewPredictionCreateSnapshot.cs)
- [NetworkCommandQueue.cs](https://github.com/proepkes/UnityLockstep/blob/993f82b5f6407353157c397265fad3717937e631/Engine/Network.Client/NetworkCommandQueue.cs)
- [Room.cs](https://github.com/proepkes/UnityLockstep/blob/993f82b5f6407353157c397265fad3717937e631/Engine/Network.Server/Room.cs)
- [GameLog.cs](https://github.com/proepkes/UnityLockstep/blob/993f82b5f6407353157c397265fad3717937e631/Engine/Game/GameLog.cs)
- [TestUtil.cs](https://github.com/proepkes/UnityLockstep/blob/993f82b5f6407353157c397265fad3717937e631/Engine/Test/TestUtil.cs)
- [CalculateHashCode.cs](https://github.com/proepkes/UnityLockstep/blob/993f82b5f6407353157c397265fad3717937e631/Engine/Core.Logic/Systems/GameState/CalculateHashCode.cs)

**Observed design**

- Local Update always enqueues local commands and predicts. NetworkCommandQueue sends input tick plus a lag-compensation offset; Room mostly relays compressed input to peers and compares reported hashes.
- When remote input arrives for an already predicted tick, Simulation finds the earliest remote tick, calls World.RevertToTick, re-simulates authoritative inputs and predicts back to the previous local target.
- The transition into predicting creates a snapshot. It copies active game entities and per-actor ID counters into Entitas shadow entities. Restore deletes prediction-created entities, copies components back and removes later snapshots.
- GameLog stores the tick at which input arrived as well as its target tick. Tests construct a clean simulation, enqueue the full input log and assert the final hash matches.
- FixedMath.NET provides numeric determinism; the larger solution also references Entitas, BEPUPhysics fork and LiteNetLib.

**Why it matters**

The code demonstrates that snapshots must include allocator state as well as visible entities, that late-input rollback must return to the prior predicted horizon, and that replay tests are an excellent determinism oracle. The README also provides valuable negative evidence: very old inputs can cause thousands of ticks of rollback, despawn recovery is incomplete, and frame-rate synchronization remains unresolved.

**Adopt**

- Snapshot at a known tick boundary, include entity-ID allocator/RNG, restore, then replay to the old horizon.
- Record initial state plus input history and verify replay digest in automated tests.
- Cap rollback age and per-render-frame catch-up work.

**Reject for V1**

- Entitas/ECS, reflection-based command factories, BinaryFormatter, old .NET targets, WPF server UI, BEPU/RVO/NavMesh stack and dependency bundle.
- A relay-only server and unconditional prediction.
- The weak XOR digest, which omits much state and has easy collisions.

# 3. Observed Architecture

Across the four repositories, the reusable architecture is smaller than any one repository:

1. Input is sampled separately from simulation and assigned to a numbered tick.
2. A frame is an ordered set of all players' inputs for one tick.
3. A fixed-tick driver passes exactly one frame to a deterministic Step pipeline.
4. Simulation state is ordinary data; the view reads it after the step.
5. A strict implementation waits for complete input. A prediction implementation substitutes a deterministic guess and later compares it with the authoritative frame.
6. A mismatch marks the earliest predicted tick dirty. Complete state is restored at or before that tick, then the same Step pipeline replays stored frames.
7. A digest over canonical deterministic state detects divergence; per-field dumps locate it.
8. Replay is not a second gameplay implementation. It is initial state plus frame history driving the same simulation.

The repositories disagree on topology and determinism quality. Corrade is P2P and Unity-physics based. UnityLockstep's server relays rather than simulates. Lockstep-Tutorial is feature-rich. Unity-Lockstep most closely matches our chosen authoritative-server/shared-simulation shape. Therefore no repository is copied wholesale.

# 4. What We Will Adopt

- A pure C# Shared simulation with no UnityEngine, socket, clock or Protobuf-generated-type dependency.
- One explicit, stable pipeline: validate frame → apply input by player slot → move players → spawn/move projectiles → resolve collisions in stable ID order → apply damage/death/respawn/score → advance tick.
- Client sends only quantized intent. It never sends Transform, Rigidbody state, hit result, damage or score as truth.
- Server assigns battle identity/player slots/start seed, collects exactly one input per active player per tick, orders inputs, broadcasts FrameData and runs the same Shared simulation.
- Fixed input delay as a named battle parameter, initially a candidate 2 ticks at 30 Hz and tuned only by network-simulator evidence.
- Bounded frame history from the first network version; bounded snapshot history when rollback begins.
- Canonical state digest and an automated twin-simulation/replay test before real networking.
- Network simulator and visible tick/input/stall/digest diagnostics.
- Unity view interpolation between the previous and current confirmed/predicted logic states.
- Explicit enum-based Session/Room/Battle lifecycle with guarded transitions.
- Protobuf as the single wire-contract source and TCP/KCP as separate control/battle transports.

# 5. What We Will Reject

- Unity Transform, Rigidbody, PhysX collision callbacks or render frame timing as battle truth.
- P2P topology, client-authored positions/hits/results and relay-only result authority.
- ECS, DI container, global service locator, generic EventBus, repository layer, factory/interface proliferation and broad Manager hierarchies.
- NavMesh, behaviour trees, skill timeline, buffs, combos, root motion, matchmaking, chat, MySQL and real accounts in V1.
- Self-written KCP or a general-purpose fixed-point mathematics library.
- Copying Protobuf DTOs into the simulation domain or hashing arbitrary Protobuf bytes.
- Unordered Dictionary/HashSet iteration in simulation, collision, serialization or digest.
- Unlimited input history, snapshot history, rollback distance or catch-up work.
- Implementing prediction/rollback before strict two-player lockstep and digest tests pass.

# 6. Lockstep Arena Minimal Architecture

~~~mermaid
flowchart LR
    subgraph Client["Unity Client"]
        APP[Application + lifecycle]
        UI[Lobby / Room / Battle UI]
        CN[Network adapters\nTCP + KCP]
        IN[Input sampler + quantizer]
        FB[Frame buffer\nfuture: prediction history]
        VIEW[Presentation\nrender interpolation]
    end

    subgraph Shared["Shared pure C#"]
        PROTO[Protocol schema + DTO mapping]
        FRAME[Frame types + canonical ordering]
        SIM[Deterministic Simulation.Step]
        MATH[Scaled integer math]
        COLL[Small XZ collision]
        DIGEST[Canonical state digest]
        REPLAY[Replay contracts - deferred]
    end

    subgraph Server["Headless .NET Server"]
        TCP[TCP control plane]
        SESSION[Session / Lobby / Room]
        KCP[KCP battle endpoint]
        COLLECT[Two-player input collector]
        BF[Battle frame history]
        AUTH[Authoritative battle runtime]
    end

    APP --> UI
    IN --> CN
    CN --> FB
    FB --> SIM
    SIM --> VIEW
    VIEW -. never writes truth back .-> SIM

    TCP --> SESSION
    KCP --> COLLECT
    SESSION --> AUTH
    COLLECT --> BF
    BF --> AUTH
    AUTH --> SIM

    CN <--> TCP
    CN <--> KCP
    PROTO --> CN
    PROTO --> TCP
    PROTO --> KCP
    FRAME --> FB
    FRAME --> COLLECT
    MATH --> SIM
    COLL --> SIM
    SIM --> DIGEST
    SIM --> REPLAY
~~~

Suggested logical ownership, not permission to scaffold it during Gate 0:

~~~text
Client
  Application          explicit app/session/room/battle state
  Network              TCP and KCP adapters; DTO mapping
  LobbyRoom            screens and user commands
  Battle               tick driver, input sampling, frame buffer
  Presentation         GameObject lifecycle and interpolation
  UI                   status, score and diagnostics

Server
  Network              TCP listener and KCP endpoint
  SessionLobbyRoom     nickname/session and guarded room transitions
  Battle               start/result authority and battle registry
  FrameSync            input collector, frame ordering, history, broadcast

Shared
  Protocol             .proto source and generated DTO boundary
  FrameSync            domain InputFrame/FrameData and canonical comparison
  Simulation           state and deterministic Step pipeline
  FixedMath            minimal scaled-integer helpers
  Digest               canonical state writer and stable hash
  Replay               contracts only until the later gate
~~~

Dependency direction is inward: Unity Client and Server depend on Shared; Shared depends only on the supported .NET base class library. Shared never depends on Client or Server.

# 7. Client Responsibilities

The Client:

- owns local UI/application flow and keeps the current Session token;
- samples input every render frame but emits at most one quantized intent for each target logic tick;
- sends BattleId, target tick, player slot/sequence and intent; no authoritative state;
- buffers authoritative FrameData and advances strict simulation only when the next frame is present;
- later predicts missing frames using one documented rule and keeps predicted/authoritative histories;
- runs Shared Simulation for responsiveness and renders its output;
- interpolates Unity GameObjects between logic states without feeding Transform values back;
- reports periodic state digests and network diagnostics;
- treats server StartBattle/Result as lifecycle authority;
- can request frame patch/snapshot only through explicit recovery messages.

The Client must not decide a hit, HP, death, respawn, score, room owner or final result.

# 8. Server Responsibilities

The Server:

- creates ephemeral PlayerId and Session from Nickname and validates each request against the Session;
- owns lobby/room membership, owner, ready state, battle start eligibility and result;
- assigns BattleId, player slot 0/1, deterministic initial state, seed, tick rate and input delay;
- binds the KCP battle connection to the already authenticated TCP Session using a short-lived battle token;
- validates input ownership, tick window, duplicates and legal bit ranges;
- stores one input per active slot per tick, orders slots 0 then 1, and publishes FrameData only when the frame policy permits;
- retains a bounded authoritative frame history for patch/catch-up;
- runs the same Shared simulation headlessly so result and digest checks do not trust a client;
- detects stalls/disconnects and terminates or resolves the battle using a documented policy;
- sends Result on the control plane and transitions players back to Room/Lobby.

For the first strict lockstep slice, a frame advances only after both inputs exist. A production timeout/missing-input policy is **UNKNOWN** until weak-network tests choose among stall, neutral input, repeat-last input or disconnect/forfeit. Whatever policy is chosen must be an explicit authoritative FrameData field; clients must never invent different missing-input behavior.

# 9. Shared Simulation Responsibilities

Shared owns all battle facts:

- BattleState: tick, phase, match timer, score, deterministic seed/RNG state and next entity/projectile ID.
- PlayerState per stable slot: position/velocity on XZ, aim, HP, alive flag, cooldowns, dash state, respawn timer and spawn index.
- ProjectileState in stable ID order: active flag, owner, position/velocity, radius, damage and remaining lifetime.
- Static ArenaData: integer bounds/obstacles/spawn points, versioned and identical on client/server.
- Step ordering, integer math, collision queries/resolution, damage/death/respawn/score and digest serialization.

Suggested deterministic InputFrame payload:

| Field | Purpose |
|---|---|
| Tick | Target simulation tick |
| PlayerSlot | Stable 0 or 1; server validates it |
| MoveX, MoveZ | Quantized signed axes, for example -127…127 |
| Aim | Quantized angle or normalized integer vector; exact format requires a math spike |
| Buttons | Bit flags: Fire, Dash; edge semantics defined per tick |
| Sequence | Network dedupe/diagnostic metadata; excluded from simulation if Tick+Slot already canonical |

BattleId, Session/battle token, acknowledgements and transport sequence belong to the protocol envelope, not deterministic input. World position, camera ray hit, float deltaTime and timestamps must not enter Simulation.Step.

# 10. Control Plane vs Battle Plane

**TCP control plane**

- Login, RoomList, CreateRoom, JoinRoom, LeaveRoom, Ready/CancelReady, StartBattle and Result.
- These messages are infrequent and require reliable ordered delivery and simple request/response error handling.
- TCP head-of-line blocking is acceptable here.

**KCP battle plane**

- FrameInput, FrameData, late-input response, frame patch, digest and future snapshot recovery.
- These messages are frequent, tick-indexed and latency-sensitive. KCP supplies reliable delivery over UDP with tunable retransmission/window behavior and message framing.
- Separate battle backpressure prevents a slow lobby/control message stream from sharing one TCP ordered queue with time-critical frames.

KCP is not magic and is not automatically faster under every loss pattern. Library choice, channel count, MTU, no-delay settings, congestion behavior and mobile compatibility are **UNKNOWN** and require a later isolated transport test. We will integrate a maintained library; self-written KCP is rejected.

# 11. Protocol Strategy

Protobuf is the single schema source for Client and Server. Generated C# types are wire DTOs only.

Rules:

1. Keep .proto files in Shared/Protocol and generate Client/Server code from the same pinned schema version.
2. Put request/response correlation, error code and protocol version in the control-plane envelope.
3. Put BattleId, tick, slot and canonical ordered inputs in battle messages.
4. Map generated DTOs at the boundary to small domain structs. Simulation never accepts a generated Protobuf message directly.
5. Define numeric ranges/defaults explicitly and reject unknown enum values at the boundary.
6. Use repeated inputs in slot order 0,1; validate count and uniqueness.
7. Do not use Protobuf bytes as the state digest. Canonical digest encoding is a separate explicit field writer.
8. Add backward compatibility only when a second deployed version exists; do not build a generic versioning framework now.

Initial message families:

- Control: Login, RoomList, CreateRoom, JoinRoom, LeaveRoom, Ready, CancelReady, StartBattle, Result.
- Battle: BattleHello/bind, FrameInput, FrameData, FramePatchRequest/Response, StateDigest. Snapshot messages are deferred.

Unity 6/IL2CPP compatibility, code-generation workflow and sharing generated assemblies with the .NET server are **UNKNOWN** and must be validated before protocol integration.

# 12. Determinism Strategy

## 12.1 Fixed tick

- Candidate V1 logic rate: 30 Hz. Render rate remains independent.
- A real-time accumulator decides how many ticks are due; Simulation.Step always uses an integer tick and compile-time/configured per-tick constants, never Time.deltaTime.
- Limit catch-up steps per rendered frame and expose backlog. Do not silently discard logic time.
- StartBattle carries tick rate, input delay, initial state version and seed so both sides agree.

Thirty Hz and two delay ticks are recommendations, not yet measurements. They must be validated with dash/projectile speeds and the network simulator.

## 12.2 Numbers and overflow

- Start with scaled integers: millimetres (or another documented scale) for position, per-tick integer velocity, integer HP/timers, and int64 intermediates for squared distance/dot products.
- Quantize aim/input once at the boundary.
- Specify rounding for division, normalization and clamping. Never use platform-dependent float results in battle logic.
- Set world, speed and lifetime bounds and test overflow edges.
- Do not write a general FixedPoint type in V1. If integer-only aim/normalization becomes awkward, run a focused library evaluation rather than expanding the math surface casually.

## 12.3 Stable execution order

The order is part of the protocol:

1. validate FrameData tick and slots;
2. apply player inputs in slot order;
3. update cooldowns/timers;
4. integrate players in slot order;
5. spawn projectiles, assigning monotonically increasing IDs;
6. move projectiles in ascending ID order;
7. generate collision pairs in a documented order;
8. resolve hits once, then damage/death/score;
9. handle respawn/end condition;
10. increment tick and compute digest.

Never depend on Dictionary or HashSet iteration. Collections used in Step are fixed arrays, sorted lists or explicitly sorted ID views.

## 12.4 Collision

The arena is logically 2D on the XZ plane. V1 needs only:

- player circle versus static arena AABB/segments;
- projectile circle/swept segment versus player circle and walls;
- deterministic tie-breaking when several hits share a tick.

High projectile speed requires swept collision or a proven maximum displacement/radius relationship; otherwise tunnelling is a risk. Full 3D physics, arbitrary meshes and a generic collision framework are rejected.

## 12.5 Random

- One deterministic PRNG algorithm with explicit seed and state.
- PRNG state is part of snapshots/digest/replay.
- Random calls occur only at named pipeline points; presentation randomness uses a separate Unity RNG and never affects logic.

## 12.6 State digest

Hash a canonical field stream in stable order:

- tick, phase, match timer, score, seed/PRNG state, next entity ID;
- each player slot's position, velocity, aim, HP, alive/dash/cooldown/respawn state;
- each active projectile ordered by ID with owner, position, velocity, radius, damage and lifetime;
- any mutable arena/object state that affects future simulation.

Exclude Transform, animation, particles, UI, audio, network statistics and frame-buffer bookkeeping. Use a stable explicitly specified hash (candidate xxHash64 or FNV-1a 64); algorithm selection is a small Gate 1 decision. Periodically compare client/server digests and preserve the first mismatch tick plus a canonical state dump.

## 12.7 Unity view boundary

Unity Transform represents a rendered estimate of logic state. Presentation reads previous/current logic poses and interpolates with an alpha derived from the render accumulator. It may spawn animations, particles and audio from simulation events. Simulation never reads Transform/Rigidbody/collider callbacks back as truth.

PhysX/Rigidbody cannot be the frame-sync fact source because floating-point and solver/contact ordering can differ by platform/runtime, fixed-step scheduling can diverge, and Unity engine state is difficult to capture and restore completely. The Corrade demo proves manual tick scheduling is possible; it does not prove cross-platform deterministic PhysX.

# 13. Prediction / Rollback Future Design

This is a design seam only; Gate 0 and proposed Gate 1 do not implement it.

**Prediction**

- For a tick without authoritative FrameData, create a canonical predicted frame.
- Local input is known. Remote input uses one rule, initially repeat the last confirmed continuous axes with one-shot buttons cleared; neutral input is an alternative to validate.
- Store the exact predicted frame bytes/domain values used.

**Dirty frame**

- When authoritative FrameData arrives, canonical-compare the complete ordered input set with the stored predicted frame for that tick.
- Any semantic difference marks that tick dirty. Keep the earliest dirty tick.
- A digest mismatch with equal inputs indicates a determinism bug, not an input prediction error, and should stop/diagnose rather than repeatedly roll back.

**Snapshot**

- Capture at a defined boundary, preferably state at start of tick N.
- Save every mutable future-affecting field listed in Section 12, plus PRNG state and entity-ID allocator.
- Do not save views, network objects or derived caches that can be rebuilt deterministically.
- Store periodic full snapshots plus frame history; exact interval/window is measured later.

**Rollback**

1. Find newest snapshot with tick <= earliest dirty tick.
2. Restore complete BattleState and clear/rebuild derived caches.
3. Replay authoritative frames through Simulation.Step up to the latest confirmed tick.
4. Replay remaining predicted frames to the old local horizon.
5. Replace presentation targets and suppress/deduplicate already-presented side effects.
6. Cap work per rendered frame; reject inputs older than the retained window and request authoritative recovery.

**Cost controls**

- Maximum prediction lead.
- Maximum accepted late-input age.
- Bounded snapshots/frames.
- Snapshot interval chosen from measured state size versus replay cost.
- Catch-up time budget and visible metrics.

# 14. Room Lifecycle

~~~mermaid
stateDiagram-v2
    [*] --> Disconnected
    Disconnected --> Lobby: Login(Nickname)
    Lobby --> Room: CreateRoom / JoinRoom
    Room --> Ready: Ready
    Ready --> Room: CancelReady
    Room --> Lobby: LeaveRoom
    Ready --> BattleStarting: owner Start + both ready
    BattleStarting --> Battle: TCP start info + KCP bind complete
    Battle --> Result: Shared simulation end / disconnect policy
    Result --> Room: ReturnRoom
    Result --> Lobby: ReturnLobby
    Room --> Lobby: room closed
    Lobby --> Disconnected: session closed
~~~

Minimal state ownership:

- Session: PlayerId, Nickname, token, connection state, current RoomId/BattleId.
- Room: RoomId, owner PlayerId, two player slots, ready flags, state Waiting/Starting/InBattle/Result.
- Battle: BattleId, room snapshot, slot mapping, seed/config, tick/input delay, frame collector/history, Shared simulation and result.

Transition rules:

- one Session is in at most one Room and one Battle;
- only owner can Start; exactly two members must be ready;
- room membership freezes during BattleStarting/Battle;
- starting is idempotent by BattleId;
- leaving/disconnect during battle follows one explicit forfeit/reconnect policy;
- Result is written once and returns both sessions through a guarded transition.

No generic FSM library is needed. Enums plus small transition methods and tests are clearer.

# 15. YAGNI Decisions

Meaning: **ADOPT NOW** is a V1 architectural commitment (implementation may be staged by gate); **DEFER** has a real possible future need but is not in the first slice; **REJECT FOR V1** is intentionally outside the demo.

| System | Decision | Reason |
|---|---|---|
| ECS | REJECT FOR V1 | Two players and a bounded projectile list do not justify generated components/query machinery. |
| DI Framework | REJECT FOR V1 | Plain constructors/composition root are sufficient. |
| EventBus | REJECT FOR V1 | Prefer direct calls and narrow C# events; a global bus hides ordering, which is dangerous in deterministic logic. |
| Service Locator | REJECT FOR V1 | Hidden dependencies and global mutable state harm tests and rollback. |
| Manager Pattern | REJECT FOR V1 | No generic Manager hierarchy; allow one clearly named lifecycle coordinator where ownership demands it. |
| Repository Pattern | REJECT FOR V1 | No database/persistence domain exists. |
| NavMesh | REJECT FOR V1 | Small static arena and direct movement need no pathfinding. The currently installed Unity navigation package is not permission to use it in battle logic. |
| BehaviorTree | REJECT FOR V1 | No AI requirement in 1v1. |
| Skill Timeline | REJECT FOR V1 | Fire and dash are explicit cooldown/state transitions. |
| Buff | REJECT FOR V1 | No buff gameplay requirement. |
| Combo | REJECT FOR V1 | No combo gameplay requirement. |
| RootMotion | REJECT FOR V1 | Animation cannot author deterministic movement. |
| MySQL | DEFER | Explicitly postponed until the frame-sync core; not required for Nickname/PlayerId/Session. |
| Account System | REJECT FOR V1 | Use ephemeral lightweight login; no password/registration/persistence. |
| Matchmaking | REJECT FOR V1 | Room list/create/join covers two-player discovery. |
| Chat | REJECT FOR V1 | No core demo value. |
| Replay | DEFER | Preserve initial-state + frame-history seam and add deterministic replay tests; user-facing replay comes later. |
| FSM | ADOPT NOW | Use explicit enum states and guarded transitions, not an FSM framework. |
| Render Interpolation | ADOPT NOW | A 30 Hz logic tick needs a smooth Unity view; it stays presentation-only. |
| Object Pool | DEFER | Two players/few projectiles can allocate simply; add only after profiling or obvious projectile churn. |
| Self-developed KCP | REJECT FOR V1 | Transport correctness is not the learning goal; select a maintained library. |
| Self-developed FixedPoint | REJECT FOR V1 | Use minimal scaled integers/int64 intermediates; evaluate a library only if actual math demands it. |
| Protobuf | ADOPT NOW | One shared typed contract avoids hand-written codecs; keep DTOs outside Simulation. |
| TCP + KCP dual channel | ADOPT NOW | Control and battle traffic have materially different ordering/latency/backpressure needs. Integrate in separate later steps. |
| Prediction | DEFER | Strict lockstep must be measured first. |
| Snapshot/Rollback | DEFER | Required future seam, but premature implementation would obscure basic correctness. |
| State Digest | ADOPT NOW | Cheap, testable evidence of deterministic agreement from the first core slice. |
| Network Simulator | ADOPT NOW | Fixed delay and recovery policy cannot be chosen credibly without it. |

# 16. Gate 1 Proposed Scope

Gate 1 should be an offline deterministic-core learning slice, not networking:

**Goal:** prove that two independent Shared simulations starting from the same initial BattleState and consuming the same recorded FrameData produce identical state and digest for every tick.

**In scope**

- Decide and document 30 Hz plus coordinate/unit bounds.
- Define minimal BattleState, two PlayerState values, InputFrame/FrameData and a stable Step pipeline.
- Implement scaled-integer movement, aim representation and arena bounds only.
- Implement stable canonical digest.
- Run twin-simulation tests over scripted inputs, including no-input, opposing movement, boundary clamp and long-run digest equality.
- Add a replay-style test that rebuilds final state from initial state + frame history.
- Add determinism guard tests for input slot ordering and collection iteration.

**Out of scope**

- Unity scene/GameObjects, projectile/combat, network sockets, TCP, KCP, Protobuf integration, lobby/room UI, prediction, snapshot, rollback, reconnect and user-facing replay.

**Exit evidence**

- Shared code has no UnityEngine reference.
- Full test suite passes repeatedly.
- Two simulation instances match digest at every tested tick.
- Replay final digest matches the original run.
- No unordered collection participates in Step.
- Owner and independent Reviewer approve the data model and Gate 2 scope.

This scope is intentionally smaller than “basic online battle.” It isolates the hardest invariant before transport and presentation add noise.

# 17. Risks / Unknowns

| Item | Status | Validation needed before commitment |
|---|---|---|
| Unity 6 Shared project structure | UNKNOWN | Prove one pure C# source/assembly can be consumed by Unity and headless .NET tests/server without duplicated files or Unity references. Compare asmdef + external csproj versus a local package. |
| Logic tick rate/input delay | UNKNOWN | Test 30 Hz and 2 ticks with actual dash/projectile speeds plus latency/jitter/loss profiles. |
| Aim quantization/integer normalization | UNKNOWN | Small math spike comparing angle quantization versus integer direction vectors and overflow/rounding. |
| Fixed math library | UNKNOWN / likely unnecessary for V1 | Start with scaled integers; evaluate maintained libraries only if required operations cannot remain small and explicit. |
| KCP library/settings | UNKNOWN | Check maintenance, license, Unity 6/IL2CPP, .NET server support, MTU, channels and simulated weak-network behavior. |
| Protobuf integration | UNKNOWN | Validate code generation, package size, AOT/IL2CPP and a shared schema workflow. |
| KCP authentication/binding | UNKNOWN | Design battle token, replay protection and Session-to-endpoint binding after TCP login exists. |
| Missing-input/disconnect policy | UNKNOWN | Use network simulator to choose stall versus authoritative neutral/repeat-last and reconnect/forfeit limits. |
| Projectile collision | UNKNOWN | Validate swept circle/segment collision and deterministic tie-breaks at maximum speed. |
| Digest algorithm | UNKNOWN | Choose stable 64-bit algorithm and canonical byte order; add golden vectors across Unity/.NET. |
| Snapshot interval/window | UNKNOWN | Measure BattleState byte size and replay cost after simulation exists. |
| Unity 6 cross-platform determinism | UNKNOWN | Run golden frame/digest vectors in Editor, standalone and server runtime; integer logic reduces but does not remove runtime-order mistakes. |
| Server authority/result | DECIDED conceptually | Server runs Shared simulation; exact digest cadence and client mismatch response remain to validate. |
| Current default Unity packages | OUT OF SCOPE | Gate 0 changes none. Remove unused template packages only under a later approved gate. |

## 17.1 Direct answers to the 22 required architecture questions

1. **Client responsibility:** input sampling/quantization, transport, frame buffering, Shared simulation, presentation/interpolation, UI and diagnostics; never author battle facts.
2. **Server responsibility:** Session/Room/Battle authority, input validation/collection/order, authoritative frame/history, Shared simulation, digest/result and disconnect policy.
3. **Shared layer:** yes; it owns domain frame types, deterministic simulation/math/collision/digest and protocol schema boundaries.
4. **Unity-independent simulation:** pure C# state plus Step(state, frame), with no UnityEngine, wall clock, socket or generated DTO dependency.
5. **Player/projectile/collision:** stable-ID data in BattleState, updated in explicit order by small integer XZ collision routines.
6. **Unity Transform role:** interpolated presentation target only.
7. **Why not Rigidbody/PhysX:** floating-point/solver/contact/scheduling differences and nontrivial snapshot/restore make them unsafe as shared truth.
8. **Fixed tick:** accumulator schedules integer ticks; Step uses per-tick constants, bounded catch-up and no Time.deltaTime.
9. **Input frame:** tick, slot, quantized move/aim and Fire/Dash bits; no world state.
10. **Server collection:** tick → fixed two-slot input set with dedupe and validation, then canonical slot ordering.
11. **Advance condition:** strict V1 publishes when both active-slot inputs exist; timeout policy remains UNKNOWN and must be authoritative.
12. **Why fixed input delay:** gives packets time to arrive before their target simulation tick, reducing stalls and later prediction corrections at a known latency cost.
13. **Why prediction:** strict waiting makes local control visibly stall under latency/loss; prediction hides that wait after the core is proven.
14. **Dirty frame:** canonical predicted FrameData differs from authoritative FrameData for the same tick; keep the earliest mismatch.
15. **Snapshot contents:** all mutable future-affecting state, including tick/phase, players, projectiles, timers/score, PRNG and ID allocator.
16. **Rollback:** restore nearest snapshot at/before dirty tick, replay authoritative frames, then re-predict to the previous horizon.
17. **Replay reuse:** initial state plus ordered input history fully determines Step, so no second gameplay path is needed.
18. **State digest:** canonical tick/phase/RNG/allocator, player and projectile state, scores/timers and mutable arena state; exclude presentation/network.
19. **TCP/KCP split:** reliable ordered low-rate lifecycle traffic and latency-sensitive tick traffic need different queues/tuning/backpressure.
20. **Protobuf placement:** one Shared schema/generated boundary used by Client/Server, mapped into domain types; Simulation does not depend on DTOs.
21. **Room/Session/Battle lifecycle:** explicit guarded states from Login → Lobby → Room → Ready → Battle → Result → Room/Lobby, owned by Server.
22. **Render versus logic:** logic advances at fixed tick; render runs independently and interpolates previous/current logic poses.

## Gate 0 boundary self-check

- Unity Scene/GameObjects changed: **NONE**
- Business scripts created: **NONE**
- ProjectSettings changed: **NONE**
- Packages changed: **NONE**
- FrameSync/Server/Client/KCP/TCP/Protobuf/FixedPoint/Projectile/Collision/Prediction/Rollback/Replay implemented: **NONE**
- Only planned repository change: this architecture study document.

Gate 1 must not begin until the Owner receives independent ChatGPT Gate 0 approval.
