using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nettrace
{
    internal class TrackingPipeWriter : PipeWriter
    {
        internal readonly PipeWriter _writer;
        internal long _written;

        public long WrittenCount => _written;

        public TrackingPipeWriter(PipeWriter writer)
        {
            _writer = writer;
        }

        public override void Advance(int bytes)
        {
            _written += bytes;
            _writer.Advance(bytes);
        }

        public override void CancelPendingFlush()
        {
            _writer.CancelPendingFlush();
        }

        public override void Complete(Exception? exception = null)
        {
            _writer.Complete(exception);
        }

        public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
        {
            return _writer.FlushAsync(cancellationToken);
        }

        public override Memory<byte> GetMemory(int sizeHint = 0)
        {
            return _writer.GetMemory(sizeHint);
        }

        public override Span<byte> GetSpan(int sizeHint = 0)
        {
            return _writer.GetSpan(sizeHint);
        }
    }
}
