using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Nettrace
{
    internal class MetadataProcessor : IThreadPoolWorkItem
    {
        private readonly EventBlockHeader _blockHeader;
        private long _offset;
        private readonly byte[] _buffer;
        private readonly ILogger _logger;

        public MetadataProcessor(EventBlockHeader blockHeader, long offset, byte[] buffer, ILogger logger)
        {
            _blockHeader = blockHeader;
            _offset = offset;
            _buffer = buffer;
            _logger = logger;
        }

        public void Execute()
        {
            _ = ExecuteAsync();
        }

        internal Task ExecuteAsync(CancellationToken token = default)
        {
            _logger.LogInformation("Processing metadata block with events between {minTimestamp} and {maxTimestamp}",
                                   _blockHeader.MinTimestamp,
                                   _blockHeader.MaxTimestamp);

            _logger.LogInformation("Processing metadata block with 0x{flags}", _blockHeader.Flags.ToString("X4"));

            var sequence = new ReadOnlySequence<byte>(_buffer);
            var sequenceReader = new SequenceReader<byte>(sequence);

            // We don't support uncompressed event blobs
            Debug.Assert((_blockHeader.Flags & 1) != 0);

            var blobHeader = new EventBlobHeader();

            var eventBlobHeaderStart = sequenceReader.Consumed;
            // Begin parsing EventBlobHeader
            _ = sequenceReader.TryRead(out var flags);

            if ((flags & (byte)CompressedHeaderFlags.MetadataId) != 0)
            {
                sequenceReader.TryReadVarInt32(out blobHeader.MetadataId);
            }
            if ((flags & (byte)CompressedHeaderFlags.CaptureThreadAndSequence) != 0)
            {
                var oldSequenceId = blobHeader.SequenceId;
                sequenceReader.TryReadVarInt32(out blobHeader.SequenceId);
                blobHeader.SequenceId += (oldSequenceId + 1);

                sequenceReader.TryReadVarInt64(out blobHeader.ThreadId);
                sequenceReader.TryReadVarInt32(out blobHeader.ProcessorNumber);
            }
            else
            {
                if (blobHeader.MetadataId != 0)
                {
                    blobHeader.SequenceId++;
                }
            }

            if ((flags & (byte)CompressedHeaderFlags.ThreadId) != 0)
            {
                sequenceReader.TryReadVarInt64(out blobHeader.ThreadId);
            }

            if ((flags & (byte)CompressedHeaderFlags.StackId) != 0)
            {
                sequenceReader.TryReadVarInt32(out blobHeader.StackId);
            }
            sequenceReader.TryReadVarInt64(out var timeStampDelta);
            blobHeader.TimeStamp += timeStampDelta;

            if ((flags & (byte)CompressedHeaderFlags.ActivityId) != 0)
            {
                // TODO
                sequenceReader.Advance(128);
            }

            if ((flags & (byte)CompressedHeaderFlags.RelatedActivityId) != 0)
            {
                // TODO
                sequenceReader.Advance(128);
            }

            if ((flags & (byte)CompressedHeaderFlags.DataLength) != 0)
            {
                sequenceReader.TryReadVarInt32(out blobHeader.PayloadSize);
            }

            var headerSize = sequenceReader.Consumed - eventBlobHeaderStart;
            var remainingSize = blobHeader.PayloadSize - headerSize;
            ArrayPool<byte>.Shared.Return(_buffer);

            return Task.CompletedTask;
        }
    }
}
