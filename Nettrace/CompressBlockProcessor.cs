using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Text;

namespace Nettrace
{
    internal class CompressBlockProcessor : IBlockProcessor, IDisposable
    {
        private readonly string _filePath;
        private bool _initialized;
        private Stream? _stream;
        private TrackingPipeWriter? _writer;

        public CompressBlockProcessor(string filePath)
        {
            _filePath = filePath;
        }

        [MemberNotNull(nameof(_writer))]
        private void Initialize()
        {
            _stream = File.OpenWrite(_filePath);
            _writer = new TrackingPipeWriter(PipeWriter.Create(_stream));
            WriteInitialFileContext(_writer);
            _initialized = true;
        }

        static private void WriteInitialFileContext(TrackingPipeWriter writer)
        {
            // Write Preamble
            Encoding.UTF8.GetBytes("Nettrace", writer);
            // Write StreamHeader
            writer.Write(BitConverter.GetBytes(20));
            Encoding.UTF8.GetBytes("!FastSerialization.1", writer);
        }

        public void ProcessBlock(NettraceBlock block)
        {
            if (!_initialized)
            {
                Initialize();
            }
            Debug.Assert(_writer is not null);

            // Write opening tag
            _writer.WriteByte((byte)Tags.BeginPrivateObject);

            // TODO: Write block header
            ProcessBlockHeader(block.Type);

            if (block.Type.Name != KnownTypeNames.Trace && block.Type.Name != KnownTypeNames.EventBlock && block.Type.Name != KnownTypeNames.StackBlock)
            {
                // padding should run before writing block size
                var padding = GetPadding(block);

                // Write block size
                _writer.Write(BitConverter.GetBytes(block.Size));
                // Write padding
                Span<byte> span = stackalloc byte[padding];
                _writer.Write(span);
            }

            ProcessBlockBody(block);

            // Write closing tag
            _writer.WriteByte((byte)Tags.EndObject);

            // TODO: Async
            _writer.FlushAsync().GetAwaiter().GetResult();
        }

        private int GetPadding(NettraceBlock block)
        {
            Debug.Assert(_writer is not null);

            // Get padding size
            var offset = (_writer.WrittenCount + sizeof(int)) % 4;
            var padding = (int)((4 - offset) % 4);
            return padding;
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
            if (type.Name == KnownTypeNames.EventBlock)
            {
                _writer.Write(BitConverter.GetBytes(KnownTypeNames.EventBlockCompressed.Length));
                Encoding.UTF8.GetBytes(KnownTypeNames.EventBlockCompressed, _writer);
            }
            else if (type.Name == KnownTypeNames.StackBlock)
            {
                _writer.Write(BitConverter.GetBytes(KnownTypeNames.StackBlockCompressed.Length));
                Encoding.UTF8.GetBytes(KnownTypeNames.StackBlockCompressed, _writer);
            }
            else
            {
                _writer.Write(BitConverter.GetBytes(type.Name.Length));
                Encoding.UTF8.GetBytes(type.Name, _writer);
            }
            _writer.WriteByte((byte)Tags.EndObject);
        }

        private void ProcessBlockBody(NettraceBlock block)
        {
            if (block.Type.Name != KnownTypeNames.EventBlock && block.Type.Name != KnownTypeNames.StackBlock)
            {
                ProcessBlockBodyUncompressed(block.BlockBody);
                return;
            }

            var blockSequence = block.BlockBody;
            var maxLength = BrotliEncoder.GetMaxCompressedLength((int)blockSequence.Length);
            var padding = GetPadding(block);
            var prefixLength = padding + sizeof(int);
            var memory = _writer!.GetMemory(maxLength + prefixLength);
            for (var i = 0; i < prefixLength; ++i)
            {
                memory.Span[i] = 0;
            }
            using var encoder = new BrotliEncoder(quality: 9, window: 10);

            var slicedMemory = memory.Slice(prefixLength);
            var totalWritten = 0;
            OperationStatus status;
            foreach (var sequence in blockSequence)
            {
                status = encoder.Compress(sequence.Span, slicedMemory.Span, out var consumed, out var written, false);
                Debug.Assert(consumed == sequence.Span.Length);
                Debug.Assert(status == OperationStatus.Done);
                slicedMemory = slicedMemory.Slice(written);
                totalWritten += written;
            }
            status = encoder.Compress(ReadOnlySpan<byte>.Empty, slicedMemory.Span, out var _, out var written2, true);
            Debug.Assert(status == OperationStatus.Done);
            totalWritten += written2;

            var size = BitConverter.GetBytes(totalWritten);
            // Write size
            size.CopyTo(memory);
            _writer.Advance(totalWritten + prefixLength);
        }

        private void ProcessBlockBodyUncompressed(ReadOnlySequence<byte> blockSequence)
        {
            foreach (var memory in blockSequence)
            {
                _writer!.Write(memory.Span);
            }
        }

        public void Dispose()
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
