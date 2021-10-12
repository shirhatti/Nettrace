using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Text;

namespace Nettrace
{
    internal class CopyBlockProcessor : IBlockProcessor, IDisposable
    {
        private readonly string _filePath;
        private bool _initialized;
        private Stream? _stream;
        private PipeWriter? _writer;
        private long _bytesWritten;
        public CopyBlockProcessor(string filePath)
        {
            _filePath = filePath;
        }

        private void Initialize()
        {
            _stream = File.OpenWrite(_filePath);
            _writer = PipeWriter.Create(_stream);
            // Write Preamble
            Encoding.UTF8.GetBytes("Nettrace", _writer);
            // Write StreamHeader
            _writer.Write(BitConverter.GetBytes(20));
            Encoding.UTF8.GetBytes("!FastSerialization.1", _writer);
            _initialized = true;
        }
        public void ProcessBlock(NettraceBlock block)
        {
            if (!_initialized)
            {
                Initialize();
            }
            if (_writer is null)
            {
                throw new InvalidOperationException();
            }
            // Write opening tag
            _writer.Write(BitConverter.GetBytes((byte)Tags.BeginPrivateObject));

            // TODO: Write block header
            //ProcessBlockHeader(block.Type);

            // Write padding
            Span<byte> span = stackalloc byte[(int)block.AlignmentPadding];
            _writer.Write(span);

            ProcessBlockBody(block.BlockBody);
            
            // Write opening tag
            _writer.Write(BitConverter.GetBytes((byte)Tags.EndObject));
        }

        private void ProcessBlockHeader(NettraceType type)
        {
            throw new NotImplementedException();
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
                // Write closing tag
                _writer.Write(BitConverter.GetBytes((byte)Tags.NullReference));

                _writer.Complete();
            }
            _stream?.Dispose();
        }
    }
}
