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
        private static State _state = State.Preamble;
        static async Task Main(string[] args)
        {
            using var stream = File.OpenRead("trace.nettrace");
            var reader = PipeReader.Create(stream);
            while (true)
            {
                ReadResult result = await reader.ReadAsync();

                var bytesConsumed = Parse(result);
                reader.AdvanceTo(result.Buffer.Slice(0, bytesConsumed).End);

                if (result.IsCompleted)
                {
                    break;
                }
            }
        }
        private static long Parse(ReadResult readResult)
        {
            return _state switch
            {
                State.Preamble => ReadPreamble(readResult),
                State.StreamHeader => ReadStreamHeader(readResult),
                State.Object => ReadObject(readResult),
                _ => throw new NotImplementedException(),
            };
        }

        private static long ReadPreamble(ReadResult readResult)
        {
            var buffer = readResult.Buffer;
            var sequenceReader = new SequenceReader<byte>(buffer);
            if (!sequenceReader.TryReadUtf8String(8, out var name))
            {
                return 0;
            }
            Debug.Assert(name == "Nettrace");
            _state = State.StreamHeader;
            return sequenceReader.Consumed;
        }

        private static long ReadStreamHeader(ReadResult readResult)
        {
            var buffer = readResult.Buffer;
            var sequenceReader = new SequenceReader<byte>(buffer);
            if (!sequenceReader.TryReadLengthPrefixedUtf8String(out var name))
            {
                return 0;
            }
            Debug.Assert(name == "!FastSerialization.1");
            _state = State.Object;
            return sequenceReader.Consumed;
        }

        private static long ReadObject(ReadResult readResult)
        {
            var buffer = readResult.Buffer;
            var sequenceReader = new SequenceReader<byte>(buffer);

            byte tagValue;
            Tags tag;

            sequenceReader.TryRead(out tagValue);
            tag = (Tags)tagValue;
            Debug.Assert(tag == Tags.BeginPrivateObject);

            if (!TryReadType(ref sequenceReader, out var type))
            {
                return 0;
            }

            var trace = new EventPipeTraceInformation();

            unsafe
            {  
                var span = new Span<byte>(&trace, Marshal.SizeOf(trace));
                sequenceReader.TryCopyTo(span);
            }
            
            sequenceReader.TryRead(out tagValue);
            tag = (Tags)tagValue;
            Debug.Assert(tag == Tags.EndObject);

            return sequenceReader.Consumed;

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
            // Discard mininumReaderVersion
            if (!sequenceReader.TryReadLittleEndian(out int _))
            {
                return false;
            }
            if (!sequenceReader.TryReadLengthPrefixedUtf8String(out var typeName))
            {
                return false;
            }
            nettraceType = new()
            {
                Name = typeName,
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
