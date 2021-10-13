using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nettrace
{
    public class RolloverBlockProcessor : IBlockProcessor, IDisposable
    {
        private readonly string _directoryPath;
        private static int fileNumber = 0;
        private bool _initialized;
        private Stream? _stream;
        private TrackingPipeWriter? _writer;
        private IList<BlockHolder> _blockHolders = new List<BlockHolder>();
        private static long MaximumFileSize = 10_000_000;

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
            WriteInitialFileContext(_writer);
            await ReplayBlocksAsync(_writer, token);
            fileNumber++;
            _initialized = true;
        }

        private static void WriteInitialFileContext(TrackingPipeWriter writer)
        {
            // Write Preamble
            Encoding.UTF8.GetBytes("Nettrace", writer);
            // Write StreamHeader
            writer.Write(BitConverter.GetBytes(20));
            Encoding.UTF8.GetBytes("!FastSerialization.1", writer);
        }

        private async ValueTask ReplayBlocksAsync(TrackingPipeWriter writer, CancellationToken token)
        {
            foreach (var blockHolder in _blockHolders)
            {
                await ProcessBlockInternalAsync(blockHolder.Block, token);
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
            if (block.Type.Name == KnownTypeNames.Trace
                || block.Type.Name == KnownTypeNames.MetadataBlock )
            {
                _blockHolders.Add(BlockHolder.Create(block));
            }

            await ProcessBlockInternalAsync(block, token);
        }

        private async ValueTask ProcessBlockInternalAsync(NettraceBlock block, CancellationToken token)
        {
            Debug.Assert(_writer is not null);
            // Write opening tag
            _writer.WriteByte((byte)Tags.BeginPrivateObject);

            ProcessBlockHeader(block.Type);

            if (block.Type.Name != KnownTypeNames.Trace)
            {
                // Write block size
                _writer.Write(BitConverter.GetBytes(block.Size));

                // Write padding
                WritePadding(block);
            }

            ProcessBlockBody(block.BlockBody);

            // Write closing tag
            _writer.WriteByte((byte)Tags.EndObject);

            // TODO: Async
            await _writer.FlushAsync(token);
        }

        private void WritePadding(NettraceBlock block)
        {
            Debug.Assert(_writer is not null);

            // Write padding
            var offset = _writer.WrittenCount % 4;
            var padding = (4 - offset) % 4;
            Span<byte> span = stackalloc byte[(int)padding];
            _writer.Write(span);
        }

        private void ProcessBlockHeader(NettraceType type)
        {
            /*
            [open tag]
                [nullreference tag]
                [type version]
                [minReader Version]
                [length of block name]
                [block name]
            [close tag]
            */
            Debug.Assert(_writer is not null);
            _writer.WriteByte((byte)Tags.BeginPrivateObject);
            _writer.WriteByte((byte)Tags.NullReference);
            _writer.Write(BitConverter.GetBytes(type.Version));
            _writer.Write(BitConverter.GetBytes(type.MinimumReaderVersion));
            _writer.Write(BitConverter.GetBytes(type.Name.Length));
            Encoding.UTF8.GetBytes(type.Name, _writer);
            _writer.WriteByte((byte)Tags.EndObject);
        }

        private void ProcessBlockBody(ReadOnlySequence<byte> blockSequence)
        {
            foreach (var memory in blockSequence)
            {
                _writer!.Write(memory.Span);
            }
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
