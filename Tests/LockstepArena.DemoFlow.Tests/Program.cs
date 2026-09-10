using System;

namespace LockstepArena.DemoFlow.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            TestCase[] tests = ControlProtocolTests.All;
            int failures = 0;
            foreach (TestCase test in tests)
            {
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
