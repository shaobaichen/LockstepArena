# Gate 7 Offline Length-Prefixed Stream Framing

## 1. Status and Frozen Baseline

This document consolidates the independently approved Gate 7 Direction, Architecture Section 1, Section 1 Amendment, Architecture Section 2, Section 2 Amendment, and Architecture Closure.

Gate 7 is:

```text
Offline Length-Prefixed Stream Framing
```

Frozen implementation base:

```text
d5be3b8c3efe03c6b4a014f2b8b7bf972e5a0af7
```

Gate 7 solves one problem: recover zero, one, or many complete payloads from arbitrarily segmented byte-stream data so Gate 6's complete-payload boundary can later sit above a TCP byte stream. Gate 7 remains fully offline and does not implement TCP.

## 2. Proof Chain and Responsibility Boundary

The approved proof chain is:

```text
PlayerInputSubmission protobuf payload
-> length-prefix framing
-> arbitrary offset/count byte segments
-> incremental decoding
-> complete submission payload
-> Gate 6 ProtocolAuthorityProcessor
-> independent authoritative payloads
-> length-prefix framing
-> different arbitrary offset/count byte segments
-> incremental decoding
-> complete authoritative payloads
-> Client Parse / ProtocolMapper / BattleSimulation
-> identical deterministic State / Digest
```

Framing production is payload- and type-agnostic:

```text
byte[] payload -> byte[] framedBytes
byte[] segment -> byte[][] completedPayloads
```

It does not know about Protobuf, `PlayerInputSubmissionMessage`, `AuthoritativeFrameMessage`, `ProtocolAuthorityProcessor`, Simulation Domain types, message direction, opcode, envelope, routing, or transport lifecycle. Gate 6 composition exists only in Gate 7 .NET tests.

## 3. Package, Assembly, and Physical Source Ownership

The sole production package is:

```text
Packages/com.locksteparena.stream-framing/
```

Package metadata is frozen as:

```text
name         com.locksteparena.stream-framing
version      0.1.0
unity        6000.3
dependencies {}
```

The sole production assembly and namespace are:

```text
LockstepArena.StreamFraming
```

Production layout:

```text
Packages/com.locksteparena.stream-framing/
|- package.json
`- Runtime/
   |- Directory.Build.props
   |- LockstepArena.StreamFraming.asmdef
   |- LockstepArena.StreamFraming.csproj
   |- LengthPrefixedFrameEncoder.cs
   `- LengthPrefixedFrameDecoder.cs
```

Unity-generated `.meta` files for authored package assets are tracked and do not constitute duplicate source.

The Runtime csproj contract is:

```text
TargetFramework       netstandard2.1
LangVersion           9.0
Nullable              enable
ImplicitUsings        disable
TreatWarningsAsErrors true
```

It compiles only Runtime production `.cs` files. It must not compile package metadata, Editor tests, Unity test APIs, or package-external sources.

The Runtime asmdef contract is:

```text
references          []
autoReferenced      false
allowUnsafeCode     false
noEngineReferences  true
```

Production has no dependency on Protocol, Simulation, Server, Google.Protobuf, UnityEngine, or UnityEditor. The package-local `Directory.Build.props` follows the existing Simulation package artifact-routing pattern and sends .NET output to repository `.artifacts/`; the embedded package must contain no `bin`, `obj`, or LockstepArena DLL.

## 4. Wire Format and Allocation Boundary

Each frame is:

```text
4-byte prefix + payload bytes
```

The prefix contract is:

```text
Width      4 bytes
Wire type  uint32
Byte order big-endian
Meaning    payload byte count only; prefix excluded
```

Encoder and Decoder configuration use the same mandatory range:

```text
1 <= MaxPayloadLength <= int.MaxValue - 4
```

There is no default product maximum. Test values are explicit test parameters and are not product conclusions. The upper bound guarantees `4 + payload.Length` cannot overflow a signed `int` in the Encoder.

## 5. Encoder API and Semantics

The sole Encoder API is:

```csharp
public static class LengthPrefixedFrameEncoder
{
    public static byte[] Encode(
        byte[] payload,
        int maxPayloadLength);
}
```

Validation and operation order is:

```text
MaxPayloadLength
-> payload null
-> payload length
-> allocation
-> prefix write
-> payload copy
```

Exceptions are:

- invalid maximum: `ArgumentOutOfRangeException`;
- null payload after a valid maximum: `ArgumentNullException`;
- payload longer than the configured maximum: `ArgumentException`.

Success returns a newly allocated `4 + payload.Length` array. The Encoder writes a uint32 big-endian length and copies the payload. The result does not share backing storage with the caller's payload. The Encoder is stateless and has no interface, registry, factory, or lifecycle.

Zero-length payload is valid and encodes as exactly:

```text
00 00 00 00
```

## 6. Decoder API and Validation Priority

The sole Decoder API is:

```csharp
public sealed class LengthPrefixedFrameDecoder
{
    public LengthPrefixedFrameDecoder(int maxPayloadLength);

    public byte[][] Feed(
        byte[] buffer,
        int offset,
        int count);
}
```

The constructor rejects a maximum outside the approved range with `ArgumentOutOfRangeException`.

`Feed` validation priority is frozen as:

```text
1. sticky fault
2. buffer null
3. offset
4. count
5. count == 0
6. consume segment
```

A faulted Decoder always throws `InvalidOperationException` first, including when the supplied buffer or range is otherwise invalid.

For a healthy Decoder:

- null buffer throws `ArgumentNullException` and preserves partial state;
- `offset < 0` or `offset > buffer.Length` throws `ArgumentOutOfRangeException` and preserves partial state;
- `count < 0` or `count > buffer.Length - offset` throws `ArgumentOutOfRangeException` and preserves partial state;
- zero count returns `Array.Empty<byte[]>()` and preserves partial state.

Range validation compares offset to `buffer.Length`, then count to `buffer.Length - offset`; it does not first compute a potentially overflowing unvalidated `offset + count`.

Only `[offset, offset + count)` is consumed. Bytes outside that segment are ignored.

## 7. Decoder Buffering and Ownership

The Decoder retains state for exactly the current frame:

```text
4-byte prefix buffer
prefix bytes received
current payload buffer
payload bytes received
sticky fault
```

It copies directly from the valid input segment into the current prefix or payload buffer. It does not concatenate the whole prior stream with the new input, allocate an exact-sized incoming chunk, retain the caller's receive buffer, or introduce pooling, Span infrastructure, a ring buffer, or a general buffer abstraction.

After four prefix bytes are present, the Decoder:

```text
reads uint32 declaredLength
-> compares the uint32 value against MaxPayloadLength without narrowing
-> only then converts to int
-> only then allocates the exact payload buffer
```

Valid streams support prefix splits, payload splits, multiple complete frames in one segment, a prior payload tail followed by another complete frame in the same segment, and a single `Feed` returning multiple payloads in stream order.

Ownership is frozen as follows:

- no caller buffer reference is retained;
- the caller may overwrite or reuse the entire receive buffer immediately after `Feed` returns;
- each non-empty completed payload is an independently allocated array;
- the returned outer array is an independent container;
- the Decoder does not retain completed payload arrays after returning them;
- zero-length payload uses `Array.Empty<byte>()`.

## 8. Zero-Length, Partial Input, and Sticky Fault

Zero-length payload is valid. It is emitted immediately without a non-empty allocation, after which parsing continues at the next prefix.

An incomplete prefix or incomplete payload is not malformed. It remains buffered until a later `Feed`. Gate 7 defines no EOF, `Complete`, `Flush`, connection-close, or truncated-stream API.

An oversize prefix is:

```text
declaredLength > MaxPayloadLength
```

As soon as its fourth byte arrives, the Decoder performs:

```text
no payload allocation
-> set sticky fault
-> throw InvalidDataException
```

The failing `Feed` returns no partial batch, even if an earlier valid frame was completed locally in that same call. Earlier payloads returned by earlier successful calls remain valid. Every later `Feed` throws `InvalidOperationException` before other validation. There is no reset, recovery, retry, resynchronization scan, or fault hierarchy.

A payload whose declared length is legal but whose content is invalid is returned unchanged; content validation belongs to its consumer.

## 9. A/B/C Golden and Shared Test Source

The frozen payloads are:

```text
A = DE AD BE
B = 00 01 02 03 04
C = FF 00 7F 80 10 20 30 40
```

With test-only `MaxPayloadLength = 64`, their exact 28-byte stream is:

```text
00 00 00 03  DE AD BE
00 00 00 05  00 01 02 03 04
00 00 00 08  FF 00 7F 80 10 20 30 40
```

The primary segmentation is:

```text
1, 2, 2, 13, 4, 6
```

Every segment is placed in a reusable receive buffer at offset 2. The buffer is overwritten after each `Feed`. The fourth Feed contains A's payload tail, the complete B frame, and the first two bytes of C's prefix, and returns `[A, B]`. The last Feed returns C. Final recovery is byte-for-byte `[A, B, C]`.

A second segmentation, including one full-stream Feed, must recover the same sequence. A separate mixed stream proves:

```text
frame(A) + frame(empty) + frame(B) -> [A, empty, B]
```

There is one physical pure-C# actual vector:

```text
Packages/com.locksteparena.stream-framing/Tests/Editor/Gate7FramingGoldenVector.cs
```

It owns actual inputs, stream construction, segmentation, execution, and actual results only. It contains no NUnit, Unity, Protocol, Simulation, Server, expected recovery array, expected test count, or expected Digest. Unity compiles it directly; the .NET test csproj compiles the same file through one explicit external `Compile Include` plus `Link`. No copy or synchronization mechanism is allowed. Unity and .NET consumers independently own their expected A/B/C literals.

## 10. Unity Editor Test Contract

The Editor test assembly is:

```text
LockstepArena.StreamFraming.Editor.Tests
```

Its exact asmdef contract is:

```json
{
  "name": "LockstepArena.StreamFraming.Editor.Tests",
  "references": [
    "LockstepArena.StreamFraming"
  ],
  "includePlatforms": [
    "Editor"
  ],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false,
  "optionalUnityReferences": [
    "TestAssemblies"
  ]
}
```

It has no Protocol, Simulation, Server, Google.Protobuf, or precompiled reference. Its sole role is to execute the exact embedded Runtime source under Unity 6000.3.10f1.

The mandatory named test is:

```text
UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden
```

It proves prefix split, payload split, multiple frames in one segment, offset/count segments, caller receive-buffer reuse/mutation, and ordered byte-for-byte A/B/C recovery. Final fresh XML must report exactly one test, one passed, zero failed, and the named test `Passed`.

## 11. Gate 7 .NET Test Project

The project is:

```text
Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj
```

Its build contract is:

```text
OutputType            Exe
TargetFramework       net8.0
LangVersion           12.0
Nullable              enable
ImplicitUsings        disable
TreatWarningsAsErrors true
BuildInParallel       false
```

Its direct ProjectReferences are exactly:

```text
Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj
Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj
Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj
Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj
```

There is no direct FrameSync reference. The project compiles only its local test sources plus the single external `Gate7FramingGoldenVector.cs` with an explicit `Compile Include` and `Link`. It does not compile or copy Gate 6 test helpers, reference a Gate 6 test project, wildcard package Editor tests, compile Unity test source, or add NUnit, xUnit, MSTest, or another test package.

Test layout is:

```text
Tests/LockstepArena.StreamFraming.Tests/
|- LockstepArena.StreamFraming.Tests.csproj
|- Program.cs
|- EncoderContractTests.cs
|- DecoderContractTests.cs
|- StreamSegmentationTests.cs
|- DecoderFaultTests.cs
|- Gate7ProtocolAuthorityFramingGoldenVector.cs
`- Gate7ProtocolAuthorityCompositionTests.cs
```

## 12. Exact 32-Test Matrix

The Gate 7 dependency-free suite must register exactly these tests.

Encoder contracts, 7:

```text
EncoderRejectsMaxBelowMinimumBeforePayloadValidation
EncoderRejectsMaxAboveAllocationBoundary
EncoderRejectsNullPayload
EncoderRejectsPayloadAboveConfiguredMaximum
EncoderWritesFourByteBigEndianLength
EncoderCopiesPayloadIntoIndependentFrame
EncoderAllowsZeroLengthPayload
```

Decoder API and validation, 8:

```text
DecoderRejectsMaxBelowMinimum
DecoderRejectsMaxAboveAllocationBoundary
NullBufferTakesPriorityAndPreservesPartialPrefix
InvalidOffsetPreservesPartialPrefix
InvalidCountPreservesPartialPayload
ZeroCountReturnsEmptyAndPreservesPartialPayload
FeedConsumesOnlyOffsetCountSegment
HealthyEmptySegmentReturnsEmptyBatch
```

Segmentation and ownership, 9:

```text
PrefixCanSplitAcrossFourFeeds
PayloadCanSplitAcrossFeeds
OneSegmentCanContainSeveralCompleteFrames
PayloadTailAndNextCompleteFrameCanShareSegment
ArbitrarySegmentsRecoverApprovedAbcSequence
DifferentSegmentationsRecoverIdenticalPayloadSequence
ZeroLengthFrameBetweenNonEmptyFramesIsRecovered
ReusedReceiveBufferCannotMutatePartialState
ReturnedBatchAndPayloadsAreIndependentlyOwned
```

Oversize and sticky fault, 4:

```text
OversizeIsRejectedAsSoonAsPrefixCompletes
UintMaxLengthIsRejectedBeforeNarrowingOrAllocation
ValidFrameBeforeOversizeInSameFeedReturnsNoPartialBatch
FaultedDecoderRejectsBeforeNullAndRangeValidation
```

Gate 6 composition, 4:

```text
FramedSubmissionsDriveGapFillPublicationOfTicks100Through102
FramedAuthoritativePayloadsMatchApprovedPerTickClientDigests
FramedServerAndClientReachApprovedFinalStateAndDigest
DifferentBidirectionalSegmentationsProduceSameAuthoritativeDomainSequence
```

The exact result is:

```text
RESULT 32/32 passed
```

## 13. Complete Gate 6 Composition Golden

The .NET-only actual vector is:

```text
Tests/LockstepArena.StreamFraming.Tests/Gate7ProtocolAuthorityFramingGoldenVector.cs
```

It recreates the approved Gate 6 scenario without compiling or copying Gate 6 test helpers.

Roster in Slot order:

```text
Slot0 PlayerId = 0x0102030405060708
Slot1 PlayerId = 0x000000000000002A
Slot2 PlayerId = 0xFFEEDDCCBBAA0099
Slot3 PlayerId = 0x00000000000F4243
```

Initial State at Tick 100:

```text
Slot0 = X -300, Z 0,    Aim 1000
Slot1 = X 300,  Z 0,    Aim 2000
Slot2 = X 0,    Z -300, Aim 3000
Slot3 = X 0,    Z 300,  Aim 4000
```

Tick 100 inputs:

```text
Slot0 = MoveX  1, MoveZ  0, Aim 10100
Slot1 = MoveX -1, MoveZ  0, Aim 20100
Slot2 = MoveX  0, MoveZ  1, Aim 30100
Slot3 = MoveX  0, MoveZ -1, Aim 40100
```

Tick 101 inputs:

```text
Slot0 = MoveX  0, MoveZ  1, Aim 10101
Slot1 = MoveX  0, MoveZ -1, Aim 20101
Slot2 = MoveX  1, MoveZ  0, Aim 30101
Slot3 = MoveX -1, MoveZ  0, Aim 40101
```

Tick 102 inputs:

```text
Slot0 = MoveX -1, MoveZ  0, Aim 10102
Slot1 = MoveX  1, MoveZ  0, Aim 20102
Slot2 = MoveX  0, MoveZ -1, Aim 30102
Slot3 = MoveX  0, MoveZ  1, Aim 40102
```

Submission order is exactly:

```text
Tick100: Slot 0,2,1
Tick101: Slot 3,1,0,2
Tick102: Slot 2,0,3,1
Tick100: Slot 3
```

Each submission is mapped and serialized, individually framed, and concatenated into one client-to-server byte stream. Test-only `MaxPayloadLength` is 4096.

Primary client-to-server segmentation cycles through:

```text
1, 2, 7, 3, 11, 5
```

Every segment uses reusable-buffer offset 3 and a final count of `min(patternCount, remainingBytes)`. Recovered submission payloads must match the original payloads byte-for-byte and are submitted to the frozen Gate 6 Processor in recovered order.

The final gap fill returns exactly three independent authoritative payloads for Ticks 100, 101, and 102. Each is separately framed and concatenated. Primary server-to-client segmentation cycles through:

```text
4, 1, 9, 2, 13, 3
```

Every server segment uses reusable-buffer offset 5. The Client parses each recovered authoritative payload, maps it with its own structurally equal roster, and steps its own Simulation.

The alternate run preserves the exact logical payload stream and changes only segmentation:

```text
Client -> Server: 17, 1, 1, 6
Server -> Client: 2, 2, 2, 2, 19
```

Both patterns cycle until their stream ends. They must recover identical submission bytes, authoritative bytes, canonical Domain Frames, per-Tick Digests, and final state.

The actual vector returns original and recovered submissions, pre-gap output counts, original and recovered authoritative payloads, mapped Frames, Client states and Digests, final Server state, final Client state, and `NextPublishTick`. It contains no expected state or Digest.

Independent consumer expectations are:

```text
After authoritative Tick100 / State Tick101: 0xD95809E1EB5CDDAA
After authoritative Tick101 / State Tick102: 0xA96B83267DD72A7D
After authoritative Tick102 / State Tick103: 0x386C4BB11A7EB7E0
```

Final State:

```text
Tick = 103
Slot0 = X -300, Z 100,  Aim 10102
Slot1 = X 300,  Z -100, Aim 20102
Slot2 = X 100,  Z -300, Aim 30102
Slot3 = X -100, Z 300,  Aim 40102
Digest = 0x386C4BB11A7EB7E0
```

Framing bytes never enter `StateDigest`.

## 14. Lockfile, Manifest, and Ignore Contract

`Packages/manifest.json` committed diff remains zero.

Relative to the frozen baseline, the only approved new `Packages/packages-lock.json` entry is exactly equivalent to:

```json
"com.locksteparena.stream-framing": {
  "version": "file:com.locksteparena.stream-framing",
  "depth": 0,
  "source": "embedded",
  "dependencies": {}
}
```

Every existing lockfile entry remains unchanged. Any other lockfile difference must be investigated and rejected or restored.

The repository's `*.csproj` ignore rule requires exactly these two exceptions and no broader rule:

```gitignore
!Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj
!Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj
```

## 15. Protected Paths

Relative to `d5be3b8c3efe03c6b4a014f2b8b7bf972e5a0af7`, committed diff must be zero under:

```text
Packages/com.locksteparena.simulation/
Packages/com.locksteparena.protocol/
Server/LockstepArena.Server.FrameSync/
Server/LockstepArena.Server.ProtocolAuthority/
Assets/
ProjectSettings/
Packages/manifest.json
```

Gate 6 production blobs remain identical to the frozen baseline. The lockfile has only the approved StreamFraming entry. The ordinary checkout retains exactly its two user-owned modifications and they are never restored, staged, cleaned, or committed by Gate 7.

## 16. Final Verification Matrix

Twelve individual Release builds must each report zero warnings and zero errors:

```text
Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj
Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj
Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj
Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj
Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj
Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj
Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj
Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj
Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj
Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj
Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj
Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj
```

Fresh .NET results must be:

```text
Gate 3: RESULT 38/38 passed
Gate 4: RESULT 32/32 passed
Gate 5: RESULT 35/35 passed
Gate 6: RESULT 24/24 passed
Gate 7: RESULT 32/32 passed
Gate 3 Server Golden: Digest=89A7DD66F8D9E871
Gate 7 final Tick: 103
Gate 7 final Digest: 386C4BB11A7EB7E0
```

Unity 6000.3.10f1 must run three independent jobs with fresh NUnit XML:

```text
Gate 7 framing assembly: total=1 passed=1 failed=0; named test Passed
Gate 5 Protocol regression: total=2 passed=2 failed=0; both named tests Passed
Gate 3 Simulation Golden: total=1 passed=1 failed=0; named test Passed
```

Process launch or exit code alone is insufficient. Any Unity license, package-manager, or instance-lock failure stops verification; the ordinary checkout is not a workaround. Every Unity-generated worktree-local Assets or ProjectSettings change is inspected and restored only by exact path. Broad reset and clean are forbidden.

## 17. Final Audits and Explicit Exclusions

Final audits require:

- exactly one Encoder and one Decoder production source;
- exactly one physical A/B/C Golden vector;
- zero production dependencies;
- no package `bin`, `obj`, or LockstepArena DLL;
- no new DLL, copy/sync wrapper, symlink, or junction;
- no caller-buffer retention or whole-stream concatenation;
- payload allocation only after validating uint32 length;
- no pooling, ring buffer, Span framework, or general buffer abstraction;
- no opcode, envelope, packet type, registry, router, or dispatcher;
- no TCP, UDP, KCP, Socket, or NetworkStream;
- no async, Task, thread, lock, connection lifecycle, disconnect, reconnect, heartbeat, retry, or timeout;
- no Login, Room, Session, TickClock, InputDelay, or missing-input replacement;
- no Prediction, Dirty Frame, Snapshot, Rollback, Replay, or State Sync;
- no compression, encryption, TLS, transport framework, middleware, DI, or generic pipeline.

Gate 7 does not change Gate 6 production and does not begin TCP work.

## 18. Planning and Exit Contract

Planning isolation is:

```text
Worktree: .worktrees/gate7-stream-framing
Branch: codex/gate7-stream-framing
Exact Base: d5be3b8c3efe03c6b4a014f2b8b7bf972e5a0af7
```

The Planning commit contains only this architecture document and the approved implementation plan. It contains no package, Runtime source, tests, `.gitignore`, lockfile, manifest, Unity asset, or other implementation change.

After Planning is pushed, Gate 7 stops until independent Planning PASS. After eventual implementation and final evidence, Gate 7 stops again before any TCP work.
