# Lockstep Arena

Lockstep Arena is a small deterministic frame-synchronization learning project. The v1 demo joins two loopback TCP clients through a nickname lobby, freezes a room roster, runs the shared simulation with client prediction and authoritative rollback/replay, verifies settlement, and returns both sessions to the lobby.

## Prerequisites

- Windows with .NET 8 SDK.
- Unity `6000.3.10f1` installed at `E:\unityhub\unity6.3\Editor\Unity.exe` for the Unity demo and EditMode verification.
- Loopback ports `46000` (CONTROL) and `46001` (BATTLE) available for the manual demo.

## Run the server

```powershell
dotnet run --project Server/LockstepArena.Server.DemoHost/LockstepArena.Server.DemoHost.csproj -c Release
```

The host binds only `127.0.0.1`, prints its CONTROL and BATTLE ports, and stops when `Q` is pressed in its terminal.

## Run the Unity demo

Open `Assets/LockstepArenaDemo/Scenes/LockstepArenaDemo.unity` in Unity. Build one Windows Player from that scene and keep a second copy running in the Editor. Use nickname `Bravo` in the first client and `Alpha` in the second. The on-screen Debug panel exposes connection phase, room list and participants, frozen battle roster, server/authority/predicted Tick, pending counts, Replay count, Dirty status, digests, and settlement verification.

## Demo procedure

1. Build and start DemoHost.
2. Open the dedicated Unity scene.
3. Launch one Player and one Editor client.
4. Connect the Player first and enter `Bravo`.
5. Connect the Editor second and enter `Alpha`.
6. Alpha creates `Golden Room` with capacity `2`.
7. Bravo selects List and joins Room `1`.
8. Confirm both panels show Alpha as host and Bravo as participant.
9. Select Start from Bravo and confirm `NOT_HOST` rejection.
10. Ready Alpha only, Start from Alpha, and confirm `NOT_READY` rejection.
11. Ready Bravo.
12. Start from Alpha.
13. Confirm roster `Slot0/PlayerId2` and `Slot1/PlayerId1`.
14. Confirm both clients enter `InBattle` after separate BATTLE attachment acceptance.
15. Confirm the Gate 13 predicted client runtime is active on both clients.
16. Observe server, authority and predicted Tick, pending counts, Replay and Dirty diagnostics.
17. Confirm each client accumulates four Dirty reconciliations.
18. Confirm settlement at State Tick `4` with digest `D8E54FF828A4C670` on both clients.
19. Return both clients and confirm Room `1` is absent from the refreshed room list.
20. Exit both clients, press `Q` in DemoHost, and close the Player/Editor.

The scripted Golden input is selected by the exact nicknames `Alpha` and `Bravo`. Other nicknames send neutral movement in this Debug demo.

## Diagnostics

`LockstepArenaDemoController` renders the current immutable client snapshot. `LatestDirty` reports the most recently reconciled authoritative frame; `CumulativeDirty` counts rollback-triggering reconciliations. `SettlementVerified=True` means final per-slot state, Tick, digest, prediction convergence, and authoritative Replay were all checked before the battle socket was released.

## Verification

Run the Gate 14 suite:

```powershell
dotnet run --project Tests/LockstepArena.DemoFlow.Tests/LockstepArena.DemoFlow.Tests.csproj -c Release
```

It must report `RESULT 48/48 passed`. The complete verification matrix and exact Unity XML requirements are in [the Gate 14 architecture](Docs/Architecture/GATE14_MINIMAL_TCP_LOBBY_TO_BATTLE_DEMO.md).

## Architecture and Gate history

- Gate 0: repository and scope baseline.
- Gate 1: deterministic shared simulation.
- Gate 2: one physical simulation source consumed by Unity and .NET.
- Gate 3: variable roster and strict complete frames.
- Gate 4: continuous authoritative multi-Tick publication.
- Gate 5: explicit Protobuf/Domain boundary.
- Gate 6: protocol-aware server authority composition.
- Gate 7: length-prefixed byte-stream framing.
- Gate 8: finite real TCP loopback proof.
- Gate 9: fixed InputDelay and logical authority scheduling.
- Gate 10: synchronous Stopwatch pacing.
- Gate 11: minimal live TCP battle runtime.
- Gate 12: bounded client prediction, Dirty detection, rollback and re-simulation.
- Gate 13: two-client shared live prediction and authoritative Replay.
- Gate 14: nickname/room/ready/start/settlement Debug demo and v1 closure.

See [Lockstep Arena v1 Architecture](Docs/Architecture/LOCKSTEP_ARENA_V1_ARCHITECTURE.md) for the ownership and data-flow diagram.

## TCP-only v1 limitations

v1 is a loopback Debug demonstration, not a public-network service. It intentionally has no account database, password/registration, chat, matchmaking service, TLS, KCP/UDP, reconnect, heartbeat, adaptive input delay, missing-input substitution, rendering interpolation, combat system, server cluster, or generic DI/EventBus/ECS/netcode framework. A missing mature input strictly blocks later authoritative frames.
