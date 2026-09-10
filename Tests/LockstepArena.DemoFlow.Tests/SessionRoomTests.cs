using System;
using System.Reflection;
using LockstepArena.Protocol.Wire;
using LockstepArena.Server.DemoHost;
using LockstepArena.Simulation;

namespace LockstepArena.DemoFlow.Tests
{
    internal static class SessionRoomTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(NicknameValidationUsesTrimControlUtf8AndOrdinalRules), NicknameValidationUsesTrimControlUtf8AndOrdinalRules),
            new TestCase(nameof(RoomNameValidationUsesTrimControlAndUtf8Rules), RoomNameValidationUsesTrimControlAndUtf8Rules),
            new TestCase(nameof(SessionIdsStartAtOneIncreaseAndAreNeverReused), SessionIdsStartAtOneIncreaseAndAreNeverReused),
            new TestCase(nameof(SessionIdExhaustionRejectsWithoutWrapOrMutation), SessionIdExhaustionRejectsWithoutWrapOrMutation),
            new TestCase(nameof(DuplicateActiveNicknameRejectsWithoutAllocatingSession), DuplicateActiveNicknameRejectsWithoutAllocatingSession),
            new TestCase(nameof(CreateRoomAutoJoinsHostAtJoinOrdinalZero), CreateRoomAutoJoinsHostAtJoinOrdinalZero),
            new TestCase(nameof(CreateRoomCapacityUsesConfiguredSpawnBoundNotOneVsOne), CreateRoomCapacityUsesConfiguredSpawnBoundNotOneVsOne),
            new TestCase(nameof(RoomIdExhaustionRejectsWithoutRoomMutation), RoomIdExhaustionRejectsWithoutRoomMutation),
            new TestCase(nameof(RoomListUsesStableCreationOrderAndCompleteSummaries), RoomListUsesStableCreationOrderAndCompleteSummaries),
            new TestCase(nameof(JoinAppendsStableJoinOrderAndBroadcastsSnapshot), JoinAppendsStableJoinOrderAndBroadcastsSnapshot),
            new TestCase(nameof(JoinRejectsMissingClosedFullOrAlreadyJoinedRoom), JoinRejectsMissingClosedFullOrAlreadyJoinedRoom),
            new TestCase(nameof(ReadyAndUnreadyBroadcastAtomicRoomSnapshots), ReadyAndUnreadyBroadcastAtomicRoomSnapshots),
            new TestCase(nameof(OpenNonHostLeaveRemovesOnlyThatParticipant), OpenNonHostLeaveRemovesOnlyThatParticipant),
            new TestCase(nameof(OpenHostLossClosesRoomAndReturnsSurvivorsToLobby), OpenHostLossClosesRoomAndReturnsSurvivorsToLobby),
        };

        private static void NicknameValidationUsesTrimControlUtf8AndOrdinalRules()
        {
            using var server = CreateServer();
            DemoSession alpha = server.EnterSession("  Alpha  ");
            TestAssert.Equal("Alpha", alpha.Nickname);
            DemoSession lower = server.EnterSession("alpha");
            TestAssert.Equal((ulong)2, lower.SessionId);
            TestAssert.Throws<ArgumentException>(() => server.EnterSession("bad\nname"));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => server.EnterSession(new string('\u754c', 11)));
        }

        private static void RoomNameValidationUsesTrimControlAndUtf8Rules()
        {
            using var server = CreateServer();
            DemoSession host = server.EnterSession("Host");
            DemoRoom room = server.CreateRoom(host.SessionId, "  Golden Room  ", 2);
            TestAssert.Equal("Golden Room", room.RoomName);
            TestAssert.Throws<ArgumentException>(() => server.CreateRoom(server.EnterSession("B").SessionId, "bad\troom", 2));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => server.CreateRoom(server.EnterSession("C").SessionId, new string('\u754c', 17), 2));
        }

        private static void SessionIdsStartAtOneIncreaseAndAreNeverReused()
        {
            using var server = CreateServer();
            DemoSession first = server.EnterSession("A");
            DemoSession second = server.EnterSession("B");
            server.CloseSession(first.SessionId);
            DemoSession third = server.EnterSession("C");
            TestAssert.Equal((ulong)1, first.SessionId);
            TestAssert.Equal((ulong)2, second.SessionId);
            TestAssert.Equal((ulong)3, third.SessionId);
        }

        private static void SessionIdExhaustionRejectsWithoutWrapOrMutation()
        {
            using var server = CreateServer(maxSessions: 3, maxRoomCapacity: 3);
            SetCounter(server, "_nextSessionId", ulong.MaxValue);
            DemoSession last = server.EnterSession("Last");
            TestAssert.Equal(ulong.MaxValue, last.SessionId);
            int count = server.SessionCount;
            TestAssert.Throws<InvalidOperationException>(() => server.EnterSession("Never"));
            TestAssert.Equal(count, server.SessionCount);
        }

        private static void DuplicateActiveNicknameRejectsWithoutAllocatingSession()
        {
            using var server = CreateServer();
            server.EnterSession("Alpha");
            TestAssert.Throws<InvalidOperationException>(() => server.EnterSession("Alpha"));
            TestAssert.Equal((ulong)2, server.EnterSession("Bravo").SessionId);
        }

        private static void CreateRoomAutoJoinsHostAtJoinOrdinalZero()
        {
            using var server = CreateServer();
            DemoSession host = server.EnterSession("Host");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            TestAssert.Equal(host.SessionId, room.HostSessionId);
            TestAssert.Equal(1, room.ParticipantCount);
            TestAssert.Equal(host.SessionId, room.GetParticipant(0).SessionId);
            TestAssert.Equal(0, room.GetParticipant(0).JoinOrdinal);
            TestAssert.Equal(DemoSessionPhase.InRoom, host.Phase);
        }

        private static void CreateRoomCapacityUsesConfiguredSpawnBoundNotOneVsOne()
        {
            TestAssert.Throws<ArgumentOutOfRangeException>(() => CreateOptions(maxSessions: 3, maxRoomCapacity: 4));
            using (var equal = new TcpDemoServer(CreateOptions(maxSessions: 4, maxRoomCapacity: 4)))
            {
                DemoSession host = equal.EnterSession("Host");
                DemoRoom room = equal.CreateRoom(host.SessionId, "Four", 4);
                TestAssert.Equal(4, room.Capacity);
            }

            TestAssert.Throws<ArgumentOutOfRangeException>(() => new DemoServerOptions(0, 0, 2, 2, 2, SpawnStates(2), 2, 8, 0, 4, 1024, 2048, 32, 3, 8, 4, 64, 1024, 32, 3, 8));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new DemoServerOptions(0, 0, 4, 2, 4, SpawnStates(3), 2, 8, 8, 4, 1024, 2048, 32, 3, 8, 4, 64, 1024, 32, 3, 8));
        }

        private static void RoomIdExhaustionRejectsWithoutRoomMutation()
        {
            using var server = CreateServer(maxRooms: 3);
            DemoSession first = server.EnterSession("A");
            DemoSession second = server.EnterSession("B");
            SetCounter(server, "_nextRoomId", ulong.MaxValue);
            TestAssert.Equal(ulong.MaxValue, server.CreateRoom(first.SessionId, "Last", 2).RoomId);
            int count = server.RoomCount;
            TestAssert.Throws<InvalidOperationException>(() => server.CreateRoom(second.SessionId, "Never", 2));
            TestAssert.Equal(count, server.RoomCount);
        }

        private static void RoomListUsesStableCreationOrderAndCompleteSummaries()
        {
            using var server = CreateServer(maxSessions: 4, maxRooms: 3, maxRoomCapacity: 2);
            DemoRoom first = server.CreateRoom(server.EnterSession("A").SessionId, "First", 2);
            DemoRoom second = server.CreateRoom(server.EnterSession("B").SessionId, "Second", 2);
            RoomSummaryMessage[] summaries = server.GetRoomSummaries();
            TestAssert.Equal(2, summaries.Length);
            TestAssert.Equal(first.RoomId, summaries[0].RoomId);
            TestAssert.Equal("A", summaries[0].HostNickname);
            TestAssert.Equal((uint)1, summaries[0].ParticipantCount);
            TestAssert.Equal(second.RoomId, summaries[1].RoomId);
        }

        private static void JoinAppendsStableJoinOrderAndBroadcastsSnapshot()
        {
            using var server = CreateServer();
            DemoSession host = server.EnterSession("Host");
            DemoSession guest = server.EnterSession("Guest");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            host.ClearEvents();
            guest.ClearEvents();
            server.JoinRoom(guest.SessionId, room.RoomId);
            TestAssert.Equal(guest.SessionId, room.GetParticipant(1).SessionId);
            TestAssert.Equal(1, room.GetParticipant(1).JoinOrdinal);
            TestAssert.Equal(ServerControlEventMessage.EventOneofCase.RoomSnapshot, host.LastEvent.EventCase);
            TestAssert.Equal(ServerControlEventMessage.EventOneofCase.RoomSnapshot, guest.LastEvent.EventCase);
        }

        private static void JoinRejectsMissingClosedFullOrAlreadyJoinedRoom()
        {
            using var server = CreateServer(maxSessions: 4);
            DemoSession host = server.EnterSession("Host");
            DemoSession guest = server.EnterSession("Guest");
            DemoSession extra = server.EnterSession("Extra");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            TestAssert.Throws<InvalidOperationException>(() => server.JoinRoom(guest.SessionId, 999));
            server.JoinRoom(guest.SessionId, room.RoomId);
            TestAssert.Throws<InvalidOperationException>(() => server.JoinRoom(guest.SessionId, room.RoomId));
            TestAssert.Throws<InvalidOperationException>(() => server.JoinRoom(extra.SessionId, room.RoomId));
            server.CloseSession(host.SessionId);
            TestAssert.Throws<InvalidOperationException>(() => server.JoinRoom(extra.SessionId, room.RoomId));
        }

        private static void ReadyAndUnreadyBroadcastAtomicRoomSnapshots()
        {
            using var server = CreateServer();
            DemoSession host = server.EnterSession("Host");
            DemoSession guest = server.EnterSession("Guest");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            server.JoinRoom(guest.SessionId, room.RoomId);
            host.ClearEvents();
            guest.ClearEvents();
            server.SetReady(guest.SessionId, true);
            TestAssert.True(room.GetParticipant(1).IsReady);
            TestAssert.True(host.LastEvent.RoomSnapshot.Participants[1].IsReady);
            server.SetReady(guest.SessionId, false);
            TestAssert.True(!room.GetParticipant(1).IsReady);
            TestAssert.True(!host.LastEvent.RoomSnapshot.Participants[1].IsReady);
        }

        private static void OpenNonHostLeaveRemovesOnlyThatParticipant()
        {
            using var server = CreateServer();
            DemoSession host = server.EnterSession("Host");
            DemoSession guest = server.EnterSession("Guest");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            server.JoinRoom(guest.SessionId, room.RoomId);
            guest.ClearEvents();
            server.LeaveRoom(guest.SessionId);
            TestAssert.Equal(1, room.ParticipantCount);
            TestAssert.Equal(DemoSessionPhase.Lobby, guest.Phase);
            TestAssert.Equal(ServerControlEventMessage.EventOneofCase.LobbyEntered, guest.LastEvent.EventCase);
        }

        private static void OpenHostLossClosesRoomAndReturnsSurvivorsToLobby()
        {
            using var server = CreateServer();
            DemoSession host = server.EnterSession("Host");
            DemoSession guest = server.EnterSession("Guest");
            DemoRoom room = server.CreateRoom(host.SessionId, "Room", 2);
            server.JoinRoom(guest.SessionId, room.RoomId);
            guest.ClearEvents();
            server.CloseSession(host.SessionId);
            TestAssert.Equal(0, server.RoomCount);
            TestAssert.Equal(DemoSessionPhase.Lobby, guest.Phase);
            TestAssert.Equal(ServerControlEventMessage.EventOneofCase.LobbyEntered, guest.LastEvent.EventCase);
        }

        internal static TcpDemoServer CreateServer(int maxSessions = 4, int maxRooms = 4, int maxRoomCapacity = 4)
        {
            return new TcpDemoServer(CreateOptions(maxSessions, maxRooms, maxRoomCapacity));
        }

        internal static DemoServerOptions CreateOptions(int maxSessions = 4, int maxRooms = 4, int maxRoomCapacity = 4)
        {
            return new DemoServerOptions(0, 0, maxSessions, maxRooms, maxRoomCapacity, SpawnStates(maxRoomCapacity), 2, 8, 8, 4, 1024, 2048, 32, 3, 8, 4, 64, 1024, 32, 3, 8);
        }

        private static PlayerState[] SpawnStates(int count)
        {
            var states = new PlayerState[count];
            for (int index = 0; index < states.Length; index++)
            {
                states[index] = new PlayerState(-300 + (index * 200), 0, (ushort)(1000 + index));
            }

            return states;
        }

        private static void SetCounter(TcpDemoServer server, string name, ulong value)
        {
            FieldInfo field = typeof(TcpDemoServer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException($"Missing counter {name}.");
            field.SetValue(server, value);
        }
    }
}
