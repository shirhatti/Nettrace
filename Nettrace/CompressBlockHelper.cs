using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace Nettrace
{
    static internal class CompressBlockHelper
    {
        static public void WriteBlockHeader(TrackingPipeWriter writer, NettraceType type)
        {
            /*
            [open tag]
                [nullreference tag]
                [type version]
                [minReader Version]
                [length of block name]
                [block name]
            [close tag]
            */
            writer.WriteByte((byte)Tags.BeginPrivateObject);
            writer.WriteByte((byte)Tags.NullReference);
            writer.WriteInt(type.Version);
            writer.WriteInt(type.MinimumReaderVersion);
            if (type.Name == KnownTypeNames.EventBlock)
            {
                writer.WriteInt(KnownTypeNames.EventBlockCompressed.Length);
                Encoding.UTF8.GetBytes(KnownTypeNames.EventBlockCompressed, writer);
            }
            else if (type.Name == KnownTypeNames.StackBlock)
            {
                writer.WriteInt(KnownTypeNames.StackBlockCompressed.Length);
                Encoding.UTF8.GetBytes(KnownTypeNames.StackBlockCompressed, writer);
            }
            else
            {
                writer.WriteInt(type.Name.Length);
                Encoding.UTF8.GetBytes(type.Name, writer);
            }
            writer.WriteByte((byte)Tags.EndObject);
        }

        public static bool WriteBlockAndPreamble(TrackingPipeWriter writer, NettraceBlock block)
        {
            if (block.Type.Name != KnownTypeNames.EventBlock && block.Type.Name != KnownTypeNames.StackBlock)
            {
                return false;
            }

            var blockSequence = block.BlockBody;
            var maxLength = BrotliEncoder.GetMaxCompressedLength((int)blockSequence.Length);
            var padding = BlockHelpers.GetPadding(writer!, block);
            var prefixLength = padding + sizeof(int);
            var memory = writer!.GetMemory(maxLength + prefixLength);
            // clear padding bits
            memory.Slice(0, prefixLength).Span.Clear();
            using var encoder = new BrotliEncoder(quality: 9, window: 10);

            var slicedMemory = memory.Slice(prefixLength);
            var totalWritten = 0;
            OperationStatus status;
            foreach (var sequence in blockSequence)
            {
                status = encoder.Compress(sequence.Span, slicedMemory.Span, out var consumed, out var written, false);
                Debug.Assert(consumed == sequence.Span.Length);
                Debug.Assert(status == OperationStatus.Done);
                slicedMemory = slicedMemory.Slice(written);
                totalWritten += written;
            }
            status = encoder.Compress(ReadOnlySpan<byte>.Empty, slicedMemory.Span, out var _, out var written2, true);
            Debug.Assert(status == OperationStatus.Done);
            totalWritten += written2;

            // Write size
            BitConverter.TryWriteBytes(memory.Span, totalWritten);
            writer.Advance(totalWritten + prefixLength);

            return true;
        }
    }
}
