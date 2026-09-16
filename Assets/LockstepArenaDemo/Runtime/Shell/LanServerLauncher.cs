#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;

namespace LockstepArena.Demo
{
    public sealed class LanServerLauncher : IDisposable
    {
        private Process? ownedProcess;

        public bool OwnsServer => ownedProcess != null && !ownedProcess.HasExited;

        public void StartOwned(string executablePath, int controlPort, int battlePort)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new ArgumentException("Server executable path is required.", nameof(executablePath));
            }

            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException("DemoHost executable was not found.", executablePath);
            }

            ValidatePort(controlPort, nameof(controlPort));
            ValidatePort(battlePort, nameof(battlePort));

            if (ownedProcess != null)
            {
                if (!ownedProcess.HasExited)
                {
                    throw new InvalidOperationException("This launcher already owns a running DemoHost process.");
                }

                ownedProcess.Dispose();
                ownedProcess = null;
            }

            string workingDirectory = Path.GetDirectoryName(Path.GetFullPath(executablePath))!;
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"--bind 0.0.0.0 --control-port {controlPort} --battle-port {battlePort}",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            ownedProcess = Process.Start(startInfo)
                ?? throw new InvalidOperationException("DemoHost process could not be started.");
        }

        public async Task WaitUntilReadyAsync(string address, int port, TimeSpan timeout)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Server address is required.", nameof(address));
            }

            ValidatePort(port, nameof(port));
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            Exception? lastFailure = null;
            while (stopwatch.Elapsed < timeout)
            {
                using var client = new TcpClient();
                try
                {
                    Task connect = client.ConnectAsync(address, port);
                    Task completed = await Task.WhenAny(connect, Task.Delay(TimeSpan.FromMilliseconds(200)));
                    if (completed == connect)
                    {
                        await connect;
                        return;
                    }

                    lastFailure = new TimeoutException("The current DemoHost connection attempt timed out.");
                }
                catch (Exception exception) when (
                    exception is SocketException ||
                    exception is InvalidOperationException)
                {
                    lastFailure = exception;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }

            throw new TimeoutException(
                $"DemoHost did not become ready at {address}:{port} within {timeout.TotalSeconds:0.##} seconds.",
                lastFailure);
        }

        public void StopOwned()
        {
            Process? process = ownedProcess;
            ownedProcess = null;
            if (process == null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    KillProcessTree(process);
                    process.WaitForExit(2000);
                }
            }
            finally
            {
                process.Dispose();
            }
        }

        public void Dispose()
        {
            StopOwned();
        }

        private static void ValidatePort(int port, string parameterName)
        {
            if (port < 1 || port > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static void KillProcessTree(Process process)
        {
            MethodInfo? killTree = typeof(Process).GetMethod("Kill", new[] { typeof(bool) });
            if (killTree != null)
            {
                killTree.Invoke(process, new object[] { true });
                return;
            }

            process.Kill();
        }
    }
}
