using System;
using LockstepArena.Protocol.Wire;
using LockstepArena.Simulation;

namespace LockstepArena.Protocol
{
    public static class ProtocolMapper
    {
        public static ActiveRosterMessage ToWire(ActiveRoster roster)
        {
            if (roster is null)
            {
                throw new ArgumentNullException(nameof(roster));
            }

            var wire = new ActiveRosterMessage();
            for (int index = 0; index < roster.Count; index++)
            {
                var slot = new PlayerSlot(index);
                wire.Players.Add(new RosterEntryMessage
                {
                    PlayerSlot = checked((uint)index),
                    PlayerId = roster.GetPlayerId(slot).Value,
                });
            }

            return wire;
        }

        public static ActiveRoster ToDomain(ActiveRosterMessage wire)
        {
            if (wire is null)
            {
                throw new ArgumentNullException(nameof(wire));
            }

            int count = wire.Players.Count;
            if (count == 0)
            {
                throw new ProtocolMappingException("Wire roster must contain at least one player.");
            }

            var playerIds = new PlayerId[count];
            var present = new bool[count];
            for (int index = 0; index < count; index++)
            {
                RosterEntryMessage entry = wire.Players[index];
                if (entry is null)
                {
                    throw new ProtocolMappingException("Wire roster cannot contain a null player entry.");
                }

                int slot = ToSlotValue(entry.PlayerSlot, "Wire roster player slot");
                if (slot >= count)
                {
                    throw new ProtocolMappingException("Wire roster player slot is outside the roster count.");
                }

                if (present[slot])
                {
                    throw new ProtocolMappingException("Wire roster cannot contain a duplicate player slot.");
                }

                playerIds[slot] = new PlayerId(entry.PlayerId);
                present[slot] = true;
            }

            for (int index = 0; index < present.Length; index++)
            {
                if (!present[index])
                {
                    throw new ProtocolMappingException("Wire roster player slots must be contiguous.");
                }
            }

            try
            {
                return new ActiveRoster(playerIds);
            }
            catch (ArgumentException exception)
            {
                throw new ProtocolMappingException("Wire roster violates the Simulation roster contract.", exception);
            }
        }

        public static PlayerInputSubmissionMessage ToWire(
            PlayerId submittedPlayerId,
            InputFrame input)
        {
            return new PlayerInputSubmissionMessage
            {
                SubmittedPlayerId = submittedPlayerId.Value,
                Input = ToWire(input),
            };
        }

        public static (PlayerId SubmittedPlayerId, InputFrame Input) ToDomain(
            PlayerInputSubmissionMessage wire)
        {
            if (wire is null)
            {
                throw new ArgumentNullException(nameof(wire));
            }

            if (wire.Input is null)
            {
                throw new ProtocolMappingException("Wire input submission must contain an input message.");
            }

            return (new PlayerId(wire.SubmittedPlayerId), ToDomainInput(wire.Input));
        }

        public static AuthoritativeFrameMessage ToWire(FrameData frame)
        {
            if (frame is null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            var wire = new AuthoritativeFrameMessage
            {
                Tick = frame.Tick,
                Roster = ToWire(frame.Roster),
            };
            for (int index = 0; index < frame.InputCount; index++)
            {
                wire.Inputs.Add(ToWire(frame.GetInput(new PlayerSlot(index))));
            }

            return wire;
        }

        public static FrameData ToDomain(
            AuthoritativeFrameMessage wire,
            ActiveRoster expectedRoster)
        {
            if (wire is null)
            {
                throw new ArgumentNullException(nameof(wire));
            }

            if (expectedRoster is null)
            {
                throw new ArgumentNullException(nameof(expectedRoster));
            }

            if (wire.Roster is null)
            {
                throw new ProtocolMappingException("Wire authoritative frame must contain a roster.");
            }

            ActiveRoster wireRoster = ToDomain(wire.Roster);
            if (!wireRoster.HasSameStructure(expectedRoster))
            {
                throw new ProtocolMappingException("Wire authoritative frame roster does not match the expected roster.");
            }

            var inputs = new InputFrame[wire.Inputs.Count];
            for (int index = 0; index < inputs.Length; index++)
            {
                InputFrameMessage input = wire.Inputs[index];
                if (input is null)
                {
                    throw new ProtocolMappingException("Wire authoritative frame cannot contain a null input.");
                }

                inputs[index] = ToDomainInput(input);
            }

            try
            {
                return FrameData.Create(expectedRoster, wire.Tick, inputs);
            }
            catch (ArgumentException exception)
            {
                throw new ProtocolMappingException(
                    "Wire authoritative frame violates the Simulation frame contract.",
                    exception);
            }
        }

        private static InputFrameMessage ToWire(InputFrame input)
        {
            return new InputFrameMessage
            {
                Tick = input.Tick,
                PlayerSlot = checked((uint)input.PlayerSlot.Value),
                MoveX = input.MoveX,
                MoveZ = input.MoveZ,
                Aim = input.Aim,
            };
        }

        private static InputFrame ToDomainInput(InputFrameMessage wire)
        {
            int slotValue = ToSlotValue(wire.PlayerSlot, "Wire input player slot");
            if (wire.MoveX < -1 || wire.MoveX > 1)
            {
                throw new ProtocolMappingException("Wire move_x must be -1, 0, or 1.");
            }

            if (wire.MoveZ < -1 || wire.MoveZ > 1)
            {
                throw new ProtocolMappingException("Wire move_z must be -1, 0, or 1.");
            }

            if (wire.Aim > ushort.MaxValue)
            {
                throw new ProtocolMappingException("Wire aim exceeds the Domain ushort range.");
            }

            try
            {
                return new InputFrame(
                    wire.Tick,
                    new PlayerSlot(slotValue),
                    checked((sbyte)wire.MoveX),
                    checked((sbyte)wire.MoveZ),
                    checked((ushort)wire.Aim));
            }
            catch (ArgumentException exception)
            {
                throw new ProtocolMappingException("Wire input violates the Simulation input contract.", exception);
            }
        }

        private static int ToSlotValue(uint wireSlot, string fieldName)
        {
            if (wireSlot > int.MaxValue)
            {
                throw new ProtocolMappingException($"{fieldName} exceeds the Domain int range.");
            }

            return checked((int)wireSlot);
        }
    }
}
