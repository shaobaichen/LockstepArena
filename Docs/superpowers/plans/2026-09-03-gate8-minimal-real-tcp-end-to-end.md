# Gate 8 Minimal Real TCP End-to-End Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove the frozen Gate 5-7 complete-payload, authority, framing, and deterministic Simulation chain over one real synchronous IPv4 loopback TCP connection without introducing production transport code.

**Architecture:** Add one dependency-free .NET integration-test executable only. It builds the frozen 12-submission Gate 6 scenario, sends one continuous framed Client-to-Server byte stream through a real BCL loopback connection, drives the frozen `ProtocolAuthorityProcessor`, returns three independently framed authority payloads over the same connection, and drives an independent Client Simulation to Tick 103 and Digest `386C4BB11A7EB7E0`. The test uses reusable offset/count receive buffers and direct Gate 7 Decoder feeds; TCP remains entirely test-side.

**Tech Stack:** .NET 8 / C# 12 dependency-free executable tests, BCL `TcpListener` / `TcpClient` / `NetworkStream`, Google.Protobuf already frozen by Gate 5, Gate 7 StreamFraming, Gate 6 ProtocolAuthority, Gate 3 Simulation, PowerShell verification, Unity 6000.3.10f1 regressions.

**Spec:** `Docs/Architecture/GATE8_MINIMAL_REAL_TCP_END_TO_END.md`

## 1. Global Constraints

- Frozen comparison base: `0a73d924944a192c16c12260447c63272b727899`.
- Branch: `codex/gate8-real-tcp-e2e`.
- Worktree: `.worktrees/gate8-real-tcp-e2e`.
- Initial Planning commit: `75b203b32827e359290eaba12a3247191490665f`.
- Implementation starts from the exact final amended Planning HEAD independently approved for this branch, not by resetting or checking out either the Initial Planning commit or frozen base. The final Planning HEAD's direct parent is Initial Planning; Initial Planning's direct parent is the frozen base. The frozen base remains the diff/regression comparison baseline.
- The worktree must be clean and local Planning HEAD must equal remote Planning HEAD before Task 1.
- The ordinary checkout must retain only its two user-owned modifications:

```text
 M Assets/Settings/Mobile_RPAsset.asset
 M ProjectSettings/ShaderGraphSettings.asset
```

- Never modify, restore, stage, clean, or commit those ordinary-checkout files.
- Create exactly one Gate 8 project under `Tests/LockstepArena.TcpEndToEnd.Tests/`; create no production assembly, package, Unity test, shared helper, solution, or transport abstraction.
- All Gate 8 TCP symbols must remain below that new test directory.
- Do not modify frozen Simulation, Protocol, StreamFraming, FrameSync, or ProtocolAuthority production.
- Do not modify any pre-existing test project/source, Assets, ProjectSettings, manifest, or packages-lock as committed scope.
- `.gitignore` may gain exactly one line: `!Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj`.
- Use real synchronous IPv4 loopback TCP. Do not use fake/in-memory streams, async/Task/thread execution, background loops, socket timeouts, retry, reconnect, heartbeat, router, opcode, envelope, session lifecycle, timing policy, KCP, UDP, prediction, rollback, replay, or a generic transport framework.
- Every task follows RED -> verify the intended failure -> minimal implementation -> GREEN -> focused audit -> commit.
- If a frozen contract is contradictory or cannot be implemented, stop and report it. Do not change architecture silently.

## 2. Implementation-Start Verification

- [ ] From the Gate 8 worktree, verify the exact branch:

```powershell
if ((git branch --show-current) -ne 'codex/gate8-real-tcp-e2e') {
    throw 'Wrong Gate 8 branch.'
}
```

- [ ] Resolve local and remote Planning HEAD and require equality. The command records the exact implementation start SHA supplied by the independent Planning approval:

```powershell
$localPlanningHead = git rev-parse HEAD
$remoteLine = git ls-remote --heads origin refs/heads/codex/gate8-real-tcp-e2e
$remotePlanningHead = ($remoteLine -split '\s+')[0]
if ([string]::IsNullOrWhiteSpace($remotePlanningHead) -or $localPlanningHead -ne $remotePlanningHead) {
    throw 'Local and remote Gate 8 Planning HEAD must match.'
}
```

- [ ] Require the two-commit Planning chain, frozen-base merge-base, and clean worktree:

```powershell
$frozenBase = '0a73d924944a192c16c12260447c63272b727899'
$initialPlanning = '75b203b32827e359290eaba12a3247191490665f'
if ((git rev-parse HEAD^) -ne $initialPlanning) {
    throw 'Final Gate 8 Planning HEAD must have Initial Planning as direct parent.'
}
if ((git rev-parse "$initialPlanning^") -ne $frozenBase) {
    throw 'Initial Gate 8 Planning must have the frozen Gate 7 baseline as direct parent.'
}
if ((git merge-base HEAD $frozenBase) -ne $frozenBase) {
    throw 'Frozen Gate 7 baseline is not the Gate 8 merge-base.'
}
$ahead = [int](git rev-list --count "$frozenBase..HEAD")
$behind = [int](git rev-list --count "HEAD..$frozenBase")
if ($ahead -ne 2 -or $behind -ne 0) {
    throw 'Frozen Base to final Gate 8 Planning must be exactly two commits ahead and zero behind.'
}
if ((git status --porcelain).Length -ne 0) {
    throw 'Gate 8 worktree must be clean.'
}
```

- [ ] From the ordinary checkout, require the exact two user-owned status lines and no Gate 8 path. Do not run any restore or clean there.
- [ ] Inspect the Planning diff and require exactly:

```text
Docs/Architecture/GATE8_MINIMAL_REAL_TCP_END_TO_END.md
Docs/superpowers/plans/2026-09-03-gate8-minimal-real-tcp-end-to-end.md
```

Use `git diff --name-only $frozenBase HEAD` and reject any third path; the Amendment may modify only the Plan while the cumulative committed diff remains the same two documents.

- [ ] Read the authoritative Spec completely before implementation and confirm the Plan contains no unresolved marker or obsolete pre-amendment contract.

---

## Task 1: Prove the Successful Real-TCP Golden Path

**Commit:** `test: prove real TCP authority round trip`

**Files:**

- Modify: `.gitignore`
- Create: `Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj`
- Create: `Tests/LockstepArena.TcpEndToEnd.Tests/Program.cs`
- Create: `Tests/LockstepArena.TcpEndToEnd.Tests/LoopbackTcpGoldenVector.cs`
- Create: `Tests/LockstepArena.TcpEndToEnd.Tests/LoopbackTcpEndToEndTests.cs`

### Frozen project XML

The csproj must be exactly equivalent to:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <BuildInParallel>false</BuildInParallel>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Packages\com.locksteparena.stream-framing\Runtime\LockstepArena.StreamFraming.csproj" />
    <ProjectReference Include="..\..\Server\LockstepArena.Server.ProtocolAuthority\LockstepArena.Server.ProtocolAuthority.csproj" />
    <ProjectReference Include="..\..\Packages\com.locksteparena.protocol\Runtime\LockstepArena.Protocol.csproj" />
    <ProjectReference Include="..\..\Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj" />
  </ItemGroup>
</Project>
```

There is no direct FrameSync reference, package reference, external `Compile Include`, test SDK, or existing test-project reference.

### Exact actual-result API

`LoopbackTcpGoldenVector.cs` must expose only this internal test-side API:

```csharp
internal static class LoopbackTcpGoldenVector
{
    internal static LoopbackTcpGoldenResult Run();
}

internal sealed class LoopbackTcpGoldenResult
{
    public IPAddress ListenerAddress { get; }
    public int ListenerPort { get; }
    public IPAddress ClientRemoteAddress { get; }
    public int ClientRemotePort { get; }
    public int ServerReadCallCount { get; }
    public int ClientReadCallCount { get; }
    public byte[][] SubmissionPayloads { get; }
    public byte[][] RecoveredSubmissionPayloads { get; }
    public int[] ProcessorOutputCounts { get; }
    public byte[][] AuthoritativePayloads { get; }
    public byte[][] RecoveredAuthoritativePayloads { get; }
    public FrameData[] AuthoritativeFrames { get; }
    public BattleState[] ClientStates { get; }
    public ulong[] ClientDigests { get; }
    public BattleState ServerState { get; }
    public BattleState ClientState { get; }
    public uint NextPublishTick { get; }
}
```

The vector contains actual construction/execution/result capture only. Every expected literal and every assertion remains in `LoopbackTcpEndToEndTests.cs`.

### RED

- [ ] Add the one exact `.gitignore` exception and no other ignore change.
- [ ] Create the csproj and dependency-free `Program` runner.
- [ ] In `LoopbackTcpEndToEndTests.cs`, define and register these six successful-path tests in this exact order:

```text
ListenerBindsIpv4LoopbackOnOsAssignedPort
ServerReadLoopUsesReusableOffsetSegmentAcrossMultipleReads
ContinuousClientStreamRecoversTwelveSubmissionPayloadsInOrder
GapFillPublishesTicks100Through102AsIndependentPayloads
ContinuousServerStreamRecoversThreeAuthoritativePayloadsInOrder
RealTcpRoundTripMatchesApprovedAuthoritySequenceStatesAndDigests
```

- [ ] Test responsibilities must remain distinct:
  - Test 1 owns endpoint assertions only.
  - Test 2 owns `ServerReadCallCount > 1` only and does not assert an individual Read size.
  - Test 3 owns byte-for-byte ordered equality of the 12 original/recovered submission payloads.
  - Test 4 owns output counts `0` for calls 1-11 and `3` for call 12, independent authority payload buffers, and authoritative Frame Ticks 100/101/102.
  - Test 5 owns `ClientReadCallCount > 1` plus byte-for-byte ordered equality of the three original/recovered authoritative payloads; it does not assert an individual Read size.
  - Test 8 owns two independent Golden executions, field-for-field equality of their authoritative Domain Frame sequences, exact intermediate Client states/Digests, complete final state, `NextPublishTick`, and Server/Client equality. It does not compare ephemeral ports or repeat endpoint, Read-count, payload-byte, output-count, payload-ownership, or literal authority-Tick assertions owned by Tests 1-5.
- [ ] Run a Release build before creating `LoopbackTcpGoldenVector.cs`. Require compilation failure caused by the missing `LoopbackTcpGoldenVector` / `LoopbackTcpGoldenResult` only. A project-reference, syntax, or runner failure is not the intended RED.

### Minimal implementation

- [ ] Implement the actual-only vector with `MaxPayloadLength = 4096` and the exact frozen roster:

```text
Slot0 = 0x0102030405060708
Slot1 = 0x000000000000002A
Slot2 = 0xFFEEDDCCBBAA0099
Slot3 = 0x00000000000F4243
```

- [ ] Create independent Server and Client BattleStates at Tick 100:

```text
Slot0 = X -300, Z 0,    Aim 1000
Slot1 = X 300,  Z 0,    Aim 2000
Slot2 = X 0,    Z -300, Aim 3000
Slot3 = X 0,    Z 300,  Aim 4000
```

- [ ] Generate these exact inputs:

```text
Tick100 Slot0 ( 1,  0, 10100)  Slot1 (-1,  0, 20100)
        Slot2 ( 0,  1, 30100)  Slot3 ( 0, -1, 40100)
Tick101 Slot0 ( 0,  1, 10101)  Slot1 ( 0, -1, 20101)
        Slot2 ( 1,  0, 30101)  Slot3 (-1,  0, 40101)
Tick102 Slot0 (-1,  0, 10102)  Slot1 ( 1,  0, 20102)
        Slot2 ( 0, -1, 30102)  Slot3 ( 0,  1, 40102)
```

- [ ] Serialize submissions in the exact arrival order:

```text
Tick100: Slot 0,2,1
Tick101: Slot 3,1,0,2
Tick102: Slot 2,0,3,1
Tick100: Slot 3
```

- [ ] Frame all 12 payloads individually and concatenate them into one continuous Client-to-Server byte array before any write.
- [ ] Create the real endpoint exactly with `TcpListener(IPAddress.Loopback, 0)`, `listener.Start(1)`, its `LocalEndpoint`, `TcpClient(AddressFamily.InterNetwork)`, `client.Connect(IPAddress.Loopback, port)`, and one `AcceptTcpClient`.
- [ ] Use the same connection bidirectionally with scoped disposal and `listener.Stop()` in `finally`.
- [ ] Client writes the full continuous submission stream. Server uses one 16-byte reusable receive buffer, offset 3, maximum Read count 3, and passes the exact valid segment directly to `decoder.Feed(buffer, 3, bytesRead)`.
- [ ] Do not allocate an exact-sized receive chunk. Count Read calls; never record or assert exact individual Read sizes.
- [ ] Stop Server reading after 12 recovered payloads. A zero-byte Read before 12 throws `EndOfStreamException`.
- [ ] Submit every recovered payload in order. Capture all 12 output counts; require no assertion inside the vector. Collect the three actual authority payloads from the final call.
- [ ] Frame the three authority payloads individually and concatenate them into one continuous Server-to-Client byte array. Server writes it on the same connection.
- [ ] Client uses one 16-byte reusable receive buffer, offset 5, maximum Read count 5, and calls `decoder.Feed(buffer, 5, bytesRead)` directly.
- [ ] Stop Client reading after three recovered payloads. A zero-byte Read before three throws `EndOfStreamException`.
- [ ] Parse/map each recovered payload with the independent Client roster, Step the Client Simulation, and capture actual Frames, Client states, Digests, final Server/Client state, and NextPublishTick.
- [ ] Keep the vector free of expected state/Digest literals, expected result arrays, assertions, pass/fail code, file/time/environment/random input, and copied Gate 6/Gate 7 test helpers.
- [ ] In Test 8, call `LoopbackTcpGoldenVector.Run()` twice. Treat both calls as independent network runs with separately allocated listener/client/server resources.
- [ ] Compare the two runs' `AuthoritativeFrames` without asserting an expected sequence length or literal Tick already owned by Test 4. First require the two actual lengths to equal each other, then compare every Frame field: Frame Tick, roster Count, every Slot's PlayerId, InputCount, and every Slot's Input Tick, PlayerSlot, MoveX, MoveZ, and Aim.
- [ ] Do not compare `ListenerPort` or any endpoint field between runs. Operating-system ephemeral-port reuse is legal.
- [ ] Test 8 must assert these exact consumer-owned Client state/Digest literals for each independent run; none may appear in `LoopbackTcpGoldenVector.cs`:

```text
After authoritative Tick100 / Client State Tick101
Slot0 = X -200, Z 0,    Aim 10100
Slot1 = X 200,  Z 0,    Aim 20100
Slot2 = X 0,    Z -200, Aim 30100
Slot3 = X 0,    Z 200,  Aim 40100
Digest = 0xD95809E1EB5CDDAA

After authoritative Tick101 / Client State Tick102
Slot0 = X -200, Z 100,  Aim 10101
Slot1 = X 200,  Z -100, Aim 20101
Slot2 = X 100,  Z -200, Aim 30101
Slot3 = X -100, Z 200,  Aim 40101
Digest = 0xA96B83267DD72A7D

After authoritative Tick102 / Client State Tick103
Slot0 = X -300, Z 100,  Aim 10102
Slot1 = X 300,  Z -100, Aim 20102
Slot2 = X 100,  Z -300, Aim 30102
Slot3 = X -100, Z 300,  Aim 40102
Digest = 0x386C4BB11A7EB7E0
```

- [ ] Test 8 independently asserts each run's final Server/Client full-state equality, structural roster equality, `NextPublishTick == 103`, and final Server/Client Digest `0x386C4BB11A7EB7E0`. These remain Test 8 responsibilities and do not add assertions to Tests 1-5.

### GREEN, audit, and commit

- [ ] Build the new project Release and require zero warnings / zero errors.
- [ ] Run the six-test executable through the bounded test-process procedure in Section 3 and require `RESULT 6/6 passed`.
- [ ] Audit that the three Digests below exist only in the consumer tests and not in the vector:

```text
D95809E1EB5CDDAA
A96B83267DD72A7D
386C4BB11A7EB7E0
```

- [ ] Audit exact project references, four-file layout, direct Read-to-Feed calls, no exact Read-size assertion, one connection, no socket timeout, and TCP source limited to the new test directory.
- [ ] Require `.gitignore` to differ by exactly the one approved line. Require all protected paths and pre-existing tests to have zero diff from the frozen base.
- [ ] Run `git diff --check`, inspect the staged diff, and commit only this task:

```powershell
git add .gitignore Tests/LockstepArena.TcpEndToEnd.Tests
git commit -m "test: prove real TCP authority round trip"
```

---

## Task 2: Prove Deterministic Early EOF Failures

**Commit:** `test: prove deterministic TCP end-of-stream failures`

**Files:**

- Modify: `Tests/LockstepArena.TcpEndToEnd.Tests/Program.cs`
- Modify: `Tests/LockstepArena.TcpEndToEnd.Tests/LoopbackTcpEndToEndTests.cs`

### RED

- [ ] Add and register the remaining tests in exact final order positions 6 and 7:

```text
ServerReadZeroBeforeTwelvePayloadsThrowsEndOfStreamException
ClientReadZeroBeforeThreePayloadsThrowsEndOfStreamException
```

- [ ] Create a `List<byte[]>` in each test and make the tests call these absent exact private helpers:

```csharp
private static void RunServerEofFixture(List<byte[]> recoveredPayloads);
private static void RunClientEofFixture(List<byte[]> recoveredPayloads);
```

Build and require compilation failure only because those private helpers are absent. Do not accept a networking or assertion failure as this RED.

### Minimal implementation

- [ ] Implement both exact signatures as private static members of `LoopbackTcpEndToEndTests.cs`; use `System.Collections.Generic.List<byte[]>` and do not change the Golden API, add another helper/result type, or create a fifth source file.
- [ ] Each public EOF test creates its own empty list, calls its helper through `TestAssert.Throws<EndOfStreamException>`, and then independently asserts the recovered count:

```csharp
var serverRecoveredPayloads = new List<byte[]>();
TestAssert.Throws<EndOfStreamException>(
    () => RunServerEofFixture(serverRecoveredPayloads));
TestAssert.Equal(11, serverRecoveredPayloads.Count);

var clientRecoveredPayloads = new List<byte[]>();
TestAssert.Throws<EndOfStreamException>(
    () => RunClientEofFixture(clientRecoveredPayloads));
TestAssert.Equal(2, clientRecoveredPayloads.Count);
```

The helpers append each completed payload before the subsequent zero-byte Read raises `EndOfStreamException`; the exception assertion and recovered-count assertion remain separate.
- [ ] Server EOF fixture:

```text
obtain the actual 12 Gate 8 submission payloads
-> create a fresh real IPv4 loopback connection
-> Client writes the first 11 complete framed submissions
-> client.Client.Shutdown(SocketShutdown.Send)
-> Server recovers exactly 11
-> next Read returns 0 before required count 12
-> throw EndOfStreamException
```

- [ ] Client EOF fixture:

```text
obtain actual authoritative payloads from one successful LoopbackTcpGoldenVector.Run
-> create a fresh real IPv4 loopback connection
-> Server writes the first two complete framed authority payloads
-> acceptedClient.Client.Shutdown(SocketShutdown.Send)
-> Client recovers exactly 2
-> next Read returns 0 before required count 3
-> throw EndOfStreamException
```

- [ ] Use the same exact receive-buffer/offset/Read contracts as the successful loops. Do not hardcode protobuf wire bytes.
- [ ] Keep `SocketShutdown.Send` confined to these two test-fixture helpers. It establishes no disconnect, half-close, reconnect, or production lifecycle policy.
- [ ] Preserve scoped disposal and listener `finally` cleanup; propagate the exact `EndOfStreamException` expected by each test.

### GREEN, audit, and commit

- [ ] Build Release with zero warnings / zero errors.
- [ ] Run through the bounded test-process procedure and require exactly `RESULT 8/8 passed`.
- [ ] Verify all eight names are registered once in the frozen order and responsibility assertions have not drifted.
- [ ] Search `SocketShutdown` and require matches only inside the private EOF fixture implementation.
- [ ] Re-run the TCP-only source-scope and protected-path audits.
- [ ] Run `git diff --check`, inspect the staged diff, and commit only the two test files:

```powershell
git add Tests/LockstepArena.TcpEndToEnd.Tests/Program.cs Tests/LockstepArena.TcpEndToEnd.Tests/LoopbackTcpEndToEndTests.cs
git commit -m "test: prove deterministic TCP end-of-stream failures"
```

---

## 3. Bounded Synchronous-Test Execution Procedure

This is an outer verification watchdog only. It adds no socket timeout, gameplay timeout, cancellation, retry, or product/test transport behavior.

- [ ] Build the Gate 8 test project first, then launch its existing `dotnet run --no-build` command as one captured process. Redirect output under `.artifacts/`, wait at most 30 seconds, and inspect the captured result:

```powershell
$stdoutPath = Join-Path (Get-Location) '.artifacts/gate8-tcp-tests.stdout.txt'
$stderrPath = Join-Path (Get-Location) '.artifacts/gate8-tcp-tests.stderr.txt'
$arguments = @(
    'run',
    '--project', 'Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj',
    '--configuration', 'Release',
    '--no-build'
)
$gate8Process = Start-Process dotnet `
    -ArgumentList $arguments `
    -PassThru `
    -WindowStyle Hidden `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath

if (-not $gate8Process.WaitForExit(30000)) {
    & taskkill.exe /PID $gate8Process.Id /T /F | Out-Null
    throw 'Gate 8 synchronous TCP test process exceeded the 30-second verification bound.'
}

$gate8Process.WaitForExit()
$stdout = Get-Content -Raw $stdoutPath
$stderr = Get-Content -Raw $stderrPath
Write-Output $stdout
if ($gate8Process.ExitCode -ne 0) {
    Write-Error $stderr
    throw "Gate 8 test process exited with code $($gate8Process.ExitCode)."
}
```

- [ ] The timeout branch kills only the captured Gate 8 process tree. Never terminate processes by the broad name `dotnet`.
- [ ] A watchdog expiration is a test failure requiring investigation; it is not an expected networking result and must not be converted into a retry.

---

## Task 3: Run Fresh Final Verification and Record Evidence

**Commit:** `docs: record Gate 8 implementation evidence`

**Files:**

- Modify: `Docs/Architecture/GATE8_MINIMAL_REAL_TCP_END_TO_END.md`

### Restore-assets preflight

- [ ] Start from a clean post-Task-2 worktree. Define the exact 13-project list:

```powershell
$projects = @(
    'Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj',
    'Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj',
    'Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj',
    'Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj',
    'Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj',
    'Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj',
    'Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj',
    'Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj',
    'Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj',
    'Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj',
    'Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj',
    'Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj',
    'Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj'
)
```

- [ ] Resolve each effective `ProjectAssetsFile`. Restore only a project whose asset is missing:

```powershell
$restoreOccurred = $false
foreach ($project in $projects) {
    $propertyOutput = & dotnet msbuild $project -nologo -verbosity:quiet -getProperty:ProjectAssetsFile
    if ($LASTEXITCODE -ne 0) {
        throw "Could not resolve ProjectAssetsFile for $project"
    }

    $assetPath = ($propertyOutput | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Last 1).Trim()
    if (-not [System.IO.Path]::IsPathRooted($assetPath)) {
        $assetPath = Join-Path (Split-Path -Parent $project) $assetPath
    }

    if (-not (Test-Path -LiteralPath $assetPath)) {
        & dotnet restore $project --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "Restore failed for $project"
        }
        $restoreOccurred = $true
    }
}
```

- [ ] Restore uses only each frozen project contract. Do not change a dependency, version, project reference, NuGet source, generated source, or project file. On restore/network failure, stop and report.
- [ ] Inspect `git status --short` and `git diff`. Any unexpected repository change is a blocker. If any restore occurred, explicitly restart the complete build matrix at build 1.

### Exact 13 Release builds

- [ ] Run these exact 13 commands independently and in this order:

```powershell
dotnet build Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj --configuration Release --no-restore --nologo
dotnet build Server/LockstepArena.Server.FrameSync/LockstepArena.Server.FrameSync.csproj --configuration Release --no-restore --nologo
dotnet build Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj --configuration Release --no-restore --nologo
dotnet build Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj --configuration Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj --configuration Release --no-restore --nologo
dotnet build Tools/LockstepArena.Protocol.CodeGen/LockstepArena.Protocol.CodeGen.csproj --configuration Release --no-restore --nologo
dotnet build Packages/com.locksteparena.protocol/Runtime/LockstepArena.Protocol.csproj --configuration Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj --configuration Release --no-restore --nologo
dotnet build Server/LockstepArena.Server.ProtocolAuthority/LockstepArena.Server.ProtocolAuthority.csproj --configuration Release --no-restore --nologo
dotnet build Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj --configuration Release --no-restore --nologo
dotnet build Packages/com.locksteparena.stream-framing/Runtime/LockstepArena.StreamFraming.csproj --configuration Release --no-restore --nologo
dotnet build Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj --configuration Release --no-restore --nologo
dotnet build Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj --configuration Release --no-restore --nologo
```

- [ ] Record zero warnings / zero errors for every project. A failure or restore discovered mid-matrix invalidates the matrix; correct only the in-scope cause and restart at build 1.

### Fresh .NET regressions

- [ ] Run and record these existing suites with `--configuration Release --no-build`:

```powershell
dotnet run --project Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj --configuration Release --no-build
dotnet run --project Tests/LockstepArena.Server.FrameSync.Tests/LockstepArena.Server.FrameSync.Tests.csproj --configuration Release --no-build
dotnet run --project Tests/LockstepArena.Server.Protocol.Tests/LockstepArena.Server.Protocol.Tests.csproj --configuration Release --no-build
dotnet run --project Tests/LockstepArena.Server.ProtocolAuthority.Tests/LockstepArena.Server.ProtocolAuthority.Tests.csproj --configuration Release --no-build
dotnet run --project Tests/LockstepArena.StreamFraming.Tests/LockstepArena.StreamFraming.Tests.csproj --configuration Release --no-build
dotnet run --project Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj --configuration Release --no-build
```

- [ ] Require exact results:

```text
Gate 3 Simulation:         RESULT 38/38 passed
Gate 4 FrameSync:          RESULT 32/32 passed
Gate 5 Protocol:           RESULT 35/35 passed
Gate 6 ProtocolAuthority:  RESULT 24/24 passed
Gate 7 StreamFraming:      RESULT 32/32 passed
Gate 3 Server Golden:      Tick=1000 Players=4 Digest=89A7DD66F8D9E871
```

- [ ] Run Gate 8 with the bounded procedure in Section 3. Require:

```text
RESULT 8/8 passed
final Tick = 103
final Digest = 386C4BB11A7EB7E0
```

### Fresh Unity regressions

- [ ] Confirm Unity Editor is closed and use Unity 6000.3.10f1 only from the Gate 8 worktree. Create distinct fresh log/XML paths under `.artifacts/gate8-unity/`.
- [ ] Run Gate 7 framing by assembly filter:

```powershell
& 'E:\unityhub\unity6.3\Editor\Unity.exe' -batchmode -nographics -quit `
    -projectPath (Get-Location).Path `
    -runTests -testPlatform EditMode `
    -assemblyNames LockstepArena.StreamFraming.Editor.Tests `
    -testResults '.artifacts/gate8-unity/gate7-results.xml' `
    -logFile '.artifacts/gate8-unity/gate7-unity.log'
```

- [ ] Parse the fresh XML and require `total=1`, `passed=1`, `failed=0`, with exactly one matching named test result `Passed`:

```text
UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden
```

- [ ] Run Gate 5 Protocol separately by assembly filter `LockstepArena.Protocol.Editor.Tests`, with new XML/log paths. Parse and require `total=2`, `passed=2`, `failed=0`, and both named tests `Passed`:

```powershell
& 'E:\unityhub\unity6.3\Editor\Unity.exe' -batchmode -nographics -quit `
    -projectPath (Get-Location).Path `
    -runTests -testPlatform EditMode `
    -assemblyNames LockstepArena.Protocol.Editor.Tests `
    -testResults '.artifacts/gate8-unity/gate5-results.xml' `
    -logFile '.artifacts/gate8-unity/gate5-unity.log'
```

```text
GoogleProtobufDependencyPreflightTests.RuntimeDependencyLoads
UnityProtocolGoldenVectorTests.UnityExecutesGate5ProtocolRoundTripGoldenVector
```

- [ ] Run Gate 3 Simulation separately:

```powershell
& 'E:\unityhub\unity6.3\Editor\Unity.exe' -batchmode -nographics -quit `
    -projectPath (Get-Location).Path `
    -runTests -testPlatform EditMode `
    -assemblyNames LockstepArena.Simulation.Editor.Tests `
    -testFilter UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector `
    -testResults '.artifacts/gate8-unity/gate3-results.xml' `
    -logFile '.artifacts/gate8-unity/gate3-unity.log'
```

Parse and require `total>=1`, `failed=0`, and `UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector` result `Passed`.
- [ ] Parse every XML with a fresh-file check and an exact named-test lookup. The following PowerShell pattern is applied to each run with its frozen expected totals and names:

```powershell
$resultsPath = '.artifacts/gate8-unity/gate7-results.xml'
if (-not (Test-Path -LiteralPath $resultsPath)) {
    throw 'Fresh Unity NUnit XML was not generated.'
}
[xml]$results = Get-Content -Raw $resultsPath
$run = $results.'test-run'
if ([int]$run.total -ne 1 -or [int]$run.passed -ne 1 -or [int]$run.failed -ne 0) {
    throw 'Unity NUnit totals do not match the frozen Gate contract.'
}
$matches = @($results.SelectNodes("//test-case[contains(@fullname,'UnityStreamFramingGoldenTests.UnityExecutesApprovedAbcSegmentationGolden')]"))
if ($matches.Count -ne 1 -or $matches[0].result -ne 'Passed') {
    throw 'The required named Unity test did not pass exactly once.'
}
```
- [ ] For each run, verify the XML timestamp belongs to this final verification and inspect the `test-run` totals plus the unique matching `test-case` result. Unity exit code alone is not evidence.
- [ ] If license, package-manager, network dependency, or instance-lock failure prevents a worktree run, stop and report. Never use the ordinary checkout as a workaround.
- [ ] After each Unity job, inspect `git status --short` and the exact diffs under Assets/ProjectSettings. Restore only a confirmed Unity-generated worktree-local path after viewing its diff:

```powershell
git diff -- Assets/Settings/Mobile_RPAsset.asset ProjectSettings/ShaderGraphSettings.asset
git restore --worktree --source=HEAD -- Assets/Settings/Mobile_RPAsset.asset ProjectSettings/ShaderGraphSettings.asset
```

Use the restore command only when those exact worktree-local diffs are verified as Unity automation. Do not use broad reset/clean or operate on the ordinary checkout.

### Protected-path, dependency, artifact, and scope audits

- [ ] Require zero frozen-base committed and working-tree diff for existing production and Unity configuration:

```powershell
$base = '0a73d924944a192c16c12260447c63272b727899'
git diff --exit-code $base -- Packages/com.locksteparena.simulation
git diff --exit-code $base -- Packages/com.locksteparena.protocol
git diff --exit-code $base -- Packages/com.locksteparena.stream-framing
git diff --exit-code $base -- Server/LockstepArena.Server.FrameSync
git diff --exit-code $base -- Server/LockstepArena.Server.ProtocolAuthority
git diff --exit-code $base -- Assets ProjectSettings Packages/manifest.json Packages/packages-lock.json
git diff --exit-code $base -- Tests ':(exclude)Tests/LockstepArena.TcpEndToEnd.Tests/**'
```

- [ ] Require `.gitignore` to add exactly one line and no deletion:

```text
!Tests/LockstepArena.TcpEndToEnd.Tests/LockstepArena.TcpEndToEnd.Tests.csproj
```

- [ ] Require the Gate 8 directory to contain exactly four authored files and the csproj to contain exactly the four approved direct ProjectReferences, no direct FrameSync reference, no package/test-framework reference, and no external Compile Include.
- [ ] Search all source for Gate 8-introduced TCP symbols and reject any match outside the new test directory. At minimum search:

```text
TcpListener
TcpClient
NetworkStream
IPAddress.Loopback
IPEndPoint
AddressFamily.InterNetwork
SocketShutdown
```

- [ ] Search the Gate 8 committed diff for forbidden production scope: TCP adapter, async/Task/thread/background loop, retry/reconnect/heartbeat, opcode/envelope/router, Login/Room/Session, TickClock/InputDelay/gameplay timeout, KCP/UDP, prediction/snapshot/rollback/replay, TLS/compression/encryption, DI/middleware/event bus, or generic transport framework.
- [ ] Confirm `SocketShutdown.Send` exists only in private EOF helpers and no socket timeout property is set.
- [ ] Confirm each successful Read feeds the same receive buffer with the exact offset and returned `bytesRead`, with no exact-sized intermediary input array.
- [ ] Confirm no copied or linked Gate 6/Gate 7 test helper, no external Golden file, no symlink/junction, and no copy/sync/cleanup script.
- [ ] Audit the package and repository for `bin`, `obj`, LockstepArena build DLL, unexpected generated source, or tracked build output. Existing build output must remain under ignored `.artifacts/`; no artifact is committed.
- [ ] From the ordinary checkout, require exactly the two user-owned status lines and no Gate 8 path. Do not mutate that checkout.

### Evidence, final commit, push, and STOP

- [ ] Append `## 19. Implementation Evidence` to `Docs/Architecture/GATE8_MINIMAL_REAL_TCP_END_TO_END.md`. Record only freshly executed facts:
  - exact frozen base, Planning HEAD, implementation commits, and final evidence commit parent;
  - restore-assets preflight result;
  - all 13 build commands and zero warning/error results;
  - Gate 3/4/5/6/7/8 suite totals and Gate 3 Server Golden;
  - Gate 8 endpoint/read-count/payload-count/authority Tick evidence;
  - per-Tick Digests, final Tick 103, full final state, and Server/Client equality;
  - all three fresh Unity XML paths, totals, and named-test results;
  - protected-path, pre-existing-test, `.gitignore`, dependency, source-scope, artifact, and ordinary-checkout audits;
  - bounded process-watchdog result and absence of socket/gameplay timeout behavior.
- [ ] Inspect the complete frozen-base diff, run `git diff --check`, and verify the Architecture contains no contradiction or unresolved marker.
- [ ] Commit only the evidence update:

```powershell
git add Docs/Architecture/GATE8_MINIMAL_REAL_TCP_END_TO_END.md
git commit -m "docs: record Gate 8 implementation evidence"
```

- [ ] Push only `codex/gate8-real-tcp-e2e` after successful final verification.
- [ ] Prove remote SHA equals local HEAD:

```powershell
$localFinal = git rev-parse HEAD
$remoteFinal = ((git ls-remote --heads origin refs/heads/codex/gate8-real-tcp-e2e) -split '\s+')[0]
if ($localFinal -ne $remoteFinal) {
    throw 'Remote Gate 8 SHA does not match local final HEAD.'
}
```

- [ ] Require the Gate 8 worktree clean, confirm ordinary-checkout preservation one final time, and submit the Gate 8 Final Implementation Handoff.
- [ ] **STOP. Do not begin Gate 9, production TCP, KCP, timing policy, or any next-Gate planning/implementation.**

## 4. Final Acceptance Invariants

Gate 8 is eligible for Final Handoff only when:

- exactly one new test project and no production assembly exists;
- Gate 8 reports `RESULT 8/8 passed` under the bounded execution procedure;
- all 13 Release builds report zero warnings / zero errors;
- Gate 3/4/5/6/7 regressions and Gate 3 Server Golden are fresh and exact;
- Unity Gate 7 is 1/1, Gate 5 is 2/2, and the Gate 3 named Golden is Passed from fresh NUnit XML;
- real TCP recovered all 12 submission and three authority payloads in order without Read-size assumptions;
- the Processor published Ticks 100/101/102 and both simulations reached Tick 103 / Digest `386C4BB11A7EB7E0`;
- EOF fixtures deterministically fail on zero-byte reads before expected counts;
- `.gitignore`, protected paths, pre-existing tests, manifest, lockfile, dependencies, source scope, and artifacts match the frozen contracts;
- remote SHA equals local HEAD and the Gate 8 worktree is clean;
- work stops before any production TCP or next Gate.
