using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Nettrace
{
    public class Program
    {
        private static ParserContext _context;
        public static async Task Main(string[] args)
        {
            using var stream = File.OpenRead("trace.nettrace");
            _context = new ParserContext()
            {
                BytesConsumed = 0,
                State = State.Preamble
            };

            var reader = PipeReader.Create(stream);
            while (true)
            {
                ReadResult result = await reader.ReadAsync();

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
        private static bool TryParse(ReadResult readResult, out SequencePosition position)
        {
            position = default;
            var buffer = readResult.Buffer;
            var sequenceReader = new SequenceReader<byte>(buffer);
            var result = _context.State switch
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
                _context.BytesConsumed += sequenceReader.Consumed;
            }
            return result;
        }

        private static bool TryReadPreamble(ref SequenceReader<byte> sequenceReader)
        {
            if (!sequenceReader.TryReadUtf8String(8, out var name))
            {
                return false;
            }
            Debug.Assert(name == "Nettrace");
            _context.State = State.StreamHeader;
            return true;
        }

        private static bool TryReadStreamHeader(ref SequenceReader<byte> sequenceReader)
        {
            if (!sequenceReader.TryReadLengthPrefixedUtf8String(out var name))
            {
                return false;
            }
            Debug.Assert(name == "!FastSerialization.1");
            _context.State = State.Object;
            return true;
        }

        private static bool TryReadObject(ref SequenceReader<byte> sequenceReader)
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
                _context.State = State.Completed;
                return true;
            }

            Debug.Assert(tag == Tags.BeginPrivateObject);
            if (!TryReadType(ref sequenceReader, out var type))
            {
                return false;
            }
            // Console.WriteLine(type.Name);
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
                return TryReadUnknownBlock(ref sequenceReader);
            }
            else if (Equals(type.Name, KnownTypeNames.EventBlock))
            {
                return TryReadUnknownBlock(ref sequenceReader);
            }
            else if (Equals(type.Name, KnownTypeNames.SPBlock))
            {
                return TryReadUnknownBlock(ref sequenceReader);
            }
            return true;
        }

        private static bool TryReadUnknownBlock(ref SequenceReader<byte> sequenceReader)
        {
            if (!sequenceReader.TryReadLittleEndian(out int BlockSize))
            {
                return false;
            }
            var offset = (_context.BytesConsumed + sequenceReader.Consumed) % 4;
            if (offset != 0)
            {
                //Perform 4-byte alignment
                sequenceReader.Advance(4 - offset);
            }


            // TODO: Parse the StackBlock
            if (BlockSize > sequenceReader.Remaining)
            {
                return false;
            }
            sequenceReader.Advance(BlockSize);

            if (!sequenceReader.TryRead(out var tagValue))
            {
                return false;
            }
            Debug.Assert((Tags)tagValue == Tags.EndObject);
            return true;
        }

        private static bool TryReadMetadataBlock(ref SequenceReader<byte> sequenceReader)
        {
            if (!sequenceReader.TryReadLittleEndian(out int BlockSize))
            {
                return false;
            }
            var offset = (_context.BytesConsumed + sequenceReader.Consumed) % 4;
            if (offset != 0)
            {
                //Perform 4-byte alignment
                sequenceReader.Advance(4 - offset);
            }
            if (!TryReadEventBlockHeader(ref sequenceReader, out var EventBlockHeader))
            {
                return false;
            }
            var remainingData = BlockSize - EventBlockHeader.HeaderSize;

            // TODO: Parse the event blob
            if (remainingData > sequenceReader.Remaining)
            {
                return false;
            }
            sequenceReader.Advance(remainingData);

            if (!sequenceReader.TryRead(out var tagValue))
            {
                return false;
            }
            Debug.Assert((Tags)tagValue == Tags.EndObject);
            return true;
        }

        private static bool TryReadEventBlockHeader(ref SequenceReader<byte> sequenceReader, out EventBlockHeader blockHeader)
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

        private static bool TryReadTraceObject(ref SequenceReader<byte> sequenceReader)
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

        private static bool TryReadType(ref SequenceReader<byte> sequenceReader, out NettraceType nettraceType)
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

    }
}
