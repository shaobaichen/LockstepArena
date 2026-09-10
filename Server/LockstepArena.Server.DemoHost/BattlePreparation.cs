using System;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;

namespace LockstepArena.Server.DemoHost
{
    internal sealed class BattlePreparation
    {
        private readonly DemoSession[] _participants;
        private readonly byte[][] _tickets;
        private readonly BattlePreparingEventMessage[] _events;

        internal BattlePreparation(ulong battleId, BattleState initialState, uint finalStateTick, DemoSession[] participants, byte[][] tickets, BattlePreparingEventMessage[] events)
        {
            BattleId = battleId;
            InitialState = initialState;
            FinalStateTick = finalStateTick;
            _participants = (DemoSession[])participants.Clone();
            _tickets = new byte[tickets.Length][];
            for (int index = 0; index < tickets.Length; index++) _tickets[index] = (byte[])tickets[index].Clone();
            _events = (BattlePreparingEventMessage[])events.Clone();
        }

        internal ulong BattleId { get; }
        internal BattleState InitialState { get; }
        internal uint FinalStateTick { get; }
        internal int ParticipantCount => _participants.Length;

        internal DemoSession GetParticipant(PlayerSlot slot)
        {
            ValidateSlot(slot);
            return _participants[slot.Value];
        }

        internal byte[] GetTicket(PlayerSlot slot)
        {
            ValidateSlot(slot);
            return (byte[])_tickets[slot.Value].Clone();
        }

        internal BattlePreparingEventMessage GetPreparingEvent(PlayerSlot slot)
        {
            ValidateSlot(slot);
            return _events[slot.Value].Clone();
        }

        private void ValidateSlot(PlayerSlot slot)
        {
            if (slot.Value >= _participants.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }
}
