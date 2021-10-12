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
        public void ProcessBlock(ReadOnlySequence<byte> blockSequence, string blockName)
        {
            if (!_initialized)
            {
                Initialize();
            }
            // Write opening tag
            _writer!.Write(BitConverter.GetBytes((byte)Tags.BeginPrivateObject));

            ProcessBlockInternal(blockSequence);
        }

        private void ProcessBlockInternal(ReadOnlySequence<byte> blockSequence)
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
