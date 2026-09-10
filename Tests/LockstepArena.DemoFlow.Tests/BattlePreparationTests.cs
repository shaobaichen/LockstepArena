using System;
using LockstepArena.Server.DemoHost;
using LockstepArena.Simulation;

namespace LockstepArena.DemoFlow.Tests
{
    internal static class BattlePreparationTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(StartRejectsNonHostWithoutMutation), StartRejectsNonHostWithoutMutation),
            new TestCase(nameof(StartRejectsNonFullRoomWithoutMutation), StartRejectsNonFullRoomWithoutMutation),
            new TestCase(nameof(StartRejectsAnyUnreadyParticipantWithoutMutation), StartRejectsAnyUnreadyParticipantWithoutMutation),
            new TestCase(nameof(StartAtomicallyFreezesRosterBootstrapTicketsAndEvents), StartAtomicallyFreezesRosterBootstrapTicketsAndEvents),
            new TestCase(nameof(RosterSlotsFollowJoinOrderRatherThanPlayerId), RosterSlotsFollowJoinOrderRatherThanPlayerId),
            new TestCase(nameof(BattleIdExhaustionRejectsWithoutPreparationMutation), BattleIdExhaustionRejectsWithoutPreparationMutation),
        };

        private static void StartRejectsNonHostWithoutMutation()
        {
            using var server = FullRoom(out DemoSession host, out DemoSession guest, out DemoRoom room);
            server.SetReady(host.SessionId, true);
            server.SetReady(guest.SessionId, true);
            TestAssert.Throws<InvalidOperationException>(() => server.StartBattle(guest.SessionId));
            TestAssert.Equal(DemoRoomLifecycle.Open, room.Lifecycle);
        }

        private static void StartRejectsNonFullRoomWithoutMutation()
        {
            using var server = SessionRoomTests.CreateServer();
            DemoSession host = server.EnterSession("Host");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            server.SetReady(host.SessionId, true);
            TestAssert.Throws<InvalidOperationException>(() => server.StartBattle(host.SessionId));
            TestAssert.Equal(DemoRoomLifecycle.Open, room.Lifecycle);
        }

        private static void StartRejectsAnyUnreadyParticipantWithoutMutation()
        {
            using var server = FullRoom(out DemoSession host, out DemoSession guest, out DemoRoom room);
            server.SetReady(host.SessionId, true);
            TestAssert.Throws<InvalidOperationException>(() => server.StartBattle(host.SessionId));
            TestAssert.Equal(DemoRoomLifecycle.Open, room.Lifecycle);
            TestAssert.True(!guest.IsReady);
        }

        private static void StartAtomicallyFreezesRosterBootstrapTicketsAndEvents()
        {
            using var server = FullRoom(out DemoSession host, out DemoSession guest, out DemoRoom room);
            server.SetReady(host.SessionId, true);
            server.SetReady(guest.SessionId, true);
            host.ClearEvents();
            guest.ClearEvents();
            BattlePreparation preparation = server.StartBattle(host.SessionId);
            TestAssert.Equal(DemoRoomLifecycle.PreparingBattle, room.Lifecycle);
            TestAssert.Equal((ulong)1, preparation.BattleId);
            TestAssert.Equal(2, preparation.ParticipantCount);
            TestAssert.Equal(16, preparation.GetTicket(new PlayerSlot(0)).Length);
            TestAssert.Equal(16, preparation.GetTicket(new PlayerSlot(1)).Length);
            TestAssert.True(!BytesEqual(preparation.GetTicket(new PlayerSlot(0)), preparation.GetTicket(new PlayerSlot(1))));
            TestAssert.Equal(host.SessionId, preparation.InitialState.Roster.GetPlayerId(new PlayerSlot(0)).Value);
            TestAssert.Equal(4U, preparation.FinalStateTick);
            TestAssert.Equal(Protocol.Wire.ServerControlEventMessage.EventOneofCase.BattlePreparing, host.LastEvent.EventCase);
            TestAssert.Equal(Protocol.Wire.ServerControlEventMessage.EventOneofCase.BattlePreparing, guest.LastEvent.EventCase);
        }

        private static void RosterSlotsFollowJoinOrderRatherThanPlayerId()
        {
            using var server = SessionRoomTests.CreateServer();
            DemoSession earlier = server.EnterSession("Bravo");
            DemoSession laterHost = server.EnterSession("Alpha");
            DemoRoom room = server.CreateRoom(laterHost.SessionId, "Room", 2);
            server.JoinRoom(earlier.SessionId, room.RoomId);
            server.SetReady(laterHost.SessionId, true);
            server.SetReady(earlier.SessionId, true);
            BattlePreparation preparation = server.StartBattle(laterHost.SessionId);
            TestAssert.Equal(laterHost.SessionId, preparation.InitialState.Roster.GetPlayerId(new PlayerSlot(0)).Value);
            TestAssert.Equal(earlier.SessionId, preparation.InitialState.Roster.GetPlayerId(new PlayerSlot(1)).Value);
        }

        private static void BattleIdExhaustionRejectsWithoutPreparationMutation()
        {
            using var server = FullRoom(out DemoSession host, out DemoSession guest, out DemoRoom room);
            server.SetReady(host.SessionId, true);
            server.SetReady(guest.SessionId, true);
            typeof(TcpDemoServer).GetField("_nextBattleId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(server, 0UL);
            TestAssert.Throws<InvalidOperationException>(() => server.StartBattle(host.SessionId));
            TestAssert.Equal(DemoRoomLifecycle.Open, room.Lifecycle);
        }

        private static TcpDemoServer FullRoom(out DemoSession host, out DemoSession guest, out DemoRoom room)
        {
            TcpDemoServer server = SessionRoomTests.CreateServer();
            host = server.EnterSession("Host");
            guest = server.EnterSession("Guest");
            room = server.CreateRoom(host.SessionId, "Room", 2);
            server.JoinRoom(guest.SessionId, room.RoomId);
            return server;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
            return true;
        }
    }
}
