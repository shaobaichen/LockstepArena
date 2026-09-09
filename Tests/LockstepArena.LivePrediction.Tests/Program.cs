using System;

namespace LockstepArena.LivePrediction.Tests
{
    internal sealed class TestCase
    {
        public TestCase(string name, Action body)
        {
            Name = name;
            Body = body;
        }

        public string Name { get; }

        public Action Body { get; }
    }

    internal static class Program
    {
        private static int Main()
        {
            TestCase[] tests = TcpSharedBattleSessionTests.ConstructionTests;
            int failures = 0;
            for (int index = 0; index < tests.Length; index++)
            {
                TestCase test = tests[index];
                try
                {
                    test.Body();
                    Console.WriteLine($"PASS {test.Name}");
                }
                catch (Exception exception)
                {
                    failures++;
                    Console.Error.WriteLine($"FAIL {test.Name}: {exception}");
                }
            }

            Console.WriteLine($"RESULT {tests.Length - failures}/{tests.Length} passed");
            return failures == 0 ? 0 : 1;
        }
    }
}
