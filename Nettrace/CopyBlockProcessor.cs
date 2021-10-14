using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

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

        private void Initialize()
        {
            _stream = File.OpenWrite(_filePath);
            _writer = new TrackingPipeWriter(PipeWriter.Create(_stream));
            BlockHelpers.WriteInitialFileContext(_writer);
            _initialized = true;
        }

        public ValueTask ProcessBlockAsync(NettraceBlock block, CancellationToken token = default)
        {
            if (!_initialized)
            {
                Initialize();
            }
            Debug.Assert(_writer is not null);
            return new (BlockHelpers.ProcessBlock(_writer, block, token).AsTask());
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
