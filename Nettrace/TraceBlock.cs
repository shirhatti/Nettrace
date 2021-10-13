using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace Nettrace
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct TraceBlock
    {
        public short Year;
        public short Month;
        public short DayOfWeek;
        public short Day;
        public short Hour;
        public short Minute;
        public short Second;
        public short Millisecond;
        public long SyncTimeQPC;
        public long QPCFrequency;
        public int PointerSize;
        public int ProcessId;
        public int NumberOfProcessors;
        public int ExpectedCPUSamplingRate;

        public TraceBlock(NettraceBlock block) : this()
        {
            if (!Equals(block.Type.Name, KnownTypeNames.Trace))
            {
                throw new ArgumentException("Requires a Trace block");
            }
            var reader = new SequenceReader<byte>(block.BlockBody);
            var span = MemoryMarshal.Cast<TraceBlock, byte>(MemoryMarshal.CreateSpan(ref this, 1));
            _ = reader.TryCopyTo(span);
            reader.Advance(span.Length);
        }
    }
}
