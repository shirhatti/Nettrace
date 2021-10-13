using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nettrace
{
    public class DecompressBlockProcessor : IBlockProcessor, IDisposable
    {
        private readonly string _filePath;
        private bool _initialized;
        private Stream? _stream;
        private TrackingPipeWriter? _writer;

        public DecompressBlockProcessor(string filePath)
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
            writer.WriteInt(20);
            Encoding.UTF8.GetBytes("!FastSerialization.1", writer);
        }

        public async ValueTask ProcessBlockAsync(NettraceBlock block, CancellationToken token = default)
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

            ProcessBlockBodyPreamble(block);

            ProcessBlockBody(block);

            // Write closing tag
            _writer.WriteByte((byte)Tags.EndObject);

            await _writer.FlushAsync(token);
        }

        private void ProcessBlockBodyPreamble(NettraceBlock block)
        {
            Debug.Assert(_writer is not null);
            if (block.Type.Name != KnownTypeNames.Trace && block.Type.Name != KnownTypeNames.EventBlockCompressed
                && block.Type.Name != KnownTypeNames.StackBlockCompressed)
            {
                // padding should run before writing block size
                var padding = GetPadding(block);

                // Write block size
                _writer.WriteInt(block.Size);
                // Write padding
                Span<byte> span = stackalloc byte[padding];
                _writer.Write(span);
            }
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
            _writer.WriteInt(type.Version);
            _writer.WriteInt(type.MinimumReaderVersion);
            if (type.Name == KnownTypeNames.EventBlockCompressed)
            {
                _writer.WriteInt(KnownTypeNames.EventBlock.Length);
                Encoding.UTF8.GetBytes(KnownTypeNames.EventBlock, _writer);
            }
            else if (type.Name == KnownTypeNames.StackBlockCompressed)
            {
                _writer.WriteInt(KnownTypeNames.StackBlock.Length);
                Encoding.UTF8.GetBytes(KnownTypeNames.StackBlock, _writer);
            }
            else
            {
                _writer.WriteInt(type.Name.Length);
                Encoding.UTF8.GetBytes(type.Name, _writer);
            }
            _writer.WriteByte((byte)Tags.EndObject);
        }

        private void ProcessBlockBody(NettraceBlock block)
        {
            if (block.Type.Name != KnownTypeNames.EventBlockCompressed && block.Type.Name != KnownTypeNames.StackBlockCompressed)
            {
                ProcessBlockBodyUncompressed(block.BlockBody);
                return;
            }

            var padding = GetPadding(block);
            var prefixLength = padding + sizeof(int);
            using var decoder = new BrotliDecoder();

            var source = ArrayPool<byte>.Shared.Rent((int)block.BlockBody.Length);
            block.BlockBody.CopyTo(source);
            var written = 0;
            Memory<byte> memory;
            try
            {
                var length = prefixLength + (int)block.BlockBody.Length;
                memory = _writer!.GetMemory(length);
                while (!BrotliDecoder.TryDecompress(source.AsSpan(0, (int)block.BlockBody.Length), memory.Slice(prefixLength).Span, out written))
                {
                    length = memory.Length * 2;
                    memory = _writer!.GetMemory(length);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(source);
            }

            // Write size
            BitConverter.TryWriteBytes(memory.Span, written);
            // clear padding bits
            memory.Slice(sizeof(int), padding).Span.Clear();
            _writer.Advance(written + prefixLength);
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
