using System;
using LockstepArena.Simulation;

namespace LockstepArena.Server.ProtocolAuthority.Tests
{
    internal static class ProcessorBootstrapTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(ConstructorRejectsNullInitialState), ConstructorRejectsNullInitialState),
            new TestCase(nameof(ConstructorDelegatesInvalidHistoryCapacity), ConstructorDelegatesInvalidHistoryCapacity),
            new TestCase(nameof(ConstructorBootstrapsExactServerState), ConstructorBootstrapsExactServerState),
            new TestCase(nameof(ConstructorStartsAuthorityAtInitialStateTick), ConstructorStartsAuthorityAtInitialStateTick),
        };

        private static void ConstructorRejectsNullInitialState()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => new ProtocolAuthorityProcessor(null!, 2U, 3));
        }

        private static void ConstructorDelegatesInvalidHistoryCapacity()
        {
            BattleState initialState = CreateInitialState();
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new ProtocolAuthorityProcessor(initialState, 2U, 0));
        }

        private static void ConstructorBootstrapsExactServerState()
        {
            BattleState initialState = CreateInitialState();
            var processor = new ProtocolAuthorityProcessor(initialState, 2U, 3);

            TestAssert.Same(initialState, processor.ServerState);
            TestAssert.Same(initialState.Roster, processor.ServerState.Roster);
        }

        private static void ConstructorStartsAuthorityAtInitialStateTick()
        {
            BattleState initialState = CreateInitialState();
            var processor = new ProtocolAuthorityProcessor(initialState, 2U, 3);

            TestAssert.Equal(100U, processor.ServerState.Tick);
            TestAssert.Equal(100U, processor.NextPublishTick);
        }

        internal static BattleState CreateInitialState()
        {
            var roster = new ActiveRoster(new[]
            {
                new PlayerId(91UL),
                new PlayerId(17UL),
            });
            return new BattleState(100U, roster, new[]
            {
                new PlayerState(-100, 0, 1_000),
                new PlayerState(100, 0, 2_000),
            });
        }
    }
}
