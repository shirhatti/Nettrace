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

        public static bool TryReadVarInt32(ref this SequenceReader<byte> reader, out int result)
        {
            result = default;
            var shift = 0;
            while (true)
            {
                _ = reader.TryRead(out var value);
                result |= (value & 0x7f) << (7 * shift);
                if ((value & 0x80) == 0)
                {
                    break;
                }
                shift ++;
                if (shift*7 >= 32)
                {
                    reader.Rewind(shift);
                    return false;
                }
            }
            return true;
        }

        public static bool TryReadVarInt64(ref this SequenceReader<byte> reader, out long result)
        {
            result = default;
            var shift = 0;
            while (true)
            {
                _ = reader.TryRead(out var value);
                result |= (long)(value & 0x7f) << (7 * shift);
                if ((value & 0x80) == 0)
                {
                    break;
                }
                shift++;
                if (shift * 7 >= 64)
                {
                    reader.Rewind(shift);
                    return false;
                }
            }
            return true;
        }
        public static bool TryReadNullTerminatedUnicodeString(
            ref this SequenceReader<byte> reader, out string value)
        {
            value = default;
            int length = 0;
            int remaining;
            while (length < 1024)
            {
                var lengthInSegment = MemoryMarshal.Cast<byte, char>(reader.UnreadSpan).IndexOf('\0');
                if (lengthInSegment != -1)
                {
                    length += lengthInSegment;
                    break;
                }
                remaining = reader.CurrentSpan.Length - reader.CurrentSpanIndex;
                reader.Advance(remaining);
                length += remaining;
            }
            // We dont't find the null terminator, rewind and fail
            if (length >= 1024)
            {
                reader.Rewind(length);
                return false;
            }

            return TryReadString(ref reader, 2 * length, Encoding.Unicode, out value);
        }
        

        public static bool TryReadLengthPrefixedUtf8String(
            ref this SequenceReader<byte> reader, out string value)
        {
            value = default;
            if(!reader.TryReadLittleEndian(out int length))
            {
                return false;
            }
            return TryReadString(ref reader, length, Encoding.UTF8, out value);
        }

        // Size depends on the encoding
        // For UTF-8, size = length
        // For Unicode, size = 2 * length
        public static bool TryReadString(
            ref this SequenceReader<byte> reader, int size, Encoding encoding, out string value)
        {
            ReadOnlySpan<byte> span = reader.UnreadSpan;
            if (span.Length < size)
            {
                return TryReadMultisegmentString(ref reader, size, encoding, out value);
            }
                

            ReadOnlySpan<byte> slice = span.Slice(0, size);
            value = encoding.GetString(slice);
            reader.Advance(size);
            return true;
        }

        private static unsafe bool TryReadMultisegmentString(
            ref SequenceReader<byte> reader, int size, Encoding encoding, out string value)
        {
            value = default;
            Debug.Assert(reader.UnreadSpan.Length < size);
            // TODO sanitize input. Large length could cause stack overflow
            Span<byte> buffer = stackalloc byte[size];
            if (!reader.TryCopyTo(buffer))
            {
                return false;
            }
            value = encoding.GetString(buffer);
            reader.Advance(size);
            return true;
        }
    }
}
