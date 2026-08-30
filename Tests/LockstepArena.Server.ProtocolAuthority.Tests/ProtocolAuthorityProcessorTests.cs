using System;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
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

    internal static class ProtocolAuthorityPublicationTests
    {
        public static readonly TestCase[] All =
        {
            new TestCase(nameof(TwoPlayerIncompleteFrameReturnsEmpty), TwoPlayerIncompleteFrameReturnsEmpty),
            new TestCase(nameof(TwoPlayerCompletionReturnsOnePayload), TwoPlayerCompletionReturnsOnePayload),
            new TestCase(nameof(ThreePlayerCompleteFutureFrameWaitsForGap), ThreePlayerCompleteFutureFrameWaitsForGap),
            new TestCase(nameof(GapFillReturnsContinuousPayloadTicks), GapFillReturnsContinuousPayloadTicks),
            new TestCase(nameof(SuccessfulBatchCatchesServerStateUpToAuthority), SuccessfulBatchCatchesServerStateUpToAuthority),
        };

        private static void TwoPlayerIncompleteFrameReturnsEmpty()
        {
            ActiveRoster roster = ProtocolAuthorityTestData.CreateRoster(2);
            ProtocolAuthorityProcessor processor = ProtocolAuthorityTestData.CreateProcessor(roster, 100U, 1U);

            byte[][] output = ProtocolAuthorityTestData.Submit(processor, roster, 100U, 0);

            TestAssert.Equal(0, output.Length);
            TestAssert.Equal(100U, processor.ServerState.Tick);
            TestAssert.Equal(100U, processor.NextPublishTick);
        }

        private static void TwoPlayerCompletionReturnsOnePayload()
        {
            ActiveRoster roster = ProtocolAuthorityTestData.CreateRoster(2);
            ProtocolAuthorityProcessor processor = ProtocolAuthorityTestData.CreateProcessor(roster, 100U, 1U);
            ProtocolAuthorityTestData.Submit(processor, roster, 100U, 0);

            byte[][] output = ProtocolAuthorityTestData.Submit(processor, roster, 100U, 1);

            TestAssert.Equal(1, output.Length);
            ActiveRoster clientRoster = ProtocolAuthorityTestData.CreateRoster(2);
            FrameData frame = ProtocolAuthorityTestData.ParseFrame(output[0], clientRoster);
            TestAssert.Equal(100U, frame.Tick);
            ProtocolAuthorityTestData.AssertCanonicalSlots(frame);
        }

        private static void ThreePlayerCompleteFutureFrameWaitsForGap()
        {
            ActiveRoster roster = ProtocolAuthorityTestData.CreateRoster(3);
            ProtocolAuthorityProcessor processor = ProtocolAuthorityTestData.CreateProcessor(roster, 100U, 1U);

            byte[][] output = ProtocolAuthorityTestData.CompleteTick(processor, roster, 101U);

            TestAssert.Equal(0, output.Length);
            TestAssert.Equal(100U, processor.NextPublishTick);
            TestAssert.Equal(100U, processor.ServerState.Tick);
        }

        private static void GapFillReturnsContinuousPayloadTicks()
        {
            ActiveRoster roster = ProtocolAuthorityTestData.CreateRoster(3);
            ProtocolAuthorityProcessor processor = ProtocolAuthorityTestData.CreateProcessor(roster, 100U, 1U);
            ProtocolAuthorityTestData.CompleteTick(processor, roster, 101U);

            byte[][] output = ProtocolAuthorityTestData.CompleteTick(processor, roster, 100U);

            TestAssert.Equal(2, output.Length);
            ActiveRoster clientRoster = ProtocolAuthorityTestData.CreateRoster(3);
            TestAssert.Equal(100U, ProtocolAuthorityTestData.ParseFrame(output[0], clientRoster).Tick);
            TestAssert.Equal(101U, ProtocolAuthorityTestData.ParseFrame(output[1], clientRoster).Tick);
        }

        private static void SuccessfulBatchCatchesServerStateUpToAuthority()
        {
            ActiveRoster roster = ProtocolAuthorityTestData.CreateRoster(3);
            ProtocolAuthorityProcessor processor = ProtocolAuthorityTestData.CreateProcessor(roster, 100U, 1U);
            ProtocolAuthorityTestData.CompleteTick(processor, roster, 101U);

            ProtocolAuthorityTestData.CompleteTick(processor, roster, 100U);

            TestAssert.Equal(102U, processor.ServerState.Tick);
            TestAssert.Equal(102U, processor.NextPublishTick);
        }
    }

    internal static class ProtocolAuthorityTestData
    {
        internal static ProtocolAuthorityProcessor CreateProcessor(
            ActiveRoster roster,
            uint tick,
            uint maxFutureTickOffset)
        {
            var states = new PlayerState[roster.Count];
            for (int index = 0; index < states.Length; index++)
            {
                states[index] = new PlayerState(index * 100, -(index * 100), checked((ushort)(1_000 + index)));
            }

            return new ProtocolAuthorityProcessor(
                new BattleState(tick, roster, states),
                maxFutureTickOffset,
                5);
        }

        internal static ActiveRoster CreateRoster(int playerCount)
        {
            var playerIds = new PlayerId[playerCount];
            for (int index = 0; index < playerIds.Length; index++)
            {
                playerIds[index] = new PlayerId(checked((ulong)(10_000 - (index * 17))));
            }

            return new ActiveRoster(playerIds);
        }

        internal static byte[][] CompleteTick(
            ProtocolAuthorityProcessor processor,
            ActiveRoster roster,
            uint tick,
            int firstSlot = 0)
        {
            byte[][] output = Array.Empty<byte[]>();
            for (int slotValue = firstSlot; slotValue < roster.Count; slotValue++)
            {
                byte[][] candidate = Submit(processor, roster, tick, slotValue);
                if (candidate.Length > 0)
                {
                    output = candidate;
                }
            }

            return output;
        }

        internal static byte[][] Submit(
            ProtocolAuthorityProcessor processor,
            ActiveRoster roster,
            uint tick,
            int slotValue,
            PlayerId? submittedPlayerId = null)
        {
            PlayerSlot slot = new PlayerSlot(slotValue);
            InputFrame input = CreateInput(tick, slotValue);
            PlayerId playerId = submittedPlayerId ?? roster.GetPlayerId(slot);
            byte[] payload = ProtocolMapper.ToWire(playerId, input).ToByteArray();
            return processor.SubmitPlayerInputPayload(payload);
        }

        internal static InputFrame CreateInput(uint tick, int slotValue)
        {
            return new InputFrame(
                tick,
                new PlayerSlot(slotValue),
                0,
                0,
                checked((ushort)(10_000 + slotValue)));
        }

        internal static FrameData ParseFrame(byte[] payload, ActiveRoster expectedRoster)
        {
            AuthoritativeFrameMessage wire = AuthoritativeFrameMessage.Parser.ParseFrom(payload);
            return ProtocolMapper.ToDomain(wire, expectedRoster);
        }

        internal static void AssertCanonicalSlots(FrameData frame)
        {
            for (int slotValue = 0; slotValue < frame.InputCount; slotValue++)
            {
                TestAssert.Equal(slotValue, frame.GetInput(new PlayerSlot(slotValue)).PlayerSlot.Value);
            }
        }
    }
}
