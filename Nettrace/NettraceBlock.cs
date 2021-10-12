﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nettrace
{
    public struct NettraceBlock
    {
        public NettraceType Type { get; init; }
        public ReadOnlySequence<byte> BlockBody { get; init; }
        public long AlignmentPadding { get; init; }
        public int Size { get; init; }

    }
}