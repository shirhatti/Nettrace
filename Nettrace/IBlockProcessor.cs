using System.Buffers;
using System.Threading.Tasks;

namespace Nettrace
{
    internal interface IBlockProcessor
    {
        void ProcessBlock(ReadOnlySequence<byte> blockSequence, string blockName);
    }
}