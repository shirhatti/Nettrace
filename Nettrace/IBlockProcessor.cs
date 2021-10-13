using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace Nettrace
{
    public interface IBlockProcessor
    {
        void ProcessBlock(NettraceBlock block);
    }
}