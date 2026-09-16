# Lockstep Arena v3-A — Game Shell / LAN UX / Lobby & Room Presentation

Status: APPROVED DESIGN
Baseline: `f6e0c1a0d6ec957e84b8c0b761e7aaf9a33ddf57`
Branch: `v3-presentation`
Unity: `6000.3.10f1`

## 1. Goal

v3-A transforms the pre-battle experience from an engineering/debug surface into a cohesive game shell while preserving the already accepted lockstep gameplay core.

The target flow is:

`Launch -> Main Menu -> Start Game -> Create LAN Game / Join LAN Game -> Lobby -> Room -> Ready -> Start -> existing BattleScene`

v3-A ends when both host and guest can reach the existing battle flow through the new presentation layer.

## 2. Frozen core

The following systems are out of scope and must remain behaviorally unchanged unless a concrete blocking defect is discovered:

- deterministic Simulation
- frame synchronization protocol
- server authority
- prediction
- Dirty detection
- rollback / re-simulation
- Replay
- StateDigest
- projectile gameplay rules
- damage and HP rules
- BO3 rules
- existing battle lifecycle contracts

v3-A is a game-shell and presentation pass, not a new gameplay version.

## 3. UI technology

Use Unity UGUI for all player-facing v3 UI, with TextMeshPro for text.

Player-facing screens use UGUI:

- Main Menu
- Multiplayer Mode Select
- Create LAN Game
- Join LAN Game
- Lobby
- Room
- loading / progress overlays
- user-readable connection errors

The existing F1 engineering diagnostics may remain on the current debug rendering path for now. It is intentionally not part of the polished player UI in v3-A.

Do not introduce a second UI framework in parallel.

## 4. Visual direction

Overall style: low-poly sci-fi arena with light esports presentation.

Visual language:

- dark blue-black / charcoal background
- cyan-blue primary accent
- red enemy / warning accent
- white / light gray text
- large primary calls to action
- restrained sci-fi frames and panels
- readable hierarchy over decorative density

The experience should look like a small complete game, not an admin panel or network test utility.

## 5. Art source policy

External source location supplied by the project owner:

`E:\unityproject\LockstepArena\LockstepArena_ArtSource`

Expected source folders:

- `01_Environment`
- `02_Props`
- `03_Characters`
- `04_UI`

Codex must not copy entire asset packs into the Unity project.

Only selected assets actually used by v3-A should enter the repository.

Formal imported art should be organized under a clean project structure such as:

- `Assets/Art/UI/SciFi/`
- `Assets/Art/Environment/SciFiArena/`
- `Assets/Art/Props/`
- `Assets/Art/Characters/PlayerRobot/` only if a lightweight static/menu usage is justified
- `Assets/Art/Materials/`

v3-A should primarily consume `04_UI`, with only a small selection from `01_Environment` and `02_Props` for menu/lobby background dressing.

Character rigging, gameplay Animator integration, weapon animation, VFX, and audio belong to v3-B unless they are trivial non-gameplay presentation elements.

## 6. Main Menu

First-launch player-facing screen should no longer expose nickname, room id, server address, ports, or debug buttons.

Primary composition:

- game title: `LOCKSTEP ARENA`
- subtitle / presentation line: `FRAME BATTLE`
- primary button: `开始游戏`
- secondary entries: `设置`, `退出`
- optional small version/build label
- background: simple sci-fi arena presentation using selected existing assets, not a blank technical panel

Settings may remain minimal in v3-A. Do not build a large settings system.

## 7. Multiplayer mode select

`开始游戏` opens a mode-selection screen with two dominant cards:

### Create LAN Game

Player-readable explanation:

- create a game on this computer
- friends on the same LAN can join
- the game starts the independent server automatically

### Join LAN Game

Player-readable explanation:

- connect to a friend's LAN game
- requires the host's local IPv4 address

Do not expose transport terminology such as CONTROL socket, BATTLE socket, TCP internals, bind address, or command-line flags in normal UI.

## 8. Create LAN Game flow

Form fields:

- nickname
- room name

Primary action:

- `创建游戏`

Expected host flow:

1. launch the independent `LockstepArena.Server.DemoHost` process
2. wait for the server to become connectable / ready
3. connect local client to `127.0.0.1`
4. enter the nickname session
5. create the room
6. transition into the polished room screen

The server remains an independent process. Do not embed server gameplay logic inside Unity.

Player-facing progress may show stages such as:

- 启动服务器
- 连接服务器
- 创建房间

Errors must be human-readable and recoverable with `重试` / `返回` actions. Do not dump stack traces in normal UI.

## 9. Server process ownership

The Unity client may only terminate a DemoHost process that it started itself.

Required ownership rule:

- manually started server: never kill it automatically
- server started by this client instance: may be terminated during explicit host shutdown / application exit if appropriate

Avoid broad process-name killing.

If the configured ports are unavailable or server startup fails, present a normal user-facing error and return to a safe menu state.

## 10. Join LAN Game flow

Fields:

- nickname
- host IPv4 address

Primary action:

- `加入游戏`

The join screen may remember the most recently successful host address if this is straightforward.

LAN auto-discovery / UDP broadcast is explicitly out of scope for v3-A.

## 11. Lobby layout

Use a conventional two-column multiplayer layout:

Left:

- room list
- room status
- participant count
- refresh action
- create room action

Right:

- selected room details
- room owner
- participant count
- primary `加入房间` action when valid

Room states should be visually distinguishable, for example:

- waiting: blue-gray
- ready / available: cyan accent
- in battle: amber / yellow
- full / unavailable: muted gray
- connection / error: red

Do not make users infer state only from raw protocol text.

## 12. Room layout

Room presentation should feel like a versus setup rather than a debug list.

Layout:

- leave-room action in the top-left
- room name centered/top area
- host invite information in the top-right when applicable
- two player cards around a central `VS`
- player names
- ready state on each card
- `BEST OF THREE` rule label
- local Ready / Cancel Ready action
- host-only Start action

When not all players are ready, the host start area should explain the state, e.g. `等待玩家准备...`, rather than presenting a mysterious disabled button.

When all required players are ready, `开始比赛` becomes the obvious primary action.

## 13. Host invite information

For a locally hosted LAN game, show the host's usable LAN IPv4 in the room UI.

Example:

`192.168.1.23`

Provide:

- `复制地址`

Optionally, if inexpensive:

- `复制邀请信息`

The application may choose a practical active private IPv4 address. Do not overbuild network adapter selection in v3-A.

## 14. Existing networking integration

The new UI must drive the existing client/session/room APIs rather than reproducing business logic.

The UGUI shell should consume current client state and invoke existing operations such as:

- connect
- enter session
- request room list
- create room
- join room
- leave room
- ready / unready
- start battle

Do not create a parallel room model or duplicate network state machine for UI convenience.

## 15. Scene / shell architecture

Prefer a small presentation shell with clear responsibilities.

Suggested conceptual components (names may vary if repository patterns suggest better names):

- `GameShellController`: high-level screen transitions and connection UX
- `LanHostLauncher`: independent DemoHost process lifecycle and host-address discovery
- `MainMenuView`
- `ModeSelectView`
- `CreateLanView`
- `JoinLanView`
- `LobbyView`
- `RoomView`

Views display state and forward user intent. They do not own Simulation or network authority.

Avoid turning the existing persistent network root into a giant UI/gameplay manager.

## 16. Battle transition

v3-A ends at a successful handoff into the already existing BattleScene flow.

Do not redesign Battle HUD, robot gameplay visuals, projectile visuals, arena visuals, animation, VFX, or audio as part of v3-A.

Temporary existing BattleScene presentation is acceptable after Start.

Those belong to v3-B.

## 17. Error handling

Normal UI should cover at least:

- invalid IPv4
- cannot connect to host
- server executable missing / cannot launch
- local ports unavailable / server startup failure
- nickname rejection
- room no longer available
- room full
- not all players ready
- preparation abort / return to safe lobby state

Errors should be concise and player-readable.

Technical details may still be available in F1/debug output.

## 18. Accessibility / usability baseline

- primary actions are visually dominant
- disabled actions explain why where practical
- labels use player language, not protocol language
- keyboard/mouse focus should be sane
- UI should remain usable at common 16:9 desktop resolutions
- do not place all controls in one unstructured row
- back navigation should be predictable

## 19. Testing

v3-A should add focused tests around the new shell without duplicating the full historical test matrix on every change.

Required coverage should include, where practical:

- host launcher does not kill unrelated/manual server processes
- host flow connects to localhost after owned server startup
- failed host startup returns a recoverable UI state
- invalid join address is rejected before connection attempt
- UI state reflects Lobby / Room / Ready / Start business state
- host Start remains unavailable until readiness requirements are satisfied
- presentation shell reaches the existing BattleScene flow without modifying gameplay contracts

Existing deterministic/network tests must remain intact.

## 20. v3-A acceptance criteria

v3-A is complete when a Windows build demonstrates:

Host path:

`Launch -> Main Menu -> Start Game -> Create LAN Game -> nickname/room -> automatic independent DemoHost startup -> local connection -> Room`

Guest path:

`Launch -> Main Menu -> Start Game -> Join LAN Game -> nickname/IP -> connection -> Lobby -> Join Room`

Shared path:

`Room -> Ready -> all players ready -> host Start -> existing BattleScene`

And:

- normal UI uses UGUI/TextMeshPro rather than the current GUILayout debug surface
- visual presentation consistently uses the selected sci-fi UI language
- only required art assets are imported
- normal users do not need to manually start DemoHost for the host flow
- independent Server architecture remains intact
- core lockstep systems remain behaviorally unchanged

## 21. Explicit non-goals

Not part of v3-A:

- final Battle HUD
- final robot gameplay model integration
- Animator combat presentation
- arena gameplay art replacement
- final projectile art
- VFX
- audio
- rendering interpolation
- LAN auto-discovery
- Internet matchmaking
- account system
- chat
- public Internet hosting
- final two-PC acceptance

These remain for v3-B / v3-C as already approved.

## 22. v3 roadmap context

The approved v3 structure is exactly three formal Steps:

- v3-A — Game Shell / LAN UX / Lobby & Room Presentation
- v3-B — Battle Presentation
- v3-C — Final Polish / Complete Game

Do not create v3-A1, v3-A2, new Gates, or hidden checkpoint phases. Internal implementation tasks are not formal Steps.
