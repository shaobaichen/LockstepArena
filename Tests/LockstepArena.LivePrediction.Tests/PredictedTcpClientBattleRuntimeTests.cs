using System;
using System.Net.Sockets;
using System.Reflection;
using Google.Protobuf;
using LockstepArena.Client.LiveTcp;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;
using LockstepArena.StreamFraming;

namespace LockstepArena.LivePrediction.Tests
{
    internal static class PredictedTcpClientBattleRuntimeTests
    {
        public static readonly TestCase[] TransportTests =
        {
            new TestCase(nameof(LegacyTcpClientBattlePumpStillStepsItsOwnSimulation), LegacyTcpClientBattlePumpStillStepsItsOwnSimulation),
            new TestCase(nameof(TransportOnlyReceiveMapsWithoutPersistentSimulation), TransportOnlyReceiveMapsWithoutPersistentSimulation),
        };

        public static readonly TestCase[] CoreTests =
        {
            new TestCase(nameof(PredictedRuntimeRejectsLocalIdentityMismatch), PredictedRuntimeRejectsLocalIdentityMismatch),
            new TestCase(nameof(PredictedRuntimeRejectsNonPositiveCapacityLimits), PredictedRuntimeRejectsNonPositiveCapacityLimits),
            new TestCase(nameof(UpdateReconcilesAuthorityBeforeCreatingPrediction), UpdateReconcilesAuthorityBeforeCreatingPrediction),
            new TestCase(nameof(UpdateCreatesAtMostOnePredictionAndSendsOnlyLocalInput), UpdateCreatesAtMostOnePredictionAndSendsOnlyLocalInput),
            new TestCase(nameof(PredictionCapacityReturnsNotSentWithoutNetworkWrite), PredictionCapacityReturnsNotSentWithoutNetworkWrite),
            new TestCase(nameof(SendFailureAfterPredictionFaultsWithoutUndoingPrediction), SendFailureAfterPredictionFaultsWithoutUndoingPrediction),
            new TestCase(nameof(NeutralRemoteSeedUsesInitialPlayerAim), NeutralRemoteSeedUsesInitialPlayerAim),
            new TestCase(nameof(NewPredictionRepeatsLatestSuccessfullyReconciledRemoteInput), NewPredictionRepeatsLatestSuccessfullyReconciledRemoteInput),
            new TestCase(nameof(RetainedPredictionsRemainFieldForFieldUnchangedAfterNewAuthority), RetainedPredictionsRemainFieldForFieldUnchangedAfterNewAuthority),
            new TestCase(nameof(ConsecutiveDirtyAuthoritiesReplayDeterministically), ConsecutiveDirtyAuthoritiesReplayDeterministically),
        };

        private static void LegacyTcpClientBattlePumpStillStepsItsOwnSimulation()
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

        private static void PredictedRuntimeRejectsLocalIdentityMismatch()
        {
            BattleState state = TcpSharedBattleSessionTests.CreateState(2);
            using var first = new LoopbackPair();
            TestAssert.Throws<ArgumentException>(() => CreateRuntime(
                first.Client,
                state,
                new PlayerId(999UL),
                new PlayerSlot(0)));

            using var second = new LoopbackPair();
            TestAssert.Throws<ArgumentOutOfRangeException>(() => CreateRuntime(
                second.Client,
                state,
                state.Roster.GetPlayerId(new PlayerSlot(0)),
                new PlayerSlot(2)));
        }

        private static void PredictedRuntimeRejectsNonPositiveCapacityLimits()
        {
            BattleState state = TcpSharedBattleSessionTests.CreateState(1);
            for (int capacityIndex = 0; capacityIndex < 4; capacityIndex++)
            {
                using var pair = new LoopbackPair();
                int prediction = capacityIndex == 0 ? 0 : 4;
                int perUpdate = capacityIndex == 1 ? 0 : 4;
                int pending = capacityIndex == 2 ? 0 : 8;
                int replay = capacityIndex == 3 ? 0 : 8;
                TestAssert.Throws<ArgumentOutOfRangeException>(() => new PredictedTcpClientBattleRuntime(
                    pair.Client,
                    state,
                    state.Roster.GetPlayerId(new PlayerSlot(0)),
                    new PlayerSlot(0),
                    prediction,
                    perUpdate,
                    pending,
                    replay,
                    4096,
                    64,
                    3,
                    61));
            }
        }

        private static void UpdateReconcilesAuthorityBeforeCreatingPrediction()
        {
            using var fixture = new PredictedClientFixture(2);
            fixture.SendAuthority(CreateFrame(
                fixture.State.Roster,
                100U,
                new InputFrame(100U, new PlayerSlot(0), 1, 0, 101),
                new InputFrame(100U, new PlayerSlot(1), -1, 0, 202)));

            PredictedClientUpdateResult result = fixture.Runtime.Update(
                new LocalInputSample(0, 1, 303));

            TestAssert.Equal(1, result.ReconciledAuthoritativeFrameCount);
            TestAssert.True(result.LocalPredictionSent);
            TestAssert.Equal(101U, fixture.Runtime.AuthoritativeState.Tick);
            TestAssert.Equal(102U, fixture.Runtime.PredictedState.Tick);
            InputFrame sent = fixture.ReadSubmission();
            TestAssert.Equal(101U, sent.Tick);
        }

        private static void UpdateCreatesAtMostOnePredictionAndSendsOnlyLocalInput()
        {
            using var fixture = new PredictedClientFixture(2);
            PredictedClientUpdateResult result = fixture.Runtime.Update(
                new LocalInputSample(1, -1, 444));

            TestAssert.True(result.LocalPredictionSent);
            TestAssert.Equal(1, fixture.Runtime.PendingPredictionCount);
            InputFrame sent = fixture.ReadSubmission();
            TestAssert.Equal(new PlayerSlot(0), sent.PlayerSlot);
            TestAssert.Equal((sbyte)1, sent.MoveX);
            TestAssert.Equal((sbyte)-1, sent.MoveZ);
            TestAssert.Equal((ushort)444, sent.Aim);
        }

        private static void PredictionCapacityReturnsNotSentWithoutNetworkWrite()
        {
            using var fixture = new PredictedClientFixture(2, maxPredictionTicks: 1);
            fixture.Runtime.Update(new LocalInputSample(1, 0, 100));
            fixture.ReadSubmission();

            PredictedClientUpdateResult result = fixture.Runtime.Update(
                new LocalInputSample(0, 1, 101));

            TestAssert.True(!result.LocalPredictionSent);
            TestAssert.Equal(1, fixture.Runtime.PendingPredictionCount);
            TestAssert.True(!fixture.Accepted.Client.Poll(1_000, SelectMode.SelectRead));
        }

        private static void SendFailureAfterPredictionFaultsWithoutUndoingPrediction()
        {
            using var fixture = new PredictedClientFixture(1);
            fixture.Client.Client.Shutdown(SocketShutdown.Send);

            TestAssert.Throws<Exception>(() => fixture.Runtime.Update(
                new LocalInputSample(1, 0, 100)));
            TestAssert.Equal(1, fixture.Runtime.PendingPredictionCount);
            TestAssert.Throws<InvalidOperationException>(() => fixture.Runtime.Update(null));
        }

        private static void NeutralRemoteSeedUsesInitialPlayerAim()
        {
            using var fixture = new PredictedClientFixture(2);
            fixture.Runtime.Update(new LocalInputSample(1, 0, 101));

            PlayerState remote = fixture.Runtime.PredictedState.GetPlayerState(new PlayerSlot(1));
            PlayerState initialRemote = fixture.State.GetPlayerState(new PlayerSlot(1));
            TestAssert.Equal(initialRemote.PositionX, remote.PositionX);
            TestAssert.Equal(initialRemote.PositionZ, remote.PositionZ);
            TestAssert.Equal(initialRemote.Aim, remote.Aim);
        }

        private static void NewPredictionRepeatsLatestSuccessfullyReconciledRemoteInput()
        {
            using var fixture = new PredictedClientFixture(2);
            fixture.SendAuthority(CreateFrame(
                fixture.State.Roster,
                100U,
                new InputFrame(100U, new PlayerSlot(0), 0, 0, 101),
                new InputFrame(100U, new PlayerSlot(1), -1, 1, 202)));
            fixture.Runtime.Update(null);
            fixture.Runtime.Update(new LocalInputSample(0, 0, 102));

            PlayerState remote = fixture.Runtime.PredictedState.GetPlayerState(new PlayerSlot(1));
            TestAssert.Equal(-100, remote.PositionX);
            TestAssert.Equal(200, remote.PositionZ);
            TestAssert.Equal((ushort)202, remote.Aim);
        }

        private static void RetainedPredictionsRemainFieldForFieldUnchangedAfterNewAuthority()
        {
            using var fixture = new PredictedClientFixture(2);
            fixture.Runtime.Update(new LocalInputSample(1, 0, 101));
            fixture.ReadSubmission();
            fixture.Runtime.Update(new LocalInputSample(0, 1, 102));
            fixture.ReadSubmission();
            FrameData retainedBefore = GetPredictedFrames(fixture.Runtime)[1];

            fixture.SendAuthority(CreateFrame(
                fixture.State.Roster,
                100U,
                new InputFrame(100U, new PlayerSlot(0), 1, 0, 101),
                new InputFrame(100U, new PlayerSlot(1), -1, 0, 202)));
            fixture.Runtime.Update(null);

            FrameData retainedAfter = GetPredictedFrames(fixture.Runtime)[0];
            TestAssert.Same(retainedBefore, retainedAfter);
            TcpSharedBattleSessionTests.AssertFramesEqual(retainedBefore, retainedAfter);
        }

        private static void ConsecutiveDirtyAuthoritiesReplayDeterministically()
        {
            using var fixture = new PredictedClientFixture(2, maxAuthoritativeFramesPerUpdate: 1);
            fixture.Runtime.Update(new LocalInputSample(1, 0, 101));
            fixture.ReadSubmission();
            fixture.Runtime.Update(new LocalInputSample(0, 1, 102));
            fixture.ReadSubmission();
            fixture.SendAuthorities(
                CreateFrame(
                    fixture.State.Roster,
                    100U,
                    new InputFrame(100U, new PlayerSlot(0), 1, 0, 101),
                    new InputFrame(100U, new PlayerSlot(1), -1, 0, 201)),
                CreateFrame(
                    fixture.State.Roster,
                    101U,
                    new InputFrame(101U, new PlayerSlot(0), 0, 1, 102),
                    new InputFrame(101U, new PlayerSlot(1), 0, -1, 202)));

            PredictedClientUpdateResult first = fixture.Runtime.Update(null);
            PredictedClientUpdateResult second = fixture.Runtime.Update(null);
            TestAssert.Equal(1, first.DirtyFrameCount);
            TestAssert.Equal(1, second.DirtyFrameCount);
            TestAssert.Equal(102U, fixture.Runtime.AuthoritativeState.Tick);
            TestAssert.Equal(102U, fixture.Runtime.PredictedState.Tick);
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

        internal static FrameData CreateFrame(
            ActiveRoster roster,
            uint tick,
            params InputFrame[] inputs)
        {
            return FrameData.Create(roster, tick, inputs);
        }

        private static PredictedTcpClientBattleRuntime CreateRuntime(
            TcpClient client,
            BattleState state,
            PlayerId playerId,
            PlayerSlot playerSlot,
            int maxPredictionTicks = 4,
            int maxAuthoritativeFramesPerUpdate = 4,
            int maxPendingAuthoritativeFrames = 8,
            int maxReplayFrames = 8)
        {
            return new PredictedTcpClientBattleRuntime(
                client,
                state,
                playerId,
                playerSlot,
                maxPredictionTicks,
                maxAuthoritativeFramesPerUpdate,
                maxPendingAuthoritativeFrames,
                maxReplayFrames,
                4096,
                64,
                3,
                61);
        }

        private static FrameData[] GetPredictedFrames(
            PredictedTcpClientBattleRuntime runtime)
        {
            FieldInfo? timelineField = null;
            FieldInfo[] runtimeFields = typeof(PredictedTcpClientBattleRuntime).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < runtimeFields.Length; index++)
            {
                if (runtimeFields[index].FieldType.FullName ==
                    "LockstepArena.Client.Prediction.ClientPredictionTimeline")
                {
                    timelineField = runtimeFields[index];
                    break;
                }
            }

            object timeline = timelineField?.GetValue(runtime) ??
                throw new InvalidOperationException("Prediction timeline was not found.");
            FieldInfo? historyField = null;
            FieldInfo[] timelineFields = timeline.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < timelineFields.Length; index++)
            {
                if (timelineFields[index].FieldType.IsArray &&
                    timelineFields[index].FieldType.GetElementType()?.Name == "PredictionRecord")
                {
                    historyField = timelineFields[index];
                    break;
                }
            }

            var records = (Array)(historyField?.GetValue(timeline) ??
                throw new InvalidOperationException("Prediction history was not found."));
            var frames = new FrameData[records.Length];
            for (int index = 0; index < records.Length; index++)
            {
                object record = records.GetValue(index) ??
                    throw new InvalidOperationException("Prediction record was null.");
                frames[index] = (FrameData)(record.GetType().GetProperty(
                    "PredictedFrame",
                    BindingFlags.Instance | BindingFlags.Public)?.GetValue(record) ??
                    throw new InvalidOperationException("Predicted frame was not found."));
            }

            return frames;
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

    internal sealed class PredictedClientFixture : IDisposable
    {
        private readonly LoopbackPair _pair;
        private readonly LengthPrefixedFrameDecoder _submissionDecoder =
            new LengthPrefixedFrameDecoder(4096);

        public PredictedClientFixture(
            int playerCount,
            int maxPredictionTicks = 4,
            int maxAuthoritativeFramesPerUpdate = 4,
            int maxPendingAuthoritativeFrames = 8,
            int maxReplayFrames = 8)
        {
            State = TcpSharedBattleSessionTests.CreateState(playerCount);
            _pair = new LoopbackPair();
            Client = _pair.Client;
            Accepted = _pair.Accepted;
            Runtime = new PredictedTcpClientBattleRuntime(
                Client,
                State,
                State.Roster.GetPlayerId(new PlayerSlot(0)),
                new PlayerSlot(0),
                maxPredictionTicks,
                maxAuthoritativeFramesPerUpdate,
                maxPendingAuthoritativeFrames,
                maxReplayFrames,
                4096,
                64,
                3,
                61);
        }

        public BattleState State { get; }

        public TcpClient Client { get; }

        public TcpClient Accepted { get; }

        public PredictedTcpClientBattleRuntime Runtime { get; }

        public void SendAuthority(FrameData frame)
        {
            SendAuthorities(frame);
        }

        public void SendAuthorities(params FrameData[] frames)
        {
            NetworkStream stream = Accepted.GetStream();
            for (int index = 0; index < frames.Length; index++)
            {
                byte[] payload = ProtocolMapper.ToWire(frames[index]).ToByteArray();
                byte[] framed = TcpSharedBattleSessionTests.Frame(payload);
                stream.Write(framed, 0, framed.Length);
            }

            TcpSharedBattleSessionTests.WaitForReadable(Client.Client);
        }

        public InputFrame ReadSubmission()
        {
            TcpSharedBattleSessionTests.WaitForReadable(Accepted.Client);
            var buffer = new byte[128];
            int bytesRead = Accepted.GetStream().Read(buffer, 3, buffer.Length - 3);
            byte[][] payloads = _submissionDecoder.Feed(buffer, 3, bytesRead);
            if (payloads.Length != 1)
            {
                throw new InvalidOperationException("Expected exactly one submission payload.");
            }

            PlayerInputSubmissionMessage message =
                PlayerInputSubmissionMessage.Parser.ParseFrom(payloads[0]);
            return ProtocolMapper.ToDomain(message).Input;
        }

        public void Dispose()
        {
            Runtime.Dispose();
            _pair.Dispose();
        }
    }
}
