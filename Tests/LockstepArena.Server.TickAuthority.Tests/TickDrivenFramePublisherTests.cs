using System;
using LockstepArena.Server.FrameSync;
using LockstepArena.Simulation;

namespace LockstepArena.Server.TickAuthority.Tests
{
    internal static class TickDrivenFramePublisherTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase("ConstructorRejectsNullRoster", ConstructorRejectsNullRoster),
            new TestCase("ConstructorAllowsFutureWindowSmallerThanInputDelay", ConstructorAllowsFutureWindowSmallerThanInputDelay),
            new TestCase("ZeroDelayStartsWithInitialTickEligible", ZeroDelayStartsWithInitialTickEligible),
            new TestCase("ZeroDelayCompleteInitialFramePublishesImmediatelyOnSubmit", ZeroDelayCompleteInitialFramePublishesImmediatelyOnSubmit),
            new TestCase("PositiveDelayStartsWithoutEligibilityCeiling", PositiveDelayStartsWithoutEligibilityCeiling),
            new TestCase("AdvanceOneTickIncrementsCollectionTickExactlyOnce", AdvanceOneTickIncrementsCollectionTickExactlyOnce),
            new TestCase("TickFirstBecomesEligibleAtDelayPlusTickOffset", TickFirstBecomesEligibleAtDelayPlusTickOffset),
            new TestCase("EligibilityCeilingAdvancesAtMostOneFrameTickPerAdvance", EligibilityCeilingAdvancesAtMostOneFrameTickPerAdvance),
            new TestCase("CompleteFrameBeforeMaturityReturnsEmpty", CompleteFrameBeforeMaturityReturnsEmpty),
            new TestCase("AdvancePublishesCompleteFrameAtExactMaturity", AdvancePublishesCompleteFrameAtExactMaturity),
            new TestCase("MatureIncompleteFrameRemainsUnpublished", MatureIncompleteFrameRemainsUnpublished),
            new TestCase("LateCompletionOfMatureFramePublishesImmediately", LateCompletionOfMatureFramePublishesImmediately),
            new TestCase("MatureFutureFramesRemainBlockedByNextPublishGap", MatureFutureFramesRemainBlockedByNextPublishGap),
            new TestCase("GapFillPublishesOnlyEligibleContinuousPrefix", GapFillPublishesOnlyEligibleContinuousPrefix),
            new TestCase("CompleteButIneligibleFramesRemainPendingAfterGapFill", CompleteButIneligibleFramesRemainPendingAfterGapFill),
            new TestCase("LaterAdvancesPublishNewlyEligibleFramesInOrder", LaterAdvancesPublishNewlyEligibleFramesInOrder),
            new TestCase("AuthoritativeHistoryContainsOnlyPublishedFrames", AuthoritativeHistoryContainsOnlyPublishedFrames),
            new TestCase("AuthoritativeHistoryCapacityRemainsBounded", AuthoritativeHistoryCapacityRemainsBounded),
            new TestCase("RejectedSubmitPreservesScheduleAndExistingPendingInputs", RejectedSubmitPreservesScheduleAndExistingPendingInputs),
            new TestCase("PublicationBatchContainerIsIndependentFromHistorySnapshot", PublicationBatchContainerIsIndependentFromHistorySnapshot),
        };

        private static void ConstructorRejectsNullRoster()
        {
            TestAssert.Throws<ArgumentNullException>(
                () => new TickDrivenFramePublisher(null!, 100U, 2U, 4U, 2));
        }

        private static void ConstructorAllowsFutureWindowSmallerThanInputDelay()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = new TickDrivenFramePublisher(roster, 100U, 2U, 1U, 4);

            AssertEmpty(CompleteTick(publisher, roster, 100U));
            AssertEmpty(CompleteTick(publisher, roster, 101U));
            AssertEmpty(publisher.AdvanceOneTick());
            AssertTicks(new[] { 100U }, publisher.AdvanceOneTick());
            AssertEmpty(Submit(publisher, roster, 102U, 0));
        }

        private static void ZeroDelayStartsWithInitialTickEligible()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = new TickDrivenFramePublisher(roster, 73U, 0U, 2U, 2);

            TestAssert.Same(roster, publisher.Roster);
            TestAssert.Equal(0U, publisher.InputDelayTicks);
            TestAssert.Equal(73UL, publisher.CollectionTick);
            TestAssert.Equal<uint?>(73U, publisher.EligibilityCeiling);
            TestAssert.Equal(73U, publisher.NextPublishTick);
        }

        private static void ZeroDelayCompleteInitialFramePublishesImmediatelyOnSubmit()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = new TickDrivenFramePublisher(roster, 100U, 0U, 2U, 2);

            AssertEmpty(Submit(publisher, roster, 100U, 0));
            AssertTicks(new[] { 100U }, Submit(publisher, roster, 100U, 1));
            TestAssert.Equal(101U, publisher.NextPublishTick);
            TestAssert.Equal(100UL, publisher.CollectionTick);
            TestAssert.Equal<uint?>(100U, publisher.EligibilityCeiling);
        }

        private static void PositiveDelayStartsWithoutEligibilityCeiling()
        {
            TickDrivenFramePublisher publisher = CreatePublisher(CreateRoster(2), 100U, 2U, 4U, 2);

            TestAssert.Equal(100UL, publisher.CollectionTick);
            TestAssert.Equal<uint?>(null, publisher.EligibilityCeiling);
        }

        private static void AdvanceOneTickIncrementsCollectionTickExactlyOnce()
        {
            TickDrivenFramePublisher publisher = CreatePublisher(CreateRoster(2), 100U, 3U, 4U, 2);

            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal(101UL, publisher.CollectionTick);
            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal(102UL, publisher.CollectionTick);
        }

        private static void TickFirstBecomesEligibleAtDelayPlusTickOffset()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 2U, 4U, 4);
            CompleteTick(publisher, roster, 102U);
            CompleteTick(publisher, roster, 101U);
            CompleteTick(publisher, roster, 100U);

            AssertEmpty(publisher.AdvanceOneTick());
            AssertTicks(new[] { 100U }, publisher.AdvanceOneTick());
            AssertTicks(new[] { 101U }, publisher.AdvanceOneTick());
            AssertTicks(new[] { 102U }, publisher.AdvanceOneTick());
        }

        private static void EligibilityCeilingAdvancesAtMostOneFrameTickPerAdvance()
        {
            TickDrivenFramePublisher publisher = CreatePublisher(CreateRoster(2), 100U, 1U, 4U, 2);

            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal<uint?>(100U, publisher.EligibilityCeiling);
            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal<uint?>(101U, publisher.EligibilityCeiling);
            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal<uint?>(102U, publisher.EligibilityCeiling);
        }

        private static void CompleteFrameBeforeMaturityReturnsEmpty()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 2U, 4U, 2);

            AssertEmpty(CompleteTick(publisher, roster, 100U));
            TestAssert.Equal(100U, publisher.NextPublishTick);
            AssertEmpty(publisher.GetAuthoritativeHistorySnapshot());
        }

        private static void AdvancePublishesCompleteFrameAtExactMaturity()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 2U, 4U, 2);
            CompleteTick(publisher, roster, 100U);

            AssertEmpty(publisher.AdvanceOneTick());
            AssertTicks(new[] { 100U }, publisher.AdvanceOneTick());
        }

        private static void MatureIncompleteFrameRemainsUnpublished()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 1U, 4U, 2);
            Submit(publisher, roster, 100U, 0);

            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal(100U, publisher.NextPublishTick);
            AssertEmpty(publisher.GetAuthoritativeHistorySnapshot());
        }

        private static void LateCompletionOfMatureFramePublishesImmediately()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 1U, 4U, 2);
            Submit(publisher, roster, 100U, 0);
            publisher.AdvanceOneTick();

            AssertTicks(new[] { 100U }, Submit(publisher, roster, 100U, 1));
        }

        private static void MatureFutureFramesRemainBlockedByNextPublishGap()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 1U, 4U, 4);
            CompleteTick(publisher, roster, 101U);
            CompleteTick(publisher, roster, 102U);

            publisher.AdvanceOneTick();
            publisher.AdvanceOneTick();
            AssertEmpty(publisher.AdvanceOneTick());
            TestAssert.Equal(100U, publisher.NextPublishTick);
            AssertEmpty(publisher.GetAuthoritativeHistorySnapshot());
        }

        private static void GapFillPublishesOnlyEligibleContinuousPrefix()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 2U, 4U, 4);
            CompleteTick(publisher, roster, 101U);
            CompleteTick(publisher, roster, 102U);
            publisher.AdvanceOneTick();
            publisher.AdvanceOneTick();
            publisher.AdvanceOneTick();

            AssertTicks(new[] { 100U, 101U }, CompleteTick(publisher, roster, 100U));
            TestAssert.Equal(102U, publisher.NextPublishTick);
        }

        private static void CompleteButIneligibleFramesRemainPendingAfterGapFill()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 0U, 4U, 4);
            CompleteTick(publisher, roster, 100U);

            AssertEmpty(CompleteTick(publisher, roster, 101U));
            TestAssert.Equal(101U, publisher.NextPublishTick);
            AssertTicks(new[] { 100U }, publisher.GetAuthoritativeHistorySnapshot());
            AssertTicks(new[] { 101U }, publisher.AdvanceOneTick());
        }

        private static void LaterAdvancesPublishNewlyEligibleFramesInOrder()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 1U, 4U, 4);
            CompleteTick(publisher, roster, 102U);
            CompleteTick(publisher, roster, 103U);
            CompleteTick(publisher, roster, 100U);

            AssertTicks(new[] { 100U }, publisher.AdvanceOneTick());
            CompleteTick(publisher, roster, 101U);
            AssertTicks(new[] { 101U }, publisher.AdvanceOneTick());
            AssertTicks(new[] { 102U }, publisher.AdvanceOneTick());
            AssertTicks(new[] { 103U }, publisher.AdvanceOneTick());
        }

        private static void AuthoritativeHistoryContainsOnlyPublishedFrames()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 2U, 4U, 4);
            CompleteTick(publisher, roster, 101U);
            CompleteTick(publisher, roster, 100U);

            AssertEmpty(publisher.GetAuthoritativeHistorySnapshot());
            publisher.AdvanceOneTick();
            AssertTicks(new[] { 100U }, publisher.AdvanceOneTick());
            AssertTicks(new[] { 100U }, publisher.GetAuthoritativeHistorySnapshot());
        }

        private static void AuthoritativeHistoryCapacityRemainsBounded()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 0U, 4U, 2);
            CompleteTick(publisher, roster, 100U);
            CompleteTick(publisher, roster, 101U);
            publisher.AdvanceOneTick();
            CompleteTick(publisher, roster, 102U);
            publisher.AdvanceOneTick();

            AssertTicks(new[] { 101U, 102U }, publisher.GetAuthoritativeHistorySnapshot());
        }

        private static void RejectedSubmitPreservesScheduleAndExistingPendingInputs()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 1U, 4U, 4);
            Submit(publisher, roster, 100U, 0);
            CompleteTick(publisher, roster, 101U);
            ulong collectionBefore = publisher.CollectionTick;
            uint? ceilingBefore = publisher.EligibilityCeiling;
            uint nextBefore = publisher.NextPublishTick;
            FrameData[] historyBefore = publisher.GetAuthoritativeHistorySnapshot();

            TestAssert.Throws<InvalidOperationException>(() => Submit(publisher, roster, 100U, 0));
            TestAssert.Throws<ArgumentException>(
                () => publisher.Submit(roster.GetPlayerId(new PlayerSlot(1)), CreateInput(100U, 0)));
            TestAssert.Equal(collectionBefore, publisher.CollectionTick);
            TestAssert.Equal(ceilingBefore, publisher.EligibilityCeiling);
            TestAssert.Equal(nextBefore, publisher.NextPublishTick);
            TestAssert.SequenceEqual(historyBefore, publisher.GetAuthoritativeHistorySnapshot());

            publisher.AdvanceOneTick();
            AssertTicks(new[] { 100U }, Submit(publisher, roster, 100U, 1));
            AssertTicks(new[] { 101U }, publisher.AdvanceOneTick());
        }

        private static void PublicationBatchContainerIsIndependentFromHistorySnapshot()
        {
            ActiveRoster roster = CreateRoster(2);
            TickDrivenFramePublisher publisher = CreatePublisher(roster, 100U, 0U, 4U, 2);
            FrameData[] publication = CompleteTick(publisher, roster, 100U);
            FrameData publishedFrame = publication[0];
            publication[0] = CreateStandaloneFrame(roster, 999U);

            FrameData[] history = publisher.GetAuthoritativeHistorySnapshot();
            TestAssert.Same(publishedFrame, history[0]);
            TestAssert.Equal(100U, history[0].Tick);
        }

        private static TickDrivenFramePublisher CreatePublisher(
            ActiveRoster roster,
            uint initialTick,
            uint inputDelayTicks,
            uint maxFutureTickOffset,
            int historyCapacity)
        {
            return new TickDrivenFramePublisher(
                roster,
                initialTick,
                inputDelayTicks,
                maxFutureTickOffset,
                historyCapacity);
        }

        private static ActiveRoster CreateRoster(int playerCount)
        {
            ulong[] values = { 91UL, 17UL, 73UL, 44UL };
            PlayerId[] playerIds = new PlayerId[playerCount];
            for (int index = 0; index < playerCount; index++)
            {
                playerIds[index] = new PlayerId(values[index]);
            }

            return new ActiveRoster(playerIds);
        }

        private static InputFrame CreateInput(uint tick, int slot)
        {
            return new InputFrame(
                tick,
                new PlayerSlot(slot),
                (sbyte)((slot % 3) - 1),
                (sbyte)(((slot + 1) % 3) - 1),
                checked((ushort)(1000 + slot)));
        }

        private static FrameData[] Submit(
            TickDrivenFramePublisher publisher,
            ActiveRoster roster,
            uint tick,
            int slot)
        {
            return publisher.Submit(roster.GetPlayerId(new PlayerSlot(slot)), CreateInput(tick, slot));
        }

        private static FrameData[] CompleteTick(
            TickDrivenFramePublisher publisher,
            ActiveRoster roster,
            uint tick)
        {
            FrameData[] publication = Array.Empty<FrameData>();
            for (int slot = 0; slot < roster.Count; slot++)
            {
                publication = Submit(publisher, roster, tick, slot);
            }

            return publication;
        }

        private static FrameData CreateStandaloneFrame(ActiveRoster roster, uint tick)
        {
            InputFrame[] inputs = new InputFrame[roster.Count];
            for (int slot = 0; slot < roster.Count; slot++)
            {
                inputs[slot] = CreateInput(tick, slot);
            }

            return FrameData.Create(roster, tick, inputs);
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
    }
}
