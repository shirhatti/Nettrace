using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nettrace
{
    public static class PipeWriterExtensions
    {
        internal static void WriteByte(this PipeWriter writer, byte value)
        {
            writer.GetSpan(1)[0] = value;
            writer.Advance(1);
        }
    }
}
