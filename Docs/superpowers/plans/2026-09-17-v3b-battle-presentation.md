# v3-B Battle Presentation Implementation Plan

> Execute on `v3-presentation` from verified baseline `b4890e463f0126b6f5d9d2222e080eca82c7205c`. Preserve the two ordinary-checkout user modifications and all frozen gameplay/network behavior.

## Task 1: Freeze presentation contracts with RED tests

**Files:**
- Modify: `Assets/LockstepArenaDemo/Tests/Editor/UnityDemoPresentationTests.cs`
- Create: `Assets/LockstepArenaDemo/Runtime/Presentation/BattlePresentationReadModel.cs`
- Create: `Assets/LockstepArenaDemo/Runtime/Presentation/BattlePresentationTracker.cs`

Add focused tests for canonical slot colors, HP/round/timer/wins, the single announcement state, reset data, projectile lifecycle observations, rollback-safe transient dedupe, and final-victory precedence. Run the Unity EditMode assembly and confirm RED is limited to missing presentation contracts. Implement the two dependency-free deterministic presentation helpers, then rerun GREEN.

## Task 2: Import the minimal asset whitelist and build the catalog

**Files:**
- Create selected files under `Assets/Art/Characters/StylooRobot`, `Assets/Art/Weapons/SciFiGun`, `Assets/Art/VFX/Kenney`, and `Assets/Art/Audio/Kenney`
- Create: `Assets/LockstepArenaDemo/Runtime/Presentation/BattlePresentationCatalog.cs`
- Create: `Assets/LockstepArenaDemo/Editor/V3BBattlePresentationBuilder.cs`
- Create generated catalog/material/prefab assets under `Assets/LockstepArenaDemo/Resources/BattlePresentation`
- Modify: `Assets/Art/ART_SOURCES.md`

Copy only the selected CC0 files. Configure the robot importer and embedded clips, generate neutral/team materials, create the Resources catalog, and create a formal HUD prefab. Add tests for asset references and public-repository attribution. Commit as `feat: add v3-b battle presentation assets`.

## Task 3: Replace primitive players and build the arena view

**Files:**
- Modify: `Assets/LockstepArenaDemo/Runtime/BattlePresenter.cs`
- Create: `Assets/LockstepArenaDemo/Runtime/Presentation/PlayerPresentation.cs`

Instantiate the robot and view-only gun for both slots, apply canonical team accents, map deterministic position/aim to transforms, select Idle/Run/Shoot, and retain a shutdown body at zero HP. Build floor, reactor/relay covers, spawn markers, exact boundary edges, back/side/front walls, door, and limited out-of-bounds props using the existing arena model set. Remove all generated colliders and rigidbodies. Add the fixed camera and cool lighting. Commit as `feat: present robots and sci-fi arena`.

## Task 4: Add projectile, VFX, audio, and transient dedupe

**Files:**
- Modify: `Assets/LockstepArenaDemo/Runtime/BattlePresenter.cs`
- Create: `Assets/LockstepArenaDemo/Runtime/Presentation/BattleAudioPresentation.cs`

Replace yellow spheres with small emissive deterministic bolts and trails. Trigger short muzzle, player/environment impact, hit flash, shutdown, and restrained audio from tracker observations. Views never simulate movement or collision. Add focused tests for no Rigidbody/Collider authority and repeated-observation dedupe. Commit as `feat: add deterministic combat feedback`.

## Task 5: Replace formal OnGUI HUD and preserve F1 diagnostics

**Files:**
- Create: `Assets/LockstepArenaDemo/Runtime/Presentation/BattleHudView.cs`
- Modify: `Assets/LockstepArenaDemo/Runtime/LockstepArenaDemoController.cs`
- Modify generated HUD prefab/catalog as needed through the builder

Drive the formal UGUI/TMP HUD from `BattlePresentationReadModel`; show canonical P1/P2 bars, score marks, round/timer, one announcement, final victory, and Return To Lobby. Keep F1 diagnostics as a separate hidden-by-default technical panel. Add UI binding tests. Commit as `feat: add v3-b battle hud`.

## Task 6: Focused and frozen regressions

Run fresh:

1. `LockstepArena.Demo.Editor.Tests` Unity EditMode.
2. DemoFlow Release suite.
3. Client Prediction Release suite.
4. Live Prediction Release suite including weak-network rollback/replay.
5. `git diff --check` and protected-boundary audits.

Fix only presentation defects; do not alter frozen rule sources.

## Task 7: Windows build and visual acceptance

Run the one-click Windows v3-A/v3-B player build with Unity 6000.3.10f1. Verify fresh player output and server packaging. Exercise two real Windows clients through Main Menu -> LAN -> Lobby -> Room -> Ready -> Battle -> BO3 -> Match Victory -> Return To Lobby. Capture representative battle screenshots with F1 hidden and visible, and verify mouse aim after camera adjustment, canonical team colors on both clients, death persistence, projectile/trail/VFX/audio, and absence of fake collision.

## Task 8: Final documentation, audit, commit, and push

**Files:**
- Update: `Docs/Architecture/V3B_BATTLE_PRESENTATION.md`
- Update: `Assets/Art/ART_SOURCES.md`

Append exact commands/results, selected asset paths, final camera values, acceptance evidence, and known issues. Confirm build output, external source packs, logs, screenshots outside the approved evidence location, and protected local files are not staged. Commit as `docs: record v3-b battle presentation evidence`, push normally to `origin/v3-presentation`, verify local/remote SHA equality and clean intended scope, then STOP.

