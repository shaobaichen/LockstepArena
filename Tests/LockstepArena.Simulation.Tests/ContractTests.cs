using System;

namespace LockstepArena.Simulation.Tests
{
    internal static class ContractTests
    {
        public static TestCase[] All { get; } =
        {
            new TestCase(nameof(FrameDataCanonicalizesPlayerSlotOrder), FrameDataCanonicalizesPlayerSlotOrder),
            new TestCase(nameof(FrameDataRejectsDuplicatePlayerSlots), FrameDataRejectsDuplicatePlayerSlots),
            new TestCase(nameof(FrameDataRejectsDifferentTicks), FrameDataRejectsDifferentTicks),
            new TestCase(nameof(InputFrameRejectsInvalidDomainValues), InputFrameRejectsInvalidDomainValues),
            new TestCase(nameof(InitialStateUsesDocumentedSpawns), InitialStateUsesDocumentedSpawns),
        };

        private static void FrameDataCanonicalizesPlayerSlotOrder()
        {
            InputFrame player1 = new InputFrame(7, 1, -1, 0, 40_000);
            InputFrame player0 = new InputFrame(7, 0, 1, 0, 2_000);

            FrameData frame = new FrameData(player1, player0);

            TestAssert.Equal((byte)0, frame.Player0Input.PlayerSlot);
            TestAssert.Equal((byte)1, frame.Player1Input.PlayerSlot);
            TestAssert.Equal((ushort)2_000, frame.Player0Input.Aim);
            TestAssert.Equal((ushort)40_000, frame.Player1Input.Aim);
        }

        private static void FrameDataRejectsDuplicatePlayerSlots()
        {
            InputFrame first = new InputFrame(3, 0, 0, 0, 0);
            InputFrame duplicate = new InputFrame(3, 0, 1, 0, 0);

            TestAssert.Throws<ArgumentException>(() => new FrameData(first, duplicate));
        }

        private static void FrameDataRejectsDifferentTicks()
        {
            InputFrame player0 = new InputFrame(3, 0, 0, 0, 0);
            InputFrame player1 = new InputFrame(4, 1, 0, 0, 0);

            TestAssert.Throws<ArgumentException>(() => new FrameData(player0, player1));
        }

        private static void InputFrameRejectsInvalidDomainValues()
        {
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new InputFrame(0, 2, 0, 0, 0));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new InputFrame(0, 0, -2, 0, 0));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new InputFrame(0, 0, 2, 0, 0));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new InputFrame(0, 0, 0, -2, 0));
            TestAssert.Throws<ArgumentOutOfRangeException>(() => new InputFrame(0, 0, 0, 2, 0));
        }

        private static void InitialStateUsesDocumentedSpawns()
        {
            BattleState state = BattleState.CreateInitial();

            TestAssert.Equal(0U, state.Tick);
            TestAssert.Equal(-1_000, state.Player0.PositionX);
            TestAssert.Equal(0, state.Player0.PositionZ);
            TestAssert.Equal(1_000, state.Player1.PositionX);
            TestAssert.Equal(0, state.Player1.PositionZ);
            TestAssert.Equal((ushort)0, state.Player0.Aim);
            TestAssert.Equal((ushort)0, state.Player1.Aim);
        }
    }
}
