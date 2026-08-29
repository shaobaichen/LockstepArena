# Lockstep Arena — Gate 2 Shared Runtime Consumption

> Status: Scope and design approved in principle by the Owner; implementation is blocked pending independent planning approval
>
> Approved base: `a4cdf3ef3a23e587b1a6ab9a6e0c2cfc93e8abf0`
>
> Scope: prove that Unity EditMode and an offline .NET Server process execute one physical Shared Simulation source tree and reach one approved Golden Digest

## 1. Executive Summary

Gate 2 will move the existing Gate 1 Shared Simulation into one complete embedded Unity package at `Packages/com.locksteparena.simulation/`. Unity will compile the package's `Runtime/*.cs` through an assembly definition while .NET will compile those exact same files through a project file located in the same Runtime directory. A single pure-C# Golden Vector file will drive both a Unity EditMode test and a minimal offline .NET Server verification process.

Gate 2 is an integration proof, not a gameplay or networking gate. It changes where the already-approved Simulation source lives and proves two runtimes consume it; it does not add simulation rules.

## 2. Goal and Required Proof

The Gate succeeds only when all of the following are simultaneously true:

1. Every production Simulation `.cs` file has exactly one physical Git path.
2. That path is below `Packages/com.locksteparena.simulation/Runtime/`.
3. Unity compiles those files as assembly `LockstepArena.Simulation` through `LockstepArena.Simulation.asmdef`.
4. A .NET Server verification project references `LockstepArena.Simulation.csproj` in that same Runtime directory.
5. Unity EditMode and the Server process independently execute the same physical `Gate2GoldenVector.cs` file.
6. Both observe the approved final state and digest.
7. The complete Gate 1 test suite remains `15/15 passed` after the source move.

Compilation alone is insufficient. Both consumers must execute all 1,000 ticks.

## 3. Approved Source Topology

~~~text
Packages/com.locksteparena.simulation/
├── package.json
├── Runtime/
│   ├── LockstepArena.Simulation.asmdef
│   ├── LockstepArena.Simulation.csproj
│   ├── BattleSimulation.cs
│   ├── BattleState.cs
│   ├── FrameData.cs
│   ├── InputFrame.cs
│   ├── PlayerState.cs
│   ├── SimulationConfig.cs
│   └── StateDigest.cs
└── Tests/Editor/
    ├── LockstepArena.Simulation.Editor.Tests.asmdef
    ├── Gate2GoldenVector.cs
    └── UnityGoldenVectorTests.cs

Server/LockstepArena.Server.Verification/
├── LockstepArena.Server.Verification.csproj
└── Program.cs

Tests/LockstepArena.Simulation.Tests/
└── existing Gate 1 tests, with only their ProjectReference path changed
~~~

The existing `Source/LockstepArena.Simulation/` directory will be removed by Git renames during implementation. It must not remain as a second source tree.

Rejected alternatives:

- root source plus junction, symlink, or linked package directory;
- source-copy or synchronization scripts;
- a precompiled Shared DLL imported into Unity;
- a published or local NuGet package as the Unity source of truth.

These alternatives add a second artifact or a synchronization boundary and weaken the physical-source proof.

## 4. Embedded Package Contract

The package is a complete embedded Unity package, not merely a directory with scripts. Its `package.json` is fixed to:

~~~json
{
  "name": "com.locksteparena.simulation",
  "version": "0.1.0",
  "displayName": "Lockstep Arena Simulation",
  "description": "Unity-free deterministic simulation shared by client and server.",
  "unity": "6000.3"
}
~~~

Unity treats a package below the project `Packages/` directory as embedded. Embedded package tests are development tests and do not require a `testables` entry in the project manifest. The existing `Packages/manifest.json` dependency list stays unchanged.

References:

- [Unity local and embedded packages](https://docs.unity3d.com/cn/6000.0/Manual/upm-ui-local.html)
- [Unity package manifest](https://docs.unity3d.com/cn/6000.0/Manual/upm-manifestPkg.html)
- [Unity assembly definitions in packages](https://docs.unity3d.com/cn/6000.0/Manual/cus-asmdef.html)
- [Unity package tests](https://docs.unity3d.com/cn/current/Manual/cus-tests.html)

## 5. Runtime Assembly Contracts

### 5.1 Unity assembly definition

`Runtime/LockstepArena.Simulation.asmdef` will define:

- name and root namespace: `LockstepArena.Simulation`;
- `autoReferenced: false` so consumers opt in explicitly;
- `noEngineReferences: true` so the assembly cannot reference UnityEngine or UnityEditor;
- no assembly references;
- no precompiled references;
- no define constraints or version defines;
- no unsafe code;
- no platform inclusion or exclusion list.

The Unity test assembly explicitly references this assembly by name.

### 5.2 .NET project

`Runtime/LockstepArena.Simulation.csproj` retains the Gate 1 production settings:

- target framework `netstandard2.1`;
- C# language version 9.0;
- nullable analysis enabled;
- implicit usings disabled;
- warnings treated as errors;
- no PackageReference;
- no ProjectReference;
- no explicit Compile item outside its own directory.

Because the project file is physically inside `Runtime/`, the .NET SDK's default compile glob includes only `.cs` files in Runtime and its descendants. Sibling `Tests/`, package metadata, Unity test code, and Server code cannot enter the production Simulation assembly.

## 6. Single Golden Vector Contract

`Tests/Editor/Gate2GoldenVector.cs` is the only physical Golden Vector source. It is compiled into the Unity Editor test assembly and linked directly into the Server Verification project through one MSBuild `Compile Include` item.

It remains pure C#:

- no NUnit;
- no UnityEngine or UnityEditor;
- no Unity Test Framework API;
- no file or network I/O;
- no environment-variable or system-time reads;
- no random source;
- no serialization;
- no mutable or generated expected value.

The vector starts from `BattleState.CreateInitial()` and executes 1,000 frames. For `phase = tick % 400`:

| Phase | Player 0 movement | Player 1 movement |
|---|---|---|
| 0–99 | X +1 | X -1 |
| 100–149 | neutral | neutral |
| 150–249 | X -1 | X +1 |
| 250–324 | Z +1 | Z -1 |
| 325–399 | Z -1 | Z +1 |

Aim inputs are fixed arithmetic over the tick:

~~~text
Player 0 Aim = unchecked ushort(tick * 997 + 123)
Player 1 Aim = unchecked ushort(tick * 619 + 45678)
~~~

The approved result is immutable acceptance data:

~~~text
Tick: 1000
Player 0: X 0, Z -3000, Aim 13086
Player 1: X 0, Z  3000, Aim 8699
Digest: 0x04633D1F8699DE68
~~~

The expected fields and digest are literals. They must never be written or updated from the actual run.

## 7. Unity Consumer

`LockstepArena.Simulation.Editor.Tests.asmdef` will:

- reference `LockstepArena.Simulation`;
- include only platform `Editor`;
- identify itself as a test assembly through `TestAssemblies`;
- disable auto-reference;
- contain no precompiled reference or define constraint.

`UnityGoldenVectorTests.cs` owns all NUnit assertions. It calls `Gate2GoldenVector.Run()` and independently asserts every final field, the approved digest, and that `typeof(BattleSimulation).Assembly.GetName().Name` is `LockstepArena.Simulation`.

This proves execution in Unity's Editor scripting runtime. It does not claim Player, Mono build, or IL2CPP cross-platform validation.

## 8. .NET Server Consumer

`LockstepArena.Server.Verification` is a one-shot `net8.0` Console executable. Its project contains:

- one ProjectReference to the package Runtime Simulation csproj;
- one Compile Include linking the package's physical `Gate2GoldenVector.cs` file;
- no PackageReference;
- no networking, hosting, dependency injection, configuration, logging framework, or lifecycle abstraction.

`Program.cs` executes the vector, compares every final field and the digest with literals, prints expected and actual data on mismatch, and returns a non-zero exit code. Success prints the tick and digest and returns zero.

This executable is verification infrastructure only. It is not the foundation of the future formal Server host.

## 9. Data Flow

~~~text
one Runtime/*.cs source tree
          │
          ├── Unity asmdef compilation ── Unity EditMode NUnit assertion
          │
          └── .NET csproj compilation ── Server Verification process assertion

one Gate2GoldenVector.cs
          │
          ├── compiled by Unity Editor test asmdef
          └── compiled by Server Verification through Compile Include
~~~

There is one Simulation execution path: `BattleSimulation.Step(FrameData)`. No adapter duplicates or reimplements simulation rules.

## 10. Verification Matrix

| Verification | Required result |
|---|---|
| Simulation Release build | 0 warnings, 0 errors |
| Gate 1 regression executable | `RESULT 15/15 passed` |
| Server Verification | exact final fields, digest `04633D1F8699DE68`, exit 0 |
| Unity package import and compile | no compiler error |
| Unity EditMode test | target assembly found, expected test executed and passed |
| Unity NUnit XML | at least one expected test, total failed 0 |
| Runtime dependency scan | no UnityEngine, UnityEditor, NUnit, network, Protobuf, or later-gate term |
| Git path scan | one path for every production Simulation `.cs` |
| Link/artifact scan | no symlink, junction, Shared DLL, copy script, or sync script |
| Project scope scan | Assets, ProjectSettings, and Packages/manifest.json unchanged |

Unity is run against the Gate 2 worktree using `E:\unityhub\unity6.3\Editor\Unity.exe` with `-batchmode -runTests -testPlatform EditMode`, the target assembly filter, a temporary XML result path, and a temporary Editor log path. The XML result must be parsed; a launched Unity process or exit code alone is not proof that the expected test ran.

If an Editor instance lock, license problem, or environmental failure prevents Unity from testing the Gate 2 worktree, work stops and the blocker is reported. The normal checkout must not be substituted.

## 11. Explicitly Out of Scope

- TCP, UDP, KCP, sockets, transport, latency, or packet loss;
- Protobuf, schemas, serialization adapters, or generated types;
- login, session, lobby, room, ready, or battle lifecycle;
- projectile, collision, damage, health, death, score, or result;
- prediction, dirty frames, snapshot, rollback, catch-up, or replay features;
- Unity Transform, GameObject, scene, view adapter, interpolation, or rendering;
- formal Server host, hosting framework, DI, configuration framework, or service lifecycle;
- Player/IL2CPP validation, build pipeline, CI workflow, NuGet publication, or package registry publication;
- general fixed-point framework or changes to Gate 1 movement/digest rules.

Gate 1's observation that public state constructors permit arbitrary integers remains deferred until a future state-restore boundary exists. Gate 2 does not add restore or state-sync validation.

## 12. Git and Isolation Contract

- Planning branch: `codex/gate2-shared-runtime-consumption`.
- Worktree: `.worktrees/gate2-shared-runtime-consumption`.
- Exact base: `a4cdf3ef3a23e587b1a6ab9a6e0c2cfc93e8abf0`.
- Planning commit contains only this Scope/Design and its Implementation Plan.
- No Gate 2 source move or implementation begins until independent planning approval.

The normal checkout's existing uncommitted files are user-owned and excluded from this worktree:

- `Assets/Settings/Mobile_RPAsset.asset`;
- `ProjectSettings/ShaderGraphSettings.asset`.

They must not be modified, cleaned, committed, or copied into Gate 2.

## 13. Gate 2 Exit Criteria

Implementation may later be submitted for Gate 2 approval only when:

1. all rows in the verification matrix have fresh passing evidence;
2. the implementation branch is pushed and its remote ref matches local HEAD;
3. both the Gate 2 worktree and normal checkout have been checked without touching user changes;
4. a Handoff names the exact branch, commit, base, test commands, XML counts, Server output, uniqueness audit, scope audit, and known limitations;
5. work stops before any Gate 3 capability begins.
