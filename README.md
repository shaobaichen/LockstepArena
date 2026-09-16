# Lockstep Arena

Lockstep Arena is a small deterministic lockstep gameplay demo. Two Unity clients enter a nickname session and room, freeze the battle roster, then play a server-authoritative projectile arena with client prediction, Dirty detection, rollback/re-simulation, authoritative Replay, and `StateDigest` verification.

## Prerequisites

- Windows with the .NET 8 SDK.
- Unity `6000.3.10f1` installed at `E:\unityhub\unity6.3\Editor\Unity.exe` for the Unity demo and EditMode verification.
- TCP ports `46000` (CONTROL) and `46001` (BATTLE) available.

## Build and run

```powershell
dotnet build Server/LockstepArena.Server.DemoHost/LockstepArena.Server.DemoHost.csproj -c Release
.\Server\LockstepArena.Server.DemoHost\bin\Release\net8.0\LockstepArena.Server.DemoHost.exe --bind 0.0.0.0 --control-port 46000 --battle-port 46001
```

The server prints its bind address and ports and stops when `Q` is pressed in its terminal. In Unity, open `Assets/LockstepArenaDemo/Scenes/LobbyScene.unity`, ensure both `LobbyScene` and `BattleScene` are enabled in Build Settings, and build a Windows x86-64 Player. Copy the entire Player output directory when moving it to another PC.

## Local two-client demo

Start two copies of the Windows Player. Use server address `127.0.0.1`, unique nicknames such as `Alpha` and `Bravo`, then select `Connect` and `Enter` on both clients.

1. Alpha creates a room with capacity `2`.
2. Bravo selects `List`, enters the room id, and selects `Join`.
3. Both clients select `Ready`; the host selects `Start`.
4. After `3/2/1`, use `WASD` to move, the mouse to aim, and hold the left mouse button to fire.
5. Finish the best-of-three match, verify settlement, and select `Return To Lobby`.
6. Without restarting the server or clients, create/join another room and start a second battle to verify clean lifecycle reuse.

Press `F1` to show the Debug panel. It exposes connection phase, room/roster state, server/authority/predicted Tick, pending counts, Replay, Dirty count, digests, and settlement verification.

## LAN demo

Run the server and Client A on PC A. Run `ipconfig` on PC A and note the active adapter's IPv4 address. Client A may use `127.0.0.1`; Client B on the same LAN must use PC A's IPv4 address. If Windows Firewall prompts for the server or Player, allow access on **Private networks**. Do not expose this Debug demo directly to the public Internet.

## Diagnostics

`LockstepArenaDemoController` renders the current immutable client snapshot. `LatestDirty` reports the most recently reconciled authoritative frame; `CumulativeDirty` counts rollback-triggering reconciliations. `SettlementVerified=True` means final per-slot state, Tick, digest, prediction convergence, and authoritative Replay were checked before the battle socket was released.

## Verification

Run every formal .NET test project under `Tests` and the Unity EditMode suite. For a focused current business-flow check:

```powershell
dotnet run --project Tests/LockstepArena.DemoFlow.Tests/LockstepArena.DemoFlow.Tests.csproj -c Release
```

The complete historical verification matrix is in [the Gate 14 architecture](Docs/Architecture/GATE14_MINIMAL_TCP_LOBBY_TO_BATTLE_DEMO.md).

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

## Current gameplay and limitations

The current gameplay is a fixed-obstacle projectile arena with hit points, round countdowns, draws, and best-of-three settlement. v1 is a TCP Debug demonstration, not a public-network service. It intentionally has no account database, password/registration, chat, matchmaking service, TLS, KCP/UDP, reconnect, heartbeat, adaptive input delay, missing-input substitution, or rendering interpolation. Slight remote visual jitter is accepted in v1. The current primitive visuals are placeholders; a later v3 Presentation / Art Pass can replace the presentation layer without changing deterministic gameplay.
