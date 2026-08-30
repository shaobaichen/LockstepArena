using System;
using Google.Protobuf;
using LockstepArena.Protocol;
using LockstepArena.Protocol.Wire;
using LockstepArena.Server.FrameSync;
using LockstepArena.Simulation;

namespace LockstepArena.Server.ProtocolAuthority
{
    public sealed class ProtocolAuthorityProcessor
    {
        private readonly AuthoritativeFrameCoordinator _coordinator;
        private readonly BattleSimulation _serverSimulation;
        private bool _faulted;

        public ProtocolAuthorityProcessor(
            BattleState initialState,
            uint maxFutureTickOffset,
            int authoritativeHistoryCapacity)
        {
            if (initialState is null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            _coordinator = new AuthoritativeFrameCoordinator(
                initialState.Roster,
                initialState.Tick,
                maxFutureTickOffset,
                authoritativeHistoryCapacity);
            _serverSimulation = new BattleSimulation(initialState);
        }

        public BattleState ServerState => _serverSimulation.State;

        public uint NextPublishTick => _coordinator.NextPublishTick;

        public byte[][] SubmitPlayerInputPayload(byte[] completePayload)
        {
            if (_faulted)
            {
                throw new InvalidOperationException("The protocol authority processor is faulted.");
            }

            if (completePayload is null)
            {
                throw new ArgumentNullException(nameof(completePayload));
            }

            PlayerInputSubmissionMessage wire =
                PlayerInputSubmissionMessage.Parser.ParseFrom(completePayload);
            (PlayerId submittedPlayerId, InputFrame input) = ProtocolMapper.ToDomain(wire);
            FrameData[] publication = _coordinator.Submit(submittedPlayerId, input);

            if (publication.Length == 0)
            {
                return Array.Empty<byte[]>();
            }

            try
            {
                var payloads = new byte[publication.Length][];
                for (var index = 0; index < publication.Length; index++)
                {
                    FrameData frame = publication[index];
                    _serverSimulation.Step(frame);
                    payloads[index] = ProtocolMapper.ToWire(frame).ToByteArray();
                }

                return payloads;
            }
            catch
            {
                _faulted = true;
                throw;
            }
        }
    }
}
