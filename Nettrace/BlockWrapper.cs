using System;
using System.Buffers;
using System.Diagnostics;

namespace Nettrace
{
    internal class BlockWrapper : IDisposable
    {
        public static BlockWrapper Create(ref SequenceReader<byte> sequenceReader, int blockSize)
        {
            var block = ArrayPool<byte>.Shared.Rent(blockSize);
            var ret = sequenceReader.TryCopyTo(block.AsSpan());
            Debug.Assert(ret);
            var blockWrapper = new BlockWrapper(block);
            return blockWrapper;
        }
        private BlockWrapper(byte[] buffer) => BlockBuffer = buffer;
        public byte[] BlockBuffer { get; }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(BlockBuffer);
        }
    }
}