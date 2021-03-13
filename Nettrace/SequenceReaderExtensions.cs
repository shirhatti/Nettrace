// This code was copied from https://github.com/dotnet/runtime/issues/29318#issuecomment-484987895

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace System.Buffers
{
    public static class SequenceReaderExtensions
    {
        internal static unsafe bool TryPeek<T>(ref this SequenceReader<byte> reader, out T value) where T : unmanaged
        {
            ReadOnlySpan<byte> span = reader.UnreadSpan;
            if (span.Length < sizeof(T))
                return TryPeekMultisegment(ref reader, out value);

            value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(span));
            return true;
        }

        private static unsafe bool TryPeekMultisegment<T>(ref SequenceReader<byte> reader, out T value) where T : unmanaged
        {
            Debug.Assert(reader.UnreadSpan.Length < sizeof(T));

            // Not enough data in the current segment, try to peek for the data we need.
            T buffer = default;
            Span<byte> tempSpan = new Span<byte>(&buffer, sizeof(T));

            if (!reader.TryCopyTo(tempSpan))
            {
                value = default;
                return false;
            }

            value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(tempSpan));
            return true;
        }

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
            value = default;
            Debug.Assert(reader.UnreadSpan.Length < length);
            // TODO sanitize input. Large length could cause stack overflow
            Span<byte> buffer = stackalloc byte[length];
            if (!reader.TryCopyTo(buffer))
            {
                return false;
            }
            value = Encoding.UTF8.GetString(buffer);
            reader.Advance(length);
            return true;
        }
    }
}
