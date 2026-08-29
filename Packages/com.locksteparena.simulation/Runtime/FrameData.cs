using System;

namespace LockstepArena.Simulation
{
    public readonly struct FrameData
    {
        public FrameData(InputFrame first, InputFrame second)
        {
            if (first.Tick != second.Tick)
            {
                throw new ArgumentException("Both inputs must belong to the same tick.");
            }

            if (first.PlayerSlot == second.PlayerSlot)
            {
                throw new ArgumentException("A frame must contain one input for each player slot.");
            }

            Tick = first.Tick;
            if (first.PlayerSlot == 0)
            {
                Player0Input = first;
                Player1Input = second;
            }
            else
            {
                Player0Input = second;
                Player1Input = first;
            }
        }

        public uint Tick { get; }

        public InputFrame Player0Input { get; }

        public InputFrame Player1Input { get; }
    }
}
