using System.Net.Sockets;
using LockstepArena.Server.DemoHost;
using LockstepArena.Simulation;

namespace LockstepArena.DemoFlow.Tests
{
    internal static class SettlementLifecycleTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(BattleFailureAbortsRoomAndNotifiesSurvivingControls), BattleFailureAbortsRoomAndNotifiesSurvivingControls),
        };

        private static void BattleFailureAbortsRoomAndNotifiesSurvivingControls()
        {
            using var fixture = new BattlePreparationTests.PreparedFixture();
            using TcpClient first = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(0)));
            using TcpClient second = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(1)));
            first.Client.Shutdown(SocketShutdown.Both);
            first.Dispose();
            for (int index = 0; index < 100 && fixture.Server.RoomCount > 0; index++) fixture.Server.PumpOnce();
            TestAssert.Equal(0, fixture.Server.RoomCount);
            TestAssert.True(fixture.Preparation.IsInvalidated);
        }
    }
}
