using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nettrace
{
    static internal class BlockHelpers
    {
        static bool compress = false;
        static bool decompress = false;

        public static void WriteInitialFileContext(TrackingPipeWriter writer)
        {
            // Write Preamble
            Encoding.UTF8.GetBytes("Nettrace", writer);
            // Write StreamHeader
            writer.WriteInt(20);
            Encoding.UTF8.GetBytes("!FastSerialization.1", writer);
        }

        public static int GetPadding(TrackingPipeWriter writer, NettraceBlock block)
        {
            // Get padding size
            var offset = (writer.WrittenCount + sizeof(int)) % 4;
            var padding = (int)((4 - offset) % 4);
            return padding;
        }

        private static void WriteBlockBody(TrackingPipeWriter writer, ReadOnlySequence<byte> blockSequence)
        {
            foreach (var memory in blockSequence)
            {
                writer.Write(memory.Span);
            }
        }

        private static void WriteBlockBodyPreamble(TrackingPipeWriter writer, NettraceBlock block)
        {
            if (block.Type.Name == KnownTypeNames.Trace)
            {
                return;
            }

            // padding should run before writing block size
            var padding = GetPadding(writer, block);

            // Write block size
            writer.WriteInt(block.Size);
            // Write padding
            Span<byte> span = stackalloc byte[padding];
            writer.Write(span);
        }

        private static void WriteBlockHeader(TrackingPipeWriter writer, NettraceType type)
        {
            if (compress)
            {
                CompressBlockHelper.WriteBlockHeader(writer, type);
            }
            else if (decompress)
            {
                DecompressBlockHelper.WriteBlockHeader(writer, type);
            }
            else
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
                writer.WriteInt(type.Name.Length);
                Encoding.UTF8.GetBytes(type.Name, writer);
                writer.WriteByte((byte)Tags.EndObject);
            }
        }

        public static ValueTask<FlushResult> ProcessBlock(TrackingPipeWriter writer, NettraceBlock block, CancellationToken token)
        {
            // Write opening tag
            writer.WriteByte((byte)Tags.BeginPrivateObject);

            WriteBlockHeader(writer, block.Type);

            var written = compress && CompressBlockHelper.WriteBlockAndPreamble(writer, block);
            if (!written)
            {
                written = decompress && DecompressBlockHelper.WriteBlockAndPreamble(writer, block);
            }
            if (!written)
            {
                WriteBlockBodyPreamble(writer, block);

                WriteBlockBody(writer, block.BlockBody);
            }

            // Write closing tag
            writer.WriteByte((byte)Tags.EndObject);

            return writer.FlushAsync(token);
        }
    }
}
