# Gate 14 Minimal TCP Lobby-to-Battle Demo Implementation Plan

> Execute only after this Planning commit receives independent FINAL PASS. Follow each task in order with RED -> verify RED -> minimal implementation -> GREEN -> focused audit -> normal commit. If a frozen contract proves contradictory, stop for review instead of changing the architecture.

**Goal:** close Lockstep Arena v1 with one minimal loopback TCP flow from nickname and rooms through the frozen Gate 13 predicted battle stack to verified settlement and return.

**Frozen base:** `57243ae82ff87a5f51dc86d34342d697963b34dc`

**Branch/worktree:** `codex/gate14-v1-demo-closure` / `.worktrees/gate14-v1-demo-closure`

**Authoritative specification:** `Docs/Architecture/GATE14_MINIMAL_TCP_LOBBY_TO_BATTLE_DEMO.md`

## Global rules

- Gate 14 is the last v1 Gate; do not create Gate 14.5 or Gate 15.
- Use separate CONTROL and BATTLE IPv4 loopback TCP connections. CONTROL owns nickname/session/room/ready/start/bootstrap/status/settlement/return; BATTLE owns a 16-byte one-time ticket followed by the unchanged Gate 13 protocol.
- Compose exactly one frozen Gate 13 `TcpSharedBattleSession` per battle and one frozen `PredictedTcpClientBattleRuntime` per participating client. Never duplicate authority, Simulation, prediction, rollback/replay, framing, or broadcast logic.
- New networking uses readiness checks plus bounded synchronous work. No Task, async, Thread, Timer, coroutine networking loop, background worker, DI, EventBus, router, middleware, or generic network/session framework.
- Preserve the ordinary checkout and its two user files: `Assets/Settings/Mobile_RPAsset.asset` and `ProjectSettings/ShaderGraphSettings.asset`.
- Expected Golden values live only in consumer tests; `Gate14DemoGoldenVector` returns actual results.
- Never silently update any frozen digest. A mismatch is a STOP boundary.

## Implementation-start checks

Run in the Gate 14 worktree before Task 1:

```powershell
$base = '57243ae82ff87a5f51dc86d34342d697963b34dc'
if ((git branch --show-current) -ne 'codex/gate14-v1-demo-closure') { throw 'Wrong Gate 14 branch.' }
if ((git rev-parse HEAD^) -ne $base) {
    # This condition applies after the Planning commit: HEAD must be the independently approved Planning HEAD and its parent must be the frozen base.
    throw 'Gate 14 implementation must start at the approved Planning commit directly above the frozen base.'
}
git merge-base --is-ancestor $base HEAD
if ($LASTEXITCODE -ne 0) { throw 'Frozen Gate 13 base is not an ancestor.' }
if ((git status --porcelain).Length -ne 0) { throw 'Gate 14 worktree must be clean.' }
$ordinary = git -C 'E:\unityproject\LockstepArena' status --short
$expected = @(' M Assets/Settings/Mobile_RPAsset.asset',' M ProjectSettings/ShaderGraphSettings.asset')
if (Compare-Object $ordinary $expected) { throw 'Ordinary checkout changed.' }
```

At implementation authorization, replace the direct-parent assertion only if an approved Planning amendment exists; never reset to the frozen base.

## Exact implementation file map

### Modified existing files

```text
.gitignore
Packages/com.locksteparena.protocol/Schema/lockstep_arena_protocol.proto
Packages/com.locksteparena.protocol/Runtime/Generated/LockstepArenaProtocol.g.cs
Packages/com.locksteparena.protocol/Runtime/ProtocolMapper.cs
Packages/packages-lock.json
Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj
Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj
README.md
Docs/Architecture/GATE14_MINIMAL_TCP_LOBBY_TO_BATTLE_DEMO.md   # evidence only in final task
```

`Packages/manifest.json` is never modified.

### Git-renamed physical sources and new package metadata

```text
Client/LockstepArena.Client.LiveTcp/TcpClientBattlePump.cs
  -> Packages/com.locksteparena.client-live-tcp/Runtime/TcpClientBattlePump.cs
Client/LockstepArena.Client.LiveTcp/PredictedTcpClientBattleRuntime.cs
  -> Packages/com.locksteparena.client-live-tcp/Runtime/PredictedTcpClientBattleRuntime.cs
Client/LockstepArena.Client.LiveTcp/LockstepArena.Client.LiveTcp.csproj
  -> Packages/com.locksteparena.client-live-tcp/Runtime/LockstepArena.Client.LiveTcp.csproj
Packages/com.locksteparena.client-live-tcp/package.json
Packages/com.locksteparena.client-live-tcp/Runtime/Directory.Build.props
Packages/com.locksteparena.client-live-tcp/Runtime/LockstepArena.Client.LiveTcp.asmdef
```

### New Client Demo package

```text
Packages/com.locksteparena.client-demo/package.json
Packages/com.locksteparena.client-demo/Runtime/Directory.Build.props
Packages/com.locksteparena.client-demo/Runtime/DemoClientOptions.cs
Packages/com.locksteparena.client-demo/Runtime/DemoClientPhase.cs
Packages/com.locksteparena.client-demo/Runtime/DemoClientSnapshot.cs
Packages/com.locksteparena.client-demo/Runtime/DemoClientPumpResult.cs
Packages/com.locksteparena.client-demo/Runtime/TcpDemoClient.cs
Packages/com.locksteparena.client-demo/Runtime/ClientControlProtocol.cs
Packages/com.locksteparena.client-demo/Runtime/LockstepArena.Client.Demo.asmdef
Packages/com.locksteparena.client-demo/Runtime/LockstepArena.Client.Demo.csproj
```

### New Server application

```text
Server/LockstepArena.Server.DemoHost/LockstepArena.Server.DemoHost.csproj
Server/LockstepArena.Server.DemoHost/Program.cs
Server/LockstepArena.Server.DemoHost/DemoServerOptions.cs
Server/LockstepArena.Server.DemoHost/DemoServerPumpResult.cs
Server/LockstepArena.Server.DemoHost/TcpDemoServer.cs
Server/LockstepArena.Server.DemoHost/DemoSession.cs
Server/LockstepArena.Server.DemoHost/DemoRoom.cs
Server/LockstepArena.Server.DemoHost/BattlePreparation.cs
Server/LockstepArena.Server.DemoHost/ServerControlProtocol.cs
```

### New Gate 14 tests, Unity demo, and closure docs

```text
Tests/LockstepArena.DemoFlow.Tests/LockstepArena.DemoFlow.Tests.csproj
Tests/LockstepArena.DemoFlow.Tests/Program.cs
Tests/LockstepArena.DemoFlow.Tests/TestAssert.cs
Tests/LockstepArena.DemoFlow.Tests/ControlProtocolTests.cs
Tests/LockstepArena.DemoFlow.Tests/SessionRoomTests.cs
Tests/LockstepArena.DemoFlow.Tests/BattlePreparationTests.cs
Tests/LockstepArena.DemoFlow.Tests/SettlementLifecycleTests.cs
Tests/LockstepArena.DemoFlow.Tests/Gate14DemoGoldenVector.cs
Tests/LockstepArena.DemoFlow.Tests/Gate14DemoGoldenTests.cs
Assets/LockstepArenaDemo/Runtime/LockstepArena.Demo.asmdef
Assets/LockstepArenaDemo/Runtime/LockstepArenaDemoController.cs
Assets/LockstepArenaDemo/Scenes/LockstepArenaDemo.unity
Assets/LockstepArenaDemo/Tests/Editor/LockstepArena.Demo.Editor.Tests.asmdef
Assets/LockstepArenaDemo/Tests/Editor/UnityDemoSceneTests.cs
Assets/LockstepArenaDemo/Tests/Editor/UnityDemoAssemblyTests.cs
Assets/LockstepArenaDemo/Tests/Editor/UnityDemoPresentationTests.cs
Docs/Architecture/LOCKSTEP_ARENA_V1_ARCHITECTURE.md
```

Unity `.meta` files generated for these new authored assets/packages are tracked as required; no unrelated serialization diff is retained.

## Exact project contracts

`LockstepArena.Server.DemoHost.csproj` is an SDK executable targeting net8.0/C#12, nullable enabled, implicit usings disabled, warnings as errors, `BuildInParallel=false`; direct ProjectReferences are exactly Server LiveTcp, Protocol, StreamFraming, and Simulation.

`LockstepArena.Client.LiveTcp.csproj` and `LockstepArena.Client.Demo.csproj` target netstandard2.1/C#9, nullable enabled, implicit usings disabled, warnings as errors, `BuildInParallel=false`, and route artifacts outside their packages. LiveTcp references ClientPrediction, Protocol, Simulation, StreamFraming; Client Demo references Client LiveTcp, Protocol, Simulation, StreamFraming. Both preserve Google.Protobuf `3.36.0` where required. Each asmdef compiles the same Runtime source, uses matching assembly references, `autoReferenced=false`, `allowUnsafeCode=false`, `noEngineReferences=true`, and the approved Google.Protobuf precompiled reference.

`LockstepArena.DemoFlow.Tests.csproj` is a dependency-free net8.0/C#12 executable with nullable enabled, implicit usings disabled, warnings as errors, `BuildInParallel=false`; its direct ProjectReferences are exactly DemoHost, Client Demo, Protocol, and Simulation. No NUnit/xUnit/MSTest or prior test-helper source is referenced.

The only `.gitignore` additions are:

```gitignore
!Packages/com.locksteparena.client-demo/Runtime/LockstepArena.Client.Demo.csproj
!Server/LockstepArena.Server.DemoHost/LockstepArena.Server.DemoHost.csproj
!Tests/LockstepArena.DemoFlow.Tests/LockstepArena.DemoFlow.Tests.csproj
```

## Exact frozen API/transaction reminders

- `DemoServerOptions` constructor parameters, in order, are `controlPort`, `battlePort`, `maxSessions`, `maxRooms`, `maxRoomCapacity`, `PlayerState[] spawnStatesInSlotOrder`, `inputDelayTicks`, `maxFutureTickOffset`, `authoritativeHistoryCapacity`, `battleDurationTicks`, `maxControlPayloadLength`, `maxPendingControlBytesPerSession`, `controlReceiveBufferLength`, `controlReceiveOffset`, `controlReceiveReadCapacity`, `maxControlMessagesPerPump`, `maxControlSendBytesPerPump`, `maxBattlePayloadLength`, `battleReceiveBufferLength`, `battleReceiveOffset`, and `battleReceiveReadCapacity`. Matching read-only properties plus `SpawnStateCount` and `GetSpawnState(PlayerSlot)` are public. It requires history `>=1`, `MaxSessions>=1`, `MaxRooms>=1`, `MaxRoomCapacity>=2`, `MaxRoomCapacity<=MaxSessions`, defensive spawn count `>=MaxRoomCapacity`, in-arena spawns, valid independent control/battle capacities, and independent InputDelay/FutureWindow.
- `TcpDemoServer` exposes `ControlPort`, `BattlePort`, `SessionCount`, `RoomCount`, and bounded `PumpOnce()` returning the six-count `DemoServerPumpResult`.
- `DemoClientOptions` constructor parameters, in order, are `controlPort`, `maxControlPayloadLength`, `maxPendingControlBytes`, `controlReceiveBufferLength`, `controlReceiveOffset`, `controlReceiveReadCapacity`, `maxControlMessagesPerPump`, `maxControlSendBytesPerPump`, `maxPredictionTicks`, `maxAuthoritativeFramesPerUpdate`, `maxPendingAuthoritativeFrames`, `maxReplayFrames`, `maxBattlePayloadLength`, `battleReceiveBufferLength`, `battleReceiveOffset`, and `battleReceiveReadCapacity`, with exactly matching read-only properties. ControlPort is nonzero; BattlePort comes only from validated bootstrap.
- `TcpDemoClient` exposes `Phase`, immutable `Snapshot`, command methods, and one bounded `PumpOnce(LocalInputSample?)`. Leave/Return only queue; phase changes only on `LobbyEntered`. CommandRejected does not change phase.
- ID counters begin at 1, exhaust through zero sentinel without wrap/reuse, and mutate only on successful candidate commit. PlayerId equals SessionId; PlayerSlot follows stable join order.
- Host Start is candidate-before-commit and freezes exactly one roster/state, one BattleId, one 16-byte random ticket per participant, all events, and capacity reservations before changing the room.
- Attachment reads exactly the remaining ticket bytes, never over-reads, writes `0x01`, and transfers ownership only after acceptance completes.
- Final Tick uses widened `S+D`; clients stop new input after `FinalInputTick` but drain authority. Settlement waits for authority equality and validates complete state/digest; mismatch/impossible ordering is fail-stop.

The additive client oneof is exactly `enter_session=1`, `request_room_list=2`, `create_room=3`, `join_room=4`, `leave_room=5`, `set_ready=6`, `start_battle=7`, `return_to_lobby=8`, `exit_session=9`. The server oneof is exactly `session_entered=1`, `room_list=2`, `room_snapshot=3`, `battle_preparing=4`, `battle_started=5`, `battle_status=6`, `battle_settlement=7`, `command_rejected=8`, `lobby_entered=9`. Existing battle DTO fields remain unchanged. Supporting room/bootstrap/settlement/rejection messages and field numbers are copied exactly from the Architecture; no request ID, opcode, or router is added.

## Frozen Gate 14 registry (`RESULT 48/48 passed`)

1. `ControlCommandOneOfRoundTripsEveryApprovedVariant`
2. `ControlEventOneOfRoundTripsEveryApprovedVariant`
3. `ExistingBattleMessagesRemainWireCompatible`
4. `BattleBootstrapRoundTripPreservesCanonicalState`
5. `BattleBootstrapRejectsMissingRosterOrState`
6. `BattleBootstrapRejectsDuplicateMissingUnknownOrNoncontiguousSlot`
7. `BattleBootstrapRejectsOutOfRangePositionOrAim`
8. `WireIdentifierCapacityAndPortNarrowingRejectBeforeMutation`
9. `NicknameValidationUsesTrimControlUtf8AndOrdinalRules`
10. `RoomNameValidationUsesTrimControlAndUtf8Rules`
11. `SessionIdsStartAtOneIncreaseAndAreNeverReused`
12. `SessionIdExhaustionRejectsWithoutWrapOrMutation`
13. `DuplicateActiveNicknameRejectsWithoutAllocatingSession`
14. `CreateRoomAutoJoinsHostAtJoinOrdinalZero`
15. `CreateRoomCapacityUsesConfiguredSpawnBoundNotOneVsOne`
16. `RoomIdExhaustionRejectsWithoutRoomMutation`
17. `RoomListUsesStableCreationOrderAndCompleteSummaries`
18. `JoinAppendsStableJoinOrderAndBroadcastsSnapshot`
19. `JoinRejectsMissingClosedFullOrAlreadyJoinedRoom`
20. `ReadyAndUnreadyBroadcastAtomicRoomSnapshots`
21. `OpenNonHostLeaveRemovesOnlyThatParticipant`
22. `OpenHostLossClosesRoomAndReturnsSurvivorsToLobby`
23. `StartRejectsNonHostWithoutMutation`
24. `StartRejectsNonFullRoomWithoutMutation`
25. `StartRejectsAnyUnreadyParticipantWithoutMutation`
26. `StartAtomicallyFreezesRosterBootstrapTicketsAndEvents`
27. `RosterSlotsFollowJoinOrderRatherThanPlayerId`
28. `BattleIdExhaustionRejectsWithoutPreparationMutation`
29. `TicketsAreSixteenByteRandomScopedUniqueAndSingleUse`
30. `BattleAttachmentAccumulatesPartialTicketWithoutOverread`
31. `InvalidExpiredReusedOrWrongScopeTicketClosesOnlyCandidate`
32. `ClientWaitsForAcceptanceAndBattleStartedBeforeBattleInput`
33. `PreparingBattleExplicitLeaveIsRejected`
34. `InBattleExplicitLeaveIsRejected`
35. `PreparingControlLossInvalidatesTicketsAndClosesPreparedSockets`
36. `AllAttachmentsCreateExactlyOneGate13SharedBattleSession`
37. `ServerAndClientPumpsPerformOnlyFrozenBoundedWork`
38. `ClientNeverSubmitsInputPastFinalInputTick`
39. `TickLimitCreatesOneNormalSettlementAtExactFinalTick`
40. `EarlySettlementRemainsPendingUntilFinalAuthority`
41. `MatchingSettlementVerifiesCompleteStateAndDigest`
42. `SettlementMismatchOrImpossibleTickFailsStop`
43. `NormalSettlementDisposesBattleButPreservesControlSession`
44. `BattleFailureAbortsRoomAndNotifiesSurvivingControls`
45. `GoldenCompletesFullNicknameRoomReadyStartBattleFlow`
46. `GoldenBothClientsDirtyRollbackAndConvergeAtStateFour`
47. `GoldenSettlementReturnRemovesRoomAndPreservesSessions`
48. `AlternateControlAndBattleSegmentationProducesSameGoldenResult`

Tests 2/21/22/47 own the frozen LobbyEntered cases. Existing constructor/resource coverage proves history zero/negative rejects; Test 15 proves `MaxRoomCapacity>MaxSessions` rejects, equality succeeds, and spawn coverage, without changing the total.

The exact Unity registry is:

1. `UnityDemoSceneTests.DemoSceneContainsSingleDebugController`
2. `UnityDemoAssemblyTests.UnityLoadsMigratedGate13ClientLiveTcpTypes`
3. `UnityDemoPresentationTests.UnityFormatsFrozenGoldenSettlementAndDiagnostics`

## Frozen Golden vector

Bravo enters first as Session/Player 1; Alpha enters second as Session/Player 2 and creates Room/Battle 1. Roster is Slot0 Player2 Alpha, Slot1 Player1 Bravo. Initial Tick0 states are `(-300,0,1000)` and `(300,0,2000)`, duration 4, InputDelay 2, MaxPrediction 4, Initial digest `D08BA63E403C71AF`.

```text
Tick0: Slot0 (-1,0,10100), Slot1 ( 1, 0,20100)
Tick1: Slot0 ( 0,1,10101), Slot1 ( 0,-1,20101)
Tick2: Slot0 ( 1,0,10102), Slot1 (-1, 0,20102)
Tick3: Slot0 ( 0,-1,10103), Slot1 (0, 1,20103)

State1: (-400,0,10100), (400,0,20100),       F82D3BE4A98024B5
State2: (-400,100,10101), (400,-100,20101), A4DAA5FBE7E940ED
State3: (-300,100,10102), (300,-100,20102), 7A653646579E6AEC
State4: (-300,0,10103), (300,0,20103),       D8E54FF828A4C670
```

Both clients predict all four Ticks before authority, both Dirty sequences are exactly four `true` values, and server/authoritative/predicted/Replay values all converge at State Tick4/digest `D8E54FF828A4C670`.

## Task 1: Add the finite control Protocol and mapping

**Files:** modify the sole `.proto`, sole generated `.g.cs`, and `ProtocolMapper.cs`; create `.gitignore`, DemoFlow csproj/runner/assertions, and `ControlProtocolTests.cs` changes described above.

1. Add Tests 1–8 to the registry and implement consumer expected literals. Add the DemoFlow csproj exception only.
2. **RED:** run `dotnet build Tests/LockstepArena.DemoFlow.Tests/LockstepArena.DemoFlow.Tests.csproj -c Release`; require failure only because approved control DTOs/mapping are absent. If restore is needed, restore the frozen projects and rerun RED.
3. Add the exact finite client/server oneofs, supporting messages/enums, and LobbyEntered field 9 to the single schema. Do not change existing battle fields.
4. Regenerate through pinned Grpc.Tools 2.83.0; do not hand-edit generated code. Add only explicit bootstrap mapping/validation to `ProtocolMapper` and delegate roster/state invariants to existing Domain types.
5. **GREEN:** DemoFlow interim `8/8`; Gate5 `35/35`; Gate6 `24/24`; pinned regeneration followed by schema/generated `git diff --exit-code`.
6. Audit exactly one proto/g.cs, existing wire compatibility, Protocol dependency direction, no router/request ID. `git diff --check`.
7. Commit: `feat: add demo control protocol`.

## Task 2: Migrate Gate 13 Client LiveTcp to one embedded package

**Files:** Git-rename the three existing Client LiveTcp files; add package.json/Directory.Build.props/asmdef; retarget only two existing test csprojs; add only the live-tcp packages-lock entry.

1. Retarget both test ProjectReferences before the destination exists.
2. **RED:** build both LiveTcp and LivePrediction tests; require missing destination project/source only.
3. Use `git mv` for the csproj and both production `.cs`; add the minimal complete package and asmdef. Preserve production source blobs exactly. Do not modify test source.
4. **GREEN:** build package and both tests; execute Gate11 `32/32` and Gate13 `49/49`; run the Unity assembly-load contract once package metadata exists.
5. Audit old paths absent, source count exactly one, `git hash-object` new files equals frozen Gate13 blobs, no DLL/symlink/sync script, lockfile only one exact embedded entry, manifest unchanged.
6. Commit: `build: expose Gate 13 client runtime to Unity`.

## Task 3: Implement session, room, Ready, and atomic Start domain lifecycle

**Files:** create DemoHost csproj, Program, options/result, `DemoSession.cs`, `DemoRoom.cs`, `BattlePreparation.cs`, initial `TcpDemoServer.cs`; create/extend `SessionRoomTests.cs`, `BattlePreparationTests.cs`; add DemoHost csproj ignore exception.

1. Register Tests 9–28. Include corrected Test15 resource boundaries and narrow reflection only for exact ID exhaustion fields.
2. **RED:** build/run interim registry; require missing DemoHost/options/lifecycle types only.
3. Implement immutable options first, then IDs/names, stable lists, room state, candidate-before-commit commands, Host Start, frozen roster/state/tickets/bootstrap. No socket behavior beyond construction ownership yet.
4. **GREEN:** DemoFlow interim `28/28`; Gate3 `38/38`; Gate4 `32/32`; Gate9 `27/27`; Gate10 `27/27`.
5. Audit validation order, history `>=1`, room capacity/session cross-field rule, no 1v1 production constant, no unordered iteration decisions, no generic repository/service/allocator.
6. Commit: `feat: add demo session and room lifecycle`.

## Task 4: Add bounded CONTROL server/client pumps

**Files:** complete `TcpDemoServer.cs`, `ServerControlProtocol.cs`, Program; create the Client Demo package files and metadata; add its csproj ignore exception and exact packages-lock entry; extend `SessionRoomTests.cs` and begin Test37.

1. Add socket-backed coverage for control connect, framing, bounded read/send/message work, LobbyEntered-confirmed transitions, and rejection-without-optimism.
2. **RED:** run the relevant interim DemoFlow registry; require missing bounded pump/client API only.
3. Implement explicit readiness checks, at most one accept per listener, one bounded read/send progress per session, retained offsets, bounded event processing, immutable snapshots, and exact client/server pump order. Keep separate control/battle capacities.
4. **GREEN:** all registered interim tests pass; Client Demo and DemoHost Release builds are 0/0; Gate7 `32/32`; Gate8 `8/8` under watchdog.
5. Audit no blocking Accept/Read without readiness, no Task/Thread/async, no generic endpoint/options/router, no optimistic Leave/Return.
6. Commit: `feat: add bounded demo control pumps`.

## Task 5: Attach battle tickets and compose frozen Gate 13 session

**Files:** complete `BattlePreparation.cs`, `TcpDemoServer.cs`, `TcpDemoClient.cs`; implement Tests 29–37 and 44 in `BattlePreparationTests.cs`/`SettlementLifecycleTests.cs`.

1. Write partial ticket, over-read sentinel, invalid scope/use, acceptance ordering, leave rejection, control-loss abort, exact-one-shared-session, bounded pump, and failure tests.
2. **RED:** require failures only because attachment/composition is absent.
3. Implement one-read/one-write bounded attachment state, exact 16-byte buffer, cryptographic ticket generation, `0x01` acceptance, slot-ordered bindings, exact Gate13 ownership transfer, and abort disposal.
4. **GREEN:** DemoFlow interim through Test37 plus Test44; Gate11 `32/32`; Gate13 `49/49`; no change to Gate13 production blobs.
5. Audit ticket threat wording, no over-read, no reconnect/retry/TLS, one disposal owner, one Gate13 shared session.
6. Commit: `feat: compose ticketed Gate 13 battles`.

## Task 6: Complete predicted client and settlement lifecycle

**Files:** complete Client Demo runtime files and Server completion/status/settlement paths; implement Tests 38–44 in `SettlementLifecycleTests.cs`.

1. Add exact FinalInputTick, early-settlement pending, full-state/digest equality, mismatch/impossible Tick fail-stop, preserved control, and Aborted coverage.
2. **RED:** require only missing completion/verification behavior.
3. Implement widened duration arithmetic, input cutoff with authority draining, status-on-change, exact Tick-limit settlement, pending verification, Gate13 Replay/convergence validation, LobbyEntered-confirmed Return, and deterministic cleanup.
4. **GREEN:** DemoFlow interim `44/44`; Gate12 `36/36`; Gate13 `49/49`.
5. Audit no runtime Snapshot, winner/reward, timeout/neutral input, server prediction, settlement retry, or client speculative authority.
6. Commit: `feat: verify demo battle settlement`.

## Task 7: Add the minimal Unity IMGUI demo

**Files:** create the demo runtime asmdef/controller/scene and Editor asmdef/three tests (plus required `.meta` files).

1. Write all three exact Unity named tests before controller/scene implementation.
2. **RED:** run assembly-filtered `LockstepArena.Demo.Editor.Tests`; require missing demo scene/controller/Unity-visible migrated types only, with fresh XML proving discovery.
3. Add one controller and one scene. Format only immutable Snapshot fields and invoke the explicit pump from `Update`; no Transform gameplay truth, interpolation, UI framework, or networking coroutine.
4. **GREEN:** fresh NUnit XML exactly `total=3 passed=3 failed=0` and all three named tests Passed.
5. Inspect `Assets`/`ProjectSettings` diff; retain only authored demo assets/meta and restore individually confirmed Unity serialization changes. Re-run Gate12 2/2, Gate7 1/1, Gate5 2/2, and Gate3 named Golden.
6. Audit asmdef references, one physical LiveTcp source, no copied DLL.
7. Commit: `feat: add Unity v1 debug demo`.

## Task 8: Prove the full two-client Golden and alternate segmentation

**Files:** complete `Gate14DemoGoldenVector.cs`, `Gate14DemoGoldenTests.cs`, Program registry; only minimal fixes inside already-approved Gate14 files if RED exposes them.

1. Add Tests 45–48 with all expected literals in the test consumer. Vector owns actual flow/results only.
2. **RED:** run under an external process watchdog; require a specific unmet Golden orchestration assertion, not a hang or test-registration error.
3. Implement the smallest finite driver: two real loopback control sockets, two real battle sockets, stopped/finitely driven pacing consistent with the frozen proof, both predicted clients, exact scripted inputs, settlement/return/exit, and a second run changing only valid control/battle segmentation.
4. **GREEN:** external watchdog exits successfully; exact `RESULT 48/48 passed`; server/client/replay Tick4; both Dirty sequences true x4; intermediate/final digests exact; Room1 removed and sessions preserved at Return.
5. Run Gates 8/11/13 under their watchdogs and confirm unchanged counts/Goldens.
6. Audit Golden file contains none of the five expected digest literals or pass/fail assertions; no ticket byte constant; no implementation workaround in frozen Gate13.
7. Commit: `test: prove complete v1 demo flow`.

## Task 9: Close README, architecture, verification, and evidence

**Files:** update `README.md`; add `Docs/Architecture/LOCKSTEP_ARENA_V1_ARCHITECTURE.md`; append evidence only to Gate14 Architecture.

1. **RED documentation audit:** require README headings for prerequisites/run/demo/diagnostics/Gate0–14/limitations/TCP-only/verification and architecture Mermaid; prove missing headings before edits.
2. Add concise commands, exact 20-step manual flow, data-flow Mermaid, ownership boundaries, known exclusions, and no unverified claims.
3. Perform restore-assets preflight. If any existing project lacks restore assets, run `dotnet restore` using its frozen csproj only, make no dependency/version edit, then start the following 24-build matrix from build 1.

### Exact 24 sequential Release builds

```powershell
$projects = @(
'Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj',
'Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj',
'Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj',
'Packages/com.locksteparena.client-prediction/Runtime/LockstepArena.Client.Prediction.csproj',
'Packages/com.locksteparena.client-live-tcp/Runtime/LockstepArena.Client.LiveTcp.csproj',
'Packages/com.locksteparena.client-demo/Runtime/LockstepArena.Client.Demo.csproj',
'Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj',
'Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj',
'Server/LockstepArena.Server.LiveTcp/LockstepArena.Server.LiveTcp.csproj',
'Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj',
'Server/LockstepArena.Server.DemoHost/LockstepArena.Server.DemoHost.csproj',
'Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj',
'Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj',
'Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj',
'Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj',
'Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj',
'Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj',
'Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj',
'Tests/LockstepArena.Server.TickAuthority.Tests/LockstepArena.Server.TickAuthority.Tests.csproj',
'Tests/LockstepArena.Server.TickPacing.Tests/LockstepArena.Server.TickPacing.Tests.csproj',
'Tests/LockstepArena.Client.Prediction.Tests/LockstepArena.Client.Prediction.Tests.csproj',
'Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj',
'Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj',
'Tests/LockstepArena.DemoFlow.Tests/LockstepArena.DemoFlow.Tests.csproj')
foreach ($project in $projects) {
  dotnet build $project -c Release --no-restore
  if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
}
```

Record 0 warnings/0 errors for each.

### Exact fresh .NET execution

Run `dotnet run --project <test-csproj> -c Release --no-build --no-restore` for:

```text
Simulation 38/38
Server.FrameSync 32/32
Server.Protocol 35/35
Server.ProtocolAuthority 24/24
StreamFraming 32/32
TcpEndToEnd 8/8       [external watchdog]
Server.TickAuthority 27/27
Server.TickPacing 27/27
Client.Prediction 36/36
LiveTcp 32/32         [external watchdog where frozen]
LivePrediction 49/49  [external watchdog]
DemoFlow 48/48        [external watchdog]
Server Verification Golden 89A7DD66F8D9E871
```

Also explicitly record Gate labels 3 through 14 matching those frozen totals. Watchdogs bound verification processes only and add no product timeout.

### Pinned Protocol regeneration

Run the existing pinned CodeGen contract, record Grpc.Tools `2.83.0`, resolved bundled protoc path/version, absence of `PROTOBUF_PROTOC`/`Protobuf_ProtocFullPath` overrides, exactly one schema, exactly one tracked `.g.cs`, then require schema/generated `git diff --exit-code`.

### Fresh Unity 6000.3.10f1 XML matrix

Run each independently with `-batchmode -nographics -runTests -testPlatform EditMode`, the frozen `-assemblyNames`/Gate3 filter, fresh result/log paths, no `-quit`, and `Start-Process -Wait`. Parse XML; process exit is not proof:

```text
LockstepArena.Demo.Editor.Tests              total=3 passed=3 failed=0; all three Gate14 names Passed
LockstepArena.Client.Prediction.Editor.Tests total=2 passed=2 failed=0; both Gate12 names Passed
LockstepArena.StreamFraming.Editor.Tests     total=1 passed=1 failed=0; Gate7 name Passed
LockstepArena.Protocol.Editor.Tests          total=2 passed=2 failed=0; both Gate5 names Passed
LockstepArena.Simulation.Editor.Tests        failed=0; UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector Passed
```

After each, inspect exact Assets/ProjectSettings diffs and restore only confirmed Unity-generated changes. Never use broad reset/clean or ordinary checkout.

### Manual demo acceptance

Use control 46000, battle 46001, capacity2, delay2, duration4, scripted input, one built Player and one Editor client. Execute and record exactly:

1. build and start DemoHost;
2. open the dedicated scene;
3. launch one Player plus Editor client;
4. connect Bravo first and enter `Bravo`;
5. connect Alpha second and enter `Alpha`;
6. Alpha creates `Golden Room`, capacity two;
7. Bravo refreshes and joins;
8. both panels show Alpha host and Bravo;
9. Bravo Start visibly rejects `NOT_HOST`;
10. Alpha Ready while Bravo Unready still rejects Start;
11. Bravo becomes Ready;
12. Alpha starts;
13. both clients show Slot0/PlayerId2 Alpha and Slot1/PlayerId1 Bravo;
14. both battle attachments are accepted;
15. both clients enter the Gate13 prediction runtime;
16. observe server/authority/predicted Tick, pending counts, Replay, and Dirty diagnostics;
17. both Dirty sequences are `true,true,true,true`;
18. server and both clients settle at Tick4/digest `D8E54FF828A4C670`;
19. both Return to Lobby and Room1 is absent;
20. both Exit and the server stops.

Evidence includes server lifecycle, both client diagnostics, complete settlement state/digest, return, and exit—not merely process exit.

### Exact audits

Run and record:

```powershell
$base='57243ae82ff87a5f51dc86d34342d697963b34dc'
git diff --check $base..HEAD
git diff --name-status $base..HEAD
git diff --exit-code $base..HEAD -- Packages/com.locksteparena.simulation Packages/com.locksteparena.client-prediction Packages/com.locksteparena.stream-framing Server/LockstepArena.Server.FrameSync Server/LockstepArena.Server.ProtocolAuthority Server/LockstepArena.Server.LiveTcp
git diff --exit-code $base..HEAD -- Packages/manifest.json ProjectSettings
git diff --exit-code $base..HEAD -- Tests -- ':!Tests/LockstepArena.LiveTcp.Tests/LockstepArena.LiveTcp.Tests.csproj' ':!Tests/LockstepArena.LivePrediction.Tests/LockstepArena.LivePrediction.Tests.csproj' ':!Tests/LockstepArena.DemoFlow.Tests/**'
git diff --numstat $base..HEAD -- Packages/com.locksteparena.client-live-tcp/Runtime/TcpClientBattlePump.cs Packages/com.locksteparena.client-live-tcp/Runtime/PredictedTcpClientBattleRuntime.cs
rg -n 'Task|Thread|async|Kcp|UDP|MySql|EventBus|IServiceCollection' Server/LockstepArena.Server.DemoHost Packages/com.locksteparena.client-demo Assets/LockstepArenaDemo
rg --files Packages | rg '(^|/)(bin|obj)/|LockstepArena.*\.dll$'
git ls-files -s | Select-String '^120000 '
git status --short
git -C 'E:\unityproject\LockstepArena' status --short
```

Additionally prove: migrated blobs equal Gate13 originals; old Client LiveTcp paths absent; exactly one physical copy; exactly one proto/generated source; package manifests/lock entries match; `.gitignore` adds exactly three lines; no junction via filesystem attributes; no copy/sync script; Gate14 source contains no excluded features; ordinary checkout has exactly its two protected files.

4. **GREEN documentation audit:** all required README/architecture headings and commands exist; manual evidence is complete.
5. Append fresh Implementation Evidence to the Gate14 Architecture only after every check passes. Do not write “complete” evidence early.
6. Commit documentation/evidence only: `docs: close Lockstep Arena v1 demo`.
7. Push normally to `origin/codex/gate14-v1-demo-closure` (temporary per-command proxy only if direct connectivity fails; never persist config). Verify remote SHA equals local HEAD, worktree clean, exact ordinary checkout status, and submit Final Implementation Handoff.
8. **STOP.** No Gate 15, KCP/UDP, or post-v1 implementation.

## Planning self-review checklist

- Exactly 48 Gate14 .NET names and exactly three Unity names are present.
- Test15 includes both corrected server resource rules; LobbyEntered remains field 9 and is owned by Tests 2/21/22/47.
- All five digests and State Tick4 are exact.
- Exact 24-build and Gate3–14 matrices are explicit.
- Both physical-source/package contracts and both control/battle capacity sets are explicit.
- Every task has RED, minimal implementation, GREEN, audit, and a normal commit.
- No unresolved planning marker, Gate15, or implementation file exists in this Planning commit.
