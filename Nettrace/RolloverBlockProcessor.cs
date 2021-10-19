using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Nettrace
{
    public class RolloverBlockProcessor : IBlockProcessor, IDisposable
    {
        private static bool replayStackBlocks = true;
        private readonly string _directoryPath;
        private static int fileNumber = 0;
        private bool _initialized;
        private Stream? _stream;
        private TrackingPipeWriter? _writer;
        private LinkedList<BlockHolder> _blockHolders = new();
        private LinkedListNode<BlockHolder>? _lastSPBlockIndex;
        private static long MaximumFileSize = 10_000_000;
        private int _currentBlockNumber = 0;

        public RolloverBlockProcessor(string directoryPath)
        {
            _directoryPath = directoryPath;
        }
        [MemberNotNull(nameof(_writer))]
        private async ValueTask InitializeFileAsync(CancellationToken token)
        {
            CloseFile();
            var filePath = Path.Combine(_directoryPath, $"trace_{fileNumber}.nettrace");
            _stream = File.OpenWrite(filePath);
            _writer = new TrackingPipeWriter(PipeWriter.Create(_stream));
            BlockHelpers.WriteInitialFileContext(_writer);
            await ReplayBlocksAsync(token);
            fileNumber++;
            _initialized = true;
        }

        private async ValueTask ReplayBlocksAsync(CancellationToken token)
        {
            var cursor = _blockHolders.First;
            var removeBlocks = true;
            while (cursor != null)
            {
                await ProcessBlockInternalAsync(cursor!.Value.Block, token);
                var advance = cursor.Next;
                if ( (cursor.Value.Block.Type.Name == KnownTypeNames.StackBlock
                    || cursor.Value.Block.Type.Name == KnownTypeNames.SPBlock)
                    && removeBlocks)
                {
                    if (cursor.Value == _lastSPBlockIndex.Value)
                    {
                        removeBlocks = false;
                    }
                    cursor.Value.Dispose();
                    _blockHolders.Remove(cursor);
                }
                cursor = advance;
            }
        }

        public async ValueTask ProcessBlockAsync(NettraceBlock block, CancellationToken token = default)
        {
            if (!_initialized)
            {
                await InitializeFileAsync(token);
            }
            Debug.Assert(_writer is not null);
            if (_writer.WrittenCount > MaximumFileSize)
            {
                await InitializeFileAsync(token);
                // This may cause an issue with rundown
                Debug.Assert(_writer.WrittenCount <= MaximumFileSize);
            }
            if (block.Type.Name == KnownTypeNames.SPBlock)
            {
                _lastSPBlockIndex = _blockHolders.Last;
            }
            // We can just entirely skip the block since it's useless now
            if (block.Type.Name == KnownTypeNames.StackBlock && !replayStackBlocks)
            {
                _currentBlockNumber++;
                return;
            }

            if (block.Type.Name == KnownTypeNames.Trace
                || block.Type.Name == KnownTypeNames.MetadataBlock
                || block.Type.Name == KnownTypeNames.StackBlock
                || block.Type.Name == KnownTypeNames.SPBlock)
            {
                _blockHolders.AddLast(BlockHolder.Create(block, _currentBlockNumber));
            }

            await ProcessBlockInternalAsync(block, token);
            _currentBlockNumber++;
        }

        private ValueTask ProcessBlockInternalAsync(NettraceBlock block, CancellationToken token)
        {
            Debug.Assert(_writer is not null);
            return new(BlockHelpers.ProcessBlock(_writer, block, token).AsTask());
        }

        public void Dispose()
        {
            CloseFile();
            foreach (var blockHolder in _blockHolders)
            {
                blockHolder?.Dispose();
            }
        }

        private void CloseFile()
        {
            if (_writer is not null)
            {
                // Write null reference tag
                _writer.WriteByte((byte)Tags.NullReference);

                _writer.Complete();
            }
            _stream?.Dispose();
        }
    }
}
