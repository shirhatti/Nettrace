using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Text;

namespace Nettrace
{
    public class CopyBlockProcessor : IBlockProcessor, IDisposable
    {
        private readonly string _filePath;
        private bool _initialized;
        private Stream? _stream;
        private TrackingPipeWriter? _writer;

        public CopyBlockProcessor(string filePath)
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
            _writer.FlushAsync().GetAwaiter().GetResult();
        }

        private void WritePadding(NettraceBlock block)
        {
            Debug.Assert(_writer is not null);

            // Write padding
            Debug.Assert(block.AlignmentPadding < 5);
            var offset = _writer.WrittenCount % 4;
            var padding = (4 - offset) % 4;
            // Remove when doing file rolling, just here for sanity for now
            Debug.Assert(block.AlignmentPadding == padding);
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
