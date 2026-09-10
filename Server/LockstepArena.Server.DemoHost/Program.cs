using System;
using LockstepArena.Simulation;

namespace LockstepArena.Server.DemoHost
{
    internal static class Program
    {
        private static int Main()
        {
            PlayerState[] spawns =
            {
                new PlayerState(-300, 0, 1000),
                new PlayerState(300, 0, 2000),
            };
            var options = new DemoServerOptions(
                46000, 46001, 8, 8, 2, spawns, 2U, 8U, 8, 4U,
                4096, 32768, 1024, 7, 257, 8, 512,
                4096, 1024, 11, 251);
            using var server = new TcpDemoServer(options);
            Console.WriteLine($"Lockstep Arena v1 DemoHost: CONTROL={server.ControlPort} BATTLE={server.BattlePort}");
            Console.WriteLine("Press Q to stop.");
            while (true)
            {
                server.PumpOnce();
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q) break;
            }
            return 0;
        }
    }
}
