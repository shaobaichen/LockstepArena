# v3-B Battle Presentation

## Goal

Turn the existing deterministic 1v1 battle into a small, readable, portfolio-ready low-poly sci-fi duel while leaving simulation, networking, authority, prediction, rollback, replay, and settlement behavior unchanged.

## Boundary

`BattleState` and `DemoClientSnapshot` are the only gameplay facts consumed by presentation. Player models, animation, particles, audio, camera, HUD, and view transforms never feed back into simulation. No presentation object owns a gameplay collider, rigidbody, damage rule, projectile motion, or phase transition.

The v3-B runtime stays intentionally small:

- `BattlePresentationReadModel` converts deterministic battle state into canonical slot colors, HP, score, timer, and one mutually-exclusive announcement.
- `BattlePresentationTracker` observes projectile IDs and HP deltas to produce presentation-only transient cues once, including across repeated rollback observations.
- `BattlePresenter` renders arena, robots, weapon views, deterministic projectiles, animation, VFX, audio, camera, and lighting.
- `BattleHudView` renders the formal UGUI/TMP battle HUD. The existing F1 diagnostics remain a separate technical overlay.
- `BattlePresentationCatalog` is the single Resources asset that references the selected models, materials, textures, sounds, and HUD prefab.

## Canonical presentation

- PlayerSlot 0 is always cyan/blue and PlayerSlot 1 is always red on every client.
- Position and facing come directly from `PlayerState`; facing uses deterministic `Aim`.
- Moving is inferred from deterministic position change and selects Idle or Run. A newly observed projectile owned by a slot selects the short Shoot clip.
- Zero HP selects a simple persistent shutdown pose. The player remains visible until the simulation resets the round.
- Projectile transforms are copied from `ProjectileState`; the TrailRenderer is visual only.
- Arena blocker visuals are built exactly from `ArenaConfig` rectangles. Solid-looking decoration stays on or outside the gameplay boundary.

## Transient feedback

A bounded identity cache records observed projectile IDs and HP-damage identities. New projectile IDs cause one muzzle flash and shoot sound. Removed IDs cause one small environment impact unless the same observation also contains player damage, in which case a player impact is used. HP decreases cause one hit flash/hit sound. Re-observing the same deterministic tick/state after rollback does not replay those cues.

The tracker never changes deterministic state and does not reconstruct gameplay collision.

## HUD and phase mapping

The formal HUD shows P1/P2 HP, two round-win marks, current round, and `mm:ss` from deterministic ticks. One central announcement maps the existing phase to countdown 3/2/1, FIGHT, ROUND WIN, DRAW, or final MATCH VICTORY. Final matches do not show a redundant round-win announcement. Settlement uses the existing Return To Lobby lifecycle.

## Camera and lighting

The fixed perspective camera frames the complete 10 x 6 arena from approximately `(0, 9.2, -8.2)`, rotation `(50, 0, 0)`, FOV 44. Lighting is a restrained cool directional/key setup plus emissive materials; there is no gameplay raycast, follow camera, Cinemachine, or post-processing framework.

## Asset selection and licensing

Only selected files are copied from `E:\unityproject\LockstepArena_ArtSource\V3B`; complete source packs remain external.

- Player: Styloo `FBXrobot/robot.fbx` plus its color/emission/normal/metallic textures. The official asset page identifies the package as CC0 1.0.
- Weapon: Quaternius `FBX/LongPistol_small.fbx`, CC0 1.0 per the local `License.docx`.
- Animation: embedded Styloo clips `Armature|iddle`, `Armature|walking`, and `Armature|attackminiguns`; death uses the allowed shutdown fallback. The separate universal animation library is not imported.
- Arena: reuse the already-attributed Quaternius models under `Assets/Art/Environment/SciFiArena` and `Assets/Art/Props`.
- UI: reuse the already-attributed Kenney sci-fi UI sprites under `Assets/Art/UI/SciFi`.
- VFX: selected Kenney particle textures only, CC0 1.0.
- Audio: selected Kenney sci-fi, impact, UI, and ambient OGG files only, CC0 1.0.

## Verification contract

Automated coverage proves canonical color mapping, HUD values, mutually-exclusive announcements, round reset data, deterministic projectile view mapping, transient dedupe, catalog/prefab wiring, lack of gameplay physics on views, and mouse-aim projection. Existing DemoFlow, Client Prediction, Live Prediction, Unity EditMode, and Windows build regressions remain green. Final acceptance additionally exercises two real clients through LAN, lobby, room, BO3, victory, and return-to-lobby with F1 both hidden and visible.

## Explicit exclusions

No new game mode, rule, weapon mechanic, gameplay event stream, interpolation, camera framework, IK, root motion authority, physics authority, Addressables, pooling framework, DI, EventBus, FMOD/Wwise, VFX Graph, extra map, replay UI, rematch, statistics, settings overhaul, account system, database, chat, matchmaking, or network protocol.

