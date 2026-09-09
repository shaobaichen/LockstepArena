using System;
using System.Net.Sockets;
using System.Reflection;
using Google.Protobuf;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Protocol;
using LockstepArena.Simulation;

namespace LockstepArena.LivePrediction.Tests
{
    internal static class PredictedTcpClientBattleRuntimeTests
    {
        public static readonly TestCase[] TransportTests =
        {
            new TestCase(nameof(LegacyClientPumpStillStepsItsSimulation), LegacyClientPumpStillStepsItsSimulation),
            new TestCase(nameof(TransportOnlyReceiveMapsWithoutPersistentSimulation), TransportOnlyReceiveMapsWithoutPersistentSimulation),
        };

        private static void LegacyClientPumpStillStepsItsSimulation()
        {
            BattleState initialState = TcpSharedBattleSessionTests.CreateState(1);
            using var pair = new LoopbackPair();
            using var pump = new TcpClientBattlePump(
                pair.Client,
                initialState,
                4096,
                64,
                3,
                61);
            FrameData authority = CreateFrame(initialState.Roster, 100U, 1, 0, 1234);

            SendAuthority(pair.Accepted, pair.Client, authority);
            FrameData[] received = pump.PumpReceiveOnce();

            TestAssert.Equal(1, received.Length);
            TcpSharedBattleSessionTests.AssertFramesEqual(authority, received[0]);
            TestAssert.Equal(101U, pump.ClientState.Tick);
        }

        private static void TransportOnlyReceiveMapsWithoutPersistentSimulation()
        {
            BattleState initialState = TcpSharedBattleSessionTests.CreateState(1);
            using var pair = new LoopbackPair();
            MethodInfo factory = typeof(TcpClientBattlePump).GetMethod(
                "CreateTransportOnly",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(TcpClient),
                    typeof(ActiveRoster),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                },
                modifiers: null) ?? throw new InvalidOperationException(
                    "Missing internal CreateTransportOnly API.");
            MethodInfo receive = typeof(TcpClientBattlePump).GetMethod(
                "PumpReceiveTransportOnlyOnce",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null) ?? throw new InvalidOperationException(
                    "Missing internal PumpReceiveTransportOnlyOnce API.");

            using var pump = (TcpClientBattlePump)(factory.Invoke(
                null,
                new object[] { pair.Client, initialState.Roster, 4096, 64, 3, 61 }) ??
                throw new InvalidOperationException("Transport-only factory returned null."));

            FieldInfo[] fields = typeof(TcpClientBattlePump).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < fields.Length; index++)
            {
                TestAssert.True(fields[index].FieldType != typeof(BattleSimulation));
            }

            FrameData authority = CreateFrame(initialState.Roster, 100U, -1, 1, 4321);
            SendAuthority(pair.Accepted, pair.Client, authority);
            FrameData[] received = (FrameData[])(receive.Invoke(pump, null) ??
                throw new InvalidOperationException("Transport-only receive returned null."));

            TestAssert.Equal(1, received.Length);
            TcpSharedBattleSessionTests.AssertFramesEqual(authority, received[0]);
            TestAssert.Equal(100U, initialState.Tick);
        }

        private static FrameData CreateFrame(
            ActiveRoster roster,
            uint tick,
            sbyte moveX,
            sbyte moveZ,
            ushort aim)
        {
            return FrameData.Create(
                roster,
                tick,
                new[] { new InputFrame(tick, new PlayerSlot(0), moveX, moveZ, aim) });
        }

        private static void SendAuthority(
            TcpClient serverClient,
            TcpClient receivingClient,
            FrameData frame)
        {
            byte[] payload = ProtocolMapper.ToWire(frame).ToByteArray();
            byte[] framed = TcpSharedBattleSessionTests.Frame(payload);
            NetworkStream stream = serverClient.GetStream();
            stream.Write(framed, 0, framed.Length);
            TcpSharedBattleSessionTests.WaitForReadable(receivingClient.Client);
        }
    }
}
