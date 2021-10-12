﻿using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Text;

namespace Nettrace
{
    internal class CopyBlockProcessor : IBlockProcessor, IDisposable
    {
        private readonly string _filePath;
        private bool _initialized;
        private Stream? _stream;
        private PipeWriter? _writer;
        private long _bytesWritten;
        public CopyBlockProcessor(string filePath)
        {
            _filePath = filePath;
        }
        [MemberNotNull(nameof(_writer))]
        private void Initialize()
        {
            _stream = File.OpenWrite(_filePath);
            _writer = PipeWriter.Create(_stream);
            // Write Preamble
            Encoding.UTF8.GetBytes("Nettrace", _writer);
            // Write StreamHeader
            _writer.Write(BitConverter.GetBytes(20));
            Encoding.UTF8.GetBytes("!FastSerialization.1", _writer);
            _initialized = true;
        }

        public void ProcessBlock(NettraceBlock block)
        {
            if (!_initialized)
            {
                Initialize();
            }
            Debug.Assert(_writer is not null);
            // Write opening tag
            _writer.WriteByte((byte)Tags.BeginPrivateObject);

            // TODO: Write block header
            ProcessBlockHeader(block.Type);

            if (block.Type.Name != KnownTypeNames.Trace)
            {
                // Write block size
                _writer.Write(BitConverter.GetBytes(block.Size));

                // Write padding
                Span<byte> span = stackalloc byte[(int)block.AlignmentPadding];
                _writer.Write(span);
            }

            ProcessBlockBody(block.BlockBody);
            
            // Write closing tag
            _writer.WriteByte((byte)Tags.EndObject);
        }

        private void ProcessBlockHeader(NettraceType type)
        {
            /*
            [open tag]
                [type version]
                [minReader Version]
                [length of block name]
                [block name]
            [close tag]
            */
            Debug.Assert(_writer is not null);
            _writer.WriteByte((byte)Tags.BeginPrivateObject);
            _writer.WriteByte((byte)Tags.NullReference);
            _writer.Write(BitConverter.GetBytes(type.Version));
            _writer.Write(BitConverter.GetBytes(type.MinimumReaderVersion));
            _writer.Write(BitConverter.GetBytes(type.Name.Length));
            Encoding.UTF8.GetBytes(type.Name, _writer);
            _writer.WriteByte((byte)Tags.EndObject);
        }

        private void ProcessBlockBody(ReadOnlySequence<byte> blockSequence)
        {
            foreach (var memory in blockSequence)
            {
                _writer!.Write(memory.Span);
            }
        }

        public void Dispose()
        {
            if (_writer is not null)
            {
                // Write null reference tag
                _writer.WriteByte((byte)Tags.NullReference);

                _writer.Complete();
            }
            _stream?.Dispose();
        }
    }
}
