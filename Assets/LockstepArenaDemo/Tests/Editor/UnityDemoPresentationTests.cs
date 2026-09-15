using System;
using System.Reflection;
using NUnit.Framework;

namespace LockstepArena.Demo.Editor.Tests
{
    public sealed class UnityDemoPresentationTests
    {
        [Test]
        public void UnityFormatsFrozenGoldenSettlementAndDiagnostics()
        {
            Type snapshotType = Type.GetType("LockstepArena.Client.Demo.DemoClientSnapshot, LockstepArena.Client.Demo")!;
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
    }
}
