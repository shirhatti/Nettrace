using System;
using System.Buffers;
using System.IO.Compression;
using System.Text;

namespace Nettrace
{
    static internal class DecompressBlockHelper
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
            if (type.Name == KnownTypeNames.EventBlockCompressed)
            {
                writer.WriteInt(KnownTypeNames.EventBlock.Length);
                Encoding.UTF8.GetBytes(KnownTypeNames.EventBlock, writer);
            }
            else if (type.Name == KnownTypeNames.StackBlockCompressed)
            {
                writer.WriteInt(KnownTypeNames.StackBlock.Length);
                Encoding.UTF8.GetBytes(KnownTypeNames.StackBlock, writer);
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
            if (block.Type.Name != KnownTypeNames.EventBlockCompressed && block.Type.Name != KnownTypeNames.StackBlockCompressed)
            {
                return false;
            }

            var padding = BlockHelpers.GetPadding(writer, block);
            var prefixLength = padding + sizeof(int);
            using var decoder = new BrotliDecoder();

            var source = ArrayPool<byte>.Shared.Rent((int)block.BlockBody.Length);
            block.BlockBody.CopyTo(source);
            var written = 0;
            Memory<byte> memory;
            try
            {
                var length = prefixLength + (int)block.BlockBody.Length;
                memory = writer.GetMemory(length);
                while (!BrotliDecoder.TryDecompress(source.AsSpan(0, (int)block.BlockBody.Length), memory.Slice(prefixLength).Span, out written))
                {
                    length = memory.Length * 2;
                    memory = writer.GetMemory(length);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(source);
            }

            // Write size
            BitConverter.TryWriteBytes(memory.Span, written);
            // clear padding bits
            memory.Slice(sizeof(int), padding).Span.Clear();
            writer.Advance(written + prefixLength);

            return true;
        }
    }
}
