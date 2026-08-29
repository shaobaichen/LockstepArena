using System;
using LockstepArena.Simulation.Verification;

namespace LockstepArena.Server.Verification
{
    internal static class Program
    {
        private static int Main()
        {
            Gate2GoldenVectorResult result = Gate2GoldenVector.Run();
            bool passed = true;
            passed &= Check("Tick", 1_000U, result.State.Tick);
            passed &= Check("Player0.PositionX", 0, result.State.Player0.PositionX);
            passed &= Check("Player0.PositionZ", -3_000, result.State.Player0.PositionZ);
            passed &= Check("Player0.Aim", (ushort)13_086, result.State.Player0.Aim);
            passed &= Check("Player1.PositionX", 0, result.State.Player1.PositionX);
            passed &= Check("Player1.PositionZ", 3_000, result.State.Player1.PositionZ);
            passed &= Check("Player1.Aim", (ushort)8_699, result.State.Player1.Aim);
            passed &= Check("Digest", 0x04633D1F8699DE68UL, result.Digest);

            if (!passed)
            {
                return 1;
            }

            Console.WriteLine($"PASS Gate2GoldenVector Tick={result.State.Tick} Digest={result.Digest:X16}");
            return 0;
        }

        private static bool Check<T>(string field, T expected, T actual)
            where T : IEquatable<T>
        {
            if (expected.Equals(actual))
            {
                return true;
            }

            Console.Error.WriteLine($"FAIL {field}: expected <{expected}> actual <{actual}>");
            return false;
        }
    }
}
