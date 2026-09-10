using System;
using System.Net;
using System.Net.Sockets;
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
            new TestCase(nameof(TicketsAreSixteenByteRandomScopedUniqueAndSingleUse), TicketsAreSixteenByteRandomScopedUniqueAndSingleUse),
            new TestCase(nameof(BattleAttachmentAccumulatesPartialTicketWithoutOverread), BattleAttachmentAccumulatesPartialTicketWithoutOverread),
            new TestCase(nameof(InvalidExpiredReusedOrWrongScopeTicketClosesOnlyCandidate), InvalidExpiredReusedOrWrongScopeTicketClosesOnlyCandidate),
            new TestCase(nameof(ClientWaitsForAcceptanceAndBattleStartedBeforeBattleInput), ClientWaitsForAcceptanceAndBattleStartedBeforeBattleInput),
            new TestCase(nameof(PreparingBattleExplicitLeaveIsRejected), PreparingBattleExplicitLeaveIsRejected),
            new TestCase(nameof(InBattleExplicitLeaveIsRejected), InBattleExplicitLeaveIsRejected),
            new TestCase(nameof(PreparingControlLossInvalidatesTicketsAndClosesPreparedSockets), PreparingControlLossInvalidatesTicketsAndClosesPreparedSockets),
            new TestCase(nameof(AllAttachmentsCreateExactlyOneGate13SharedBattleSession), AllAttachmentsCreateExactlyOneGate13SharedBattleSession),
            new TestCase(nameof(ServerAndClientPumpsPerformOnlyFrozenBoundedWork), ServerAndClientPumpsPerformOnlyFrozenBoundedWork),
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

        private static void TicketsAreSixteenByteRandomScopedUniqueAndSingleUse()
        {
            using var fixture = new PreparedFixture();
            byte[] first = fixture.Preparation.GetTicket(new PlayerSlot(0));
            byte[] second = fixture.Preparation.GetTicket(new PlayerSlot(1));
            TestAssert.Equal(16, first.Length);
            TestAssert.Equal(16, second.Length);
            TestAssert.True(!BytesEqual(first, second));
            using TcpClient accepted = fixture.Attach(first);
            TestAssert.Equal(1, fixture.Preparation.AttachedCount);
            using TcpClient reused = fixture.ConnectAndWrite(first);
            fixture.Pump(20);
            TestAssert.Equal(1, fixture.Preparation.AttachedCount);
        }

        private static void BattleAttachmentAccumulatesPartialTicketWithoutOverread()
        {
            using var fixture = new PreparedFixture();
            byte[] ticket = fixture.Preparation.GetTicket(new PlayerSlot(0));
            using var client = new TcpClient(AddressFamily.InterNetwork);
            client.Connect(IPAddress.Loopback, fixture.Server.BattlePort);
            fixture.Server.PumpOnce();
            NetworkStream stream = client.GetStream();
            stream.Write(ticket, 0, 7);
            fixture.Server.PumpOnce();
            TestAssert.Equal(0, fixture.Preparation.AttachedCount);
            var tailWithSentinel = new byte[13];
            Array.Copy(ticket, 7, tailWithSentinel, 0, 9);
            tailWithSentinel[9] = 0xAA;
            tailWithSentinel[10] = 0xBB;
            tailWithSentinel[11] = 0xCC;
            tailWithSentinel[12] = 0xDD;
            stream.Write(tailWithSentinel, 0, tailWithSentinel.Length);
            fixture.Pump(20);
            TestAssert.Equal(1, fixture.Preparation.AttachedCount);
            TestAssert.Equal(4, fixture.Preparation.GetAttachedClient(new PlayerSlot(0)).Client.Available);
        }

        private static void InvalidExpiredReusedOrWrongScopeTicketClosesOnlyCandidate()
        {
            using var fixture = new PreparedFixture();
            using TcpClient invalid = fixture.ConnectAndWrite(new byte[16]);
            fixture.Pump(20);
            TestAssert.Equal(0, fixture.Preparation.AttachedCount);
            using TcpClient valid = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(0)));
            TestAssert.Equal(1, fixture.Preparation.AttachedCount);
            using var otherFixture = new PreparedFixture();
            using TcpClient wrongScope = fixture.ConnectAndWrite(otherFixture.Preparation.GetTicket(new PlayerSlot(0)));
            fixture.Pump(20);
            TestAssert.Equal(1, fixture.Preparation.AttachedCount);
        }

        private static void ClientWaitsForAcceptanceAndBattleStartedBeforeBattleInput()
        {
            using var fixture = new PreparedFixture();
            using TcpClient first = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(0)));
            TestAssert.Equal(DemoSessionPhase.PreparingBattle, fixture.Host.Phase);
            using TcpClient second = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(1)));
            fixture.Pump(20);
            TestAssert.Equal(DemoSessionPhase.InBattle, fixture.Host.Phase);
            TestAssert.Equal(DemoSessionPhase.InBattle, fixture.Guest.Phase);
        }

        private static void PreparingBattleExplicitLeaveIsRejected()
        {
            using var fixture = new PreparedFixture();
            TestAssert.Throws<InvalidOperationException>(() => fixture.Server.LeaveRoom(fixture.Host.SessionId));
            TestAssert.Equal(DemoRoomLifecycle.PreparingBattle, fixture.Room.Lifecycle);
        }

        private static void InBattleExplicitLeaveIsRejected()
        {
            using var fixture = new PreparedFixture();
            using TcpClient first = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(0)));
            using TcpClient second = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(1)));
            fixture.Pump(20);
            TestAssert.Throws<InvalidOperationException>(() => fixture.Server.LeaveRoom(fixture.Host.SessionId));
            TestAssert.Equal(DemoRoomLifecycle.InBattle, fixture.Room.Lifecycle);
        }

        private static void PreparingControlLossInvalidatesTicketsAndClosesPreparedSockets()
        {
            using var fixture = new PreparedFixture();
            using TcpClient first = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(0)));
            fixture.Server.CloseSession(fixture.Host.SessionId);
            TestAssert.True(fixture.Preparation.IsInvalidated);
            TestAssert.Equal(0, fixture.Server.RoomCount);
        }

        private static void AllAttachmentsCreateExactlyOneGate13SharedBattleSession()
        {
            using var fixture = new PreparedFixture();
            using TcpClient first = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(0)));
            using TcpClient second = fixture.Attach(fixture.Preparation.GetTicket(new PlayerSlot(1)));
            fixture.Pump(20);
            TestAssert.True(fixture.Preparation.SharedSession is not null);
            object instance = fixture.Preparation.SharedSession!;
            fixture.Pump(5);
            TestAssert.True(ReferenceEquals(instance, fixture.Preparation.SharedSession));
        }

        private static void ServerAndClientPumpsPerformOnlyFrozenBoundedWork()
        {
            using var fixture = new PreparedFixture();
            using TcpClient first = fixture.ConnectAndWrite(fixture.Preparation.GetTicket(new PlayerSlot(0)));
            using TcpClient second = fixture.ConnectAndWrite(fixture.Preparation.GetTicket(new PlayerSlot(1)));
            DemoServerPumpResult result = fixture.Server.PumpOnce();
            TestAssert.True(result.AcceptedBattleConnections <= 1);
            TestAssert.True(result.AcceptedControlConnections <= 1);
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

        internal sealed class PreparedFixture : IDisposable
        {
            internal PreparedFixture()
            {
                Server = SessionRoomTests.CreateServer();
                Host = Server.EnterSession("Host");
                Guest = Server.EnterSession("Guest");
                Room = Server.CreateRoom(Host.SessionId, "Room", 2);
                Server.JoinRoom(Guest.SessionId, Room.RoomId);
                Server.SetReady(Host.SessionId, true);
                Server.SetReady(Guest.SessionId, true);
                Preparation = Server.StartBattle(Host.SessionId);
            }

            internal TcpDemoServer Server { get; }
            internal DemoSession Host { get; }
            internal DemoSession Guest { get; }
            internal DemoRoom Room { get; }
            internal BattlePreparation Preparation { get; }

            internal TcpClient ConnectAndWrite(byte[] ticket)
            {
                var client = new TcpClient(AddressFamily.InterNetwork);
                client.Connect(IPAddress.Loopback, Server.BattlePort);
                client.GetStream().Write(ticket, 0, ticket.Length);
                return client;
            }

            internal TcpClient Attach(byte[] ticket)
            {
                TcpClient client = ConnectAndWrite(ticket);
                Pump(30);
                return client;
            }

            internal void Pump(int count)
            {
                for (int index = 0; index < count; index++) Server.PumpOnce();
            }

            public void Dispose()
            {
                Server.Dispose();
            }
        }
    }
}
