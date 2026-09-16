#nullable enable

using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace LockstepArena.Demo.Editor.Tests
{
    public sealed class UnityDemoPresentationTests
    {
        [Test]
        public void UnityFormatsFrozenGoldenSettlementAndDiagnostics()
        {
            Type snapshotType = Type.GetType("LockstepArena.Client.Demo.DemoClientSnapshot, LockstepArena.Client.Demo")!;
            Type roomType = Type.GetType("LockstepArena.Client.Demo.DemoRoomSummarySnapshot, LockstepArena.Client.Demo")!;
            Type participantType = Type.GetType("LockstepArena.Client.Demo.DemoRoomParticipantSnapshot, LockstepArena.Client.Demo")!;
            Type lifecycleType = Type.GetType("LockstepArena.Protocol.Wire.RoomLifecycleMessage, LockstepArena.Protocol")!;
            ConstructorInfo constructor = snapshotType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)[0];
            object snapshot = constructor.Invoke(new object[]
            {
                Enum.Parse(Type.GetType("LockstepArena.Client.Demo.DemoClientPhase, LockstepArena.Client.Demo")!, "Settlement"),
                2UL, "Alpha", 1UL, "Golden Room",
                "Room1 Golden Room host=Alpha 2/2 RoomLifecycleOpen",
                "0:Alpha PlayerId2 host ready | 1:Bravo PlayerId1 ready",
                1UL, "Slot0/PlayerId2 | Slot1/PlayerId1", "NOT_HOST",
                4U, 4U, 4U, 4U, 0, 0, 4, true, 4,
                0xD8E54FF828A4C670UL, 0xD8E54FF828A4C670UL, true,
                Enum.Parse(Type.GetType("LockstepArena.Protocol.Wire.BattleSettlementReasonMessage, LockstepArena.Protocol")!, "BattleSettlementReasonMatchCompleted"),
                2UL, 2U, 1U, "Match completed.",
                Array.CreateInstance(roomType, 0), Array.CreateInstance(participantType, 0),
                0UL, 0U, Enum.Parse(lifecycleType, "RoomLifecycleUnspecified"),
            });
            Type controllerType = Type.GetType("LockstepArena.Demo.LockstepArenaDemoController, LockstepArena.Demo")!;
            MethodInfo formatter = controllerType.GetMethod("FormatDiagnostics", BindingFlags.Public | BindingFlags.Static)!;
            string actual = (string)formatter.Invoke(null, new[] { snapshot })!;
            Assert.That(actual, Is.EqualTo(
                "Phase=Settlement Session=2 Room=1 Battle=1 " +
                "Rooms=[Room1 Golden Room host=Alpha 2/2 RoomLifecycleOpen] " +
                "Participants=[0:Alpha PlayerId2 host ready | 1:Bravo PlayerId1 ready] " +
                "Roster=[Slot0/PlayerId2 | Slot1/PlayerId1] Rejection=NOT_HOST " +
                "ServerTick=4 NextPublishTick=4 " +
                "AuthorityTick=4 PredictedTick=4 PendingPredictions=0 PendingAuthority=0 Replay=4 " +
                "LatestDirty=True CumulativeDirty=4 AuthorityDigest=D8E54FF828A4C670 " +
                "PredictedDigest=D8E54FF828A4C670 SettlementVerified=True " +
                "Settlement=BattleSettlementReasonMatchCompleted Winner=2 Score=2-1"));
        }

        [Test]
        public void UnityLoadsGameplayRootAndPresenterTypes()
        {
            Assert.That(Type.GetType("LockstepArena.Demo.LockstepArenaDemoController, LockstepArena.Demo"), Is.Not.Null);
            Assert.That(Type.GetType("LockstepArena.Demo.BattlePresenter, LockstepArena.Demo"), Is.Not.Null);
        }

        [Test]
        public void UnityGameplayPredictionCapacityMatchesServerFutureWindow()
        {
            Type controllerType = Type.GetType("LockstepArena.Demo.LockstepArenaDemoController, LockstepArena.Demo")!;
            FieldInfo capacity = controllerType.GetField(
                "ClientPredictionCapacity",
                BindingFlags.NonPublic | BindingFlags.Static)!;

            Assert.That(capacity, Is.Not.Null);
            Assert.That(capacity.GetRawConstantValue(), Is.EqualTo(8));
        }

        [Test]
        public void UnityLoadsSupportedDemoUnlitShader()
        {
            Shader shader = Resources.Load<Shader>("LockstepArenaDemoUnlit");

            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
        }

        [Test]
        public void UnityDemoContinuesPumpingWhenWindowLosesFocus()
        {
            bool previous = Application.runInBackground;
            var root = new GameObject("Background Pump Test");
            try
            {
                LockstepArenaDemoController controller = root.AddComponent<LockstepArenaDemoController>();
                Application.runInBackground = false;
                MethodInfo awake = typeof(LockstepArenaDemoController).GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                try
                {
                    awake.Invoke(controller, null);
                }
                catch (TargetInvocationException exception) when (
                    exception.InnerException is InvalidOperationException invalidOperation &&
                    invalidOperation.Message.Contains("DontDestroyOnLoad"))
                {
                    // EditMode cannot execute the final player-only persistence call.
                }

                Assert.That(Application.runInBackground, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                Application.runInBackground = previous;
            }
        }

        [Test]
        public void UnityPredictionInputUsesSimulationTickCadence()
        {
            MethodInfo pacer = typeof(LockstepArenaDemoController).GetMethod(
                "ConsumePredictionTick",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            Assert.That(pacer, Is.Not.Null);
            var root = new GameObject("Prediction Pacer Test");
            try
            {
                LockstepArenaDemoController controller = root.AddComponent<LockstepArenaDemoController>();
                Assert.That((bool)pacer.Invoke(controller, new object[] { 0.01d })!, Is.False);
                Assert.That((bool)pacer.Invoke(controller, new object[] { 0.01d })!, Is.False);
                Assert.That((bool)pacer.Invoke(controller, new object[] { 0.01d })!, Is.False);
                Assert.That((bool)pacer.Invoke(controller, new object[] { 0.004d })!, Is.True);
                Assert.That((bool)pacer.Invoke(controller, new object[] { 0d })!, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UnitySelectsPreferredPrivateLanAddress()
        {
            string? address = LanAddressUtility.SelectPreferredPrivateIpv4(new[]
            {
                IPAddress.Parse("127.0.0.1"),
                IPAddress.Parse("169.254.1.4"),
                IPAddress.Parse("172.20.1.4"),
                IPAddress.Parse("10.1.2.3"),
                IPAddress.Parse("192.168.1.23"),
            });

            Assert.That(address, Is.EqualTo("192.168.1.23"));
        }

        [Test]
        public void UnityResolvesEditorAndPlayerDemoHostLayouts()
        {
            string editor = LanServerPathResolver.ResolveFromApplicationDataPath(
                Path.Combine("E:\\", "repo", "Assets"), true);
            string player = LanServerPathResolver.ResolveFromApplicationDataPath(
                Path.Combine("C:\\", "Games", "LockstepArena_Data"), false);

            Assert.That(editor, Does.EndWith(Path.Combine(
                "Server", "LockstepArena.Server.DemoHost", "bin", "Release", "net8.0",
                "LockstepArena.Server.DemoHost.exe")));
            Assert.That(player, Is.EqualTo(Path.Combine(
                "C:\\", "Games", "Server", "LockstepArena.Server.DemoHost.exe")));
        }

        [Test]
        public void UnityDefinesRecoverableShellStagesAndSafeEmptyLauncher()
        {
            Assert.That(Enum.IsDefined(typeof(ShellConnectionStage), "StartingServer"), Is.True);
            Assert.That(Enum.IsDefined(typeof(ShellConnectionStage), "CreatingRoom"), Is.True);
            Assert.That(Enum.IsDefined(typeof(ShellConnectionStage), "Failed"), Is.True);

            using var launcher = new LanServerLauncher();
            launcher.StopOwned();
            Assert.That(launcher.OwnsServer, Is.False);
        }

        [Test]
        public void UnityExposesThePlayerFacingGameShellApi()
        {
            Type controller = typeof(LockstepArenaDemoController);

            Assert.That(controller.GetProperty("ClientSnapshot")?.GetMethod?.IsPublic, Is.True);
            Assert.That(controller.GetProperty("ShellStage")?.GetMethod?.IsPublic, Is.True);
            Assert.That(controller.GetProperty("UserFacingError")?.GetMethod?.IsPublic, Is.True);
            Assert.That(controller.GetProperty("HostedLanAddress")?.GetMethod?.IsPublic, Is.True);
            Assert.That(controller.GetProperty("OwnsLocalServer")?.GetMethod?.IsPublic, Is.True);

            AssertPublicMethod(controller, "BeginHostLanAsync", typeof(Task), typeof(string), typeof(string));
            AssertPublicMethod(controller, "BeginJoinLan", typeof(void), typeof(string), typeof(string));
            AssertPublicMethod(controller, "RefreshRooms", typeof(void));
            AssertPublicMethod(controller, "CreateRoom", typeof(void), typeof(string));
            AssertPublicMethod(controller, "JoinRoom", typeof(void), typeof(ulong));
            AssertPublicMethod(controller, "LeaveRoom", typeof(void));
            AssertPublicMethod(controller, "SetReady", typeof(void), typeof(bool));
            AssertPublicMethod(controller, "StartBattle", typeof(void));
            AssertPublicMethod(controller, "DisconnectToMainMenu", typeof(void));
        }

        [Test]
        public void UnityDisconnectToMainMenuIsSafeWithoutAClient()
        {
            var root = new GameObject("Safe Disconnect Test");
            try
            {
                LockstepArenaDemoController controller = root.AddComponent<LockstepArenaDemoController>();

                Assert.DoesNotThrow(controller.DisconnectToMainMenu);
                Assert.That(controller.ShellStage, Is.EqualTo(ShellConnectionStage.Idle));
                Assert.That(controller.UserFacingError, Is.Empty);
                Assert.That(controller.OwnsLocalServer, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AssertPublicMethod(
            Type type,
            string name,
            Type returnType,
            params Type[] parameterTypes)
        {
            MethodInfo? method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null);
            Assert.That(method, Is.Not.Null, $"Missing public method {name}.");
            Assert.That(method!.ReturnType, Is.EqualTo(returnType), $"Unexpected return type for {name}.");
        }
    }
}
