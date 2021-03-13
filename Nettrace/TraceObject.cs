using System;
using System.Runtime.InteropServices;

namespace Nettrace
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct TraceObject
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
    }
}
