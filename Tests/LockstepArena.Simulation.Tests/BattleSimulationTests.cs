using System;

namespace LockstepArena.Simulation.Tests
{
    internal static class BattleSimulationTests
    {
        public static TestCase[] All { get; } =
        {
            new TestCase(nameof(NeutralInputAdvancesOneTickWithoutMovement), NeutralInputAdvancesOneTickWithoutMovement),
            new TestCase(nameof(OpposingMovementUpdatesEachPlayer), OpposingMovementUpdatesEachPlayer),
            new TestCase(nameof(InputAimReplacesPlayerAim), InputAimReplacesPlayerAim),
            new TestCase(nameof(MovementClampsAtEveryArenaBoundary), MovementClampsAtEveryArenaBoundary),
            new TestCase(nameof(UnexpectedFrameTickIsRejectedWithoutMutation), UnexpectedFrameTickIsRejectedWithoutMutation),
        };

        private static void NeutralInputAdvancesOneTickWithoutMovement()
        {
            BattleSimulation simulation = new BattleSimulation(BattleState.CreateInitial());

            simulation.Step(Frame(0, 0, 0, 0, 0));

            TestAssert.Equal(1U, simulation.State.Tick);
            TestAssert.Equal(-1_000, simulation.State.Player0.PositionX);
            TestAssert.Equal(0, simulation.State.Player0.PositionZ);
            TestAssert.Equal(1_000, simulation.State.Player1.PositionX);
            TestAssert.Equal(0, simulation.State.Player1.PositionZ);
        }

        private static void OpposingMovementUpdatesEachPlayer()
        {
            BattleSimulation simulation = new BattleSimulation(BattleState.CreateInitial());
            FrameData frame = new FrameData(
                new InputFrame(0, 0, 1, 1, 0),
                new InputFrame(0, 1, -1, -1, 0));

            simulation.Step(frame);

            TestAssert.Equal(-900, simulation.State.Player0.PositionX);
            TestAssert.Equal(100, simulation.State.Player0.PositionZ);
            TestAssert.Equal(900, simulation.State.Player1.PositionX);
            TestAssert.Equal(-100, simulation.State.Player1.PositionZ);
        }

        private static void InputAimReplacesPlayerAim()
        {
            BattleState initial = new BattleState(
                12,
                new PlayerState(-200, 300, 111),
                new PlayerState(400, -500, 222));
            BattleSimulation simulation = new BattleSimulation(initial);
            FrameData frame = new FrameData(
                new InputFrame(12, 1, 0, 0, 65_535),
                new InputFrame(12, 0, 0, 0, 32_768));

            simulation.Step(frame);

            TestAssert.Equal((ushort)32_768, simulation.State.Player0.Aim);
            TestAssert.Equal((ushort)65_535, simulation.State.Player1.Aim);
        }

        private static void MovementClampsAtEveryArenaBoundary()
        {
            BattleState initial = new BattleState(
                4,
                new PlayerState(SimulationConfig.ArenaMaxX, SimulationConfig.ArenaMaxZ, 0),
                new PlayerState(SimulationConfig.ArenaMinX, SimulationConfig.ArenaMinZ, 0));
            BattleSimulation simulation = new BattleSimulation(initial);
            FrameData frame = new FrameData(
                new InputFrame(4, 0, 1, 1, 0),
                new InputFrame(4, 1, -1, -1, 0));

            simulation.Step(frame);

            TestAssert.Equal(5_000, simulation.State.Player0.PositionX);
            TestAssert.Equal(3_000, simulation.State.Player0.PositionZ);
            TestAssert.Equal(-5_000, simulation.State.Player1.PositionX);
            TestAssert.Equal(-3_000, simulation.State.Player1.PositionZ);
        }

        private static void UnexpectedFrameTickIsRejectedWithoutMutation()
        {
            BattleSimulation simulation = new BattleSimulation(BattleState.CreateInitial());
            FrameData futureFrame = Frame(1, 1, 0, -1, 0);

            TestAssert.Throws<ArgumentException>(() => simulation.Step(futureFrame));

            TestAssert.Equal(0U, simulation.State.Tick);
            TestAssert.Equal(-1_000, simulation.State.Player0.PositionX);
            TestAssert.Equal(1_000, simulation.State.Player1.PositionX);
        }

        private static FrameData Frame(uint tick, sbyte player0X, sbyte player0Z, sbyte player1X, sbyte player1Z)
        {
            return new FrameData(
                new InputFrame(tick, 0, player0X, player0Z, 0),
                new InputFrame(tick, 1, player1X, player1Z, 0));
        }
    }
}
