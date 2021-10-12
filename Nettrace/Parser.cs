﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nettrace
{
    public class Parser : IDisposable
    {
        private ILogger _logger;
        private int? _readAtLeast = null;
        private readonly IBlockProcessor _blockProcessor = new CopyBlockProcessor(@"C:\temp\temp.dat");
        public long BytesConsumed { get; private set; } = 0;
        public State State { get; private set; } = State.Preamble;
        public Parser() : this(NullLogger.Instance) { }
        public Parser(ILogger logger) => _logger = logger;
        public async Task ProcessAsync(Stream stream, CancellationToken token = default)
        {
            var reader = PipeReader.Create(stream);
            while (true)
            {
                ReadResult result;
                if (_readAtLeast.HasValue)
                {
                    result = await reader.ReadAtLeastAsync(_readAtLeast.Value, token);
                    _readAtLeast = null;
                }
                else
                {
                    result = await reader.ReadAsync(token);
                }
                _logger.LogTrace("Buffer length: {length}", result.Buffer.Length);
                // In the event that no message is parsed successfully, mark consumed
                // as nothing and examined as the entire buffer.
                SequencePosition consumed = result.Buffer.Start;
                SequencePosition examined = result.Buffer.End;

                var parseResult = TryParse(result, out var position);

                // Console.WriteLine($"\t{parseResult}");
                if (parseResult)
                {
                    examined = position;
                    consumed = position;
                }

                if (result.IsCompleted)
                {
                    if (result.Buffer.Length > 0)
                    {
                        // The message is incomplete and there's no more data to process.
                        throw new InvalidDataException("Incomplete message.");
                    }

                    break;
                }
                reader.AdvanceTo(consumed, examined);
            }
        }

        private bool TryParse(ReadResult readResult, out SequencePosition position)
        {
            position = default;
            var buffer = readResult.Buffer;
            var sequenceReader = new SequenceReader<byte>(buffer);
            var result = State switch
            {
                State.Preamble => TryReadPreamble(ref sequenceReader),
                State.StreamHeader => TryReadStreamHeader(ref sequenceReader),
                State.Object => TryReadObject(ref sequenceReader),
                State.Completed => true,
                _ => throw new NotImplementedException(),
            };
            position = sequenceReader.Position;
            if (result)
            {
                BytesConsumed += sequenceReader.Consumed;
            }
            return result;
        }

        private bool TryReadPreamble(ref SequenceReader<byte> sequenceReader)
        {
            if (!sequenceReader.TryReadString(8, Encoding.UTF8, out var name))
            {
                return false;
            }
            Debug.Assert(name == "Nettrace");
            State = State.StreamHeader;
            return true;
        }

        private bool TryReadStreamHeader(ref SequenceReader<byte> sequenceReader)
        {
            if (!sequenceReader.TryReadLengthPrefixedUtf8String(out var name))
            {
                return false;
            }
            Debug.Assert(name == "!FastSerialization.1");
            State = State.Object;
            return true;
        }

        private bool TryReadObject(ref SequenceReader<byte> sequenceReader)
        {
            byte tagValue;
            Tags tag;

            if (!sequenceReader.TryRead(out tagValue))
            {
                return false;
            }
            tag = (Tags)tagValue;

            if (tag == Tags.NullReference)
            {
                State = State.Completed;
                return true;
            }

            Debug.Assert(tag == Tags.BeginPrivateObject);
            if (!TryReadType(ref sequenceReader, out var type))
            {
                return false;
            }
            _logger.LogInformation("Found block of type: {type}", type.Name);
            if (Equals(type.Name, KnownTypeNames.Trace))
            {
                return TryReadTraceObject(ref sequenceReader);
            }
            else if (Equals(type.Name, KnownTypeNames.MetadataBlock))
            {
                return TryReadMetadataBlock(ref sequenceReader);
            }
            else if (Equals(type.Name, KnownTypeNames.StackBlock))
            {
                return TryReadUnknownBlock(ref sequenceReader, KnownTypeNames.StackBlock);
            }
            else if (Equals(type.Name, KnownTypeNames.EventBlock))
            {
                return TryReadUnknownBlock(ref sequenceReader, KnownTypeNames.EventBlock);
            }
            else if (Equals(type.Name, KnownTypeNames.SPBlock))
            {
                return TryReadUnknownBlock(ref sequenceReader, KnownTypeNames.EventBlock);
            }
            // Unreachable code
            Debug.Assert(false);
            return true;
        }

        private bool TryReadUnknownBlock(ref SequenceReader<byte> sequenceReader, string blockName)
        {
            if (!sequenceReader.TryReadLittleEndian(out int BlockSize))
            {
                return false;
            }
            PerformFourByteAllignment(ref sequenceReader);


            // TODO: Parse the block
            if (BlockSize > sequenceReader.Remaining)
            {
                _readAtLeast = BlockSize+1;
                return false;
            }
            var blockSequence = sequenceReader.UnreadSequence.Slice(0, BlockSize);
            _blockProcessor.ProcessBlock(blockSequence, blockName);
            //using var block = BlockWrapper.Create(ref sequenceReader, BlockSize);
            sequenceReader.Advance(BlockSize);
            if (!sequenceReader.TryRead(out var tagValue))
            {
                return false;
            }
            Debug.Assert((Tags)tagValue == Tags.EndObject);
            return true;
        }

        private void PerformFourByteAllignment(ref SequenceReader<byte> sequenceReader)
        {
            var offset = (BytesConsumed + sequenceReader.Consumed) % 4;
            if (offset != 0)
            {
                sequenceReader.Advance(4 - offset);
            }
        }

        private bool TryReadMetadataBlock(ref SequenceReader<byte> sequenceReader)
        {
            if (!sequenceReader.TryReadLittleEndian(out int BlockSize))
            {
                return false;
            }

            PerformFourByteAllignment(ref sequenceReader);

            if (BlockSize > sequenceReader.Remaining)
            {
                return false;
            }

            //using var block = BlockWrapper.Create(ref sequenceReader, BlockSize);

            if (!TryReadEventBlockHeader(ref sequenceReader, out var EventBlockHeader))
            {
                return false;
            }
            var remainingData = BlockSize - EventBlockHeader.HeaderSize;

            if (remainingData + 1 <= sequenceReader.Remaining)
            {
                _logger.LogInformation("Processing metadata block with events between {minTimestamp} and {maxTimestamp}",
                       EventBlockHeader.MinTimestamp,
                       EventBlockHeader.MaxTimestamp);

                _logger.LogInformation("Processing metadata block with 0x{flags}", EventBlockHeader.Flags.ToString("X4"));

                sequenceReader.Advance(remainingData);
                //var eventBlobHeader = new EventBlobHeader();
                //while (remainingData > 0)
                //{
                //    var blobStartPosition = sequenceReader.Consumed;

                //    if (!TryReadEventBlobHeader(ref sequenceReader, EventBlockHeader, ref eventBlobHeader))
                //    {
                //        return false;
                //    }
                //    Debug.Assert(eventBlobHeader.IsMetadata());
                //    if (!TryReadMetadataEventData(ref sequenceReader, eventBlobHeader, out var metadataEvent))
                //    {
                //        return false;
                //    }
                //    Debug.Assert(eventBlobHeader.PayloadSize == (sequenceReader.Consumed - blobStartPosition));
                //    if (eventBlobHeader.PayloadSize != (sequenceReader.Consumed - blobStartPosition))
                //    {
                //        sequenceReader.Advance(remainingData - sequenceReader.Consumed + blobStartPosition);
                //        break;
                //    }
                //    remainingData -= eventBlobHeader.PayloadSize;
                //}
            }
            else
            {
                return false;
            }

            if (!sequenceReader.TryRead(out var tagValue))
            {
                return false;
            }
            Debug.Assert((Tags)tagValue == Tags.EndObject);
            return true;
        }

        private bool TryReadMetadataEventData(ref SequenceReader<byte> sequenceReader,
                                    EventBlobHeader eventBlobHeader,
                                    out MetadataEvent metadataEvent)
        {
            metadataEvent = new MetadataEvent();
            if (!sequenceReader.TryReadLittleEndian(out metadataEvent.MetaDataId))
            {
                return false;
            }
            if (!sequenceReader.TryReadNullTerminatedUnicodeString(out metadataEvent.ProviderName))
            {
                return false;
            }
            if (!sequenceReader.TryReadLittleEndian(out metadataEvent.EventId))
            {
                return false;
            }
            if (!sequenceReader.TryReadNullTerminatedUnicodeString(out metadataEvent.EventName))
            {
                return false;
            }
            if (!sequenceReader.TryReadLittleEndian(out metadataEvent.Keywords))
            {
                return false;
            }
            if (!sequenceReader.TryReadLittleEndian(out metadataEvent.Level))
            {
                return false;
            }
            return true;
        }

        private bool TryReadEventBlobHeader(ref SequenceReader<byte> sequenceReader,
                                            EventBlockHeader eventBlockHeader,
                                            ref EventBlobHeader blobHeader)
        {
            Debug.Assert((eventBlockHeader.Flags & 1) != 0);

            if (!sequenceReader.TryRead(out var flags))
            {
                return false;
            }

            if ((flags & (byte)CompressedHeaderFlags.MetadataId) != 0)
            {
                if (!sequenceReader.TryReadVarInt32(out blobHeader.MetadataId))
                {
                    return false;
                }
            }
            if ((flags & (byte)CompressedHeaderFlags.CaptureThreadAndSequence) != 0)
            {
                if (!sequenceReader.TryReadVarInt32(out var sequenceIdDelta))
                {
                    return false;
                }
                blobHeader.SequenceId += sequenceIdDelta + 1;

                if (!sequenceReader.TryReadVarInt64(out blobHeader.ThreadId))
                {
                    return false;
                }
                if (!sequenceReader.TryReadVarInt32(out blobHeader.ProcessorNumber))
                {
                    return false;
                }
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
                if (!sequenceReader.TryReadVarInt64(out blobHeader.CaptureThreadId))
                {
                    return false;
                }
            }

            if ((flags & (byte)CompressedHeaderFlags.StackId) != 0)
            {
                if (!sequenceReader.TryReadVarInt32(out blobHeader.StackId))
                {
                    return false;
                }
            }
            if (!sequenceReader.TryReadVarInt64(out var timeStampDelta))
            {
                return false;
            }
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
                if (!sequenceReader.TryReadVarInt32(out blobHeader.PayloadSize))
                {
                    return false;
                }
            }
            return true;
        }

        private bool TryReadEventBlockHeader(ref SequenceReader<byte> sequenceReader, out EventBlockHeader blockHeader)
        {
            sequenceReader.TryPeek(out short HeaderSize);
            blockHeader = new EventBlockHeader();
            var span = MemoryMarshal.Cast<EventBlockHeader, byte>(MemoryMarshal.CreateSpan(ref blockHeader, 1));
            if (!sequenceReader.TryCopyTo(span))
            {
                return false;
            }
            sequenceReader.Advance(span.Length);
            var optionalReservedSpace = (HeaderSize - span.Length);
            Debug.Assert(optionalReservedSpace >= 0);
            if (optionalReservedSpace > 0)
            {
                sequenceReader.Advance(optionalReservedSpace);
            }
            return true;
        }

        private bool TryReadTraceObject(ref SequenceReader<byte> sequenceReader)
        {
            var trace = new TraceObject();
            var span = MemoryMarshal.Cast<TraceObject, byte>(MemoryMarshal.CreateSpan(ref trace, 1));

            if (!sequenceReader.TryCopyTo(span))
            {
                return false;
            }
            sequenceReader.Advance(span.Length);

            if (!sequenceReader.TryRead(out var tagValue))
            {
                return false;
            }
            Debug.Assert((Tags)tagValue == Tags.EndObject);
            return true;
        }

        private bool TryReadType(ref SequenceReader<byte> sequenceReader, out NettraceType nettraceType)
        {
            nettraceType = default;

            byte tagValue;
            Tags tag;

            if (!sequenceReader.TryRead(out tagValue))
            {
                return false;
            }
            tag = (Tags)tagValue;
            Debug.Assert(tag == Tags.BeginPrivateObject);

            if (!sequenceReader.TryRead(out tagValue))
            {
                return false;
            }
            tag = (Tags)tagValue;
            Debug.Assert(tag == Tags.NullReference);


            if (!sequenceReader.TryReadLittleEndian(out int typeVersion))
            {
                return false;
            }
            if (!sequenceReader.TryReadLittleEndian(out int minReaderVersion))
            {
                return false;
            }
            if (!sequenceReader.TryReadLittleEndian(out int length))
            {
                return false;
            }
            if (!sequenceReader.TryPeek(out byte firstByte))
            {
                return false;
            }
            var typeName = KnownTypeNames.OptimisticMatching(firstByte, length);
            Debug.Assert(typeName != default);
            if (length > sequenceReader.Remaining)
            {
                return false;
            }
            sequenceReader.Advance(length);

            nettraceType = new()
            {
                Name = typeName,
                MinimumReaderVersion = minReaderVersion,
                Version = typeVersion
            };

            if (!sequenceReader.TryRead(out tagValue))
            {
                return false;
            }
            tag = (Tags)tagValue;
            Debug.Assert(tag == Tags.EndObject);

            return true;
        }

        public void Dispose()
        {
            (_blockProcessor as IDisposable)?.Dispose();
        }
    }
}