using System;
using System.Buffers;
using System.Diagnostics;

namespace Nettrace
{
    internal class BlockHolder : IDisposable
    {
        private readonly byte[] _buffer;
        public NettraceBlock Block { get; private set; }
        public static BlockHolder Create(NettraceBlock nettraceBlock)
        {
            var block = ArrayPool<byte>.Shared.Rent(nettraceBlock.Size);
            nettraceBlock.BlockBody.CopyTo(block);
            var blockWrapper = new BlockHolder(block, nettraceBlock.Size);
            blockWrapper.Block = new NettraceBlock()
            {
                Type = nettraceBlock.Type,
                AlignmentPadding = nettraceBlock.AlignmentPadding,
                Size = nettraceBlock.Size,
                BlockBody = blockWrapper.BlockBuffer
            };
            return blockWrapper;
        }
        private BlockHolder(byte[] buffer, int size)
        {
            _buffer = buffer;
            BlockBuffer = new ReadOnlySequence<byte>(buffer, 0, size);
        }

        public ReadOnlySequence<byte> BlockBuffer { get; }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }
    }
}