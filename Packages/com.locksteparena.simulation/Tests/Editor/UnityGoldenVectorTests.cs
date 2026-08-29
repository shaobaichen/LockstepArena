using NUnit.Framework;
using LockstepArena.Simulation.Verification;

namespace LockstepArena.Simulation.Editor.Tests
{
    public sealed class UnityGoldenVectorTests
    {
        [Test]
        public void UnityExecutesApprovedGoldenVector()
        {
            Gate2GoldenVectorResult result = Gate2GoldenVector.Run();

            Assert.That(
                typeof(BattleSimulation).Assembly.GetName().Name,
                Is.EqualTo("LockstepArena.Simulation"));
            Assert.That(result.State.Tick, Is.EqualTo(1_000U));
            Assert.That(result.State.Player0.PositionX, Is.EqualTo(0));
            Assert.That(result.State.Player0.PositionZ, Is.EqualTo(-3_000));
            Assert.That(result.State.Player0.Aim, Is.EqualTo((ushort)13_086));
            Assert.That(result.State.Player1.PositionX, Is.EqualTo(0));
            Assert.That(result.State.Player1.PositionZ, Is.EqualTo(3_000));
            Assert.That(result.State.Player1.Aim, Is.EqualTo((ushort)8_699));
            Assert.That(result.Digest, Is.EqualTo(0x04633D1F8699DE68UL));
        }
    }
}
