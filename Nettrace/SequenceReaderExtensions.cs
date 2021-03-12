// This code was copied from https://github.com/dotnet/runtime/issues/29318#issuecomment-484987895

using System.Diagnostics;
using System.Text;

namespace System.Buffers
{
    public static class SequenceReaderExtensions
    {
        public static bool TryReadLengthPrefixedUtf8String(
    ref this SequenceReader<byte> reader, out string value)
        {
            value = default;
            if(!reader.TryReadLittleEndian(out int length))
            {
                return false;
            }
            return TryReadUtf8String(ref reader, length, out value);
        }
        public static bool TryReadUtf8String(
    ref this SequenceReader<byte> reader, int length, out string value)
        {
            ReadOnlySpan<byte> span = reader.UnreadSpan;
            if (span.Length < length)
            {
                return TryReadMultisegmentUtf8String(ref reader, length, out value);
            }
                

            ReadOnlySpan<byte> slice = span.Slice(0, length);
            value = Encoding.UTF8.GetString(slice);
            reader.Advance(length);
            return true;
        }

        private static unsafe bool TryReadMultisegmentUtf8String(
            ref SequenceReader<byte> reader, int length, out string value)
        {
            Debug.Assert(reader.UnreadSpan.Length < length);
            // TODO sanitize input. Large length could cause stack overflow
            Span<byte> buffer = stackalloc byte[length];
            reader.TryCopyTo(buffer);
            value = Encoding.UTF8.GetString(buffer);
            reader.Advance(length);
            return true;
        }
    }
}
