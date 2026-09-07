using System;
using System.Collections.Generic;
using System.Reflection;
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
            new TestCase("CatchUpFlattensPublicationsInAdvanceOrder", CatchUpFlattensPublicationsInAdvanceOrder),
            new TestCase("ReturnedPublicationArrayDoesNotBackAuthoritativeHistory", ReturnedPublicationArrayDoesNotBackAuthoritativeHistory),
            new TestCase("PartialCatchUpFailureKeepsEarlierAdvancesAndRethrowsOriginal", PartialCatchUpFailureKeepsEarlierAdvancesAndRethrowsOriginal),
            new TestCase("PartialCatchUpFailureReturnsNoPartialBatchAndFaultsPacer", PartialCatchUpFailureReturnsNoPartialBatchAndFaultsPacer),
            new TestCase("StickyFaultPrecedesNegativeElapsedValidation", StickyFaultPrecedesNegativeElapsedValidation),
            new TestCase("FaultedPacerCannotMutatePublisherAgain", FaultedPacerCannotMutatePublisherAgain),
            new TestCase("TerminalAtEntryReturnsEmptyWithoutRemainderAccumulation", TerminalAtEntryReturnsEmptyWithoutRemainderAccumulation),
            new TestCase("CatchUpStopsNormallyWhenTerminalReachedMidCall", CatchUpStopsNormallyWhenTerminalReachedMidCall),
            new TestCase("FinalMatureIncompleteFrameCanCompleteThroughSubmitAfterTerminal", FinalMatureIncompleteFrameCanCompleteThroughSubmitAfterTerminal),
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

        private static void CatchUpFlattensPublicationsInAdvanceOrder()
        {
            ActiveRoster roster = CreateRoster(1);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 1U);
            ElapsedTickPacer pacer = new ElapsedTickPacer(publisher, 300L);
            AssertEmpty(CompleteTick(publisher, roster, 102U));
            AssertEmpty(CompleteTick(publisher, roster, 100U));
            AssertEmpty(CompleteTick(publisher, roster, 101U));

            AssertTicks(new[] { 100U, 101U, 102U }, pacer.ProcessElapsedStopwatchTicks(30L));
        }

        private static void ReturnedPublicationArrayDoesNotBackAuthoritativeHistory()
        {
            ActiveRoster roster = CreateRoster(1);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 1U);
            ElapsedTickPacer pacer = new ElapsedTickPacer(publisher, 300L);
            AssertEmpty(CompleteTick(publisher, roster, 100U));

            FrameData[] publication = pacer.ProcessElapsedStopwatchTicks(10L);
            FrameData original = publication[0];
            publication[0] = CreateStandaloneFrame(roster, 500U);

            FrameData[] history = publisher.GetAuthoritativeHistorySnapshot();
            TestAssert.Equal(1, history.Length);
            TestAssert.Same(original, history[0]);
        }

        private static void PartialCatchUpFailureKeepsEarlierAdvancesAndRethrowsOriginal()
        {
            FaultFixture fixture = CreateFaultFixture();

            InvalidOperationException exception = TestAssert.ThrowsAndReturn<InvalidOperationException>(
                () => fixture.Pacer.ProcessElapsedStopwatchTicks(20L));

            TestAssert.Equal(
                "A planned publication Tick was absent from pending storage.",
                exception.Message);
            TestAssert.Equal(101UL, fixture.Publisher.CollectionTick);
            TestAssert.Equal<uint?>(101U, fixture.Publisher.EligibilityCeiling);
            TestAssert.Equal(102U, fixture.Publisher.NextPublishTick);
            AssertTicks(new[] { 100U, 101U }, fixture.Publisher.GetAuthoritativeHistorySnapshot());
        }

        private static void PartialCatchUpFailureReturnsNoPartialBatchAndFaultsPacer()
        {
            FaultFixture fixture = CreateFaultFixture();
            FrameData[] sentinel = { CreateStandaloneFrame(fixture.Publisher.Roster, 900U) };
            FrameData[] result = sentinel;

            try
            {
                result = fixture.Pacer.ProcessElapsedStopwatchTicks(20L);
                throw new InvalidOperationException("Expected partial catch-up failure.");
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "A planned publication Tick was absent from pending storage.")
            {
            }

            TestAssert.Same(sentinel, result);
            InvalidOperationException fault = TestAssert.ThrowsAndReturn<InvalidOperationException>(
                () => fixture.Pacer.ProcessElapsedStopwatchTicks(0L));
            TestAssert.Equal("The elapsed Tick pacer is faulted.", fault.Message);
        }

        private static void StickyFaultPrecedesNegativeElapsedValidation()
        {
            FaultFixture fixture = CreateFaultFixture();
            TestAssert.Throws<InvalidOperationException>(
                () => fixture.Pacer.ProcessElapsedStopwatchTicks(20L));

            InvalidOperationException exception = TestAssert.ThrowsAndReturn<InvalidOperationException>(
                () => fixture.Pacer.ProcessElapsedStopwatchTicks(-1L));
            TestAssert.Equal("The elapsed Tick pacer is faulted.", exception.Message);
        }

        private static void FaultedPacerCannotMutatePublisherAgain()
        {
            FaultFixture fixture = CreateFaultFixture();
            TestAssert.Throws<InvalidOperationException>(
                () => fixture.Pacer.ProcessElapsedStopwatchTicks(20L));
            PublisherSnapshot before = Snapshot(fixture.Publisher);

            InvalidOperationException exception = TestAssert.ThrowsAndReturn<InvalidOperationException>(
                () => fixture.Pacer.ProcessElapsedStopwatchTicks(300L));

            TestAssert.Equal("The elapsed Tick pacer is faulted.", exception.Message);
            AssertSnapshot(before, fixture.Publisher);
        }

        private static void TerminalAtEntryReturnsEmptyWithoutRemainderAccumulation()
        {
            TickDrivenFramePublisher publisher = new TickDrivenFramePublisher(
                CreateRoster(1),
                uint.MaxValue - 1U,
                0U,
                0U,
                2);
            ElapsedTickPacer pacer = new ElapsedTickPacer(publisher, 300L);
            PublisherSnapshot before = Snapshot(publisher);

            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(long.MaxValue));
            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(long.MaxValue));

            AssertSnapshot(before, publisher);
        }

        private static void CatchUpStopsNormallyWhenTerminalReachedMidCall()
        {
            ActiveRoster roster = CreateRoster(1);
            TickDrivenFramePublisher publisher = new TickDrivenFramePublisher(
                roster,
                uint.MaxValue - 2U,
                0U,
                1U,
                2);
            ElapsedTickPacer pacer = new ElapsedTickPacer(publisher, 300L);
            AssertTicks(
                new[] { uint.MaxValue - 2U },
                CompleteTick(publisher, roster, uint.MaxValue - 2U));
            AssertEmpty(CompleteTick(publisher, roster, uint.MaxValue - 1U));

            AssertTicks(
                new[] { uint.MaxValue - 1U },
                pacer.ProcessElapsedStopwatchTicks(30L));
            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(30L));
            TestAssert.Equal((ulong)uint.MaxValue - 1UL, publisher.CollectionTick);
            TestAssert.Equal<uint?>(uint.MaxValue - 1U, publisher.EligibilityCeiling);
            TestAssert.Equal(uint.MaxValue, publisher.NextPublishTick);
        }

        private static void FinalMatureIncompleteFrameCanCompleteThroughSubmitAfterTerminal()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = new TickDrivenFramePublisher(
                roster,
                uint.MaxValue - 2U,
                0U,
                1U,
                2);
            ElapsedTickPacer pacer = new ElapsedTickPacer(publisher, 300L);
            AssertTicks(
                new[] { uint.MaxValue - 2U },
                CompleteTick(publisher, roster, uint.MaxValue - 2U));
            AssertEmpty(SubmitSlot(publisher, roster, uint.MaxValue - 1U, 0));

            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(30L));
            AssertTicks(
                new[] { uint.MaxValue - 1U },
                SubmitSlot(publisher, roster, uint.MaxValue - 1U, 1));
            AssertEmpty(pacer.ProcessElapsedStopwatchTicks(30L));
            TestAssert.Equal(uint.MaxValue, publisher.NextPublishTick);
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

        private static FrameData[] SubmitSlot(
            TickDrivenFramePublisher publisher,
            ActiveRoster roster,
            uint tick,
            int slotValue)
        {
            PlayerSlot slot = new PlayerSlot(slotValue);
            InputFrame input = new InputFrame(tick, slot, 0, 0, checked((ushort)(100 + slotValue)));
            return publisher.Submit(roster.GetPlayerId(slot), input);
        }

        private static FrameData CreateStandaloneFrame(ActiveRoster roster, uint tick)
        {
            InputFrame[] inputs = new InputFrame[roster.Count];
            for (int slotValue = 0; slotValue < roster.Count; slotValue++)
            {
                PlayerSlot slot = new PlayerSlot(slotValue);
                inputs[slotValue] = new InputFrame(
                    tick,
                    slot,
                    0,
                    0,
                    checked((ushort)(100 + slotValue)));
            }

            return FrameData.Create(roster, tick, inputs);
        }

        private static FaultFixture CreateFaultFixture()
        {
            ActiveRoster roster = CreateRoster(1);
            TickDrivenFramePublisher publisher = new TickDrivenFramePublisher(
                roster,
                100U,
                0U,
                4U,
                4);
            AssertTicks(new[] { 100U }, CompleteTick(publisher, roster, 100U));
            AssertEmpty(CompleteTick(publisher, roster, 101U));
            AssertEmpty(CompleteTick(publisher, roster, 102U));

            FieldInfo coordinatorField = GetUniquePrivateField(
                typeof(TickDrivenFramePublisher),
                typeof(AuthoritativeFrameCoordinator));
            AuthoritativeFrameCoordinator coordinator =
                (AuthoritativeFrameCoordinator)(coordinatorField.GetValue(publisher)
                    ?? throw new InvalidOperationException("Publisher coordinator was null."));
            Type pendingType = typeof(Dictionary<uint, StrictFrameCollector>);
            FieldInfo pendingField = GetUniquePrivateField(
                typeof(AuthoritativeFrameCoordinator),
                pendingType);
            Dictionary<uint, StrictFrameCollector> pending =
                (Dictionary<uint, StrictFrameCollector>)(pendingField.GetValue(coordinator)
                    ?? throw new InvalidOperationException("Coordinator pending storage was null."));
            StrictFrameCollector collector = pending[102U];
            FieldInfo completedFrameField = GetUniquePrivateField(
                typeof(StrictFrameCollector),
                typeof(FrameData));
            completedFrameField.SetValue(collector, CreateStandaloneFrame(roster, 101U));

            return new FaultFixture(
                publisher,
                new ElapsedTickPacer(publisher, 300L));
        }

        private static FieldInfo GetUniquePrivateField(Type declaringType, Type fieldType)
        {
            FieldInfo? match = null;
            FieldInfo[] fields = declaringType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < fields.Length; index++)
            {
                if (fields[index].FieldType != fieldType)
                {
                    continue;
                }

                if (match is not null)
                {
                    throw new InvalidOperationException(
                        $"Multiple private {fieldType.Name} fields found on {declaringType.Name}.");
                }

                match = fields[index];
            }

            return match ?? throw new InvalidOperationException(
                $"No private {fieldType.Name} field found on {declaringType.Name}.");
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

        private sealed class FaultFixture
        {
            public FaultFixture(
                TickDrivenFramePublisher publisher,
                ElapsedTickPacer pacer)
            {
                Publisher = publisher;
                Pacer = pacer;
            }

            public TickDrivenFramePublisher Publisher { get; }

            public ElapsedTickPacer Pacer { get; }
        }
    }
}
