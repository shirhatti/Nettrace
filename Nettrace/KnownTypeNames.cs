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
        public static readonly string Trace = nameof(Trace);
        public static readonly string MetadataBlock = nameof(MetadataBlock);
        public static readonly string StackBlock = nameof(StackBlock);
        public static readonly string EventBlock = nameof(EventBlock);
        public static readonly string SPBlock = nameof(SPBlock);
        public static string OptimisticMatching(byte firstByte, int length)
        {
            return ((char)firstByte, length) switch
            {
                ('T', 5) => Trace,
                ('M', 13) => MetadataBlock,
                ('S', 10) => StackBlock,
                ('E', 10) => EventBlock,
                ('S', 7) => SPBlock,
                _ => default
            };
        }
    }
}
