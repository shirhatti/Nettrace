using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Nettrace
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct SequencePointBlock
    {
        public long Timestamp;
        public int ThreadCount;
        public SequencePointThread[] Threads;
        public SequencePointBlock(NettraceBlock block)
        {
            if (!Equals(block.Type.Name, KnownTypeNames.SPBlock))
            {
                throw new ArgumentException("Requires a SPBlock");
            }
            var reader = new SequenceReader<byte>(block.BlockBody);
            _ = reader.TryReadLittleEndian(out Timestamp);
            _ = reader.TryReadLittleEndian(out ThreadCount);
            Threads = new SequencePointThread[ThreadCount];
            for (int i = 0; i < Threads.Length; i++)
            {
                var span = MemoryMarshal.Cast<SequencePointThread, byte>(MemoryMarshal.CreateSpan(ref Threads[i], 1));
                _ = reader.TryCopyTo(span);
                reader.Advance(span.Length);
            }
        }
    }
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct SequencePointThread
    {
        public long ThreadId;
        public int SequenceNumber;
    }
}
