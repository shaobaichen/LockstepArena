using System;
using LockstepArena.Server.FrameSync;
using LockstepArena.Simulation;

namespace LockstepArena.Server.ProtocolAuthority
{
    public sealed class ProtocolAuthorityProcessor
    {
        private readonly AuthoritativeFrameCoordinator _coordinator;
        private readonly BattleSimulation _serverSimulation;

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
    }
}
