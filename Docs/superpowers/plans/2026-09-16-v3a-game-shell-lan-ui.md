# Lockstep Arena v3-A Game Shell / LAN UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the pre-battle engineering/debug surface with a polished UGUI game shell that supports Main Menu -> Create/Join LAN -> Lobby -> Room -> Ready/Start -> existing BattleScene, including automatic launch of the independent DemoHost for the host path.

**Architecture:** Keep the accepted deterministic gameplay/network stack frozen. Extend the client demo read model with structured room data, keep `LockstepArenaDemoController` as the persistent app/network owner, add a small owned-server launcher and LAN address utility, and place all player-facing v3-A screens in scene-local UGUI presentation components. The independent DemoHost remains a separate process; the Unity client only starts/stops the process instance it owns.

**Tech Stack:** Unity `6000.3.10f1`, UGUI `2.0.0`, TextMeshPro, Input System `1.18.0`, existing TCP/Protobuf client/server packages, .NET 8 DemoHost, Windows x86-64.

**Spec:** `Docs/Architecture/V3A_GAME_SHELL_LAN_UI_DESIGN.md`

## Global Constraints

- Branch: `v3-presentation`.
- Design baseline parent: `f6e0c1a0d6ec957e84b8c0b761e7aaf9a33ddf57`.
- Current design commit: `16becb1ed107afb37240e4ce219a3df4741557d0`.
- Unity version is exactly `6000.3.10f1`.
- Do not behaviorally change deterministic Simulation, frame sync, server authority, prediction, Dirty detection, rollback/re-simulation, Replay, StateDigest, projectile rules, damage/HP, BO3, or existing battle lifecycle contracts.
- Do not introduce UI Toolkit alongside UGUI. Player-facing v3-A UI is UGUI + TextMeshPro.
- Keep F1 diagnostics on the existing engineering/debug path for v3-A; only normal player UI leaves IMGUI.
- External art source is `E:\unityproject\LockstepArena\LockstepArena_ArtSource` and must never be committed wholesale.
- Only selected art actually used by v3-A enters `Assets/Art/...`.
- Do not integrate gameplay character rigging, Animator combat logic, weapon animation, VFX, or audio in v3-A.
- The Unity client may terminate only the DemoHost process instance that it started itself. Never kill by process name.
- LAN discovery/broadcast is out of scope. Guest joins by host IPv4.
- Preserve user-owned uncommitted files `Assets/Settings/Mobile_RPAsset.asset` and `ProjectSettings/ShaderGraphSettings.asset` exactly.
- Do not use `git reset --hard`, `git clean`, rebase, amend, squash, or force-push.
- Internal tasks below are implementation units, not additional v3 Steps/checkpoints. Execute all of v3-A continuously and produce one final v3-A handoff.

## File Structure

### Existing files to modify

- `.gitignore` — ignore the local source-art staging folder.
- `Packages/com.locksteparena.client-demo/Runtime/DemoClientSnapshot.cs` — expose structured room/participant read data while preserving existing diagnostic strings.
- `Packages/com.locksteparena.client-demo/Runtime/TcpDemoClient.cs` — retain latest structured room list/snapshot data.
- `Tests/LockstepArena.DemoFlow.Tests/SessionRoomTests.cs` — verify structured client read model.
- `Tests/LockstepArena.DemoFlow.Tests/Program.cs` — register any new DemoFlow test only if the current runner requires explicit registration.
- `Assets/LockstepArenaDemo/Runtime/LockstepArena.Demo.asmdef` — reference UGUI/TextMeshPro assemblies.
- `Assets/LockstepArenaDemo/Runtime/LockstepArenaDemoController.cs` — remain persistent network/app root, expose game-shell commands/state, automate host/join flow, retain F1 debug only.
- `Assets/LockstepArenaDemo/Scenes/LobbyScene.unity` — scene-local v3-A Canvas, EventSystem, background presentation, shell view.
- `Assets/LockstepArenaDemo/Tests/Editor/UnityDemoPresentationTests.cs` — preserve current tests and add shell integration checks where reflection is sufficient.
- `Assets/LockstepArenaDemo/Tests/Editor/UnityDemoSceneTests.cs` — assert LobbyScene contains the v3-A shell and BattleScene remains a separate existing battle scene.

### New runtime files

- `Packages/com.locksteparena.client-demo/Runtime/DemoRoomSummarySnapshot.cs`
- `Packages/com.locksteparena.client-demo/Runtime/DemoRoomParticipantSnapshot.cs`
- `Assets/LockstepArenaDemo/Runtime/Shell/ShellConnectionStage.cs`
- `Assets/LockstepArenaDemo/Runtime/Shell/LanServerPathResolver.cs`
- `Assets/LockstepArenaDemo/Runtime/Shell/LanServerLauncher.cs`
- `Assets/LockstepArenaDemo/Runtime/Shell/LanAddressUtility.cs`
- `Assets/LockstepArenaDemo/Runtime/UI/GameShellUiController.cs`
- `Assets/LockstepArenaDemo/Runtime/UI/RoomListItemView.cs`
- `Assets/LockstepArenaDemo/Runtime/UI/PlayerCardView.cs`

### New editor/build files

- `Assets/LockstepArenaDemo/Editor/V3AWindowsBuild.cs` — one-click final Windows build that publishes the independent DemoHost next to the Player.
- `Assets/LockstepArenaDemo/Editor/LockstepArena.Demo.Editor.asmdef` — editor-only assembly if no suitable editor asmdef already exists.

### New art/presentation content

- `Assets/Art/ART_SOURCES.md`
- `Assets/Art/UI/SciFi/` — selected Kenney Sci-Fi UI sprites only.
- `Assets/Art/Environment/SciFiArena/` — a small selected set for menu/lobby background dressing only.
- `Assets/Art/Props/` — a small selected set only when actually used.
- `Assets/LockstepArenaDemo/Prefabs/UI/GameShellCanvas.prefab`
- `Assets/LockstepArenaDemo/Prefabs/UI/RoomListItem.prefab`
- `Assets/LockstepArenaDemo/Prefabs/UI/PlayerCard.prefab`

---

### Task 1: Protect the external art source and expose structured room read data

**Files:**
- Modify: `.gitignore`
- Create: `Packages/com.locksteparena.client-demo/Runtime/DemoRoomSummarySnapshot.cs`
- Create: `Packages/com.locksteparena.client-demo/Runtime/DemoRoomParticipantSnapshot.cs`
- Modify: `Packages/com.locksteparena.client-demo/Runtime/DemoClientSnapshot.cs`
- Modify: `Packages/com.locksteparena.client-demo/Runtime/TcpDemoClient.cs`
- Modify/Test: `Tests/LockstepArena.DemoFlow.Tests/SessionRoomTests.cs`

**Interfaces:**
- Produces: `DemoClientSnapshot.Rooms`, `DemoClientSnapshot.Participants`, `DemoClientSnapshot.RoomHostSessionId`, `DemoClientSnapshot.RoomCapacity`, `DemoClientSnapshot.RoomLifecycle`.
- Existing `RoomList` and `RoomParticipants` diagnostic strings remain unchanged for F1/debug compatibility.

- [ ] **Step 1: Ignore the local art staging directory before inspecting/importing assets**

Add exactly this repository-root ignore rule:

```gitignore
/LockstepArena_ArtSource/
```

Run:

```powershell
git status --short --ignored LockstepArena_ArtSource
```

Expected: the staging directory is ignored and cannot be accidentally added by a broad `git add`.

- [ ] **Step 2: Write the failing structured-snapshot test**

Add a DemoFlow test that drives two clients through session entry, room creation/join, room-list refresh and Ready state, then asserts structured data instead of parsing formatted strings. The assertions must include:

```csharp
DemoClientSnapshot lobby = guest.Snapshot;
TestAssert.Equal(1, lobby.Rooms.Count);
TestAssert.Equal("Room", lobby.Rooms[0].RoomName);
TestAssert.Equal(2U, lobby.Rooms[0].Capacity);
TestAssert.Equal(1U, lobby.Rooms[0].ParticipantCount);

DemoClientSnapshot room = host.Snapshot;
TestAssert.Equal(host.Snapshot.SessionId, room.RoomHostSessionId);
TestAssert.Equal(2U, room.RoomCapacity);
TestAssert.Equal(2, room.Participants.Count);
TestAssert.True(room.Participants[0].IsHost);
TestAssert.True(room.Participants[0].IsReady);
```

Run:

```powershell
dotnet run --project Tests/LockstepArena.DemoFlow.Tests/LockstepArena.DemoFlow.Tests.csproj -c Release
```

Expected: FAIL because the structured snapshot members do not yet exist.

- [ ] **Step 3: Add immutable structured snapshot value types**

Implement the two classes with constructor-only state:

```csharp
public sealed class DemoRoomSummarySnapshot
{
    public DemoRoomSummarySnapshot(ulong roomId, string roomName, string hostNickname,
        uint participantCount, uint capacity, RoomLifecycleMessage lifecycle)
    {
        RoomId = roomId;
        RoomName = roomName ?? throw new ArgumentNullException(nameof(roomName));
        HostNickname = hostNickname ?? throw new ArgumentNullException(nameof(hostNickname));
        ParticipantCount = participantCount;
        Capacity = capacity;
        Lifecycle = lifecycle;
    }

    public ulong RoomId { get; }
    public string RoomName { get; }
    public string HostNickname { get; }
    public uint ParticipantCount { get; }
    public uint Capacity { get; }
    public RoomLifecycleMessage Lifecycle { get; }
}
```

```csharp
public sealed class DemoRoomParticipantSnapshot
{
    public DemoRoomParticipantSnapshot(ulong sessionId, string nickname, bool isHost, bool isReady, uint joinOrdinal)
    {
        SessionId = sessionId;
        Nickname = nickname ?? throw new ArgumentNullException(nameof(nickname));
        IsHost = isHost;
        IsReady = isReady;
        JoinOrdinal = joinOrdinal;
    }

    public ulong SessionId { get; }
    public string Nickname { get; }
    public bool IsHost { get; }
    public bool IsReady { get; }
    public uint JoinOrdinal { get; }
}
```

- [ ] **Step 4: Populate the structured read model without changing protocol behavior**

In `TcpDemoClient`, store arrays for the latest room list and current room participants. On `RoomList`, map each `RoomSummaryMessage` into `DemoRoomSummarySnapshot`. On `RoomSnapshot`, map participants and copy `HostSessionId`, `Capacity`, and `Lifecycle`. On `LobbyEntered`/room clear, clear current-room structured state. Expose cloned/read-only arrays through `DemoClientSnapshot` so UI cannot mutate client state.

Preserve the existing `_roomList = FormatRoomList(...)` and `_roomParticipants = FormatRoomParticipants(...)` assignments.

- [ ] **Step 5: Re-run DemoFlow**

Run:

```powershell
dotnet run --project Tests/LockstepArena.DemoFlow.Tests/LockstepArena.DemoFlow.Tests.csproj -c Release
```

Expected: all DemoFlow tests pass, including the new structured read-model test.

- [ ] **Step 6: Commit**

```powershell
git add .gitignore Packages/com.locksteparena.client-demo/Runtime Tests/LockstepArena.DemoFlow.Tests
git commit -m "feat: expose structured lobby read model"
```

---

### Task 2: Add owned DemoHost launch and LAN address utilities

**Files:**
- Create: `Assets/LockstepArenaDemo/Runtime/Shell/ShellConnectionStage.cs`
- Create: `Assets/LockstepArenaDemo/Runtime/Shell/LanServerPathResolver.cs`
- Create: `Assets/LockstepArenaDemo/Runtime/Shell/LanServerLauncher.cs`
- Create: `Assets/LockstepArenaDemo/Runtime/Shell/LanAddressUtility.cs`
- Modify/Test: `Assets/LockstepArenaDemo/Tests/Editor/UnityDemoPresentationTests.cs`

**Interfaces:**
- `LanServerPathResolver.Resolve()` -> absolute path to DemoHost executable for Editor or built Player.
- `LanServerLauncher.StartOwned(string executablePath, int controlPort, int battlePort)` -> starts exactly one owned server process.
- `LanServerLauncher.WaitUntilReadyAsync(string address, int port, TimeSpan timeout)` -> completes only when CONTROL is connectable.
- `LanServerLauncher.StopOwned()` -> kills only the stored owned process instance, never processes discovered by name.
- `LanAddressUtility.SelectPreferredPrivateIpv4(IEnumerable<IPAddress>)` -> preferred RFC1918 IPv4 or `null`.

- [ ] **Step 1: Add failing EditMode tests for path/address/stage contracts**

Tests must assert:

```csharp
Assert.That(LanAddressUtility.SelectPreferredPrivateIpv4(new[]
{
    IPAddress.Parse("127.0.0.1"),
    IPAddress.Parse("169.254.1.4"),
    IPAddress.Parse("192.168.1.23"),
}), Is.EqualTo("192.168.1.23"));

Assert.That(Enum.IsDefined(typeof(ShellConnectionStage), "StartingServer"), Is.True);
Assert.That(Enum.IsDefined(typeof(ShellConnectionStage), "CreatingRoom"), Is.True);
Assert.That(Enum.IsDefined(typeof(ShellConnectionStage), "Failed"), Is.True);
```

Also assert that built-player path resolution ends with:

```text
Server\LockstepArena.Server.DemoHost.exe
```

Run the focused Unity EditMode suite and verify FAIL because the shell utility types do not exist.

- [ ] **Step 2: Implement the shell stage enum**

Use exactly these states:

```csharp
public enum ShellConnectionStage
{
    Idle = 0,
    StartingServer = 1,
    WaitingForServer = 2,
    Connecting = 3,
    EnteringSession = 4,
    CreatingRoom = 5,
    Ready = 6,
    Failed = 7,
}
```

- [ ] **Step 3: Implement path resolution**

Editor path:

```text
<repo>\Server\LockstepArena.Server.DemoHost\bin\Release\net8.0\LockstepArena.Server.DemoHost.exe
```

Built Player path:

```text
<player-dir>\Server\LockstepArena.Server.DemoHost.exe
```

Use `Application.dataPath` and `Directory.GetParent(...)`; do not hard-code `E:\...` into runtime code.

- [ ] **Step 4: Implement owned-process launch semantics**

Start info must be equivalent to:

```csharp
var info = new ProcessStartInfo
{
    FileName = executablePath,
    Arguments = $"--bind 0.0.0.0 --control-port {controlPort} --battle-port {battlePort}",
    WorkingDirectory = Path.GetDirectoryName(executablePath)!,
    UseShellExecute = false,
    CreateNoWindow = true,
};
```

Store only the returned `Process` instance. `StopOwned()` checks that stored instance, calls `Kill(entireProcessTree: true)` only when still running, disposes it, then clears the field. Never call `Process.GetProcessesByName`.

`WaitUntilReadyAsync` retries a short `TcpClient.ConnectAsync` to `127.0.0.1:46000` until success or a five-second timeout; each failed attempt is disposed before retrying. It must not block the Unity main thread with `Thread.Sleep`.

- [ ] **Step 5: Implement private IPv4 selection**

Prefer in order: `192.168/16`, then `10/8`, then `172.16/12`. Ignore loopback, link-local `169.254/16`, IPv6, and `0.0.0.0`. Runtime discovery may enumerate active non-loopback interfaces, then feed candidates into the pure selector.

- [ ] **Step 6: Re-run focused Unity EditMode tests**

Expected: the new shell utility tests pass and all existing presentation tests remain green.

- [ ] **Step 7: Commit**

```powershell
git add Assets/LockstepArenaDemo/Runtime/Shell Assets/LockstepArenaDemo/Tests/Editor/UnityDemoPresentationTests.cs
git commit -m "feat: add owned LAN host launcher"
```

---

### Task 3: Refactor the persistent controller into a player-facing game-shell API

**Files:**
- Modify: `Assets/LockstepArenaDemo/Runtime/LockstepArenaDemoController.cs`
- Modify: `Assets/LockstepArenaDemo/Runtime/LockstepArena.Demo.asmdef`
- Modify/Test: `Assets/LockstepArenaDemo/Tests/Editor/UnityDemoPresentationTests.cs`

**Interfaces:**
- `public DemoClientSnapshot? ClientSnapshot { get; }`
- `public ShellConnectionStage ShellStage { get; }`
- `public string UserFacingError { get; }`
- `public string HostedLanAddress { get; }`
- `public bool OwnsLocalServer { get; }`
- `public Task BeginHostLanAsync(string nickname, string roomName)`
- `public void BeginJoinLan(string nickname, string serverAddress)`
- `public void RefreshRooms()`
- `public void CreateRoom(string roomName)`
- `public void JoinRoom(ulong roomId)`
- `public void LeaveRoom()`
- `public void SetReady(bool value)`
- `public void StartBattle()`
- `public void DisconnectToMainMenu()`

- [ ] **Step 1: Write failing reflection/API tests**

Assert that the public properties/methods listed above exist. Add a behavioral EditMode test for `DisconnectToMainMenu()` using a controller with no client: it must leave `ShellStage == Idle`, clear the error, and not throw.

Run focused EditMode tests; expected FAIL.

- [ ] **Step 2: Add UGUI/TextMeshPro assembly references**

Extend `LockstepArena.Demo.asmdef` references with the package assembly names used by Unity 6 for UGUI and TextMeshPro. Compile immediately; do not proceed with a guessed reference name if Unity reports an unresolved assembly. The intended dependencies are the installed `com.unity.ugui` package and TextMeshPro assembly from that package.

- [ ] **Step 3: Keep `Update()` as the pump owner and move normal UI actions to public commands**

Do not move network pumping into the Canvas. `LockstepArenaDemoController.Update()` continues to:

```text
SynchronizeScene -> PumpOnce -> SynchronizeScene -> BattlePresenter
```

Only the UI invocation surface changes.

`ClientSnapshot` returns `_client?.Snapshot`.

`RefreshRooms/CreateRoom/JoinRoom/LeaveRoom/SetReady/StartBattle` validate `_client` and call the existing `TcpDemoClient` commands without changing network semantics. `CreateRoom` uses capacity `2` for v3-A.

- [ ] **Step 4: Implement host automation as a small state machine**

`BeginHostLanAsync(nickname, roomName)` must:

```text
validate nickname/room name
ShellStage = StartingServer
StartOwned(...)
ShellStage = WaitingForServer
await WaitUntilReadyAsync("127.0.0.1", 46000, 5s)
HostedLanAddress = LanAddressUtility current private IPv4 or "Unavailable"
create TcpDemoClient for 127.0.0.1
BeginConnect()
remember pending nickname + pending room name
ShellStage = Connecting
```

During subsequent `Update()` pumps:

```text
AwaitingSessionEntry + pending nickname -> EnterSession once -> EnteringSession
Lobby + pending host room name -> CreateRoom(roomName, 2) once -> CreatingRoom
Room -> clear pending host fields -> Ready
```

Guest `BeginJoinLan` creates the same client against the validated numeric IPv4, automatically enters the supplied nickname once connection reaches `AwaitingSessionEntry`, but does not auto-create a room.

- [ ] **Step 5: Implement recoverable failure handling**

Any shell connection/startup exception before entering battle must set:

```csharp
ShellStage = ShellConnectionStage.Failed;
UserFacingError = HumanizeShellError(exception);
```

Map common cases to human text: invalid IPv4, server executable missing, ports unavailable/server failed to start, connection refused/timeout, nickname rejection. Keep the raw exception only for F1/Unity log; normal UI receives the friendly message.

On host startup failure, dispose the partial client and stop only the owned server process.

- [ ] **Step 6: Restrict `OnGUI()` to F1 diagnostics only**

Delete the player-facing `DrawLobby`, labels, text fields and normal action buttons from IMGUI. Keep F1 toggle and diagnostics text. In BattleScene, leave the existing v1 battle HUD temporarily available only if removing it would make v3-A battle unusable; v3-B owns the polished battle HUD replacement.

- [ ] **Step 7: Implement safe disconnect-to-menu ownership cleanup**

`DisconnectToMainMenu()` is allowed from pre-battle menu/lobby/room states. Dispose the client, clear pending shell flow state, clear room presentation state, and call `_lanServerLauncher.StopOwned()` only if this instance owns the server. Manually started servers are untouched.

Also dispose/stop an owned server from `OnDestroy()` only for the surviving singleton instance.

- [ ] **Step 8: Run tests**

Run focused Unity EditMode and DemoFlow. Expected: all pass; no existing client/server lifecycle tests change behavior.

- [ ] **Step 9: Commit**

```powershell
git add Assets/LockstepArenaDemo/Runtime/LockstepArenaDemoController.cs Assets/LockstepArenaDemo/Runtime/LockstepArena.Demo.asmdef Assets/LockstepArenaDemo/Tests/Editor/UnityDemoPresentationTests.cs
git commit -m "refactor: expose v3 game shell controller"
```

---

### Task 4: Import only the selected v3-A art and define the shell theme

**Files:**
- Create: `Assets/Art/ART_SOURCES.md`
- Create: selected content under `Assets/Art/UI/SciFi/`
- Create: selected content under `Assets/Art/Environment/SciFiArena/`
- Create: selected content under `Assets/Art/Props/` only when actually used

**Interfaces:**
- Produces the sprite/model/material assets consumed by `GameShellCanvas.prefab` and LobbyScene background dressing.

- [ ] **Step 1: Inventory the local staging packs without importing them**

Run from repository root:

```powershell
Get-ChildItem .\LockstepArena_ArtSource\04_UI -Recurse -File | Select-Object FullName,Length
Get-ChildItem .\LockstepArena_ArtSource\01_Environment -Recurse -File | Select-Object FullName,Length
Get-ChildItem .\LockstepArena_ArtSource\02_Props -Recurse -File | Select-Object FullName,Length
```

Confirm `git status --short` still does not list the staging folder.

- [ ] **Step 2: Select a deliberately small UI subset**

From `04_UI`, import no more than 20 PNG assets total for v3-A, covering exactly these roles:

```text
1 scalable dark panel/frame style
1 primary cyan button style with available normal/hover/pressed variants
1 secondary/dark button style with available normal/hover/pressed variants
1 input-field/background style
1 selection/highlight style for room cards
small icons only for back, refresh, copy, ready/check, warning if present
```

If the pack uses one sprite atlas rather than separate PNG state files, import that atlas instead of duplicating state images.

- [ ] **Step 3: Select background dressing only**

From `01_Environment` and `02_Props`, import at most 8 model assets total. They are decorative menu/lobby background objects only in v3-A. Do not import character rigs or animation packs here.

- [ ] **Step 4: Record exact provenance**

`Assets/Art/ART_SOURCES.md` must list for every imported source pack:

```text
pack name
source URL
license (CC0 when applicable)
original local source subfolder
exact imported asset relative paths
```

Do not rely on memory; copy license/source text from the downloaded pack when present.

- [ ] **Step 5: Let Unity import and verify no magenta/missing materials**

Open the project in Unity `6000.3.10f1`, allow only selected assets to import, inspect them, and fix import/material settings only inside `Assets/Art/...`. Do not modify the protected render-pipeline/user files.

- [ ] **Step 6: Commit**

```powershell
git add Assets/Art
git commit -m "art: add selected sci-fi shell assets"
```

---

### Task 5: Build the polished UGUI Main Menu, LAN screens, Lobby and Room presentation

**Files:**
- Create: `Assets/LockstepArenaDemo/Runtime/UI/GameShellUiController.cs`
- Create: `Assets/LockstepArenaDemo/Runtime/UI/RoomListItemView.cs`
- Create: `Assets/LockstepArenaDemo/Runtime/UI/PlayerCardView.cs`
- Create: `Assets/LockstepArenaDemo/Prefabs/UI/GameShellCanvas.prefab`
- Create: `Assets/LockstepArenaDemo/Prefabs/UI/RoomListItem.prefab`
- Create: `Assets/LockstepArenaDemo/Prefabs/UI/PlayerCard.prefab`
- Modify: `Assets/LockstepArenaDemo/Scenes/LobbyScene.unity`
- Modify/Test: `Assets/LockstepArenaDemo/Tests/Editor/UnityDemoSceneTests.cs`

**Interfaces:**
- `GameShellUiController` reads only `LockstepArenaDemoController.ClientSnapshot`, `ShellStage`, `UserFacingError`, `HostedLanAddress`, and invokes its public commands.
- UI never calls `TcpDemoClient` directly.

- [ ] **Step 1: Add failing scene/prefab tests**

Open `LobbyScene` in the EditMode test and assert it contains:

```text
one LockstepArenaDemoController
one Canvas named GameShellCanvas
one EventSystem using the Input System UI module
one GameShellUiController
```

Load `GameShellCanvas.prefab` and assert it contains named roots:

```text
MainMenuScreen
ModeSelectScreen
CreateLanScreen
JoinLanScreen
ConnectingOverlay
LobbyScreen
RoomScreen
ErrorOverlay
SettingsOverlay
```

Expected: FAIL before prefab/scene creation.

- [ ] **Step 2: Build the prefab in Unity, not by hand-editing YAML**

Use the Unity Editor / available Unity tooling to construct a 1920x1080 reference Canvas with `CanvasScaler` set to `Scale With Screen Size` and reference resolution `1920x1080`. Use proper anchors so 16:9 and common wider layouts remain usable.

Use TextMeshPro for all player-facing text.

- [ ] **Step 3: Implement screen composition from the approved spec**

Main Menu:

```text
LOCKSTEP ARENA
FRAME BATTLE
[开始游戏]
[设置]
[退出]
```

Mode Select:

```text
[创建局域网游戏]  large card
[加入局域网游戏]  large card
[返回]
```

Create LAN:

```text
nickname TMP_InputField
room name TMP_InputField
[创建游戏]
[返回]
```

Join LAN:

```text
nickname TMP_InputField
host IPv4 TMP_InputField
[加入游戏]
[返回]
```

Connecting overlay displays the current `ShellConnectionStage` as human-readable progress. Error overlay displays `UserFacingError` with `重试` and `返回`.

Settings overlay contains only a real fullscreen toggle and `返回`; do not invent audio/video systems not present in v3-A.

- [ ] **Step 4: Implement Lobby as two-column room browser**

Left column uses pooled/instantiated `RoomListItemView` entries driven by `ClientSnapshot.Rooms`. Each entry shows room name, `participant/capacity`, and lifecycle label/color.

Right column shows selected room owner/count/status and enables `加入房间` only when lifecycle/capacity allow it.

Bottom actions:

```text
[刷新]
[+ 创建房间]
[返回主菜单]
```

`+ 创建房间` opens a small UGUI modal with room-name field and fixed capacity 2, then calls `CreateRoom(roomName)`.

- [ ] **Step 5: Implement Room as a VS presentation**

Use two `PlayerCardView` instances around a centered `VS` label. Each card shows nickname and Ready/Waiting state. Host badge is visible on the host card.

Top area:

```text
[离开房间]       room name       invite address + [复制地址] when locally hosted
```

Middle/bottom:

```text
BEST OF THREE
[准备] / [取消准备]
```

Host start state:

```text
not full or anyone not ready -> disabled primary area text "等待玩家准备..."
full and all ready -> enabled [开始比赛]
non-host -> no start button
```

Compute host/full/ready purely from structured `DemoClientSnapshot`; do not parse the legacy diagnostic strings.

- [ ] **Step 6: Wire button listeners only to `LockstepArenaDemoController` public commands**

No UI component owns sockets, DemoHost processes, battle runtime, or scene transitions.

- [ ] **Step 7: Keep decorative background isolated from gameplay truth**

Use selected environment/prop assets behind the Canvas or in a background camera composition. They must not contain colliders/scripts that affect deterministic gameplay. Lobby decoration may animate visually, but has no network/gameplay state.

- [ ] **Step 8: Run EditMode scene tests and visually inspect Game view**

Verify at 1920x1080 and one wider aspect such as 2560x1440 that no primary control overlaps or leaves the safe screen area.

- [ ] **Step 9: Commit**

```powershell
git add Assets/LockstepArenaDemo/Runtime/UI Assets/LockstepArenaDemo/Prefabs Assets/LockstepArenaDemo/Scenes/LobbyScene.unity Assets/LockstepArenaDemo/Tests/Editor/UnityDemoSceneTests.cs
git commit -m "feat: add polished multiplayer game shell"
```

---

### Task 6: Package the independent DemoHost beside the Windows Player

**Files:**
- Create: `Assets/LockstepArenaDemo/Editor/LockstepArena.Demo.Editor.asmdef`
- Create: `Assets/LockstepArenaDemo/Editor/V3AWindowsBuild.cs`
- Test/verify: existing Unity EditMode suite + physical build output

**Interfaces:**
- Unity menu command: `Lockstep Arena/Build v3-A Windows Player`.
- Output root: `.artifacts/V3APlayer/`.
- Player: `.artifacts/V3APlayer/LockstepArena.exe`.
- Bundled host: `.artifacts/V3APlayer/Server/LockstepArena.Server.DemoHost.exe`.

- [ ] **Step 1: Implement one-click build orchestration**

The editor command must:

1. locate repository root from `Application.dataPath`;
2. clear/recreate only `.artifacts/V3APlayer` and `.artifacts/V3AServerPublish` using normal filesystem APIs, never Git cleanup;
3. run:

```powershell
dotnet publish Server/LockstepArena.Server.DemoHost/LockstepArena.Server.DemoHost.csproj -c Release -r win-x64 --self-contained true -o .artifacts/V3AServerPublish
```

4. fail the Unity build with the captured process exit code/stdout/stderr if publish fails;
5. build `LobbyScene` and `BattleScene` to `.artifacts/V3APlayer/LockstepArena.exe`;
6. copy all publish output into `.artifacts/V3APlayer/Server/`.

Do not commit `.artifacts`.

- [ ] **Step 2: Verify launcher path agreement**

After build, assert manually or in an editor test that `LanServerPathResolver.Resolve()` for built layout corresponds to:

```text
<player-dir>\Server\LockstepArena.Server.DemoHost.exe
```

- [ ] **Step 3: Build and smoke-launch the packaged host**

Run the one-click build, then from `.artifacts/V3APlayer` launch the game, choose Create LAN Game, and verify it starts the bundled server without a separate PowerShell server command.

- [ ] **Step 4: Commit editor build tooling**

```powershell
git add Assets/LockstepArenaDemo/Editor
git commit -m "build: package DemoHost with v3 player"
```

---

### Task 7: v3-A end-to-end acceptance and focused regression

**Files:**
- Modify tests only if acceptance exposes a real defect.
- Do not add evidence folders or progress markdown.

**Interfaces:**
- Final v3-A flow must reach the existing BattleScene without altering accepted battle contracts.

- [ ] **Step 1: Run focused .NET regression**

```powershell
dotnet run --project Tests/LockstepArena.DemoFlow.Tests/LockstepArena.DemoFlow.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj -c Release
dotnet run --project Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj -c Release
```

Expected: all pass.

- [ ] **Step 2: Run the full Unity EditMode suite**

Use Unity `6000.3.10f1` in batch mode and write test results only under ignored `.artifacts`. Expected: `failed=0`.

- [ ] **Step 3: Run the one-click v3-A Windows build**

Expected:

```text
Player build succeeds
self-contained DemoHost publish succeeds
Server/LockstepArena.Server.DemoHost.exe exists next to Player
```

- [ ] **Step 4: Manual local two-client shell acceptance**

On one computer:

```text
Client A -> Main Menu -> Start -> Create LAN Game
DemoHost starts automatically
A auto-connects/enters session/creates room
Client B -> Main Menu -> Start -> Join LAN Game -> 127.0.0.1
B enters Lobby -> selects A room -> Join
Room shows two player cards
A/B Ready states update visually
Host sees "等待玩家准备..." until both Ready
Host Start becomes active when valid
Start -> existing BattleScene -> existing countdown/gameplay begins
```

Also verify `复制地址`, Leave Room, Refresh, Back to Main Menu, invalid-IP error, and missing-server executable error are human-readable/recoverable.

- [ ] **Step 5: Ownership safety acceptance**

Run a DemoHost manually, then run a client that joins it as a guest. Exit/return to menu from the client. Verify the manually started DemoHost remains running.

Then create a LAN game from a host client and exit explicitly from the shell. Verify only that client-owned DemoHost process stops.

- [ ] **Step 6: Repository cleanliness**

Run:

```powershell
git diff --check
git status --short
```

Expected: no build output, source-art staging files, Library, Temp, Logs, bin/obj or `.artifacts` are staged/tracked. Preserve the two user-owned files unchanged and uncommitted.

- [ ] **Step 7: Push and STOP**

Push `v3-presentation` by normal fast-forward only. Do not begin v3-B.

Final handoff format:

```text
Presentation Step: v3-A
Branch: v3-presentation
Spec commit: 16becb1ed107afb37240e4ce219a3df4741557d0
Plan commit: <plan commit>
Final HEAD:
Remote HEAD:

Goal achieved:
Selected art actually imported:
Game-shell pages completed:
Automatic LAN host result:
Local host+guest acceptance:
Focused .NET regression:
Unity EditMode:
Windows Player + bundled DemoHost build:
Repository cleanliness:
Protected files status:
Known limitations intentionally deferred to v3-B:
Review requests:

STOP — v3-B has not started.
```

## Plan Self-Review

- Spec coverage: all approved v3-A sections are mapped to Tasks 1-7: UGUI/TMP, normal UI removal from IMGUI, Create/Join LAN, owned DemoHost lifecycle, friendly errors, structured Lobby, VS Room, invite IP/copy, selected art only, Windows packaging and acceptance.
- Scope: no Battle HUD redesign, character rigging, combat animation, VFX, audio, interpolation, LAN discovery, account/chat/matchmaking or core lockstep refactor is included.
- Type consistency: UI consumes `DemoClientSnapshot` structured room data and controller public commands; no UI component accesses `TcpDemoClient` directly.
- Artifact safety: local `LockstepArena_ArtSource` is ignored before any asset inspection/import.
- Completion rule: internal task commits are allowed, but there is only one user-facing v3-A implementation handoff; do not stop between tasks unless an architectural blocker would require violating the approved spec.
