using System;
using System.Runtime.InteropServices;

namespace Nettrace
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct EventBlobHeader
    {
        public int EventSize;
        public int MetadataId;
        public int SequenceId;
        public long ThreadId;
        public long CaptureThreadId;
        public int ProcessorNumber;
        public int StackId;
        public long TimeStamp;
        public Guid ActivityId;
        public Guid RelatedActivityId;
        public int PayloadSize;

        public bool IsMetadata() => MetadataId == 0; // 0 means that it's a metadata Id
    }
}
