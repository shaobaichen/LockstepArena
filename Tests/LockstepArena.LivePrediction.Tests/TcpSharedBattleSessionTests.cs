using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using LockstepArena.Server.LiveTcp;
using LockstepArena.Simulation;

namespace LockstepArena.LivePrediction.Tests
{
    internal static class TcpSharedBattleSessionTests
    {
        private const int MaxPayloadLength = 4096;

        public static readonly TestCase[] ConstructionTests =
        {
            new TestCase(nameof(ParticipantBindingRejectsNullTcpClient), ParticipantBindingRejectsNullTcpClient),
            new TestCase(nameof(SessionRejectsNullInitialState), SessionRejectsNullInitialState),
            new TestCase(nameof(SessionRejectsNullParticipantArray), SessionRejectsNullParticipantArray),
            new TestCase(nameof(SessionRejectsWrongParticipantCount), SessionRejectsWrongParticipantCount),
            new TestCase(nameof(SessionRejectsNullParticipantEntry), SessionRejectsNullParticipantEntry),
            new TestCase(nameof(SessionRejectsDuplicateTcpClient), SessionRejectsDuplicateTcpClient),
            new TestCase(nameof(SessionRejectsDuplicatePlayerId), SessionRejectsDuplicatePlayerId),
            new TestCase(nameof(SessionRejectsDuplicatePlayerSlot), SessionRejectsDuplicatePlayerSlot),
            new TestCase(nameof(SessionRejectsOutOfRosterOrMismatchedBinding), SessionRejectsOutOfRosterOrMismatchedBinding),
            new TestCase(nameof(SessionRejectsDisconnectedParticipant), SessionRejectsDisconnectedParticipant),
            new TestCase(nameof(SessionRejectsNonIpv4Participant), SessionRejectsNonIpv4Participant),
            new TestCase(nameof(ConstructionFailureLeavesCallerConnectionsUsable), ConstructionFailureLeavesCallerConnectionsUsable),
            new TestCase(nameof(SharedSessionUsesExactlyOneProtocolAuthorityProcessor), SharedSessionUsesExactlyOneProtocolAuthorityProcessor),
        };

        private static void ParticipantBindingRejectsNullTcpClient()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => new TcpBattleParticipantBinding(null!, new PlayerId(1UL), new PlayerSlot(0)));
        }

        private static void SessionRejectsNullInitialState()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => CreateSession(null!, Array.Empty<TcpBattleParticipantBinding>()));
        }

        private static void SessionRejectsNullParticipantArray()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => CreateSession(CreateState(1), null!));
        }

        private static void SessionRejectsWrongParticipantCount()
        {
            TestAssert.Throws<ArgumentException>(
                () => CreateSession(CreateState(2), Array.Empty<TcpBattleParticipantBinding>()));
        }

        private static void SessionRejectsNullParticipantEntry()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => CreateSession(
                    CreateState(1),
                    new TcpBattleParticipantBinding[] { null! }));
        }

        private static void SessionRejectsDuplicateTcpClient()
        {
            BattleState state = CreateState(2);
            using var pair = new LoopbackPair();
            var bindings = new[]
            {
                new TcpBattleParticipantBinding(pair.Accepted, state.Roster.GetPlayerId(new PlayerSlot(0)), new PlayerSlot(0)),
                new TcpBattleParticipantBinding(pair.Accepted, state.Roster.GetPlayerId(new PlayerSlot(1)), new PlayerSlot(1)),
            };

            TestAssert.Throws<ArgumentException>(() => CreateSession(state, bindings));
        }

        private static void SessionRejectsDuplicatePlayerId()
        {
            BattleState state = CreateState(2);
            using var first = new LoopbackPair();
            using var second = new LoopbackPair();
            PlayerId duplicate = state.Roster.GetPlayerId(new PlayerSlot(0));
            var bindings = new[]
            {
                new TcpBattleParticipantBinding(first.Accepted, duplicate, new PlayerSlot(0)),
                new TcpBattleParticipantBinding(second.Accepted, duplicate, new PlayerSlot(1)),
            };

            TestAssert.Throws<ArgumentException>(() => CreateSession(state, bindings));
        }

        private static void SessionRejectsDuplicatePlayerSlot()
        {
            BattleState state = CreateState(2);
            using var first = new LoopbackPair();
            using var second = new LoopbackPair();
            var bindings = new[]
            {
                new TcpBattleParticipantBinding(first.Accepted, state.Roster.GetPlayerId(new PlayerSlot(0)), new PlayerSlot(0)),
                new TcpBattleParticipantBinding(second.Accepted, state.Roster.GetPlayerId(new PlayerSlot(1)), new PlayerSlot(0)),
            };

            TestAssert.Throws<ArgumentException>(() => CreateSession(state, bindings));
        }

        private static void SessionRejectsOutOfRosterOrMismatchedBinding()
        {
            BattleState state = CreateState(1);
            using var first = new LoopbackPair();
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => CreateSession(
                    state,
                    new[]
                    {
                        new TcpBattleParticipantBinding(
                            first.Accepted,
                            state.Roster.GetPlayerId(new PlayerSlot(0)),
                            new PlayerSlot(1)),
                    }));

            using var second = new LoopbackPair();
            TestAssert.Throws<ArgumentException>(
                () => CreateSession(
                    state,
                    new[]
                    {
                        new TcpBattleParticipantBinding(
                            second.Accepted,
                            new PlayerId(999UL),
                            new PlayerSlot(0)),
                    }));
        }

        private static void SessionRejectsDisconnectedParticipant()
        {
            BattleState state = CreateState(1);
            using var client = new TcpClient(AddressFamily.InterNetwork);
            var binding = new TcpBattleParticipantBinding(
                client,
                state.Roster.GetPlayerId(new PlayerSlot(0)),
                new PlayerSlot(0));

            TestAssert.Throws<InvalidOperationException>(
                () => CreateSession(state, new[] { binding }));
        }

        private static void SessionRejectsNonIpv4Participant()
        {
            BattleState state = CreateState(1);
            using var client = new TcpClient(AddressFamily.InterNetworkV6);
            var binding = new TcpBattleParticipantBinding(
                client,
                state.Roster.GetPlayerId(new PlayerSlot(0)),
                new PlayerSlot(0));

            TestAssert.Throws<ArgumentException>(
                () => CreateSession(state, new[] { binding }));
        }

        private static void ConstructionFailureLeavesCallerConnectionsUsable()
        {
            BattleState state = CreateState(1);
            using var pair = new LoopbackPair();
            var binding = new TcpBattleParticipantBinding(
                pair.Accepted,
                new PlayerId(999UL),
                new PlayerSlot(0));

            TestAssert.Throws<ArgumentException>(
                () => CreateSession(state, new[] { binding }));

            TestAssert.True(pair.Accepted.Connected);
            TestAssert.True(pair.Accepted.GetStream().CanRead);
            TestAssert.True(pair.Accepted.GetStream().CanWrite);
        }

        private static void SharedSessionUsesExactlyOneProtocolAuthorityProcessor()
        {
            BattleState state = CreateState(1);
            using var pair = new LoopbackPair();
            var bindings = new[]
            {
                new TcpBattleParticipantBinding(
                    pair.Accepted,
                    state.Roster.GetPlayerId(new PlayerSlot(0)),
                    new PlayerSlot(0)),
            };
            using TcpSharedBattleSession session = CreateSession(state, bindings);

            int processorFields = 0;
            FieldInfo[] fields = typeof(TcpSharedBattleSession).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < fields.Length; index++)
            {
                if (fields[index].FieldType.FullName ==
                    "LockstepArena.Server.ProtocolAuthority.ProtocolAuthorityProcessor")
                {
                    processorFields++;
                }
            }

            TestAssert.Equal(1, processorFields);
            TestAssert.Same(state, session.ServerState);
            TestAssert.Equal(state.Tick, session.NextPublishTick);
            TestAssert.Equal(1, session.ParticipantCount);
        }

        private static TcpSharedBattleSession CreateSession(
            BattleState initialState,
            TcpBattleParticipantBinding[] participants)
        {
            return new TcpSharedBattleSession(
                initialState,
                participants,
                2U,
                8U,
                8,
                MaxPayloadLength,
                16,
                3,
                3);
        }

        internal static BattleState CreateState(int playerCount, uint tick = 100U)
        {
            var ids = new PlayerId[playerCount];
            var states = new PlayerState[playerCount];
            for (int index = 0; index < playerCount; index++)
            {
                ids[index] = new PlayerId((ulong)(1001 + index));
                states[index] = new PlayerState(index * 100, 0, checked((ushort)(1000 + index)));
            }

            return new BattleState(tick, new ActiveRoster(ids), states);
        }
    }

    internal sealed class LoopbackPair : IDisposable
    {
        private readonly TcpListener _listener;

        public LoopbackPair()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start(1);
            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            Client = new TcpClient(AddressFamily.InterNetwork);
            Client.Connect(IPAddress.Loopback, endpoint.Port);
            Accepted = _listener.AcceptTcpClient();
        }

        public TcpClient Client { get; }

        public TcpClient Accepted { get; }

        public void Dispose()
        {
            Accepted.Dispose();
            Client.Dispose();
            _listener.Stop();
        }
    }
}
