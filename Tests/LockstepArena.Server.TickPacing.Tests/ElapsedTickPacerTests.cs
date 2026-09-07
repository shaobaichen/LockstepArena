using System;
using LockstepArena.Server.FrameSync;
using LockstepArena.Simulation;

namespace LockstepArena.Server.TickPacing.Tests
{
    internal static class ElapsedTickPacerTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase("ConstructorRejectsNullPublisher", ConstructorRejectsNullPublisher),
            new TestCase("ConstructorRejectsZeroStopwatchFrequency", ConstructorRejectsZeroStopwatchFrequency),
            new TestCase("ConstructorRejectsNegativeStopwatchFrequency", ConstructorRejectsNegativeStopwatchFrequency),
            new TestCase("NegativeElapsedTicksRejectBeforeAnyMutation", NegativeElapsedTicksRejectBeforeAnyMutation),
            new TestCase("NegativeElapsedRejectionDoesNotFaultPacer", NegativeElapsedRejectionDoesNotFaultPacer),
            new TestCase("ZeroElapsedTicksReturnsEmptyWithoutMutation", ZeroElapsedTicksReturnsEmptyWithoutMutation),
            new TestCase("PacingUsesSimulationConfigTickRateThirty", PacingUsesSimulationConfigTickRateThirty),
            new TestCase("SubTickElapsedCarriesFractionalRemainder", SubTickElapsedCarriesFractionalRemainder),
            new TestCase("RationalBoundaryDoesNotUseTruncatedStopwatchPeriod", RationalBoundaryDoesNotUseTruncatedStopwatchPeriod),
            new TestCase("SplitElapsedDeltasMatchSingleDelta", SplitElapsedDeltasMatchSingleDelta),
            new TestCase("MultipleDueIntervalsCatchUpWithoutCap", MultipleDueIntervalsCatchUpWithoutCap),
            new TestCase("UInt128ArithmeticHandlesLongMaxElapsedDelta", UInt128ArithmeticHandlesLongMaxElapsedDelta),
            new TestCase("InputDelayMaturityRemainsOwnedByTickDrivenPublisher", InputDelayMaturityRemainsOwnedByTickDrivenPublisher),
        };

        private static void ConstructorRejectsNullPublisher()
        {
            TestAssert.Throws<ArgumentNullException>(() => new ElapsedTickPacer(null!, 300L));
        }

        private static void ConstructorRejectsZeroStopwatchFrequency()
        {
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new ElapsedTickPacer(CreatePublisher(CreateRoster(1), 100U, 0U), 0L));
        }

        private static void ConstructorRejectsNegativeStopwatchFrequency()
        {
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => new ElapsedTickPacer(CreatePublisher(CreateRoster(1), 100U, 0U), -1L));
        }

        private static void NegativeElapsedTicksRejectBeforeAnyMutation()
        {
            ActiveRoster roster = CreateRoster(1);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 1U);
            ElapsedTickPacer pacer = new ElapsedTickPacer(publisher, 300L);
            PublisherSnapshot before = Snapshot(publisher);

            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => pacer.ProcessElapsedStopwatchTicks(-1L));

            AssertSnapshot(before, publisher);
        }

        private static void NegativeElapsedRejectionDoesNotFaultPacer()
        {
            TickDrivenFramePublisher publisher = CreatePublisher(CreateRoster(1), 100U, 1U);
            ElapsedTickPacer pacer = new ElapsedTickPacer(publisher, 300L);
            TestAssert.Throws<ArgumentOutOfRangeException>(
                () => pacer.ProcessElapsedStopwatchTicks(-1L));

            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(10L));
            TestAssert.Equal(101UL, publisher.CollectionTick);
        }

        private static void ZeroElapsedTicksReturnsEmptyWithoutMutation()
        {
            TickDrivenFramePublisher publisher = CreatePublisher(CreateRoster(1), 100U, 1U);
            ElapsedTickPacer pacer = new ElapsedTickPacer(publisher, 300L);
            PublisherSnapshot before = Snapshot(publisher);

            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(0L));

            AssertSnapshot(before, publisher);
        }

        private static void PacingUsesSimulationConfigTickRateThirty()
        {
            TestAssert.Equal(30, SimulationConfig.TickRate);
            TickDrivenFramePublisher publisher = CreatePublisher(CreateRoster(1), 100U, 1U);
            ElapsedTickPacer pacer = new ElapsedTickPacer(publisher, 300L);

            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(10L));

            TestAssert.Equal(101UL, publisher.CollectionTick);
        }

        private static void SubTickElapsedCarriesFractionalRemainder()
        {
            TickDrivenFramePublisher publisher = CreatePublisher(CreateRoster(1), 100U, 1U);
            ElapsedTickPacer pacer = new ElapsedTickPacer(publisher, 300L);

            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(9L));
            TestAssert.Equal(100UL, publisher.CollectionTick);
            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(1L));
            TestAssert.Equal(101UL, publisher.CollectionTick);
        }

        private static void RationalBoundaryDoesNotUseTruncatedStopwatchPeriod()
        {
            TickDrivenFramePublisher publisher = CreatePublisher(CreateRoster(1), 100U, 1U);
            ElapsedTickPacer pacer = new ElapsedTickPacer(publisher, 1000L);

            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(33L));
            TestAssert.Equal(100UL, publisher.CollectionTick);
            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(1L));
            TestAssert.Equal(101UL, publisher.CollectionTick);
        }

        private static void SplitElapsedDeltasMatchSingleDelta()
        {
            TickDrivenFramePublisher singlePublisher = CreatePublisher(CreateRoster(1), 100U, 100U);
            TickDrivenFramePublisher splitPublisher = CreatePublisher(CreateRoster(1), 100U, 100U);
            ElapsedTickPacer single = new ElapsedTickPacer(singlePublisher, 1000L);
            ElapsedTickPacer split = new ElapsedTickPacer(splitPublisher, 1000L);

            AssertEmpty(single.ProcessElapsedStopwatchTicks(100L));
            long[] pieces = { 7L, 11L, 15L, 1L, 33L, 33L };
            for (int index = 0; index < pieces.Length; index++)
            {
                AssertEmpty(split.ProcessElapsedStopwatchTicks(pieces[index]));
            }

            TestAssert.Equal(103UL, singlePublisher.CollectionTick);
            TestAssert.Equal(singlePublisher.CollectionTick, splitPublisher.CollectionTick);

            AssertEmpty(single.ProcessElapsedStopwatchTicks(34L));
            AssertEmpty(split.ProcessElapsedStopwatchTicks(34L));
            TestAssert.Equal(singlePublisher.CollectionTick, splitPublisher.CollectionTick);
        }

        private static void MultipleDueIntervalsCatchUpWithoutCap()
        {
            TickDrivenFramePublisher publisher = CreatePublisher(CreateRoster(1), 100U, 100U);
            ElapsedTickPacer pacer = new ElapsedTickPacer(publisher, 300L);

            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(50L));

            TestAssert.Equal(105UL, publisher.CollectionTick);
        }

        private static void UInt128ArithmeticHandlesLongMaxElapsedDelta()
        {
            TickDrivenFramePublisher publisher = new TickDrivenFramePublisher(
                CreateRoster(1),
                uint.MaxValue - 3U,
                0U,
                2U,
                2);
            ElapsedTickPacer pacer = new ElapsedTickPacer(publisher, long.MaxValue);

            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(long.MaxValue));

            TestAssert.Equal((ulong)uint.MaxValue - 1UL, publisher.CollectionTick);
            TestAssert.Equal<uint?>(uint.MaxValue - 1U, publisher.EligibilityCeiling);
        }

        private static void InputDelayMaturityRemainsOwnedByTickDrivenPublisher()
        {
            ActiveRoster roster = CreateRoster(1);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 2U);
            ElapsedTickPacer pacer = new ElapsedTickPacer(publisher, 300L);
            AssertEmpty(CompleteTick(publisher, roster, 100U));

            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(10L));
            AssertTicks(new[] { 100U }, pacer.ProcessElapsedStopwatchTicks(10L));
        }

        private static ActiveRoster CreateRoster(int count)
        {
            PlayerId[] ids = new PlayerId[count];
            for (int index = 0; index < count; index++)
            {
                ids[index] = new PlayerId(1000UL + (ulong)index);
            }

            return new ActiveRoster(ids);
        }

        private static TickDrivenFramePublisher CreatePublisher(
            ActiveRoster roster,
            uint initialTick,
            uint inputDelayTicks)
        {
            return new TickDrivenFramePublisher(roster, initialTick, inputDelayTicks, 8U, 8);
        }

        private static FrameData[] CompleteTick(
            TickDrivenFramePublisher publisher,
            ActiveRoster roster,
            uint tick)
        {
            FrameData[] publication = Array.Empty<FrameData>();
            for (int slot = 0; slot < roster.Count; slot++)
            {
                PlayerSlot playerSlot = new PlayerSlot(slot);
                InputFrame input = new InputFrame(tick, playerSlot, 0, 0, checked((ushort)(100 + slot)));
                publication = publisher.Submit(roster.GetPlayerId(playerSlot), input);
            }

            return publication;
        }

        private static PublisherSnapshot Snapshot(TickDrivenFramePublisher publisher)
        {
            return new PublisherSnapshot(
                publisher.CollectionTick,
                publisher.EligibilityCeiling,
                publisher.NextPublishTick,
                publisher.GetAuthoritativeHistorySnapshot());
        }

        private static void AssertSnapshot(
            PublisherSnapshot expected,
            TickDrivenFramePublisher publisher)
        {
            TestAssert.Equal(expected.CollectionTick, publisher.CollectionTick);
            TestAssert.Equal(expected.EligibilityCeiling, publisher.EligibilityCeiling);
            TestAssert.Equal(expected.NextPublishTick, publisher.NextPublishTick);
            FrameData[] actualHistory = publisher.GetAuthoritativeHistorySnapshot();
            TestAssert.Equal(expected.History.Length, actualHistory.Length);
            for (int index = 0; index < expected.History.Length; index++)
            {
                TestAssert.Same(expected.History[index], actualHistory[index]);
            }
        }

        private static void AssertEmpty(FrameData[] frames)
        {
            TestAssert.Equal(0, frames.Length);
        }

        private static void AssertTicks(uint[] expectedTicks, FrameData[] frames)
        {
            TestAssert.Equal(expectedTicks.Length, frames.Length);
            for (int index = 0; index < expectedTicks.Length; index++)
            {
                TestAssert.Equal(expectedTicks[index], frames[index].Tick);
            }
        }

        private readonly struct PublisherSnapshot
        {
            public PublisherSnapshot(
                ulong collectionTick,
                uint? eligibilityCeiling,
                uint nextPublishTick,
                FrameData[] history)
            {
                CollectionTick = collectionTick;
                EligibilityCeiling = eligibilityCeiling;
                NextPublishTick = nextPublishTick;
                History = history;
            }

            public ulong CollectionTick { get; }

            public uint? EligibilityCeiling { get; }

            public uint NextPublishTick { get; }

            public FrameData[] History { get; }
        }
    }
}
