using System;
using System.Collections.Generic;

namespace LockstepArena.Simulation.Tests
{
    internal static class DeterminismTests
    {
        private const int TwinTickCount = 10_000;
        private const int HistoryTickCount = 2_000;

        public static TestCase[] All { get; } =
        {
            new TestCase(nameof(EqualStatesHaveEqualDigests), EqualStatesHaveEqualDigests),
            new TestCase(nameof(CanonicalStateChangesAlterDigest), CanonicalStateChangesAlterDigest),
            new TestCase(nameof(GoldenDigestLocksFieldAndByteOrder), GoldenDigestLocksFieldAndByteOrder),
            new TestCase(nameof(TwinSimulationsMatchDigestAtEveryTick), TwinSimulationsMatchDigestAtEveryTick),
            new TestCase(nameof(InitialStateAndFrameHistoryRebuildFinalDigest), InitialStateAndFrameHistoryRebuildFinalDigest),
        };

        private static void EqualStatesHaveEqualDigests()
        {
            BattleState first = new BattleState(
                77,
                new PlayerState(-2_300, 1_200, 456),
                new PlayerState(4_500, -2_900, 65_000));
            BattleState second = new BattleState(
                77,
                new PlayerState(-2_300, 1_200, 456),
                new PlayerState(4_500, -2_900, 65_000));

            TestAssert.Equal(StateDigest.Compute(first), StateDigest.Compute(second));
        }

        private static void CanonicalStateChangesAlterDigest()
        {
            BattleState baseline = new BattleState(
                12,
                new PlayerState(-100, 200, 300),
                new PlayerState(400, -500, 600));
            BattleState moved = new BattleState(
                12,
                new PlayerState(-99, 200, 300),
                new PlayerState(400, -500, 600));
            BattleState aimed = new BattleState(
                12,
                new PlayerState(-100, 200, 301),
                new PlayerState(400, -500, 600));

            ulong baselineDigest = StateDigest.Compute(baseline);
            TestAssert.NotEqual(baselineDigest, StateDigest.Compute(moved));
            TestAssert.NotEqual(baselineDigest, StateDigest.Compute(aimed));
        }

        private static void GoldenDigestLocksFieldAndByteOrder()
        {
            BattleState state = new BattleState(
                0x01020304,
                new PlayerState(-1, 0x01020304, 0xABCD),
                new PlayerState(5_000, -3_000, 0xFFFF));

            TestAssert.Equal(0x6123AD83F7831D54UL, StateDigest.Compute(state));
        }

        private static void TwinSimulationsMatchDigestAtEveryTick()
        {
            BattleState initial = BattleState.CreateInitial();
            BattleSimulation first = new BattleSimulation(initial);
            BattleSimulation second = new BattleSimulation(initial);

            for (uint tick = 0; tick < TwinTickCount; tick++)
            {
                CreateScriptedInputs(tick, out InputFrame player0, out InputFrame player1);
                first.Step(new FrameData(player0, player1));
                second.Step(new FrameData(player1, player0));

                ulong firstDigest = StateDigest.Compute(first.State);
                ulong secondDigest = StateDigest.Compute(second.State);
                if (firstDigest != secondDigest)
                {
                    throw new InvalidOperationException(
                        $"Twin digest mismatch after tick {tick}: {firstDigest:X16} != {secondDigest:X16}.");
                }
            }

            TestAssert.Equal((uint)TwinTickCount, first.State.Tick);
        }

        private static void InitialStateAndFrameHistoryRebuildFinalDigest()
        {
            BattleState initial = BattleState.CreateInitial();
            BattleSimulation original = new BattleSimulation(initial);
            List<FrameData> history = new List<FrameData>(HistoryTickCount);

            for (uint tick = 0; tick < HistoryTickCount; tick++)
            {
                CreateScriptedInputs(tick, out InputFrame player0, out InputFrame player1);
                FrameData frame = new FrameData(player0, player1);
                history.Add(frame);
                original.Step(frame);
            }

            BattleSimulation rebuilt = new BattleSimulation(initial);
            foreach (FrameData frame in history)
            {
                rebuilt.Step(frame);
            }

            TestAssert.Equal((uint)HistoryTickCount, rebuilt.State.Tick);
            TestAssert.Equal(StateDigest.Compute(original.State), StateDigest.Compute(rebuilt.State));
        }

        private static void CreateScriptedInputs(
            uint tick,
            out InputFrame player0,
            out InputFrame player1)
        {
            int phase = (int)(tick % 400);
            sbyte player0X = 0;
            sbyte player0Z = 0;
            sbyte player1X = 0;
            sbyte player1Z = 0;

            if (phase < 100)
            {
                player0X = 1;
                player1X = -1;
            }
            else if (phase >= 150 && phase < 250)
            {
                player0X = -1;
                player1X = 1;
            }
            else if (phase >= 250 && phase < 325)
            {
                player0Z = 1;
                player1Z = -1;
            }
            else if (phase >= 325)
            {
                player0Z = -1;
                player1Z = 1;
            }

            ushort player0Aim = unchecked((ushort)((tick * 997U) + 123U));
            ushort player1Aim = unchecked((ushort)((tick * 619U) + 45_678U));
            player0 = new InputFrame(tick, 0, player0X, player0Z, player0Aim);
            player1 = new InputFrame(tick, 1, player1X, player1Z, player1Aim);
        }
    }
}
