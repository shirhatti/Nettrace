using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Nettrace
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    internal struct EventBlockHeader
    {
        public short HeaderSize;
        public short Flags;
        public long MinTimestamp;
        public long MaxTimestamp;
    }
}
