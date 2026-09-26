# v3-B Battle Presentation

## Goal

Turn the existing deterministic 1v1 battle into a small, readable, portfolio-ready low-poly sci-fi duel while leaving simulation, networking, authority, prediction, rollback, replay, and settlement behavior unchanged.

## Boundary

`BattleState` and `DemoClientSnapshot` are the only gameplay facts consumed by presentation. Player models, animation, particles, audio, camera, HUD, and view transforms never feed back into simulation. No presentation object owns a gameplay collider, rigidbody, damage rule, projectile motion, or phase transition.

The v3-B runtime stays intentionally small:

- `BattlePresentationReadModel` converts deterministic battle state into canonical slot colors, HP, score, timer, and one mutually-exclusive announcement.
- `BattlePresentationTracker` observes projectile IDs and HP deltas to produce presentation-only transient cues once, including across repeated rollback observations.
- `BattleScenePresentation` binds the authored arena, camera, and audio sources. `BattlePresenter` renders robots, weapon views, deterministic projectiles, animation, VFX, and audio without rebuilding the authored environment.
- `BattleHudView` renders the formal UGUI/TMP battle HUD. The existing F1 diagnostics remain a separate technical overlay.
- `BattlePresentationCatalog` is the single Resources asset that references the selected models, materials, textures, sounds, and HUD prefab.

## Canonical presentation

- PlayerSlot 0 is always cyan/blue and PlayerSlot 1 is always red on every client.
- Position comes from `PlayerState`. Remote facing uses deterministic `Aim`; local facing can use the current locally sampled canonical aim for immediate visual response, without mutating simulation state.
- Moving is inferred from deterministic position change and selects Idle or Run. A newly observed projectile owned by a slot selects the short Shoot clip.
- Zero HP selects a simple persistent shutdown pose. The player remains visible until the simulation resets the round.
- Projectile transforms are copied from `ProjectileState`; the TrailRenderer is visual only.
- Arena art is authored in `BattleScene.unity`. The user aligned the visual blockers against the unchanged `ArenaConfig` rectangles; Unity scene colliders do not become simulation authority. There is no automatic mesh-to-collision baking or validation in v3-B.

## Transient feedback

A bounded identity cache records observed projectile IDs and HP-damage identities. New projectile IDs cause one muzzle flash and shoot sound. BattleState does not expose a removed projectile's hit target or removal reason, so removed IDs receive only neutral/environment feedback at their last observed position. HP decreases independently cause one player hit flash/hit sound; they never classify unrelated projectile removals. Re-observing the same deterministic tick/state after rollback does not replay those cues.

The tracker never changes deterministic state and does not reconstruct gameplay collision.

### Independent review correction (2026-09-27)

Removed the persistent controls/help row from the formal Battle HUD; F1 diagnostics remain separate and unchanged. Added real-simulation mixed hit/expiry feedback and HUD absence regressions, both verified RED before the fix and GREEN afterward. Fresh complete Unity EditMode: 58/58 passed (00:49 local); DemoFlow: 59/59, Client Prediction: 37/37, Live Prediction: 51/51. Fresh Windows package build succeeded at 00:50 local. No simulation, collision, prediction, rollback, replay, or v3-C changes.

## HUD and phase mapping

The formal HUD shows P1/P2 HP, two round-win marks, current round, and `mm:ss` from deterministic ticks. One central announcement maps the existing phase to countdown 3/2/1, FIGHT, ROUND WIN, DRAW, or final MATCH VICTORY. Final matches do not show a redundant round-win announcement. Settlement uses the existing Return To Lobby lifecycle.

## Camera and lighting

The fixed perspective camera frames the complete 10 x 6 arena from approximately `(0, 9.2, -8.2)`, rotation `(50, 0, 0)`, FOV 44. Lighting is a restrained cool directional/key setup plus emissive materials; there is no gameplay raycast, follow camera, Cinemachine, or post-processing framework.

## Asset selection and licensing

Only selected files are copied from `E:\unityproject\LockstepArena_ArtSource\V3B`; complete source packs remain external.

- Player: Styloo `FBXrobot/robot.fbx` plus its color/emission/normal/metallic textures. The official asset page identifies the package as CC0 1.0.
- Weapon: Quaternius `FBX/LongPistol_small.fbx`, CC0 1.0 per the local `License.docx`.
- Animation: embedded Styloo clips `Armature|iddle`, `Armature|walking`, and `Armature|attackminiguns`; death uses the allowed shutdown fallback. The separate universal animation library is not imported.
- Arena: reuse the already-attributed Quaternius models, plus the ten selected Modular SciFi MegaKit Standard FBX files and five FBX-referenced textures used by the user-authored arena. See `Assets/Art/ART_SOURCES.md` for the exact whitelist and included CC0 license.
- UI: reuse the already-attributed Kenney sci-fi UI sprites under `Assets/Art/UI/SciFi`.
- VFX: selected Kenney particle textures only, CC0 1.0.
- Audio: selected Kenney sci-fi, impact, UI, and ambient OGG files only, CC0 1.0.

## Verification contract

Automated coverage proves canonical color mapping, HUD values, mutually-exclusive announcements, round reset data, deterministic projectile view mapping, transient dedupe, catalog/prefab wiring, lack of gameplay physics on views, and mouse-aim projection. Existing DemoFlow, Client Prediction, Live Prediction, Unity EditMode, and Windows build regressions remain green. Final acceptance additionally exercises two real clients through LAN, lobby, room, BO3, victory, and return-to-lobby with F1 both hidden and visible.

## Explicit exclusions

No new game mode, rule, weapon mechanic, gameplay event stream, interpolation, camera framework, IK, root motion authority, physics authority, Addressables, pooling framework, DI, EventBus, FMOD/Wwise, VFX Graph, extra map, replay UI, rematch, statistics, settings overhaul, account system, database, chat, matchmaking, or network protocol.

## Final correction and acceptance record (2026-09-27)

- Persistent controller/presenter initialization now retries scene binding after an early scene-load failure. Configuration is committed only once the authored camera/audio/arena references are complete. The regression test was observed RED (`GameplayCamera` null) by the user before the fix; the user subsequently confirmed mouse-facing and attack direction work.
- `StylooRobotView.prefab` exposes Robot Model Anchor, Robot Scale, Weapon Anchor, Visual Muzzle, and Team Ground Ring. The builder preserves existing authored anchors. Animation restores only the imported model root TRS, not these editing anchors. Body and weapon geometry are checked across Idle/Run/Shoot using baked skin geometry rather than inflated renderer bounds.
- HP bars now shrink with canonical HP; settlement input has an EventSystem and Return To Lobby uses the existing control lifecycle. The user confirmed both behaviors in real Windows Players.
- Fresh Release regressions: DemoFlow 59/59, Client Prediction 37/37, Live Prediction 51/51. Commands: `dotnet run --project Tests/LockstepArena.DemoFlow.Tests/LockstepArena.DemoFlow.Tests.csproj -c Release --no-restore`, corresponding Client.Prediction and LivePrediction project commands.
- Latest full Unity EditMode suite: **56/56 passed**, run manually by the user in the open Unity editor. This count is user-reported; a fresh exported XML is not attached. Earlier XML files under `.artifacts/v3b` are historical, not proof of the latest 56-test run.
- Manual acceptance: user confirmed normal play, HP bars, return to lobby, restored robot visibility/scale, mouse-facing/attack direction, and completion of visual collision alignment. This is user-operated evidence, not an automated screenshot/video certification of every visual/audio cue.
- Death uses a shutdown/collapse view fallback, not a retargeted death clip. Collision authoring/export and random map selection were discussed but **not implemented**; deterministic geometry and all frozen core sources remain unchanged.
- Latest Windows build succeeded on 2026-09-27 at 00:03 (local time), after the final 23:59 map save. The editor log confirms `Windows package completed`; Player DLL/EXE and packaged DemoHost exist. Build binaries/logs, unused source-pack candidates, legacy scratch scene changes, generated render-pipeline/project settings, and the protected Mobile_RPAsset/ShaderGraphSettings files are excluded from this correction commit. Final Git identifiers are supplied in the push handoff.
- No final screenshot is added: the local `Docs/Evidence/V3B/v3b-battle-preview.png` predates these corrections and is intentionally not submitted as final evidence.
