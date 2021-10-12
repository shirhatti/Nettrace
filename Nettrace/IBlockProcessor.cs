using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace Nettrace
{
    internal interface IBlockProcessor
    {
        void ProcessBlock(NettraceBlock block);
    }
}