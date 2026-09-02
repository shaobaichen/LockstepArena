# Gate 7 Offline Length-Prefixed Stream Framing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a dependency-free, payload-agnostic length-prefixed framing package that converts complete payloads to framed bytes and recovers 0/1/N complete payloads from arbitrarily segmented byte-stream input, then prove the existing Gate 6 authority composition remains deterministic across the framing boundary.

**Architecture:** `LockstepArena.StreamFraming` is an embedded Unity package and a .NET project compiled from the same two production source files. It owns only a stateless encoder and one incremental decoder. Protocol, authority, Simulation, and Unity-specific composition stay outside production framing code; Gate 6 end-to-end composition exists only in the Gate 7 .NET test executable.

**Tech Stack:** C# 9/netstandard2.1 production, C# 12/net8.0 dependency-free test runner, Unity 6000.3.10f1 EditMode tests, NUnit through Unity TestAssemblies, Git worktrees, MSBuild SDK projects.

---

## 1. Frozen implementation boundary

- Frozen Gate 6 base: `d5be3b8c3efe03c6b4a014f2b8b7bf972e5a0af7`.
- Planning worktree: `.worktrees/gate7-stream-framing`.
- Planning/implementation branch: `codex/gate7-stream-framing`.
- Implementation starts from the independently approved remote Planning HEAD on that branch. Do not reset or check out the worktree back to the frozen base; the base remains the regression and committed-diff comparison point.
- Production package: `Packages/com.locksteparena.stream-framing/`.
- Production assembly: `LockstepArena.StreamFraming`.
- Production contains exactly two public types:
  - `LengthPrefixedFrameEncoder`
  - `LengthPrefixedFrameDecoder`
- Production is byte/payload agnostic and has no Protocol, Simulation, Server, Google.Protobuf, UnityEngine, or UnityEditor dependency.
- Prefix: 4-byte unsigned length, big-endian, payload length only.
- Configuration: `1 <= maxPayloadLength <= int.MaxValue - 4`.
- Encoder API: `public static byte[] Encode(byte[] payload, int maxPayloadLength)`.
- Decoder API: `public byte[][] Feed(byte[] buffer, int offset, int count)`.
- Zero-length payload is legal.
- Decoder validation priority is sticky fault, null buffer, offset, count, zero count, then consumption.
- Decoder consumes only `[offset, offset + count)`, retains no caller buffer reference, and never requires an exact-sized incoming chunk allocation.
- Decoder buffers only the current 4-byte prefix/current payload rather than concatenating the whole stream.
- It parses the wire prefix as `uint`, compares it with the configured maximum before narrowing, then converts to `int` and allocates.
- An oversize prefix causes `InvalidDataException`, sets a private sticky fault, returns no partial batch from that Feed, and makes every later Feed fail first with `InvalidOperationException`.
- There is no reset, recovery, resynchronization, pooling, ring buffer, transport abstraction, opcode, envelope, or router.
- Output arrays and payload arrays are independently owned. Caller mutation cannot change decoder state or later results.
- Gate 7 does not add TCP, UDP, KCP, Socket, NetworkStream, async, Task, threads, connection lifecycle, time policy, missing-input policy, prediction, snapshot, rollback, replay, Room, Session, View, or Combat.

## 2. Required implementation start check

- [ ] In the Gate 7 worktree, verify the branch is the approved planning branch:

```powershell
if ((git branch --show-current) -ne 'codex/gate7-stream-framing') {
    throw 'Wrong Gate 7 branch.'
}
```

- [ ] Fetch and verify local HEAD is the independently approved remote Planning HEAD, and that the frozen Gate 6 base is its ancestor:

```powershell
git fetch origin codex/gate7-stream-framing
$localHead = git rev-parse HEAD
$remoteHead = git rev-parse origin/codex/gate7-stream-framing
if ($localHead -ne $remoteHead) {
    throw 'Gate 7 implementation must start from the approved remote Planning HEAD.'
}

$approvedBase = 'd5be3b8c3efe03c6b4a014f2b8b7bf972e5a0af7'
if ((git merge-base HEAD $approvedBase) -ne $approvedBase) {
    throw 'Frozen Gate 6 base is not an ancestor of Gate 7 Planning HEAD.'
}
```

- [ ] Verify the Gate 7 worktree is clean.
- [ ] From the ordinary checkout, verify the only user-owned changes remain:
  - `Assets/Settings/Mobile_RPAsset.asset`
  - `ProjectSettings/ShaderGraphSettings.asset`
- [ ] Do not clean, restore, stage, modify, or copy either ordinary-checkout file.

## 3. Final authored file map

Create:

- `Packages/com.locksteparena.stream-framing/package.json`
- `Packages/com.locksteparena.stream-framing/Runtime/Directory.Build.props`
- `Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.asmdef`
- `Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj`
- `Packages/com.locksteparena.stream-framing/Runtime/LengthPrefixedFrameEncoder.cs`
- `Packages/com.locksteparena.stream-framing/Runtime/LengthPrefixedFrameDecoder.cs`
- `Packages/com.locksteparena.stream-framing/Tests/Editor/LockstepArena.StreamFraming.Editor.Tests.asmdef`
- `Packages/com.locksteparena.stream-framing/Tests/Editor/Gate7FramingGoldenVector.cs`
- `Packages/com.locksteparena.stream-framing/Tests/Editor/UnityStreamFramingGoldenTests.cs`
- `Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj`
- `Tests/LockstepArena.StreamFraming.Tests/Program.cs`
- `Tests/LockstepArena.StreamFraming.Tests/EncoderContractTests.cs`
- `Tests/LockstepArena.StreamFraming.Tests/DecoderContractTests.cs`
- `Tests/LockstepArena.StreamFraming.Tests/StreamSegmentationTests.cs`
- `Tests/LockstepArena.StreamFraming.Tests/DecoderFaultTests.cs`
- `Tests/LockstepArena.StreamFraming.Tests/Gate7ProtocolAuthorityFramingGoldenVector.cs`
- `Tests/LockstepArena.StreamFraming.Tests/Gate7ProtocolAuthorityCompositionTests.cs`

Modify only when required by the approved contract:

- `.gitignore`: add exactly two negated authored-project exceptions if the current `*.csproj` rule hides them:
  - `!Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj`
  - `!Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj`
- `Packages/packages-lock.json`: add exactly the approved embedded package entry.
- `Docs/Architecture/GATE7_OFFLINE_LENGTH_PREFIXED_STREAM_FRAMING.md`: append final implementation evidence only after all fresh verification passes.

Do not modify `Packages/manifest.json`, any existing Simulation/Protocol/Server production source, existing Unity assets/settings, or Gate 3/4/5/6 test source.

## 4. Exact Gate 7 .NET test inventory

The dependency-free runner must register and execute exactly these 32 tests.

Encoder (7):

1. `EncoderRejectsMaxBelowMinimumBeforePayloadValidation`
2. `EncoderRejectsMaxAboveAllocationBoundary`
3. `EncoderRejectsNullPayload`
4. `EncoderRejectsPayloadAboveConfiguredMaximum`
5. `EncoderWritesFourByteBigEndianLength`
6. `EncoderCopiesPayloadIntoIndependentFrame`
7. `EncoderAllowsZeroLengthPayload`

Decoder API (8):

8. `DecoderRejectsMaxBelowMinimum`
9. `DecoderRejectsMaxAboveAllocationBoundary`
10. `NullBufferTakesPriorityAndPreservesPartialPrefix`
11. `InvalidOffsetPreservesPartialPrefix`
12. `InvalidCountPreservesPartialPayload`
13. `ZeroCountReturnsEmptyAndPreservesPartialPayload`
14. `FeedConsumesOnlyOffsetCountSegment`
15. `HealthyEmptySegmentReturnsEmptyBatch`

Segmentation and ownership (9):

16. `PrefixCanSplitAcrossFourFeeds`
17. `PayloadCanSplitAcrossFeeds`
18. `OneSegmentCanContainSeveralCompleteFrames`
19. `PayloadTailAndNextCompleteFrameCanShareSegment`
20. `ArbitrarySegmentsRecoverApprovedAbcSequence`
21. `DifferentSegmentationsRecoverIdenticalPayloadSequence`
22. `ZeroLengthFrameBetweenNonEmptyFramesIsRecovered`
23. `ReusedReceiveBufferCannotMutatePartialState`
24. `ReturnedBatchAndPayloadsAreIndependentlyOwned`

Oversize and sticky fault (4):

25. `OversizeIsRejectedAsSoonAsPrefixCompletes`
26. `UintMaxLengthIsRejectedBeforeNarrowingOrAllocation`
27. `ValidFrameBeforeOversizeInSameFeedReturnsNoPartialBatch`
28. `FaultedDecoderRejectsBeforeNullAndRangeValidation`

Gate 6 framed composition (4):

29. `FramedSubmissionsDriveGapFillPublicationOfTicks100Through102`
30. `FramedAuthoritativePayloadsMatchApprovedPerTickClientDigests`
31. `FramedServerAndClientReachApprovedFinalStateAndDigest`
32. `DifferentBidirectionalSegmentationsProduceSameAuthoritativeDomainSequence`

Final runner output must be exactly `RESULT 32/32 passed`.

## 5. Gate 6 composition Golden frozen data

Roster in PlayerSlot order:

- Slot 0: PlayerId `0x0102030405060708`
- Slot 1: PlayerId `0x000000000000002A`
- Slot 2: PlayerId `0xFFEEDDCCBBAA0099`
- Slot 3: PlayerId `0x00000000000F4243`

Initial State at Tick 100:

- Slot 0: X=-300, Z=0, Aim=1000
- Slot 1: X=300, Z=0, Aim=2000
- Slot 2: X=0, Z=-300, Aim=3000
- Slot 3: X=0, Z=300, Aim=4000

Logical inputs:

| Tick | Slot | MoveX | MoveZ | Aim |
|---:|---:|---:|---:|---:|
| 100 | 0 | 1 | 0 | 10100 |
| 100 | 1 | -1 | 0 | 20100 |
| 100 | 2 | 0 | 1 | 30100 |
| 100 | 3 | 0 | -1 | 40100 |
| 101 | 0 | 0 | 1 | 10101 |
| 101 | 1 | 0 | -1 | 20101 |
| 101 | 2 | 1 | 0 | 30101 |
| 101 | 3 | -1 | 0 | 40101 |
| 102 | 0 | -1 | 0 | 10102 |
| 102 | 1 | 1 | 0 | 20102 |
| 102 | 2 | 0 | -1 | 30102 |
| 102 | 3 | 0 | 1 | 40102 |

Submission order:

1. Tick 100: Slots `0, 2, 1`
2. Tick 101: Slots `3, 1, 0, 2`
3. Tick 102: Slots `2, 0, 3, 1`
4. Tick 100: Slot `3`

The first eleven submissions publish no authority batch. The final Tick 100/Slot 3 submission closes the gap and publishes Tick 100, 101, 102 in that order.

Gate 7 composition framing parameters:

- `maxPayloadLength = 4096`
- Primary client-to-server segmentation: `1, 2, 7, 3, 11, 5`, cycling until the byte stream is exhausted, copied through a reusable receive buffer with a non-zero offset of 3.
- Primary server-to-client segmentation: `4, 1, 9, 2, 13, 3`, cycling until exhausted, copied through a reusable receive buffer with a non-zero offset of 5.
- Alternate client-to-server segmentation: `17, 1, 1, 6`, cycling until exhausted.
- Alternate server-to-client segmentation: `2, 2, 2, 2, 19`, cycling until exhausted.

Client digest oracle after each authoritative frame:

- after authoritative Tick 100 / State Tick 101: `0xD95809E1EB5CDDAA`
- after authoritative Tick 101 / State Tick 102: `0xA96B83267DD72A7D`
- after authoritative Tick 102 / State Tick 103: `0x386C4BB11A7EB7E0`

Final State at Tick 103:

- Slot 0: X=-300, Z=100, Aim=10102
- Slot 1: X=300, Z=-100, Aim=20102
- Slot 2: X=100, Z=-300, Aim=30102
- Slot 3: X=-100, Z=300, Aim=40102

The actual vector must not contain expected state or digest constants. Each test consumer owns its expected literals.

## 6. A/B/C framing Golden frozen data

- Payload A: `DE AD BE`
- Payload B: `00 01 02 03 04`
- Payload C: `FF 00 7F 80 10 20 30 40`
- `maxPayloadLength = 64`
- Exact framed stream (28 bytes):
  `00 00 00 03 DE AD BE 00 00 00 05 00 01 02 03 04 00 00 00 08 FF 00 7F 80 10 20 30 40`
- Primary segment lengths: `1, 2, 2, 13, 4, 6`.
- Feed each segment from a reusable receive buffer at a non-zero offset, then overwrite/reuse that buffer after Feed returns.
- The fourth Feed completes A and B in the same Feed; the sixth completes C.
- The single physical `Gate7FramingGoldenVector.cs` generates actual payloads, frames, segment feeds, and recovered results only. Expected stream bytes, batch shape, and recovered A/B/C assertions remain in the Unity and .NET consumers.

## Task 1: Encode length-prefixed payload frames

**Commit:** `feat: encode length-prefixed payload frames`

**Files:**

- Create `Packages/com.locksteparena.stream-framing/package.json`
- Create `Packages/com.locksteparena.stream-framing/Runtime/Directory.Build.props`
- Create `Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.asmdef`
- Create `Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj`
- Create `Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj`
- Create `Tests/LockstepArena.StreamFraming.Tests/Program.cs`
- Create `Tests/LockstepArena.StreamFraming.Tests/EncoderContractTests.cs`
- Create `Packages/com.locksteparena.stream-framing/Runtime/LengthPrefixedFrameEncoder.cs` only after the RED run
- Modify `.gitignore` only if both authored csproj files are actually ignored

- [ ] Create the legal embedded package metadata, package-local `.artifacts/` routing, frozen runtime asmdef, frozen production csproj, dependency-free test runner, and seven encoder tests. Do not create the encoder production source yet.
- [ ] Ensure the production csproj uses `netstandard2.1`, C# 9, nullable enabled, implicit usings disabled, and warnings as errors. Ensure the asmdef has no references, `autoReferenced=false`, `allowUnsafeCode=false`, and `noEngineReferences=true`.
- [ ] Ensure the test csproj uses `OutputType=Exe`, `net8.0`, C# 12, nullable enabled, implicit usings disabled, warnings as errors, and `BuildInParallel=false`, with the four exact direct ProjectReferences frozen in Architecture. At this stage the three existing references may be present even though composition tests arrive later.
- [ ] Run the Gate 7 test project and capture the expected RED failure proving the missing `LengthPrefixedFrameEncoder` production API is the cause.
- [ ] Implement the minimal stateless encoder: validate maximum first, then null payload, then length; allocate exactly `4 + payload.Length`; write the uint32 big-endian prefix explicitly; copy payload bytes.
- [ ] Run the Gate 7 suite and require `RESULT 7/7 passed`.
- [ ] Build the StreamFraming production and Gate 7 test projects in Release and require 0 warnings / 0 errors.
- [ ] Audit that production has no dependency and package-local `bin`, `obj`, or LockstepArena DLL artifacts do not exist.
- [ ] If `git check-ignore` proves the two authored csproj files need exceptions, add exactly the two approved negated paths; otherwise do not modify `.gitignore`.
- [ ] Commit only this slice with the exact commit message above.

## Task 2: Decode healthy incremental stream segments

**Commit:** `feat: decode incremental stream segments`

**Files:**

- Create `Tests/LockstepArena.StreamFraming.Tests/DecoderContractTests.cs`
- Create `Tests/LockstepArena.StreamFraming.Tests/StreamSegmentationTests.cs`
- Modify `Tests/LockstepArena.StreamFraming.Tests/Program.cs`
- Create `Packages/com.locksteparena.stream-framing/Runtime/LengthPrefixedFrameDecoder.cs` only after the RED run

- [ ] Add the exact eight Decoder API tests and nine Segmentation/Ownership tests, registering all 24 cumulative tests. Use local test data temporarily; the shared physical A/B/C vector is introduced in Task 5.
- [ ] Run the Gate 7 suite and capture the expected RED failure proving the missing decoder API/behavior.
- [ ] Implement the smallest healthy incremental decoder with fixed 4-byte prefix storage, current-payload storage, counts/offsets, and a local list only for complete payloads produced by the current Feed.
- [ ] Follow Feed validation priority exactly. Use range validation that cannot overflow. Copy consumed bytes into decoder-owned prefix/payload storage and never retain caller buffers.
- [ ] Parse big-endian prefix as uint, compare to maximum before narrowing, support zero-length frames, and allow one Feed to emit N independently owned payload arrays.
- [ ] At this checkpoint, oversize input may throw `InvalidDataException` without sticky behavior; Task 3 must replace that temporary fault behavior in the final implementation.
- [ ] Run the Gate 7 suite and require cumulative `RESULT 24/24 passed`.
- [ ] Build both Gate 7 projects in Release and require 0 warnings / 0 errors.
- [ ] Audit offset/count isolation, no caller-buffer retention, no whole-stream concatenation, and no pooling/ring/transport abstraction.
- [ ] Commit only this slice with the exact commit message above.

## Task 3: Make oversize framing faults sticky and atomic

**Commit:** `feat: reject oversize stream frames`

**Files:**

- Create `Tests/LockstepArena.StreamFraming.Tests/DecoderFaultTests.cs`
- Modify `Tests/LockstepArena.StreamFraming.Tests/Program.cs`
- Modify `Packages/com.locksteparena.stream-framing/Runtime/LengthPrefixedFrameDecoder.cs`

- [ ] Add and register the exact four Oversize/Fault tests. Include a Feed containing a valid frame followed by an oversize prefix to prove the call returns no partial batch.
- [ ] Run the Gate 7 suite and capture the expected RED failure showing the temporary non-sticky decoder does not satisfy the frozen fault contract.
- [ ] Add one private sticky fault boolean. Check it before every other Feed validation; when a complete declared length exceeds the maximum, set it and throw `InvalidDataException` before narrowing or allocating.
- [ ] Keep all payloads completed during a Feed in a local unpublished container until the Feed finishes successfully, so an oversize prefix later in the same Feed prevents any partial return.
- [ ] Do not add reset, recovery, resynchronization, exception hierarchy, or public fault state.
- [ ] Run the Gate 7 suite and require cumulative `RESULT 28/28 passed`.
- [ ] Build both Gate 7 projects in Release and require 0 warnings / 0 errors.
- [ ] Audit that later Feed calls fail first with `InvalidOperationException`, including null/range arguments.
- [ ] Commit only this slice with the exact commit message above.

## Task 4: Prove framed Gate 6 authority composition

**Commit:** `test: prove framed protocol authority composition`

**Files:**

- Create `Tests/LockstepArena.StreamFraming.Tests/Gate7ProtocolAuthorityFramingGoldenVector.cs`
- Create `Tests/LockstepArena.StreamFraming.Tests/Gate7ProtocolAuthorityCompositionTests.cs`
- Modify `Tests/LockstepArena.StreamFraming.Tests/Program.cs`

- [ ] Write and register the four exact Gate 6 Composition tests first, referencing a not-yet-created vector API.
- [ ] Run the Gate 7 test project and capture the expected RED compile failure caused only by the missing vector.
- [ ] Implement the smallest test-only actual vector using the exact roster, Tick 100 initial state, all twelve logical inputs, frozen submission order, `maxPayloadLength=4096`, and both approved bidirectional segmentation patterns from Sections 5 and 6.
- [ ] For each client submission: serialize its complete protobuf payload, encode it as one framing payload, segment through a reusable receive buffer, recover it on the server decoder, and call the existing Gate 6 `ProtocolAuthorityProcessor` once per recovered payload.
- [ ] For each independent authority payload returned by the Processor: frame independently, segment the server-to-client stream, decode complete payloads, parse/map, and step the independent client Simulation in order.
- [ ] Keep expected batch shape, per-Tick digests, final state, final digest, and alternate-order equality literals in the four test consumers—not in the actual vector.
- [ ] Run the Gate 7 suite and require exact `RESULT 32/32 passed`.
- [ ] Require authoritative Tick 100/101/102 order, client digests `D95809E1EB5CDDAA`, `A96B83267DD72A7D`, `386C4BB11A7EB7E0`, and the approved final State Tick 103.
- [ ] Run Gate 3 `38/38`, Gate 4 `32/32`, Gate 5 `35/35`, Gate 6 `24/24`, and Gate 3 Server Golden `89A7DD66F8D9E871`.
- [ ] Build the six existing production/test projects touched by the composition dependency graph plus both Gate 7 projects as appropriate, requiring 0 warnings / 0 errors.
- [ ] Audit that framing production still knows no protobuf, authority, or Simulation type and that no Gate 6 test helper was copied or compiled.
- [ ] Commit only this slice with the exact commit message above.

## Task 5: Prove exact StreamFraming production under Unity

**Commit:** `test: prove Unity stream framing execution`

**Files:**

- Create `Packages/com.locksteparena.stream-framing/Tests/Editor/LockstepArena.StreamFraming.Editor.Tests.asmdef`
- Create `Packages/com.locksteparena.stream-framing/Tests/Editor/UnityStreamFramingGoldenTests.cs`
- Create `Packages/com.locksteparena.stream-framing/Tests/Editor/Gate7FramingGoldenVector.cs` only after the RED runs
- Modify `Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj`
- Modify `Tests/LockstepArena.StreamFraming.Tests/StreamSegmentationTests.cs`
- Modify `Packages/packages-lock.json` only through the verified embedded-package import result

- [ ] Create the exact frozen Editor test asmdef and both Unity/.NET consumers referencing the absent shared `Gate7FramingGoldenVector`. Add one explicit external `Compile Include` + `Link` for that exact physical file to the .NET csproj; do not wildcard Editor tests or compile the NUnit test source.
- [ ] Run the .NET Gate 7 build and Unity Gate 7 assembly-filtered EditMode test, capturing RED failures caused only by the missing shared vector.
- [ ] Implement the single pure-C# actual vector with A=`DE AD BE`, B=`00 01 02 03 04`, C=`FF 00 7F 80 10 20 30 40`, maximum 64, exact 28-byte encoded stream, primary segmentation `1,2,2,13,4,6`, non-zero offset, and reusable-buffer overwrite after each Feed.
- [ ] Keep the vector free of NUnit, UnityEngine, UnityEditor, expected byte arrays, expected batch sizes, and expected result assertions.
- [ ] Run Gate 7 .NET and require `RESULT 32/32 passed`.
- [ ] Run Unity 6000.3.10f1 with assembly filter `LockstepArena.StreamFraming.Editor.Tests`; parse fresh NUnit XML and require total=1, passed=1, failed=0, and `UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden` result `Passed`.
- [ ] Verify the named Unity test covers prefix split, payload split, same-Feed A/B completion, offset/count segments, receive-buffer reuse/mutation, and exact ordered A/B/C recovery.
- [ ] Inspect Unity-generated worktree-local serialization changes. Restore only verified Unity automation changes under Assets/ProjectSettings when necessary; do not use broad reset/clean and do not touch the ordinary checkout.
- [ ] Verify `Packages/packages-lock.json` differs from the frozen base only by:

```json
"com.locksteparena.stream-framing": {
  "version": "file:com.locksteparena.stream-framing",
  "depth": 0,
  "source": "embedded",
  "dependencies": {}
}
```

- [ ] Verify `Packages/manifest.json` committed diff remains zero and reject/restore any other lockfile change.
- [ ] Build both Gate 7 projects in Release and require 0 warnings / 0 errors.
- [ ] Commit only this slice with the exact commit message above.

## Task 6: Run the full fresh acceptance matrix and record evidence

**Commit:** `docs: record Gate 7 implementation evidence`

**Files:**

- Modify `Docs/Architecture/GATE7_OFFLINE_LENGTH_PREFIXED_STREAM_FRAMING.md`

- [ ] Before verification, run a check that the Architecture document does not yet contain the final `## 19. Implementation Evidence` section. Treat its absence as the expected RED state for this evidence-only task.
- [ ] Verify branch, approved-base ancestry, clean starting state for final verification, implementation commit sequence, and ordinary-checkout preservation.
- [ ] Perform these 12 fresh Release builds independently, requiring 0 warnings / 0 errors for every build:
  1. `Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj`
  2. `Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj`
  3. `Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj`
  4. `Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj`
  5. `Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj`
  6. `Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj`
  7. `Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj`
  8. `Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj`
  9. `Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj`
  10. `Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj`
  11. `Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj`
  12. `Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj`
- [ ] Run the fresh .NET matrix and record exact results:
  - Gate 3 Simulation suite: `RESULT 38/38 passed`
  - Gate 4 FrameSync suite: `RESULT 32/32 passed`
  - Gate 5 Protocol suite: `RESULT 35/35 passed`
  - Gate 6 ProtocolAuthority suite: `RESULT 24/24 passed`
  - Gate 7 StreamFraming suite: `RESULT 32/32 passed`
  - Gate 3 Server Golden: `89A7DD66F8D9E871`
  - Gate 7 framed Gate 6 final Digest: `386C4BB11A7EB7E0`
- [ ] Run three separate Unity 6000.3.10f1 EditMode jobs with fresh result files. Parse each NUnit XML rather than relying on exit code:
  - Gate 7 assembly `LockstepArena.StreamFraming.Editor.Tests`: total=1, passed=1, failed=0, named `UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden` result `Passed`.
  - Gate 5 assembly `LockstepArena.Protocol.Editor.Tests`: total=2, passed=2, failed=0, both named Gate 5 tests `Passed`.
  - Gate 3 named `UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector`: total>=1, failed=0, named result `Passed`.
- [ ] After every Unity run, inspect serialization changes and restore only confirmed worktree-local Unity automation changes. Never use broad reset/clean.
- [ ] Audit frozen-base committed diffs:
  - existing Simulation production = 0
  - existing Protocol production = 0
  - existing Server FrameSync production = 0
  - existing Server ProtocolAuthority production = 0
  - Assets = 0
  - ProjectSettings = 0
  - `Packages/manifest.json` = 0
  - existing `packages-lock.json` entries unchanged
  - new lock entry exactly approved
  - two `.gitignore` additions = exactly the approved csproj exceptions
- [ ] Audit production/runtime source and dependencies:
  - exactly one physical Encoder source and one physical Decoder source
  - Unity and .NET compile those same physical sources
  - no copied/synced/generated framing production source
  - no symlink/junction
  - no cleanup/copy/sync script
  - no package `bin`, `obj`, LockstepArena DLL, or unexpected managed artifact
  - package-local MSBuild outputs route to repository `.artifacts/`
  - runtime asmdef/csproj have no forbidden dependency
  - test csproj direct ProjectReferences are exactly StreamFraming, ProtocolAuthority, Protocol, Simulation; no direct FrameSync reference
  - exactly one external Compile Include for the physical Gate7 framing vector
- [ ] Run source/scope searches proving no Socket, NetworkStream, TCP, UDP, KCP, async/Task/thread, opcode/envelope/router, time policy, prediction/snapshot/rollback/replay, Room/Session/View/Combat, pooling, ring buffer, middleware, or transport framework entered Gate 7 production.
- [ ] Confirm the ordinary checkout still has exactly the two user-owned changes and no Gate 7 files.
- [ ] Append a concise, reproducible `## 19. Implementation Evidence` section containing commits, commands/results, XML named-test evidence, Golden results, audit results, remote/clean handoff checklist, and any environmental facts.
- [ ] Run the evidence check again and require it to pass, then inspect the entire final diff for scope and contradictions.
- [ ] Commit only the Architecture evidence update with the exact commit message above.
- [ ] Push `codex/gate7-stream-framing`.
- [ ] Prove `git ls-remote` SHA equals local HEAD and the Gate 7 worktree is clean.
- [ ] Submit the Gate 7 Final Implementation Handoff and STOP. Do not begin Gate 8, TCP, or any next-Gate planning/implementation.

## 7. Final acceptance invariants

Gate 7 is eligible for Final Handoff only when all of the following are true:

- Exact Gate 7 .NET result is `32/32`.
- Unity executes the exact embedded package production code and the named Gate 7 test is Passed.
- Gate 3/4/5/6 regression matrix is fully green.
- The Gate 6 framed client reaches State Tick 103 and Digest `386C4BB11A7EB7E0`.
- Framing production remains byte-only and dependency-free.
- The decoder's oversize behavior is sticky, allocation-safe, and same-Feed atomic.
- Manifest and protected existing production paths are unchanged from the frozen Gate 6 base.
- Lockfile, `.gitignore`, artifacts, source uniqueness, Unity serialization, and ordinary checkout match the frozen contracts.
- Final remote SHA equals local HEAD and the worktree is clean.
- Work stops before TCP or Gate 8.
