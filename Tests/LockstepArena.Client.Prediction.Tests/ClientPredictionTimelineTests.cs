using System;
using LockstepArena.Simulation;

namespace LockstepArena.Client.Prediction.Tests
{
    internal static class ClientPredictionTimelineTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase("ConstructorRejectsNullInitialState", ConstructorRejectsNullInitialState),
            new TestCase("ConstructorRejectsNonPositiveMaxPredictionTicks", ConstructorRejectsNonPositiveMaxPredictionTicks),
            new TestCase("ConstructorStartsAlignedWithEmptyHistory", ConstructorStartsAlignedWithEmptyHistory),
            new TestCase("PredictRejectsNullFrameWithoutMutation", PredictRejectsNullFrameWithoutMutation),
            new TestCase("PredictRejectsWrongTickWithoutMutation", PredictRejectsWrongTickWithoutMutation),
            new TestCase("PredictRejectsRosterMismatchWithoutMutation", PredictRejectsRosterMismatchWithoutMutation),
            new TestCase("PredictRejectsWhenLeadEqualsCapacityWithoutMutation", PredictRejectsWhenLeadEqualsCapacityWithoutMutation),
            new TestCase("PredictAdvancesOnlyPredictedTimeline", PredictAdvancesOnlyPredictedTimeline),
            new TestCase("PredictionSupportsTwoThreeAndFourPlayerRosters", PredictionSupportsTwoThreeAndFourPlayerRosters),
            new TestCase("PredictionUsesCanonicalSlotOrder", PredictionUsesCanonicalSlotOrder),
            new TestCase("FinalConsumablePredictionReachesUintMaxValue", FinalConsumablePredictionReachesUintMaxValue),
            new TestCase("PredictionBeyondTerminalIsRejectedWithoutMutation", PredictionBeyondTerminalIsRejectedWithoutMutation),
        };

        private static void ConstructorRejectsNullInitialState()
        {
            TestAssert.Throws<ArgumentNullException>(() => new ClientPredictionTimeline(null!, 1));
        }

        private static void ConstructorRejectsNonPositiveMaxPredictionTicks()
        {
            BattleState state = CreateState(10U, CreateRoster(2));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new ClientPredictionTimeline(state, 0));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new ClientPredictionTimeline(state, -1));
        }

        private static void ConstructorStartsAlignedWithEmptyHistory()
        {
            BattleState state = CreateState(10U, CreateRoster(2));
            var timeline = new ClientPredictionTimeline(state, 4);

            TestAssert.Same(state, timeline.AuthoritativeState);
            TestAssert.Same(state, timeline.PredictedState);
            TestAssert.Equal(4, timeline.MaxPredictionTicks);
            TestAssert.Equal(0, timeline.PendingPredictionCount);
        }

        private static void PredictRejectsNullFrameWithoutMutation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;

            TestAssert.Throws<ArgumentNullException>(() => timeline.Predict(null!));

            AssertUnchanged(timeline, authoritative, predicted, 0);
        }

        private static void PredictRejectsWrongTickWithoutMutation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;

            TestAssert.Throws<ArgumentException>(() => timeline.Predict(CreateNeutralFrame(predicted.Roster, 11U)));

            AssertUnchanged(timeline, authoritative, predicted, 0);
        }

        private static void PredictRejectsRosterMismatchWithoutMutation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            ActiveRoster differentRoster = new ActiveRoster(new[] { new PlayerId(900UL), new PlayerId(700UL) });
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;

            TestAssert.Throws<ArgumentException>(() => timeline.Predict(CreateNeutralFrame(differentRoster, 10U)));

            AssertUnchanged(timeline, authoritative, predicted, 0);
        }

        private static void PredictRejectsWhenLeadEqualsCapacityWithoutMutation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 1);
            timeline.Predict(CreateNeutralFrame(timeline.PredictedState.Roster, 10U));
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;

            TestAssert.Throws<InvalidOperationException>(() => timeline.Predict(CreateNeutralFrame(predicted.Roster, 11U)));

            AssertUnchanged(timeline, authoritative, predicted, 1);
        }

        private static void PredictAdvancesOnlyPredictedTimeline()
        {
            ClientPredictionTimeline timeline = CreateTimeline(10U, 2, 4);
            BattleState authoritative = timeline.AuthoritativeState;

            timeline.Predict(CreateNeutralFrame(authoritative.Roster, 10U));

            TestAssert.Same(authoritative, timeline.AuthoritativeState);
            TestAssert.Equal(10U, timeline.AuthoritativeState.Tick);
            TestAssert.Equal(11U, timeline.PredictedState.Tick);
            TestAssert.Equal(1, timeline.PendingPredictionCount);
            TestAssert.NotSame(authoritative, timeline.PredictedState);
        }

        private static void PredictionSupportsTwoThreeAndFourPlayerRosters()
        {
            for (int count = 2; count <= 4; count++)
            {
                ClientPredictionTimeline timeline = CreateTimeline(20U, count, 2);
                timeline.Predict(CreateNeutralFrame(timeline.PredictedState.Roster, 20U));

                TestAssert.Equal(count, timeline.PredictedState.PlayerCount);
                TestAssert.Equal(21U, timeline.PredictedState.Tick);
                TestAssert.Equal(1, timeline.PendingPredictionCount);
            }
        }

        private static void PredictionUsesCanonicalSlotOrder()
        {
            ActiveRoster roster = CreateRoster(3);
            BattleState initial = CreateState(30U, roster);
            var timeline = new ClientPredictionTimeline(initial, 2);
            FrameData shuffled = FrameData.Create(
                roster,
                30U,
                new[]
                {
                    new InputFrame(30U, new PlayerSlot(2), -1, 0, 302),
                    new InputFrame(30U, new PlayerSlot(0), 1, 0, 100),
                    new InputFrame(30U, new PlayerSlot(1), 0, 1, 201),
                });

            timeline.Predict(shuffled);

            AssertPlayer(timeline.PredictedState, 0, 100, 0, 100);
            AssertPlayer(timeline.PredictedState, 1, 0, 100, 201);
            AssertPlayer(timeline.PredictedState, 2, -100, 0, 302);
        }

        private static void FinalConsumablePredictionReachesUintMaxValue()
        {
            ClientPredictionTimeline timeline = CreateTimeline(uint.MaxValue - 1U, 2, 2);

            timeline.Predict(CreateNeutralFrame(timeline.PredictedState.Roster, uint.MaxValue - 1U));

            TestAssert.Equal(uint.MaxValue - 1U, timeline.AuthoritativeState.Tick);
            TestAssert.Equal(uint.MaxValue, timeline.PredictedState.Tick);
            TestAssert.Equal(1, timeline.PendingPredictionCount);
        }

        private static void PredictionBeyondTerminalIsRejectedWithoutMutation()
        {
            ClientPredictionTimeline timeline = CreateTimeline(uint.MaxValue - 1U, 2, 2);
            timeline.Predict(CreateNeutralFrame(timeline.PredictedState.Roster, uint.MaxValue - 1U));
            BattleState authoritative = timeline.AuthoritativeState;
            BattleState predicted = timeline.PredictedState;

            TestAssert.Throws<InvalidOperationException>(() => timeline.Predict(CreateNeutralFrame(predicted.Roster, uint.MaxValue)));

            AssertUnchanged(timeline, authoritative, predicted, 1);
        }

        private static ClientPredictionTimeline CreateTimeline(uint tick, int playerCount, int capacity)
        {
            return new ClientPredictionTimeline(CreateState(tick, CreateRoster(playerCount)), capacity);
        }

        private static ActiveRoster CreateRoster(int count)
        {
            var ids = new PlayerId[count];
            for (int index = 0; index < count; index++)
            {
                ids[index] = new PlayerId(10_000UL - checked((ulong)(index * 17)));
            }

            return new ActiveRoster(ids);
        }

        private static BattleState CreateState(uint tick, ActiveRoster roster)
        {
            var players = new PlayerState[roster.Count];
            for (int index = 0; index < players.Length; index++)
            {
                players[index] = new PlayerState(0, 0, checked((ushort)(1000 + index)));
            }

            return new BattleState(tick, roster, players);
        }

        private static FrameData CreateNeutralFrame(ActiveRoster roster, uint tick)
        {
            var inputs = new InputFrame[roster.Count];
            for (int index = 0; index < inputs.Length; index++)
            {
                inputs[index] = new InputFrame(tick, new PlayerSlot(index), 0, 0, checked((ushort)(2000 + index)));
            }

            return FrameData.Create(roster, tick, inputs);
        }

        private static void AssertUnchanged(
            ClientPredictionTimeline timeline,
            BattleState authoritative,
            BattleState predicted,
            int pendingCount)
        {
            TestAssert.Same(authoritative, timeline.AuthoritativeState);
            TestAssert.Same(predicted, timeline.PredictedState);
            TestAssert.Equal(pendingCount, timeline.PendingPredictionCount);
        }

        private static void AssertPlayer(BattleState state, int slotValue, int x, int z, ushort aim)
        {
            PlayerState player = state.GetPlayerState(new PlayerSlot(slotValue));
            TestAssert.Equal(x, player.PositionX);
            TestAssert.Equal(z, player.PositionZ);
            TestAssert.Equal(aim, player.Aim);
        }
    }
}
