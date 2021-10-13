using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Nettrace
{
    internal static class KnownTypeNames
    {
        public const string Trace = nameof(Trace);
        public const string MetadataBlock = nameof(MetadataBlock);
        public const string StackBlock = nameof(StackBlock);
        public const string EventBlock = nameof(EventBlock);
        public const string EventBlockCompressed = nameof(EventBlockCompressed);
        public const string StackBlockCompressed = nameof(StackBlockCompressed);
        public const string SPBlock = nameof(SPBlock);
        public static string? OptimisticMatching(byte firstByte, int length)
        {
            return ((char)firstByte, length) switch
            {
                ('T', 5) => Trace,
                ('M', 13) => MetadataBlock,
                ('S', 10) => StackBlock,
                ('E', 10) => EventBlock,
                ('E', 20) => EventBlockCompressed,
                ('S', 20) => StackBlockCompressed,
                ('S', 7) => SPBlock,
                _ => default
            };
        }
    }
}
