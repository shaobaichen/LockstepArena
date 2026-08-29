# Gate 2 Shared Runtime Consumption Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove that Unity EditMode and an offline .NET Server process execute the repository's one physical Shared Simulation source tree and produce the approved state and Golden Digest after the same 1,000 inputs.

**Architecture:** Move the approved Gate 1 Simulation files, without duplicating them, into the Runtime directory of one complete embedded Unity package. Unity compiles that directory through an asmdef and .NET compiles it through a co-located netstandard2.1 csproj. A single pure-C# Golden Vector file is compiled by the Unity Editor test assembly and linked into a one-shot Server Verification executable.

**Tech Stack:** Unity 6000.3.10f1, Unity Test Framework 1.6.0, NUnit supplied by Unity Test Framework, C# 9 production source, .NET Standard 2.1 Shared assembly, .NET 8 verification executables, SDK-style MSBuild projects, PowerShell verification commands.

**Spec:** `Docs/Architecture/GATE2_SHARED_RUNTIME_CONSUMPTION.md`

## Global Constraints

- Implement only after independent approval of this plan.
- Work only in `.worktrees/gate2-shared-runtime-consumption` on branch `codex/gate2-shared-runtime-consumption`.
- Preserve exact approved ancestry from `a4cdf3ef3a23e587b1a6ab9a6e0c2cfc93e8abf0`.
- Keep exactly one physical copy of every production Simulation `.cs` file under `Packages/com.locksteparena.simulation/Runtime/`.
- Do not use copy/sync scripts, junctions, symlinks, precompiled Shared DLLs, NuGet publication, or a second Simulation tree.
- Keep Runtime free of UnityEngine, UnityEditor, NUnit, Unity Test Framework, PackageReference, ProjectReference, unsafe code, networking, Protobuf, combat, prediction, snapshot, rollback, and view logic.
- `Gate2GoldenVector.cs` is one physical pure-C# file with no NUnit, Unity, I/O, time, environment, random, or serialization dependency.
- The Golden Vector executes exactly 1,000 ticks and never derives or rewrites its expected values from actual output.
- Unity and Server separately assert Tick 1000, Player 0 `(0, -3000, 13086)`, Player 1 `(0, 3000, 8699)`, and digest `0x04633D1F8699DE68`.
- Preserve the complete Gate 1 test suite and its `15/15 passed` result.
- Do not modify `Assets/`, `ProjectSettings/`, or `Packages/manifest.json`.
- Do not touch the normal checkout's user-owned `Assets/Settings/Mobile_RPAsset.asset` or `ProjectSettings/ShaderGraphSettings.asset` changes.
- If Unity cannot test the Gate 2 worktree because of an instance lock, license, or environment error, stop and report; never test the normal checkout as a substitute.
- Keep all Unity test XML, Editor logs, `Library/`, `.NET bin/obj`, and other generated output untracked.
- Do not begin any Gate 3 work after Gate 2 verification.

---

### Task 1: Make the Embedded Package the Only Production Source Home

**Files:**

- Create: `Packages/com.locksteparena.simulation/package.json`
- Move: `Source/LockstepArena.Simulation/BattleSimulation.cs` → `Packages/com.locksteparena.simulation/Runtime/BattleSimulation.cs`
- Move: `Source/LockstepArena.Simulation/BattleState.cs` → `Packages/com.locksteparena.simulation/Runtime/BattleState.cs`
- Move: `Source/LockstepArena.Simulation/FrameData.cs` → `Packages/com.locksteparena.simulation/Runtime/FrameData.cs`
- Move: `Source/LockstepArena.Simulation/InputFrame.cs` → `Packages/com.locksteparena.simulation/Runtime/InputFrame.cs`
- Move: `Source/LockstepArena.Simulation/PlayerState.cs` → `Packages/com.locksteparena.simulation/Runtime/PlayerState.cs`
- Move: `Source/LockstepArena.Simulation/SimulationConfig.cs` → `Packages/com.locksteparena.simulation/Runtime/SimulationConfig.cs`
- Move: `Source/LockstepArena.Simulation/StateDigest.cs` → `Packages/com.locksteparena.simulation/Runtime/StateDigest.cs`
- Move: `Source/LockstepArena.Simulation/LockstepArena.Simulation.csproj` → `Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj`
- Create: `Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.asmdef`
- Modify: `Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj`
- Modify: `.gitignore`

**Interfaces:**

- Consumes: the exact Gate 1 Simulation public API and tests at approved commit `a4cdf3e`.
- Produces: Unity assembly and .NET project both named `LockstepArena.Simulation`, compiling the same Runtime `.cs` files.

- [ ] **Step 1: Record the clean approved baseline**

Run:

~~~powershell
git status --short --branch
git rev-parse HEAD
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'locksteparena-gate2-dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet run --project Tests\LockstepArena.Simulation.Tests\LockstepArena.Simulation.Tests.csproj -c Release
~~~

Expected: clean branch, HEAD `a4cdf3ef3a23e587b1a6ab9a6e0c2cfc93e8abf0`, and `RESULT 15/15 passed`.

- [ ] **Step 2: Move, rather than copy, the approved source files**

Create `Packages/com.locksteparena.simulation/Runtime/`, then use `git mv` for the eight source/project files listed above. Confirm the old directory has no tracked files:

~~~powershell
git ls-files Source/LockstepArena.Simulation
~~~

Expected: no output after all moves.

- [ ] **Step 3: Add the complete package manifest**

Create `Packages/com.locksteparena.simulation/package.json` with exactly:

~~~json
{
  "name": "com.locksteparena.simulation",
  "version": "0.1.0",
  "displayName": "Lockstep Arena Simulation",
  "description": "Unity-free deterministic simulation shared by client and server.",
  "unity": "6000.3"
}
~~~

Do not add an entry to `Packages/manifest.json`.

- [ ] **Step 4: Add the Unity-free Runtime asmdef**

Create `Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.asmdef` with exactly:

~~~json
{
  "name": "LockstepArena.Simulation",
  "rootNamespace": "LockstepArena.Simulation",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": false,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": true
}
~~~

- [ ] **Step 5: Keep the Runtime csproj production-only**

After the move, its exact content remains:

~~~xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
~~~

Do not add `Compile`, `PackageReference`, or `ProjectReference` items. Its directory location is the compilation boundary.

- [ ] **Step 6: Redirect the Gate 1 tests to the package project**

Replace the test project's ProjectReference with:

~~~xml
<ProjectReference Include="..\..\Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj" />
~~~

- [ ] **Step 7: Update only the authored-csproj ignore exception**

Replace the obsolete Source exception in `.gitignore` with:

~~~gitignore
!Packages/com.locksteparena.simulation/Runtime/LockstepArena.Simulation.csproj
~~~

Keep the existing test-project exception and generated `bin/`/`obj/` ignores.

- [ ] **Step 8: Build the relocated production project**

Run:

~~~powershell
dotnet build Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj -c Release
~~~

Expected: 0 warnings and 0 errors.

- [ ] **Step 9: Re-run every Gate 1 test**

Run:

~~~powershell
dotnet run --project Tests\LockstepArena.Simulation.Tests\LockstepArena.Simulation.Tests.csproj -c Release
~~~

Expected: all original names are printed and `RESULT 15/15 passed`.

- [ ] **Step 10: Review the migration diff**

Run:

~~~powershell
git diff --check
git status --short
git diff --stat
~~~

Expected: Git recognizes the source movement, the old Source directory is absent, and no Assets, ProjectSettings, manifest, Server, Unity test, DLL, bin, or obj path appears.

- [ ] **Step 11: Commit the single-source package migration**

~~~powershell
git add -- .gitignore Packages/com.locksteparena.simulation Tests/LockstepArena.Simulation.Tests/LockstepArena.Simulation.Tests.csproj
git commit -m "refactor: make embedded package the shared source"
~~~

### Task 2: Add One Pure Golden Vector and the Offline Server Consumer

**Files:**

- Create: `Packages/com.locksteparena.simulation/Tests/Editor/Gate2GoldenVector.cs`
- Create: `Packages/com.locksteparena.simulation/Tests/Editor/LockstepArena.Simulation.Editor.Tests.asmdef`
- Create: `Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj`
- Create: `Server/LockstepArena.Server.Verification/Program.cs`
- Modify: `.gitignore`

**Interfaces:**

- Consumes: `BattleState.CreateInitial()`, `InputFrame`, `FrameData`, `BattleSimulation.Step`, and `StateDigest.Compute` from the relocated Runtime project.
- Produces: `Gate2GoldenVector.Run(): Gate2GoldenVectorResult` and a process exit code from `LockstepArena.Server.Verification.Program.Main()`.

- [ ] **Step 1: Add the Server project and failing consumer first**

Add this exact project:

~~~xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj" />
    <Compile Include="..\..\Packages\com.locksteparena.simulation\Tests\Editor\Gate2GoldenVector.cs"
             Link="Gate2GoldenVector.cs" />
  </ItemGroup>
</Project>
~~~

Add the `.gitignore` exception:

~~~gitignore
!Server/LockstepArena.Server.Verification/LockstepArena.Server.Verification.csproj
~~~

Create `Program.cs` with the exact consumer below. It intentionally references the not-yet-created vector:

~~~csharp
using System;
using LockstepArena.Simulation.Verification;

namespace LockstepArena.Server.Verification
{
    internal static class Program
    {
        private static int Main()
        {
            Gate2GoldenVectorResult result = Gate2GoldenVector.Run();
            bool passed = true;
            passed &= Check("Tick", 1_000U, result.State.Tick);
            passed &= Check("Player0.PositionX", 0, result.State.Player0.PositionX);
            passed &= Check("Player0.PositionZ", -3_000, result.State.Player0.PositionZ);
            passed &= Check("Player0.Aim", (ushort)13_086, result.State.Player0.Aim);
            passed &= Check("Player1.PositionX", 0, result.State.Player1.PositionX);
            passed &= Check("Player1.PositionZ", 3_000, result.State.Player1.PositionZ);
            passed &= Check("Player1.Aim", (ushort)8_699, result.State.Player1.Aim);
            passed &= Check("Digest", 0x04633D1F8699DE68UL, result.Digest);

            if (!passed)
            {
                return 1;
            }

            Console.WriteLine($"PASS Gate2GoldenVector Tick={result.State.Tick} Digest={result.Digest:X16}");
            return 0;
        }

        private static bool Check<T>(string field, T expected, T actual)
            where T : IEquatable<T>
        {
            if (expected.Equals(actual))
            {
                return true;
            }

            Console.Error.WriteLine($"FAIL {field}: expected <{expected}> actual <{actual}>");
            return false;
        }
    }
}
~~~

- [ ] **Step 2: Run the Server consumer and confirm RED**

Run:

~~~powershell
dotnet run --project Server\LockstepArena.Server.Verification\LockstepArena.Server.Verification.csproj -c Release
~~~

Expected: compilation fails because `Gate2GoldenVector.cs`, `Gate2GoldenVector`, and `Gate2GoldenVectorResult` do not exist. A restore, SDK, or network failure is not an acceptable RED.

- [ ] **Step 3: Add the one physical pure-C# Golden Vector**

Create `Packages/com.locksteparena.simulation/Tests/Editor/Gate2GoldenVector.cs` with exactly:

~~~csharp
namespace LockstepArena.Simulation.Verification
{
    public readonly struct Gate2GoldenVectorResult
    {
        public Gate2GoldenVectorResult(BattleState state, ulong digest)
        {
            State = state;
            Digest = digest;
        }

        public BattleState State { get; }

        public ulong Digest { get; }
    }

    public static class Gate2GoldenVector
    {
        public const uint TickCount = 1_000;

        public static Gate2GoldenVectorResult Run()
        {
            BattleSimulation simulation = new BattleSimulation(BattleState.CreateInitial());

            for (uint tick = 0; tick < TickCount; tick++)
            {
                CreateInputs(tick, out InputFrame player0, out InputFrame player1);
                simulation.Step(new FrameData(player0, player1));
            }

            BattleState state = simulation.State;
            return new Gate2GoldenVectorResult(state, StateDigest.Compute(state));
        }

        private static void CreateInputs(
            uint tick,
            out InputFrame player0,
            out InputFrame player1)
        {
            int phase = (int)(tick % 400);
            sbyte player0X = 0;
            sbyte player0Z = 0;
            sbyte player1X = 0;
            sbyte player1Z = 0;

            if (phase < 100)
            {
                player0X = 1;
                player1X = -1;
            }
            else if (phase >= 150 && phase < 250)
            {
                player0X = -1;
                player1X = 1;
            }
            else if (phase >= 250 && phase < 325)
            {
                player0Z = 1;
                player1Z = -1;
            }
            else if (phase >= 325)
            {
                player0Z = -1;
                player1Z = 1;
            }

            ushort player0Aim = unchecked((ushort)((tick * 997U) + 123U));
            ushort player1Aim = unchecked((ushort)((tick * 619U) + 45_678U));
            player0 = new InputFrame(tick, 0, player0X, player0Z, player0Aim);
            player1 = new InputFrame(tick, 1, player1X, player1Z, player1Aim);
        }
    }
}
~~~

Do not add expected state or digest constants to this file. Each consumer owns its independent literal assertions.

- [ ] **Step 4: Add the Editor test assembly boundary now so the package remains structurally valid**

Create `Packages/com.locksteparena.simulation/Tests/Editor/LockstepArena.Simulation.Editor.Tests.asmdef` with exactly:

~~~json
{
  "name": "LockstepArena.Simulation.Editor.Tests",
  "rootNamespace": "LockstepArena.Simulation.Editor.Tests",
  "references": [
    "LockstepArena.Simulation"
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
~~~

- [ ] **Step 5: Run the Server Verification and confirm GREEN**

Run:

~~~powershell
dotnet run --project Server\LockstepArena.Server.Verification\LockstepArena.Server.Verification.csproj -c Release
~~~

Expected exact success line:

~~~text
PASS Gate2GoldenVector Tick=1000 Digest=04633D1F8699DE68
~~~

- [ ] **Step 6: Re-run Gate 1 regressions after introducing the vector**

Run:

~~~powershell
dotnet run --project Tests\LockstepArena.Simulation.Tests\LockstepArena.Simulation.Tests.csproj -c Release
~~~

Expected: `RESULT 15/15 passed`.

- [ ] **Step 7: Confirm the Server references rather than contains Simulation source**

Run:

~~~powershell
rg -n "ProjectReference|Compile Include" Server\LockstepArena.Server.Verification\LockstepArena.Server.Verification.csproj
rg -n "BattleSimulation|StateDigest|SimulationConfig" Server\LockstepArena.Server.Verification
~~~

Expected: one ProjectReference to package Runtime, one Compile Include for only `Gate2GoldenVector.cs`, and no reimplemented Simulation type in Server.

- [ ] **Step 8: Commit the shared vector and Server consumer**

~~~powershell
git add -- .gitignore Packages/com.locksteparena.simulation/Tests/Editor Server/LockstepArena.Server.Verification
git commit -m "test: add shared runtime golden vector"
~~~

### Task 3: Execute the Same Vector in Unity EditMode

**Files:**

- Create: `Packages/com.locksteparena.simulation/Tests/Editor/UnityGoldenVectorTests.cs`
- Create through Unity import: package `.meta` files required for `Runtime/`, `Tests/Editor/`, asmdefs, source files, and package assets
- Inspect after Unity import and modify only when Unity adds the `com.locksteparena.simulation` embedded-package record: `Packages/packages-lock.json`

**Interfaces:**

- Consumes: `Gate2GoldenVector.Run()` and `LockstepArena.Simulation` Unity assembly.
- Produces: NUnit XML containing a passed `UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector` test.

- [ ] **Step 1: Add the Unity-only assertion wrapper**

Create `Packages/com.locksteparena.simulation/Tests/Editor/UnityGoldenVectorTests.cs` with exactly:

~~~csharp
using NUnit.Framework;
using LockstepArena.Simulation.Verification;

namespace LockstepArena.Simulation.Editor.Tests
{
    public sealed class UnityGoldenVectorTests
    {
        [Test]
        public void UnityExecutesApprovedGoldenVector()
        {
            Gate2GoldenVectorResult result = Gate2GoldenVector.Run();

            Assert.That(
                typeof(BattleSimulation).Assembly.GetName().Name,
                Is.EqualTo("LockstepArena.Simulation"));
            Assert.That(result.State.Tick, Is.EqualTo(1_000U));
            Assert.That(result.State.Player0.PositionX, Is.EqualTo(0));
            Assert.That(result.State.Player0.PositionZ, Is.EqualTo(-3_000));
            Assert.That(result.State.Player0.Aim, Is.EqualTo((ushort)13_086));
            Assert.That(result.State.Player1.PositionX, Is.EqualTo(0));
            Assert.That(result.State.Player1.PositionZ, Is.EqualTo(3_000));
            Assert.That(result.State.Player1.Aim, Is.EqualTo((ushort)8_699));
            Assert.That(result.Digest, Is.EqualTo(0x04633D1F8699DE68UL));
        }
    }
}
~~~

Only this wrapper may import NUnit. `Gate2GoldenVector.cs` must remain unchanged and pure C#.

- [ ] **Step 2: Confirm the dedicated Unity executable and no active Unity process**

Run:

~~~powershell
$unity = 'E:\unityhub\unity6.3\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $unity)) { throw "Unity 6000.3.10f1 executable not found: $unity" }
if (Get-Process -Name Unity -ErrorAction SilentlyContinue) { throw 'A Unity instance is active; stop and request that it be closed before testing the worktree.' }
~~~

Do not terminate the user's Unity process automatically.

- [ ] **Step 3: Run only the Gate 2 EditMode test assembly**

Run from the Gate 2 worktree:

~~~powershell
$projectPath = (Get-Location).ProviderPath
$results = Join-Path $env:TEMP 'locksteparena-gate2-editmode-results.xml'
$editorLog = Join-Path $env:TEMP 'locksteparena-gate2-editor.log'
Remove-Item -LiteralPath $results -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $editorLog -Force -ErrorAction SilentlyContinue
& $unity -batchmode -runTests -projectPath $projectPath -testPlatform EditMode -assemblyNames LockstepArena.Simulation.Editor.Tests -testResults $results -logFile $editorLog
$unityExit = $LASTEXITCODE
if ($unityExit -ne 0) {
    Get-Content -LiteralPath $editorLog -Tail 200
    throw "Unity EditMode run failed with exit code $unityExit"
}
if (-not (Test-Path -LiteralPath $results)) {
    Get-Content -LiteralPath $editorLog -Tail 200
    throw 'Unity did not create the NUnit results XML.'
}
~~~

Expected: Unity imports and compiles the embedded package and produces the XML. Do not use `-quit`; the test runner exits when complete.

- [ ] **Step 4: Parse XML and prove the expected test ran**

Run:

~~~powershell
[xml]$testXml = Get-Content -LiteralPath $results -Raw
$run = $testXml.'test-run'
if ($null -eq $run) { throw 'NUnit XML has no test-run root.' }
if ([int]$run.total -lt 1) { throw 'Unity discovered zero tests.' }
if ([int]$run.failed -ne 0) { throw "Unity reported $($run.failed) failed tests." }
$case = $testXml.SelectSingleNode("//test-case[contains(@fullname, 'UnityGoldenVectorTests.UnityExecutesApprovedGoldenVector')]")
if ($null -eq $case) { throw 'Expected Gate 2 Unity test was not present in results.xml.' }
if ($case.result -ne 'Passed') { throw "Expected Gate 2 Unity test result was $($case.result)." }
Write-Output "Unity Gate 2 XML: total=$($run.total) passed=$($run.passed) failed=$($run.failed) test=$($case.fullname)"
~~~

Expected: total at least 1, failed 0, and the named test result `Passed`.

- [ ] **Step 5: Inspect Unity import output without accepting unrelated changes**

Run:

~~~powershell
git status --short
git diff -- Packages/manifest.json Assets ProjectSettings
~~~

Expected: no diff in `Packages/manifest.json`, `Assets`, or `ProjectSettings`. Package `.meta` files are expected and must be kept with package assets. If `Packages/packages-lock.json` changes, accept it only when its diff is limited to `com.locksteparena.simulation` with source `embedded`; otherwise stop and investigate.

- [ ] **Step 6: Repeat .NET consumers after Unity import**

Run:

~~~powershell
dotnet run --project Server\LockstepArena.Server.Verification\LockstepArena.Server.Verification.csproj -c Release
dotnet run --project Tests\LockstepArena.Simulation.Tests\LockstepArena.Simulation.Tests.csproj -c Release
~~~

Expected: Server exact PASS line and Gate 1 `RESULT 15/15 passed`.

- [ ] **Step 7: Commit Unity consumer and Unity-generated package metadata**

Stage only `Packages/com.locksteparena.simulation/`, the optional permitted package-lock diff, and no Library/log/result path:

~~~powershell
git add -- Packages/com.locksteparena.simulation
if (git diff --name-only -- Packages/packages-lock.json) { git add -- Packages/packages-lock.json }
git commit -m "test: verify shared simulation in Unity EditMode"
~~~

### Task 4: Prove Physical Uniqueness and Re-run the Full Gate

**Files:**

- Modify: `Docs/Architecture/GATE2_SHARED_RUNTIME_CONSUMPTION.md`

**Interfaces:**

- Consumes: all Gate 2 projects, tests, Unity XML, and Git tree.
- Produces: final verification evidence with no new runtime capability.

- [ ] **Step 1: Build every .NET deliverable in Release**

Run:

~~~powershell
dotnet build Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj -c Release
dotnet build Tests\LockstepArena.Simulation.Tests\LockstepArena.Simulation.Tests.csproj -c Release
dotnet build Server\LockstepArena.Server.Verification\LockstepArena.Server.Verification.csproj -c Release
~~~

Expected: each build reports 0 warnings and 0 errors.

- [ ] **Step 2: Execute both .NET verification paths again**

Run:

~~~powershell
dotnet run --project Tests\LockstepArena.Simulation.Tests\LockstepArena.Simulation.Tests.csproj -c Release --no-restore
dotnet run --project Server\LockstepArena.Server.Verification\LockstepArena.Server.Verification.csproj -c Release --no-restore
~~~

Expected: `RESULT 15/15 passed` and `PASS Gate2GoldenVector Tick=1000 Digest=04633D1F8699DE68`.

- [ ] **Step 3: Execute Unity EditMode again and parse fresh XML**

Repeat Task 3 Steps 2–4 with the temporary XML and log deleted before the run. Expected: the named test exists, is `Passed`, and the run reports failed 0.

- [ ] **Step 4: Prove every Simulation source name has one Git path**

Run:

~~~powershell
$runtimeRoot = 'Packages/com.locksteparena.simulation/Runtime/'
$sourceNames = @(
    'BattleSimulation.cs',
    'BattleState.cs',
    'FrameData.cs',
    'InputFrame.cs',
    'PlayerState.cs',
    'SimulationConfig.cs',
    'StateDigest.cs'
)
foreach ($sourceName in $sourceNames) {
    $paths = @(git ls-files | Where-Object { $_ -like "*$sourceName" })
    if ($paths.Count -ne 1) { throw "$sourceName has $($paths.Count) tracked paths: $paths" }
    if (-not $paths[0].StartsWith($runtimeRoot, [StringComparison]::Ordinal)) {
        throw "$sourceName is outside the canonical Runtime root: $($paths[0])"
    }
    Write-Output "$sourceName -> $($paths[0])"
}
if (git ls-files Source/LockstepArena.Simulation) { throw 'Old Simulation source tree is still tracked.' }
~~~

Expected: exactly seven mappings into package Runtime and no old Source output.

- [ ] **Step 5: Prove there is no alternate source or artifact mechanism**

Run:

~~~powershell
$symlinks = git ls-files --stage Packages/com.locksteparena.simulation Server | Select-String '^120000 '
if ($symlinks) { $symlinks; throw 'Tracked symlink found.' }
$links = Get-ChildItem -LiteralPath 'Packages\com.locksteparena.simulation','Server\LockstepArena.Server.Verification' -Recurse -Force | Where-Object { $null -ne $_.LinkType }
if ($links) { $links; throw 'Filesystem link or junction found.' }
$sharedDlls = git ls-files Packages/com.locksteparena.simulation Server | Where-Object { $_ -match '\.dll$' }
if ($sharedDlls) { $sharedDlls; throw 'Tracked Shared DLL found.' }
$changedScripts = git diff --name-only a4cdf3ef3a23e587b1a6ab9a6e0c2cfc93e8abf0..HEAD | Where-Object { $_ -match '\.(ps1|sh|cmd|bat|py)$' }
if ($changedScripts) { $changedScripts; throw 'Copy or sync script entered Gate 2.' }
Write-Output 'alternate source/artifact audit: PASS'
~~~

- [ ] **Step 6: Prove Runtime has no forbidden dependency or later-gate code**

Run:

~~~powershell
$forbidden = rg -n -i "UnityEngine|UnityEditor|NUnit|TestFramework|PackageReference|ProjectReference|System\.Net|Socket|\bTcp\b|\bUdp\b|\bKcp\b|Protobuf|Projectile|Combat|Damage|Health|Prediction|Snapshot|Rollback|\bReplay\b" Packages\com.locksteparena.simulation\Runtime
if ($LASTEXITCODE -eq 0) { $forbidden; throw 'Forbidden Runtime dependency or capability found.' }
if ($LASTEXITCODE -gt 1) { throw 'Runtime dependency audit failed to run.' }
Write-Output 'Runtime dependency audit: PASS'
~~~

- [ ] **Step 7: Prove the Runtime csproj cannot pull test or external source**

Run:

~~~powershell
$projectText = Get-Content -LiteralPath 'Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj' -Raw
if ($projectText -match '<Compile|<PackageReference|<ProjectReference') { throw 'Runtime csproj contains an explicit source or dependency item.' }
dotnet list Packages\com.locksteparena.simulation\Runtime\LockstepArena.Simulation.csproj package
~~~

Expected: no explicit items and no package references.

- [ ] **Step 8: Prove Gate 2 did not touch prohibited Unity project content**

Run:

~~~powershell
$base = 'a4cdf3ef3a23e587b1a6ab9a6e0c2cfc93e8abf0'
$prohibited = git diff --name-only "$base..HEAD" -- Assets ProjectSettings Packages/manifest.json
if ($prohibited) { $prohibited; throw 'Prohibited Unity project content changed.' }
git diff --check "$base..HEAD"
~~~

Expected: no prohibited path and no whitespace error.

- [ ] **Step 9: Update the design document with actual evidence**

Change its status to implementation complete pending independent Gate 2 approval. Record:

- exact Unity XML total/passed/failed and named test;
- exact Server success line;
- Gate 1 `15/15` result;
- three 0-warning/0-error builds;
- source-path mappings;
- package-lock outcome;
- dependency, artifact, and scope audit results;
- any environment limitation without weakening the acceptance criteria.

- [ ] **Step 10: Commit the evidence-only documentation update**

~~~powershell
git add -- Docs/Architecture/GATE2_SHARED_RUNTIME_CONSUMPTION.md
git commit -m "docs: record Gate 2 shared runtime evidence"
~~~

### Task 5: Push and Stop at the Gate

**Files:** None.

**Interfaces:**

- Consumes: a clean, verified Gate 2 branch.
- Produces: a remote branch and review Handoff; no merge and no Gate 3 code.

- [ ] **Step 1: Run final repository checks**

~~~powershell
git status --short --branch
git log --oneline --decorate a4cdf3ef3a23e587b1a6ab9a6e0c2cfc93e8abf0..HEAD
git merge-base --is-ancestor a4cdf3ef3a23e587b1a6ab9a6e0c2cfc93e8abf0 HEAD
~~~

Expected: clean Gate 2 worktree and approved base is an ancestor.

- [ ] **Step 2: Inspect the normal checkout without changing it**

~~~powershell
git -C 'E:\unityproject\LockstepArena' status --short --branch
~~~

Expected: report user-owned changes as found. Do not clean, restore, stage, commit, or copy them.

- [ ] **Step 3: Push the exact branch**

~~~powershell
git push --set-upstream origin codex/gate2-shared-runtime-consumption
git ls-remote origin refs/heads/codex/gate2-shared-runtime-consumption
~~~

Expected: remote SHA equals `git rev-parse HEAD`.

- [ ] **Step 4: Submit the Gate 2 implementation Handoff and stop**

The Handoff must contain:

- branch, final commit, and approved base;
- one-source package topology and changed files;
- exact Server output;
- Unity executable, test command, XML counts, and named test result;
- Gate 1 regression count;
- build warnings/errors;
- uniqueness, dependency, link/artifact, and scope audits;
- Unity files, package dependencies, and user checkout changes not touched;
- explicit confirmation that network, Protobuf, combat, view, prediction, snapshot, rollback, Player/IL2CPP, and Gate 3 work remain absent.

Do not merge and do not begin another task until independent Gate 2 approval.
